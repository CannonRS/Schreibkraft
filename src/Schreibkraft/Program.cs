using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Schreibkraft.Core;
using Schreibkraft.Infrastructure;
using Schreibkraft.Infrastructure.Logging;

namespace Schreibkraft;

public sealed partial class App : Microsoft.UI.Xaml.Application
{
    private ServiceProvider? _services;
    private MainWindow? _window;
    private TrayIconController? _tray;
    private IHotkeyService? _hotkeys;
    private SpeechPipeline? _pipeline;
    private IAudioRecorder? _recorder;
    private ITrayStatusService? _status;
    private IFeedbackSoundService? _sounds;
    private IAppProfile? _profile;
    private IClipboardSourceCapture? _clipboardCapture;
    private string? _capturedSourceText;
    private string? _activeRecordingAssistantId;
    private string? _pendingRecordingAssistantId;
    private CancellationTokenSource? _recordingStartCts;
    private bool _recordingActive;
    private Mutex? _singleInstanceMutex;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            AppLogger.Write(args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLogger.Write(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Write(args.Exception);
            args.SetObserved();
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            AppLogger.WriteMessage("OnLaunched: start");
            _services = ServiceConfigurator.Build();
            AppLogger.WriteMessage("OnLaunched: services configured");
            _profile = _services.GetRequiredService<IAppProfile>();
            AppLogger.Configure(_profile.AppName, _profile.DataFolderName);
            _singleInstanceMutex = new Mutex(initiallyOwned: true, _profile.MutexName, out var firstInstance);
            if (!firstInstance)
            {
                AppLogger.WriteMessage("OnLaunched: another instance is already running, exiting");
                Exit();
                return;
            }
            // Localize as early as possible — MainWindow's readonly field initializers call L.S(...),
            // so the language must be applied BEFORE the window is constructed.
            // 1) Apply system-detected language so default-fresh installs render in the OS language.
            L.Apply(UiLanguage.Auto);
            // 2) Load settings to learn the user's saved preference.
            var settingsService = _services.GetRequiredService<ISettingsService>();
            var earlySettings = await settingsService.LoadAsync();
            // 3) Re-apply with the persisted preference (Auto keeps system-detection).
            L.Apply(earlySettings.UiLanguage);

            _window = ActivatorUtilities.CreateInstance<MainWindow>(_services);
            AppLogger.WriteMessage("OnLaunched: window created");
            _hotkeys = _services.GetRequiredService<IHotkeyService>();
            _pipeline = _services.GetRequiredService<SpeechPipeline>();
            _recorder = _services.GetRequiredService<IAudioRecorder>();
            _status = _services.GetRequiredService<ITrayStatusService>();
            _sounds = _services.GetRequiredService<IFeedbackSoundService>();
            _clipboardCapture = _services.GetRequiredService<IClipboardSourceCapture>();

            _hotkeys.HotkeyDown += OnHotkeyDown;
            _hotkeys.HotkeyUp += OnHotkeyUp;

            var earlyReady = settingsService.Validate(earlySettings).IsReady;
            var deferWindowPresentation = earlySettings.LaunchMinimizedToTray && earlyReady;

            // WinUI 3: Window.Activate macht das Fenster zwingend kurz sichtbar. Bei Tray-Start
            // schieben wir es vorher off-screen, damit der erste Frame nicht im sichtbaren
            // Monitorbereich aufblitzt. HideForDeferredPresentation versteckt es danach komplett.
            if (deferWindowPresentation)
            {
                _window.PreActivateMoveOffScreen();
            }

            _window.Activate();
            AppLogger.WriteMessage("OnLaunched: window activated");
            if (deferWindowPresentation)
            {
                _window.HideForDeferredPresentation();
            }

            await _window.InitializeAfterActivationAsync();
            AppLogger.WriteMessage("OnLaunched: window initialized");
            if (deferWindowPresentation)
            {
                _window.ReassertDeferredPresentationHidden();
            }

            var settingsModel = _window.Settings;
            var hotkeyIssues = await _hotkeys.RegisterAsync(settingsModel);
            AppLogger.WriteMessage("OnLaunched: hotkeys registered");
            var readiness = settingsService.Validate(settingsModel);
            var readyForTray = readiness.IsReady && hotkeyIssues.Count == 0;
            var startHidden = settingsModel.LaunchMinimizedToTray && readyForTray;

            if (startHidden)
            {
                if (deferWindowPresentation)
                {
                    _window.ReassertDeferredPresentationHidden();
                }

                _window.HideToTray();
                _window.AppWindow.IsShownInSwitchers = true;
                AppLogger.WriteMessage("OnLaunched: started hidden in tray");
            }
            else if (deferWindowPresentation)
            {
                _window.AppWindow.IsShownInSwitchers = true;
                _window.ShowFromTray();
                AppLogger.WriteMessage("OnLaunched: main window visible (tray start aborted)");
            }
            else
            {
                AppLogger.WriteMessage("OnLaunched: main window visible (no tray-only start)");
            }

            _tray = ActivatorUtilities.CreateInstance<TrayIconController>(_services, _window);
            AppLogger.WriteMessage("OnLaunched: tray created");

            if (hotkeyIssues.Count > 0)
            {
                _status.SetStatus(TrayStatus.ConfigurationRequired, string.Join(" ", hotkeyIssues.Select(issue => issue.Message)));
            }
            else if (!readiness.IsReady)
            {
                _status.SetStatus(TrayStatus.ConfigurationRequired, L.S("status.setup_required.long"));
            }
            else
            {
                _status.SetStatus(TrayStatus.Idle, L.S("status.ready.hint"));
            }

            AppLogger.WriteMessage("OnLaunched: complete");
        }
        catch (Exception ex)
        {
            AppLogger.Write(ex);
            Exit();
        }
    }

    private async void OnHotkeyDown(object? sender, HotkeyPressedEventArgs e)
    {
        if (_status?.CurrentStatus is TrayStatus.Paused or TrayStatus.Processing or TrayStatus.Recording
            || _pendingRecordingAssistantId is not null)
        {
            return;
        }

        var assistant = await ResolveAssistantAsync(e.AssistantId);
        if (assistant is null)
        {
            return;
        }

        var typeDefinition = _profile?.Modes.FirstOrDefault(m => m.Mode == assistant.Type);
        if (typeDefinition is { RequiresClipboardSource: true })
        {
            var clipboard = _clipboardCapture?.TryGetText();
            if (string.IsNullOrWhiteSpace(clipboard))
            {
                var label = string.IsNullOrWhiteSpace(assistant.Name) ? typeDefinition.Name : assistant.Name;
                _status?.SetStatus(TrayStatus.Error, $"{label} benötigt Text in der Zwischenablage. Es wurde nichts aufgenommen.");
                _capturedSourceText = null;
                return;
            }

            _capturedSourceText = clipboard;
        }
        else
        {
            _capturedSourceText = null;
        }

        try
        {
            var startCts = new CancellationTokenSource();
            _recordingStartCts = startCts;
            _pendingRecordingAssistantId = e.AssistantId;

            if (_sounds is not null)
            {
                await _sounds.PlayRecordingStartAsync(startCts.Token);
            }

            startCts.Token.ThrowIfCancellationRequested();
            await _recorder!.StartAsync(startCts.Token);
            _activeRecordingAssistantId = e.AssistantId;
            _recordingActive = true;
            _status?.SetStatus(TrayStatus.Recording, L.S("status.recording_release"));
        }
        catch (OperationCanceledException)
        {
            _capturedSourceText = null;
        }
        catch (InvalidOperationException)
        {
            _status?.SetStatus(TrayStatus.Error, L.S("error.mic_access"));
            _capturedSourceText = null;
        }
        finally
        {
            _pendingRecordingAssistantId = null;
            _recordingStartCts?.Dispose();
            _recordingStartCts = null;
        }
    }

    private async void OnHotkeyUp(object? sender, HotkeyPressedEventArgs e)
    {
        if (_pipeline is null)
        {
            return;
        }

        var sourceText = _capturedSourceText;
        _capturedSourceText = null;

        if (_pendingRecordingAssistantId == e.AssistantId && !_recordingActive)
        {
            _recordingStartCts?.Cancel();
            return;
        }

        if (!_recordingActive || _activeRecordingAssistantId != e.AssistantId)
        {
            return;
        }

        _recordingActive = false;
        _activeRecordingAssistantId = null;

        AudioBuffer audio;
        try
        {
            audio = await _recorder!.StopAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Write(ex);
            _status?.SetStatus(TrayStatus.Error, L.S("audio.recording_failed"));
            return;
        }

        if (_sounds is not null)
        {
            await _sounds.PlayRecordingStopAsync();
        }

        await _pipeline.RunAsync(e.AssistantId, sourceText, audio);
    }

    private async Task<AssistantInstance?> ResolveAssistantAsync(string assistantId)
    {
        if (_services is null)
        {
            return null;
        }

        var settings = await _services.GetRequiredService<ISettingsService>().LoadAsync();
        return settings.Assistants.FirstOrDefault(a => string.Equals(a.Id, assistantId, StringComparison.Ordinal));
    }

    public async Task ShutdownAsync()
    {
        _window?.PrepareForExit();
        if (_window is not null)
        {
            await _window.SaveWindowBoundsAsync();
        }

        if (_hotkeys is not null)
        {
            await _hotkeys.DisposeAsync();
        }

        _tray?.Dispose();
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }
}

public sealed class TrayIconController : IDisposable
{
    private const int TrayId = 1;
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const int TrayCallbackMessage = 0x8000 + 42;
    private const int WmCommand = 0x0111;
    private const int WmDestroy = 0x0002;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;
    private const int IdiApplication = 32512;
    private const int MfString = 0x00000000;
    private const int MfSeparator = 0x00000800;
    private const int MfGrayed = 0x00000001;
    private const int MfChecked = 0x00000008;
    private const int MfDefault = 0x00001000;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const int CmdOpen = 1000;
    private const int CmdActive = 1001;
    private const int CmdExit = 1004;

    private readonly IAppProfile _profile;
    private readonly MainWindow _window;
    private readonly ITrayStatusService _status;
    private readonly IHotkeyService _hotkeys;
    private readonly ISettingsService _settingsService;
    private readonly WndProc _wndProc;
    private readonly uint _taskbarCreatedMessage;
    private readonly IntPtr _hwnd;
    private IntPtr _icon;
    private bool _ownsIcon;
    private bool _disposed;

    public TrayIconController(IAppProfile profile, MainWindow window, ITrayStatusService status, IHotkeyService hotkeys, ISettingsService settingsService)
    {
        _profile = profile;
        _window = window;
        _status = status;
        _hotkeys = hotkeys;
        _settingsService = settingsService;
        _taskbarCreatedMessage = RegisterWindowMessage(TaskbarCreatedMessageName);
        if (_taskbarCreatedMessage == 0)
        {
            AppLogger.WriteMessage($"Tray RegisterWindowMessage failed: {Marshal.GetLastWin32Error()}");
        }

        _wndProc = WindowProc;
        _hwnd = CreateMessageWindow(_wndProc);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(L.S("error.tray_window"));
        }

        _status.StatusChanged += OnStatusChanged;
        AddOrUpdateIcon(_status.CurrentStatus, TooltipFor(_status.CurrentStatus, _status.Message), NimAdd);
    }

    private void OnStatusChanged(object? sender, TrayStatusChangedEventArgs args) => Update(args.Status, args.Message);

    private async Task ToggleActiveAsync()
    {
        if (_status.CurrentStatus == TrayStatus.Recording)
        {
            return;
        }

        if (_status.CurrentStatus == TrayStatus.Paused)
        {
            _hotkeys.Resume();
            var readiness = _settingsService.Validate(await _settingsService.LoadAsync());
            _status.SetStatus(
                readiness.IsReady ? TrayStatus.Idle : TrayStatus.ConfigurationRequired,
                readiness.IsReady
                    ? L.S("status.ready.hint")
                    : L.S("status.setup_required.long"));
            return;
        }

        _hotkeys.Pause();
        if (_status.CurrentStatus != TrayStatus.Processing)
        {
            _status.SetStatus(TrayStatus.Paused, L.S("status.inactive.long"));
        }
    }

    private void Update(TrayStatus status, string message)
    {
        AddOrUpdateIcon(status, TooltipFor(status, message), NimModify);
    }

    private void AddOrUpdateIcon(TrayStatus status, string tooltip, uint message)
    {
        var newIcon = LoadIconFor(status, out var ownsNewIcon);
        var data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = TrayId,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = newIcon,
            szTip = TrimTooltip(tooltip),
            uVersionOrTimeout = NotifyIconVersion4
        };

        if (!Shell_NotifyIcon(message, ref data))
        {
            DestroyIconIfOwned(newIcon, ownsNewIcon);
            return;
        }

        DestroyIconIfOwned(_icon, _ownsIcon);
        _icon = newIcon;
        _ownsIcon = ownsNewIcon;
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage)
        {
            AppLogger.WriteMessage("Tray TaskbarCreated received: re-adding icon");
            AddOrUpdateIcon(_status.CurrentStatus, TooltipFor(_status.CurrentStatus, _status.Message), NimAdd);
            return IntPtr.Zero;
        }

        if (msg == TrayCallbackMessage)
        {
            var mouseMessage = lParam.ToInt32();
            if (mouseMessage == WmLButtonDblClk)
            {
                _window.ShowFromTray();
                return IntPtr.Zero;
            }

            if (mouseMessage == WmRButtonUp)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        if (msg == WmCommand)
        {
            HandleCommand(wParam.ToInt32() & 0xFFFF);
            return IntPtr.Zero;
        }

        if (msg == WmDestroy)
        {
            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MfString | MfDefault, CmdOpen, L.S("tray.open"));
        AppendMenu(menu, MfSeparator, 0, string.Empty);
        AppendMenu(menu, MfString | (_hotkeys.IsPaused ? 0 : MfChecked) | (_status.CurrentStatus == TrayStatus.Recording ? MfGrayed : 0), CmdActive, L.S("tray.active"));
        AppendMenu(menu, MfSeparator, 0, string.Empty);
        AppendMenu(menu, MfString, CmdExit, L.S("tray.exit"));
        GetCursorPos(out var point);
        SetForegroundWindow(_hwnd);
        var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCmd, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);
        if (command != 0)
        {
            HandleCommand(command);
        }
    }

    private void HandleCommand(int command)
    {
        switch (command)
        {
            case CmdOpen:
                _window.ShowFromTray();
                break;
            case CmdActive:
                _ = ToggleActiveAsync();
                break;
            case CmdExit:
                _ = ExitAsync();
                break;
        }
    }

    private string TooltipFor(TrayStatus status, string message) => status switch
    {
        TrayStatus.Idle => $"{_profile.AppName} - bereit",
        TrayStatus.Paused => $"{_profile.AppName} - inaktiv",
        TrayStatus.Recording => $"{_profile.AppName} - Aufnahme läuft",
        TrayStatus.Processing => $"{_profile.AppName} - verarbeitet den Text",
        TrayStatus.Success => $"{_profile.AppName} - Text eingefügt",
        TrayStatus.Error => $"{_profile.AppName} - {message}",
        TrayStatus.Attention => TrimTooltip($"{_profile.AppName} – {message}"),
        TrayStatus.ConfigurationRequired => TrimTooltip($"{_profile.AppName} – {message}"),
        _ => _profile.AppName
    };

    private static string IconPathFor(TrayStatus status)
    {
        var fileName = status switch
        {
            TrayStatus.Paused => "TrayPaused.ico",
            TrayStatus.Recording => "TrayRecording.ico",
            TrayStatus.Processing => "TrayProcessing.ico",
            TrayStatus.Success => "TraySuccess.ico",
            TrayStatus.Error => "TrayError.ico",
            TrayStatus.Attention => "TrayIdle.ico",
            TrayStatus.ConfigurationRequired => "TrayConfigurationRequired.ico",
            _ => "TrayIdle.ico"
        };
        return Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
    }

    private static IntPtr LoadIconFor(TrayStatus status, out bool ownsIcon)
    {
        var path = IconPathFor(status);
        var icon = File.Exists(path)
            ? LoadImage(IntPtr.Zero, path, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize)
            : IntPtr.Zero;
        if (icon != IntPtr.Zero)
        {
            ownsIcon = true;
            return icon;
        }

        ownsIcon = false;
        return LoadIcon(IntPtr.Zero, new IntPtr(IdiApplication));
    }

    private static string TrimTooltip(string tooltip) => tooltip.Length > 127 ? tooltip[..127] : tooltip;

    private static void DestroyIconIfOwned(IntPtr icon, bool ownsIcon)
    {
        if (icon != IntPtr.Zero && ownsIcon)
        {
            DestroyIcon(icon);
        }
    }

    private static async Task ExitAsync()
    {
        if (Microsoft.UI.Xaml.Application.Current is App app)
        {
            await app.ShutdownAsync();
        }

        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    private static IntPtr CreateMessageWindow(WndProc wndProc)
    {
        var className = "SchreibkraftTrayWindow_" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var moduleHandle = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            cbSize = (uint)Marshal.SizeOf<WindowClass>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = moduleHandle,
            lpszClassName = className
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            AppLogger.WriteMessage($"Tray RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
            return IntPtr.Zero;
        }

        var hwnd = CreateWindowEx(0, className, className, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, moduleHandle, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
        {
            AppLogger.WriteMessage($"Tray CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }

        return hwnd;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _status.StatusChanged -= OnStatusChanged;
        var data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = TrayId
        };
        Shell_NotifyIcon(NimDelete, ref data);
        DestroyIconIfOwned(_icon, _ownsIcon);
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
}

// AppLogger ist in den Template-konformen AppLogger umgezogen:
// Schreibkraft.Infrastructure.Logging.AppLogger. Die Klasse ist vollständig durch
// using-Alias bzw. direkten Namespace-Zugriff ersetzt.

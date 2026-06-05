using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Controls.Primitives;
using Schreibkraft.Core;
using Schreibkraft.Infrastructure;
using Schreibkraft.Infrastructure.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace Schreibkraft;

public sealed partial class MainWindow : Window
{
    private const int MaxInitialWindowWidth = 1500;
    private const int MaxInitialWindowHeight = 960;
    private const int MinimumWindowWidth = 760;
    /// <summary>Größe des Status-Infoleisten-Icons: Standard-InfoBar nutzt für „Informational“ einen kleinen ProgressRing in einer Kachel – wir ersetzen durch flächige MDL2-Glyphen.</summary>
    private const double StatusInfoBarIconSize = 22.0;
    private const int MinimumWindowHeight = 560;
    private const string OverviewPage = "overview";
    private const string AudioLanguagePage = "audioLanguage";
    private const string PipelinePage = "pipeline";
    private const string HotkeyPage = "hotkeys";
    private const string SpellingPage = "spelling";
    private const string GeneralPage = "general";
    private const string DiagnosticsPage = "diagnostics";
    private const string AboutPage = "about";
    private const string GlobalStandardLabel = "(globaler Standard)";

    private enum SettingsCaptureScope
    {
        None,
        Shell,
        Assistants,
        All
    }

    private readonly ISettingsService _settingsService;
    private readonly ISecretProtector _secretProtector;
    private readonly ITrayStatusService _statusService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IAutostartService _autostartService;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly ILlmService _llmService;
    private readonly IFeedbackSoundService _feedbackSoundService;
    private readonly FileLogger _fileLogger;
    private readonly IProcessingFailureLog _processingFailureLog;
    private readonly IProcessingHistoryLog _processingHistoryLog;
    private readonly SemaphoreSlim _persistGate = new(1, 1);
    private readonly CheckBox _keepHistory = new() { Content = L.S("diagnostics.history.keep_label") };
    private readonly NavigationView _navigation = new()
    {
        PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        IsSettingsVisible = false
    };
    private readonly InfoBar _statusInfo = new() { IsOpen = true, Title = L.S("error.config"), Message = L.S("status.please_check"), Severity = InfoBarSeverity.Warning };
    private readonly TextBox _statusMessageText = new()
    {
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        BorderThickness = new Thickness(0),
        Background = new SolidColorBrush(Colors.Transparent),
        Padding = new Thickness(0),
        IsTabStop = false
    };
    private readonly ComboBox _sttProvider = Combo(L.S("pipeline.provider"));
    private readonly ComboBox _sttModel = new()
    {
        Header = L.S("pipeline.model"),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        MinWidth = 260,
        IsEditable = true,
        PlaceholderText = L.S("pipeline.model.suggest_stt")
    };
    private readonly PasswordBox _sttApiKey = new()
    {
        Header = L.S("pipeline.api_key.stt"),
        PlaceholderText = L.S("pipeline.api_key.placeholder"),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock _sttApiKeyStatus = Body(string.Empty);
    private readonly Button _sttApiKeyReplace = new();
    private readonly TextBox _sttEndpoint = new()
    {
        Header = L.S("pipeline.custom_endpoint"),
        PlaceholderText = L.S("pipeline.custom_endpoint.placeholder_stt"),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        IsSpellCheckEnabled = false
    };
    private StackPanel? _sttEndpointPanel;
    private readonly ComboBox _audioInputDevice = Combo(L.S("audio.source"));
    private readonly ComboBox _inputLanguage = Combo(L.S("language.input"));
    private readonly ComboBox _outputLanguage = Combo(L.S("language.output"));
    private readonly ComboBox _llmProvider = Combo(L.S("pipeline.provider"));
    private readonly ComboBox _llmModel = new()
    {
        Header = L.S("pipeline.model"),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        MinWidth = 260,
        IsEditable = true,
        PlaceholderText = L.S("pipeline.model.suggest_llm")
    };
    private readonly PasswordBox _llmApiKey = new()
    {
        Header = L.S("pipeline.api_key.llm"),
        PlaceholderText = L.S("pipeline.api_key.placeholder"),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock _llmApiKeyStatus = Body(string.Empty);
    private readonly Button _llmApiKeyReplace = new();
    private readonly TextBox _llmEndpoint = new()
    {
        Header = L.S("pipeline.custom_endpoint"),
        PlaceholderText = L.S("pipeline.custom_endpoint.placeholder_llm"),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        IsSpellCheckEnabled = false
    };
    private StackPanel? _llmEndpointPanel;
    private readonly ComboBox _insertMethod = Combo(L.S("pipeline.insert_method"));
    private readonly TextBox _insertionTestTarget = new() { TextWrapping = TextWrapping.Wrap, PlaceholderText = L.S("overview.test_field.placeholder"), MinHeight = 96, AcceptsReturn = true };
    private readonly CheckBox _restoreClipboard = new() { Content = L.S("pipeline.restore_clipboard") };
    private readonly CheckBox _launchMinimized = new() { Content = L.S("general.launch_minimized") };
    private readonly CheckBox _minimizeToTray = new() { Content = L.S("general.minimize_to_tray") };
    private readonly CheckBox _playRecordingSounds = new() { Content = L.S("general.play_sounds") };
    private readonly Slider _recordingSoundVolume = new()
    {
        Minimum = 0,
        Maximum = 100,
        StepFrequency = 5,
        Value = 100,
        TickFrequency = 25,
        TickPlacement = Microsoft.UI.Xaml.Controls.Primitives.TickPlacement.BottomRight,
        SnapsTo = Microsoft.UI.Xaml.Controls.Primitives.SliderSnapsTo.StepValues,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock _recordingSoundVolumeMinValue = Body("0 %");
    private readonly TextBlock _recordingSoundVolumeMaxValue = Body("100 %");
    private StackPanel? _recordingSoundControls;
    private readonly Button _testRecordingSound = new()
    {
        Content = new FontIcon { Glyph = "\uE768", FontSize = 16 },
        Width = 36,
        Height = 32,
        Padding = new Thickness(0)
    };
    private readonly CheckBox _autostart = new() { Content = L.S("general.autostart") };
    private readonly TextBox _maxSeconds = TextField(L.S("general.max_recording_seconds"), "60");
    private readonly TextBox _timeoutSeconds = TextField(L.S("general.timeout_seconds"), "45");
    private readonly NumberBox _transcriptionRetriesOnFailure = RetryCountNumberBox(L.S("pipeline.retries"));
    private readonly NumberBox _llmRetriesOnFailure = RetryCountNumberBox(L.S("pipeline.retries"));
    private readonly NumberBox _clipboardInsertRetriesOnFailure = RetryCountNumberBox(L.S("pipeline.retries"));
    // MaxHeight + interner Scrollbar: sonst scrollt beim Klick in die TextBox der äußere
    // Seiten-ScrollViewer mit, weil WinUI den (unsichtbaren) Caret in den View bringen will.
    private readonly TextBox _diagnostics = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        IsReadOnly = true,
        MinHeight = 260,
        MaxHeight = 480,
        FontFamily = new FontFamily("Cascadia Mono")
    };
    private readonly Dictionary<string, TextBlock> _hotkeyValues = [];
    private readonly Dictionary<string, TextBox> _prompts = [];
    private readonly Dictionary<string, TextBox> _names = [];
    private readonly Dictionary<string, CheckBox> _systemPromptOverrideCheckBoxes = [];
    private readonly Dictionary<string, TextBox> _systemPrompts = [];
    private readonly Dictionary<string, ComboBox> _assistantInputLanguageOverrides = [];
    private readonly Dictionary<string, ComboBox> _assistantOutputLanguageOverrides = [];
    private readonly Dictionary<string, ToolTip> _policyTooltips = [];
    private readonly Dictionary<string, Slider> _intensitySliders = [];
    private readonly Dictionary<string, TextBlock> _intensityValues = [];
    private readonly Dictionary<string, Button> _captureButtons = [];
    private readonly Dictionary<string, ComboBox> _writingStyles = [];
    private readonly Dictionary<string, ComboBox> _paragraphDensities = [];
    private readonly Dictionary<string, ComboBox> _emojiExpressions = [];
    private readonly Dictionary<string, ComboBox> _assistantTypes = [];
    private ListView? _hotkeyPagePanel;
    private ScrollViewer? _hotkeyPageScroll;
    private readonly Dictionary<string, string> _audioDeviceIdsByDisplayName = [];
    private readonly Dictionary<string, UIElement> _pages = [];
    // Computed on each access so they reflect the current UI language (LocalizationService).
    private static IReadOnlyDictionary<InsertMethod, string> InsertMethodLabels => new Dictionary<InsertMethod, string>
    {
        [InsertMethod.Clipboard] = L.S("common.via_clipboard"),
        [InsertMethod.SendInput] = L.S("common.directly_typed")
    };
    private static IReadOnlyDictionary<WritingStyle, string> WritingStyleLabels => new Dictionary<WritingStyle, string>
    {
        [WritingStyle.Casual] = L.S("style.label.casual"),
        [WritingStyle.Neutral] = L.S("style.label.neutral"),
        [WritingStyle.Professional] = L.S("style.label.professional"),
        [WritingStyle.Academic] = L.S("style.label.academic")
    };
    private static IReadOnlyDictionary<ParagraphDensity, string> ParagraphDensityLabels => new Dictionary<ParagraphDensity, string>
    {
        [ParagraphDensity.Compact] = L.S("paragraph.label.compact"),
        [ParagraphDensity.Balanced] = L.S("paragraph.label.balanced"),
        [ParagraphDensity.Spacious] = L.S("paragraph.label.spacious")
    };
    private static IReadOnlyDictionary<EmojiExpression, string> EmojiExpressionLabels => new Dictionary<EmojiExpression, string>
    {
        [EmojiExpression.None] = L.S("emoji.label.none"),
        [EmojiExpression.Sparse] = L.S("emoji.label.sparse"),
        [EmojiExpression.Balanced] = L.S("emoji.label.balanced"),
        [EmojiExpression.Lively] = L.S("emoji.label.lively"),
        [EmojiExpression.Heavy] = L.S("emoji.label.heavy")
    };
    private AppSettings _settings = new();
    private string? _capturingAssistantId;
    private bool _windowConfigured;
    private bool _isExiting;
    private bool _initialNavigationDone;
    private bool _autoPersistWired;
    private bool _suppressApiKeyDirty;
    private bool _llmApiKeyDirty;
    private bool _sttApiKeyDirty;
    private bool _llmApiKeyEditing;
    private bool _sttApiKeyEditing;
    private bool _deferredPresentationActive;
    private SubclassProc? _minimizeSubclassProc;

    private readonly IAppProfile _profile;

    public MainWindow(
        IAppProfile profile,
        ISettingsService settingsService,
        ISecretProtector secretProtector,
        ITrayStatusService statusService,
        IHotkeyService hotkeyService,
        IAutostartService autostartService,
        IAudioDeviceService audioDeviceService,
        ILlmService llmService,
        IFeedbackSoundService feedbackSoundService,
        FileLogger fileLogger,
        IProcessingFailureLog processingFailureLog,
        IProcessingHistoryLog processingHistoryLog)
    {
        Root = new Grid();
        Content = Root;
        _profile = profile;
        _settingsService = settingsService;
        _secretProtector = secretProtector;
        _statusService = statusService;
        _hotkeyService = hotkeyService;
        _autostartService = autostartService;
        _audioDeviceService = audioDeviceService;
        _llmService = llmService;
        _feedbackSoundService = feedbackSoundService;
        _fileLogger = fileLogger;
        _processingFailureLog = processingFailureLog;
        _processingHistoryLog = processingHistoryLog;

        Title = $"{_profile.AppName} {GetAppVersion()}";
        PopulateLists();
        BuildShell();
        _statusService.StatusChanged += (_, args) => DispatcherQueue.TryEnqueue(() => UpdateInfoBar(args.Status, args.Message));
    }

    public async Task InitializeAfterActivationAsync()
    {
        ConfigureWindow();
        await LoadSettingsAsync();
        // Bounds nur anwenden, wenn das Fenster nicht gerade für einen Tray-Start versteckt wird –
        // sonst würde Resize/Move das versteckte Fenster kurzzeitig wieder aufpoppen lassen.
        // Im Tray-/Deferred-Pfad ruft ShowFromTray später ohnehin ApplyWindowBounds erneut.
        if (!_deferredPresentationActive)
        {
            ApplyWindowBounds();
        }
    }

    /// <summary>
    /// Vor <see cref="Window.Activate"/>: Fenster off-screen platzieren, damit der von WinUI erzwungene
    /// erste sichtbare Frame nach Activate nicht im sichtbaren Monitorbereich aufblitzt.
    /// Muss aufgerufen werden, bevor Activate startet.
    /// </summary>
    internal void PreActivateMoveOffScreen()
    {
        _deferredPresentationActive = true;
        try
        {
            var appWindow = CurrentAppWindow();
            appWindow.IsShownInSwitchers = false;
            appWindow.Move(new PointInt32(-32000, -32000));
        }
        catch
        {
            // Ignorieren; Activate zeigt das Fenster dann ggf. kurz im Standardbereich.
        }
    }

    /// <summary>
    /// Nach <see cref="Window.Activate"/>: Fenster ausblenden, während Einstellungen/UI geladen werden.
    /// Anschließend erst <see cref="ShowFromTray"/> (sichtbar) oder <see cref="HideToTray"/> (nur Tray).
    /// </summary>
    internal void HideForDeferredPresentation()
    {
        _deferredPresentationActive = true;
        Root.Opacity = 0;

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            ShowWindow(hwnd, SwHide);
            var appWindow = CurrentAppWindow();
            appWindow.IsShownInSwitchers = false;
            appWindow.Hide();
        }
        catch
        {
            try
            {
                var appWindow = CurrentAppWindow();
                appWindow.IsShownInSwitchers = false;
                appWindow.Move(new PointInt32(-32000, -32000));
            }
            catch
            {
                // Ignorieren; Startpfad zeigt das Fenster dann ggf. kurz.
            }
        }
    }

    /// <summary>
    /// Nach Resize/Layout kann WinUI das Fenster kurz sichtbar machen — erneut verstecken.
    /// </summary>
    internal void ReassertDeferredPresentationHidden()
    {
        if (!_deferredPresentationActive)
        {
            return;
        }

        ReassertDeferredHiddenAfterLayout();
    }

    private void ReassertDeferredHiddenAfterLayout()
    {
        if (!_deferredPresentationActive)
        {
            return;
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SwHide);
            }

            var appWindow = CurrentAppWindow();
            appWindow.IsShownInSwitchers = false;
            appWindow.Hide();
        }
        catch
        {
            // bewusst leer
        }
    }

    private void ScheduleDeferredHiddenReassert()
    {
        if (!_deferredPresentationActive)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () => ReassertDeferredHiddenAfterLayout());
    }

    /// <summary>Aktuelle Einstellungen (nach <see cref="InitializeAfterActivationAsync"/>), für den App-Startpfad.</summary>
    internal AppSettings Settings => _settings;

    public void HideToTray()
    {
        _ = SaveWindowBoundsAsync();
        ShowWindow(WindowNative.GetWindowHandle(this), SwHide);
        _deferredPresentationActive = false;
        Root.Opacity = 1;
    }

    public void ShowFromTray()
    {
        _deferredPresentationActive = false;
        Root.Opacity = 1;
        // Falls das Fenster beim Start off-screen platziert wurde (siehe App.OnLaunched), zurück
        // an die gespeicherte oder eine sinnvolle Default-Position holen, bevor wir es zeigen.
        ApplyWindowBounds();
        var hwnd = WindowNative.GetWindowHandle(this);
        // Falls zuvor per SW_HIDE versteckt: erst wieder sichtbar machen, dann aktivieren.
        ShowWindow(hwnd, SwRestore);
        Activate();
    }

    public void PrepareForExit() => _isExiting = true;

    private async Task ShutdownFromCloseAsync()
    {
        try
        {
            CaptureWindowBoundsToSettings();
            await PersistAsync(CaptureScopeForPage(_settings.LastSelectedSettingsSection));
        }
        catch
        {
            // Beim Schließen darf ein Speicherfehler die App nicht am Beenden hindern.
        }

        if (Microsoft.UI.Xaml.Application.Current is App app)
        {
            await app.ShutdownAsync();
        }

        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    public async Task SaveWindowBoundsAsync()
    {
        CaptureWindowBoundsToSettings();
        await PersistAsync(SettingsCaptureScope.None);
    }

    private void CaptureWindowBoundsToSettings()
    {
        // Wenn das Fenster nie sichtbar war (Tray-Start mit verzögerter Präsentation),
        // sind Size/Position die Defaults aus ConfigureWindow – nicht repräsentativ.
        if (_deferredPresentationActive)
        {
            return;
        }

        var appWindow = CurrentAppWindow();
        // Beim Beenden mit minimiertem Fenster sind Size/Position nicht repräsentativ – dann nicht überschreiben.
        if (appWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized)
        {
            return;
        }

        _settings.WindowBounds.Width = Math.Max(MinimumWindowWidth, appWindow.Size.Width);
        _settings.WindowBounds.Height = Math.Max(MinimumWindowHeight, appWindow.Size.Height);
        _settings.WindowBounds.X = appWindow.Position.X;
        _settings.WindowBounds.Y = appWindow.Position.Y;
    }

    public void RefreshDiagnostics()
    {
        var readiness = _settingsService.Validate(_settings);
        var exePath = Environment.ProcessPath;
        var hotkeyStatus = readiness.Issues.Any(issue => issue.Field.StartsWith("hotkey", StringComparison.OrdinalIgnoreCase))
            ? L.S("diag.label.hotkey_status.check")
            : L.S("diag.label.hotkey_status.valid");
        var setupStatus = readiness.IsReady ? L.S("status.ready") : L.S("status.setup_required");
        _diagnostics.Text =
            $"{L.S("diag.label.app_version")}: {GetAppVersion()}{Environment.NewLine}" +
            $"{L.S("diag.label.assembly_version")}: {GetAssemblyInformationalVersion()}{Environment.NewLine}" +
            $"{L.S("diag.label.exe_product_version")}: {GetExeProductVersion()}{Environment.NewLine}" +
            $"{L.S("diag.label.exe")}: {exePath ?? L.S("diag.unknown")}{Environment.NewLine}" +
            $"{L.S("diag.label.net_runtime")}: {Environment.Version}{Environment.NewLine}" +
            $"{L.S("diag.label.data_dir")}: {_settingsService.DataDirectory}{Environment.NewLine}" +
            $"{L.S("diag.label.log_dir")}: {_settingsService.LogDirectory}{Environment.NewLine}" +
            $"{L.S("diag.label.stt_provider")}: {_settings.SttProvider}{Environment.NewLine}" +
            $"{L.S("diag.label.stt_model")}: {_settings.SttModel}{Environment.NewLine}" +
            $"{L.S("diag.label.audio_source")}: {AudioDeviceName(_settings.AudioInputDeviceId)}{Environment.NewLine}" +
            $"{L.S("diag.label.input_lang")}: {Defaults.LanguageName(_settings.InputLanguage)}{Environment.NewLine}" +
            $"{L.S("diag.label.output_lang")}: {Defaults.LanguageName(_settings.OutputLanguage)}{Environment.NewLine}" +
            $"{L.S("diag.label.llm_provider")}: {_settings.LlmProvider}{Environment.NewLine}" +
            $"{L.S("diag.label.llm_model")}: {_settings.LlmModel}{Environment.NewLine}" +
            $"{L.S("diag.label.hotkey_status")}: {hotkeyStatus}{Environment.NewLine}" +
            $"{L.S("diag.label.setup_status")}: {setupStatus}{Environment.NewLine}" +
            $"{L.S("diag.label.status")}: {FriendlyStatus(_statusService.CurrentStatus)} - {SanitizeStatus(_statusService.Message)}{Environment.NewLine}" +
            $"{L.S("diag.elevated_note")}{Environment.NewLine}" +
            string.Join(Environment.NewLine, readiness.Issues.Select(issue => $"- {issue.Message}")) +
            LastProcessingFailureSection() +
            ProcessingHistorySection();
    }

    private string LastProcessingFailureSection()
    {
        var entry = _processingFailureLog.LastEntry;
        if (string.IsNullOrWhiteSpace(entry))
        {
            return string.Empty;
        }

        return $"{Environment.NewLine}{Environment.NewLine}Letzter Verarbeitungsfehler:{Environment.NewLine}{entry}";
    }

    private string ProcessingHistorySection()
    {
        if (!_settings.KeepProcessingHistory)
        {
            return string.Empty;
        }

        var entries = _processingHistoryLog.Entries;
        if (entries.Count == 0)
        {
            return $"{Environment.NewLine}{Environment.NewLine}{L.S("diag.history.empty")}";
        }

        var sb = new StringBuilder();
        sb.Append(Environment.NewLine).Append(Environment.NewLine);
        sb.Append(L.F("diag.history.header", InMemoryProcessingHistoryLog.MaxEntries)).Append(Environment.NewLine);
        var index = 1;
        foreach (var entry in entries)
        {
            sb.Append(Environment.NewLine);
            sb.Append("────────────── #").Append(index).Append(" ──────────────").Append(Environment.NewLine);
            sb.Append(L.S("diag.entry.time")).Append(": ").Append(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(Environment.NewLine);
            sb.Append(L.S("diag.entry.assistant")).Append(": ").Append(entry.AssistantName).Append(" (").Append(entry.AssistantType).Append(")").Append(Environment.NewLine);
            sb.Append(L.S("diag.entry.insert_method")).Append(": ").Append(InsertMethodLabels.TryGetValue(entry.InsertMethod, out var im) ? im : entry.InsertMethod.ToString());
            if (entry.UsedClipboardFallback)
            {
                sb.Append("  [fallback via keyboard]");
            }
            sb.Append(Environment.NewLine);
            if (entry.Timings is not null)
            {
                sb.Append(L.S("diag.entry.timings")).Append(": ")
                    .Append(L.F("diag.entry.timings.value",
                        FormatDuration(entry.Timings.Total),
                        FormatDuration(entry.Timings.AudioDuration),
                        FormatDuration(entry.Timings.Transcription),
                        FormatDuration(entry.Timings.Llm),
                        FormatDuration(entry.Timings.Insert)));
                if (entry.Timings.RecorderStop > TimeSpan.Zero)
                {
                    sb.Append(" | ")
                        .Append(L.S("diag.entry.timings.recorder_stop"))
                        .Append(' ')
                        .Append(FormatDuration(entry.Timings.RecorderStop));
                }
                sb.Append(Environment.NewLine);
            }
            sb.Append("→ ").Append(L.S("diag.entry.system_prompt")).Append(":").Append(Environment.NewLine);
            sb.Append(VisualizeNewlines(Truncate(entry.SystemPrompt, 1200))).Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            sb.Append("→ ").Append(L.S("diag.entry.mode_prompt")).Append(":").Append(Environment.NewLine);
            sb.Append(VisualizeNewlines(Truncate(entry.ModePrompt, 1200))).Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            sb.Append("→ ").Append(L.S("diag.entry.transcript")).Append(":").Append(Environment.NewLine);
            sb.Append(VisualizeNewlines(Truncate(entry.Transcript, 600))).Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            sb.Append("← ").Append(L.S("diag.entry.ai_response")).Append(":").Append(Environment.NewLine);
            sb.Append(VisualizeNewlines(Truncate(entry.FinalText, 1200))).Append(Environment.NewLine);
            index++;
        }
        return sb.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:0.0}s"
            : $"{duration.TotalMilliseconds:0}ms";
    }

    private static string VisualizeNewlines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // \n als ↵ markieren, dann den echten Umbruch behalten — so erkennt man auf einen Blick,
        // ob die KI Absätze geliefert hat (zwei ↵ direkt untereinander = Leerzeile = Absatz).
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "↵" + Environment.NewLine);
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..max] + "…";
    }

    private void ConfigureWindow()
    {
        if (_windowConfigured)
        {
            return;
        }

        _windowConfigured = true;
        var appWindow = CurrentAppWindow();
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
        };

        // Modern Windows 11 look: extend content under the title bar and use WinUI's themed caption
        // buttons instead of the classic Win32 frame. Mica then shows through the title bar area and
        // Min/Max/Close get the flat, themed style.
        ExtendsContentIntoTitleBar = true;
        ApplyCaptionButtonColors();
        if (Root is FrameworkElement themedRoot)
        {
            // Re-apply once the visual tree is up (ActualTheme is only valid then) and any time
            // the OS or app theme flips between light and dark.
            themedRoot.Loaded += (_, _) => ApplyCaptionButtonColors();
            themedRoot.ActualThemeChanged += (_, _) => ApplyCaptionButtonColors();
        }

        var initialWorkArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        appWindow.Resize(DefaultWindowSizeFor(initialWorkArea));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico"));
        // AppWindow.Closing: Schließen über das X beendet immer. Cancel synchron setzen wäre nötig,
        // wenn wir abbrechen wollten – tun wir nicht mehr.
        appWindow.Closing += (sender, args) =>
        {
            if (_isExiting)
            {
                return;
            }

            args.Cancel = true;
            _ = ShutdownFromCloseAsync();
        };

        InstallMinimizeHook();

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.PreferredMinimumWidth = MinimumWindowWidth;
            presenter.PreferredMinimumHeight = MinimumWindowHeight;
        }

        ReassertDeferredHiddenAfterLayout();
        ScheduleDeferredHiddenReassert();
    }

    /// <summary>
    /// Subclassing der WinUI-Window-Procedure, um SC_MINIMIZE abzufangen und – falls in den Einstellungen
    /// gewünscht – statt einer regulären Minimierung das Fenster ins Tray zu verbergen.
    /// </summary>
    private void InstallMinimizeHook()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        // Delegate als Field halten, damit der GC ihn nicht einsammelt, solange das Fenster lebt.
        _minimizeSubclassProc = MinimizeSubclassProc;
        SetWindowSubclass(hwnd, _minimizeSubclassProc, new UIntPtr(1), UIntPtr.Zero);
    }

    private IntPtr MinimizeSubclassProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        const uint WM_SYSCOMMAND = 0x0112;
        const uint SC_MINIMIZE = 0xF020;
        const uint SC_MASK = 0xFFF0;

        if (uMsg == WM_SYSCOMMAND && (wParam.ToUInt32() & SC_MASK) == SC_MINIMIZE && _settings.MinimizeToTray)
        {
            HideToTray();
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private AppWindow CurrentAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        return AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
    }

    private void ApplyWindowBounds()
    {
        var appWindow = CurrentAppWindow();
        var fallbackWorkArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var fallbackSize = DefaultWindowSizeFor(fallbackWorkArea);
        var width = _settings.WindowBounds.Width is double savedWidth
            ? Math.Clamp((int)savedWidth, MinimumWindowWidth, Math.Max(MinimumWindowWidth, (int)(fallbackWorkArea.Width * 0.9)))
            : fallbackSize.Width;
        var height = _settings.WindowBounds.Height is double savedHeight
            ? Math.Clamp((int)savedHeight, MinimumWindowHeight, Math.Max(MinimumWindowHeight, (int)(fallbackWorkArea.Height * 0.9)))
            : fallbackSize.Height;
        appWindow.Resize(new SizeInt32(width, height));
        if (_settings.WindowBounds.IsSet)
        {
            var desiredX = (int)_settings.WindowBounds.X!.Value;
            var desiredY = (int)_settings.WindowBounds.Y!.Value;
            var desiredRect = new RectInt32(desiredX, desiredY, width, height);

            // Koordinaten können nach Monitorwechsel/DPI/WorkArea-Änderung off-screen werden.
            // Wir prüfen gegen die WorkArea des nächstgelegenen Monitors und erzwingen, dass
            // mindestens ein sinnvoller Teil sichtbar ist (mind. Titelzeile/Grip-Bereich).
            var workArea = MonitorWorkAreaFor(desiredRect);
            if (!IsRectSufficientlyVisible(desiredRect, workArea))
            {
                var centered = CenteredRectWithin(workArea, width, height);
                appWindow.Move(new PointInt32(centered.X, centered.Y));
            }
            else
            {
                var clamped = ClampRectIntoWorkArea(desiredRect, workArea);
                appWindow.Move(new PointInt32(clamped.X, clamped.Y));
            }
        }
        else
        {
            var workArea = fallbackWorkArea;
            var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);
            appWindow.Move(new PointInt32(x, y));
        }
    }

    private static SizeInt32 DefaultWindowSizeFor(RectInt32 workArea)
    {
        var width = Math.Clamp((int)(workArea.Width * 0.78), MinimumWindowWidth, Math.Min(MaxInitialWindowWidth, workArea.Width));
        var height = Math.Clamp((int)(workArea.Height * 0.82), MinimumWindowHeight, Math.Min(MaxInitialWindowHeight, workArea.Height));
        return new SizeInt32(width, height);
    }

    private static RectInt32 CenteredRectWithin(RectInt32 workArea, int width, int height)
    {
        var w = Math.Min(width, workArea.Width);
        var h = Math.Min(height, workArea.Height);
        var x = workArea.X + Math.Max(0, (workArea.Width - w) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - h) / 2);
        return new RectInt32(x, y, w, h);
    }

    private static RectInt32 ClampRectIntoWorkArea(RectInt32 rect, RectInt32 workArea)
    {
        // Wir wollen mindestens ein Stück (Titlebar/Grip) sichtbar halten.
        const int minVisibleX = 80;
        const int minVisibleY = 40;

        var maxX = workArea.X + Math.Max(0, workArea.Width - minVisibleX);
        var maxY = workArea.Y + Math.Max(0, workArea.Height - minVisibleY);
        var minX = workArea.X - Math.Max(0, rect.Width - minVisibleX);
        var minY = workArea.Y;

        var x = Math.Clamp(rect.X, minX, maxX);
        var y = Math.Clamp(rect.Y, minY, maxY);
        return new RectInt32(x, y, rect.Width, rect.Height);
    }

    private static bool IsRectSufficientlyVisible(RectInt32 rect, RectInt32 workArea)
    {
        var intersect = Intersect(rect, workArea);
        // Mindestens ein brauchbarer sichtbarer Bereich, sonst neu zentrieren.
        return intersect.Width >= 120 && intersect.Height >= 80;
    }

    private static RectInt32 Intersect(RectInt32 a, RectInt32 b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);
        return new RectInt32(left, top, width, height);
    }

    private static RectInt32 MonitorWorkAreaFor(RectInt32 rect)
    {
        var r = new Rect
        {
            Left = rect.X,
            Top = rect.Y,
            Right = rect.X + rect.Width,
            Bottom = rect.Y + rect.Height
        };
        var monitor = MonitorFromRect(ref r, MonitorDefaultToNearest);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            var wa = info.rcWork;
            return new RectInt32(wa.Left, wa.Top, wa.Right - wa.Left, wa.Bottom - wa.Top);
        }

        // Fallback: Primary work area via DisplayArea.
        var primary = DisplayArea.GetFromPoint(new PointInt32(0, 0), DisplayAreaFallback.Primary).WorkArea;
        return new RectInt32(primary.X, primary.Y, primary.Width, primary.Height);
    }

    private const int SwHide = 0;
    private const int SwRestore = 9;
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref Rect lprc, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private void PopulateLists()
    {
        AddItems(_sttProvider, Defaults.KnownSttProviders);
        AddItems(_llmProvider, Defaults.KnownLlmProviders);
        AddItems(_sttModel, Defaults.OpenAiSttModels);
        AddItems(_llmModel, Defaults.OpenAiLlmModels);
        AddItems(_inputLanguage, Defaults.InputLanguages.Select(language => language.Name));
        AddItems(_outputLanguage, Defaults.OutputLanguages.Select(language => language.Name));
        AddItems(_insertMethod, InsertMethodLabels.Values);
        RefreshAudioDeviceList();
        // Providers are now selectable; users can switch between OpenAI, Anthropic, Gemini, Deepgram and the custom-endpoint backend.
        _sttProvider.IsEnabled = true;
        _llmProvider.IsEnabled = true;
        _sttProvider.SelectionChanged += (_, _) => OnSttProviderChanged();
        _llmProvider.SelectionChanged += (_, _) => OnLlmProviderChanged();
    }

    private void OnSttProviderChanged()
    {
        var provider = _sttProvider.SelectedItem?.ToString() ?? Defaults.OpenAiProviderName;
        UpdateModelSuggestions(_sttModel, provider, Defaults.ProviderSttModelSuggestions, Defaults.OpenAiSttModels);
        UpdateEndpointVisibility(_sttEndpointPanel, provider);
    }

    private void OnLlmProviderChanged()
    {
        var provider = _llmProvider.SelectedItem?.ToString() ?? Defaults.OpenAiProviderName;
        UpdateModelSuggestions(_llmModel, provider, Defaults.ProviderLlmModelSuggestions, Defaults.OpenAiLlmModels);
        UpdateEndpointVisibility(_llmEndpointPanel, provider);
    }

    private static void UpdateModelSuggestions(ComboBox combo, string provider,
        IReadOnlyDictionary<string, IReadOnlyList<string>> suggestions, IReadOnlyList<string> openAiFallback)
    {
        var current = combo.Text;
        combo.Items.Clear();
        IReadOnlyList<string> items;
        if (provider.Equals(Defaults.OpenAiProviderName, StringComparison.OrdinalIgnoreCase))
        {
            items = openAiFallback;
        }
        else if (suggestions.TryGetValue(provider, out var found))
        {
            items = found;
        }
        else
        {
            items = Array.Empty<string>();
        }
        foreach (var item in items)
        {
            combo.Items.Add(item);
        }
        // Keep the user-entered or saved value visible even if it's not in the suggestions.
        combo.Text = current;
    }

    private static void UpdateEndpointVisibility(StackPanel? panel, string provider)
    {
        if (panel is null)
        {
            return;
        }
        // Show the endpoint field for providers where the user must (or may) supply a URL:
        // - OpenAI-compatible custom: required
        // - Azure Speech: required (region-specific URL)
        // - Ollama / LM Studio: optional override (default is localhost)
        var showAlways =
            provider.Equals(Defaults.OpenAiCompatibleProviderName, StringComparison.OrdinalIgnoreCase)
            || provider.Equals(Defaults.AzureSpeechProviderName, StringComparison.OrdinalIgnoreCase)
            || provider.Equals(Defaults.OllamaProviderName, StringComparison.OrdinalIgnoreCase)
            || provider.Equals(Defaults.LmStudioProviderName, StringComparison.OrdinalIgnoreCase);
        panel.Visibility = showAlways ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildShell()
    {
        // Hintergrund transparent lassen, damit der Mica-Backdrop des Fensters durchscheint.
        Root.Background = new SolidColorBrush(Colors.Transparent);
        // Row 0 = custom title bar (drag region + icon + app name/version)
        // Row 1 = status info
        // Row 2 = navigation/content
        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Root.Padding = new Thickness(0);
        Root.RowSpacing = 0;
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRootKeyDown), true);

        var titleBar = BuildCustomTitleBar();
        Grid.SetRow(titleBar, 0);
        Root.Children.Add(titleBar);
        SetTitleBar(titleBar);

        // Inner padding for the actual content rows (status + navigation).
        _statusInfo.Margin = new Thickness(16, 8, 16, 0);
        _statusInfo.Message = string.Empty;
        _statusInfo.Content = _statusMessageText;
        Grid.SetRow(_statusInfo, 1);
        Root.Children.Add(_statusInfo);

        _navigation.Header = null;
        _navigation.CompactPaneLength = 44;
        // Show the built-in hamburger toggle so the user can collapse the navigation to icon-only width.
        _navigation.IsPaneToggleButtonVisible = true;
        // "LeftCompact" keeps icons visible when collapsed; the toggle button switches between Left and LeftCompact.
        _navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        StripNavigationViewContentChrome(_navigation);
        _navigation.MenuItems.Add(NavItem(L.S("nav.overview"), OverviewPage, "\uE80F"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.assistants"), HotkeyPage, "\uE765"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.spelling"), SpellingPage, "\uE8D2"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.processing"), PipelinePage, "\uE774"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.audio_language"), AudioLanguagePage, "\uE8D6"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.general"), GeneralPage, "\uE713"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.diagnostics"), DiagnosticsPage, "\uE9D9"));
        _navigation.MenuItems.Add(NavItem(L.S("nav.about"), AboutPage, "\uE946"));
        _navigation.OpenPaneLength = ComputeNavigationPaneWidth();
        _navigation.SelectionChanged += OnNavigationSelectionChanged;
        _navigation.Margin = new Thickness(16, 12, 16, 16);
        Grid.SetRow(_navigation, 2);
        Root.Children.Add(_navigation);

        _navigation.SelectedItem = _navigation.MenuItems[0];
        Navigate(OverviewPage);
    }

    /// <summary>
    /// Sets theme-aware caption-button colors. By default the system uses high-contrast values
    /// that look out of place on a Mica backdrop; here we pick a flat black/white scheme that
    /// matches the current light/dark theme of the window.
    /// </summary>
    private void ApplyCaptionButtonColors()
    {
        var appWindow = CurrentAppWindow();
        if (appWindow.TitleBar is not { } titleBar)
        {
            return;
        }
        titleBar.ExtendsContentIntoTitleBar = true;

        // ActualTheme is only reliable once the element is in the visual tree. Prefer that, fall
        // back to the application-level requested theme so the very first call (during window
        // construction, before Activate) still gets a sensible value.
        ElementTheme effective = ElementTheme.Default;
        if (Root is FrameworkElement fe && fe.ActualTheme != ElementTheme.Default)
        {
            effective = fe.ActualTheme;
        }
        if (effective == ElementTheme.Default)
        {
            effective = Application.Current.RequestedTheme == ApplicationTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }
        var isDark = effective == ElementTheme.Dark;
        var fg = isDark ? Colors.White : Colors.Black;
        var fgInactive = isDark
            ? Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0)
            : Windows.UI.Color.FromArgb(0xFF, 0x80, 0x80, 0x80);
        var hoverBg = isDark
            ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x14, 0x00, 0x00, 0x00);
        var pressedBg = isDark
            ? Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x22, 0x00, 0x00, 0x00);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = fg;
        titleBar.ButtonHoverForegroundColor = fg;
        titleBar.ButtonPressedForegroundColor = fg;
        titleBar.ButtonInactiveForegroundColor = fgInactive;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
    }

    /// <summary>
    /// Custom title bar in the Mica area: app icon + app name + version on the left,
    /// space for the system caption buttons (Min/Max/Close) reserved on the right.
    /// Acts as the window drag region via <see cref="Window.SetTitleBar"/>.
    /// </summary>
    private FrameworkElement BuildCustomTitleBar()
    {
        var grid = new Grid
        {
            Height = 36,
            // The right padding mirrors the caption-button width so our content does not slide under them.
            Padding = new Thickness(12, 0, 144, 0),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 8
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");
        var iconImage = new Image
        {
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (File.Exists(iconPath))
        {
            try
            {
                iconImage.Source = new BitmapImage(new Uri(iconPath));
            }
            catch
            {
                // Icon load is purely cosmetic — ignore failures.
            }
        }
        Grid.SetColumn(iconImage, 0);
        grid.Children.Add(iconImage);

        var title = new TextBlock
        {
            Text = $"{_profile.AppName} {GetAppVersion()}",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Opacity = 0.85
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        return grid;
    }

    private void Navigate(string tag)
    {
        _capturingAssistantId = null;
        ResetCaptureButtons();
        if (_initialNavigationDone)
        {
            // UI-Werte zuerst in _settings übernehmen, damit z.B. die Diagnose-Seite
            // die aktuellen Einstellungen anzeigt (auch ohne den I/O-Persist abzuwarten).
            CaptureSettingsFromFields(CaptureScopeForPage(_settings.LastSelectedSettingsSection));
        }
        _settings.LastSelectedSettingsSection = tag;
        _navigation.Content = null;
        try
        {
            if (tag == DiagnosticsPage)
            {
                RefreshDiagnostics();
            }

            if (!_pages.TryGetValue(tag, out var page))
            {
                page = tag switch
                {
                    AudioLanguagePage => BuildAudioLanguagePage(),
                    PipelinePage => BuildPipelinePage(),
                    HotkeyPage => BuildHotkeyPage(),
                    SpellingPage => BuildSpellingPage(),
                    GeneralPage => BuildGeneralPage(),
                    DiagnosticsPage => BuildDiagnosticsPage(),
                    AboutPage => BuildAboutPage(),
                    _ => BuildOverviewPage()
                };
                _pages[tag] = page;
            }

            _navigation.Content = page;
        }
        catch (Exception ex)
        {
            AppLogger.Write(ex);
            _navigation.Content = BuildNavigationErrorPage(tag, ex);
            _statusService.SetStatus(TrayStatus.Error, L.F("error.page.open.body", TitleFor(tag)));
        }
    }

    private void ClearPageCache()
    {
        _navigation.Content = null;
        _pages.Clear();
    }

    private UIElement BuildOverviewPage()
    {
        var panel = PageStack();
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("status.ready_to_dictate")),
                Body(L.S("status.ready.hint")),
                ActionRow(
                    Button(L.S("pipeline.test_connection"), async () => await TestLlmConnectionAsync()))
            }
        }));
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("overview.test_field")),
                Body(L.S("overview.test_field.body")),
                _insertionTestTarget
            }
        }));
        return Page(panel);
    }

    private UIElement BuildNavigationErrorPage(string tag, Exception exception)
    {
        var panel = PageStack();
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("error.page.open")),
                Body(L.F("error.page.open_with_tag", TitleFor(tag))),
                Body(exception.Message),
                ActionRow(Button(L.S("diagnostics.open"), () => Navigate(DiagnosticsPage)))
            }
        }));
        return Page(panel);
    }

    private UIElement BuildPipelinePage()
    {
        _sttEndpointPanel = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        _sttEndpointPanel.Children.Add(_sttEndpoint);
        _llmEndpointPanel = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        _llmEndpointPanel.Children.Add(_llmEndpoint);

        var panel = PageStack();
        panel.Children.Add(Card(Form(L.S("pipeline.transcription"), null,
            _sttProvider,
            _sttModel,
            ApiKeyEditor(L.S("pipeline.api_key.stt"), _sttApiKey, _sttApiKeyReplace, _sttApiKeyStatus),
            _sttEndpointPanel,
            _transcriptionRetriesOnFailure)));
        panel.Children.Add(Card(Form(L.S("pipeline.ai_processing"), null,
            _llmProvider,
            _llmModel,
            ApiKeyEditor(L.S("pipeline.api_key.llm"), _llmApiKey, _llmApiKeyReplace, _llmApiKeyStatus),
            _llmEndpointPanel,
            _llmRetriesOnFailure)));
        panel.Children.Add(Card(Form(L.S("pipeline.insertion"), null,
            _insertMethod,
            _restoreClipboard,
            _clipboardInsertRetriesOnFailure)));
        return Page(panel);
    }

    private UIElement BuildAudioLanguagePage()
    {
        var panel = PageStack();
        panel.Children.Add(Card(Form(L.S("audio.input"), L.S("audio.input.intro"),
            _audioInputDevice,
            ActionRow(Button(L.S("audio.refresh_devices"), RefreshAudioDevices)))));
        panel.Children.Add(Card(Form(L.S("language.section.title"), L.S("language.section.intro"),
            _inputLanguage,
            _outputLanguage)));
        return Page(panel);
    }

    private UIElement BuildHotkeyPage()
    {
        var panel = PageStack();
        panel.Children.Add(Body(L.S("assistants.intro")));

        // ListView als reiner Layout-Container. Reorder erfolgt über Pfeil-Buttons im Card-Header
        // (siehe BuildAssistantCard) — der wStreamAudio-CanReorderItems-Weg setzt XAML-DataTemplate
        // mit Bindings voraus, was bei der Schreibkraft komplexer Code-Behind-Card nicht ohne
        // ViewModel-Refactor funktioniert.
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
        };
        var itemStyle = new Style(typeof(ListViewItem));
        itemStyle.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 4)));
        itemStyle.Setters.Add(new Setter(Control.MinHeightProperty, 0.0));
        listView.ItemContainerStyle = itemStyle;

        _hotkeyPagePanel = listView;
        RenderAssistantCards(listView);
        panel.Children.Add(listView);

        var typePicker = new ComboBox
        {
            Header = L.S("assistants.new_type"),
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var mode in _profile.Modes)
        {
            typePicker.Items.Add(mode.Name);
        }
        if (typePicker.Items.Count > 0)
        {
            typePicker.SelectedIndex = 0;
        }

        var addButton = Button(L.S("assistants.add_button"), () =>
        {
            if (typePicker.SelectedIndex < 0 || typePicker.SelectedIndex >= _profile.Modes.Count)
            {
                return;
            }

            var template = _profile.Modes[typePicker.SelectedIndex];
            var newAssistant = new AssistantInstance
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = template.Mode,
                Name = template.Name,
                Hotkey = string.Empty,
                Prompt = template.DefaultPrompt,
                Intensity = Defaults.DefaultModeIntensity,
                WritingStyle = WritingStyle.Neutral
            };
            CaptureSettingsFromFields(SettingsCaptureScope.Assistants);
            _settings.Assistants.Add(newAssistant);
            RebuildHotkeyPage();
            ScrollToAssistant(newAssistant.Id);
        });
        addButton.VerticalAlignment = VerticalAlignment.Bottom;

        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("assistants.add_new")),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    ColumnSpacing = 12,
                    Children =
                    {
                        typePicker,
                        WithColumn(addButton, 1)
                    }
                }
            }
        }));

        var page = Page(panel);
        _hotkeyPageScroll = page as ScrollViewer;
        return page;
    }

    private void ScrollToAssistant(string assistantId)
    {
        if (_hotkeyPagePanel is null)
        {
            return;
        }

        if (!_names.TryGetValue(assistantId, out var anchor))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => anchor.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalAlignmentRatio = 0.0
        }));
    }

    private void RebuildHotkeyPage()
    {
        if (_hotkeyPagePanel is null)
        {
            return;
        }

        RenderAssistantCards(_hotkeyPagePanel);
    }

    // Drag&Drop wird seit dem Umstieg auf ListView nativ vom Control gehandhabt
    // (CanReorderItems + CanDragItems + AllowDrop). Die Sortierung wird im
    // DragItemsCompleted-Handler nach <see cref="OnAssistantsReordered"/> gespiegelt.

    private void RenderAssistantCards(ListView listPanel)
    {
        _hotkeyValues.Clear();
        _prompts.Clear();
        _names.Clear();
        _systemPromptOverrideCheckBoxes.Clear();
        _systemPrompts.Clear();
        _assistantInputLanguageOverrides.Clear();
        _assistantOutputLanguageOverrides.Clear();
        _policyTooltips.Clear();
        _intensitySliders.Clear();
        _intensityValues.Clear();
        _captureButtons.Clear();
        _writingStyles.Clear();
        _paragraphDensities.Clear();
        _emojiExpressions.Clear();
        _assistantTypes.Clear();
        _spellingSetHosts.Clear();

        listPanel.Items.Clear();
        if (_settings.Assistants.Count == 0)
        {
            listPanel.Items.Add(Body(L.S("assistants.empty")));
            return;
        }
        foreach (var assistant in _settings.Assistants.ToList())
        {
            listPanel.Items.Add(BuildAssistantCard(assistant));
        }
    }

    /// <summary>
    /// Verschiebt den Assistant um <paramref name="delta"/> Positionen (-1 = nach oben,
    /// +1 = nach unten) und rendert die Hotkey-Page neu. Wird von den ↑/↓-Buttons im
    /// Card-Header aufgerufen.
    /// </summary>
    private void MoveAssistant(string assistantId, int delta)
    {
        var index = _settings.Assistants.FindIndex(a => string.Equals(a.Id, assistantId, StringComparison.Ordinal));
        if (index < 0) return;
        var target = index + delta;
        if (target < 0 || target >= _settings.Assistants.Count) return;
        var item = _settings.Assistants[index];
        _settings.Assistants.RemoveAt(index);
        _settings.Assistants.Insert(target, item);
        RebuildHotkeyPage();
        _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
    }

    private UIElement BuildAssistantCard(AssistantInstance assistant)
    {
        var template = _profile.Modes.FirstOrDefault(m => m.Mode == assistant.Type);
        var typeLabel = template?.Name ?? assistant.Type.ToString();
        var assistantListIndex = _settings.Assistants.FindIndex(a =>
            string.Equals(a.Id, assistant.Id, StringComparison.Ordinal));

        var nameBox = new TextBox
        {
            Header = L.S("assistants.display_name"),
            Text = assistant.Name,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        nameBox.LostFocus += (_, _) => _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        _names[assistant.Id] = nameBox;

        var typeCombo = new ComboBox
        {
            Header = L.S("assistants.assistant_type"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 260
        };
        foreach (var mode in _profile.Modes)
        {
            typeCombo.Items.Add(mode.Name);
        }
        typeCombo.SelectedItem = template?.Name ?? _profile.Modes[0].Name;
        _assistantTypes[assistant.Id] = typeCombo;
        var assistantIdForType = assistant.Id;
        typeCombo.SelectionChanged += (_, _) =>
        {
            if (typeCombo.SelectedItem is not string selectedName)
            {
                return;
            }

            var modeDef = _profile.Modes.FirstOrDefault(m =>
                string.Equals(m.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (modeDef is null)
            {
                return;
            }

            var stored = _settings.Assistants.FirstOrDefault(a => string.Equals(a.Id, assistantIdForType, StringComparison.Ordinal));
            if (stored is null || stored.Type == modeDef.Mode)
            {
                return;
            }

            var previousType = stored.Type;
            var previousPrompt = _prompts.TryGetValue(assistantIdForType, out var promptBox)
                ? promptBox.Text
                : stored.Prompt;
            CaptureSettingsFromFields(SettingsCaptureScope.Assistants);
            if (AssistantPromptDefaults.IsKnownDefaultPrompt(_profile, previousType, previousPrompt))
            {
                stored.Prompt = modeDef.DefaultPrompt;
            }
            RebuildHotkeyPage();
            ScrollToAssistant(assistantIdForType);
            _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        };

        var hotkeyValue = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(assistant.Hotkey) ? "(kein Tastenkürzel)" : assistant.Hotkey,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _hotkeyValues[assistant.Id] = hotkeyValue;

        var captureButton = Button(L.S("assistants.hotkey.capture"), () => StartHotkeyCapture(assistant.Id));
        _captureButtons[assistant.Id] = captureButton;

        var initialIntensity = Math.Clamp(assistant.Intensity, Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
        var intensityValue = new TextBlock
        {
            Text = FormatIntensityValue(assistant.Type, initialIntensity),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            MinWidth = 170,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Right
        };
        var intensity = new Slider
        {
            Minimum = Defaults.MinModeIntensity,
            Maximum = Defaults.MaxModeIntensity,
            StepFrequency = 1,
            Value = initialIntensity,
            SmallChange = 1,
            LargeChange = 1,
            TickFrequency = 1,
            TickPlacement = Microsoft.UI.Xaml.Controls.Primitives.TickPlacement.BottomRight,
            SnapsTo = Microsoft.UI.Xaml.Controls.Primitives.SliderSnapsTo.StepValues,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var capturedType = assistant.Type;
        intensity.ValueChanged += (_, args) =>
        {
            intensityValue.Text = FormatIntensityValue(capturedType, (int)Math.Round(args.NewValue));
            _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        };
        _intensitySliders[assistant.Id] = intensity;
        _intensityValues[assistant.Id] = intensityValue;

        var prompt = new TextBox
        {
            Header = BuildPromptHeader(assistant),
            Text = assistant.Prompt,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120
        };
        prompt.LostFocus += (_, _) => _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        _prompts[assistant.Id] = prompt;

        // Sprachen (Overrides)
        var inputOverride = new ComboBox
        {
            Header = L.S("language.input.assistant"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 260
        };
        inputOverride.Items.Add(GlobalStandardLabel);
        foreach (var language in Defaults.InputLanguages.Select(l => l.Name))
        {
            inputOverride.Items.Add(language);
        }
        inputOverride.SelectedItem = string.IsNullOrWhiteSpace(assistant.InputLanguageOverride)
            ? GlobalStandardLabel
            : Defaults.LanguageName(assistant.InputLanguageOverride);
        inputOverride.SelectionChanged += (_, _) => _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        _assistantInputLanguageOverrides[assistant.Id] = inputOverride;

        var outputOverride = new ComboBox
        {
            Header = L.S("language.output.assistant"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 260
        };
        outputOverride.Items.Add(GlobalStandardLabel);
        foreach (var language in Defaults.OutputLanguages.Select(l => l.Name))
        {
            outputOverride.Items.Add(language);
        }
        outputOverride.SelectedItem = string.IsNullOrWhiteSpace(assistant.OutputLanguageOverride)
            ? GlobalStandardLabel
            : Defaults.LanguageName(assistant.OutputLanguageOverride);
        outputOverride.SelectionChanged += (_, _) => _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        _assistantOutputLanguageOverrides[assistant.Id] = outputOverride;

        // System-Prompt (Override)
        var systemPromptOverrideCheck = new CheckBox
        {
            Content = L.S("assistant.system_prompt_override"),
            IsChecked = !string.IsNullOrWhiteSpace(assistant.SystemPromptOverride),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _systemPromptOverrideCheckBoxes[assistant.Id] = systemPromptOverrideCheck;

        var systemPrompt = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 96,
            Text = string.IsNullOrWhiteSpace(assistant.SystemPromptOverride) ? _profile.SystemPrompt : assistant.SystemPromptOverride
        };
        systemPrompt.IsEnabled = systemPromptOverrideCheck.IsChecked == true;
        _systemPrompts[assistant.Id] = systemPrompt;

        void OnSystemPromptOverrideCheckChanged()
        {
            var useCustom = systemPromptOverrideCheck.IsChecked == true;
            systemPrompt.IsEnabled = useCustom;
            // Copy-on-enable: wenn leer, Standard übernehmen, damit das Feld nicht leer startet.
            if (useCustom && string.IsNullOrWhiteSpace(systemPrompt.Text))
            {
                systemPrompt.Text = _profile.SystemPrompt;
            }
            _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        }

        systemPromptOverrideCheck.Checked += (_, _) => OnSystemPromptOverrideCheckChanged();
        systemPromptOverrideCheck.Unchecked += (_, _) => OnSystemPromptOverrideCheckChanged();
        systemPrompt.LostFocus += (_, _) => _ = AutoPersistAsync(SettingsCaptureScope.Assistants);

        var assistantIdForDelete = assistant.Id;
        var deleteButton = CompactDeleteAssistantButton(async () =>
        {
            if (_settings.Assistants.Count <= 1)
            {
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(nameBox.Text) ? assistant.Name : nameBox.Text.Trim();
            if (!await ConfirmDeleteAssistantAsync(displayName))
            {
                return;
            }

            CaptureSettingsFromFields(SettingsCaptureScope.Assistants);
            _settings.Assistants.RemoveAll(a => a.Id == assistantIdForDelete);
            RebuildHotkeyPage();
            _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        });
        deleteButton.IsEnabled = _settings.Assistants.Count > 1;

        // Body panel: filled below. Visibility is driven by the Expander's IsExpanded property.
        var bodyPanel = new StackPanel { Spacing = 10 };

        // SettingsExpander aus dem CommunityToolkit liefert das gleiche Card-Design + Hover-Visual
        // wie SettingsCard (über die ganze Card, nicht nur über den Chevron). Items werden zwar
        // eingerückt gerendert; das gleichen wir am bodyPanel mit negativem Margin aus.
        var displayName = string.IsNullOrWhiteSpace(nameBox.Text) ? typeLabel : nameBox.Text;
        var subtitleText = string.IsNullOrWhiteSpace(assistant.Hotkey) ? typeLabel : $"{typeLabel} · {assistant.Hotkey}";
        // ↑/↓-Buttons für Reorder. Tapped wird mit Handled=true gestoppt, sonst bubblt
        // der Event zum SettingsExpander-Header und der toggelt zusätzlich beim Klick.
        var assistantIdForMove = assistant.Id;
        var upButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 12 },
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 36,
            MinHeight = 32,
        };
        ToolTipService.SetToolTip(upButton, "nach oben");
        upButton.Click += (_, _) => MoveAssistant(assistantIdForMove, -1);
        upButton.Tapped += (_, e) => e.Handled = true;

        var downButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 12 },
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 36,
            MinHeight = 32,
        };
        ToolTipService.SetToolTip(downButton, "nach unten");
        downButton.Click += (_, _) => MoveAssistant(assistantIdForMove, +1);
        downButton.Tapped += (_, e) => e.Handled = true;

        var moveButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { upButton, downButton },
        };

        // Body section: everything that is hidden when the card is collapsed.
        // Delete-Button als kleine Action-Leiste rechtsbündig oben — sichtbar nur im
        // aufgeklappten Zustand, ohne den engen Header zu überladen.
        var deleteActionRow = new Grid();
        deleteActionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        deleteActionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(deleteButton, 1);
        deleteActionRow.Children.Add(deleteButton);
        bodyPanel.Children.Add(deleteActionRow);

        bodyPanel.Children.Add(nameBox);
        bodyPanel.Children.Add(typeCombo);
        bodyPanel.Children.Add(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12,
            Children =
            {
                hotkeyValue,
                WithColumn(captureButton, 1)
            }
        });
        bodyPanel.Children.Add(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 12,
            Children =
            {
                inputOverride,
                WithColumn(outputOverride, 1)
            }
        });
        bodyPanel.Children.Add(systemPromptOverrideCheck);
        bodyPanel.Children.Add(systemPrompt);
        bodyPanel.Children.Add(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12,
            Children =
            {
                BuildIntensityWithPolicyTooltip(assistant, intensity),
                WithColumn(intensityValue, 1)
            }
        });

        // SettingsExpander aus dem CommunityToolkit — Hover-Visual über die GANZE Card (anders als
        // beim Standard-WinUI-Expander, der nur den Chevron-ToggleButton hot-bar macht). Header
        // und Description als Strings; das Toolkit-Template rendert sie mit BodyStrong + Caption.
        // ↑↓-Buttons sitzen im Content-Slot rechts (wie ToggleSwitch in wStreamAudio's LmsServerPage).
        var expander = new CommunityToolkit.WinUI.Controls.SettingsExpander
        {
            IsExpanded = assistant.IsExpanded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = displayName,
            Description = subtitleText,
            Content = moveButtons,
            Tag = assistant.Id,
        };
        // SettingsExpander hat kein eigenes Expanding/Collapsed-Event — wir hängen uns an
        // IsExpanded-DependencyProperty-Änderungen.
        expander.RegisterPropertyChangedCallback(
            CommunityToolkit.WinUI.Controls.SettingsExpander.IsExpandedProperty,
            (_, _) =>
            {
                if (assistant.IsExpanded == expander.IsExpanded) return;
                assistant.IsExpanded = expander.IsExpanded;
                _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
            });
        // Body in Items[0] mit negativem Margin: SettingsExpander rendert Items mit ~48px Indent
        // (für die hierarchischen Sub-Settings-Cards wie in wStreamAudio's LmsServerPage). Da wir
        // hier KEINE Hierarchie wollen, sondern bündigen Body, gleichen wir das Indent aus.
        var bodyHost = new CommunityToolkit.WinUI.Controls.SettingsCard
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ContentAlignment = CommunityToolkit.WinUI.Controls.ContentAlignment.Vertical,
            Content = bodyPanel,
            Margin = new Thickness(-48, 0, 0, 0),
        };
        expander.Items.Add(bodyHost);
        // Header bei Name-Änderung mit aktualisieren.
        nameBox.TextChanged += (_, _) =>
        {
            expander.Header = string.IsNullOrWhiteSpace(nameBox.Text) ? typeLabel : nameBox.Text;
        };

        if (ProfileSupportsWritingStyle())
        {
            var styleCombo = new ComboBox
            {
                Header = L.S("assistant.writing_style.header"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 260
            };
            foreach (var label in WritingStyleLabels.Values)
            {
                styleCombo.Items.Add(label);
            }
            styleCombo.SelectedItem = WritingStyleLabels[assistant.WritingStyle];
            _writingStyles[assistant.Id] = styleCombo;
            styleCombo.SelectionChanged += (_, _) =>
            {
                if (_policyTooltips.TryGetValue(assistant.Id, out var t))
                {
                    t.Content = BuildPolicyPreviewText(assistant.Id);
                }
                _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
            };
            bodyPanel.Children.Add(styleCombo);
        }

        var paragraphCombo = new ComboBox
        {
            Header = L.S("assistant.paragraphs.header"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 260
        };
        foreach (var label in ParagraphDensityLabels.Values)
        {
            paragraphCombo.Items.Add(label);
        }
        paragraphCombo.SelectedItem = ParagraphDensityLabels[assistant.ParagraphDensity];
        _paragraphDensities[assistant.Id] = paragraphCombo;
        paragraphCombo.SelectionChanged += (_, _) =>
        {
            if (_policyTooltips.TryGetValue(assistant.Id, out var t))
            {
                t.Content = BuildPolicyPreviewText(assistant.Id);
            }
            _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
        };
        bodyPanel.Children.Add(paragraphCombo);

        if (ProfileSupportsEmojiExpression())
        {
            var emojiCombo = new ComboBox
            {
                Header = L.S("assistant.emojis.header"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 260
            };
            foreach (var label in EmojiExpressionLabels.Values)
            {
                emojiCombo.Items.Add(label);
            }

            emojiCombo.SelectedItem = EmojiExpressionLabels[assistant.EmojiExpression];
            _emojiExpressions[assistant.Id] = emojiCombo;
            emojiCombo.SelectionChanged += (_, _) =>
            {
                if (_policyTooltips.TryGetValue(assistant.Id, out var t))
                {
                    t.Content = BuildPolicyPreviewText(assistant.Id);
                }
                _ = AutoPersistAsync(SettingsCaptureScope.Assistants);
            };
            bodyPanel.Children.Add(emojiCombo);
        }

        bodyPanel.Children.Add(BuildSpellingReplacementsSection(assistant));
        bodyPanel.Children.Add(prompt);

        // Drag&Drop wird von der ListView nativ über CanReorderItems + CanDragItems gemacht —
        // an der Card selbst kein CanDrag-Setup nötig.
        return expander;
    }

    private readonly Dictionary<string, Action> _spellingSetHosts = new();

    private void RefreshAllAssistantSpellingHosts()
    {
        foreach (var render in _spellingSetHosts.Values)
        {
            render();
        }
    }

    private FrameworkElement BuildSpellingReplacementsSection(AssistantInstance assistant)
    {
        var section = new StackPanel { Spacing = 6 };
        section.Children.Add(new TextBlock
        {
            Text = L.S("spelling.title"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var info = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12
        };
        section.Children.Add(info);

        var setsHost = new StackPanel { Spacing = 4 };
        section.Children.Add(setsHost);

        void Render()
        {
            setsHost.Children.Clear();
            var sets = _settings.SpellingCorrectionSets;
            if (sets is null || sets.Count == 0)
            {
                info.Text = L.S("spelling.assistant_section.empty");
                return;
            }

            info.Text = L.S("spelling.assistant_section.intro");

            foreach (var set in sets)
            {
                var checkbox = new CheckBox
                {
                    Content = string.IsNullOrWhiteSpace(set.Name) ? "(unbenannt)" : set.Name,
                    IsChecked = assistant.EnabledSpellingSetIds.Contains(set.Id)
                };
                checkbox.Checked += (_, _) =>
                {
                    if (!assistant.EnabledSpellingSetIds.Contains(set.Id))
                    {
                        assistant.EnabledSpellingSetIds.Add(set.Id);
                        _ = AutoPersistAsync(SettingsCaptureScope.None);
                    }
                };
                checkbox.Unchecked += (_, _) =>
                {
                    if (assistant.EnabledSpellingSetIds.Remove(set.Id))
                    {
                        _ = AutoPersistAsync(SettingsCaptureScope.None);
                    }
                };
                setsHost.Children.Add(checkbox);
            }
        }

        Render();
        _spellingSetHosts[assistant.Id] = Render;
        return section;
    }

    private FrameworkElement BuildSpellingRow(SpellingCorrectionSet set, SpellingReplacement item, int index, Action<int?> rerender)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        var fromBox = new TextBox
        {
            Text = item.From,
            PlaceholderText = L.S("spelling.row.from"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(fromBox, 0);
        fromBox.TextChanged += (_, _) =>
        {
            item.From = fromBox.Text;
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };

        var arrow = new TextBlock
        {
            Text = "→",
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.6,
            Margin = new Thickness(2, 0, 2, 0)
        };
        Grid.SetColumn(arrow, 1);

        var toBox = new TextBox
        {
            Text = item.To,
            PlaceholderText = L.S("spelling.row.to"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(toBox, 2);
        toBox.TextChanged += (_, _) =>
        {
            item.To = toBox.Text;
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        fromBox.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                toBox.Focus(FocusState.Programmatic);
            }
        };
        toBox.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                var newIndex = index + 1;
                set.Replacements.Insert(newIndex, new SpellingReplacement());
                rerender(newIndex);
                _ = AutoPersistAsync(SettingsCaptureScope.None);
            }
        };

        var removeButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 18 },
            Padding = new Thickness(10, 6, 10, 6),
            MinWidth = 44,
            MinHeight = 36
        };
        ToolTipService.SetToolTip(removeButton, L.S("spelling.row.delete_tooltip"));
        removeButton.Click += (_, _) =>
        {
            if (index >= 0 && index < set.Replacements.Count)
            {
                set.Replacements.RemoveAt(index);
                rerender(null);
                _ = AutoPersistAsync(SettingsCaptureScope.None);
            }
        };
        Grid.SetColumn(removeButton, 3);

        grid.Children.Add(fromBox);
        grid.Children.Add(arrow);
        grid.Children.Add(toBox);
        grid.Children.Add(removeButton);
        return grid;
    }

    private UIElement BuildSpellingPage()
    {
        var panel = PageStack();
        // Page header + intro inside its own card so the visual style matches Pipeline/Audio/General.
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Header(L.S("spelling.title")),
                Body(L.S("spelling.intro"))
            }
        }));

        var setsHost = new StackPanel { Spacing = 12 };
        panel.Children.Add(setsHost);

        void Render()
        {
            setsHost.Children.Clear();
            for (var i = 0; i < _settings.SpellingCorrectionSets.Count; i++)
            {
                var set = _settings.SpellingCorrectionSets[i];
                setsHost.Children.Add(BuildSpellingSetCard(set, Render));
            }

            if (_settings.SpellingCorrectionSets.Count == 0)
            {
                setsHost.Children.Add(Body(L.S("spelling.no_sets")));
            }
        }

        Render();

        var addSetBtn = new Button
        {
            Content = L.S("spelling.add_set"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        addSetBtn.Click += (_, _) =>
        {
            _settings.SpellingCorrectionSets.Add(new SpellingCorrectionSet { Name = L.S("spelling.new_set_default_name") });
            Render();
            RefreshAllAssistantSpellingHosts();
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        panel.Children.Add(addSetBtn);

        return panel;
    }

    private FrameworkElement BuildSpellingSetCard(SpellingCorrectionSet set, Action rerenderPage)
    {
        var cardPanel = new StackPanel { Spacing = 8 };

        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        var nameBox = new TextBox
        {
            Header = L.S("spelling.set_name"),
            Text = set.Name,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        nameBox.TextChanged += (_, _) =>
        {
            set.Name = nameBox.Text;
            RefreshAllAssistantSpellingHosts();
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        Grid.SetColumn(nameBox, 0);
        headerRow.Children.Add(nameBox);

        var deleteSetBtn = new Button
        {
            Content = L.S("spelling.delete_set"),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        deleteSetBtn.Click += (_, _) =>
        {
            _settings.SpellingCorrectionSets.Remove(set);
            foreach (var a in _settings.Assistants)
            {
                a.EnabledSpellingSetIds.Remove(set.Id);
            }
            rerenderPage();
            RefreshAllAssistantSpellingHosts();
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        Grid.SetColumn(deleteSetBtn, 1);
        headerRow.Children.Add(deleteSetBtn);

        cardPanel.Children.Add(headerRow);

        var rowsHost = new StackPanel { Spacing = 4 };
        cardPanel.Children.Add(rowsHost);

        void RenderRows(int? focusIdx = null)
        {
            rowsHost.Children.Clear();
            for (var i = 0; i < set.Replacements.Count; i++)
            {
                var idx = i;
                var item = set.Replacements[idx];
                rowsHost.Children.Add(BuildSpellingRow(set, item, idx, RenderRows));
            }

            if (focusIdx is int fi && fi >= 0 && fi < rowsHost.Children.Count)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (rowsHost.Children[fi] is Grid g && g.Children.Count > 0 && g.Children[0] is Control c)
                    {
                        c.Focus(FocusState.Programmatic);
                    }
                });
            }
        }

        RenderRows();

        var addRowBtn = new Button
        {
            Content = L.S("spelling.add_row"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        addRowBtn.Click += (_, _) =>
        {
            set.Replacements.Add(new SpellingReplacement());
            RenderRows(set.Replacements.Count - 1);
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        cardPanel.Children.Add(addRowBtn);

        // Glossary section: one proper name / term per line, passed to the AI as a correct-spelling reference.
        cardPanel.Children.Add(new TextBlock
        {
            Text = L.S("spelling.terms.title"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        });
        cardPanel.Children.Add(new TextBlock
        {
            Text = L.S("spelling.terms.intro"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12
        });
        // Comma-separated single-line list — simplest possible UI, no multi-line edge cases.
        // NOTE: Only LostFocus writes back. TextChanged is intentionally NOT wired up — WinUI
        // can fire TextChanged during initial layout with stale/empty text, which would wipe the
        // freshly loaded set.Terms when the page is first opened.
        var termsBox = new TextBox
        {
            Text = string.Join(", ", set.Terms),
            PlaceholderText = L.S("spelling.terms.placeholder"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        termsBox.LostFocus += (_, _) =>
        {
            set.Terms = termsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        cardPanel.Children.Add(termsBox);

        return Card(cardPanel);
    }

    private UIElement BuildIntensityWithPolicyTooltip(AssistantInstance assistant, Slider intensitySlider)
    {
        var label = new TextBlock
        {
            Text = IntensityLabel(assistant.Type),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        // SymbolIcon skaliert in StackPanels sonst oft falsch (riesiges Fragezeichen) — feste Kachel.
        var helpGlyph = new SymbolIcon(Symbol.Help);
        var info = new Viewbox
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(6, 0, 0, 0),
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = helpGlyph
        };
        var tooltip = new ToolTip { Content = BuildPolicyPreviewText(assistant.Id) };
        _policyTooltips[assistant.Id] = tooltip;
        ToolTipService.SetToolTip(info, tooltip);

        void UpdateTooltip()
        {
            if (_policyTooltips.TryGetValue(assistant.Id, out var t))
            {
                t.Content = BuildPolicyPreviewText(assistant.Id);
            }
        }

        intensitySlider.ValueChanged += (_, _) => UpdateTooltip();
        if (_assistantInputLanguageOverrides.TryGetValue(assistant.Id, out var inCombo)) inCombo.SelectionChanged += (_, _) => UpdateTooltip();
        if (_assistantOutputLanguageOverrides.TryGetValue(assistant.Id, out var outCombo)) outCombo.SelectionChanged += (_, _) => UpdateTooltip();

        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { label, info }
        };

        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                headerRow,
                intensitySlider
            }
        };
    }

    private string BuildPolicyPreviewText(string assistantId)
    {
        var assistant = _settings.Assistants.FirstOrDefault(a => string.Equals(a.Id, assistantId, StringComparison.Ordinal));
        if (assistant is null)
        {
            return string.Empty;
        }

        var effective = new AssistantInstance
        {
            Id = assistant.Id,
            Type = assistant.Type,
            Name = assistant.Name,
            Hotkey = assistant.Hotkey,
            Prompt = assistant.Prompt,
            Intensity = assistant.Intensity,
            WritingStyle = assistant.WritingStyle,
            ParagraphDensity = assistant.ParagraphDensity,
            EmojiExpression = assistant.EmojiExpression,
            SystemPromptOverride = assistant.SystemPromptOverride,
            InputLanguageOverride = assistant.InputLanguageOverride,
            OutputLanguageOverride = assistant.OutputLanguageOverride
        };

        if (_intensitySliders.TryGetValue(assistantId, out var intensity))
        {
            effective.Intensity = Math.Clamp((int)Math.Round(intensity.Value), Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
        }
        if (_writingStyles.TryGetValue(assistantId, out var style) && style.SelectedItem is string label)
        {
            var match = WritingStyleLabels.FirstOrDefault(pair => pair.Value == label);
            if (!string.IsNullOrEmpty(match.Value))
            {
                effective.WritingStyle = match.Key;
            }
        }
        if (_assistantInputLanguageOverrides.TryGetValue(assistantId, out var inCombo))
        {
            effective.InputLanguageOverride = SelectedAssistantLanguageOverride(inCombo, isInput: true);
        }
        if (_assistantOutputLanguageOverrides.TryGetValue(assistantId, out var outCombo))
        {
            effective.OutputLanguageOverride = SelectedAssistantLanguageOverride(outCombo, isInput: false);
        }
        if (_paragraphDensities.TryGetValue(assistantId, out var paragraphCombo) && paragraphCombo.SelectedItem is string pLabel)
        {
            var match = ParagraphDensityLabels.FirstOrDefault(pair => pair.Value == pLabel);
            if (!string.IsNullOrEmpty(match.Value))
            {
                effective.ParagraphDensity = match.Key;
            }
        }

        if (_emojiExpressions.TryGetValue(assistantId, out var emojiCombo) && emojiCombo.SelectedItem is string eLabel)
        {
            var match = EmojiExpressionLabels.FirstOrDefault(pair => pair.Value == eLabel);
            if (!string.IsNullOrEmpty(match.Value))
            {
                effective.EmojiExpression = match.Key;
            }
        }

        var effectiveInput = PromptComposition.EffectiveInputLanguage(_settings, effective);
        var effectiveOutput = PromptComposition.EffectiveOutputLanguage(_settings, effective);
        return PromptComposition.BuildPolicyBlock(_profile, effective, effectiveInput, effectiveOutput);
    }

    private static string? SelectedAssistantLanguageOverride(ComboBox combo, bool isInput)
    {
        var selected = combo.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected) || selected.Equals(GlobalStandardLabel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var list = isInput ? Defaults.InputLanguages : Defaults.OutputLanguages;
        return list.FirstOrDefault(language => string.Equals(language.Name, selected, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    private bool ProfileSupportsWritingStyle() =>
        !string.IsNullOrWhiteSpace(_profile.WritingStyleInstruction(WritingStyle.Neutral));

    private bool ProfileSupportsEmojiExpression() =>
        !string.IsNullOrWhiteSpace(_profile.EmojiExpressionInstruction(EmojiExpression.Balanced));

    private FrameworkElement BuildLanguageCard()
    {
        var stack = new StackPanel { Spacing = 8 };
        // Use the shared Header() helper so the font size matches every other card title (20pt).
        stack.Children.Add(Header(L.S("language.label")));

        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 240
        };
        combo.Items.Add(L.S("language.auto"));
        combo.Items.Add(L.S("language.english"));
        combo.Items.Add(L.S("language.german"));
        combo.SelectedIndex = _settings.UiLanguage switch
        {
            UiLanguage.English => 1,
            UiLanguage.German => 2,
            _ => 0
        };
        combo.SelectionChanged += (_, _) =>
        {
            var newLang = combo.SelectedIndex switch
            {
                1 => UiLanguage.English,
                2 => UiLanguage.German,
                _ => UiLanguage.Auto
            };
            if (newLang == _settings.UiLanguage)
            {
                return;
            }
            _settings.UiLanguage = newLang;
            L.Apply(newLang);
            ReapplyLanguage();
            _ = AutoPersistAsync(SettingsCaptureScope.None);
        };
        stack.Children.Add(combo);

        return Card(stack);
    }

    /// <summary>
    /// Re-labels live UI elements after a language switch and rebuilds the cached pages so the new
    /// strings show up immediately, without an app restart.
    /// </summary>
    private void ReapplyLanguage()
    {
        // Refresh navigation labels in place.
        var labels = new[]
        {
            (OverviewPage, "nav.overview"),
            (AudioLanguagePage, "nav.audio_language"),
            (PipelinePage, "nav.processing"),
            (HotkeyPage, "nav.assistants"),
            (SpellingPage, "nav.spelling"),
            (GeneralPage, "nav.general"),
            (DiagnosticsPage, "nav.diagnostics"),
            (AboutPage, "nav.about"),
        };
        foreach (var item in _navigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string tag)
            {
                var label = labels.FirstOrDefault(t => t.Item1 == tag);
                if (label.Item2 is not null)
                {
                    item.Content = L.S(label.Item2);
                }
            }
        }
        _navigation.OpenPaneLength = ComputeNavigationPaneWidth();

        // Re-label the readonly shared UI elements (constructed once with old culture).
        RelabelSharedFields();

        // Drop cached pages and rebuild the currently selected one.
        var currentTag = _settings.LastSelectedSettingsSection;
        ClearPageCache();
        Navigate(currentTag);
    }

    /// <summary>
    /// Updates Header/Content/PlaceholderText on the long-lived control instances that were
    /// constructed once with the previous UI language. Without this, a live language switch
    /// leaves those labels stuck in their old language until the app is restarted.
    /// </summary>
    private void RelabelSharedFields()
    {
        _statusInfo.Title = L.S("error.config");
        SetStatusMessage(L.S("status.please_check"));

        _sttProvider.Header = L.S("pipeline.provider");
        _sttModel.Header = L.S("pipeline.model");
        _sttModel.PlaceholderText = L.S("pipeline.model.suggest_stt");
        _sttApiKey.Header = null;
        SetApiKeyPlaceholders();
        _sttApiKeyReplace.Content = L.S("pipeline.api_key.replace");
        _sttEndpoint.Header = L.S("pipeline.custom_endpoint");
        _sttEndpoint.PlaceholderText = L.S("pipeline.custom_endpoint.placeholder_stt");

        _llmProvider.Header = L.S("pipeline.provider");
        _llmModel.Header = L.S("pipeline.model");
        _llmModel.PlaceholderText = L.S("pipeline.model.suggest_llm");
        _llmApiKey.Header = null;
        SetApiKeyPlaceholders();
        _llmApiKeyReplace.Content = L.S("pipeline.api_key.replace");
        _llmEndpoint.Header = L.S("pipeline.custom_endpoint");
        _llmEndpoint.PlaceholderText = L.S("pipeline.custom_endpoint.placeholder_llm");

        _audioInputDevice.Header = L.S("audio.source");
        _inputLanguage.Header = L.S("language.input");
        _outputLanguage.Header = L.S("language.output");
        _insertMethod.Header = L.S("pipeline.insert_method");
        _insertionTestTarget.PlaceholderText = L.S("overview.test_field.placeholder");
        _restoreClipboard.Content = L.S("pipeline.restore_clipboard");
        _playRecordingSounds.Content = L.S("general.play_sounds");
        _recordingSoundVolumeMinValue.Text = L.F("general.sound.volume.value", 0);
        _recordingSoundVolumeMaxValue.Text = L.F("general.sound.volume.value", 100);
        ToolTipService.SetToolTip(_testRecordingSound, L.S("general.sound.test"));
        _launchMinimized.Content = L.S("general.launch_minimized");
        _minimizeToTray.Content = L.S("general.minimize_to_tray");
        _autostart.Content = L.S("general.autostart");
        _maxSeconds.Header = L.S("general.max_recording_seconds");
        _timeoutSeconds.Header = L.S("general.timeout_seconds");
        _transcriptionRetriesOnFailure.Header = L.S("pipeline.retries");
        _llmRetriesOnFailure.Header = L.S("pipeline.retries");
        _keepHistory.Content = L.S("diagnostics.history.keep_label");

        // Refresh combos that hold localized item strings (InsertMethod is the main one).
        var oldInsertSelection = _insertMethod.SelectedIndex;
        _insertMethod.Items.Clear();
        foreach (var label in InsertMethodLabels.Values)
        {
            _insertMethod.Items.Add(label);
        }
        if (oldInsertSelection >= 0 && oldInsertSelection < _insertMethod.Items.Count)
        {
            _insertMethod.SelectedIndex = oldInsertSelection;
        }
    }

    private UIElement BuildGeneralPage()
    {
        var panel = PageStack();
        panel.Children.Add(BuildLanguageCard());
        panel.Children.Add(Card(Form(L.S("general.startup"), null,
            _autostart,
            _launchMinimized,
            _minimizeToTray,
            _playRecordingSounds,
            BuildRecordingSoundControls(),
            _maxSeconds,
            _timeoutSeconds)));
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("common.reset")),
                Body(L.S("general.reset.intro")),
                ActionRow(Button(L.S("general.reset"), ResetDefaults))
            }
        }));
        return Page(panel);
    }

    private UIElement BuildRecordingSoundControls()
    {
        var sliderRow = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _recordingSoundVolumeMinValue.VerticalAlignment = VerticalAlignment.Center;
        _recordingSoundVolumeMaxValue.VerticalAlignment = VerticalAlignment.Center;
        _recordingSoundVolume.VerticalAlignment = VerticalAlignment.Center;
        _testRecordingSound.VerticalAlignment = VerticalAlignment.Center;
        ToolTipService.SetToolTip(_testRecordingSound, L.S("general.sound.test"));

        sliderRow.Children.Add(_recordingSoundVolumeMinValue);
        sliderRow.Children.Add(WithColumn(_recordingSoundVolume, 1));
        sliderRow.Children.Add(WithColumn(_recordingSoundVolumeMaxValue, 2));
        sliderRow.Children.Add(WithColumn(_testRecordingSound, 3));

        _recordingSoundControls = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                Body(L.S("general.sound.volume")),
                sliderRow
            }
        };

        UpdateRecordingSoundControls();
        return _recordingSoundControls;
    }

    private UIElement BuildDiagnosticsPage()
    {
        RefreshDiagnostics();
        var panel = PageStack();
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Header(L.S("diagnostics.history.title")),
                Body(L.S("diagnostics.history.intro")),
                _keepHistory,
                ActionRow(Button(L.S("diagnostics.history.clear_now"), ClearProcessingHistory))
            }
        }));
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Header(L.S("diagnostics.title")),
                ActionRow(
                    Button(L.S("common.update"), RefreshDiagnostics),
                    Button(L.S("diagnostics.open_log"), OpenLogFile),
                    Button(L.S("diagnostics.copy"), CopyDiagnostics)),
                _diagnostics
            }
        }));
        return Page(panel);
    }

    private void ClearProcessingHistory()
    {
        _processingHistoryLog.Clear();
        RefreshDiagnostics();
        _statusService.SetStatus(TrayStatus.Idle, L.S("diagnostics.history.cleared"));
    }

    private void UpdateRecordingSoundControls()
    {
        var enabled = _playRecordingSounds.IsChecked == true;
        if (_recordingSoundControls is not null)
        {
            _recordingSoundControls.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        _recordingSoundVolume.IsEnabled = enabled;
        _testRecordingSound.IsEnabled = enabled;
    }

    private async Task TestRecordingSoundAsync()
    {
        if (_playRecordingSounds.IsChecked != true)
        {
            return;
        }

        await PersistAsync(SettingsCaptureScope.Shell);
        await _feedbackSoundService.PlayRecordingStartAsync();
    }

    private UIElement BuildAboutPage()
    {
        var panel = PageStack();
        // Basis-Block analog zu allen anderen Apps: Version-Card mit App-Name+Version, Copyright
        // als Description (kein eigener „Autor"-Eintrag, weil CopyrightText den Namen enthält).
        // Die folgenden Schreibkraft-spezifischen Karten (Open-Source / Feature-Scope / Privacy /
        // Tech) bleiben darunter erhalten — sie tragen App-eigene Infos, die andere Apps nicht haben.
        var versionCard = new CommunityToolkit.WinUI.Controls.SettingsCard
        {
            Header = "Version",
            Description = _profile.CopyrightText,
            Content = new TextBlock
            {
                Text = $"{_profile.AppName} {GetAppVersion()}",
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            },
        };
        panel.Children.Add(versionCard);

        panel.Children.Add(new CommunityToolkit.WinUI.Controls.SettingsCard
        {
            Header = L.S("about.license"),
            Description = _profile.LicenseName,
            Content = HyperlinkButton(L.S("about.open_license"), OpenLicense),
        });

        panel.Children.Add(new CommunityToolkit.WinUI.Controls.SettingsCard
        {
            Header = L.S("about.third_party.header"),
            Description = L.S("about.third_party.description"),
            Content = HyperlinkButton(L.S("about.third_party.open"), OpenThirdPartyNotices),
        });

        var localDataStack = new StackPanel { Spacing = 2 };
        localDataStack.Children.Add(new TextBlock
        {
            Text = "Settings: " + Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _profile.DataFolderName, "settings.json"),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            IsTextSelectionEnabled = true,
        });
        localDataStack.Children.Add(new TextBlock
        {
            Text = "Logs: " + Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _profile.DataFolderName, "logs"),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            IsTextSelectionEnabled = true,
        });
        panel.Children.Add(new CommunityToolkit.WinUI.Controls.SettingsCard
        {
            Header = L.S("about.local_data.header"),
            Description = L.S("about.local_data.description"),
            Content = localDataStack,
        });
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("about.tech.open_source")),
                Body(L.S("about.license.body")),
                Body(L.S("about.license.body2")),
                Body(L.S("about.license.body3"))
            }
        }));
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("overview.feature_scope")),
                Body(L.S("overview.feature_scope.body")),
                Body(L.S("overview.feature_scope.translation")),
                Body(L.S("overview.feature_scope.type"))
            }
        }));
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("about.privacy.title")),
                Body(L.S("about.privacy.body2")),
                Body(L.S("pipeline.api_key.note")),
                Body(L.S("about.privacy.body"))
            }
        }));
        panel.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Header(L.S("common.tech")),
                KeyValue("UI", L.S("about.tech.intro")),
                KeyValue(L.S("about.tech.runtime"), $".NET {Environment.Version}"),
                KeyValue(L.S("common.architecture"), L.S("about.tech.dependency")),
                ActionRow(
                    Button(L.S("about.open_license"), OpenLicense),
                    Button(L.S("about.third_party.open"), OpenThirdPartyNotices),
                    Button(L.S("diagnostics.open"), () => Navigate(DiagnosticsPage)))
            }
        }));
        return Page(panel);
    }

    private async Task LoadSettingsAsync()
    {
        ScheduleDeferredHiddenReassert();
        _settings = await _settingsService.LoadAsync();
        ReassertDeferredHiddenAfterLayout();
        ScheduleDeferredHiddenReassert();
        SelectCombo(_sttProvider, _settings.SttProvider);
        SelectEditableComboValue(_sttModel, _settings.SttModel, Defaults.OpenAiSttModels[0]);
        _sttEndpoint.Text = _settings.SttEndpointOverride ?? string.Empty;
        OnSttProviderChanged();
        SelectLanguage(_inputLanguage, _settings.InputLanguage);
        SelectLanguage(_outputLanguage, _settings.OutputLanguage);
        SelectAudioDevice(_settings.AudioInputDeviceId);
        SelectCombo(_llmProvider, _settings.LlmProvider);
        SelectEditableComboValue(_llmModel, _settings.LlmModel, Defaults.DefaultLlmModel);
        _llmEndpoint.Text = _settings.LlmEndpointOverride ?? string.Empty;
        OnLlmProviderChanged();
        SelectCombo(_insertMethod, InsertMethodLabel(_settings.InsertMethod));
        _restoreClipboard.IsChecked = _settings.RestoreClipboard;
        _keepHistory.IsChecked = _settings.KeepProcessingHistory;
        _launchMinimized.IsChecked = _settings.LaunchMinimizedToTray;
        _minimizeToTray.IsChecked = _settings.MinimizeToTray;
        _playRecordingSounds.IsChecked = _settings.PlayRecordingSounds;
        _recordingSoundVolume.Value = Math.Clamp(_settings.RecordingSoundVolumePercent, 0, 100);
        UpdateRecordingSoundControls();
        _autostart.IsChecked = _autostartService.IsEnabled();
        _maxSeconds.Text = _settings.RecordingMaxSeconds.ToString();
        _timeoutSeconds.Text = _settings.ProcessingTimeoutSeconds.ToString();
        _transcriptionRetriesOnFailure.Value = _settings.TranscriptionRetriesOnFailure;
        _llmRetriesOnFailure.Value = _settings.LlmRetriesOnFailure;
        _clipboardInsertRetriesOnFailure.Value = _settings.ClipboardInsertRetriesOnFailure;
        ApplyApiKeysFromSettings();
        RefreshDiagnostics();
        _settings.LastSelectedSettingsSection = NormalizeSectionTag(_settings.LastSelectedSettingsSection);
        SelectNavigationItemByTag(_settings.LastSelectedSettingsSection);
        Navigate(_settings.LastSelectedSettingsSection);
        _initialNavigationDone = true;
        WireAutoPersist();
        ReassertDeferredHiddenAfterLayout();
        ScheduleDeferredHiddenReassert();
    }

    /// <summary>
    /// Hängt LostFocus-/SelectionChanged-/Toggle-Handler an die relevanten
    /// Eingabefelder, damit Änderungen sofort gespeichert werden und der
    /// Tray-Status (Warnung L.S("status.setup_required")) ohne Neustart oder
    /// Navigationswechsel wegfällt, sobald alle Pflichtfelder gefüllt sind.
    /// </summary>
    private void WireAutoPersist()
    {
        if (_autoPersistWired)
        {
            return;
        }
        _autoPersistWired = true;

        void OnTextLost(object _, RoutedEventArgs __) => _ = AutoPersistAsync(SettingsCaptureScope.Shell);
        void OnSelectionChanged(object _, SelectionChangedEventArgs __) => _ = AutoPersistAsync(SettingsCaptureScope.Shell);
        void OnToggle(object _, RoutedEventArgs __) => _ = AutoPersistAsync(SettingsCaptureScope.Shell);
        void OnApiKeyPasswordChanged(PasswordBox box)
        {
            if (_suppressApiKeyDirty)
            {
                return;
            }

            if (ReferenceEquals(box, _llmApiKey))
            {
                if (_llmApiKeyEditing || !_settings.HasEncryptedLlmApiKey)
                {
                    _llmApiKeyDirty = true;
                }
            }
            else if (ReferenceEquals(box, _sttApiKey))
            {
                if (_sttApiKeyEditing || !_settings.HasEncryptedSttApiKey)
                {
                    _sttApiKeyDirty = true;
                }
            }
        }

        foreach (var tb in new Control[] { _llmApiKey, _sttApiKey, _maxSeconds, _timeoutSeconds })
        {
            tb.LostFocus += OnTextLost;
        }

        _llmApiKey.PasswordChanged += (_, _) => OnApiKeyPasswordChanged(_llmApiKey);
        _sttApiKey.PasswordChanged += (_, _) => OnApiKeyPasswordChanged(_sttApiKey);
        _llmApiKeyReplace.Click += (_, _) => BeginApiKeyReplacement(isLlm: true);
        _sttApiKeyReplace.Click += (_, _) => BeginApiKeyReplacement(isLlm: false);

        foreach (var cb in new[] { _sttProvider, _sttModel, _llmProvider, _llmModel,
                                   _audioInputDevice, _inputLanguage, _outputLanguage, _insertMethod })
        {
            cb.SelectionChanged += OnSelectionChanged;
        }

        void OnRetryValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            _ = AutoPersistAsync(SettingsCaptureScope.Shell);
        }

        _transcriptionRetriesOnFailure.ValueChanged += OnRetryValueChanged;
        _llmRetriesOnFailure.ValueChanged += OnRetryValueChanged;
        _clipboardInsertRetriesOnFailure.ValueChanged += OnRetryValueChanged;
        _recordingSoundVolume.ValueChanged += (_, _) =>
        {
            UpdateRecordingSoundControls();
            _ = AutoPersistAsync(SettingsCaptureScope.Shell);
        };
        _testRecordingSound.Click += async (_, _) => await TestRecordingSoundAsync();

        foreach (var chk in new[] { _restoreClipboard, _launchMinimized, _minimizeToTray, _playRecordingSounds, _autostart })
        {
            chk.Checked += OnToggle;
            chk.Unchecked += OnToggle;
        }
        _playRecordingSounds.Checked += (_, _) => UpdateRecordingSoundControls();
        _playRecordingSounds.Unchecked += (_, _) => UpdateRecordingSoundControls();

        void OnKeepHistoryToggle(object _, RoutedEventArgs __)
        {
            if (_keepHistory.IsChecked != true)
            {
                _processingHistoryLog.Clear();
            }
            RefreshDiagnostics();
            _ = AutoPersistAsync(SettingsCaptureScope.Shell);
        }
        _keepHistory.Checked += OnKeepHistoryToggle;
        _keepHistory.Unchecked += OnKeepHistoryToggle;
    }

    private async Task AutoPersistAsync(SettingsCaptureScope captureScope = SettingsCaptureScope.All)
    {
        if (!_initialNavigationDone || _isExiting)
        {
            return;
        }
        try
        {
            await PersistAsync(captureScope);
        }
        catch (Exception ex)
        {
            await _fileLogger.WriteAsync($"Settings save failed: {ex}");
            _statusService.SetStatus(TrayStatus.Error, SettingsSaveErrorMessage(ex));
        }
    }

    private void ApplyApiKeysFromSettings()
    {
        _suppressApiKeyDirty = true;
        try
        {
            // Gespeicherte API-Schlüssel nicht zurück in die UI laden. Die PasswordBox dient nur
            // zum Setzen/Ersetzen; vorhandene Schlüssel bleiben DPAPI-verschlüsselt in settings.json.
            _llmApiKey.Password = string.Empty;
            _sttApiKey.Password = string.Empty;
            _llmApiKeyEditing = false;
            _sttApiKeyEditing = false;
            SetApiKeyPlaceholders();
        }
        finally
        {
            _suppressApiKeyDirty = false;
            _llmApiKeyDirty = false;
            _sttApiKeyDirty = false;
        }
    }

    private async Task PersistAsync(SettingsCaptureScope captureScope = SettingsCaptureScope.All, string? successMessage = null)
    {
        await _persistGate.WaitAsync();
        try
        {
            CaptureSettingsFromFields(captureScope);
            if (captureScope is SettingsCaptureScope.Shell or SettingsCaptureScope.All)
            {
                // Leere Felder ohne Benutzeränderung an den Schlüsselfeldern dürfen die DPAPI-Werte
                // nicht löschen (z. B. Speichern von der Seite „Allgemein“, ohne Anbieter geöffnet zu haben).
                if (!string.IsNullOrWhiteSpace(_llmApiKey.Password))
                {
                    _settings.LlmApiKeyEncrypted = _secretProtector.Protect(_llmApiKey.Password.Trim());
                    _llmApiKeyDirty = false;
                    _llmApiKeyEditing = false;
                }

                if (!string.IsNullOrWhiteSpace(_sttApiKey.Password))
                {
                    _settings.SttApiKeyEncrypted = _secretProtector.Protect(_sttApiKey.Password.Trim());
                    _sttApiKeyDirty = false;
                    _sttApiKeyEditing = false;
                }
            }

            await _settingsService.SaveAsync(_settings);
            if (captureScope is SettingsCaptureScope.Shell or SettingsCaptureScope.All)
            {
                _autostartService.SetEnabled(_autostart.IsChecked == true);
            }

            var issues = captureScope is SettingsCaptureScope.Assistants or SettingsCaptureScope.All
                ? await _hotkeyService.RegisterAsync(_settings)
                : Array.Empty<ValidationIssue>();
            var readiness = _settingsService.Validate(_settings);
            if (issues.Count > 0)
            {
                _statusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("assistants.hotkey.register_failed"));
            }
            else if (!readiness.IsReady)
            {
                _statusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("status.setup_required.long"));
            }
            else if (_statusService.CurrentStatus is not TrayStatus.Recording and not TrayStatus.Processing)
            {
                _statusService.SetStatus(
                    TrayStatus.Idle,
                    !string.IsNullOrEmpty(successMessage)
                        ? successMessage
                        : L.S("status.ready.hint"));
            }

            await _fileLogger.WriteAsync(L.F("settings.saved.status", _statusService.CurrentStatus));
            if (!_isExiting)
            {
                if (captureScope is SettingsCaptureScope.Shell or SettingsCaptureScope.All)
                {
                    ApplyApiKeysFromSettings();
                }
                RefreshDiagnostics();
            }
        }
        finally
        {
            _persistGate.Release();
        }
    }

    private void SetApiKeyPlaceholders()
    {
        ApplyApiKeyFieldState(
            _llmApiKey,
            _llmApiKeyStatus,
            _llmApiKeyReplace,
            _settings.HasEncryptedLlmApiKey,
            _llmApiKeyEditing);
        ApplyApiKeyFieldState(
            _sttApiKey,
            _sttApiKeyStatus,
            _sttApiKeyReplace,
            _settings.HasEncryptedSttApiKey,
            _sttApiKeyEditing);
    }

    private static void ApplyApiKeyFieldState(PasswordBox box, TextBlock status, Button replaceButton, bool hasSavedKey, bool isEditing)
    {
        box.IsEnabled = !hasSavedKey || isEditing;
        box.PlaceholderText = hasSavedKey
            ? isEditing ? L.S("pipeline.api_key.new_placeholder") : L.S("pipeline.api_key.saved_placeholder")
            : L.S("pipeline.api_key.placeholder");
        status.Text = hasSavedKey
            ? L.S("pipeline.api_key.saved_status")
            : L.S("pipeline.api_key.missing_status");
        replaceButton.Content = L.S("pipeline.api_key.replace");
        replaceButton.IsEnabled = hasSavedKey && !isEditing;
    }

    private void BeginApiKeyReplacement(bool isLlm)
    {
        if (isLlm)
        {
            _llmApiKeyEditing = true;
            _llmApiKeyDirty = false;
            SetApiKeyPasswordWithoutDirtyFlag(_llmApiKey, string.Empty);
            SetApiKeyPlaceholders();
            _llmApiKey.Focus(FocusState.Programmatic);
            return;
        }

        _sttApiKeyEditing = true;
        _sttApiKeyDirty = false;
        SetApiKeyPasswordWithoutDirtyFlag(_sttApiKey, string.Empty);
        SetApiKeyPlaceholders();
        _sttApiKey.Focus(FocusState.Programmatic);
    }

    private void SetApiKeyPasswordWithoutDirtyFlag(PasswordBox box, string password)
    {
        _suppressApiKeyDirty = true;
        try
        {
            box.Password = password;
        }
        finally
        {
            _suppressApiKeyDirty = false;
        }
    }

    private void CaptureSettingsFromFields(SettingsCaptureScope scope)
    {
        if (scope is SettingsCaptureScope.None)
        {
            return;
        }

        if (scope is SettingsCaptureScope.Shell or SettingsCaptureScope.All)
        {
        _settings.SttProvider = Selected(_sttProvider, Defaults.OpenAiProviderName);
        _settings.SttModel = ReadEditableComboValue(_sttModel, Defaults.OpenAiSttModels[0]);
        _settings.SttEndpointOverride = string.IsNullOrWhiteSpace(_sttEndpoint.Text) ? null : _sttEndpoint.Text.Trim();
        _settings.InputLanguage = SelectedLanguage(_inputLanguage, "de");
        _settings.OutputLanguage = SelectedLanguage(_outputLanguage, Defaults.SameAsInputLanguageCode);
        _settings.AudioInputDeviceId = SelectedAudioDeviceId();
        _settings.LlmProvider = Selected(_llmProvider, Defaults.OpenAiProviderName);
        _settings.LlmModel = ReadEditableComboValue(_llmModel, Defaults.DefaultLlmModel);
        _settings.LlmEndpointOverride = string.IsNullOrWhiteSpace(_llmEndpoint.Text) ? null : _llmEndpoint.Text.Trim();
        _settings.InsertMethod = SelectedInsertMethod();
        _settings.RestoreClipboard = _restoreClipboard.IsChecked == true;
        _settings.KeepProcessingHistory = _keepHistory.IsChecked == true;
        _settings.LaunchMinimizedToTray = _launchMinimized.IsChecked == true;
        _settings.MinimizeToTray = _minimizeToTray.IsChecked == true;
        _settings.PlayRecordingSounds = _playRecordingSounds.IsChecked == true;
        _settings.RecordingSoundVolumePercent = (int)Math.Round(Math.Clamp(_recordingSoundVolume.Value, 0, 100));
        _settings.RecordingMaxSeconds = ParseBoundedInt(_maxSeconds.Text, 60, 1, 300);
        _settings.ProcessingTimeoutSeconds = ParseBoundedInt(_timeoutSeconds.Text, 45, 5, 180);
        _settings.TranscriptionRetriesOnFailure = ReadRetryCount(_transcriptionRetriesOnFailure);
        _settings.LlmRetriesOnFailure = ReadRetryCount(_llmRetriesOnFailure);
        _settings.ClipboardInsertRetriesOnFailure = ReadRetryCount(_clipboardInsertRetriesOnFailure);
        }

        if (scope is not (SettingsCaptureScope.Assistants or SettingsCaptureScope.All))
        {
            return;
        }

        foreach (var assistant in _settings.Assistants)
        {
            if (_names.TryGetValue(assistant.Id, out var nameBox) && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                assistant.Name = nameBox.Text.Trim();
            }

            if (_assistantTypes.TryGetValue(assistant.Id, out var typePicker) && typePicker.SelectedItem is string typeName)
            {
                var modeDef = _profile.Modes.FirstOrDefault(m =>
                    string.Equals(m.Name, typeName, StringComparison.OrdinalIgnoreCase));
                if (modeDef is not null)
                {
                    assistant.Type = modeDef.Mode;
                }
            }

            if (_prompts.TryGetValue(assistant.Id, out var prompt))
            {
                assistant.Prompt = prompt.Text;
            }

            if (_systemPromptOverrideCheckBoxes.TryGetValue(assistant.Id, out var systemPromptOverrideCheck)
                && _systemPrompts.TryGetValue(assistant.Id, out var systemPrompt))
            {
                assistant.SystemPromptOverride = systemPromptOverrideCheck.IsChecked == true
                    ? (string.IsNullOrWhiteSpace(systemPrompt.Text) ? null : systemPrompt.Text)
                    : null;
            }

            if (_assistantInputLanguageOverrides.TryGetValue(assistant.Id, out var inputOverride))
            {
                assistant.InputLanguageOverride = SelectedAssistantLanguageOverride(inputOverride, isInput: true);
            }
            if (_assistantOutputLanguageOverrides.TryGetValue(assistant.Id, out var outputOverride))
            {
                assistant.OutputLanguageOverride = SelectedAssistantLanguageOverride(outputOverride, isInput: false);
            }

            if (_intensitySliders.TryGetValue(assistant.Id, out var intensity))
            {
                assistant.Intensity = Math.Clamp((int)Math.Round(intensity.Value), Defaults.MinModeIntensity, Defaults.MaxModeIntensity);
            }

            if (_writingStyles.TryGetValue(assistant.Id, out var style) && style.SelectedItem is string label)
            {
                var match = WritingStyleLabels.FirstOrDefault(pair => pair.Value == label);
                if (!string.IsNullOrEmpty(match.Value))
                {
                    assistant.WritingStyle = match.Key;
                }
            }

            if (_paragraphDensities.TryGetValue(assistant.Id, out var paragraphCombo) && paragraphCombo.SelectedItem is string pLabel)
            {
                var pMatch = ParagraphDensityLabels.FirstOrDefault(pair => pair.Value == pLabel);
                if (!string.IsNullOrEmpty(pMatch.Value))
                {
                    assistant.ParagraphDensity = pMatch.Key;
                }
            }

            if (_emojiExpressions.TryGetValue(assistant.Id, out var emojiCombo) && emojiCombo.SelectedItem is string eLabel)
            {
                var eMatch = EmojiExpressionLabels.FirstOrDefault(pair => pair.Value == eLabel);
                if (!string.IsNullOrEmpty(eMatch.Value))
                {
                    assistant.EmojiExpression = eMatch.Key;
                }
            }
        }
    }

    private static SettingsCaptureScope CaptureScopeForPage(string? page) => NormalizeSectionTag(page) switch
    {
        HotkeyPage => SettingsCaptureScope.Assistants,
        OverviewPage or AudioLanguagePage or PipelinePage or GeneralPage => SettingsCaptureScope.Shell,
        _ => SettingsCaptureScope.None
    };

    private string FormatIntensityValue(AssistantMode type, int intensity) =>
        $"{intensity}/{Defaults.MaxModeIntensity} – {_profile.IntensityStepName(type, intensity)}";

    private string AssistantDisplayLabel(AssistantInstance a)
    {
        if (!string.IsNullOrWhiteSpace(a.Name))
        {
            return a.Name.Trim();
        }

        return _profile.Modes.FirstOrDefault(m => m.Mode == a.Type)?.Name ?? a.Type.ToString();
    }

    private static string FormatAssistantNameList(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return string.Empty;
        }

        var quoted = names.Select(n => $"„{n}“").ToList();
        return quoted.Count switch
        {
            1 => quoted[0],
            2 => $"{quoted[0]} und {quoted[1]}",
            _ => string.Join(", ", quoted.Take(quoted.Count - 1)) + " und " + quoted[^1]
        };
    }

    private void ResetDefaults()
    {
        SelectCombo(_sttProvider, Defaults.OpenAiProviderName);
        SelectEditableComboValue(_sttModel, Defaults.OpenAiSttModels[0], Defaults.OpenAiSttModels[0]);
        _sttEndpoint.Text = string.Empty;
        _llmEndpoint.Text = string.Empty;
        OnSttProviderChanged();
        OnLlmProviderChanged();
        SelectLanguage(_inputLanguage, "de");
        SelectLanguage(_outputLanguage, Defaults.SameAsInputLanguageCode);
        SelectAudioDevice(Defaults.DefaultAudioInputDeviceId);
        SelectCombo(_llmProvider, Defaults.OpenAiProviderName);
        SelectEditableComboValue(_llmModel, Defaults.DefaultLlmModel, Defaults.DefaultLlmModel);
        SelectCombo(_insertMethod, InsertMethodLabels[InsertMethod.SendInput]);
        _transcriptionRetriesOnFailure.Value = 0;
        _llmRetriesOnFailure.Value = 0;
        _clipboardInsertRetriesOnFailure.Value = 0;
        _launchMinimized.IsChecked = true;
        _minimizeToTray.IsChecked = true;
        _playRecordingSounds.IsChecked = true;
        _recordingSoundVolume.Value = 100;
        UpdateRecordingSoundControls();

        _settings.Assistants = _profile.CreateDefaultAssistants();
        RebuildHotkeyPage();

        _ = PersistAsync(SettingsCaptureScope.All, L.S("general.reset.done"));
    }

    private void StartHotkeyCapture(string assistantId)
    {
        _capturingAssistantId = assistantId;
        ResetCaptureButtons();
        if (_captureButtons.TryGetValue(assistantId, out var button))
        {
            button.Content = L.S("assistants.hotkey.prompt");
        }
        _statusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("assistants.hotkey.press_now"));
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_capturingAssistantId is not { } assistantId)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            _capturingAssistantId = null;
            ResetCaptureButtons();
            _statusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("assistants.hotkey.aborted"));
            e.Handled = true;
            return;
        }

        if (IsModifierKey(e.Key))
        {
            return;
        }

        var gesture = BuildGesture(e.Key);
        if (!HotkeyParser.TryParse(gesture, out var parsedGesture, out var error))
        {
            _statusService.SetStatus(TrayStatus.ConfigurationRequired, error);
            e.Handled = true;
            return;
        }

        var assistant = _settings.Assistants.FirstOrDefault(a => a.Id == assistantId);
        if (assistant is null)
        {
            _capturingAssistantId = null;
            ResetCaptureButtons();
            return;
        }

        var clearedAssistantLabels = new List<string>();
        foreach (var other in _settings.Assistants)
        {
            if (string.Equals(other.Id, assistant.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(other.Hotkey)
                || !HotkeyParser.TryParse(other.Hotkey, out var otherGesture, out _))
            {
                continue;
            }

            if (otherGesture != parsedGesture)
            {
                continue;
            }

            other.Hotkey = string.Empty;
            if (_hotkeyValues.TryGetValue(other.Id, out var clearedHotkeyBlock))
            {
                clearedHotkeyBlock.Text = "(kein Tastenkürzel)";
            }

            clearedAssistantLabels.Add(AssistantDisplayLabel(other));
        }

        assistant.Hotkey = gesture;
        if (_hotkeyValues.TryGetValue(assistantId, out var hotkeyValue))
        {
            hotkeyValue.Text = gesture;
        }
        _capturingAssistantId = null;
        ResetCaptureButtons();
        e.Handled = true;

        var successMessage = clearedAssistantLabels.Count == 0
            ? L.S("assistants.hotkey.applied")
            : L.F("assistants.hotkey.applied_cleared", FormatAssistantNameList(clearedAssistantLabels));
        _ = PersistAsync(SettingsCaptureScope.Assistants, successMessage);
    }

    private string BuildGesture(VirtualKey key)
    {
        var parts = new List<string>();
        if (IsPressed(0x11)) parts.Add("Ctrl");
        if (IsPressed(0x12)) parts.Add("Alt");
        if (IsPressed(0x10)) parts.Add("Shift");
        if (IsPressed(0x5B) || IsPressed(0x5C)) parts.Add("Win");
        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    private void ResetCaptureButtons()
    {
        foreach (var button in _captureButtons.Values)
        {
            button.Content = L.S("assistants.hotkey.capture");
        }
    }

    private async Task TestLlmConnectionAsync()
    {
        await PersistAsync(SettingsCaptureScope.Shell);
        var readiness = _settingsService.Validate(_settings);
        var issues = readiness.Issues
            .Where(issue => issue.Field.StartsWith("llm", StringComparison.OrdinalIgnoreCase) || issue.Field.StartsWith("stt", StringComparison.OrdinalIgnoreCase))
            .Where(issue => !(issue.Field == "llmApiKey" && !string.IsNullOrWhiteSpace(_llmApiKey.Password)))
            .Where(issue => !(issue.Field == "sttApiKey" && !string.IsNullOrWhiteSpace(_sttApiKey.Password)))
            .ToList();
        if (issues.Count > 0)
        {
            _statusService.SetStatus(TrayStatus.ConfigurationRequired, string.Join(" ", issues.Select(issue => issue.Message)));
            RefreshDiagnostics();
            return;
        }

        var keyPlain = !string.IsNullOrWhiteSpace(_llmApiKey.Password)
            ? _llmApiKey.Password.Trim()
            : _settings.HasEncryptedLlmApiKey
                ? _secretProtector.Unprotect(_settings.LlmApiKeyEncrypted!)
                : null;
        if (string.IsNullOrEmpty(keyPlain))
        {
            _statusService.SetStatus(TrayStatus.ConfigurationRequired, L.S("status.api_key_needed"));
            RefreshDiagnostics();
            return;
        }

        try
        {
            Defaults.TryGetLlmEndpoint(_settings.LlmProvider, _settings.LlmEndpointOverride, _settings.LlmModel, out var endpoint);
            await _llmService.ProcessAsync(
                new LlmRequest(L.S("pipeline.connection_test"), _profile.Modes[0].Mode, _settings.LlmProvider, endpoint, _settings.LlmModel, keyPlain, _profile.SystemPrompt, L.S("pipeline.connection_test.prompt")),
                CancellationToken.None);
            _statusService.SetStatus(TrayStatus.Idle, L.S("pipeline.connection_test.success"));
        }
        catch
        {
            _statusService.SetStatus(TrayStatus.Error, L.S("pipeline.connection_test.failure"));
        }

        RefreshDiagnostics();
    }

    private void OpenLogFile()
    {
        try
        {
            Directory.CreateDirectory(_settingsService.LogDirectory);
            if (!File.Exists(_fileLogger.CurrentLogPath))
            {
                File.WriteAllText(_fileLogger.CurrentLogPath, string.Empty);
            }

            Process.Start(new ProcessStartInfo(_fileLogger.CurrentLogPath) { UseShellExecute = true });
        }
        catch
        {
            _statusService.SetStatus(TrayStatus.Error, L.S("diagnostics.open_log.failure"));
        }
    }

    private void OpenThirdPartyNotices()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.md");
            if (!File.Exists(path))
            {
                _statusService.SetStatus(TrayStatus.Error, L.S("about.third_party.not_found"));
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            _statusService.SetStatus(TrayStatus.Error, L.S("about.third_party.open_failed"));
        }
    }

    private void OpenLicense()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "LICENSE");
            if (!File.Exists(path))
            {
                _statusService.SetStatus(TrayStatus.Error, L.S("about.license.not_found"));
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            _statusService.SetStatus(TrayStatus.Error, L.S("about.license.open_failed"));
        }
    }

    private void CopyDiagnostics()
    {
        try
        {
            var package = new DataPackage();
            package.SetText(_diagnostics.Text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            _statusService.SetStatus(TrayStatus.Idle, L.S("diagnostics.copy.success"));
        }
        catch
        {
            _statusService.SetStatus(TrayStatus.Error, L.S("diagnostics.copy.failure"));
        }
    }

    private void UpdateInfoBar(TrayStatus status, string message)
    {
        _statusInfo.Title = status switch
        {
            TrayStatus.Idle => L.S("status.ready"),
            TrayStatus.Paused => L.S("status.inactive"),
            TrayStatus.Recording => L.S("status.recording_in_progress"),
            TrayStatus.Processing => L.S("status.processing"),
            TrayStatus.Success => L.S("status.success"),
            TrayStatus.Error => L.S("status.error"),
            TrayStatus.Attention => L.S("common.note"),
            TrayStatus.ConfigurationRequired => L.S("error.config"),
            _ => L.S("common.note")
        };
        SetStatusMessage(message);
        _statusInfo.Severity = status switch
        {
            TrayStatus.Idle or TrayStatus.Success => InfoBarSeverity.Success,
            TrayStatus.Error => InfoBarSeverity.Error,
            TrayStatus.Processing or TrayStatus.Recording => InfoBarSeverity.Informational,
            TrayStatus.Attention => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Warning
        };
        // Kein kleines ProgressRing-Symbol im Kachelfeld: Segoe MDL2 wie bei den Navigations-Icons (ungeführt, größer).
        _statusInfo.IconSource = status switch
        {
            TrayStatus.Recording => new FontIconSource { Glyph = "\uE8B7", FontSize = 20 },
            TrayStatus.Processing => new FontIconSource { Glyph = "\uE8EF", FontSize = 20 },
            _ => null
        };
        RefreshDiagnostics();
    }

    private void SetStatusMessage(string message)
    {
        _statusInfo.Message = string.Empty;
        _statusMessageText.Text = message;
    }

    private string SettingsSaveErrorMessage(Exception exception)
    {
        var path = _settingsService.SettingsPath;
        return exception switch
        {
            UnauthorizedAccessException => L.F("error.settings_save.access", path),
            DirectoryNotFoundException => L.F("error.settings_save.path", path),
            IOException => L.F("error.settings_save.io", path),
            _ => L.F("error.settings_save.generic", path)
        };
    }

    private static NavigationViewItem NavItem(string text, string tag, string glyph) => new()
    {
        Content = text,
        Tag = tag,
        Icon = new FontIcon { Glyph = glyph }
    };

    private UIElement Page(UIElement content)
    {
        Detach(content);
        return new ScrollViewer
        {
            Content = content,
            Background = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(20, 16, 20, 28),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    /// <summary>
    /// Entfernt den Standard-Inhaltsrahmen der NavigationView (eigener Layer, Kartenstrich, abgeschnittene Ecken).
    /// </summary>
    private static void StripNavigationViewContentChrome(NavigationView navigationView)
    {
        navigationView.Resources["NavigationViewContentGridBorderBrush"] = new SolidColorBrush(Colors.Transparent);
        navigationView.Resources["NavigationViewContentGridBorderThickness"] = new Thickness(0);
        // Transparent lassen, damit der Mica-Backdrop des Fensters durchscheint
        // (sonst legt NavigationView einen opaken Theme-Brush über Mica = sichtbarer Kasten).
        navigationView.Resources["NavigationViewContentBackground"] = new SolidColorBrush(Colors.Transparent);
        navigationView.Resources["NavigationViewContentGridCornerRadius"] = new CornerRadius(0);
    }

    private static StackPanel PageStack() => new() { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Stretch };

    private double ComputeNavigationPaneWidth()
    {
        double max = 0;
        foreach (var item in _navigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Content is string label)
            {
                var tb = new TextBlock { Text = label, FontSize = 14 };
                tb.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                if (tb.DesiredSize.Width > max)
                {
                    max = tb.DesiredSize.Width;
                }
            }
        }
        // Glyph (~24) + Spacing zwischen Icon und Text (~12) + linkes/rechtes Padding (~40 gesamt) + Reserve.
        return Math.Max(188, max + 92);
    }

    // Card-Optik vereinheitlicht mit CommunityToolkit-SettingsCard (Win11-Standard, wie wStreamAudio).
    // Vorher: CornerRadius=12, Padding=22 — sah neben dem SettingsExpander der Hotkey-Page wie zwei
    // verschiedene Designsysteme aus.
    private static Border Card(UIElement child) => new()
    {
        Child = Detach(child),
        Padding = new Thickness(16),
        CornerRadius = new CornerRadius(4),
        Background = ResourceBrush("CardBackgroundFillColorDefaultBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(18, 128, 128, 128))),
        BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(72, 128, 128, 128))),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private static StackPanel Form(string title, string? description, params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(Header(title));
        if (!string.IsNullOrWhiteSpace(description))
        {
            panel.Children.Add(Body(description));
        }

        foreach (var child in children)
        {
            panel.Children.Add(Detach(child));
        }

        return panel;
    }

    private static StackPanel ActionRow(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var child in children)
        {
            panel.Children.Add(Detach(child));
        }

        return panel;
    }

    private static StackPanel ApiKeyEditor(string label, PasswordBox box, Button replaceButton, TextBlock status)
    {
        box.Header = null;
        replaceButton.VerticalAlignment = VerticalAlignment.Bottom;
        replaceButton.MinWidth = 150;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.Children.Add(box);
        row.Children.Add(WithColumn(replaceButton, 1));

        return new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                row,
                status
            }
        };
    }

    private static UIElement Detach(UIElement element)
    {
        if (element is not FrameworkElement frameworkElement || frameworkElement.Parent is not { } parent)
        {
            return element;
        }

        switch (parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Border border when ReferenceEquals(border.Child, element):
                border.Child = null;
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                contentControl.Content = null;
                break;
            case ScrollViewer scrollViewer when ReferenceEquals(scrollViewer.Content, element):
                scrollViewer.Content = null;
                break;
        }

        return element;
    }

    private static TextBlock Header(string text) => new() { Text = text, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };

    private static TextBlock Body(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.84 };

    private static Grid KeyValue(string key, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(170) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            ColumnSpacing = 12
        };
        grid.Children.Add(new TextBlock
        {
            Text = key,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        grid.Children.Add(WithColumn(new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.86
        }, 1));
        return grid;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static HyperlinkButton HyperlinkButton(string text, Action action)
    {
        var button = new HyperlinkButton { Content = text };
        button.Click += (_, _) => action();
        return button;
    }

        /// <summary>Delete-Button für Assistant-Karten: Icon (MDL2 Delete) + lokalisiertes Label.
    /// Wird im Body der Karte platziert — nur sichtbar, wenn die Karte aufgeklappt ist.</summary>
    private static Button CompactDeleteAssistantButton(Func<Task> action)
    {
        var label = new TextBlock
        {
            Text = L.S("assistants.delete"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var icon = new FontIcon
        {
            Glyph = "",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, label }
        };
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(12, 6, 14, 6)
        };
        ToolTipService.SetToolTip(button, L.S("assistants.delete"));
        button.Click += async (_, _) => await action();
        return button;
    }

    private FrameworkElement BuildPromptHeader(AssistantInstance assistant)
    {
        var label = new TextBlock
        {
            Text = L.S("assistant.instruction.placeholder"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var flyout = new MenuFlyout();
        var compatibleTemplates = _profile.PromptTemplates
            .Where(t => t.CompatibleModes.Count == 0 || t.CompatibleModes.Contains(assistant.Type))
            .ToList();

        if (compatibleTemplates.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = L.S("assistants.template.none"), IsEnabled = false });
        }
        else
        {
            foreach (var tpl in compatibleTemplates)
            {
                var item = new MenuFlyoutItem { Text = tpl.Name };
                if (!string.IsNullOrWhiteSpace(tpl.Description))
                {
                    ToolTipService.SetToolTip(item, tpl.Description);
                }
                var assistantId = assistant.Id;
                var capturedTpl = tpl;
                item.Click += async (_, _) => await ApplyPromptTemplateAsync(assistantId, capturedTpl);
                flyout.Items.Add(item);
            }
        }

        var dropDown = new DropDownButton
        {
            Content = L.S("assistants.template"),
            Flyout = flyout,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 2, 8, 2),
            MinHeight = 0,
            MinWidth = 0,
            FontSize = 12
        };
        ToolTipService.SetToolTip(dropDown, L.S("assistants.template.apply"));

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };
        grid.Children.Add(label);
        grid.Children.Add(WithColumn(dropDown, 1));
        return grid;
    }

    private async Task ApplyPromptTemplateAsync(string assistantId, PromptTemplate template)
    {
        if (!_prompts.TryGetValue(assistantId, out var box))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(box.Text)
            && !string.Equals(box.Text.Trim(), template.Text.Trim(), StringComparison.Ordinal))
        {
            if (!await ConfirmReplacePromptAsync())
            {
                return;
            }
        }

        box.Text = template.Text;
        await AutoPersistAsync(SettingsCaptureScope.Assistants);
    }

    private async Task<bool> ConfirmReplacePromptAsync()
    {
        if (Root.XamlRoot is null)
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            Title = L.S("assistants.template.replace.title"),
            Content = L.S("assistants.template.replace.body"),
            PrimaryButtonText = L.S("assistants.template.replace.button"),
            CloseButtonText = L.S("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Root.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmDeleteAssistantAsync(string displayName)
    {
        if (Root.XamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = L.S("assistants.delete.confirm.title"),
            Content = L.F("assistants.delete.confirm.body", displayName),
            PrimaryButtonText = L.S("common.delete"),
            CloseButtonText = L.S("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Root.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static UIElement WithColumn(FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        return element;
    }

    private static ComboBox Combo(string header) => new() { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 260 };

    private static TextBox TextField(string header, string placeholder) => new() { Header = header, PlaceholderText = placeholder, HorizontalAlignment = HorizontalAlignment.Stretch };

    private static NumberBox RetryCountNumberBox(string header) => new()
    {
        Header = header,
        Minimum = 0,
        Maximum = 5,
        SmallChange = 1,
        LargeChange = 1,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        HorizontalAlignment = HorizontalAlignment.Left,
        MinWidth = 140
    };

    private static int ReadRetryCount(NumberBox box) =>
        double.IsFinite(box.Value) ? Math.Clamp((int)Math.Round(box.Value), 0, 5) : 0;

    private void RefreshAudioDevices()
    {
        RefreshAudioDeviceList();
        SelectAudioDevice(_settings.AudioInputDeviceId);
        _statusService.SetStatus(TrayStatus.Attention, L.S("audio.devices_updated"));
    }

    private void RefreshAudioDeviceList()
    {
        var selectedId = SelectedAudioDeviceId();
        _audioInputDevice.Items.Clear();
        _audioDeviceIdsByDisplayName.Clear();
        foreach (var device in _audioDeviceService.GetInputDevices())
        {
            var displayName = DeviceDisplayName(device);
            _audioDeviceIdsByDisplayName[displayName] = device.Id;
            _audioInputDevice.Items.Add(displayName);
        }

        SelectAudioDevice(string.IsNullOrWhiteSpace(selectedId) ? Defaults.DefaultAudioInputDeviceId : selectedId);
    }

    private void SelectAudioDevice(string deviceId)
    {
        var match = _audioDeviceIdsByDisplayName
            .FirstOrDefault(pair => pair.Value.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
        _audioInputDevice.SelectedItem = string.IsNullOrWhiteSpace(match.Key) ? _audioInputDevice.Items.FirstOrDefault() : match.Key;
    }

    private string SelectedAudioDeviceId() =>
        _audioInputDevice.SelectedItem is string displayName && _audioDeviceIdsByDisplayName.TryGetValue(displayName, out var deviceId)
            ? deviceId
            : Defaults.DefaultAudioInputDeviceId;

    private string AudioDeviceName(string deviceId) =>
        _audioDeviceService.GetInputDevices()
            .FirstOrDefault(device => device.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase))?.Name
        ?? L.S("language.auto");

    private static string DeviceDisplayName(AudioInputDevice device) =>
        device.IsDefault ? L.S("language.auto") : $"{device.Name} ({device.Id})";

    private static void SelectLanguage(ComboBox combo, string languageCode)
    {
        combo.SelectedItem = Defaults.LanguageName(languageCode);
        if (combo.SelectedItem is null || !combo.Items.Contains(combo.SelectedItem))
        {
            combo.SelectedItem = combo.Items.FirstOrDefault();
        }
    }

    private static string SelectedLanguage(ComboBox combo, string fallback) =>
        Defaults.InputLanguages.Concat(Defaults.OutputLanguages)
            .FirstOrDefault(language => string.Equals(language.Name, combo.SelectedItem?.ToString(), StringComparison.OrdinalIgnoreCase))?.Code
        ?? fallback;

    private static void AddItems<T>(ComboBox combo, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            combo.Items.Add(item);
        }
    }

    private static void SelectCombo(ComboBox combo, string value)
    {
        combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(item => string.Equals(item.ToString(), value, StringComparison.OrdinalIgnoreCase)) ?? combo.Items.FirstOrDefault();
    }

    private void SelectEditableComboValue(ComboBox combo, string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            value = fallback;
        }

        var match = combo.Items.Cast<object>().FirstOrDefault(item =>
            string.Equals(item.ToString(), value, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = match;
        combo.Text = value;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            combo.Text = value;
            if (match is not null)
            {
                combo.SelectedItem = match;
            }
        });
    }

    private static string ReadEditableComboValue(ComboBox combo, string fallback)
    {
        if (combo.IsEditable)
        {
            var text = combo.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return combo.SelectedItem?.ToString()?.Trim() ?? fallback;
    }

    private static string Selected(ComboBox combo, string fallback) => combo.SelectedItem?.ToString() ?? fallback;

    private InsertMethod SelectedInsertMethod() =>
        _insertMethod.SelectedItem?.ToString() == InsertMethodLabels[InsertMethod.Clipboard]
            ? InsertMethod.Clipboard
            : InsertMethod.SendInput;

    private static string InsertMethodLabel(InsertMethod method) =>
        InsertMethodLabels.TryGetValue(method, out var label) ? label : InsertMethodLabels[InsertMethod.Clipboard];

    private static Style? ResourceStyle(string key) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var value) && value is Style style
            ? style
            : null;

    private static Brush ResourceBrush(string key, Brush? fallback = null) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : fallback ?? new SolidColorBrush(Colors.Transparent);

    private static string TitleFor(string tag) => tag switch
    {
        OverviewPage => L.S("page.title.overview"),
        PipelinePage => L.S("page.title.processing"),
        HotkeyPage => L.S("page.title.assistants"),
        SpellingPage => L.S("page.title.spelling"),
        GeneralPage => L.S("page.title.general"),
        DiagnosticsPage => L.S("page.title.diagnostics"),
        AudioLanguagePage => L.S("page.title.audio_language"),
        AboutPage => L.S("page.title.about"),
        _ => L.S("page.title.overview")
    };

    private async void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            var shouldPersist = _initialNavigationDone
                && !string.Equals(_settings.LastSelectedSettingsSection, tag, StringComparison.Ordinal);
            Navigate(tag);
            if (shouldPersist)
            {
                try
                {
                    await PersistAsync(SettingsCaptureScope.None);
                }
                catch (Exception ex)
                {
                    await _fileLogger.WriteAsync($"Settings save failed after navigation: {ex}");
                    _statusService.SetStatus(TrayStatus.Error, SettingsSaveErrorMessage(ex));
                }
            }
        }
    }

    private void SelectNavigationItemByTag(string tag)
    {
        foreach (var item in _navigation.MenuItems)
        {
            if (item is NavigationViewItem nav && nav.Tag is string t && t == tag)
            {
                _navigation.SelectedItem = item;
                return;
            }
        }

        if (_navigation.MenuItems.Count > 0)
        {
            _navigation.SelectedItem = _navigation.MenuItems[0];
        }
    }

    private static string NormalizeSectionTag(string? tag)
    {
        var t = tag switch
        {
            null or "" => OverviewPage,
            "Übersicht" => OverviewPage,
            "providers" => PipelinePage,
            "insert" => PipelinePage,
            _ => tag
        };

        return t is OverviewPage or AudioLanguagePage or PipelinePage or HotkeyPage or SpellingPage or GeneralPage or DiagnosticsPage or AboutPage
            ? t
            : OverviewPage;
    }

    private static string IntensityLabel(AssistantMode mode) => mode switch
    {
        AssistantMode.Transform => L.S("assistant.intensity.transform"),
        AssistantMode.Generate => L.S("assistant.intensity.generate"),
        AssistantMode.AnswerClipboard => L.S("assistant.intensity.answer"),
        _ => L.S("assistant.intensity.generic")
    };

    private static int ParseBoundedInt(string text, int fallback, int min, int max) =>
        int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;

    private static string SanitizeStatus(string message) =>
        message.Contains(Environment.NewLine, StringComparison.Ordinal) ? message.Split(Environment.NewLine)[0] : message;

    private static string FriendlyStatus(TrayStatus status) => status switch
    {
        TrayStatus.Idle => L.S("status.ready"),
        TrayStatus.Paused => L.S("status.inactive"),
        TrayStatus.Recording => L.S("status.recording"),
        TrayStatus.Processing => L.S("status.processing"),
        TrayStatus.Success => L.S("status.success"),
        TrayStatus.Error => L.S("status.error"),
        TrayStatus.Attention => L.S("common.note"),
        TrayStatus.ConfigurationRequired => L.S("error.config"),
        _ => status.ToString()
    };

    private static string GetAppVersion() =>
        GetExeProductVersion()
        ?? GetAssemblyInformationalVersion()
        ?? typeof(MainWindow).Assembly.GetName().Version?.ToString()
        ?? L.S("common.unknown");

    private static string? GetAssemblyInformationalVersion() =>
        typeof(MainWindow).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
            ?.InformationalVersion;

    private static string? GetExeProductVersion()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var v = FileVersionInfo.GetVersionInfo(path).ProductVersion;
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsModifierKey(VirtualKey key) =>
        key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
            or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static string KeyName(VirtualKey key) => key switch
    {
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((int)key - (int)VirtualKey.Number0).ToString(),
        >= VirtualKey.A and <= VirtualKey.Z => key.ToString(),
        >= VirtualKey.F1 and <= VirtualKey.F24 => key.ToString(),
        VirtualKey.Space => "Space",
        VirtualKey.Enter => "Enter",
        VirtualKey.Tab => "Tab",
        VirtualKey.Escape => "Esc",
        _ => key.ToString()
    };

    private static bool IsPressed(int key) => (GetKeyState(key) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("Comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);
}

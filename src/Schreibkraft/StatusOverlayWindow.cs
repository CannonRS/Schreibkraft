using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;
using Microsoft.UI.Text;
using Schreibkraft.Core;
using Windows.Foundation;
using Windows.UI;

namespace Schreibkraft;

/// <summary>
/// Nicht-fokussierbares Overlay-Fenster am oberen Bildschirmrand, das den Aufnahme- und
/// Verarbeitungsstatus in Echtzeit anzeigt. Komplett vektorbasiert (kein PNG-Hintergrund):
/// frosted-glass Acrylic-Material, glänzender Lichtreflex, runder Status-Badge mit Farbverlauf,
/// weicher Glow und Animationen. Verwendet WS_EX_NOACTIVATE, um den Fokus der aktiven
/// Anwendung nicht zu stehlen, und bleibt dauerhaft im Vordergrund (HWND_TOPMOST).
/// </summary>
internal sealed class StatusOverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaWindowCornerPreference = 33;
    // Dunkle Randfarbe (COLORREF 0x00BBGGRR) passend zur Kartenoberkante – lässt eine
    // evtl. system­gezeichnete 1px-Rahmenlinie mit dem Hintergrund verschmelzen.
    private const uint DwmBorderDark = 0x002C1C1A;
    private const int DwmwcpRound = 2;

    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    private const double OverlayWidth = 500;
    private const double OverlayHeight = 88;
    private const double TopMargin = 14;
    private const double BadgeSize = 46;

    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(2);

    private readonly Grid _root;
    private readonly Border _card;
    private readonly Ellipse _ambientAccent;
    private readonly Ellipse _ambientCool;
    private readonly TranslateTransform _cardTranslate;
    private readonly Grid _badge;
    private readonly Ellipse _glow;
    private readonly Ellipse _badgeFill;
    private readonly ShapePath _spinner;
    private readonly FontIcon _glyph;
    private readonly ScaleTransform _badgeScale;
    private readonly RotateTransform _spinnerRotate;
    private readonly TextBlock _statusText;
    private readonly TextBlock _timerText;

    private readonly Storyboard _entranceStoryboard;
    private readonly Storyboard _pulseStoryboard;
    private readonly Storyboard _spinStoryboard;

    private readonly DispatcherTimer _recordingTimer;
    private readonly DispatcherTimer _autoHideTimer;
    private DateTime _recordingStartTime;
    private bool _isVisible;

    /// <summary>
    /// Steuert, ob das Overlay bei Statusänderungen eingeblendet wird.
    /// Wenn false, wird das Overlay versteckt und neue Statusupdates werden ignoriert.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                Hide();
            }
        }
    }
    private bool _isEnabled = true;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public StatusOverlayWindow()
    {
        AppWindow.IsShownInSwitchers = false;
        ExtendsContentIntoTitleBar = true;

        // Deckend dunkler Hintergrund – garantiert randlos (kein durchschimmerndes Material).
        _root = new Grid { Background = new SolidColorBrush(Color.FromArgb(255, 13, 14, 26)) };

        // ----- Status-Badge (runder Kreis mit Farbverlauf + Glow + Icon) -----
        _glow = new Ellipse
        {
            Width = BadgeSize + 14,
            Height = BadgeSize + 14,
            Opacity = 0.5,
            IsHitTestVisible = false
        };

        _badgeFill = new Ellipse
        {
            Width = BadgeSize,
            Height = BadgeSize
        };

        // Glanzlicht oben links – lässt den Badge wie eine glänzende Kugel wirken.
        var specular = new Ellipse
        {
            Width = BadgeSize * 0.74,
            Height = BadgeSize * 0.46,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, BadgeSize * 0.14, 0, 0),
            IsHitTestVisible = false,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(190, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(40, 255, 255, 255), Offset = 0.7 },
                    new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 }
                }
            }
        };

        _spinner = new ShapePath
        {
            Width = BadgeSize + 8,
            Height = BadgeSize + 8,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Visibility = Visibility.Collapsed,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Data = BuildSpinnerArc((BadgeSize + 8) / 2, (BadgeSize + 2) / 2)
        };
        _spinnerRotate = new RotateTransform();
        _spinner.RenderTransform = _spinnerRotate;

        _glyph = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 20,
            Glyph = "",
            Foreground = new SolidColorBrush(Colors.White)
        };

        _badge = new Grid
        {
            Width = BadgeSize + 14,
            Height = BadgeSize + 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        _badgeScale = new ScaleTransform();
        _badge.RenderTransform = _badgeScale;
        _badge.Children.Add(_glow);
        _badge.Children.Add(_badgeFill);
        _badge.Children.Add(specular);
        _badge.Children.Add(_spinner);
        _badge.Children.Add(_glyph);
        Grid.SetColumn(_badge, 0);

        // ----- Text-Bereich -----
        _statusText = new TextBlock
        {
            Text = "Bereit",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(_statusText, 1);

        // ----- Timer (nur während Recording) -----
        _timerText = new TextBlock
        {
            Text = "00:00",
            FontSize = 15,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 2, 0),
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(_timerText, 2);

        var contentGrid = new Grid
        {
            Margin = new Thickness(12, 0, 18, 0),
            ColumnSpacing = 14,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        contentGrid.Children.Add(_badge);
        contentGrid.Children.Add(_statusText);
        contentGrid.Children.Add(_timerText);

        // ----- Ambiente: weiche, farbige Licht-Blobs für einen lebendigen Hintergrund -----
        _ambientAccent = new Ellipse
        {
            Width = 320,
            Height = 260,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-110, -150, 0, 0),
            IsHitTestVisible = false
        };
        _ambientCool = new Ellipse
        {
            Width = 360,
            Height = 280,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -120, -150),
            IsHitTestVisible = false
        };

        var cardContent = new Grid();
        cardContent.Children.Add(_ambientAccent);
        cardContent.Children.Add(_ambientCool);
        cardContent.Children.Add(contentGrid);

        // CornerRadius 0: die Rundung übernimmt komplett der DWM-Compositor – dadurch keine
        // durchscheinenden Ecken (weder hell noch schwarz).
        _card = new Border
        {
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
            Child = cardContent
        };
        _cardTranslate = new TranslateTransform();
        _card.RenderTransform = _cardTranslate;

        _root.Children.Add(_card);

        Content = _root;

        // ----- Storyboards -----
        _entranceStoryboard = BuildEntranceStoryboard();
        _pulseStoryboard = BuildPulseStoryboard();
        _spinStoryboard = BuildSpinStoryboard();

        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordingTimer.Tick += OnRecordingTimerTick;

        _autoHideTimer = new DispatcherTimer { Interval = AutoHideDelay };
        _autoHideTimer.Tick += OnAutoHideTimerTick;

        ApplyAccent(TrayStatus.Idle);
        _root.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var exStyle = GetWindowLong(hwnd, GwlExstyle);
        exStyle |= WsExNoactivate | WsExToolwindow | WsExTopmost;
        SetWindowLong(hwnd, GwlExstyle, exStyle);
        ApplyWindowChrome(hwnd);

        PositionOverlay();

        AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;
    }

    private static void ApplyWindowChrome(IntPtr hwnd)
    {
        var borderColor = DwmBorderDark;
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(uint));

        // Abgerundete Ecken im Windows-11-Stil (vom DWM-Compositor, kantengeglättet).
        var cornerPreference = DwmwcpRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
    }

    private void PositionOverlay()
    {
        var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

        var x = (displayArea.WorkArea.Width - (int)OverlayWidth) / 2 + displayArea.WorkArea.X;
        var y = displayArea.WorkArea.Y + (int)TopMargin;

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, (int)OverlayWidth, (int)OverlayHeight));
    }

    private void OnRecordingTimerTick(object? sender, object e)
    {
        var elapsed = DateTime.Now - _recordingStartTime;
        _timerText.Text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    private void OnAutoHideTimerTick(object? sender, object e)
    {
        _autoHideTimer.Stop();
        Hide();
    }

    public void UpdateStatus(TrayStatus status, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isEnabled)
            {
                return;
            }

            switch (status)
            {
                case TrayStatus.Recording:
                    ShowRecording(message);
                    break;

                case TrayStatus.Processing:
                    ShowProcessing(message);
                    break;

                case TrayStatus.Success:
                    ShowSuccess(message);
                    break;

                case TrayStatus.Error:
                    ShowError(message);
                    break;

                default:
                    Hide();
                    break;
            }
        });
    }

    private void ShowRecording(string message)
    {
        _autoHideTimer.Stop();
        _recordingStartTime = DateTime.Now;
        _timerText.Text = "00:00";
        _timerText.Visibility = Visibility.Visible;

        ApplyVisuals(TrayStatus.Recording, string.IsNullOrEmpty(message) ? "Aufnahme läuft…" : message);

        _recordingTimer.Start();
        Show();
    }

    private void ShowProcessing(string message)
    {
        _recordingTimer.Stop();
        _timerText.Visibility = Visibility.Collapsed;

        ApplyVisuals(TrayStatus.Processing, string.IsNullOrEmpty(message) ? "Verarbeitung…" : message);

        _autoHideTimer.Stop();
        Show();
    }

    private void ShowSuccess(string message)
    {
        _recordingTimer.Stop();
        _timerText.Visibility = Visibility.Collapsed;

        ApplyVisuals(TrayStatus.Success, string.IsNullOrEmpty(message) ? "Erfolgreich" : message);

        _autoHideTimer.Start();
        Show();
    }

    private void ShowError(string message)
    {
        _recordingTimer.Stop();
        _timerText.Visibility = Visibility.Collapsed;

        ApplyVisuals(TrayStatus.Error, string.IsNullOrEmpty(message) ? "Fehler" : message);

        _autoHideTimer.Start();
        Show();
    }

    private void ApplyVisuals(TrayStatus status, string text)
    {
        ApplyAccent(status);
        _statusText.Text = text;

        // Animationen je nach Status starten/stoppen.
        _pulseStoryboard.Stop();
        _spinStoryboard.Stop();
        _badgeScale.ScaleX = 1;
        _badgeScale.ScaleY = 1;
        _spinnerRotate.Angle = 0;

        switch (status)
        {
            case TrayStatus.Recording:
                _spinner.Visibility = Visibility.Collapsed;
                _pulseStoryboard.Begin();
                break;

            case TrayStatus.Processing:
                _spinner.Visibility = Visibility.Visible;
                _spinStoryboard.Begin();
                break;

            default:
                _spinner.Visibility = Visibility.Collapsed;
                break;
        }
    }

    /// <summary>
    /// Setzt alle status-abhängigen Farben (Badge-Verlauf, Glow, Glyphe, Karten-Tönung).
    /// </summary>
    private void ApplyAccent(TrayStatus status)
    {
        var (accent, glyph) = status switch
        {
            TrayStatus.Recording => (Color.FromArgb(255, 255, 77, 109), ""),            // Mikrofon, Rosé-Rot
            TrayStatus.Processing => (Color.FromArgb(255, 61, 169, 252), ""),           // Sync, Azur
            TrayStatus.Success => (Color.FromArgb(255, 43, 214, 106), ""),              // Häkchen, Grün
            TrayStatus.Error => (Color.FromArgb(255, 255, 90, 82), ""),                 // Fehler, Rot-Orange
            TrayStatus.ConfigurationRequired => (Color.FromArgb(255, 255, 176, 32), ""),// Zahnrad, Bernstein
            TrayStatus.Attention => (Color.FromArgb(255, 255, 176, 32), ""),            // Warnung, Bernstein
            TrayStatus.Paused => (Color.FromArgb(255, 150, 162, 184), ""),             // Pause, Schiefer
            _ => (Color.FromArgb(255, 124, 139, 161), "")
        };

        _glyph.Glyph = glyph;

        // Badge: glänzender Orb-Verlauf von oben-hell nach unten-dunkel (3D-Kugel-Anmutung).
        _badgeFill.Fill = new LinearGradientBrush
        {
            StartPoint = new Point(0.15, 0),
            EndPoint = new Point(0.85, 1),
            GradientStops =
            {
                new GradientStop { Color = Lighten(accent, 0.40), Offset = 0 },
                new GradientStop { Color = accent, Offset = 0.50 },
                new GradientStop { Color = Darken(accent, 0.38), Offset = 1 }
            }
        };
        // Dunkle Randkante unten gibt dem Badge Volumen.
        _badgeFill.Stroke = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = WithAlpha(Lighten(accent, 0.5), 110), Offset = 0 },
                new GradientStop { Color = WithAlpha(Darken(accent, 0.55), 160), Offset = 1 }
            }
        };
        _badgeFill.StrokeThickness = 1;

        // Weicher, farbiger Glow hinter dem Badge.
        _glow.Fill = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop { Color = WithAlpha(accent, 200), Offset = 0 },
                new GradientStop { Color = WithAlpha(accent, 0), Offset = 1 }
            }
        };

        _spinner.Stroke = new SolidColorBrush(Lighten(accent, 0.30));

        // Karte: dunkle Glas-Tönung mit deutlichem Hell-Dunkel-Verlauf für plastische Tiefe.
        var tintTop = BlendTowards(Color.FromArgb(255, 38, 41, 64), accent, 0.12);
        var tintBottom = BlendTowards(Color.FromArgb(255, 13, 14, 26), accent, 0.05);
        _card.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = tintTop, Offset = 0 },
                new GradientStop { Color = BlendTowards(tintTop, tintBottom, 0.5), Offset = 0.5 },
                new GradientStop { Color = tintBottom, Offset = 1 }
            }
        };

        // Ambiente: farbiger Licht-Pool in Akzentfarbe (links oben) + kühler Violett-Schimmer
        // (rechts unten) – verleiht dem Hintergrund Tiefe statt eines flachen Verlaufs.
        _ambientAccent.Fill = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop { Color = WithAlpha(Lighten(accent, 0.15), 120), Offset = 0 },
                new GradientStop { Color = WithAlpha(accent, 0), Offset = 1 }
            }
        };
        var cool = Color.FromArgb(255, 108, 77, 255);
        _ambientCool.Fill = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop { Color = WithAlpha(cool, 95), Offset = 0 },
                new GradientStop { Color = WithAlpha(cool, 0), Offset = 1 }
            }
        };
    }

    private void Show()
    {
        if (!_isVisible)
        {
            _isVisible = true;
            Activate();
            EnsureTopmost();
            _entranceStoryboard.Begin();
        }
        else
        {
            EnsureTopmost();
        }
    }

    private void EnsureTopmost()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0,
            SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
    }

    private void Hide()
    {
        if (_isVisible)
        {
            _isVisible = false;
            _pulseStoryboard.Stop();
            _spinStoryboard.Stop();
            AppWindow.Hide();
        }
    }

    // ----- Animationen -----

    private Storyboard BuildEntranceStoryboard()
    {
        var sb = new Storyboard();

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(220))
        };
        Storyboard.SetTarget(fade, _card);
        Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);

        var slide = new DoubleAnimation
        {
            From = -12,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(360)),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
        };
        Storyboard.SetTarget(slide, _cardTranslate);
        Storyboard.SetTargetProperty(slide, "Y");
        sb.Children.Add(slide);

        return sb;
    }

    private Storyboard BuildPulseStoryboard()
    {
        var sb = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(750));
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        var scaleX = new DoubleAnimation
        {
            From = 1.0,
            To = 1.09,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        Storyboard.SetTarget(scaleX, _badgeScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");
        sb.Children.Add(scaleX);

        var scaleY = new DoubleAnimation
        {
            From = 1.0,
            To = 1.09,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        Storyboard.SetTarget(scaleY, _badgeScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");
        sb.Children.Add(scaleY);

        var glow = new DoubleAnimation
        {
            From = 0.30,
            To = 0.85,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        Storyboard.SetTarget(glow, _glow);
        Storyboard.SetTargetProperty(glow, "Opacity");
        sb.Children.Add(glow);

        return sb;
    }

    private Storyboard BuildSpinStoryboard()
    {
        var sb = new Storyboard();
        var spin = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromMilliseconds(950)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(spin, _spinnerRotate);
        Storyboard.SetTargetProperty(spin, "Angle");
        sb.Children.Add(spin);
        return sb;
    }

    /// <summary>
    /// Erzeugt einen 270°-Bogen für den Verarbeitungs-Spinner.
    /// </summary>
    private static Geometry BuildSpinnerArc(double center, double radius)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(center, center - radius),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(center - radius, center),
            Size = new Size(radius, radius),
            IsLargeArc = true,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    // ----- Farb-Helfer -----

    private static Color Lighten(Color c, double amount) => BlendTowards(c, Colors.White, amount);

    private static Color Darken(Color c, double amount) => BlendTowards(c, Colors.Black, amount);

    private static Color BlendTowards(Color c, Color target, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            c.A,
            (byte)(c.R + (target.R - c.R) * amount),
            (byte)(c.G + (target.G - c.G) * amount),
            (byte)(c.B + (target.B - c.B) * amount));
    }

    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
}

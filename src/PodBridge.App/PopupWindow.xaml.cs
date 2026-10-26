using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using PodBridge.Core.Popup;

namespace PodBridge.App;

/// <summary>
/// The connect / case-open pop-up: a rounded card centred at the bottom of the screen that
/// rises out from behind the taskbar edge with a small spring overshoot. It shows the model
/// name and, for each component the model reports, a simple earbud / case drawing with a
/// battery icon and percentage; it follows the Windows light / dark app theme, then sinks
/// back down and fades out on its own.
/// <para>
/// The window's bottom edge is aligned to the bottom of the work area (the top of the
/// taskbar, wherever it is docked) and the card is moved with a transform inside the window,
/// so the window clips it at that edge: it slides out from behind the taskbar and can never
/// overlap it. Hovering keeps the card open; clicking dismisses it.
/// <see cref="Window.ShowActivated"/> is off so it never steals keyboard focus. It owns no
/// subscriptions — <see cref="PopupController"/> creates one per pop-up and closes it.
/// </para>
/// </summary>
public partial class PopupWindow : Window
{
    private const string ThemeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string LightThemeValue = "AppsUseLightTheme";

    private static readonly Duration RiseDuration = new(TimeSpan.FromMilliseconds(560));
    private static readonly Duration SinkDuration = new(TimeSpan.FromMilliseconds(260));
    private static readonly Duration FadeInDuration = new(TimeSpan.FromMilliseconds(200));
    private static readonly Duration FadeOutDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly TimeSpan DisplayTime = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _dismissTimer;
    private bool _isClosing;
    private bool _isClosed;

    /// <summary>Creates the pop-up for <paramref name="content"/>; call <see cref="Window.Show()"/> to display it.</summary>
    public PopupWindow(PopupContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        InitializeComponent();
        DataContext = content;
        SetTitle(content.Title);
        if (IsLightTheme())
        {
            ApplyLightTheme();
        }

        _dismissTimer = new DispatcherTimer { Interval = DisplayTime };
        _dismissTimer.Tick += (_, _) => BeginClose();

        Loaded += OnLoaded;
        MouseEnter += (_, _) => _dismissTimer.Stop();
        MouseLeave += (_, _) => RestartDismissTimer();
        MouseLeftButtonUp += (_, _) => BeginClose();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _dismissTimer.Stop();
        base.OnClosed(e);
    }

    // "Bryan Chuah's AirPods Pro": the owner's name in regular weight, then "AirPods Pro" in
    // bold. A title without "AirPods" (e.g. a generic model name) is shown bold throughout.
    private void SetTitle(string title)
    {
        TitleText.Inlines.Clear();

        var brandStart = title.IndexOf("AirPods", StringComparison.Ordinal);
        if (brandStart > 0)
        {
            TitleText.Inlines.Add(new Run(title[..brandStart]));
        }

        var boldPart = brandStart >= 0 ? title[brandStart..] : title;
        TitleText.Inlines.Add(new Run(boldPart) { FontWeight = FontWeights.Bold });
    }

    // Read-only check of the per-user "apps use light theme" setting; any failure to read it
    // falls back to the dark card.
    private static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeKeyPath);
            return key?.GetValue(LightThemeValue) is int value && value != 0;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    // Swap the dark brushes declared in XAML for their light equivalents; the XAML refers to
    // them with DynamicResource, so the card picks the new values up.
    private void ApplyLightTheme()
    {
        Resources["CardBackground"] = SolidBrush(0xFAFFFFFF);
        Resources["CardBorder"] = SolidBrush(0x1F000000);
        Resources["PrimaryText"] = SolidBrush(0xFF1C1C1E);
        Resources["BodyFill"] = SolidBrush(0xFFF7F7FA);
        Resources["BodyStroke"] = SolidBrush(0xFFC7C7CC);
        Resources["BatteryOutline"] = SolidBrush(0x99000000);
        Resources["CloseBackground"] = SolidBrush(0x14000000);
        Resources["CloseForeground"] = SolidBrush(0x99000000);
    }

    private static SolidColorBrush SolidBrush(uint argb)
        => new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

    // Centre the window at the bottom of the work area (its bottom edge on the taskbar's top
    // edge), then lift the card up into view from below that edge with a spring overshoot.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - ActualWidth) / 2);
        Top = workArea.Bottom - ActualHeight;

        var spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
        CardShift.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(ActualHeight, 0, RiseDuration) { EasingFunction = spring });
        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, FadeInDuration));

        _dismissTimer.Start();
    }

    private void RestartDismissTimer()
    {
        if (_isClosing || _isClosed)
        {
            return;
        }

        _dismissTimer.Stop();
        _dismissTimer.Start();
    }

    // Sink back below the taskbar edge and fade out, then really close. Safe to call
    // repeatedly or after the window has closed.
    private void BeginClose()
    {
        if (_isClosing || _isClosed)
        {
            return;
        }

        _isClosing = true;
        _dismissTimer.Stop();

        var fade = new DoubleAnimation(0, FadeOutDuration);
        fade.Completed += (_, _) =>
        {
            if (!_isClosed)
            {
                Close();
            }
        };

        CardShift.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(ActualHeight, SinkDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            });
        Card.BeginAnimation(OpacityProperty, fade);
    }
}

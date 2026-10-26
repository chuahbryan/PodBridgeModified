using System.Windows;
using System.Windows.Animation;
using System.Windows.Threading;
using PodBridge.Core.Popup;

namespace PodBridge.App;

/// <summary>
/// The connect / case-open pop-up: a small borderless, always-on-top, non-activating
/// card that slides up and fades in above the taskbar (bottom-right of the work area),
/// shows the model name and battery gauges, then slides down and fades out on its own.
/// Hovering keeps it open; clicking dismisses it. <see cref="Window.ShowActivated"/> is
/// off so it never steals keyboard focus from whatever the user is typing in. It owns no
/// subscriptions — <see cref="PopupController"/> creates one per pop-up and closes it.
/// </summary>
public partial class PopupWindow : Window
{
    private const double ScreenMargin = 12;  // gap from the work-area edge (above the taskbar)
    private const double SlideDistance = 28; // how far the card travels while fading

    private static readonly Duration ShowDuration = new(TimeSpan.FromMilliseconds(320));
    private static readonly Duration HideDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly TimeSpan DisplayTime = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _dismissTimer;
    private double _restTop;
    private bool _isClosing;
    private bool _isClosed;

    /// <summary>Creates the pop-up for <paramref name="content"/>; call <see cref="Window.Show()"/> to display it.</summary>
    public PopupWindow(PopupContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        InitializeComponent();
        DataContext = content;

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

    // Place the card in the bottom-right of the work area (which already excludes the
    // taskbar, wherever it is docked), then slide it up from below while fading in.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - ScreenMargin;
        _restTop = workArea.Bottom - ActualHeight - ScreenMargin;
        Top = _restTop;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(TopProperty, new DoubleAnimation(_restTop + SlideDistance, _restTop, ShowDuration)
        {
            EasingFunction = ease,
        });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ShowDuration));

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

    // Slide down + fade out, then really close. Safe to call repeatedly or after close.
    private void BeginClose()
    {
        if (_isClosing || _isClosed)
        {
            return;
        }

        _isClosing = true;
        _dismissTimer.Stop();

        var fade = new DoubleAnimation(0, HideDuration);
        fade.Completed += (_, _) =>
        {
            if (!_isClosed)
            {
                Close();
            }
        };

        BeginAnimation(TopProperty, new DoubleAnimation(_restTop + SlideDistance, HideDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        });
        BeginAnimation(OpacityProperty, fade);
    }
}

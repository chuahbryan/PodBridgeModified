using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PodBridge.Core.Popup;

namespace PodBridge.App;

/// <summary>
/// The connect / case-open pop-up, in the style of the iPhone AirPods card: a rounded dark
/// card centred at the bottom of the screen that rises out from behind the taskbar edge with
/// a small spring overshoot, shows an earbuds-and-case drawing, the model name and a battery
/// ring per component, then sinks back down and fades out on its own.
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

using System.Windows;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private BootstrapWindow? _bootstrapWindow;

    private void OnBootstrapStartup(object? sender, StartupEventArgs e)
    {
        try
        {
            _bootstrapWindow = new BootstrapWindow();
            _bootstrapWindow.Show();
            _bootstrapWindow.UpdateLayout();

            // Force one paint before the existing synchronous preflight work starts.
            // The normal dispatcher loop takes over as soon as startup returns.
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(CloseBootstrap));
        }
        catch
        {
            _bootstrapWindow = null;
        }
    }

    private void CloseBootstrap()
    {
        BootstrapWindow? window = _bootstrapWindow;
        _bootstrapWindow = null;
        if (window is null)
            return;

        try
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation(
                    window.Opacity,
                    0,
                    TimeSpan.FromMilliseconds(110));
                animation.Completed += (_, _) => window.Close();
                window.BeginAnimation(Window.OpacityProperty, animation);
            }
            else
            {
                window.Close();
            }
        }
        catch
        {
            try { window.Close(); } catch { }
        }
    }
}

using QuietNight.Night;
using QuietNight.Training;
using QuietNight.Ui;

namespace QuietNight;

public sealed class App : Application
{
    public App() => UserAppTheme = AppTheme.Dark;

    protected override Window CreateWindow(IActivationState? activationState) => new(Styled(new Shell
    {
        BackgroundColor = Theme.Ink,
        FlyoutBehavior = FlyoutBehavior.Disabled,
        Items =
        {
            new TabBar
            {
                Items =
                {
                    new ShellContent { Title = "Night", Icon = "tab_night.png", Route = "night", ContentTemplate = new DataTemplate(() => new NightPage()) },
                    new ShellContent { Title = "Teach", Icon = "tab_train.png", Route = "teach", ContentTemplate = new DataTemplate(() => new TrainPage()) },
                },
            },
        },
    }))
    {
        Title = "Quiet Night",
    };

    private static Shell Styled(Shell shell)
    {
        Shell.SetTabBarBackgroundColor(shell, Theme.Dusk);
        Shell.SetTabBarForegroundColor(shell, Theme.Lamplight);
        Shell.SetTabBarUnselectedColor(shell, Theme.Haze);
        Shell.SetTabBarTitleColor(shell, Theme.Lamplight);
        Shell.SetNavBarIsVisible(shell, false);
        return shell;
    }
}

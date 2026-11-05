using QuietNight.Night;
using QuietNight.Training;
using QuietNight.Ui;

namespace QuietNight;

public sealed class App : Application
{
    public App() => UserAppTheme = AppTheme.Dark;

    protected override Window CreateWindow(IActivationState? activationState) => new(Styled(new Shell
    {
        BackgroundColor = Theme.Background,
        FlyoutBehavior = FlyoutBehavior.Disabled,
        Items =
        {
            new TabBar
            {
                Items =
                {
                    new ShellContent { Title = "Night", Icon = "tab_night.png", Route = "night", ContentTemplate = new DataTemplate(() => new NightPage()) },
                    new ShellContent { Title = "Train", Icon = "tab_train.png", Route = "train", ContentTemplate = new DataTemplate(() => new TrainPage()) },
                },
            },
        },
    }))
    {
        Title = "Quiet Night",
    };

    private static Shell Styled(Shell shell)
    {
        Shell.SetTabBarBackgroundColor(shell, Theme.Surface);
        Shell.SetTabBarForegroundColor(shell, Theme.Accent);
        Shell.SetTabBarUnselectedColor(shell, Theme.Muted);
        Shell.SetTabBarTitleColor(shell, Theme.Accent);
        Shell.SetNavBarIsVisible(shell, false);
        return shell;
    }
}

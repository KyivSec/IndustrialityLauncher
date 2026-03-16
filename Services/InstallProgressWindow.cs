using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LauncherApp.Services;

public sealed class InstallProgressWindow : Window
{
    private readonly Grid RootGrid;
    private readonly Grid TitleBarGrid;
    private readonly TextBlock StageTextBlock;
    private readonly TextBlock MessageTextBlock;
    private readonly ProgressBar InstallProgressBar;

    public InstallProgressWindow()
    {
        Title = "Installing";
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };

        StageTextBlock = new TextBlock
        {
            Text = "Preparing...",
            FontSize = 20,
            Margin = new Thickness(0, 0, 0, 8)
        };

        MessageTextBlock = new TextBlock
        {
            Text = "Starting installation.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85,
            Margin = new Thickness(0, 0, 0, 16)
        };

        InstallProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 50,
            IsIndeterminate = false
        };

        TitleBarGrid = new Grid
        {
            Height = 40,
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        TitleBarGrid.Children.Add(new TextBlock
        {
            Text = "Installing",
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14
        });

        var ContentPanel = new StackPanel
        {
            Margin = new Thickness(24, 8, 24, 24),
            Children =
            {
                StageTextBlock,
                MessageTextBlock,
                InstallProgressBar
            }
        };

        RootGrid = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
        };

        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(TitleBarGrid, 0);
        RootGrid.Children.Add(TitleBarGrid);

        Grid.SetRow(ContentPanel, 1);
        RootGrid.Children.Add(ContentPanel);

        Content = RootGrid;

        ConfigureWindow();
    }

    private void ConfigureWindow()
    {
        if (AppWindow is null)
        {
            return;
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 220));

        if (AppWindow.TitleBar is not null)
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
            AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.White;

            AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(30, 255, 255, 255);
            AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(60, 255, 255, 255);

            AppWindow.TitleBar.BackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
        }

        SetTitleBar(TitleBarGrid);
    }

    public void UpdateProgress(LauncherProgress Progress)
    {
        StageTextBlock.Text = Progress.Stage;
        MessageTextBlock.Text = Progress.Message;
        InstallProgressBar.IsIndeterminate = Progress.Percent < 0;

        if (!InstallProgressBar.IsIndeterminate)
        {
            InstallProgressBar.Value = Progress.Percent;
        }
    }
}
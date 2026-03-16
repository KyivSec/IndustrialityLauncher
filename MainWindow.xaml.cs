using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;

namespace LauncherApp
{
    public sealed partial class MainWindow : Window
    {
        private UiSettings CurrentSettings = new();
        private LauncherService? LauncherService;
        private string SettingsFilePath = string.Empty;
        private bool IsLoadingSettings;
        private bool IsBusy;

        public MainWindow()
        {
            InitializeComponent();

            SetupBackdrop();
            SetupTitleBar();

            RootNavigationView.SelectedItem = ModpackNavItem;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            InitializeLauncherService();
            await LoadBackgroundImageAsync();
            await LoadSettingsAsync();
            UpdateUiState();
        }

        private void InitializeLauncherService()
        {
            var ServiceSettings = LauncherSettings.CreateDefault();
            LauncherService = new LauncherService(ServiceSettings);
            SettingsFilePath = LauncherService.SettingsFilePath;
        }

        private void SetupBackdrop()
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }

        private void SetupTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (AppWindow.TitleBar is AppWindowTitleBar TitleBar)
            {
                TitleBar.BackgroundColor = Colors.Transparent;
                TitleBar.InactiveBackgroundColor = Colors.Transparent;
                TitleBar.ForegroundColor = Colors.White;
                TitleBar.InactiveForegroundColor = Colors.White;
                TitleBar.ButtonBackgroundColor = Colors.Transparent;
                TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                TitleBar.ButtonForegroundColor = Colors.White;
                TitleBar.ButtonInactiveForegroundColor = Colors.White;
                TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(30, 255, 255, 255);
                TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(60, 255, 255, 255);
            }
        }

        private async Task LoadBackgroundImageAsync()
        {
            try
            {
                StorageFolder InstalledFolder = Package.Current.InstalledLocation;
                StorageFolder AssetsFolder = await InstalledFolder.GetFolderAsync("Assets");
                StorageFolder BackgroundsFolder = await AssetsFolder.GetFolderAsync("Backgrounds");
                var Files = await BackgroundsFolder.GetFilesAsync();

                StorageFile? FirstImageFile = Files
                    .Where(File => IsSupportedImageExtension(Path.GetExtension(File.Name)))
                    .OrderBy(File => File.Name)
                    .FirstOrDefault();

                string ImageUri = FirstImageFile is null
                    ? "ms-appx:///Assets/bg1.png"
                    : $"ms-appx:///Assets/Backgrounds/{FirstImageFile.Name}";

                BackgroundImage.Source = new BitmapImage(new Uri(ImageUri));
            }
            catch
            {
                BackgroundImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/bg1.png"));
            }
        }

        private static bool IsSupportedImageExtension(string Extension)
        {
            string Lower = Extension.ToLowerInvariant();
            return Lower == ".png" || Lower == ".jpg" || Lower == ".jpeg" || Lower == ".bmp";
        }

        private async Task LoadSettingsAsync()
        {
            if (string.IsNullOrWhiteSpace(SettingsFilePath))
            {
                return;
            }

            IsLoadingSettings = true;

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string Json = await File.ReadAllTextAsync(SettingsFilePath);
                    UiSettings? LoadedSettings = JsonSerializer.Deserialize(Json, LauncherJsonContext.Default.UiSettings);

                    if (LoadedSettings is not null)
                    {
                        CurrentSettings = LoadedSettings;
                    }
                }
            }
            catch
            {
                CurrentSettings = new UiSettings();
            }

            UsernameTextBox.Text = CurrentSettings.Username;
            MinRamNumberBox.Value = CurrentSettings.MinRamMb;
            MaxRamNumberBox.Value = CurrentSettings.MaxRamMb;

            IsLoadingSettings = false;
            ApplySettingsToService();
        }

        private async Task SaveSettingsAsync()
        {
            if (IsLoadingSettings || string.IsNullOrWhiteSpace(SettingsFilePath))
            {
                return;
            }

            int MinRamMb = Math.Max(512, (int)Math.Round(MinRamNumberBox.Value));
            int MaxRamMb = Math.Max(1024, (int)Math.Round(MaxRamNumberBox.Value));

            if (MinRamMb > MaxRamMb)
            {
                MaxRamMb = MinRamMb;
                MaxRamNumberBox.Value = MaxRamMb;
            }

            CurrentSettings = new UiSettings
            {
                Username = UsernameTextBox.Text?.Trim() ?? string.Empty,
                MinRamMb = MinRamMb,
                MaxRamMb = MaxRamMb
            };

            string Json = JsonSerializer.Serialize(CurrentSettings, LauncherJsonContext.Default.UiSettings);
            await File.WriteAllTextAsync(SettingsFilePath, Json);

            ApplySettingsToService();
        }

        private void ApplySettingsToService()
        {
            if (LauncherService is null)
            {
                return;
            }

            LauncherService.UpdateSettings(Settings =>
            {
                Settings.PlayerName = string.IsNullOrWhiteSpace(CurrentSettings.Username) ? "Player" : CurrentSettings.Username;
                Settings.MinRamMb = CurrentSettings.MinRamMb;
                Settings.MaxRamMb = CurrentSettings.MaxRamMb;
            });
        }

        private void UpdateUiState()
        {
            bool IsInstalled = LauncherService?.IsInstalled() ?? false;

            InstallButton.IsEnabled = !IsBusy && !IsInstalled;
            PlayButton.IsEnabled = !IsBusy && IsInstalled;
            UpdateButton.IsEnabled = !IsBusy;

            if (InstallButton.Content is StackPanel InstallPanel &&
                InstallPanel.Children.OfType<TextBlock>().LastOrDefault() is TextBlock InstallText)
            {
                InstallText.Text = IsInstalled ? "Installed" : (IsBusy ? "Working..." : "Install");
            }

            if (PlayButton.Content is StackPanel PlayPanel &&
                PlayPanel.Children.OfType<TextBlock>().LastOrDefault() is TextBlock PlayText)
            {
                PlayText.Text = IsBusy ? "Working..." : "Play";
            }

            if (UpdateButton.Content is StackPanel UpdatePanel &&
                UpdatePanel.Children.OfType<TextBlock>().LastOrDefault() is TextBlock UpdateText)
            {
                UpdateText.Text = IsBusy ? "Working..." : "Update";
            }
        }

        private void RootNavigationView_SelectionChanged(NavigationView Sender, NavigationViewSelectionChangedEventArgs Args)
        {
            NavigationViewItem? SelectedItem = Args.SelectedItem as NavigationViewItem;

            ModpackPage.Visibility = SelectedItem == ModpackNavItem ? Visibility.Visible : Visibility.Collapsed;
            SettingsScrollViewer.Visibility = SelectedItem == SettingsNavItem ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BackgroundHost_SizeChanged(object Sender, SizeChangedEventArgs E)
        {
            BackgroundClipRect.Rect = new Rect(0, 0, BackgroundHost.ActualWidth, BackgroundHost.ActualHeight);
        }

        private async void UsernameTextBox_TextChanged(object Sender, TextChangedEventArgs Args)
        {
            if (!IsLoadingSettings)
            {
                await SaveSettingsAsync();
            }
        }

        private async void RamNumberBox_ValueChanged(NumberBox Sender, NumberBoxValueChangedEventArgs Args)
        {
            if (!IsLoadingSettings)
            {
                await SaveSettingsAsync();
            }
        }

        private async void InstallButton_Click(object Sender, RoutedEventArgs E)
        {
            if (LauncherService is null || IsBusy)
            {
                return;
            }

            InstallProgressWindow? ProgressWindow = null;

            try
            {
                IsBusy = true;
                UpdateUiState();
                ApplySettingsToService();

                ProgressWindow = new InstallProgressWindow();
                ProgressWindow.Activate();

                var Progress = new Progress<LauncherProgress>(Value =>
                {
                    ProgressWindow.UpdateProgress(Value);
                });

                await LauncherService.InstallAsync(Progress);
            }
            catch (Exception Exception)
            {
                await ShowMessageAsync("Install Failed", Exception.Message);
            }
            finally
            {
                ProgressWindow?.Close();
                IsBusy = false;
                UpdateUiState();
            }
        }

        private async void PlayButton_Click(object Sender, RoutedEventArgs E)
        {
            if (LauncherService is null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                UpdateUiState();
                ApplySettingsToService();

                await LauncherService.PlayAsync();
            }
            catch (Exception Exception)
            {
                await ShowMessageAsync("Play Failed", Exception.Message);
            }
            finally
            {
                IsBusy = false;
                UpdateUiState();
            }
        }

        private async void UpdateButton_Click(object Sender, RoutedEventArgs E)
        {
            if (LauncherService is null || IsBusy)
            {
                return;
            }

            InstallProgressWindow? ProgressWindow = null;

            try
            {
                IsBusy = true;
                UpdateUiState();
                ApplySettingsToService();

                ProgressWindow = new InstallProgressWindow();
                ProgressWindow.Activate();

                var Progress = new Progress<LauncherProgress>(Value =>
                {
                    ProgressWindow.UpdateProgress(Value);
                });

                bool Updated = await LauncherService.UpdateModpackAsync(Progress);
                if (!Updated)
                {
                    await ShowMessageAsync("Update", "Modpack is already up to date.");
                }
            }
            catch (Exception Exception)
            {
                await ShowMessageAsync("Update Failed", Exception.Message);
            }
            finally
            {
                ProgressWindow?.Close();
                IsBusy = false;
                UpdateUiState();
            }
        }

        private void OpenFolderButton_Click(object Sender, RoutedEventArgs E)
        {
            LauncherService?.OpenRootFolder();
        }

        private async void DeleteModpackButton_Click(object Sender, RoutedEventArgs E)
        {
            if (LauncherService is null)
            {
                return;
            }

            ContentDialog Dialog = new()
            {
                Title = "Delete Modpack",
                Content = "This will delete the installed modpack files. Settings and Java will stay. Continue?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = RootNavigationView.XamlRoot
            };

            var Result = await Dialog.ShowAsync();
            if (Result != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                LauncherService.DeleteModpack();
                UpdateUiState();
            }
            catch (Exception Exception)
            {
                await ShowMessageAsync("Delete Modpack Failed", Exception.Message);
            }
        }

        private async Task ShowMessageAsync(string Title, string Content)
        {
            ContentDialog Dialog = new()
            {
                Title = Title,
                Content = Content,
                CloseButtonText = "OK",
                XamlRoot = RootNavigationView.XamlRoot
            };

            await Dialog.ShowAsync();
        }

    }
}

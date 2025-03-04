using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Valve.VR;

namespace HelseVestIKT_Dashboard
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        private string _currentTime = string.Empty;
        public string CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_currentTime != value)
                {
                    _currentTime = value;
                    OnPropertyChanged(nameof(CurrentTime));
                }
            }
        }

        // Deklarer volumeStatusTimer som nullable.
        private DispatcherTimer? volumeStatusTimer = null;

        public MainWindow()
        {
            InitializeComponent();
            // Set DataContext for bindings (for example, for CurrentTime)
            DataContext = this;
            this.Loaded += MainWindow_Loaded;

            // Åpner en konsoll for å vise utskrift (Brukes til testing av metoder)
            AllocConsole();

            // Oppdaterer CurrentTime hvert sekund
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("HH:mm");
            };
            timer.Start();

            EmbedSteamVRSpectator();

            //Test for å se at openVR kjører
            EVRInitError error = EVRInitError.None;
            CVRSystem vrSystem = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

            if (error != EVRInitError.None)
            {
                Console.WriteLine($"Feil ved initiering av OpenVR: {error}");
            }
            else
            {
                Console.WriteLine("OpenVR initialisert! Headset funnet.");
            }


        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            SteamApi steamApi = new SteamApi("3424AFEFF08E6EFC9084271524DDDFE2", "76561198071462154");
            await steamApi.GetSteamGamesAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Spill1_Click(object sender, RoutedEventArgs e)
        {
            // Hide header and game library, show the Return button in the toolbar.
            HeaderGrid.Visibility = Visibility.Collapsed;
            GameLibraryScrollViewer.Visibility = Visibility.Collapsed;
            ReturnButton.Visibility = Visibility.Visible;
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            // Restore header and game library, hide the Return button.
            HeaderGrid.Visibility = Visibility.Visible;
            GameLibraryScrollViewer.Visibility = Visibility.Visible;
            ReturnButton.Visibility = Visibility.Collapsed;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            HeaderGrid.Visibility = Visibility.Visible;
            GameLibraryScrollViewer.Visibility = Visibility.Visible;
            ResetButton.Visibility = Visibility.Visible;
        }

        private void KalibreringButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle status text between "Tilkoblet" and "Frakoblet"
            if (StatusTextBlock.Text == "Tilkoblet")
                StatusTextBlock.Text = "Frakoblet";
            else
                StatusTextBlock.Text = "Tilkoblet";
        }

        private void GlassesButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle glasses button click
        }

        private void LeftHandButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle left hand button click
        }

        private void RightHandButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle right hand button click
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowStyle == WindowStyle.None && this.WindowState == WindowState.Maximized)
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Søk etter spill...")
                SearchBox.Text = "";
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
                SearchBox.Text = "Søk etter spill...";
        }

        // Ny event handler for volumslideren
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeStatusTextBlock == null)
                return;

            VolumeStatusTextBlock.Text = $"{(int)e.NewValue}%";
            VolumeStatusTextBlock.Visibility = Visibility.Visible;

            if (volumeStatusTimer == null)
            {
                volumeStatusTimer = new DispatcherTimer();
                volumeStatusTimer.Interval = TimeSpan.FromSeconds(2);
                volumeStatusTimer.Tick += (s, args) =>
                {
                    VolumeStatusTextBlock.Visibility = Visibility.Collapsed;
                    volumeStatusTimer?.Stop();
                };
            }
            else
            {
                volumeStatusTimer.Stop();
            }
            volumeStatusTimer.Start();
        }

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            // Create and display the log window modally
            LogWindow logWindow = new LogWindow();
            logWindow.ShowDialog();
        }

        private void LaunchGameButton_Click(object sender, RoutedEventArgs e)
        {
            SteamLauncher.LaunchSteamGame("70");
            // Hide header and game library, show the Return button in the toolbar.
            HeaderGrid.Visibility = Visibility.Collapsed;
            GameLibraryScrollViewer.Visibility = Visibility.Collapsed;

            if (SteamGameImage != null)
            {
                SteamGameImage.Visibility = Visibility.Collapsed;
            }
            ReturnButton.Visibility = Visibility.Visible;

        }


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Replace "123456" with the actual Steam AppID.
            BitmapImage? gameImage = await SteamImageHelper.GetSteamGameImageAsync("70");
            if (gameImage != null)
            {
                SteamGameImage.Source = gameImage;

                SteamGameImage.Visibility = Visibility.Visible;

            }

        }

        private void EmbedSteamVRSpectator()
        {
            // Hent vinduet for SteamVR spectator
            IntPtr spectatorHandle = FindWindow(null, "VR-visning");

            if (spectatorHandle != IntPtr.Zero)
            {
                // Sett spectator-vinduet inn i vår WPF-app
                var helper = new WindowInteropHelper(this);
                SetParent(spectatorHandle, helper.Handle);
            }
            else
            {
                System.Windows.MessageBox.Show("Kunne ikke finne SteamVR Spectator-vinduet.");
            }
        }

    }
}

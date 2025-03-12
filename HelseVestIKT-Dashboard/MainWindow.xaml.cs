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
using MessageBox = System.Windows.MessageBox;  
using Button = System.Windows.Controls.Button;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using ThreadingTimer = System.Threading.Timer;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Net.NetworkInformation;
using SimpleWifi;
using System.Linq;

namespace HelseVestIKT_Dashboard
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

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

        private CVRSystem? vrSystem;

        public ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();
        public object? WifiConnectionButton { get; private set; }

        private bool _gamesLoaded = false;

        public VREquipmentStatusViewModel VREquipmentStatus { get; set; } = new VREquipmentStatusViewModel();
        private DispatcherTimer vrStatusTimer;

        private ThreadingTimer? _vrStatusTimer;

        public ImageSource VolumeIcon => StockIcons.GetVolumeIcon();

        private DispatcherTimer _wifiSignalTimer;


        public MainWindow()
        {
            InitializeComponent();
            StartMonitoringWifiSignal();
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

            _vrStatusTimer = new ThreadingTimer(VRStatusCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            EmbedSteamVRSpectatorAsync();
            InitializeOpenVR();
            StartVRStatusTimer();

 
        }

        #region Wifi Connection

        private void StartMonitoringWifiSignal()
        {
            _wifiSignalTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2) // update every 2 seconds
            };
            _wifiSignalTimer.Tick += WifiSignalTimer_Tick;
            _wifiSignalTimer.Start();
        }

		private void UpdateWifiIcon(uint signalStrength)
		{
			string iconPath;

			if (signalStrength >= 80)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_4.png";
			else if (signalStrength >= 60)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_3.png";
			else if (signalStrength >= 40)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_2.png";
			else if (signalStrength >= 20)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_1.png";
			else
				iconPath = "pack://application:,,,/Bilder/wifi_bar_0.png";


			// Update the Image control source
			WifiSignalIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
		}

		private void WifiSignalTimer_Tick(object sender, EventArgs e)
		{
			Wifi wifi = new Wifi();
			var accessPoints = wifi.GetAccessPoints();
			var connected = accessPoints.FirstOrDefault(ap => ap.IsConnected);

			if (connected != null)
			{
				// Cast the uint signal strength to int
				int signalQuality = (int)connected.SignalStrength;
				// Update text indicator if needed

				// Update the icon based on the signal strength
				UpdateWifiIcon(connected.SignalStrength);
			}
			else
			{
				WifiSignalTextBlock.Text = "No Connection";
				// Optionally show the "no signal" icon
				UpdateWifiIcon(0);
			}
		}


		#endregion

		private void InitializeOpenVR()
        {
            EVRInitError initError = EVRInitError.None;
            vrSystem = OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Scene);
            if (initError != EVRInitError.None)
            {
                string errorMessage = OpenVR.GetStringForHmdError(initError);
                System.Windows.MessageBox.Show("OpenVR Initialization failed: " + errorMessage);
                // Optionally, close the application:
                //this.Close();
            }
            //Test for å se at openVR kjører
            EVRInitError error = EVRInitError.None;
            vrSystem = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

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
            if (_gamesLoaded)
                return;

            _gamesLoaded = true;
            
            SteamApi steamApi = new SteamApi("384082C6759AAF7B6974A9CCE1ECF6CE", "76561198081888308");
            var fetchedGames = await steamApi.GetSteamGamesAsync();
            foreach (var game in fetchedGames)
            {
                Games.Add(game);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
		{
			Console.WriteLine("Settings clicked");
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

  

        //VR Status: Henter informasjon om headset og kontrollere fra SteamAPI.

        private void VRStatusCallback(object? state)
		{
			UpdateVREquipmentStatus();

            Dispatcher.Invoke(() =>
			{
				OnPropertyChanged(nameof(VREquipmentStatus));
			});
		}

		private void StartVRStatusTimer()
        {
			vrStatusTimer = new DispatcherTimer();
			vrStatusTimer.Interval = TimeSpan.FromSeconds(5);
			vrStatusTimer.Tick += VrStatusTimer_Tick;
			vrStatusTimer.Start();
		}

        private void VrStatusTimer_Tick(object sender, EventArgs e)
		{
			UpdateVREquipmentStatus();
		}

        private void UpdateVREquipmentStatus()
        {
            if (vrSystem == null)
                return;

			// Update headset status (assuming headset is device index 0)
			bool headsetConnected = vrSystem.IsTrackedDeviceConnected(0);
			VREquipmentStatus.IsHeadsetConnected = headsetConnected;
			if (headsetConnected)
			{
				ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
				float battery = vrSystem.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
				double newBatteryPercentage = battery * 100; // assuming value is between 0 and 1
                if (Math.Abs(VREquipmentStatus.HeadsetBatteryPercentage - newBatteryPercentage) > 1)
                {
                    VREquipmentStatus.HeadsetBatteryPercentage = newBatteryPercentage;
                }
			}
			else
			{
				VREquipmentStatus.HeadsetBatteryPercentage = 0;
			}

			// For left controller:
                uint leftIndex = vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
                bool leftConnected = vrSystem.IsTrackedDeviceConnected(leftIndex);
			VREquipmentStatus.IsLeftControllerConnected = leftConnected;
			if (leftConnected)
			{
				ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
				float battery = vrSystem.GetFloatTrackedDeviceProperty(leftIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
				VREquipmentStatus.LeftControllerBatteryPercentage = battery * 100;
			}
			else
			{
				VREquipmentStatus.LeftControllerBatteryPercentage = 0;
			}

			// For right controller:
			uint rightIndex = vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
			bool rightConnected = vrSystem.IsTrackedDeviceConnected(rightIndex);
			VREquipmentStatus.IsRightControllerConnected = rightConnected;
			if (rightConnected)
			{
				ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
				float battery = vrSystem.GetFloatTrackedDeviceProperty(rightIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
				VREquipmentStatus.RightControllerBatteryPercentage = battery * 100;
			}
			else
			{
				VREquipmentStatus.RightControllerBatteryPercentage = 0;
			}
		}

		protected override void OnClosed(EventArgs e)
		{
		_vrStatusTimer?.Dispose();
            base.OnClosed(e);
		}


		private void LeftControllerButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle left hand button click
            Console.WriteLine("Left controller button clicked");
		}

        private void RightControllerButton_Click(object sender, RoutedEventArgs e)
        {
			// Handle right hand button click
			Console.WriteLine("Right controller button clicked");
		}

		private void HeadsetButton_Click(object sender, RoutedEventArgs e)
		{
			// Handle headset button click
			Console.WriteLine("Headset button clicked");
		}


		//Høyre side av toolbar: VolumeSlider, Fullscreen og ExitButton

		// Ny event handler for volumslideren
		private void SpeakerButton_MouseEnter(object sender, MouseEventArgs e)
		{
			VolumePopup.IsOpen = true;
		}

		private void SpeakerButton_MouseLeave(object sender, MouseEventArgs e)
		{
			// Use a dispatcher to check if the mouse is over the popup before closing.
			Dispatcher.BeginInvoke(new Action(() =>
			{
				if (!VolumePopup.IsMouseOver)
				{
					VolumePopup.IsOpen = false;
				}
			}), DispatcherPriority.Background);
		}

		private void VolumePopup_MouseEnter(object sender, MouseEventArgs e)
		{
			// When the mouse enters the popup, keep it open.
			VolumePopup.IsOpen = true;
		}

		private void VolumePopup_MouseLeave(object sender, MouseEventArgs e)
		{
			// When leaving the popup, close it.
			VolumePopup.IsOpen = false;
		}

		private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (VolumeStatusTextBlock == null)
				return;

			VolumeStatusTextBlock.Text = $"{(int)e.NewValue}%";
			VolumeStatusTextBlock.Visibility = Visibility.Visible;

			// If no timer exists, create one; otherwise restart it.
			if (volumeStatusTimer == null)
			{
				volumeStatusTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromSeconds(2)
				};
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

        //Andre rad i MainWindow: Inkl. Filter/Sorter, Header(Alle Spill) og en searchbar for søke etter spill.
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Søk etter spill...")
                SearchBox.Text = "";
        }

		private void VRConnectButton_Click(object sender, RoutedEventArgs e)
		{
			// TODO: Implement logic to connect VR equipment
			MessageBox.Show("VR Connect clicked");
		}

		private void VRDisconnectButton_Click(object sender, RoutedEventArgs e)
		{
			// TODO: Implement logic to disconnect VR equipment
			MessageBox.Show("VR Disconnect clicked");
		}

		private void VRSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			// TODO: Implement logic to open VR settings
			MessageBox.Show("VR Settings clicked");
		}


		private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
                SearchBox.Text = "Søk etter spill...";
        }

        
        // Egen log knapp for å sjekke diverse feil i programmet.
        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            // Create and display the log window modally
            LogWindow logWindow = new LogWindow();
            logWindow.ShowDialog();
        }


        //Dette omhandler Spillgrid seksjonen av vinduet
        private async void LaunchGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Game game)
            {
                // Use the AppID from the game object dynamically
                SteamLauncher.LaunchSteamGame(game.AppID);
            }

            // Hide header and game library, show the Return button in the toolbar.
            HeaderGrid.Visibility = Visibility.Collapsed;
            GameLibraryScrollViewer.Visibility = Visibility.Collapsed;
            ReturnButton.Visibility = Visibility.Visible;

            await Task.Delay(5000);
            await EmbedSteamVRSpectatorAsync();
        }


        //Henter VR View vinduet fra SteamVR og setter det inn/embedder i WPF vinduet
        private async Task EmbedSteamVRSpectatorAsync()
        {
            const int maxAttempts = 20;
            const int delayMs = 5000;
            IntPtr spectatorHandle = IntPtr.Zero;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                spectatorHandle = FindWindow(null, "VR View");
                if (spectatorHandle != IntPtr.Zero)
                {
                    Console.WriteLine("Found Steam VR Spectator Window");
                    break;
                }
                Console.WriteLine($"Attempt {attempt + 1}: Steam VR Spectator Window not found. Waiting...");
                await Task.Delay(delayMs);
            }

            if (spectatorHandle != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                SetParent(spectatorHandle, helper.Handle);
                Console.WriteLine("Embedded Steam VR Spectator Window");
            }
            else
            {
                MessageBox.Show("Kunne ikke finne SteamVR Specator Window etter venting");
            }
        }
    }
}



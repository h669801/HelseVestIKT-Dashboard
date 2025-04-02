using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Diagnostics;
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
using WPoint = System.Windows.Point;
using System.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Windows.Forms.Integration;
using SteamKit2.Internal;
using NAudio.CoreAudioApi;
using Application = System.Windows.Application;

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
		static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

		[DllImport("user32.dll")]
		static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		#region sinmulering av tastetrykk "ESC" for å pause et spill
		[DllImport("user32.dll", SetLastError = true)]
		static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
		// Constants for input type and key flags.
		const int INPUT_KEYBOARD = 1;
		const uint KEYEVENTF_KEYUP = 0x0002;
		const ushort VK_ESCAPE = 0x1B;

		// Define the INPUT structure.
		[StructLayout(LayoutKind.Sequential)]
		struct INPUT
		{
			public uint type;
			public InputUnion u;

			public static int Size => Marshal.SizeOf(typeof(INPUT));
		}

		[StructLayout(LayoutKind.Explicit)]
		struct InputUnion
		{
			[FieldOffset(0)]
			public KEYBDINPUT ki;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct KEYBDINPUT
		{
			public ushort wVk;
			public ushort wScan;
			public uint dwFlags;
			public uint time;
			public IntPtr dwExtraInfo;
		}
		#endregion

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
		public ObservableCollection<SpillKategori> SpillKategorier { get; set; } = new ObservableCollection<SpillKategori>();
		public SpillKategori? ValgtKategori { get; set; }
		public object? WifiConnectionButton { get; private set; }

		private bool _gamesLoaded = false;

		public VRStatusManager VREquipmentStatus { get; set; } = new VRStatusManager();
		private DispatcherTimer? vrStatusTimer;

		private ThreadingTimer? _vrStatusTimer;

		public ImageSource VolumeIcon => StockIcons.GetVolumeIcon();

		public object? WifiSignalProgressBar { get; private set; }

		private DispatcherTimer? _wifiSignalTimer;

		private ObservableCollection<Game> AllGames = new ObservableCollection<Game>();
		public ObservableCollection<Game> FilteredGames { get; set; } = new ObservableCollection<Game>();

		private DispatcherTimer searchTimer;

		private bool isFullscreen = false;
		private VRFullscreenWindow vrFullscreenWindow;

		private Wifi wifi;

		private string _currentPlayer;
		public string CurrentPlayer
		{
			get => _currentPlayer;
			set
			{
				if (_currentPlayer != value)
				{
					_currentPlayer = value;
					OnPropertyChanged(nameof(CurrentPlayer));
				}
			}
		}

		private string _currentStatus;
		public string CurrentStatus
		{
			get => _currentStatus;
			set
			{
				if (_currentStatus != value)
				{
					_currentStatus = value;
					OnPropertyChanged(nameof(CurrentStatus));
				}
			}
		}

		private GameStatusManager _gameStatusManager;
		_gameStatusManager = new GameStatusManager(AllGames);


		public MainWindow()
		{
			InitializeComponent();
			StartMonitoringWifiSignal();
			// Set DataContext for bindings (for example, for CurrentTime)
			DataContext = this;
			this.Loaded += MainWindow_Loaded;

			// Åpner en konsoll for å vise utskrift (Brukes til testing av metoder)
			AllocConsole();

			// Oppretter timeren og sett intervall til 500ms
			searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
			searchTimer.Tick += SearchTimer_Tick;

			_gameStatusManager = new GameStatusManager();

			// Set up a timer to periodically update the game status
			DispatcherTimer gameStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
			gameStatusTimer.Tick += (s, e) => UpdateGameStatus();
			gameStatusTimer.Start();

			// Oppdaterer CurrentTime hvert sekund
			DispatcherTimer timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromSeconds(1);
			timer.Tick += (s, e) =>
			{
				CurrentTime = DateTime.Now.ToString("HH:mm");
			};
			timer.Start();

			_vrStatusTimer = new ThreadingTimer(VRStatusCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
			_wifiSignalTimer = new DispatcherTimer();

			InitializeOpenVR();
			StartVRStatusTimer();
			StartMonitoringWifiSignal();
		}

		private void UpdateGameStatus()
		{
			// Use the GameStatusManager to update the game and status
			_gameStatusManager.UpdateCurrentGameAndStatus();

			// Update the UI with the current game and status
			CurrentPlayer = _gameStatusManager.CurrentPlayer;
			CurrentStatus = _gameStatusManager.CurrentStatus;
		}


		private void StartMonitoringWifiSignal()
		{
			wifi = new Wifi();
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

			if (signalStrength >= 78)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_4.png";
			else if (signalStrength >=
				52)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_3.png";
			else if (signalStrength >= 26)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_2.png";
			else if (signalStrength >= 1)
				iconPath = "pack://application:,,,/Bilder/wifi_bar_1.png";
			else
				iconPath = "pack://application:,,,/Bilder/wifi_bar_0.png";


			// Update the Image control source
			WifiSignalIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
		}

		private void WifiSignalTimer_Tick(object? sender, EventArgs e)
		{
			var accessPoints = wifi.GetAccessPoints();
			var connected = accessPoints.FirstOrDefault(ap => ap.IsConnected);

			if (connected != null)
			{
				// Optionally, cast the uint signal strength to int if needed
				int signalQuality = (int)connected.SignalStrength;
				// Set text to blank when a connection is present
				// Update the icon based on the signal strength
				UpdateWifiIcon(connected.SignalStrength);
			}
			else
			{
				// Optionally show the "no signal" icon
				UpdateWifiIcon(0);
			}
		}


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
			string steamAPIKey = "384082C6759AAF7B6974A9CCE1ECF6CE";
            string steamID = "76561198081888308";
            SteamApi steamApi = new SteamApi("384082C6759AAF7B6974A9CCE1ECF6CE", "76561198081888308");
			var fetchedGames = await steamApi.GetSteamGamesAsync();
            GameDetailsFetcher gameDetailsFetcher = new GameDetailsFetcher(steamAPIKey, steamID);

            var tasks = fetchedGames.Select(async game =>
            {
                await gameDetailsFetcher.AddDetailsAsync(game);
                Application.Current.Dispatcher.Invoke(() => Games.Add(game));
            });
            await Task.WhenAll(tasks);
            await LoadGameAsync(steamApi);

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
			//VRHost.Visibility = Visibility.Collapsed;
		}

		private void PauseKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Restore header and game library areas.
			HeaderGrid.Visibility = Visibility.Visible;
			GameLibraryScrollViewer.Visibility = Visibility.Visible;

			// Hide the Return button (if it was used to go back to the library).
			PauseKnapp.Visibility = Visibility.Visible;

			SimulerEscapeTasteTrykk();
		}

		// Hente informasjon om hvilket spill som kjøres og returnere spill tittel inne i denne textblocken
		private void SpillerNaa(Game game, SteamApi steamapi)
		{
			// Setter opp metoden for å displaye spillnavn i textblock

			
		}

		//Denne knappen lukker/Avslutter spillvinduet gjennom applikasjonen
		private void AvsluttKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Metode for å avslutte spillet ved å trykke på knappen uten å exite applikasjonen
			HeaderGrid.Visibility = Visibility.Visible;

			
		}

		private void NodStoppKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Emergency stop button if application is not responding and VR functions are not working
			// This is a last resort to close the application
			if (MessageBox.Show("Er du sikker på at du vil avslutte programmet?", "Avslutt programmet", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				System.Windows.Application.Current.Shutdown();
		}

		private void ResetButton_Click(object sender, RoutedEventArgs e)
		{
			// Restore header and game library areas.
			HeaderGrid.Visibility = Visibility.Visible;
			GameLibraryScrollViewer.Visibility = Visibility.Visible;

			// Hide the Return button (if it was used to go back to the library).
			ReturnButton.Visibility = Visibility.Collapsed;

			// Gjemmer Kalibrering popup
			KalibreringPopup.IsOpen = false;

			// Exit full-screen mode if currently active.
			if (this.WindowStyle == WindowStyle.None && this.WindowState == WindowState.Maximized)
			{
				this.WindowStyle = WindowStyle.SingleBorderWindow;
				this.WindowState = WindowState.Normal;
			}

			// Reset the search box text to the placeholder.
			SearchBox.Text = "Søk etter spill...";

			// Reset the game list: repopulate the UI-bound collection (Games)
			// from the backup (AllGames) so that any filtering is undone.
			Games.Clear();
			foreach (var game in AllGames)
			{
				Games.Add(game);
			}

			// Optionally, reset the scroll position of the game library.
			GameLibraryScrollViewer.ScrollToHome();

			// Reset any other UI elements or state as needed.

			// Lukk SteamVR
			Process.Start("cmd.exe", "/C taskkill /F /IM vrserver.exe /IM vrmonitor.exe");

			// Vent litt før du starter på nytt
			System.Threading.Thread.Sleep(3000);

			// Start SteamVR på nytt
			Process.Start("C:\\Program Files (x86)\\Steam\\Steam.exe", "-applaunch 250820");
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
			vrStatusTimer.Interval = TimeSpan.FromSeconds(7);
			vrStatusTimer.Tick += VrStatusTimer_Tick;
			vrStatusTimer.Start();
		}

		private void VrStatusTimer_Tick(object? sender, EventArgs e)
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
				Console.WriteLine($"Right Controller battery: {battery}, error {error}");
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


		//Høyre side av toolbar: VolumeSlider,Innstillinger, Fullscreen og ExitButton

		private void Settings_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Innstillinger klikket");

		}

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

		private async void VolumePopup_MouseLeave(object sender, MouseEventArgs e)
		{
			await Task.Delay(100);

			if (!VolumePopup.IsMouseOver && !SpeakerButton.IsMouseOver)
			{
				VolumePopup.IsOpen = false;
			}
		}

		private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{

			float volumeScalar = (float)(e.NewValue / 100.0);
			var enumerator = new MMDeviceEnumerator();
			MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			device.AudioEndpointVolume.MasterVolumeLevelScalar = volumeScalar;

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

		private CustomPopupPlacement[] VolumePopupPlacementCallback(System.Windows.Size popupSize, System.Windows.Size targetSize, WPoint offset)
		{
			double horizontalOffset = (targetSize.Width - popupSize.Width) / 2;
			double verticalOffset = targetSize.Height;
			var palcement = new CustomPopupPlacement(new WPoint(horizontalOffset, verticalOffset), PopupPrimaryAxis.Horizontal);
			return new CustomPopupPlacement[] { palcement };

		}

		private void FullScreenButton_Click(object sender, RoutedEventArgs e)
		{

			if (!isFullscreen)
			{
				if (VRHost.Parent is System.Windows.Controls.Panel parentPanel)
				{
					parentPanel.Children.Remove(VRHost);
				}

				vrFullscreenWindow = new VRFullscreenWindow();
				vrFullscreenWindow.SetVRContent(VRHost);

				VRFullscreenContainer.Content = vrFullscreenWindow.Content;
				VRFullscreenContainer.Visibility = Visibility.Visible;

			}
			else
			{
				// Avslutt fullskjerm: fjern VRHost fra VRFullscreenWindow
				vrFullscreenWindow.RemoveVRContent(VRHost);
				VRFullscreenContainer.Content = null;
				VRFullscreenContainer.Visibility = Visibility.Collapsed;

				// Legg VRHost tilbake i den opprinnelige containeren
				MainContentGrid.Children.Add(VRHost);
			}

		}

		private void ExitButton_Click(object sender, RoutedEventArgs e)
		{
			//Sjekker om brukeren er sikker på at de vil avslutte programmet
			if (MessageBox.Show("Er du sikker på at du vil avslutte programmet?", "Avslutt programmet", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{ this.Close(); }
		}


		#region VR Verktøylinje knapper
		//Andre rad i MainWindow: Inkl. Filter/Sorter, Header(Alle Spill) og en searchbar for søke etter spill.

		

		private void Nodstopp_Click(object sender, RoutedEventArgs e)
		{
			// Emergency stop button if application is not responding and VR functions are not working
			// This is a last resort to close the application

			if (MessageBox.Show("Er du sikker på at du vil avslutte programmet?", "Avslutt programmet", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				System.Windows.Application.Current.Shutdown();
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
		#endregion

		#region Filter Button

		
		#endregion


		#region Søkeboks logikk

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

		private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (searchTimer == null)
				return;
			searchTimer.Stop();
			searchTimer.Start();
		}

		private void SearchTimer_Tick(object? sender, EventArgs e)
		{
			searchTimer.Stop();
			SearchGames();
		}

		private void SearchGames()
		{
			// For example, filter based on the current text
			string searchQuery = SearchBox.Text;

			// If the user left the placeholder text or typed nothing, 
			// you might want to reset or skip filtering
			if (string.IsNullOrWhiteSpace(searchQuery) || searchQuery == "Søk etter spill...")
			{
				// Possibly reset the game list or do nothing
				Games.Clear();
				foreach (var game in AllGames)
				{
					Games.Add(game);
				}
				return;
			}

			// Filter the backup collection
			var filteredGames = AllGames
				.Where(g => g.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
				.ToList();

	
			// Reset the Games collection with filtered results
			Games.Clear();
			foreach (var game in filteredGames)
			{
				Games.Add(game);
			}
		}

		private void ApplyGenreFilter()
		{
			// For example, check if a specific genre checkbox is checked.
			bool filterAction = CheckBoxAction.IsChecked == true;
			bool filterAdventure = CheckBoxEventyr.IsChecked == true;

			var filteredGames = AllGames.Where(game =>
			{
				// If filtering by Action, check if the game's genres include "Action"
				if (filterAction && !game.Genres.Contains("Action"))
					return false;
				// Similarly for Adventure, add more conditions as needed
				if (filterAdventure && !game.Genres.Contains("Adventure"))
					return false;

				// Additional filters can be added here

				return true;
			}).ToList();

			Games.Clear();
			foreach (var game in filteredGames)
			{
				Games.Add(game);
			}
		}

		private async Task LoadGameAsync(SteamApi steamApi)
		{
			var loadedGames = await steamApi.GetSteamGamesAsync();


			AllGames.Clear();
			Games.Clear();

			foreach (var game in loadedGames)
			{
				AllGames.Add(game);
				Games.Add(game);
			}


		}

		#endregion


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
			HeaderGrid.Visibility = Visibility.Visible;
			StatusBar.Visibility = Visibility.Visible;	
			GameLibraryScrollViewer.Visibility = Visibility.Collapsed;
			// VRHost.Visibility = Visibility.Visible;
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

		#region VR-Kalibreringsknapper og funksjoner

		private void Romkalibrering_Click(object sender, RoutedEventArgs e)
		{
			KalibrerKnapp.Visibility = Visibility.Visible;
			KalibreringPopup.IsOpen = true;

		}

		private void RomKalibrering(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "steam://run/250820//roomsetup",
				UseShellExecute = true
			});
		}

		private void HøydeKalibrering(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "steam://run/250820//standalone",
				UseShellExecute = true
			});
		}
		private void MidstillView(object sender, RoutedEventArgs e)
		{
			if (OpenVR.Chaperone != null)
			{
				OpenVR.Chaperone.ResetZeroPose(ETrackingUniverseOrigin.TrackingUniverseSeated);
				Console.WriteLine("Seated zero pose has been reset.");
			}
		}

		// Denne metoden skal pause et spill som kjører i VR
		private void PauseSpillKnapp_Click(Object sender, RoutedEventArgs e)
		{ 

		}
		#endregion

		private void SearchBox_KeyDown_1(object sender, System.Windows.Input.KeyEventArgs e)
		{

		}

		// Denne knappen skal skjule sidemenyen og vise en knapp for å vise den igjen
		private void SkjulKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Skjuler sidemenyen
			SideMenu.Visibility = Visibility.Collapsed;
			// Viser knappen for å vise sidemenyen igjen
			VisKnapp.Visibility = Visibility.Visible;

		}

		private void VisKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Viser sidemenyen
			SideMenu.Visibility = Visibility.Visible;
			// Skjuler knappen for å vise sidemenyen igjen
			VisKnapp.Visibility = Visibility.Collapsed;
		}
	
		// Denne metoden legger til et spill i en valgt kategori
		public void LeggTilSpillIKategori(SpillKategori kategori, Game game)
		{
			if (ValgtKategori != null && game != null)
			{
				ValgtKategori.Games.Add(game);
			}
		}

		// Denne metoden fjerner et spill fra en kategori
		public void FjernSpillFraKategori(Game game)
		{
			if (ValgtKategori != null && ValgtKategori.Games.Contains(game))
			{
				ValgtKategori.Games.Remove(game);
			}
		}


		private void ApplyFilter_Click(object sender, RoutedEventArgs e)
		{
			// Build a filter object from the MainWindow UI elements (e.g., CheckBoxes)
			var filteredGames = AllGames.Where(game =>
			{
				// Example: filter by search text
				if (!string.IsNullOrWhiteSpace(SearchBox.Text) &&
					SearchBox.Text != "Søk etter spill..." &&
					!game.Title.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				
				return true;
			}).ToList();

			// Update the Games collection to show filtered results
			Games.Clear();
			foreach (var game in filteredGames)
			{
				Games.Add(game);
			}
		}


		private void SimulerEscapeTasteTrykk()
		{
			const int INPUT_KEYBOARD = 1;
			const uint KEYEVENTF_KEYUP = 0x0002;
			const ushort VK_ESCAPE = 0x1B;
			INPUT[] inputs = new INPUT[2];

			// Key down event for ESC
			inputs[0].type = INPUT_KEYBOARD;
			inputs[0].u.ki.wVk = VK_ESCAPE;
			inputs[0].u.ki.wScan = 0;
			inputs[0].u.ki.dwFlags = 0; // 0 for key press
			inputs[0].u.ki.time = 0;
			inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

			// Key up event for ESC
			inputs[1].type = INPUT_KEYBOARD;
			inputs[1].u.ki.wVk = VK_ESCAPE;
			inputs[1].u.ki.wScan = 0;
			inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP; // key release flag
			inputs[1].u.ki.time = 0;
			inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

			// Send the input events
			uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
			if (result == 0)
			{
				int error = Marshal.GetLastWin32Error();
				// Optionally log the error or display a message.
				Console.WriteLine("SendInput failed with error: " + error);
			}
		}


	}
}

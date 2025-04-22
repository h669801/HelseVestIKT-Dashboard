using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Valve.VR;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using ThreadingTimer = System.Threading.Timer;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SimpleWifi;
using WPoint = System.Windows.Point;
using NAudio.CoreAudioApi;
using CheckBox = System.Windows.Controls.CheckBox;
using Vortice.Direct3D11;
using WpfPanel = System.Windows.Controls.Panel;
using System.IO;
using Path = System.IO.Path;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Numerics;


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

		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		#region Simulering av tastetrykk "ESC" for å pause et spill
		[DllImport("user32.dll", SetLastError = true)]
		static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
		// Constants for input type and key flags.
		const int INPUT_KEYBOARD = 1;
		const uint KEYEVENTF_KEYUP = 0x0002;
		const ushort VK_ESCAPE = 0x1B;


		[DllImport("user32.dll", SetLastError = true)]
		static extern bool SetWindowPos(
		IntPtr hWnd,
		IntPtr hWndInsertAfter,
		int X, int Y, int cx, int cy,
		uint uFlags);

		static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
		const uint SWP_NOMOVE = 0x0002;
		const uint SWP_NOSIZE = 0x0001;


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

		#region Egenskaper og Variabler
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

		/// <summary>
		/// Henter den for øyeblikket kjørende spillprosessen,
		/// basert på det Game-objektet GameStatusManager holder på.
		/// </summary>
		private Process? GetRunningGameProcess()
		{
			var game = _gameStatusManager.CurrentGame;
			if (game == null)
				return null;

			Console.WriteLine($"[DEBUG] Letes etter prosesser med navn: {game.ProcessName}");
			var procs = Process.GetProcessesByName(game.ProcessName);
			Console.WriteLine($"[DEBUG] Funnet {procs.Length} prosesser.");
			return procs.FirstOrDefault();
		}

public static string? GetProcessNameFromSteam(string steamPath, string appId)
	{
		// 1) Les manifest
		var manifest = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
		if (!File.Exists(manifest)) return null;

		string content = File.ReadAllText(manifest);
		// 2) Trekk ut installdir
		var m = Regex.Match(content, "\"installdir\"\\s*\"(?<dir>.*?)\"");
		if (!m.Success) return null;

		string installDir = m.Groups["dir"].Value;
		// 3) Gå til common‑mappen og let etter exe
		var gameFolder = Path.Combine(steamPath, "steamapps", "common", installDir);
		if (!Directory.Exists(gameFolder)) return null;

		// Finn alle exe i roten (du kan snevre mer inn hvis du vet mønster)
		var exes = Directory.GetFiles(gameFolder, "*.exe", SearchOption.TopDirectoryOnly);
		if (exes.Length == 0) return null;

		// F.eks. velg den største exe (antakelse: launcheren er stor)
		var chosen = exes.OrderByDescending(f => new FileInfo(f).Length).First();
		return Path.GetFileNameWithoutExtension(chosen);
	}


		double eyeHeight = Properties.Settings.Default.Equals("EyeHeight") ? 1.8 : 0.0; // Default to 1.8m if not set

		public ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();
		public ObservableCollection<SpillKategori> SpillKategorier { get; set; } = new ObservableCollection<SpillKategori>();
		public SpillKategori? ValgtKategori { get; set; }
		private ObservableCollection<Game> AllGames = new ObservableCollection<Game>();
		public ObservableCollection<Game> FilteredGames { get; set; } = new ObservableCollection<Game>();
		private bool _gamesLoaded = false;
		private GameStatusManager _gameStatusManager;
        private FilterHandler filterHandler = new FilterHandler();
        private GameDetailsFetcher gameDetailsFetcher;

        public object? WifiConnectionButton { get; private set; }
		public object? WifiSignalProgressBar { get; private set; }
		private Wifi wifi;
		private DispatcherTimer? _wifiSignalTimer;


		private DispatcherTimer? volumeStatusTimer = null;
		public ImageSource VolumeIcon => StockIcons.GetVolumeIcon();


		private bool isFullscreen = false;
		private DispatcherTimer? vrStatusTimer;
		private ThreadingTimer? _vrStatusTimer;
		private CVRSystem? vrSystem;
		public VRStatusManager VREquipmentStatus { get; set; } = new VRStatusManager();

		private DispatcherTimer searchTimer;

	
		#endregion

		#region VRMirrorTextureTest
		// Define the delegate that maps to the OpenVR function.

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		internal delegate EVRCompositorError _GetMirrorTextureD3D11(EVREye eEye, IntPtr pD3D11DeviceOrResource, ref IntPtr ppD3D11ShaderResourceView);

		// This field will hold the function pointer as a delegate.
		[MarshalAs(UnmanagedType.FunctionPtr)]
		internal _GetMirrorTextureD3D11 GetMirrorTextureD3D11;

		private bool _alreadyEmbedded = false;
		private int _vrEmbedAttempts = 0;
		private DispatcherTimer _vrEmbedTimer;
		private const int MaxVREmbedAttempts = 20;



		#endregion



		#region MainWindow Konstruktør
		public MainWindow()
		{
			InitializeComponent();
			StartMonitoringWifiSignal();
			// Set DataContext for bindings (for example, for CurrentTime)
			DataContext = this;
			this.Loaded += MainWindow_Loaded;


			CurrentPlayer = "";
			CurrentStatus = "";


			// Åpner en konsoll for å vise utskrift (Brukes til testing av metoder)
			AllocConsole();

			// Oppretter timeren og sett intervall til 500ms
			searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
			searchTimer.Tick += SearchTimer_Tick;

		
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

			// Lukk SteamVR
			Process.Start("cmd.exe", "/C taskkill /F /IM vrserver.exe /IM vrmonitor.exe");

			// Vent litt før du starter på nytt
			System.Threading.Thread.Sleep(3000);

			// Start SteamVR på nytt
			Process.Start("C:\\Program Files (x86)\\Steam\\Steam.exe", "-applaunch 250820");

			EVRInitError initError = EVRInitError.None;
			OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Background);
			if (initError != EVRInitError.None)
				MessageBox.Show($"Kan ikke initialisere OpenVR: {initError}");


			InitializeOpenVR();
			StartVRStatusTimer();
			StartMonitoringWifiSignal();

			
		}
		#endregion

		private void UpdateGameStatus()
		{
			_gameStatusManager.UpdateCurrentGameAndStatus();
			CurrentPlayer = _gameStatusManager.CurrentPlayer;
			CurrentStatus = _gameStatusManager.CurrentStatus;
		}


		private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (_gamesLoaded)
				return;
			_gamesLoaded = true;

			// 1) Hent Steam-spill én gang
			string steamAPIKey = "384082C6759AAF7B6974A9CCE1ECF6CE";
			string steamID = "76561198081888308";
			SteamApi steamApi = new SteamApi(steamAPIKey, steamID);

			var steamGames = await steamApi.GetSteamGamesAsync();

			// 2) Berik med detaljer
			gameDetailsFetcher = new GameDetailsFetcher(steamAPIKey, steamID);
			await Task.WhenAll(steamGames.Select(g => gameDetailsFetcher.AddDetailsAsync(g)));

			// 3) Fyll AllGames og Games én gang
			AllGames.Clear();
			Games.Clear();
			foreach (var g in steamGames)
			{
				AllGames.Add(g);
				Games.Add(g);
			}

			// 4) Hent non‑Steam-spill (og unngå duplikater)
			var offlineGames = new OfflineSteamGamesManager()
								   .GetNonSteamGames(@"C:\Program Files (x86)\Steam")
								   .Where(g => !steamGames.Any(s => s.AppID == g.AppID));

			foreach (var g in offlineGames)
			{
				AllGames.Add(g);
				Games.Add(g);
				g.GameImage = GameImage.LoadIconFromExe(g.InstallPath);
			}


			var steamPath = GetSteamInstallPathFromRegistry();
			foreach (var g in AllGames)
			{
				if (!string.IsNullOrEmpty(g.InstallPath))
				{
					g.ProcessName = Path.GetFileNameWithoutExtension(g.InstallPath);
				}
				else if (g.IsSteamGame)
				{
					g.ProcessName = GetProcessNameFromSteam(steamPath, g.AppID) ?? "";
				}
			}


			_gameStatusManager = new GameStatusManager(AllGames);
			UpdateGameStatus();
			var gameStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
			gameStatusTimer.Tick += (s, e) => UpdateGameStatus();
			gameStatusTimer.Start();


			// Resten av initialiseringen
			await Task.Delay(2000);
			
		}

		/// <summary>
		/// Reads Steam’s install path from the registry (HKCU\Software\Valve\Steam\SteamPath),
		/// or falls back to the default Program Files location if not found.
		/// </summary>
		private static string GetSteamInstallPathFromRegistry()
		{
			const string steamKey = @"Software\Valve\Steam";
			using (var key = Registry.CurrentUser.OpenSubKey(steamKey))
			{
				if (key != null)
				{
					var path = key.GetValue("SteamPath") as string;
					if (!string.IsNullOrEmpty(path))
						return path;
				}
			}

			// fallback if the registry lookup fails
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				"Steam");
		}

	

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

        #region FilterEvent 
        private void UpdateFilters(object sender, RoutedEventArgs e)
        {

            List<CheckBox> genreFilters = new()
            {
                CheckBoxAction, CheckBoxEventyr, CheckBoxIndie, CheckBoxLettbeint, CheckBoxMassivtFlerspill,
                CheckBoxSport, CheckBoxStrategi, CheckBoxRacing, CheckBoxRollespill, CheckBoxSimulering
            };

            List<CheckBox> typeFilters = new()
            {
                CheckBoxNyligSpilt, CheckBoxVRSpill, CheckFlerspiller, CheckBoxSteamSpill, CheckBoxAndreSpill, CheckBoxKunFavoritter
            };

            Games.Clear();

            foreach (Game game in AllGames)
            {

                if (filterHandler.FilterGame(genreFilters, typeFilters, game))
                {
                    Console.WriteLine("Game Added: " + game.Title);
                    Games.Add(game);
                }
            }
            Console.WriteLine("Filters applied: " + ((CheckBox)sender).Content.ToString() + "\n\n");



        }

        #endregion


        #region OpenVR og VR Status

        private void InitializeOpenVR()
		{
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

		#endregion

		#region Wifi Signal Monitorering
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
				iconPath = "pack://application:,,,/Bilder/wifi_3_bar.png";
			else if (signalStrength >= 52)
				iconPath = "pack://application:,,,/Bilder/wifi_2_bar.png";
			else if (signalStrength >= 1)
				iconPath = "pack://application:,,,/Bilder/wifi_1_bar.png";
			else
				iconPath = "pack://application:,,,/Bilder/wifi_0_bar.png";


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

	
		#endregion

		#region Fullscreen og VR Embedding

		private void FullScreenButton_Click(object sender, RoutedEventArgs e)
		{
			// Skjul GameLibrary-området
			GameLibraryArea.Visibility = Visibility.Collapsed;
			ReturnButton.Visibility = Visibility.Visible;

			// Gjør VRHost synlig (det ligger allerede i MainContentGrid på riktig rad/kolonne i XAML)
			VRHost.Visibility = Visibility.Visible;

			// Sørg for at det ligger øverst i z‑rekkefølgen
			WpfPanel.SetZIndex(VRHost, 100);

			Console.WriteLine("VRHost er nå synlig over spillbiblioteket.");
		}


		// This method embeds the external VR view window into your host control.
		private void EmbedVRView(IntPtr vrViewHandle)
		{
			// Get the handle for your WindowsFormsHost control.
			// VRHost is your WindowsFormsHost defined in XAML.
			IntPtr hostHandle = ((System.Windows.Forms.Control)VRHost.Child)?.Handle ?? IntPtr.Zero;
			if (hostHandle == IntPtr.Zero)
			{
				Console.WriteLine("VRHost child not available.");
				return;
			}

			// Re-parent the external VR View window.
			IntPtr prevParent = Win32.SetParent(vrViewHandle, hostHandle);
			if (prevParent == IntPtr.Zero)
			{
				Console.WriteLine("Failed to reparent the VR View window.");
				return;
			}

			// Remove window borders and title.
			int style = Win32.GetWindowLong(vrViewHandle, Win32.GWL_STYLE);
			style &= ~(Win32.WS_CAPTION | Win32.WS_BORDER);
			style |= Win32.WS_CHILD;
			Win32.SetWindowLong(vrViewHandle, Win32.GWL_STYLE, style);

			// Position and size the window to fill your host.
			int width = (int)VRHost.ActualWidth;
			int height = (int)VRHost.ActualHeight;
			bool posResult = Win32.SetWindowPos(vrViewHandle, IntPtr.Zero, 0, 0, width, height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
			if (!posResult)
			{
				Console.WriteLine("SetWindowPos failed for the VR View window.");
			}
			Console.WriteLine("VR View embedded successfully.");
		}

		private void VRHost_SizeChanged(object sender, EventArgs e)
		{
			IntPtr vrViewHandle = Win32.FindWindow(null, "VR View");
			if (vrViewHandle == IntPtr.Zero) return;

			int width = (int)VRHost.ActualWidth;
			int height = (int)VRHost.ActualHeight;
			Win32.SetWindowPos(vrViewHandle, IntPtr.Zero, 0, 0, width, height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
		}

		// Starter retry-polling for å fange opp VR View-vinduet
		private void StartVREmbedRetry()
		{
			_alreadyEmbedded = false;
			_vrEmbedAttempts = 0;
			_vrEmbedTimer?.Stop();
			_vrEmbedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
			_vrEmbedTimer.Tick += VREmbedTimer_Tick;
			_vrEmbedTimer.Start();
		}

		// Kalles hver gang timeren går av – forsøker å embedde én gang
		private void VREmbedTimer_Tick(object sender, EventArgs e)
		{
			if (_alreadyEmbedded) return;

			_vrEmbedAttempts++;
			IntPtr vrViewHandle = Win32.FindWindow(null, "VR View");
			if (vrViewHandle != IntPtr.Zero)
			{
				_vrEmbedTimer.Stop();
				EmbedVRView(vrViewHandle);
				_alreadyEmbedded = true;
				Console.WriteLine("VR View embedded successfully.");
			}
			else if (_vrEmbedAttempts >= MaxVREmbedAttempts)
			{
				_vrEmbedTimer.Stop();
				Console.WriteLine("Unable to embed VR View after multiple attempts.");
			}
		}

		#endregion

		#region Toolbar og Volum kontroller
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
			// Beregn volum‐verdi (0.0–1.0)
			float volumeScalar = (float)(e.NewValue / 100.0);

			// Hent standard avspillingsenhet
			var enumerator = new MMDeviceEnumerator();
			MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

			// Sett volumet
			device.AudioEndpointVolume.MasterVolumeLevelScalar = volumeScalar;

			// Sørg for at lyden ikke er dempet
			device.AudioEndpointVolume.Mute = false;

			// Oppdater status‐teksten
			if (VolumeStatusTextBlock != null)
			{
				VolumeStatusTextBlock.Text = $"{(int)e.NewValue}%";
				VolumeStatusTextBlock.Visibility = Visibility.Visible;
			}

			// (Re)start timeren som skjuler status‐teksten etter 2 sekunder
			if (volumeStatusTimer == null)
			{
				volumeStatusTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromSeconds(2)
				};
				volumeStatusTimer.Tick += (s, args) =>
				{
					VolumeStatusTextBlock.Visibility = Visibility.Collapsed;
					volumeStatusTimer.Stop();
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
			var placement = new CustomPopupPlacement(new WPoint(horizontalOffset, verticalOffset), PopupPrimaryAxis.Horizontal);
			return new CustomPopupPlacement[] { placement };
		}


		private void ExitButton_Click(object sender, RoutedEventArgs e)
		{
			//Sjekker om brukeren er sikker på at de vil avslutte programmet
			if (MessageBox.Show("Er du sikker på at du vil avslutte programmet?", "Avslutt programmet", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{ this.Close(); }
		}

		#endregion

		#region VR Statuslinje og knappersk

		//Denne knappen lukker/Avslutter spillvinduet gjennom applikasjonen
		private void AvsluttKnapp_Click(object sender, RoutedEventArgs e)
		{
			var proc = GetRunningGameProcess();
			if (proc == null)
			{
				MessageBox.Show("Ingen spill å avslutte.", "Avslutt spill", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// Prøv å lese modul‑filnavn, men unngå crash
			string exeName;
			try
			{
				exeName = proc.MainModule?.FileName ?? "";
			}
			catch
			{
				exeName = "";
			}

			bool isSteam = exeName.IndexOf("Steam.exe", StringComparison.OrdinalIgnoreCase) >= 0;
			if (isSteam)
			{
				// Lukk Steam‑spill via URI (merk: fungerer kun for noen titler)
				Process.Start(new ProcessStartInfo
				{
					FileName = $"steam://close/{_gameStatusManager.CurrentPlayer}",
					UseShellExecute = true
				});
			}
			else
			{
				// Vanlig prosess: be om å lukke, vent, tving deretter kill
				proc.CloseMainWindow();
				Task.Delay(2000).ContinueWith(_ =>
				{
					if (!proc.HasExited) proc.Kill();
				});
			}
		}


		private void Nodstopp_Click(object sender, RoutedEventArgs e)
		{
			// Emergency stop button if application is not responding and VR functions are not working
			// This is a last resort to close the application

			if (MessageBox.Show("Er du sikker på at du vil avslutte programmet?", "Avslutt programmet", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				System.Windows.Application.Current.Shutdown();
		}

		private async void ReturnButton_Click(object sender, RoutedEventArgs e)
		{
			// ———————— RESET UI ————————
			// Gjem VR‑visningen
			VRHost.Visibility = Visibility.Collapsed;
			// Flytt VRHost bak alt annet
			System.Windows.Controls.Panel.SetZIndex(VRHost, 0);

			// Vis sidemeny og spillbibliotek
			SideMenu.Visibility = Visibility.Visible;
			GameLibraryArea.Visibility = Visibility.Visible;
			GameLibraryScrollViewer.Visibility = Visibility.Visible;

			// Skjul Hjem-knappen og vis Pause-knappen igjen (hvis ønsket)
			ReturnButton.Visibility = Visibility.Collapsed;
			PauseKnapp.Visibility = Visibility.Visible;

			// —————— RESTART STEAMVR ——————
			try
			{
				await RestartSteamVRAsync();
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					$"Kunne ikke restarte SteamVR: {ex.Message}",
					"SteamVR-feil",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
			}
		}
		private async Task RestartSteamVRAsync()
		{
			// Stopp SteamVR-prosessene
			Process.Start("cmd.exe", "/C taskkill /F /IM vrserver.exe /IM vrmonitor.exe");
			// Vent et par sekunder for at de skal dø ordentlig
			await Task.Delay(3000);
			// Start Steam og launch SteamVR
			Process.Start(new ProcessStartInfo
			{
				FileName = @"C:\Program Files (x86)\Steam\Steam.exe",
				Arguments = "-applaunch 250820",
				UseShellExecute = true
			});
		}


		private void PauseKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Finn spillprosessen
			var proc = GetRunningGameProcess();
			if (proc == null)
			{
				MessageBox.Show("Fant ingen spill å pause.", "Pause", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// Eksempel: åpne SteamVR dashbord
			if (OpenVR.Applications != null)
			{
				OpenVR.Applications.LaunchDashboardOverlay("");
			}
		}

		#endregion

		#region Søkeboks logikk og filtrering

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

		#region Spillbibliotek og Logg

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
			if (!(sender is Button btn && btn.DataContext is Game game))
				return;

			Console.WriteLine(">>> LaunchGameButton_Click fired");
			Console.WriteLine($"Title: {game.Title}");
			Console.WriteLine($"IsSteamGame: {game.IsSteamGame}");
			Console.WriteLine($"InstallPath: '{game.InstallPath}'");
			Console.WriteLine($"AppID: '{game.AppID}'");

			// 1) Start spillet, enten direkte .exe eller via Steam-URI
			Process? proc = null;
			bool hasExe = !string.IsNullOrWhiteSpace(game.InstallPath)
						  && File.Exists(game.InstallPath);

			if (hasExe)
			{
				var psi = new ProcessStartInfo
				{
					FileName = game.InstallPath,
					WorkingDirectory = Path.GetDirectoryName(game.InstallPath)!
				};
				try
				{
					proc = Process.Start(psi);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Kunne ikke starte spillet: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}
			else if (!string.IsNullOrWhiteSpace(game.AppID))
			{
				try
				{
					proc = Process.Start(new ProcessStartInfo
					{
						FileName = $"steam://rungameid/{game.AppID}",
						UseShellExecute = true
					});
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Kunne ikke starte Steam-spill: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}
			else
			{
				MessageBox.Show(
					$"Ingen gyldig kjørbar fil eller Steam‑ID funnet for «{game.Title}».",
					"Feil",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return;
			}

			// 2) Vent til vinduet er klart, så dytt det bak applikasjonen
			if (proc != null)
			{
				await Task.Run(() => proc.WaitForInputIdle(5000));
				IntPtr hGame = proc.MainWindowHandle;
				if (hGame != IntPtr.Zero)
				{
					SetWindowPos(hGame, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
				}
			}

			// 3) Skyv også SteamVR‑kontrollpanelet bak (hvis det allerede er åpent)
			IntPtr hSteamVR = FindWindow(null, "SteamVR");
			if (hSteamVR != IntPtr.Zero)
			{
				SetWindowPos(hSteamVR, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
			}

			// 4) Gi UI-en et lite pusterom før vi embedder VR‑visningen
			await Task.Delay(300);

			// 5) Gå rett i fullskjerm/VR‑view
			FullScreenButton_Click(null, null);
			StartVREmbedRetry();
			await EmbedVRSpectatorAsync();

			// 6) Oppdater resten av UI
			HeaderGrid.Visibility = Visibility.Visible;
			StatusBar.Visibility = Visibility.Visible;
			GameLibraryScrollViewer.Visibility = Visibility.Collapsed;
			VRHost.Visibility = Visibility.Visible;
			ReturnButton.Visibility = Visibility.Visible;
		}


		private async Task EmbedVRSpectatorAsync()
		{
			int attempts = 0;
			while (attempts < MaxVREmbedAttempts)
			{
				// Søk etter VR-vinduet, her benytter vi tittelen "VR View".
				IntPtr vrViewHandle = Win32.FindWindow(null, "VR View");
				if (vrViewHandle != IntPtr.Zero)
				{
					// Når vinduet er funnet, kall EmbedVRView og avslutt metoden.
					EmbedVRView(vrViewHandle);
					Console.WriteLine("VR View embedded successfully after {0} attempts.", attempts + 1);
					return;
				}

				attempts++;
				// Vent 500ms før neste forsøk. Juster ventetiden etter behov.
				await Task.Delay(3000);
			}

			Console.WriteLine("Unable to embed VR View after multiple attempts.");
		}


		#endregion

		#region VR-Kalibrering og Funksjoner

		private void KalibreringKnapp_Click(object sender, RoutedEventArgs e)
		{
			KalibrerKnapp.Visibility = Visibility.Visible;
		
			if (StatusBarKalibrering.IsVisible)
			{
				StatusBarKalibrering.Visibility = Visibility.Collapsed;
			}
			else
			{
				StatusBarKalibrering.Visibility = Visibility.Visible;
			}
		}


		private async void RomKalibrering_Click(object sender, RoutedEventArgs e)
		{
			// 1) Slå midlertidig av our Topmost
			bool wasTopmost = this.Topmost;
			this.Topmost = false;

			// 2) Finn exe‑stien
			string exePath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				"Steam", "steamapps", "common", "SteamVR",
				"tools", "steamvr_room_setup", "win64", "steamvr_room_setup.exe"
			);
			if (!File.Exists(exePath))
			{
				MessageBox.Show($"Fant ikke Room Setup på:\n{exePath}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Topmost = wasTopmost;
				return;
			}

			// 3) Start prosessen
			var psi = new ProcessStartInfo
			{
				FileName = exePath,
				UseShellExecute = true
			};
			var proc = Process.Start(psi);
			if (proc == null)
			{
				MessageBox.Show("Kunne ikke starte Room Setup‑prosessen.", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Topmost = wasTopmost;
				return;
			}

			// 4) Poll på MainWindowHandle inntil det blir satt, eller til timeout
			IntPtr handle = IntPtr.Zero;
			const int maxAttempts = 20;
			for (int i = 0; i < maxAttempts; i++)
			{
				proc.Refresh();
				handle = proc.MainWindowHandle;
				if (handle != IntPtr.Zero)
					break;
				await Task.Delay(500);
			}

			// 5) Hvis vi fant vinduet, bring det foran
			if (handle != IntPtr.Zero)
			{
				SetForegroundWindow(handle);
			}
			else
			{
				MessageBox.Show("Fikk ikke tak i Room Setup–vinduet for å sette det i front.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			// 6) Gjenopprett Topmost
			this.Topmost = wasTopmost;
		}



		private void HoydeKalibrering_Click(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "steam://run/250820//standalone",
				UseShellExecute = true
			});
		}
		private void MidtstillView_Sittende_Click(object sender, RoutedEventArgs e)
		{
			Recenter(ETrackingUniverseOrigin.TrackingUniverseSeated);
		}

		private void MidstillView_Staaende_Click(object sender, RoutedEventArgs e)
		{
			Recenter(ETrackingUniverseOrigin.TrackingUniverseStanding);
		}


		// Felles helper-metode som skal resentrere VR-visningen
		// Shared helper you can call for both seated and standing
		private void Recenter(ETrackingUniverseOrigin origin)
		{

			EnsureOverlaySession();

			// 1) Sørg for at vi har tilgang til Compositor/Chaperone
			if (OpenVR.Compositor == null || OpenVR.Chaperone == null)
			{
				Console.WriteLine("OpenVR ikke klart for recenter.");
				return;
			}

			// 2) Sett ønsket tracking space (sittende eller stående)
			OpenVR.Compositor.SetTrackingSpace(origin);

			// 3) Nullstill zero‑pose i det valgte universet
			OpenVR.Chaperone.ResetZeroPose(origin);

			// 4) Tving compositoren til å hente oppdaterte poser umiddelbart
			var emptyRender = new Valve.VR.TrackedDevicePose_t[0];
			var emptyGame = new Valve.VR.TrackedDevicePose_t[0];
			OpenVR.Compositor.WaitGetPoses(emptyRender, emptyGame);

			Console.WriteLine($"Recenter fullført: {origin}");
		}


		/// <summary>
		/// Sørger for at vi har en gyldig OpenVR‑session som overlay.
		/// </summary>
		private void EnsureOverlaySession()
		{
			// Hvis Compositor eller Chaperone er null, initier på nytt som overlay‑app
			if (OpenVR.Compositor == null || OpenVR.Chaperone == null)
			{
				EVRInitError initError = EVRInitError.None;
				OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Overlay);
				if (initError != EVRInitError.None)
				{
					Console.WriteLine($"Kunne ikke init Overlay‑session: {initError}");
				}
				else
				{
					Console.WriteLine("Overlay‑session initiert på nytt.");
				}
			}
		}


		#endregion

		#region Diverse Brukergrensesnitt funksjoner
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

		

		#endregion
	}
}

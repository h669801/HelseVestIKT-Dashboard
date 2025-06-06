﻿using System.Collections.ObjectModel;
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
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SimpleWifi;
using WPoint = System.Windows.Point;
using NAudio.CoreAudioApi;
using CheckBox = System.Windows.Controls.CheckBox;
using Dialogs;
using Vortice.Direct3D11;
using WpfPanel = System.Windows.Controls.Panel;
using System.IO;
using Path = System.IO.Path;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Numerics;
using System.Windows.Input;
using System.Net.Sockets;
using System.Net;


namespace HelseVestIKT_Dashboard
{

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
		private static extern bool AllocConsole();

		

		#region Simulering av tastetrykk "ESC" for å pause et spill
		
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

		private static string? GetSteamExePath(string steamPath, string appId)
		{
			// katalogen der SteamVR og andre Steam-spill ligger
			var common = Path.Combine(steamPath, "steamapps", "common");

			// finn undermappen til dette appId
			var manifest = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
			if (!File.Exists(manifest))
				return null;

			// les ut “installdir”
			var text = File.ReadAllText(manifest);
			var m = Regex.Match(text, "\"installdir\"\\s*\"(?<d>.*?)\"");
			if (!m.Success) return null;
			var dir = m.Groups["d"].Value;

			var folder = Path.Combine(common, dir);
			if (!Directory.Exists(folder)) return null;

			// let etter exe i hele treet, velg største
			var exes = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories);
			if (exes.Length == 0) return null;
			return exes
				.OrderByDescending(f => new FileInfo(f).Length)
				.First();
		}

		private double _eyeHeightSetting;
		public double EyeHeightSetting
		{
			get => _eyeHeightSetting;
			set
			{
				if (Math.Abs(_eyeHeightSetting - value) > 0.0001)
				{
					_eyeHeightSetting = value;
					OnPropertyChanged(nameof(EyeHeightSetting));
				}

			}
		}

		private float _baseEyeHeight;



		private bool _isInNewOrRenameMode = false;

		private Process? _launchedProcess;
		private Game? _launchedGame;

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
		private CVRSystem? vrSystem;
		public VRStatusManager VREquipmentStatus { get; set; } = new VRStatusManager();

		private DispatcherTimer searchTimer;

	
		#endregion
		private GameGroupHandler gameGroupHandler;


        private bool isRenaming = false;
		private GameGroup gameGroupToRename;

        

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

		private DispatcherTimer vrHealthTimer;

		#endregion



		#region MainWindow Konstruktør
		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;

			SetupTimers();
			StartMonitoringWifiSignal();
			AllocateDebugConsole();

			RestartSteamVR();
			InitializeVrAndCalibration();

			this.Loaded += MainWindow_Loaded;
		}
		#endregion

		private void UpdateGameStatus()
		{
			if (_gameStatusManager == null)
				return;   // gjør ingenting om manager ikke er klar ennå

			_gameStatusManager.UpdateCurrentGameAndStatus();
			CurrentPlayer = _gameStatusManager.CurrentPlayer;
			CurrentStatus = _gameStatusManager.CurrentStatus;
		}


		private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (_gamesLoaded) return;
			_gamesLoaded = true;

			// --- 1) Finn Steam-path ---
			var steamPath = GetSteamInstallPathFromRegistry();
			Console.WriteLine($"[DEBUG] Steam folder: {steamPath}");

			// --- 2) Hent Steam-spill via Web-API ---
			string steamAPIKey = "384082C6759AAF7B6974A9CCE1ECF6CE";
			string steamID = "76561198081888308";
			var steamApi = new SteamApi(steamAPIKey, steamID);
			var steamGames = await steamApi.GetSteamGamesAsync();
			Console.WriteLine($"[DEBUG] SteamGames count: {steamGames.Count}");

			// --- 3) Berik med detaljer (sjanger, VR, nylig spilt etc.) ---
			gameDetailsFetcher = new GameDetailsFetcher(steamAPIKey, steamID);
			await Task.WhenAll(steamGames.Select(g => gameDetailsFetcher.AddDetailsAsync(g)));

			// --- 4) Fyll inn i AllGames og Games ---
			AllGames.Clear();
			Games.Clear();
			foreach (var g in steamGames)
			{
				// 4a) Sett prosessnavn / exe-sti
				if (!string.IsNullOrEmpty(g.InstallPath) && File.Exists(g.InstallPath))
				{
					g.ProcessName = Path.GetFileNameWithoutExtension(g.InstallPath);
				}
				else if (g.IsSteamGame)
				{
					var exe = GetSteamExePath(steamPath, g.AppID);
					if (exe != null)
					{
						g.InstallPath = exe;
						g.ProcessName = Path.GetFileNameWithoutExtension(exe);
					}
				}

				AllGames.Add(g);
				Games.Add(g);
			}

			// --- 5) Legg til non-Steam-spill uten duplikater ---
			var offline = new OfflineSteamGamesManager()
							  .GetNonSteamGames(steamPath)
							  .Where(g => !steamGames.Any(s => s.AppID == g.AppID));
			foreach (var g in offline)
			{
				g.GameImage = GameImage.LoadIconFromExe(g.InstallPath);
				AllGames.Add(g);
				Games.Add(g);
			}

			gameGroupHandler = new GameGroupHandler();
			// --- 6) Start game-status-timer og fyll grupper etc. ---
			_gameStatusManager = new GameStatusManager(AllGames);
			StartGameStatusTimer();  // <– Du må implementere denne (se under)
			LoadGameGroups();

			// Til slutt kan du så starte OpenVR-timere eller annet du trenger:
			StartVRStatusTimer();
			
		}

		private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			// 1) Oppdater databinding og settings
			EyeHeightSetting = e.NewValue;
			Properties.Settings.Default.EyeHeight = EyeHeightSetting;
			Properties.Settings.Default.Save();

			// 2) Faktisk bruk av høyden i VR
			ApplyHeightCalibration(EyeHeightSetting);
		}


		private void StartGameStatusTimer()
		{
			var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
			timer.Tick += (s, e) => UpdateGameStatus();  // UpdateGameStatus setter både CurrentPlayer og CurrentStatus
			timer.Start();
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
				if (filterHandler.FilterGame(
					 genreFilters,
					 typeFilters,
					 gameGroupHandler.GetGameGroups(),
					 game))
				{
					Games.Add(game);
				}
			}
			if (sender != null) { 
            Console.WriteLine($"Filters {((sender as CheckBox).IsChecked.Value ? "applied":"unapplied")}: " + ((CheckBox)sender).Content.ToString() + "\n\n");
            }


        }

		#endregion

		#region GameGroups
		private void CreateCategory_Click(object sender, RoutedEventArgs e)
		{
			_isInNewOrRenameMode = true;
			NewCategoryTextBox.Text = "";           // for ny kategori
			NewCategoryTextBox.Visibility = Visibility.Visible;
			NewCategoryTextBox.Focus();
		}

		// Håndter Enter/Escape istedenfor blind LostFocus
		private void NewCategoryTextBox_KeyDown(object sender,System.Windows.Input.KeyEventArgs e)
		{
			if (!_isInNewOrRenameMode) return;

			if (e.Key == Key.Enter)
			{
				CommitNewOrRename();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				CancelNewOrRename();
				e.Handled = true;
			}
		}

		private void CommitNewOrRename()
		{
			var text = NewCategoryTextBox.Text.Trim();
			if (!string.IsNullOrEmpty(text))
			{
				if (isRenaming)
				{
					// oppdater eksisterende
					gameGroupToRename.GroupName = text;
					// oppdater CheckBox-innhold
					var pair = gameGroupHandler.GetGameGroups()
											  .FirstOrDefault(g => g.Item2 == gameGroupToRename);
					if (pair.Item1 != null)
						pair.Item1.Content = text;
					gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");

					isRenaming = false;
				}
				else
				{
					// lag ny gruppe
					var newGroup = new GameGroup { GroupName = text };
					var cb = new CheckBox { Content = text, Style = (Style)FindResource("RoundedCheckBoxWithSourceSansFontStyle") };
					cb.Click += UpdateFilters;
					AddGameGroupCheckBox(cb, newGroup);
					gameGroupHandler.AddGameGroup(cb, newGroup);
					gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");

				}
			}
			EndNewOrRename();
		}

		private void CancelNewOrRename()
		{
			// bare clean up
			isRenaming = false;
			gameGroupToRename = null;
			EndNewOrRename();
		}

		private void EndNewOrRename()
		{
			NewCategoryTextBox.Visibility = Visibility.Collapsed;
			NewCategoryTextBox.Text = "";
			_isInNewOrRenameMode = false;
		}

		private void NewCategoryTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isRenaming)
            {
                if (!string.IsNullOrWhiteSpace(NewCategoryTextBox.Text))
                {
                    // Update the group name
                    gameGroupToRename.GroupName = NewCategoryTextBox.Text;
                    gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");

                    // Directly update the checkbox text by using the game group reference
                    var checkBoxToUpdate = gameGroupHandler.GetGameGroups()
                                             .FirstOrDefault(g => g.Item2 == gameGroupToRename).Item1;
                    if (checkBoxToUpdate != null)
                    {
                        checkBoxToUpdate.Content = NewCategoryTextBox.Text;
                        checkBoxToUpdate.Visibility = Visibility.Visible; // Make sure it's visible after renaming
                    }
                }

                isRenaming = false;
                gameGroupToRename = null;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(NewCategoryTextBox.Text))
                {
                    var newGroup = new GameGroup { GroupName = NewCategoryTextBox.Text };
                    var checkBox = new CheckBox
                    {
                        Content = newGroup.GroupName,
                        Style = (Style)FindResource("RoundedCheckBoxWithSourceSansFontStyle")
                    };
                    checkBox.Click += UpdateFilters;

                    gameGroupHandler.AddGameGroup(checkBox, newGroup);
                    AddGameGroupCheckBox(checkBox, newGroup);
                    gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");
                }
            }
            NewCategoryTextBox.Text = string.Empty;
            NewCategoryTextBox.Visibility = Visibility.Collapsed;
        }

      
        private void AddGameGroupCheckBox(CheckBox checkBox, GameGroup gameGroup)
        {
            var contextMenu = new ContextMenu();
            var renameMenuItem = new MenuItem { Header = "Gi nytt navn" };
            renameMenuItem.Click += (s, e) => RenameGameGroup(gameGroup);
            var editMenuItem = new MenuItem { Header = "Rediger" };
            editMenuItem.Click += (s, e) => EditGameGroup(gameGroup);
            var deleteMenuItem = new MenuItem { Header = "Slett" };
            deleteMenuItem.Click += (s, e) => DeleteGameGroup(gameGroup, checkBox);

            contextMenu.Items.Add(renameMenuItem);
            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(deleteMenuItem);
            checkBox.ContextMenu = contextMenu;

            GameCategoriesPanel.Children.Add(checkBox);

            // Sort the checkboxes alphabetically
            var sortedChildren = GameCategoriesPanel.Children.Cast<CheckBox>()
                .OrderBy(cb => cb.Content.ToString())
                .ToList();

            GameCategoriesPanel.Children.Clear();
            foreach (var child in sortedChildren)
            {
                GameCategoriesPanel.Children.Add(child);
            }
        }
		private void RenameGameGroup(GameGroup gameGroup)
		{
            isRenaming = true;
            _isInNewOrRenameMode = true;
			gameGroupToRename = gameGroup;
			NewCategoryTextBox.Text = gameGroup.GroupName;
			NewCategoryTextBox.Visibility = Visibility.Visible;
			NewCategoryTextBox.Focus();
		}





		private void EditGameGroup(GameGroup gameGroup)
		{
			// 1) Fjern Topmost på hovedvinduet slik at dialogen kan komme foran
			bool wasTopmost = this.Topmost;
			this.Topmost = false;

			// 2) Opprett og vis dialogen – sett Owner slik at vinduene "henger sammen"
			var dialog = new GameCategoryDialog(AllGames.ToList(), gameGroup)
			{
				Owner = this,
				Topmost = true
			};
			bool? result = dialog.ShowDialog();

			// 3) Gjenopprett Topmost på hovedvinduet
			this.Topmost = wasTopmost;

			// 4) (Evt. håndter resultat her…)
			if (result == true)
			{
				// Oppdater filter / UI om noe har endret seg
				UpdateFilters(null, null);
			}
		}



		private void DeleteGameGroup(GameGroup gameGroup, CheckBox checkBox)
        {
            gameGroupHandler.RemoveGameGroup(gameGroup.GroupName);
            GameCategoriesPanel.Children.Remove(checkBox);
            gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");
			UpdateFilters(null, null); // Refresh the filters
        }


        private void LoadGameGroups()
        {
            var checkBoxStyle = (Style)FindResource("RoundedCheckBoxWithSourceSansFontStyle");
            var gameGroups = gameGroupHandler.LoadGroupsFromFile(UpdateFilters, checkBoxStyle,AllGames.ToList());
            foreach (var (checkBox, group) in gameGroups)
            {
                AddGameGroupCheckBox(checkBox, group);
            }
        }




		#endregion

		#region OpenVR og VR Status

		/// <summary>
		/// Prøver å (gjen)initialisere OpenVR.
		/// </summary>
		private void InitializeOpenVR()
		{
			// 1) Steng ned eventuell gammel sesjon
			OpenVR.Shutdown();

			// 2) Initier ny
			EVRInitError error = EVRInitError.None;
			OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
			if (error != EVRInitError.None)
			{
				MessageBox.Show($"Kan ikke initialisere OpenVR: {error}");
				vrSystem = null;
			}
			else
			{
				// 3) Hent CVRSystem‐wrapperen direkte
				vrSystem = OpenVR.System;
			}
		}


		private void StartVRStatusTimer()
		{
			vrStatusTimer = new DispatcherTimer();
			vrStatusTimer.Interval = TimeSpan.FromSeconds(7);
			vrStatusTimer.Tick += (s, e) => UpdateVREquipmentStatus();
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
			vrStatusTimer?.Stop();
			_wifiSignalTimer?.Stop();
			volumeStatusTimer?.Stop();
			searchTimer?.Stop();
			vrHealthTimer.Stop();
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

			device.AudioEndpointVolume.MasterVolumeLevelScalar = volumeScalar;

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


	
		#endregion

		#region Spillbibliotek og Logg

		// Egen log knapp for å sjekke diverse feil i programmet.
		private void LogButton_Click(object sender, RoutedEventArgs e)
		{
			// Create and display the log window modally
			LogWindow logWindow = new LogWindow();
			logWindow.ShowDialog();
		}

		// Legg dette som felter i MainWindow-klassen:

		// 1) Den nye, konsise async-metoden som starter spillet og håndterer vindusflytting
		private async Task LaunchGameAsync(Game game)
		{
			Process? proc = null;

			// Start enten direkte exe eller via Steam URI
			if (!string.IsNullOrWhiteSpace(game.InstallPath) && File.Exists(game.InstallPath))
			{
				var psi = new ProcessStartInfo(game.InstallPath)
				{
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
			else if (!string.IsNullOrEmpty(game.AppID))
			{
				try
				{
					proc = Process.Start(new ProcessStartInfo($"steam://rungameid/{game.AppID}")
					{
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
				MessageBox.Show($"Ingen gyldig kjørbar fil eller Steam-ID funnet for «{game.Title}».", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// Hold på prosessen og game-objektet for evt. senere bruk
			_launchedProcess = proc;
			_launchedGame = game;

			// 2) Vent til spillet er klart til input (inntil 5 sek)
			await Task.Run(() =>
			{
				try
				{
					if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
					{
						// Dette kaster kun om prosessen ikke har GUI
						proc.WaitForInputIdle(5000);
					}
				}
				catch (InvalidOperationException)
				{
					// Ignorer — prosessen har ikke grafisk grensesnitt 
					Console.WriteLine("Vent-for-input feilet: prosessen har ikke GUI.");
				}
			});

			// 3) Flytt spillvinduet bak applikasjonen
			if (proc != null)
			{
				IntPtr hGame = proc.MainWindowHandle;
				if (hGame != IntPtr.Zero)
				{
					SetWindowPos(hGame, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
				}
			}

			// 4) Skyv også SteamVR-kontrollpanelet bak om det er åpent
			IntPtr hSteamVR = FindWindow(null, "SteamVR");
			if (hSteamVR != IntPtr.Zero)
			{
				SetWindowPos(hSteamVR, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
			}

			// 5) Oppdater status i statusbar
			UpdateGameStatus();
		}

		// 2) Den oppdaterte click-handleren som bare kaller LaunchGameAsync + resten av UI-logikken
		private async void LaunchGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (!(sender is Button btn && btn.DataContext is Game game))
				return;

			Console.WriteLine($">>> Starter spill: {game.Title}");
			await LaunchGameAsync(game);

			// 6) Gi UI-en et lite pusterom før VR-visning
			await Task.Delay(300);

			// 7) Bytt til fullskjerm/VR-view
			FullScreenButton_Click(null, null);
			StartVREmbedRetry();
			await EmbedVRSpectatorAsync();

			// 8) Oppdater resten av UI
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
			// Toggle Visibility
			StatusBarKalibrering.Visibility =
				StatusBarKalibrering.Visibility == Visibility.Visible
				? Visibility.Collapsed
				: Visibility.Visible;
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
			// 1) Hvis du allerede har en session, steng den
			OpenVR.Shutdown();

			// 2) Initier på nytt som overlay-app
			EVRInitError initError = EVRInitError.None;
			OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Overlay);
			if (initError != EVRInitError.None)
			{
				Console.WriteLine($"Kunne ikke init Overlay-session: {initError}");
				return;
			}

			// Nå er både OpenVR.Compositor og OpenVR.Chaperone ikke-null

			// 3) Sett ønsket tracking-space
			OpenVR.Compositor.SetTrackingSpace(origin);

			// 4) Nullstill zero-pose
			OpenVR.Chaperone.ResetZeroPose(origin);

			// 5) Hent oppdaterte poser (du kan eventuelt sende tomme arrays i stedet for null)
			var renderPoses = new Valve.VR.TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
			var gamePoses = new Valve.VR.TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
			OpenVR.Compositor.WaitGetPoses(renderPoses, gamePoses);

			Console.WriteLine($"Recenter fullført: {origin}");
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

		/// <summary>
		/// Justerer verdens Y-offset slik at brukeren oppleves høyere/lavere.
		/// </summary>
		private void ApplyHeightCalibration(double heightMeters)
		{
			if (vrSystem == null)
				return;

			// Hent rå zero‐pose
			HmdMatrix34_t rawPose = vrSystem.GetRawZeroPoseToStandingAbsoluteTrackingPose();

			// Sett world‐offset direkte til -(ønsket høyde)
			rawPose.m7 = -(float)heightMeters;

			var chaperoneSetup = OpenVR.ChaperoneSetup;
			chaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref rawPose);
			chaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live);
		}


		/// <summary>
		/// Eksempel: henter ut gjeldende kalibrering fra ChaperoneSetup.
		/// </summary>
		private float LoadCurrentHeightCalibration()
		{
			if (vrSystem == null)
				throw new InvalidOperationException("VR‐system ikke klart");

			HmdMatrix34_t pose = new HmdMatrix34_t();
			bool ok = OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref pose);
			if (!ok)
				throw new InvalidOperationException("Kunne ikke hente zero‐pose");

			// m7 er Y‐offset i matrisen, negativ av øyehøyde
			return -(float)pose.m7;
		}


		/// <summary>
		/// Sjekk om vrSystem lever — ellers forsøk re-init.
		/// </summary>
		private void EnsureVrSystemAlive()
		{
			try
			{
				// Dette kaster hvis systemet ikke er gyldig
				bool onDesktop = vrSystem != null && vrSystem.IsDisplayOnDesktop();
				if (!onDesktop)
					InitializeOpenVR();
			}
			catch
			{
				InitializeOpenVR();
			}
		}

		/// <summary>
		/// 1) Setter opp alle DispatcherTimer-instanser: søk, klokke, VR-status osv.
		/// </summary>
		private void SetupTimers()
		{
			// 500 ms søketimer
			searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
			searchTimer.Tick += SearchTimer_Tick;

			// Klokke hver sekund
			var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm");
			clockTimer.Start();

			// VR‐helse hver 5 min
			vrHealthTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
			vrHealthTimer.Tick += (s, e) => EnsureVrSystemAlive();
			vrHealthTimer.Start();

			// VR‐status hver 7 s
			StartVRStatusTimer();
		}


		/// <summary>
		/// 2) Starter bakgrunnskonsoll for debug‐utskrifter.
		/// </summary>
		private void AllocateDebugConsole()
		{
			AllocConsole();
		}

		/// <summary>
		/// 3) Restarter SteamVR helt rent.
		/// </summary>
		private void RestartSteamVR()
		{
			// Steng ned først
			Process.Start("cmd.exe", "/C taskkill /F /IM vrserver.exe /IM vrmonitor.exe");
			Thread.Sleep(3000);

			// Start på nytt
			Process.Start(@"C:\Program Files (x86)\Steam\Steam.exe", "-applaunch 250820");
		}

		/// <summary>
		/// 4) Initialiserer OpenVR, henter base‐høyde og setter slider‐start.
		/// </summary>
		private void InitializeVrAndCalibration()
		{
			// Init OpenVR
			EVRInitError initError = EVRInitError.None;
			OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Background);
			if (initError != EVRInitError.None)
			{
				MessageBox.Show($"Kan ikke initialisere OpenVR: {initError}");
				return;
			}

			// Hent CVRSystem‐instans
			InitializeOpenVR();              // din eksisterende metode som setter vrSystem

			// Last nåværende stående zero‐pose og seed slider
			try
			{
				float baseHeight = LoadCurrentHeightCalibration();  // returnerer meter
				EyeHeightSetting = baseHeight;
				Properties.Settings.Default.EyeHeight = baseHeight;
				Properties.Settings.Default.Save();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Feil ved lasting av base‐høyde: " + ex.Message);
			}
		}

		#region MAPPESTRUKTUR KODE UNDER
		

		#endregion

	}
}

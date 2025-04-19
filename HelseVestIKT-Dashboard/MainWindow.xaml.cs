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
using CheckBox = System.Windows.Controls.CheckBox;
using Dialogs;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using WpfPanel = System.Windows.Controls.Panel;
using System.IO;
using Path = System.IO.Path;


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

		#region Simulering av tastetrykk "ESC" for å pause et spill
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

		private DispatcherTimer _vrEmbedTimer;
		private int _vrEmbedAttempts = 0;	
		private const int MaxVREmbedAttempts = 20;

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

		// The D3D11 device pointer acquired from your D3D11DeviceManager.
		private IntPtr d3d11DevicePointer;
		private D3DImage? d3dImage;
		private DispatcherTimer renderTimer;
		private ID3D11Device? d3d11Device;
		private ID3D11Texture2D? _sharedTexture;
		private D3D11DeviceManager? deviceManager;
		private ID3D11RenderTargetView? shareTextureRTV;	

		#endregion



		#region MainWindow Konstruktør
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

			_gameStatusManager = new GameStatusManager(AllGames);

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

			// Lukk SteamVR
			Process.Start("cmd.exe", "/C taskkill /F /IM vrserver.exe /IM vrmonitor.exe");

			// Vent litt før du starter på nytt
			System.Threading.Thread.Sleep(3000);

			// Start SteamVR på nytt
			Process.Start("C:\\Program Files (x86)\\Steam\\Steam.exe", "-applaunch 250820");

			InitializeOpenVR();
			StartVRStatusTimer();
			StartMonitoringWifiSignal();

			
		}
		#endregion

		private void UpdateGameStatus()
		{
			// Use the GameStatusManager to update the game and status
			_gameStatusManager.UpdateCurrentGameAndStatus();

			// Update the UI with the current game and status
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
            gameGroupHandler = new GameGroupHandler();
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

			LoadGameGroups();
			// Resten av initialiseringen
			await Task.Delay(2000);
			
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

                if (filterHandler.FilterGame(genreFilters, typeFilters,gameGroupHandler.GetGameGroups(), game))
                {
                    Console.WriteLine("Game Added: " + game.Title);
                    Games.Add(game);
                }
            }
			if(sender != null) { 
            Console.WriteLine($"Filters {((sender as CheckBox).IsChecked.Value ? "applied":"unapplied")}: " + ((CheckBox)sender).Content.ToString() + "\n\n");
            }


        }

        #endregion

        #region GameGroups
        private void CreateCategory_Click(object sender, RoutedEventArgs e)
        {
            NewCategoryTextBox.Visibility = Visibility.Visible;
            NewCategoryTextBox.Focus();
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
            gameGroupToRename = gameGroup;
            NewCategoryTextBox.Text = gameGroup.GroupName;
            NewCategoryTextBox.Visibility = Visibility.Visible;
            NewCategoryTextBox.Focus();
        }


      


        private void EditGameGroup(GameGroup gameGroup)
        {

            GameCategoryDialog dialog = new GameCategoryDialog(AllGames.ToList(),gameGroup);
            dialog.GameGroupChanged += (s, e) =>
            {
                gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");
                UpdateFilters(null, null); // Refresh the filters
            };
            
			dialog.ShowDialog();



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

		#endregion

		#region Fullscreen og VR Embedding

		private void FullScreenButton_Click(object sender, RoutedEventArgs e)
		{
			// Skjul GameLibrary-området
			GameLibraryArea.Visibility = Visibility.Collapsed;

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

			float volumeScalar = (float)(e.NewValue / 100.0);
			var enumerator = new MMDeviceEnumerator();
			MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			//device.AudioEndpointVolume.MasterVolumeLevelScalar = volumeScalar; TEMP

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
			// Metode for å avslutte spillet ved å trykke på knappen uten å exite applikasjonen
			HeaderGrid.Visibility = Visibility.Visible;


		}

			private void Nodstopp_Click(object sender, RoutedEventArgs e)
		{
			// Emergency stop button if application is not responding and VR functions are not working
			// This is a last resort to close the application

			if (MessageBox.Show("Er du sikker på at du vil avslutte programmet?", "Avslutt programmet", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				System.Windows.Application.Current.Shutdown();
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

			// Sjekk om non‑Steam‑spill har en gyldig .exe-bane:
			bool hasExe = !string.IsNullOrWhiteSpace(game.InstallPath)
						  && File.Exists(game.InstallPath);

			if (hasExe)
			{
				// Non‑Steam: start direkte via exe
				var psi = new ProcessStartInfo
				{
					FileName = game.InstallPath,
					WorkingDirectory = Path.GetDirectoryName(game.InstallPath)!
				};
				try
				{
					Process.Start(psi);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Kunne ikke starte spillet: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}
			else if (!string.IsNullOrWhiteSpace(game.AppID))
			{
				// Fallback for Steam‑spill: bruk steam://-URI
				try
				{
					Process.Start(new ProcessStartInfo
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
				// Hverken exe eller AppID → gi tydelig feilmelding
				MessageBox.Show(
				  $"Ingen gyldig kjørbar fil eller Steam‑ID funnet for «{game.Title}».",
				  "Feil",
				  MessageBoxButton.OK,
				  MessageBoxImage.Error);
				return;
			}

			// Dersom du ønsker å gå rett i VR‑visning:
			FullScreenButton_Click(null, null);
			await EmbedVRSpectatorAsync();
			

			// Oppdater UI
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
				await Task.Delay(1000);
			}

			Console.WriteLine("Unable to embed VR View after multiple attempts.");
		}


		#endregion

		#region VR-Kalibrering og Funksjoner

		private void KalibrerKnapp_Click(object sender, RoutedEventArgs e)
		{
			KalibreringPopup.IsOpen = true;

		}

		private void TilbakeKnapp_Click(object sender, RoutedEventArgs e)
		{
			KalibreringPopup.IsOpen = false;

		}

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

		#endregion

		#region Kommentert ut en metode om ResetButton_Click

		/*
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

			*/

		#endregion

	}
}

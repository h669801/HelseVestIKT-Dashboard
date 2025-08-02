using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.Services.HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.ViewModels;
using System.Windows;
using System.Windows.Media;

namespace HelseVestIKT_Dashboard.Views
{
	public partial class MainWindow : Window
	{
		private readonly VREmbedder _vrEmbedder;
		private readonly IVrAutomationService _vrAutomationService;
		public MainWindow()
		{
			InitializeComponent();

			// 1) Hent profil-data
			var data = ProfileStore.Load();
			var profile = data.Profiles
				.FirstOrDefault(p => p.Name == data.LastProfileName)
				?? new SteamProfile { Name = "Default", ApiKey = "", UserId = "" };
			if (string.IsNullOrWhiteSpace(profile.ApiKey) || string.IsNullOrWhiteSpace(profile.UserId))
			{
				System.Windows.MessageBox.Show("Ugyldig profil!", "Feil", MessageBoxButton.OK, MessageBoxImage.Warning);
				Close();
				return;
			}

			// 2) Initialiser Steam-tjenester for lasting av spill
			var steamApi = new SteamApi(profile.ApiKey, profile.UserId);
			var fetcher = new GameDetailsFetcher(profile.ApiKey, profile.UserId);
			var offlineMgr = new OfflineSteamGamesManager();
			var gameLoadService = new GameLoadService(steamApi, fetcher, offlineMgr);

			// 4) Initialiser VR-embedder
			_vrEmbedder = new VREmbedder(VRHost, MainContentGrid, GameLibraryArea, ReturnButton);
			 var vrAutomation = new VrAutomationService();

			// 3) Opprett og bind ViewModel
			var vm = new MainWindowViewModel(
				gameLoadService,
				new FilterService(),
				new GameGroupHandler(),
				_vrEmbedder, new AudioService(), vrAutomation);
			vm.SetVREmbedder(_vrEmbedder);
			DataContext = vm;

			this.Closed += (_, __) => vrAutomation.Dispose();

			SearchBox.Text = "Søk etter spill...";
			SearchBox.Foreground = System.Windows.Media.Brushes.Gray;



			// 5) Kjør init-kommando på Loaded
			Loaded += async (_, __) => {
				await vm.InitializeCommand.ExecuteAsync(null);
				vm.StartVrWatcher();
			};

			// KeyDown += Window_KeyDown;
		}

		/*private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (DataContext is MainWindowViewModel vm)
				vm.OnKeyDown(e);
		}
		*/

		public void ApplyLockState(bool isLocked)
		{
			if (isLocked)
			{

				Win32.EnableKeyBlock(); // 🛑 Blokker tastatur-snarlinjer
			}
			else
			{
				Win32.DisableKeyBlock(); // ✅ Slå på igjen tastaturet
			}
		}
	}
}









/*using Dialogs.Views;
using HelseVestIKT_Dashboard.Helpers;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.ViewModels;
using HelseVestIKT_Dashboard.Views;
using Microsoft.Win32;
using SteamKit2;
using SteamKit2.GC.Dota.Internal;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Valve.VR;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace HelseVestIKT_Dashboard.Views
{

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
       

		#region Egenskaper og Variabler
		private float _baseHeight;
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

        private bool _isLocked = true;
        public bool IsLocked => _isLocked;
		private string _logPath = "";


        private string _currentPlayer = "Ingen spill kjører";
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

		

		private float _eyeHeightSetting;
		public float EyeHeightSetting { get; set; }

		// Under klasse-deklarasjonen:
		private bool _isTouchScrolling;
		private System.Windows.Point _touchStartPoint;
		private double _initialScrollOffset;


		public ObservableCollection<Game> Games { get; } = new ObservableCollection<Game>();
		private ObservableCollection<Game> AllGames { get; } = new ObservableCollection<Game>();

		public VRStatusManager VREquipmentStatus { get; } = new VRStatusManager();

		// tjenester og manager‐felt som du faktisk bruker:
		// feltdeklarasjoner i MainWindow:

		private DispatcherTimer _vrMonitorTimer;
		private bool _isUpdatingVolumeSlider = false;
		public SpillKategori? ValgtKategori { get; set; }
		private GameGroup gameGroupToRename;
		private bool _isInNewOrRenameMode = false;
		private bool _gamesLoaded = false;
		private bool isRenaming = false;
		private OfflineSteamGamesManager _offlineMgr;
		private SteamProfile _currentProfile;
		private DispatcherTimer volumeStatusTimer;
		private SteamApi _steamApi;
		private GameDetailsFetcher _gameDetailsFetcher;
		private readonly SearchService _searchService;
		private readonly TimerService _timerService;
		private readonly VRInitService _initService;
		private readonly GameGroupHandler _gameGroupHandler = new GameGroupHandler();
		private readonly GameStatusService _gameStatusManager;
		private readonly GameProcessService _processService;
		private GameLoadService _gameLoadService;
		private VRStatusService _statusService;
		private readonly VRDashboardService _dashSvc;
		private VRCalibrator _calibrator;
		private readonly VREmbedder _embedder;
		private readonly AudioService _audioService;
		private WifiStatusManager? _wifiStatusManager;
		private readonly FilterService _filterService;
		private readonly InputService _inputService;
		private readonly StatusBarService _statusBarService;

		//VR WATCHER
		private readonly SteamVrAutomation _steamVrAutomation = new SteamVrAutomation();

		//ViewModel
		private readonly MainWindowViewModel _vm;

		public MainWindow()
		{
			InitializeComponent();
			
			// — 0) Opprett alt som aldri skal være null —
			_gameGroupHandler = new GameGroupHandler();
			_searchService = new SearchService(SearchBox, Games, AllGames);
			_timerService = new TimerService();
			_initService = new VRInitService();
			_gameStatusManager = new GameStatusService(AllGames);
			_processService = new GameProcessService(_gameStatusManager);
			_filterService = new FilterService();
			_inputService = new InputService();
			//_statusBarService = new StatusBarService();
			DataContext = new MainWindowViewModel(_gameLoadService, _filterService, _gameGroupHandler);

			// — 1) TimerService for klokke, status og VR-helse —
			_timerService.TickEverySecond((s, e) => CurrentTime = DateTime.Now.ToString("HH:mm"));
			_timerService.TickEveryTwoSeconds((s, e) => UpdateGameStatus());
			// _timerService.TickEveryFiveMinutes((s, e) => _initService.EnsureVrSystemAlive());
			_timerService.Start();

			// — 3) VR-tjenester & embedder —
			_dashSvc = new VRDashboardService(_processService, _gameStatusManager, _initService);
			_embedder = new VREmbedder(VRHost, MainContentGrid, GameLibraryArea, ReturnButton);
			
			ExitButton.Visibility = Visibility.Collapsed; // Skjul Exit-knappen i startmodus

			// — 4) Audio & Wifi —
			_audioService = new AudioService();
			SyncVolumeSliderWithSystem();
			_audioService.VolumeChanged += OnServiceVolumeChanged;
			_wifiStatusManager = new WifiStatusManager(WifiSignalIcon);
			_wifiStatusManager.StartMonitoringWifiSignal();

			AllocateDebugConsole();

			// — 5) Hook Loaded og aktiver/deaktiver 
			this.Loaded += MainWindow_Loaded;
			Activated += MainWindow_Activated;
         } 

public MainWindow()
		{
			InitializeComponent();

			// 1) Profile + Steam services
			LoadOrCreateProfile();
			if (!ValidateProfile()) return;

			_steamApi = new SteamApi(_currentProfile.ApiKey, _currentProfile.UserId);
			_gameDetailsFetcher = new GameDetailsFetcher(_currentProfile.ApiKey, _currentProfile.UserId);
			_offlineMgr = new OfflineSteamGamesManager();
			_gameLoadService = new GameLoadService(_steamApi, _gameDetailsFetcher, _offlineMgr);

			// 2) “Never-null” services for the view
			_filterService = new FilterService();
			_gameGroupHandler = new GameGroupHandler();

			// 3) Create VM and attach to DataContext
			_vm = new MainWindowViewModel(
				_gameLoadService,
				_filterService,
				_gameGroupHandler
			);
			

			// 5) All the rest of your “never-null” UI wiring, timers and VR setup…
			_timerService = new TimerService();
			_processService = new GameProcessService(_gameStatusManager);
			_initService = new VRInitService();
			_gameStatusManager = new GameStatusService(AllGames);
			_searchService = new SearchService(SearchBox, Games, AllGames);
			_dashSvc = new VRDashboardService(_processService, _gameStatusManager, _initService);
			_embedder = new VREmbedder(VRHost, MainContentGrid, GameLibraryArea, ReturnButton);
			_audioService = new AudioService();
			_wifiStatusManager = new WifiStatusManager(WifiSignalIcon);
			
			AllocateDebugConsole();

			DataContext = _vm;

			// 4) Hook a single Loaded event
			Loaded += async (_, __) =>
			{
			
				// 4b) Then do your old MainWindow_Loaded logic (VR init, timers, etc.)
				await InitializeApplicationAsync();
				_steamVrAutomation.Start();
			};

			Activated += MainWindow_Activated;
		}
		#endregion
		//Det som blir brukt i MainWindow.xaml.cs
		private void VRHost_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			_embedder.ResizeHost(sender, e);
		}

		public void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			_inputService.HandleKeyDown(sender, e);
		}



	}
}
*/
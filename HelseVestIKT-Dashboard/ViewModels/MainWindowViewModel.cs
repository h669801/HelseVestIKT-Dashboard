using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;                      // Legg til fra CommunityToolkit.Mvvm        // Hvis du har egne commands her
using Dialogs.Views;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.Services.HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.Views;
using SharpDX.Direct3D9;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Valve.VR;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace HelseVestIKT_Dashboard.ViewModels
{
	public class MainWindowViewModel : ObservableObject
	{
		// ── Services & dependencies ───────────────────────────────
		private SteamProfile _profile;
		private SteamApi _steamApi;
		private GameDetailsFetcher _gameDetailsFetcher;
		private readonly OfflineSteamGamesManager _offlineMgr = new OfflineSteamGamesManager();
		private readonly GameLoadService _gameLoadService;
		private readonly FilterService _filterService;
		private readonly GameGroupHandler _groupHandler;
		private readonly TimerService _timerService = new TimerService();
		private readonly VRInitService _vrInitService = new VRInitService();
		private readonly VRCalibrator _calibrator = new VRCalibrator();
		private readonly VRStatusManager VREquipmentStatus = new VRStatusManager();
		private readonly VRStatusService _vrStatusService;
		private readonly VRDashboardService _vrDashboardService;
		private  VREmbedder _vrEmbedder;
		private readonly GameStatusService _gameStatusService;
		private readonly AudioService _audioService = new AudioService();
		private bool _isSpectating;
		private readonly IVrAutomationService _vrAutomation;
	
	

		// ── Data & binding ───────────────────────────────────────
		private List<Game> _allGames = new List<Game>();
		public ObservableCollection<Game> Games { get; } = new ObservableCollection<Game>();
		public ObservableCollection<CategoryFilterItem> CategoryFilters { get; } = new ObservableCollection<CategoryFilterItem>();
		public ObservableCollection<FilterOption> GenreFilters { get; } = new ObservableCollection<FilterOption>();
		public ObservableCollection<FilterOption> TypeFilters { get; } = new ObservableCollection<FilterOption>();
		public IReadOnlyList<Game> AllGames => _allGames;
		public IRelayCommand<GameGroup> AddGameToCategoryCommand { get; }
		public ImageSource VolumeIcon => _audioService.VolumeIcon;
		public ObservableCollection<GameGroupViewModel> GameGroups { get; }
		= new ObservableCollection<GameGroupViewModel>();


		private bool _filterSteamSpill;
		public bool FilterSteamSpill
		{ get => _filterSteamSpill; 
			set { SetProperty(ref _filterSteamSpill, value); ApplyFilters();
			}
		}

		private CategoryFilterItem _editingCategoryItem;


		private bool _isLocked;
		public bool IsLocked
		{
			get => _isLocked;
			set
			{
				SetProperty(ref _isLocked, value);
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						if (Application.Current.MainWindow is MainWindow mw)
							mw.ApplyLockState(value);
					});
				}
			}
		}



		private bool _isEditingCategory;
		public bool IsEditingCategory
		{
			get => _isEditingCategory;
			private set => SetProperty(ref _isEditingCategory, value);
		}

		private string _newCategoryName;
		public string NewCategoryName
		{
			get => _newCategoryName;
			set { _newCategoryName = value; OnPropertyChanged(); }
		}

		public bool IsSpectating
		{
			get => _isSpectating;
			set => SetProperty(ref _isSpectating, value);
		}

		private string _currentTime;
		public string CurrentTime
		{
			get => _currentTime;
			private set { _currentTime = value; OnPropertyChanged(); }
		}

		private string _currentPlayer = "Ingen spill kjører";
		public string CurrentPlayer
		{
			get => _currentPlayer;
			set { _currentPlayer = value; OnPropertyChanged(); }
		}


		private string _searchText = "";
		public string SearchText
		{
			get => _searchText;
			set
			{
				if (_searchText == value) return;
				_searchText = value;
				OnPropertyChanged();
				ApplyFilters();
			}
		}



		private double _volumePercent;
		public double VolumePercent
		{
			get => _volumePercent;
			set
			{
				if (SetProperty(ref _volumePercent, value))
					_audioService.CurrentVolume = (float)(value / 100);
			}
		}

		private string _volumeText;
		public string VolumeText
		{
			get => _volumeText;
			private set => SetProperty(ref _volumeText, value);
		}

		private bool _isCalibrationPanelVisible;
		public bool IsCalibrationPanelVisible
		{
			get => _isCalibrationPanelVisible;
			set => SetProperty(ref _isCalibrationPanelVisible, value);
		}



		protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string name = null)
		{
			if (EqualityComparer<T>.Default.Equals(backingField, value)) return false;
			backingField = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			return true;
		}



		/// <summary>
		/// Applies search, then genre/type/group filtering, and updates the UI collection.
		/// </summary>
		public void ApplyFilters()
		{
			// 0) Tekst-søk
			IEnumerable<Game> søkResultat = string.IsNullOrWhiteSpace(SearchText)
				? _allGames
				: _allGames.Where(g =>
					g.Title.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

			var selectedGenres = GenreFilters
				.Where(f => f.IsChecked)
				.Select(f => f.Key);

			var selectedTypes = TypeFilters
				.Where(f => f.IsChecked)
				.Select(f => f.Key);

			var selectedGroups = CategoryFilters
				.Where(cf => cf.IsChecked)
				.Select(cf => cf.Group);

			var filtrert = _filterService.ApplyFilters(
				selectedGenres, selectedTypes, selectedGroups, _allGames);

			Games.Clear();
			foreach (var g in filtrert)
				Games.Add(g);
		}



		private float _eyeHeightSetting;
		public float EyeHeightSetting
		{
			get => _eyeHeightSetting;
			set
			{
				_eyeHeightSetting = value; OnPropertyChanged();
				// Flytt høyde‐påslag til VRCalibrator, ikke VRInitService
				_calibrator.ApplyHeight(value);
			}
		}


		// ── Commands ──────────────────────────────────────────────
		public IAsyncRelayCommand InitializeCommand { get; }
		public IAsyncRelayCommand<Game> LaunchGameCommand { get; }
		public IRelayCommand PauseCommand { get; }
		public IRelayCommand ExitApplicationCommand { get; }
		public IAsyncRelayCommand RecenterSeatedCommand { get; }
		public IAsyncRelayCommand RecenterStandingCommand { get; }
		public IRelayCommand OpenSettingsCommand { get; }
		public IRelayCommand ReturnHomeCommand { get; }
		public IRelayCommand EnterFullscreenCommand { get; }
		public IRelayCommand EmergencyStopCommand { get; }
		public IRelayCommand QuitGameCommand { get; }
		public IRelayCommand<KeyEventArgs> WindowKeyDownCommand { get; }
		public IRelayCommand<SizeChangedEventArgs> VrHostResizedCommand { get; }
		public IRelayCommand<double> ChangeVolumeCommand { get; }
		public IRelayCommand<TouchEventArgs> GameLibraryTouchDownCommand { get; }
		public IRelayCommand<TouchEventArgs> GameLibraryTouchMoveCommand { get; }
		public IRelayCommand<TouchEventArgs> GameLibraryTouchUpCommand { get; }
		public IRelayCommand<SizeChangedEventArgs> VrHostResizedCommon { get; }
		public IRelayCommand OpenCategoryEditorCommand { get; }
		public IRelayCommand ToggleVrViewCommand { get; }
		public IRelayCommand ToggleCalibrationPanelCommand { get; }



		public IAsyncRelayCommand RoomSetupCommand { get; }
		public IRelayCommand HeightCalibrationCommand { get; }
		public IRelayCommand HideSideMenuCommand { get; }
		public IRelayCommand ShowSideMenuCommand { get; }
		public IRelayCommand BeginAddCategoryCommand { get; }
		public IRelayCommand BeginRenameCategoryCommand { get; }
		public IRelayCommand CancelCategoryCommand { get; }
		public IRelayCommand CommitCategoryCommand { get; }
		public IRelayCommand DeleteCategoryCommand { get; }



		// ── Constructor ────────────────────────────────────────────

		public MainWindowViewModel(
			GameLoadService gameLoadService,
			FilterService filterService,
			GameGroupHandler groupHandler,
			VREmbedder vrEmbedder,
			AudioService audioService,
			IVrAutomationService vrAutomation)
		{
			_gameStatusService = new GameStatusService(_allGames);
			_vrStatusService = new VRStatusService(VREquipmentStatus);
			_vrDashboardService = new VRDashboardService(new GameProcessService(_gameStatusService), _gameStatusService, _vrInitService);
			_gameLoadService = gameLoadService;
			_audioService = audioService;
			_filterService = filterService;
			_groupHandler = groupHandler;
			_vrEmbedder = vrEmbedder;
			_vrAutomation = vrAutomation;

			ToggleVrViewCommand = new RelayCommand(() => 
			_vrAutomation.EnsureVrViewVisible()
			);

			VREquipmentStatus = new VRStatusManager();

			_vrStatusService.StartStatusUpdates(TimeSpan.FromSeconds(5));

			ToggleCalibrationPanelCommand = new RelayCommand(() =>
			{
				IsCalibrationPanelVisible = !IsCalibrationPanelVisible;
			});

			// 1) sjanger‐filtre
			var genreMap = new Dictionary<string, string>
			{
				["Action"] = "Action",
				["Eventyr"] = "Adventure",
				["Flerspiller"] = "Multiplayer",
				["Indie"] = "Indie",
				["Lettbeint"] = "Casual",
				["Massivt flerspill"] = "Massively Multiplayer",
				["Racing"] = "Racing",
				["Rollespill"] = "RPG",
				["Simulering"] = "Simulation",
				["Sport"] = "Sports",
				["Strategi"] = "Strategy"
			};

			// 2) Fyll GenreFilters
			foreach (var kv in genreMap)
				GenreFilters.Add(new FilterOption(kv.Key, kv.Value));
			foreach (var f in GenreFilters)
				f.PropertyChanged += (_, __) => ApplyFilters();

			// 2) type‐filtre
			var typeMap = new Dictionary<string, string>
			{
				["Steam-spill"] = "Steam",
				["Andre spill"] = "Other",
				["Vis kun favoritter"] = "Favorite",
				["Vis kun nylig spilt"] = "Recent",
				["VR-spill"] = "VR"
			};

			// 3) Fyll TypeFilters
			foreach (var kv in typeMap)
				TypeFilters.Add(new FilterOption(kv.Key, kv.Value));
			foreach (var f in TypeFilters)
				f.PropertyChanged += (_, __) => ApplyFilters();
	
			// VR‐status og dashboard‐service krever modell‐objekt
			VrHostResizedCommand = new RelayCommand<SizeChangedEventArgs>(args =>
			{
				var newSize = args.NewSize;
				var oldSize = args.PreviousSize;
			});

	
			// 4) Fyll CategoryFilters (om du har noen lagrede dynamiske grupper)
			LoadGroupsAndFilters();    // denne _må_ opprette Itemene i CategoryFilters

			// 5) Nå som alle tre samlinger er ikke‐null og ferdig fylt:
			

			// Kommandoer
			InitializeCommand = new AsyncRelayCommand(InitializeAsync);
			LaunchGameCommand = new AsyncRelayCommand<Game>(LaunchGameAsync);
			PauseCommand = new RelayCommand(() => _vrDashboardService.Pause());
			RecenterSeatedCommand = new AsyncRelayCommand(() => _calibrator.RecenterAsync(ETrackingUniverseOrigin.TrackingUniverseSeated));
			RecenterStandingCommand = new AsyncRelayCommand(() => _calibrator.RecenterAsync(ETrackingUniverseOrigin.TrackingUniverseStanding));
			OpenSettingsCommand = new RelayCommand(() =>
			{
				// 1. Vis PIN-vindu først
				var pinWindow = new PinWindow
				{
					Owner = Application.Current.MainWindow
				};

				if (pinWindow.ShowDialog() != true)
					return; // Avbrutt eller feil PIN

				// 2. Hvis PIN er riktig, åpne SettingsWindow
				var settings = new SettingsWindow(this)
				{
					Owner = Application.Current.MainWindow
				};

				if (settings.ShowDialog() == true)
				{
					// 1. Bytt profil hvis valgt
					var selected = settings.ViewModel?.SelectedProfile;
					if (selected != null)
					{
						_profile = selected;
						InitializeCommand.Execute(null);
					}

					// 2. Restart
					if (settings.ViewModel?.RestartRequested == true)
						Process.Start("shutdown", "/r /t 0");

					// 3. Lås opp
					if (settings.ViewModel?.UnlockRequested == true)
						IsLocked = false;
				}
			});

			ExitApplicationCommand = new RelayCommand(() =>
			{
				var result = MessageBox.Show(
					"Er du sikker på at du vil avslutte applikasjonen?",
					"Bekreft avslutning",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question
				);

				if (result == MessageBoxResult.Yes)
				{
					Application.Current.Shutdown();
				}
			});

			RoomSetupCommand = new AsyncRelayCommand(RoomSetupAsync);


			HeightCalibrationCommand = new RelayCommand(() =>
			{
				try
				{
					_calibrator.ApplyHeight(EyeHeightSetting);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Feil under høydejustering: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				}

			});

			RecenterSeatedCommand = new AsyncRelayCommand(async () =>
			{
				try
				{
					await _calibrator.RecenterAsync(ETrackingUniverseOrigin.TrackingUniverseSeated);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Feil under resentrering: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			});

			RecenterStandingCommand = new AsyncRelayCommand(async () =>
			{
				try
				{
					await _calibrator.RecenterAsync(ETrackingUniverseOrigin.TrackingUniverseStanding);
				}
				catch
				{
					MessageBox.Show("Feil under resentrering til stående modus.", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			});

			ReturnHomeCommand = new RelayCommand(() => { _vrEmbedder.ExitFullScreen(); IsSpectating = false; });
			EnterFullscreenCommand = new RelayCommand(() => { _vrEmbedder.EnterFullScreen(); IsSpectating = true; });
			EmergencyStopCommand = new RelayCommand(() => _vrStatusService.StopStatusUpdates());
			QuitGameCommand = new RelayCommand(() => _vrDashboardService.CloseCurrentGame());

			BeginAddCategoryCommand = new RelayCommand(() => BeginEditCategory(null));
			BeginRenameCategoryCommand = new RelayCommand<CategoryFilterItem>(ci => BeginEditCategory(ci));
			CancelCategoryCommand = new RelayCommand(() => CancelEditCategory());
			CommitCategoryCommand = new RelayCommand(() => CommitEditCategory());
			DeleteCategoryCommand = new RelayCommand<CategoryFilterItem>(ci => DeleteCategory(ci));
			OpenCategoryEditorCommand = new RelayCommand<CategoryFilterItem>(ShowCategoryEditor);


			// Timers
			var t1 = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			t1.Tick += (_, __) => CurrentTime = DateTime.Now.ToString("HH:mm");
			t1.Start();
			var t2 = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
			t2.Tick += (_, __) => UpdateCurrentPlayer();
			t2.Start();

			// 1) Initial synk mot systemvolum:
			VolumePercent = _audioService.CurrentVolume * 100;
			VolumeText = $"{(int)VolumePercent}%";
			OnPropertyChanged(nameof(VolumeIcon)); // ikon finnes allerede i servicen


			// 2) Abonner på endringer fra AudioService
			_audioService.VolumeChanged += (_, newVol) =>
			{
				// merk: setter VolumePercent _vil_ kalle back til servicen,
				// men for eksterne endringer er newVol kilden, så det er trygt:
				VolumePercent = newVol * 100;
				VolumeText = $"{(int)VolumePercent}%";
				OnPropertyChanged(nameof(VolumeIcon));
			};

			// Du kan også kjøre én gang for å synkronisere UI med gjeldende volum:
			var initial = _audioService.CurrentVolume;
			VolumePercent = initial * 100;
			VolumeText = $"{(int)(initial * 100)}%";

			ApplyFilters();
		}

		public void StartVrWatcher() => _vrAutomation.StartWatching();

		private void BeginEditCategory(CategoryFilterItem item)
		{
			_editingCategoryItem = item;
			NewCategoryName = item?.Group.GroupName ?? "";
			IsEditingCategory = true;    // now fires PropertyChanged
		}

		private async Task RoomSetupAsync()
		{
			// 1) Locate the SteamVR Room Setup .exe
			string exePath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				"Steam", "steamapps", "common", "SteamVR",
				"tools", "steamvr_room_setup", "win64", "steamvr_room_setup.exe"
			);

			if (!File.Exists(exePath))
			{
				MessageBox.Show($"Fant ikke Room Setup på:\n{exePath}",
								"Feil",
								MessageBoxButton.OK,
								MessageBoxImage.Error);
				return;
			}

			// 2) Launch it in its own process
			var psi = new ProcessStartInfo(exePath) { UseShellExecute = true };
			var proc = Process.Start(psi);
			if (proc == null)
			{
				MessageBox.Show("Kunne ikke starte Room Setup–prosessen.",
								"Feil",
								MessageBoxButton.OK,
								MessageBoxImage.Error);
				return;
			}

			// 3) Poll for its MainWindowHandle up to ~10s
			IntPtr handle = IntPtr.Zero;
			const int maxAttempts = 20;
			for (int i = 0; i < maxAttempts; i++)
			{
				proc.Refresh();
				handle = proc.MainWindowHandle;
				if (handle != IntPtr.Zero) break;
				await Task.Delay(500);
			}

			// 4) If we found it, bring it to front
			if (handle != IntPtr.Zero)
			{
				Win32.BringToFront(handle);
			}
			else
			{
				MessageBox.Show("Fikk ikke tak i Room Setup–vinduet for å sette det i front.",
								"Info",
								MessageBoxButton.OK,
								MessageBoxImage.Warning);
			}
		}

		private void ShowCategoryEditor(CategoryFilterItem filterItem)
		{
			var grp = filterItem.Group;
			var dlg = new GameCategoryDialog(_allGames, grp)
			{
				Owner = Application.Current.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			};
			if (dlg.ShowDialog() == true)
			{
				ApplyFilters();
			}
		}


		private void LoadDynamicGroups()
		{
			GameGroups.Clear();
			foreach (var grp in _groupHandler.LoadGroups(_allGames))
				GameGroups.Add(new GameGroupViewModel(grp));
		}

		public void SetVREmbedder(VREmbedder embedder)
		{
			_vrEmbedder = embedder;
		}
		/// <summary>
		/// Avbryter pågående kategori-redigering
		/// </summary>
		private void CancelEditCategory()
		{
			_editingCategoryItem = null;
			NewCategoryName = "";
			IsEditingCategory = false;
		}

		/// <summary>
		/// Fullfører enten ny kategori eller endring av eksisterende
		/// </summary>
		private void CommitEditCategory()
		{
			if (string.IsNullOrWhiteSpace(NewCategoryName))
				return;

			if (_editingCategoryItem != null)
			{
				// Endre eksisterende
				_editingCategoryItem.Group.GroupName = NewCategoryName;
			}
			else
			{
				// Legg til ny kategori
				var newGroup = new GameGroup { GroupName = NewCategoryName };
				var ci = new CategoryFilterItem(newGroup);
				ci.PropertyChanged += (_, e) =>
				{
					if (e.PropertyName == nameof(ci.IsChecked))
						ApplyFilters();
				};
				CategoryFilters.Add(ci);

				_groupHandler.SaveGroups(CategoryFilters.Select(cf => cf.Group));
				CancelEditCategory();

				ApplyFilters();
				LoadDynamicGroups();
			}

			// Lagre og oppdater filterliste
			_groupHandler.SaveGroups(CategoryFilters.Select(cf => cf.Group));
			CancelEditCategory();
			ApplyFilters();
		}

		private async Task InitializeAsync()
		{

			// last profil, valider, initialiser Steam‐API + spillliste…
			// … så:
			await _vrInitService.RestartSteamVRAsync();
			if (await Task.Run(() => _vrInitService.SafeInitOpenVR()))
				_calibrator.Initialize();    // gjør intern OpenVR.Init



	
			

			var spill = await _gameLoadService.LoadAllGamesAsync(_profile);
			_allGames = spill.ToList();

			var alleGenres = _allGames
	.SelectMany(g => g.Genres)
	.Distinct()
	.OrderBy(s => s);

			Debug.WriteLine("Unike Steam-sjangre: " + string.Join(", ", alleGenres));

			LoadGroupsAndFilters();
			LoadDynamicGroups();
			ApplyFilters();

			_vrAutomation.StartWatching();
		}

		private async Task LaunchGameAsync(Game game)
		{
			try
			{
				if (game == null) return;
				// start process
				var psi = !string.IsNullOrWhiteSpace(game.InstallPath) && File.Exists(game.InstallPath)
					? new ProcessStartInfo(game.InstallPath)
					: new ProcessStartInfo($"steam://rungameid/{game.AppID}") { UseShellExecute = true };
				var proc = Process.Start(psi);
				_gameStatusService.SetLaunchedGame(game, proc);
				await Task.Delay(500);
				_vrEmbedder.EnterFullScreen();
				IsSpectating = true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Feil under oppstart av spill: {ex.Message}");
			}
		}


		private void UpdateCurrentPlayer()
		{
			_gameStatusService.UpdateCurrentGameAndStatus();
			CurrentPlayer = _gameStatusService.CurrentPlayer;
		}

		/// <summary>
		/// Laster alle lagrede grupper og bygger opp CategoryFilters-collecton.
		/// Hver filter-hendelse kobles til ApplyFilters() så listen oppdateres dynamisk.
		/// </summary>
		private void LoadGroupsAndFilters()
		{
			CategoryFilters.Clear();
			foreach (var grp in _groupHandler.LoadGroups(_allGames))
			{
				var ci = new CategoryFilterItem(grp);
				ci.PropertyChanged += (_, e) =>
				{
					if (e.PropertyName == nameof(ci.IsChecked))
						ApplyFilters();
				};
				CategoryFilters.Add(ci);
			}
		}

		

		/// <summary>
		/// Sletter en kategori fra collection, lagrer alle grupper på nytt og oppdaterer filterlisten.
		/// </summary>
		private void DeleteCategory(CategoryFilterItem ci)
		{
			CategoryFilters.Remove(ci);
			_groupHandler.SaveGroups(CategoryFilters.Select(cf => cf.Group));
			ApplyFilters();
		}


		// … + kategori‐metoder (BeginEditCategory, CommitEditCategory osv.) …

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public class CategoryFilterItem : ObservableObject , INotifyPropertyChanged
	{
		public GameGroup Group { get; }
		public CategoryFilterItem(GameGroup g) => Group = g;

		private bool _isChecked;
		public bool IsChecked
		{
			get => _isChecked;
			set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}













/*
using HelseVestIKT_Dashboard.Helpers.Commands;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.Views;
using NAudio.Gui;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Valve.VR;

namespace HelseVestIKT_Dashboard.ViewModels
{
	public class MainWindowViewModel : INotifyPropertyChanged
	{
		private readonly GameLoadService _gameLoadService;
		private readonly FilterService _filterService;
		private readonly GameGroupHandler _groupHandler;
		private List<Game> _allGames = new List<Game>();

		public string CurrentTime { get; private set; }
		public string CurrentPlayer { get; private set; }
		public float EyeHeightSetting { get; set; }
		public VRStatusManager VREquipmentStatus { get; }
		
		// plus SearchText, IsEditingCategory, NewCategoryName, etc.



		public ObservableCollection<Game> Games { get; } = new ObservableCollection<Game>();
		public ObservableCollection<CategoryFilterItem> CategoryFilters { get; } = new ObservableCollection<CategoryFilterItem>();

		private bool _isEditingCategory;
		public bool IsEditingCategory
		{
			get => _isEditingCategory;
			set { _isEditingCategory = value; OnPropertyChanged(nameof(IsEditingCategory)); }
		}

		private string _newCategoryName = "";
		public string NewCategoryName
		{
			get => _newCategoryName;
			set { _newCategoryName = value; OnPropertyChanged(nameof(NewCategoryName)); }
		}

		private string _searchText = "Søk etter spill…";
		public string SearchText
		{
			get => _searchText;
			set
			{
				if (_searchText == value) return;
				_searchText = value;
				OnPropertyChanged(nameof(SearchText));
				ApplyFilters();
			}
		}


		private CategoryFilterItem _editingCategoryItem;


		public ICommand LaunchGameCommand { get; }
		public ICommand PauseCommand { get; }
		public ICommand RecenterSeatedCommand { get; }
		public ICommand RecenterStandingCommand { get; }
		public ICommand OpenSettingsCommand { get; }
		public ICommand ExitApplicationCommand { get; }
		public ICommand BeginAddCategoryCommand { get; }
		public ICommand CancelCategoryCommand { get; }
		public ICommand CommitCategoryCommand { get; }
		public ICommand BeginRenameCategoryCommand { get; }
		public ICommand DeleteCategoryCommand { get; }

		public MainWindowViewModel(GameLoadService gameLoadService, FilterService filterService, GameGroupHandler groupHandler)
		{
			_filterService = filterService;
			_groupHandler = groupHandler;
			_gameLoadService = gameLoadService ?? throw new ArgumentNullException(nameof(gameLoadService));

			// Commands
			BeginAddCategoryCommand = new RelayCommand(_ =>
			{
				_editingCategoryItem = null;
				NewCategoryName = string.Empty;
				IsEditingCategory = true;
			});

			BeginRenameCategoryCommand = new RelayCommand(param =>
			{
				if (param is CategoryFilterItem item)
				{
					_editingCategoryItem = item;
					NewCategoryName = item.Group.GroupName;
					IsEditingCategory = true;
				}
			});

			CancelCategoryCommand = new RelayCommand(_ =>
			{
				NewCategoryName = "";
				_editingCategoryItem = null;
				IsEditingCategory = false;
			});

			CommitCategoryCommand = new RelayCommand(_ =>
			{
				if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

				if (_editingCategoryItem != null)
				{
					// Rename existing
					_editingCategoryItem.Group.GroupName = NewCategoryName;
				}
				else
				{
					var g = new GameGroup { GroupName = NewCategoryName };
					var cf = new CategoryFilterItem(g);
					WireItem(cf);
					CategoryFilters.Add(cf);
				}

				// Persist & reset
				_groupHandler.SaveGroups(CategoryFilters.Select(cf => cf.Group)
			);
				NewCategoryName = "";
				_editingCategoryItem = null;
				IsEditingCategory = false;
			});

			DeleteCategoryCommand = new RelayCommand(param =>
			{
				if (param is CategoryFilterItem item)
				{
					CategoryFilters.Remove(item);
					_groupHandler.SaveGroups(CategoryFilters.Select(cf => cf.Group));
					ApplyFilters();
				}
			});


			_timerService.TickEverySecond((_, __) => CurrentTime = DateTime.Now.ToString("HH:mm"));
			_timerService.TickEveryTwoSeconds((_, __) => UpdateGameStatus());
			_timerService.Start();

		}


		/// <summary>
		/// Called once on Loaded: pulls in the SteamProfile, loads games,
		/// loads saved groups, wires up the IsChecked→ApplyFilters, then
		/// does an initial ApplyFilters so the UI is populated.
		/// </summary>
		public async Task InitializeAsync()
		{
			LoadOrCreateProfile();
			if (!ValidateProfile()) return;
			InitializeServices();
			await LoadAndDisplayGamesAsync();
			LoadGroupsAndFilters();
			StartTimers();            // drives CurrentTime + GameStatus
			HookAudioVolumeChanged(); // drives Volume slider text
			await InitializeVrAsync();
		}

		private void RefreshGamesView()
		{
			Games.Clear();
			foreach (var g in _allGames)
				Games.Add(g);
		}

		/// <summary>
		/// Call once after you load from Steam; also stores the master list.
		/// </summary> 
		public void SetGames(IEnumerable<Game> games)
		{
			_allGames.Clear();
			foreach (var g in games)
				_allGames.Add(g);
			ApplyFilters();

		}

		// Call this right after you LoadGroupsAndFilters()
		private void WireCategoryEvents()
		{
			foreach (var cf in CategoryFilters)
				cf.PropertyChanged += (_, e) =>
				{
					if (e.PropertyName == nameof(CategoryFilterItem.IsChecked))
						ApplyFilters();
				};
		}

		/// <summary>
		/// Initializes user‐defined groups from storage and populates CategoryFilters
		/// </summary>
		public void LoadGroupsAndFilters()
		{
			CategoryFilters.Clear();
			var groups = _groupHandler.LoadGroups(_allGames);
			foreach (var g in groups)
			{
				var cf = new CategoryFilterItem(g);
				WireItem(cf);
				CategoryFilters.Add(cf);
			}
		}

		private void WireItem(CategoryFilterItem cf)
		{
			cf.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(CategoryFilterItem.IsChecked))
					ApplyFilters();
			};
		}



		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string name) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		/// <summary>
		/// Applies search, then genre/type/group filtering, and updates the UI collection.
		/// </summary>
		public void ApplyFilters()
		{
			// 1) search‐text
			var first = _allGames
				.Where(g => string.IsNullOrWhiteSpace(SearchText)
						 || g.Title.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
				.ToList();

			// 2) gather genre+type (if you bind those into lists, fill here)
			var selectedGenres = new List<string>();
			var selectedTypes = new List<string>();
			var selectedGroups = CategoryFilters.Where(cf => cf.IsChecked)
												.Select(cf => cf.Group);

			// 3) apply service
			var filtered = _filterService.ApplyFilters(
				selectedGenres,
				selectedTypes,
				selectedGroups,
				first);

			// 4) push to UI
			Games.Clear();
			foreach (var g in filtered)
				Games.Add(g);
		}

		public class CategoryFilterItem : INotifyPropertyChanged
		{
			public GameGroup Group { get; }
			private bool _isChecked;
			public bool IsChecked
			{
				get => _isChecked;
				set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); }
			}

			public CategoryFilterItem(GameGroup g) => Group = g;

			public event PropertyChangedEventHandler? PropertyChanged;
			protected void OnPropertyChanged(string n) =>
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
		}


		//Importerer fra xaml.cs: 

	


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

			
			#endregion

			protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
			{
				// 1) Løs overlay-vinduet mens applikasjonen fortsatt er levende
				try
				{
					_embedder?.DetachOverlay();
					Thread.Sleep(100); // Gi OS tid til å prosessere før vi lukker
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Feil under DetachOverlay i OnClosing: {ex}");
				}

				// 2) Gi OS tid til å prosessere Win32-kallene
				base.OnClosing(e);
			}

			protected override void OnClosed(EventArgs e)
			{
				// 1) Rydd opp tjenester
				try { _audioService?.Dispose(); } catch { }
				try { _wifiStatusManager?.StopMonitoringWifiSignal(); } catch { }
				try { _statusService?.Shutdown(); } catch { }
				try { _timerService?.Dispose(); } catch { }
				try { _initService?.Shutdown(); } catch { }
				try { _calibrator?.Dispose(); } catch { }

				// OpenVR-shutdown håndteres i VRCalibrator.Dispose(), så vi fjerner denne:
				// try { OpenVR.Shutdown(); } catch { }

				// 5) Gjenopprett eventuelt global Hotkey-block eller annet
				Win32.DisableKeyBlock();
				base.OnClosed(e);
			}




			private void UpdateGameStatus()
			{
				if (_gameStatusManager == null)
					return;   // gjør ingenting om manager ikke er klar ennå

				_gameStatusManager.UpdateCurrentGameAndStatus();
				CurrentPlayer = _gameStatusManager.CurrentPlayer;
			}

			private void MainWindow_Activated(object sender, EventArgs e)
			{
				if (_isLocked)
				{
					Topmost = true;
					Activate();
				}
			}

			protected override void OnActivated(EventArgs e)
			{
				base.OnActivated(e);

				// Sett vinduet alltid øverst
				Win32.SetWindowTopMost(new System.Windows.Interop.WindowInteropHelper(this).Handle);

				// Start blokkering av Alt+Tab, Win-taster og Ctrl+Esc
				Win32.EnableKeyBlock();
			}

			public bool isLocked => _isLocked;
			public void LockApplication()
			{
				_isLocked = true;
				Topmost = true;
				Win32.EnableKeyBlock();
				ExitButton.Visibility = Visibility.Collapsed;
			}

			public void UnlockApplication()
			{
				_isLocked = false;
				Topmost = false;
				Win32.DisableKeyBlock();
				//Vis Exitknappen når man låser opp applikasjonen
				ExitButton.Visibility = Visibility.Visible;

			}

			public void ToggleLock()
			{
				if (_isLocked) UnlockApplication();
				else LockApplication();
			}

			/*
			private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
			{

				await _vm.InitializeAsync(_currentProfile);
				await InitializeApplicationAsync();
				_steamVrAutomation.Start();

			} 


private async Task InitializeApplicationAsync()
			{
				if (_gamesLoaded) return;
				_gamesLoaded = true;

				// — PROFILE & SERVICES (still in the View) —
				LoadOrCreateProfile();
				if (!ValidateProfile()) return;
				InitializeServices();    // configures _gameLoadService

				// — DELEGATE to the VM to load games & categories & do the first filter —
				await _vm.InitializeAsync(_currentProfile);

				// — Finally do your VR / timer setup —
				await InitializeVrAsync();
			}


			private void LoadOrCreateProfile()
			{
				// Hent all data (profiler + sist brukte) fra profiles.json
				var data = ProfileStore.Load();
				var profiles = data.Profiles;
				var lastName = data.LastProfileName;

				// Velg profil: enten sist brukte, eller første i listen, eller en helt ny "Default"
				_currentProfile = profiles
					.FirstOrDefault(p => p.Name == lastName)
					?? profiles.FirstOrDefault()
					?? new SteamProfile { Name = "Default", ApiKey = "", UserId = "" };

				// Oppdater "sist brukte" i strukturen
				data.LastProfileName = _currentProfile.Name;

				// Lagre alt tilbake til profiles.json
				ProfileStore.Save(data);
			}


			private bool ValidateProfile()
			{
				if (string.IsNullOrWhiteSpace(_currentProfile.ApiKey) ||
					string.IsNullOrWhiteSpace(_currentProfile.UserId))
				{
					MessageBox.Show($"Profil «{_currentProfile.Name}» mangler ApiKey/UserId.…",
									"Ugyldig profil", MessageBoxButton.OK, MessageBoxImage.Warning);
					return false;
				}
				return true;
			}

			private void InitializeServices()
			{
				Console.WriteLine(_currentProfile.ApiKey);
				_steamApi = new SteamApi(_currentProfile.ApiKey, _currentProfile.UserId);
				_gameDetailsFetcher = new GameDetailsFetcher(_currentProfile.ApiKey, _currentProfile.UserId);
				var offlineMgr = new OfflineSteamGamesManager();
				_gameLoadService = new GameLoadService(_steamApi, _gameDetailsFetcher, offlineMgr);
			}

			private async Task LoadAndDisplayGamesAsync()
			{
				var allGames = await _gameLoadService.LoadAllGamesAsync(_currentProfile);
				AllGames.Clear();
				Games.Clear();
				foreach (var g in allGames)
				{
					AllGames.Add(g);
					Games.Add(g);
					if (!string.IsNullOrWhiteSpace(g.InstallPath))
						g.ProcessName = Path.GetFileNameWithoutExtension(g.InstallPath);
				}
			}

			private void InitializeGroupsAndFilters()
			{
				StartGameStatusTimer();
				UpdateFilters(null, null);
			}

			private async Task InitializeVrAsync()
			{
				try
				{
					// 1) Restart SteamVR én gang
					await _initService.RestartSteamVRAsync();
					bool vrOk = await Task.Run(() => _initService.SafeInitOpenVR());
					if (!vrOk)
					{
						CollapseVrControls();
						return;
					}

					// 2) Initialiser VRCalibrator
					_calibrator?.Dispose();
					_calibrator = new VRCalibrator();                 // parameter­løs ctor
					_calibrator.Initialize();       // injiser systemet

					// 3) Les og vis base-høyde
					_baseHeight = _calibrator.LoadCurrentHeightCalibration();
					EyeHeightSetting = _baseHeight;
					HeightSlider.Value = 0;

					// 4) Start status-oppdateringer osv.
					_statusService = new VRStatusService(VREquipmentStatus);
					_statusService.StartStatusUpdates(TimeSpan.FromSeconds(7));

					// 5) Vis VR-UI
					VRHost.Visibility = Visibility.Visible;
					PauseKnapp.IsEnabled = true;
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Kunne ikke starte VR-tjenester: {ex}");
					CollapseVrControls();
				}
			}



			private void CollapseVrControls()
			{
				VRHost.Visibility = Visibility.Collapsed;
				PauseKnapp.IsEnabled = true;
			}

			public async Task SetProfileAsync(SteamProfile p)
			{
				_currentProfile = p;

				// 1) Re‑init SteamApi / GameLoadService for ny profil
				_steamApi = new SteamApi(p.ApiKey, p.UserId);
				_gameDetailsFetcher = new GameDetailsFetcher(p.ApiKey, p.UserId);
				var offlineMgr = new OfflineSteamGamesManager();
				_gameLoadService = new GameLoadService(_steamApi, _gameDetailsFetcher, offlineMgr);

				// 2) Hent spill
				var allGames = await _gameLoadService.LoadAllGamesAsync(p);

				// 3) Fyll AllGames og Games
				AllGames.Clear();
				Games.Clear();
				foreach (var g in allGames)
				{
					AllGames.Add(g);
					Games.Add(g);
				}
				// 4) Re‑compute ProcessName
				foreach (var g in AllGames)
					if (!string.IsNullOrWhiteSpace(g.InstallPath))
						g.ProcessName = Path.GetFileNameWithoutExtension(g.InstallPath);

				// 6) **Re‑apply filter** på de nye spillene
				UpdateFilters(null, null);
			}

			private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
			{
				// 2) Faktisk bruk av høyden i VR
				_calibrator.ApplyHeight(EyeHeightSetting);
				// 1) Oppdater databinding og settings
				var offset = (float)e.NewValue;
				EyeHeightSetting = _baseHeight + offset;
				Properties.Settings.Default.EyeHeight = EyeHeightSetting;
				Properties.Settings.Default.Save();

			}


			private void StartGameStatusTimer()
			{
				var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
				_timerService.TickEveryTwoSeconds((s, e) => UpdateGameStatus());  // UpdateGameStatus setter både CurrentPlayer og CurrentStatus
				timer.Start();
			}


			public event PropertyChangedEventHandler? PropertyChanged;
			protected void OnPropertyChanged(string propertyName)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}

			#region FilterEvent 

			private void UpdateFilters(object sender, RoutedEventArgs e)
			{
				if (DataContext is MainWindowViewModel vm)
					vm.ApplyFilters();
			}

			#endregion

			#region GameGroups
			// inside MainWindow class

			private MainWindowViewModel Vm => DataContext as MainWindowViewModel;

			private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
			{
				if (Vm.SearchText == "Søk etter spill…")
					Vm.SearchText = "";
			}

			private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
			{
				if (string.IsNullOrWhiteSpace(Vm.SearchText))
					Vm.SearchText = "Søk etter spill…";
			}


			private void NewCategoryTextBox_LostFocus(object sender, RoutedEventArgs e)
			{
				// whatever logic you had to commit or cancel the new/rename
				// e.g. call CommitNewOrRename() or EndNewOrRename(), etc.
				// Or simply collapse the textbox if you've bound everything into view-model
			}


			#endregion



			#region Toolbar og Volum kontroller


			// Når systemvolum endrer seg utenfra:
			private void OnServiceVolumeChanged(object? sender, float newScalar)
			{
				// Oppdater UI på UI‐tråd:
				Dispatcher.Invoke(() =>
				{
					_isUpdatingVolumeSlider = true;
					VolumeSlider.ValueChanged -= VolumeSlider_ValueChanged; // Unsubscribe to avoid recursion
					double newValue = newScalar * 100;
					if (Math.Abs(VolumeSlider.Value - newValue) > 0.1)
						VolumeSlider.Value = newValue;

					VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;

					VolumeStatusTextBlock.Text = $"{(int)newValue}%";
					VolumeStatusTextBlock.Visibility = Visibility.Visible;


					// (Re)start timer
					volumeStatusTimer?.Stop();
					volumeStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
					volumeStatusTimer.Tick += (s, e) =>
					{
						VolumeStatusTextBlock.Visibility = Visibility.Collapsed;
						volumeStatusTimer.Stop();
					};
					volumeStatusTimer.Start();

					_isUpdatingVolumeSlider = false;
				});
			}

			private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
			{
				if (_isUpdatingVolumeSlider)
					return;

				float scalar = (float)(e.NewValue / 100.0);
				_audioService.CurrentVolume = scalar;
			}

			private void SyncVolumeSliderWithSystem()
			{
				float current = _audioService.CurrentVolume;
				VolumeSlider.Value = current * 100;
				VolumeStatusTextBlock.Text = $"{(int)(current * 100)}%";
				VolumeStatusTextBlock.Visibility = Visibility.Visible;
			}


			private void ExitButton_Click(object sender, RoutedEventArgs e)
			{
				if (MessageBox.Show(
					"Er du sikker på at du vil avslutte programmet?",
					"Avslutt programmet",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
					this.Close();
				}
			}




			#endregion

			#region VR Statuslinje og knappersk

			private async Task<bool> WaitForExitAsync(Process proc, int timeoutMs)
			{
				var tcs = new TaskCompletionSource<bool>();
				proc.EnableRaisingEvents = true;
				proc.Exited += (s, e) => tcs.TrySetResult(true);
				if (proc.HasExited) return true;

				using (var cts = new CancellationTokenSource(timeoutMs))
					return await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token)) == tcs.Task;
			}


			// Åpne innstillinger med trykk på innstillingsknappen
			private async void OpenSettings_Click(object sender, RoutedEventArgs e)
			{
				// 1) Spør om PIN
				var pinDlg = new PinWindow
				{
					Owner = this,
					WindowStartupLocation = WindowStartupLocation.CenterOwner
				};
				if (pinDlg.ShowDialog() != true)
					return;

				// 2) Åpne SettingsWindow
				var settingsDlg = new SettingsWindow
				{
					Owner = this,
					WindowStartupLocation = WindowStartupLocation.CenterOwner
				};
				settingsDlg.ShowDialog();

				// 3) Etter at SettingsWindow er lukket: les profildata fra JSON
				var data = ProfileStore.Load();
				var lastName = data.LastProfileName;
				var profile = data.Profiles.FirstOrDefault(p => p.Name == lastName);
				if (profile == null)
					return;

				// 4) Dersom det faktisk er en ny profil (eller andre endringer), oppdater
				if (_currentProfile == null
					|| profile.Name != _currentProfile.Name
					|| profile.ApiKey != _currentProfile.ApiKey
					|| profile.UserId != _currentProfile.UserId)
				{
					_currentProfile = profile;
					_steamApi = new SteamApi(profile.ApiKey, profile.UserId);
					_gameDetailsFetcher = new GameDetailsFetcher(profile.ApiKey, profile.UserId);
					_gameLoadService = new GameLoadService(_steamApi, _gameDetailsFetcher, new OfflineSteamGamesManager());

					// 5) Re-hent alle spill og oppdater UI via din eksisterende metode
					await _vm.InitializeAsync(_currentProfile);

					ExitButton.Visibility = Visibility.Visible;
				}
			}



			/// <summary>
			/// “Nodstopp”-knappen fungerer som en “Reconnect VR”:
			/// Den restart­er SteamVR, prøver å initialisere OpenVR igjen,
			/// og slår på VR-relaterte UI-elementer hvis det lykkes.
			/// </summary>
			 private async void Nodstopp_Click(object sender, RoutedEventArgs e)
			  {

				  // 1) Forsøk å restarte SteamVR-prosessene
				  _statusService.StopStatusUpdates();

				  try
				  {
					  await _initService.RestartSteamVRAsync();
				  }
				  catch (Exception ex)
				  {
					  MessageBox.Show(
						  $"Kunne ikke restarte SteamVR: {ex.Message}",
						  "VR-feil",
						  MessageBoxButton.OK,
						  MessageBoxImage.Warning);
					  return;
				  }

				  // 2) Prøv å initialisere OpenVR på nytt
				  bool vrOk = _initService.InitializeOpenVR();
				  if (!vrOk)
				  {
					  MessageBox.Show(
						  "Kunne ikke koble til VR-headset.\n" +
						  "Sørg for at SteamVR kjører og at headset er tilkoblet.",
						  "VR-tilkobling mislyktes",
						  MessageBoxButton.OK,
						  MessageBoxImage.Warning);
					  return;
				  }

				  // 3) Aktiver VR-delen i UI
				  //    Vis VR-host-vindu, aktiver pause- og kalibreringsknapp
				  VRHost.Visibility = Visibility.Visible;
				  PauseKnapp.IsEnabled = true;
				  KalibrerKnapp.IsEnabled = true;

				  // 4) Start status-oppdateringene på nytt
				  _statusService?.StartStatusUpdates(TimeSpan.FromSeconds(7));

				  // 5) Logg at VR nå er tilgjengelig
				  Debug.WriteLine("VR er gjenopprettet og klar til bruk.");
			  }

			private async void Nodstopp_Click(object sender, RoutedEventArgs e)
			{
				VRHost.Visibility = Visibility.Collapsed;
				GameLibraryArea.Visibility = Visibility.Visible;
				SideMenu.Visibility = Visibility.Visible;
				ReturnButton.Visibility = Visibility.Collapsed;

				// 1) Stopp status-oppdateringer og løs embeddet overlay
				try
				{
					_statusService?.StopStatusUpdates();
					_embedder?.DetachOverlay();
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Feil i Nodstopp forberedelse: {ex}");
				}

				// 2) Drepe SteamVR-prosesser og eventuelt Steam-klient
				void KillProcess(string name)
				{
					foreach (var p in Process.GetProcessesByName(name))
					{
						try { p.Kill(); }
						catch {  }
					}
				}
				KillProcess("vrserver");
				KillProcess("vrmonitor");
				// Hvis du vil tvinge Steam også:
				// KillProcess("steam");

				// 3) Vent til prosessene er virkelig borte
				await Task.Delay(500);

				// 4) (Re)start Steam + SteamVR
				try
				{
					await _initService.RestartSteamVRAsync();
				}
				catch (Exception ex)
				{
					MessageBox.Show(
						$"Kunne ikke restarte SteamVR: {ex.Message}",
						"VR-feil",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				// 5) Vent til vrserver dukker opp (maks 10s)
				var sw = Stopwatch.StartNew();
				while (sw.Elapsed < TimeSpan.FromSeconds(10))
				{
					if (Process.GetProcessesByName("vrserver").Any())
						break;
					await Task.Delay(200);
				}

				// ————— 6) Rydd opp gammel VRCalibrator (inkluderer OpenVR.Shutdown) —————
				try
				{
					_calibrator?.Dispose();
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Feil ved disposing av calibrator: {ex}");
				}

				// ————— 7) Initialiser ny VRCalibrator i Scene-modus —————
				_calibrator = new VRCalibrator();
				try
				{
					_calibrator.Initialize();   // Kaller internt OpenVR.Init(..., VRApplication_Scene)
				}
				catch (Exception ex)
				{
					MessageBox.Show(
						$"Kunne ikke initialisere VR-systemet: {ex.Message}",
						"VR-tilkobling mislyktes",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				// ————— 8) Gjenopprett VR-UI og start status-oppdateringer —————
				VRHost.Visibility = Visibility.Visible;
				PauseKnapp.IsEnabled = true;
				_statusService?.StartStatusUpdates(TimeSpan.FromSeconds(7));

				Debug.WriteLine("VR er gjenopprettet og klar til bruk.");
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
			}

			#endregion

			#region Spillbibliotek og Logg

			private void GameLibraryScrollViewer_PreviewTouchDown(object sender, TouchEventArgs e)
			{
				// Start touch-scroll
				_isTouchScrolling = true;
				_touchStartPoint = e.GetTouchPoint(GameLibraryScrollViewer).Position;
				_initialScrollOffset = GameLibraryScrollViewer.VerticalOffset;
				GameLibraryScrollViewer.CaptureTouch(e.TouchDevice);
				e.Handled = true;
			}

			private void GameLibraryScrollViewer_PreviewTouchMove(object sender, TouchEventArgs e)
			{
				if (!_isTouchScrolling) return;

				var currentPoint = e.GetTouchPoint(GameLibraryScrollViewer).Position;
				double delta = _touchStartPoint.Y - currentPoint.Y;
				GameLibraryScrollViewer.ScrollToVerticalOffset(_initialScrollOffset + delta);

				e.Handled = true;
			}

			private void GameLibraryScrollViewer_PreviewTouchUp(object sender, TouchEventArgs e)
			{
				// Avslutt touch-scroll
				_isTouchScrolling = false;
				GameLibraryScrollViewer.ReleaseTouchCapture(e.TouchDevice);
				e.Handled = true;
			}



			// Egen log knapp for å sjekke diverse feil i programmet.
			private void LogButton_Click(object sender, RoutedEventArgs e)
			{
				// Create and display the log window modally
				LogWindow logWindow = new LogWindow();
				logWindow.ShowDialog();
			}

			// MainWindow.xaml.cs

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

				// —————— NYTT: Fortell manager at dette er spillet vi nettopp startet ——————
				if (proc != null)
				{
					_gameStatusManager.SetLaunchedGame(game, proc);
				}

				// 2) Vent til spillet er klart til input (inntil 5 sek)
				await Task.Run(() =>
				{
					try
					{
						if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
						{
							proc.WaitForInputIdle(5000);
						}
					}
					catch (InvalidOperationException)
					{
						Console.WriteLine("Vent-for-input feilet: prosessen har ikke GUI.");
					}
				});

				// 3) Flytt spillvinduet bak applikasjonen
				if (proc != null)
				{
					IntPtr hGame = proc.MainWindowHandle;
					if (hGame != IntPtr.Zero)
					{
						Win32.SetWindowPos(hGame, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE);
					}
				}

				// 4) Skyv også SteamVR-kontrollpanelet bak om det er åpent
				IntPtr hSteamVR = Win32.FindWindow(null, "SteamVR");
				if (hSteamVR != IntPtr.Zero)
				{
					Win32.SetWindowPos(hSteamVR, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE);
				}

				// 5) Oppdater status i statusbar (vil nå prioritere det eksplisitte lanserte spillet)
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
				await Task.Delay(500);

				// 7) Bytt til fullskjerm/VR-view
				FullScreenButton_Click(null, null);
				_embedder.StartVREmbedRetry();

				// 8) Oppdater resten av UI
				HeaderGrid.Visibility = Visibility.Visible;
				StatusBar.Visibility = Visibility.Visible;
				GameLibraryScrollViewer.Visibility = Visibility.Collapsed;
				VRHost.Visibility = Visibility.Visible;
				ReturnButton.Visibility = Visibility.Visible;
			}

			#endregion

			#region VR-Kalibrering og Funksjoner


			private void KalibreringKnapp_Click(object sender, RoutedEventArgs e)
			{
				bool vis = StatusBarKalibrering.Visibility != Visibility.Visible;

				// a) Vis/skjul kalibreringspanelet
				StatusBarKalibrering.Visibility = vis
					? Visibility.Visible
					: Visibility.Collapsed;

				// b) Slå av eller på hit-testing på VRHost
				//    når kalibrering er synlig vil vi IKKE at VR-vinduet snapper museklikk
				VRHost.IsHitTestVisible = !vis;

				// c) Juster Z-indeksen om nødvendig
				System.Windows.Controls.Panel.SetZIndex(VRHost, 100);
				System.Windows.Controls.Panel.SetZIndex(StatusBarKalibrering, 200);
			}


			

			private void MidtstillView_Sittende_Click(object s, RoutedEventArgs e)
				=> SafeRecenter(ETrackingUniverseOrigin.TrackingUniverseSeated);


			private void MidstillView_Staaende_Click(object sender, RoutedEventArgs e)
			{
				SafeRecenter(ETrackingUniverseOrigin.TrackingUniverseStanding);
			}

			private void SafeRecenter(ETrackingUniverseOrigin origin)
			{
				try
				{
					_calibrator.Recenter(origin);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Kunne ikke midtstille: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
			

			private async void MidtstillView_Sittende_Click(object sender, RoutedEventArgs e)
			{
				if (_calibrator != null)
					await _calibrator.RecenterAsync(ETrackingUniverseOrigin.TrackingUniverseSeated);
			}

			private async void MidstillView_Staaende_Click(object sender, RoutedEventArgs e)
			{
				if (_calibrator != null)
					await _calibrator.RecenterAsync(ETrackingUniverseOrigin.TrackingUniverseStanding);
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

			private void VRHost_SizeChanged(object sender, SizeChangedEventArgs e)
			{
				_embedder.ResizeHost(sender, e);
			}
			#endregion


			private void SearchTimer_Tick(object sender, EventArgs e)
			{
				_searchService.ApplySearch();
			}

			/// <summary>
			/// 2) Starter bakgrunnskonsoll for debug‐utskrifter.
			/// </summary>
			private void AllocateDebugConsole()
			{
				Win32.AllocConsole();
			}

			private void FullScreenButton_Click(object sender, RoutedEventArgs e) => _embedder.EnterFullScreen();

			/// <summary>
			/// Pauser den aktive VR-spillsesjonen ved å åpne SteamVR dashboard-overlay.
			/// </summary>
			/// <param name="sender">Knappen som ble klikket.</param>
			/// <param name="e">Event-args for klikket.</param>
			private void PauseKnapp_Click(object sender, RoutedEventArgs e) => _dashSvc.PauseKnapp_Click(sender, e);
			private void AvsluttKnapp_Click(object sender, RoutedEventArgs e) => _dashSvc.CloseCurrentGame();

			// private void Recenter(ETrackingUniverseOrigin origin) => _calibrator.Recenter(origin);


			private void HoydeKalibrering_Click(object sender, RoutedEventArgs e)
			{

				_initService.EnsureVrSystemAlive();

				Hoydekalibrering_Slider.Visibility =
					Hoydekalibrering_Slider.Visibility == Visibility.Visible
					? Visibility.Collapsed
					: Visibility.Visible;

				Process.Start(new ProcessStartInfo
				{
					FileName = "explorer.exe",
					Arguments = "steam://run/250820//standalone",
					UseShellExecute = true
				});
			}

			private async void RomKalibrering_Click(object sender, RoutedEventArgs e)
			{
				var main = System.Windows.Application.Current.MainWindow as MainWindow;

				// 2) Finn exe‑stien
				string? exePath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
					"Steam", "steamapps", "common", "SteamVR",
					"tools", "steamvr_room_setup", "win64", "steamvr_room_setup.exe"
				);
				if (!File.Exists(exePath))
				{
					MessageBox.Show($"Fant ikke Room Setup på:\n{exePath}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
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
					Win32.BringToFront(handle);
				}
				else
				{
					MessageBox.Show("Fikk ikke tak i Room Setup–vinduet for å sette det i front.", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
			}

			/// <summary>
			/// 4) Initialiserer OpenVR, henter base‐høyde og setter slider‐start.
			/// </summary>
			private void InitializeVrAndCalibration()
			{
				// a) Sørg for at vi har et gyldig CVRSystem fra VRInitService
				var system = _initService.System;
				if (system == null) return;
				_calibrator?.Dispose();
				_calibrator = new VRCalibrator();
				_calibrator.Initialize();
				_baseHeight = _calibrator.LoadCurrentHeightCalibration();

				// c) Last inn tidligere høyde‐kalibrering
				try
				{
					_baseHeight = _calibrator.LoadCurrentHeightCalibration();
					EyeHeightSetting = _baseHeight;
					HeightSlider.Value = 0;
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Feil ved lasting av base-høyde: {ex}");
				}
			}


			public void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
			{
				_inputService.HandleKeyDown(sender, e);
			}
		}
	}


*/
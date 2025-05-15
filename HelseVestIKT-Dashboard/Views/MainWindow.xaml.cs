using Dialogs.Views;
using HelseVestIKT_Dashboard.Helpers;
using HelseVestIKT_Dashboard.ViewModels;
using HelseVestIKT_Dashboard.Views;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Services;
using Microsoft.Win32;
using SteamKit2.GC.Dota.Internal;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Valve.VR;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using System.Windows.Media;

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

        private string _currentPlayer;
		public string CurrentPlayer { get; set; }

		private string _currentStatus;
		public string CurrentStatus { get; set; }

		private double _eyeHeightSetting;
		public double EyeHeightSetting { get; set; }

		public ObservableCollection<Game> Games { get; } = new ObservableCollection<Game>();
		private ObservableCollection<Game> AllGames { get; } = new ObservableCollection<Game>();

		public VRStatusManager VREquipmentStatus { get; } = new VRStatusManager();

		// tjenester og manager‐felt som du faktisk bruker:
		// feltdeklarasjoner i MainWindow:

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
		private readonly GameGroupHandler _gameGroupHandler;
		private readonly GameStatusManager _gameStatusManager;
		private readonly GameProcessService _processService;
		private GameLoadService _gameLoadService;
		private VRStatusService _statusService;
		private readonly VRDashboardService _dashSvc;
		private readonly VRCalibrator _calibrator;
		private readonly VREmbedder _embedder;
		private readonly AudioService _audioService;
		private readonly WifiStatusManager _wifiStatusManager;
		private readonly FilterService _filterService;
		private readonly InputService _inputService;

		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;

			// — 0) Opprett alt som aldri skal være null —
			_gameGroupHandler = new GameGroupHandler();
			_searchService = new SearchService(SearchBox, Games, AllGames);
			_timerService = new TimerService();
			_initService = new VRInitService();
			_gameGroupHandler = new GameGroupHandler();
			_gameStatusManager = new GameStatusManager(AllGames);
			_processService = new GameProcessService(_gameStatusManager);
			_filterService = new FilterService();
			_inputService = new InputService();

			// — 1) TimerService for klokke, status og VR-helse —
			_timerService.TickEverySecond((s, e) => CurrentTime = DateTime.Now.ToString("HH:mm"));
			_timerService.TickEveryTwoSeconds((s, e) => UpdateGameStatus());
			// _timerService.TickEveryFiveMinutes((s, e) => _initService.EnsureVrSystemAlive());
			_timerService.Start();

			// — 3) VR-tjenester & embedder —
			_dashSvc = new VRDashboardService(_processService, _gameStatusManager, _initService);
			_calibrator = new VRCalibrator();
			_embedder = new VREmbedder(VRHost, MainContentGrid, GameLibraryArea, ReturnButton);

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

		#endregion

		protected override void OnClosed(EventArgs e)
		{
			try { _audioService?.Dispose(); } catch { }
			try { _wifiStatusManager?.StopMonitoringWifiSignal(); } catch { }
			try { _statusService?.Shutdown(); } catch { }
			try { _timerService?.Dispose(); } catch { }
			try { _initService?.Shutdown(); } catch { }

			// 6) Løs overlay‐vinduet
			try
			{
				_embedder?.DetachOverlay();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Feil under DetachOverlay: {ex}");
			}

			// 7) Sørg for OpenVR-stenging
			try { OpenVR.Shutdown(); } catch { }
			Win32.DisableKeyBlock();

			base.OnClosed(e);
		}




		private void UpdateGameStatus()
		{
			if (_gameStatusManager == null)
				return;   // gjør ingenting om manager ikke er klar ennå

			_gameStatusManager.UpdateCurrentGameAndStatus();
			CurrentPlayer = _gameStatusManager.CurrentPlayer;
			CurrentStatus = _gameStatusManager.CurrentStatus;
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

		public void LockApplication()
        {
            _isLocked = true;
            Topmost = true;
        }

        public void UnlockApplication()
        {
            _isLocked = false;
            Topmost = false;
        }

        public void ToggleLock()
        {
            if (_isLocked) UnlockApplication();
            else LockApplication();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeApplicationAsync();
        }

        private async Task InitializeApplicationAsync()
        {
            if (_gamesLoaded) return;
            _gamesLoaded = true;

            Console.WriteLine("—> Startup: Velger/gjenoppretter profil…");
            LoadOrCreateProfile();

            Console.WriteLine($"—> Startup: Profil funnet: {_currentProfile.Name}, ApiKey={(string.IsNullOrEmpty(_currentProfile.ApiKey) ? "(tom)" : "(har verdi)")}");
            if (!ValidateProfile()) return;

            Console.WriteLine("—> Startup: Profil validert, går videre med init…");
            InitializeServices();
            await LoadAndDisplayGamesAsync();
            InitializeGroupsAndFilters();
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
            LoadGameGroups();
            UpdateFilters(null, null);
        }

        private async Task InitializeVrAsync()
        {
            try
            {
                await _initService.RestartSteamVRAsync();
                bool vrOk = await Task.Run(() => _initService.SafeInitOpenVR());

                if (vrOk)
                {
                    InitializeVrAndCalibration();
                    if (_initService.System != null)
                    {
                        _statusService = new VRStatusService(VREquipmentStatus);
                        _statusService.StartStatusUpdates(TimeSpan.FromSeconds(7));
                    }
                    else
                    {
                        CollapseVrControls();
                    }
                }
                else
                {
                    CollapseVrControls();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Kunne ikke starte VR-tjenester: {ex.Message}");
                CollapseVrControls();
            }
        }

        private void CollapseVrControls()
        {
            VRHost.Visibility = Visibility.Collapsed;
            PauseKnapp.IsEnabled = true;
            KalibrerKnapp.IsEnabled = true;
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

			// 5) **Tøm gamle gruppe‑checkboxer** og gjenskap dem
			GameCategoriesPanel.Children.Clear();
			LoadGameGroups();

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
			_timerService.TickEveryTwoSeconds((s,e) => UpdateGameStatus());  // UpdateGameStatus setter både CurrentPlayer og CurrentStatus
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
			var selectedGenres = new[]
			{
		CheckBoxAction,
		CheckBoxEventyr,
		CheckBoxIndie,
		CheckBoxLettbeint,
		CheckBoxMassivtFlerspill,
		CheckBoxRacing,
		CheckBoxRollespill,
		CheckBoxSimulering,
		CheckBoxSport,
		CheckBoxStrategi
		}
			 .Where(cb => cb.IsChecked == true)
	.Select(cb => cb.Content.ToString() ?? "");


			var selectedTypes = new[]
			{
		CheckBoxKunFavoritter,
		CheckBoxNyligSpilt,
		CheckBoxVRSpill,
		CheckBoxSteamSpill,
		CheckBoxAndreSpill,
		CheckFlerspiller
	}
			.Where(cb => cb.IsChecked == true)
	.Select(cb => cb.Content.ToString() ?? "");

			// 3) Hent valgte GameGroup-instansene
			var selectedGroups = _gameGroupHandler
				.GetGameGroups()                  // IEnumerable<(CheckBox, GameGroup)>
				.Where(pair => pair.Item1.IsChecked == true)
				.Select(pair => pair.Item2);

			// 4) Kall FilterService med korrekte typer
			var filtered = _filterService.ApplyFilters(
				selectedGenres,
				selectedTypes,
				selectedGroups,
				AllGames
			);

			Games.Clear();
			foreach (var g in filtered)
				Games.Add(g);

			if (sender is CheckBox cb)
				Console.WriteLine($"Filters {(cb.IsChecked == true ? "applied" : "unapplied")}: {cb.Content}");
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
					var pair = _gameGroupHandler.GetGameGroups()
											  .FirstOrDefault(g => g.Item2 == gameGroupToRename);
					if (pair.Item1 != null)
						pair.Item1.Content = text;
					_gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");

					isRenaming = false;
				}
				else
				{
					// lag ny gruppe
					var newGroup = new GameGroup { GroupName = text };
					var cb = new CheckBox { Content = text, Style = (Style)FindResource("RoundedCheckBoxWithSourceSansFontStyle") };
					cb.Click += UpdateFilters;
					AddGameGroupCheckBox(cb, newGroup);
					_gameGroupHandler.AddGameGroup(cb, newGroup);
					_gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");

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

		// Når boksen får fokus: fjern placeholder‑tekst
		private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
		{
			// Hvis teksten er akkurat placeholderen, tøm
			if (SearchBox.Text == "Søk etter spill...")
			{
				SearchBox.Text = "";
				// Sett normalt tekst‐farge
				SearchBox.Foreground = new SolidColorBrush(Colors.Black);
			}
		}
		private void NewCategoryTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isRenaming)
            {
                if (!string.IsNullOrWhiteSpace(NewCategoryTextBox.Text))
                {
                    // Update the group name
                    gameGroupToRename.GroupName = NewCategoryTextBox.Text;
                    _gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");

                    // Directly update the checkbox text by using the game group reference
                    var checkBoxToUpdate = _gameGroupHandler.GetGameGroups()
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

                    _gameGroupHandler.AddGameGroup(checkBox, newGroup);
                    AddGameGroupCheckBox(checkBox, newGroup);
                    _gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");
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

			checkBox.Checked += UpdateFilters;
			checkBox.Unchecked += UpdateFilters;

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

		private void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			UpdateFilters(sender, e);
		}

		private void CheckBox_Click(object sender, RoutedEventArgs e)
		{
			UpdateFilters(sender, e);
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

				// Lagre de nye spill-medlemsskapene i JSON-filen:
				_gameGroupHandler.SaveGameGroupsToFile(
				eventHandler: nameof(UpdateFilters),
				styleName: "RoundedCheckBoxWithSourceSansFontStyle");
			}
		}

		private void DeleteGameGroup(GameGroup gameGroup, CheckBox checkBox)
        {
            _gameGroupHandler.RemoveGameGroup(gameGroup.GroupName);
            GameCategoriesPanel.Children.Remove(checkBox);
            _gameGroupHandler.SaveGameGroupsToFile("UpdateFilters", "RoundedCheckBoxWithSourceSansFontStyle");
			UpdateFilters(null, null); // Refresh the filters
        }


		private void LoadGameGroups()
		{
			var style = (Style)FindResource("RoundedCheckBoxWithSourceSansFontStyle");
			// Hent én gang
			var loaded = _gameGroupHandler.LoadGroupsFromFile(UpdateFilters, style, AllGames.ToList());
			// Legg til i UI
			foreach (var (cb, group) in loaded)
				AddGameGroupCheckBox(cb, group);
		}


		#endregion

		#region Toolbar og Volum kontroller


		// Når systemvolum endrer seg utenfra:
		private void OnServiceVolumeChanged(object? sender, float newScalar)
		{
			// Oppdater UI på UI‐tråd:
			Dispatcher.Invoke(() =>
			{
				VolumeSlider.Value = newScalar * 100;
				VolumeStatusTextBlock.Text = $"{(int)(newScalar * 100)}%";
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
			});
		}

		private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
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

		private void SpeakerButton_Click(object sender, RoutedEventArgs e)
		{
			// Toggle popup åpen/lukket ved trykk
			VolumePopup.IsOpen = !VolumePopup.IsOpen;
		}

		private void ExitButton_Click(object sender, RoutedEventArgs e)
		{
			if (MessageBox.Show(
				"Er du sikker på at du vil avslutte programmet?",
				"Avslutt programmet",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				// lukk! all cleanup skjer i OnClosed
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
		
			using(var cts = new CancellationTokenSource(timeoutMs))
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
                await LoadAndDisplayGamesAsync();
                InitializeGroupsAndFilters();

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
			_statusService.StartStatusUpdates(TimeSpan.FromSeconds(7));

			// 5) Logg at VR nå er tilgjengelig
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

			// —————— RESTART STEAMVR ——————
			try
			{
				await _initService.RestartSteamVRAsync();
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
	
		#endregion

		#region Spillbibliotek og Logg

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
			System.Windows.Controls.Panel.SetZIndex(KalibrerKnapp, 200);
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
			if (_initService.System == null)
				return;

			// b) Gi CVRSystem til kalibratoren – gjør dette på UI-tråd
			_calibrator.Initialize(_initService.System);

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
			_inputService.HandleKeyDown(sender,e);
		}
	}
}

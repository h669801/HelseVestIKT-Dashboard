using System;
using System.Windows.Input;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.Helpers.Commands;
using HelseVestIKT_Dashboard.Models;
using System.Collections.ObjectModel;

namespace HelseVestIKT_Dashboard.ViewModels
{
	public class MainWindowViewModel : BaseViewModel
	{
		private readonly AudioService _audio;
		private readonly GameLoadService _gameLoadService;
		private readonly FilterService _filterService;
		private readonly GameGroupHandler _gameGroupHandler;

		private double _heightSetting;
		private float _volume;
		private string _currentPlayer = "";
		private string _currentStatus = "";

		// --- Sjanger-filtre ---
		private bool _filterAction ;
		private bool _filterEventyr;
		private bool _filterFlerspiller;
		private bool _filterIndie;
		private bool _filterLettbeint;
		private bool _filterMassivtFlerspill;
		private bool _filterRacing;
		private bool _filterRollespill;
		private bool _filterSimulering;
		private bool _filterSport;
		private bool _filterStrategi;

		// --- Type-filtre ---
		private bool _filterSteamSpill;
		private bool _filterAndreSpill;
		private bool _filterVrSpill;
		private bool _filterKunFavoritter; 


		// --- Start med “Nylig spilt” aktivert ---
		private bool _filterNyligSpilt = true;

		public ObservableCollection<Game> AllGames { get; } = new ObservableCollection<Game>();
		public ObservableCollection<Game> FilteredGames { get; } = new ObservableCollection<Game>();
		public ObservableCollection<GameGroup> GameGroups { get; } = new ObservableCollection<GameGroup>();



		public MainWindowViewModel(AudioService audioService, SteamApi steamApi, GameDetailsFetcher detailsFetcher, OfflineSteamGamesManager offlineMgr)
		{
			_audio = audioService ?? throw new ArgumentNullException(nameof(audioService));
			_audio.VolumeChanged += (s, v) => Volume = v;

			_gameLoadService = new GameLoadService(steamApi, detailsFetcher, offlineMgr);
			_filterService = new FilterService();
			_gameGroupHandler = new GameGroupHandler();


			_ = InitializeAsync();
		}

		private async Task InitializeAsync()
		{
			var games = await _gameLoadService.LoadAllGamesAsync(/*SteamProfile*/ null);
			foreach (var g in games)
			{
				AllGames.Add(g);

				ApplyFilters();
			}
		}

	
		public double HeightSetting
		{
			get => _heightSetting;
			set
			{
				if (Math.Abs(_heightSetting - value) > 0.0001)
				{
					_heightSetting = value;
					RaisePropertyChanged();
					OpenVrInterop.SetHeightOffset((float)value);
				}
			}
		}

		public float Volume
		{
			get => _volume;
			set
			{
				if (Math.Abs(_volume - value) > 0.0001f)
				{
					_volume = value;
					RaisePropertyChanged();
					_audio.CurrentVolume = value;
				}
			}
		}

		public string CurrentPlayer
		{
			get => _currentPlayer;
			set { _currentPlayer = value; RaisePropertyChanged(); }
		}

		public string CurrentStatus
		{
			get => _currentStatus;
			set { _currentStatus = value; RaisePropertyChanged(); }
		}

		// ─── Sjanger-filtre ────────────────────────────────────────

		public bool FilterAction
		{
			get => _filterAction;
			set
			{
				if (_filterAction == value) return;
				_filterAction = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterEventyr
		{
			get => _filterEventyr;
			set
			{
				if (_filterEventyr == value) return;
				_filterEventyr = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterFlerspiller
		{
			get => _filterFlerspiller;
			set
			{
				if (_filterFlerspiller == value) return;
				_filterFlerspiller = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterIndie
		{
			get => _filterIndie;
			set
			{
				if (_filterIndie == value) return;
				_filterIndie = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterLettbeint
		{
			get => _filterLettbeint;
			set
			{
				if (_filterLettbeint == value) return;
				_filterLettbeint = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterMassivtFlerspill
		{
			get => _filterMassivtFlerspill;
			set
			{
				if (_filterMassivtFlerspill == value) return;
				_filterMassivtFlerspill = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterRacing
		{
			get => _filterRacing;
			set
			{
				if (_filterRacing == value) return;
				_filterRacing = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterRollespill
		{
			get => _filterRollespill;
			set
			{
				if (_filterRollespill == value) return;
				_filterRollespill = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterSimulering
		{
			get => _filterSimulering;
			set
			{
				if (_filterSimulering == value) return;
				_filterSimulering = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterSport
		{
			get => _filterSport;
			set
			{
				if (_filterSport == value) return;
				_filterSport = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterStrategi
		{
			get => _filterStrategi;
			set
			{
				if (_filterStrategi == value) return;
				_filterStrategi = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}

		// ─── Type-filtre ────────────────────────────────────────────

		public bool FilterSteamSpill
		{
			get => _filterSteamSpill;
			set
			{
				if (_filterSteamSpill == value) return;
				_filterSteamSpill = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterAndreSpill
		{
			get => _filterAndreSpill;
			set
			{
				if (_filterAndreSpill == value) return;
				_filterAndreSpill = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterVrSpill
		{
			get => _filterVrSpill;
			set
			{
				if (_filterVrSpill == value) return;
				_filterVrSpill = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterKunFavoritter
		{
			get => _filterKunFavoritter;
			set
			{
				if (_filterKunFavoritter == value) return;
				_filterKunFavoritter = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}
		public bool FilterNyligSpilt
		{
			get => _filterNyligSpilt;
			set
			{
				if (_filterNyligSpilt == value) return;
				_filterNyligSpilt = value;
				RaisePropertyChanged();
				ApplyFilters();
			}
		}

		// ─── Filter-metode ──────────────────────────────────────────

		public void ApplyFilters()
		{
			// 1) Bygg opp liste over hvilke sjangre som er huket av
			var selectedGenres = new List<string>();
			if (FilterAction) selectedGenres.Add("Action");
			if (FilterEventyr) selectedGenres.Add("Eventyr");
			if (FilterFlerspiller) selectedGenres.Add("Flerspiller");
			if (FilterIndie) selectedGenres.Add("Indie");
			if (FilterLettbeint) selectedGenres.Add("Lettbeint");
			if (FilterMassivtFlerspill) selectedGenres.Add("Massivt flerspill");
			if (FilterRacing) selectedGenres.Add("Racing");
			if (FilterRollespill) selectedGenres.Add("Rollespill");
			if (FilterSimulering) selectedGenres.Add("Simulering");
			if (FilterSport) selectedGenres.Add("Sport");
			if (FilterStrategi) selectedGenres.Add("Strategi");

			// 2) Bygg opp liste over typer
			var selectedTypes = new List<string>();
			if (FilterSteamSpill) selectedTypes.Add("Steam-spill");
			if (FilterAndreSpill) selectedTypes.Add("Andre spill");
			if (FilterVrSpill) selectedTypes.Add("VR-spill");
			if (FilterKunFavoritter) selectedTypes.Add("Vis kun favoritter");
			if (FilterNyligSpilt) selectedTypes.Add("Vis kun nylig spilt");

			// 3) Hent eventuelle GameGroup-filter
			var selectedGroups = _gameGroupHandler
				.GetGameGroups()                    // IEnumerable<(CheckBox, GameGroup)>
				.Where(p => p.Item1.IsChecked == true)
				.Select(p => p.Item2);

			// 4) Kall filter-servicen
			var filtered = _filterService.ApplyFilters(
				selectedGenres,
				selectedTypes,
				selectedGroups,
				AllGames);

			// 5) Oppdater ObservableCollection
			FilteredGames.Clear();
			foreach (var g in filtered)
				FilteredGames.Add(g);
		}

		public ICommand RefreshGameStatusCommand => new RelayCommand(_ =>
		{
			// TODO: kall GameStatusManager.UpdateCurrentGameAndStatus() og oppdater
		});

		public ICommand AddCategoryCommand { get; }
		public ICommand RenameCategoryCommand { get; }

		public ICommand DeleteCategoryCommand { get; }

	}
}
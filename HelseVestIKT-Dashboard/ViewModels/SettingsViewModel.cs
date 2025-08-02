using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Views;
using System.Collections.ObjectModel;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace HelseVestIKT_Dashboard.ViewModels
{
	public partial class SettingsViewModel : ObservableObject
	{

		// ── Data ───────────────────────────────
		public ObservableCollection<SteamProfile> Profiles { get; }
		public SteamProfile? SelectedProfile { get; set; }
		private readonly MainWindowViewModel _mainVm;

		// ── Flags ──────────────────────────────
		public bool RestartRequested { get; private set; }
		public bool UnlockRequested { get; private set; }

		// ── Commands ───────────────────────────
		public IRelayCommand RestartPCCommand { get; }
		public IRelayCommand UnlockCommand { get; }
		public IRelayCommand PinCommand { get; }
		public IRelayCommand<Window> CloseSettingsCommand { get; }
		public IAsyncRelayCommand ChangeUserCommand { get; }

		// ── Events ─────────────────────────────
		private string _lockButtonText = "Lås opp applikasjon";
		public string LockButtonText
		{
			get => _lockButtonText;
			set => SetProperty(ref _lockButtonText, value);
		}

		private bool _isLocked;
		public bool IsLocked
		{
			get => _isLocked;
			set
			{
				if (SetProperty(ref _isLocked, value))
				{
					LockButtonText = value ? "Lås opp applikasjon" : "Lås applikasjon";
				}
			}
		}

		// ── Constructor ────────────────────────
		public SettingsViewModel(MainWindowViewModel mainVm)
		{
			_mainVm = mainVm;
			// 1) Last profiler
			var data = ProfileStore.Load();
			Profiles = new ObservableCollection<SteamProfile>(data.Profiles);
			SelectedProfile = data.Profiles.FirstOrDefault(p => p.Name == data.LastProfileName);

			// 2) Definer kommandoer
			RestartPCCommand = new RelayCommand(() =>
			{
				var result = MessageBox.Show(
					"Er du sikker på at du vil restarte PC?",
					"Bekreft restart",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);
				if (result == MessageBoxResult.Yes)
				{
					RestartRequested = true;
					Win32.RestartWindows();
					CloseWindow();
				}
			});

			


			UnlockCommand = new RelayCommand(() =>
			{
				UnlockRequested = true;
				IsLocked = !IsLocked;
			});

			PinCommand = new RelayCommand(() =>
			{
				var pinWin = new PinWindow
				{
					Owner = Application.Current.MainWindow
				};
				if (pinWin.ShowDialog() == true)
				{
					UnlockRequested = true;
				}
			});


			CloseSettingsCommand = new RelayCommand<Window>(w => w?.Close());

			ChangeUserCommand = new AsyncRelayCommand(async () =>
			{
				var dlg = new ProfileManagerWindow
				{
					Owner = Application.Current.MainWindow
				};

				if (dlg.ShowDialog() == true && dlg.SelectedProfile != null)
				{
					ProfileStore.Save(new ProfilesFile
					{
						LastProfileName = dlg.SelectedProfile.Name,
						Profiles = Profiles.ToList()
					});
					CloseWindow();
				}
			});
		
		}

		private void CloseWindow()
		{
			if (Application.Current.Windows
				.OfType<Window>()
				.FirstOrDefault(w => w is SettingsWindow) is Window win)
			{
				win.DialogResult = true;
			}
		}
	}

	
}

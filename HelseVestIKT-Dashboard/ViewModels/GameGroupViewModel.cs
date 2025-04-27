using System.Collections.ObjectModel;
using HelseVestIKT_Dashboard.Models;
using System.Windows.Input;
using HelseVestIKT_Dashboard.Helpers.Commands;
using SteamKit2.WebUI.Internal;

namespace HelseVestIKT_Dashboard.ViewModels
{
	/// <summary>
	/// ViewModel for en gruppe av spill (GameGroup).
	/// </summary>
	public class GameGroupViewModel : BaseViewModel
	{
		private readonly GameGroup _model;
		public ObservableCollection<GameViewModel> Games { get; }

		public GameGroupViewModel(GameGroup model)
		{
			_model = model;
			Games = new ObservableCollection<GameViewModel>(
				model.Games.Select(g => new GameViewModel(g)));
		}

		public string GroupName
		{
			get => _model.GroupName;
			set
			{
				if (_model.GroupName != value)
				{
					_model.GroupName = value;
					RaisePropertyChanged();
				}
			}
		}

		// Kommando for å legge til et spill i gruppen
		private ICommand? _addGameCommand;
		public ICommand AddGameCommand => _addGameCommand ??= new RelayCommand(param =>
		{
			if (param is GameViewModel gvm && !Games.Contains(gvm))
			{
				Games.Add(gvm);
				_model.Games.Add(gvm.Model);
				RaisePropertyChanged(nameof(Games));
			}
		});

		// Kommando for å fjerne et spill fra gruppen
		private ICommand? _removeGameCommand;
		public ICommand RemoveGameCommand => _removeGameCommand ??= new RelayCommand(param =>
		{
			if (param is GameViewModel gvm && Games.Contains(gvm))
			{
				Games.Remove(gvm);
				_model.Games.Remove(_model.Games.Find(x => x.AppID == gvm.AppID));
				RaisePropertyChanged(nameof(Games));
			}
		});
	}
}
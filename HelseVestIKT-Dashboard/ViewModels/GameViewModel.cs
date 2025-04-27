using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HelseVestIKT_Dashboard.Helpers.Commands;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.ViewModels;
using SteamKit2.Internal;

namespace HelseVestIKT_Dashboard.ViewModels
{
	/// <summary>
	/// ViewModel for en enkel Game-modell med bindbare egenskaper.
	/// </summary>
	public class GameViewModel : BaseViewModel
	{
		private readonly Game _model;
		public Game Model => _model;


		public GameViewModel(Game model)
		{
			_model = model;
			Genres = new ObservableCollection<string>(_model.Genres);
		}

		public string AppID => _model.AppID;
		public string Title
		{
			get => _model.Title;
			set
			{
				if (_model.Title != value)
				{
					_model.Title = value;
					RaisePropertyChanged();
				}
			}
		}
		public ObservableCollection<string> Genres { get; }

		public bool IsSinglePlayer
		{
			get => _model.IsSinglePlayer;
			set
			{
				if (_model.IsSinglePlayer != value)
				{
					_model.IsSinglePlayer = value;
					RaisePropertyChanged();
				}
			}
		}

		public bool IsVR
		{
			get => _model.IsVR;
			set
			{
				if (_model.IsVR != value)
				{
					_model.IsVR = value;
					RaisePropertyChanged();
				}
			}
		}

		public bool IsFavorite
		{
			get => _model.IsFavorite;
			set
			{
				if (_model.IsFavorite != value)
				{
					_model.IsFavorite = value;
					RaisePropertyChanged();
				}
			}
		}
	}
}
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HelseVestIKT_Dashboard.Models
{
	public class SpillFilter : INotifyPropertyChanged
	{
		private string _name = string.Empty;
		public string Name
		{
			get => _name;
			set
			{
				if (_name != value)
				{
					_name = value;
					OnPropertyChanged(nameof(Name));
				}
			}
		}

		// Filteregenskaper – alle med public get og set
		public bool IsSinglePlayer { get; set; }
		public bool IsMultiplayer { get; set; }
		public bool IsCooperative { get; set; }
		public bool IsReadyToPlay { get; set; }
		public bool IsInstalledLocally { get; set; }
		public bool IsPlayed { get; set; }
		public bool IsUnplayed { get; set; }
		public bool IsControllerPreferred { get; set; }
		public bool IsFullControllerSupport { get; set; }
		public bool IsVR { get; set; }
		public bool IsAction { get; set; }
		public bool IsAdventure { get; set; }
		public bool IsCasual { get; set; }
		public bool IsIndie { get; set; }
		public bool IsRPG { get; set; }
		public bool IsSimulation { get; set; }
		public bool IsSports { get; set; }
		public bool IsStrategy { get; set; }

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

using System.ComponentModel;

namespace HelseVestIKT_Dashboard.Models
{
	/// <summary>
	/// Representerer et spill i gruppe‐dialogen med tittel og avkrysning.
	/// </summary>
	public class GameElement : INotifyPropertyChanged
	{
		private bool _isChecked;

		/// <summary>Visningsnavn for spillet.</summary>
		public string Title { get; }

		/// <summary>Om elementet er valgt (avkrysset) i UI.</summary>
		public bool IsChecked
		{
			get => _isChecked;
			set
			{
				if (_isChecked != value)
				{
					_isChecked = value;
					OnPropertyChanged(nameof(IsChecked));
				}
			}
		}

		public GameElement(string title, bool isChecked)
		{
			Title = title;
			_isChecked = isChecked;
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

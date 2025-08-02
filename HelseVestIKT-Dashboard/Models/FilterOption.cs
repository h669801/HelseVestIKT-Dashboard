// Fil: Models/FilterOption.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace HelseVestIKT_Dashboard.Models
{
	public class FilterOption : INotifyPropertyChanged
	{
		public string DisplayName { get; }
		public string Key { get; }    // den engelske tag‐strengen

		private bool _isChecked;
		public bool IsChecked
		{
			get => _isChecked;
			set
			{
				if (_isChecked == value) return;
				_isChecked = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
			}
		}

		public FilterOption(string displayName, string key)
		{
			DisplayName = displayName;
			Key = key;
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}

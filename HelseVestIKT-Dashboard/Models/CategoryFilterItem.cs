using System.ComponentModel;

namespace HelseVestIKT_Dashboard.Models
{
	/// <summary>
	/// Wrapper en GameGroup for bruk i MVVM-filtrering.
	/// Eksponerer både selve GameGroup og en bool for om den er huket av.
	/// </summary>
	public class CategoryFilterItem : INotifyPropertyChanged
	{
		/// <summary>Den underliggende spill­gruppen (modell).</summary>
		public GameGroup Group { get; }

		private bool _isChecked;
		/// <summary>Om brukeren har huket av denne kategorien.</summary>
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

		/// <summary>
		/// Konstruktør tar inn en eksisterende GameGroup.
		/// </summary>
		public CategoryFilterItem(GameGroup group)
		{
			Group = group ?? throw new System.ArgumentNullException(nameof(group));
			IsChecked = false;  // start med avhukete = false
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}
}

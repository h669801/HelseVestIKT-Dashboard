using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HelseVestIKT_Dashboard.ViewModels
{
	public class BaseViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		protected void RaisePropertyChanged([CallerMemberName] string name = "")
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
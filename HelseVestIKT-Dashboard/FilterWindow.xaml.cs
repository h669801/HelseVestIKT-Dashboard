using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfFilterDemo
{
	public partial class FilterWindow : Window
	{
		public FilterWindow()
		{
			InitializeComponent();
		}

		// Example: store the user’s selections in properties that MainWindow can read
		public bool IsSinglePlayer { get; private set; }
		public bool IsMultiplayer { get; private set; }
		public bool IsCooperative { get; private set; }

		public bool IsReadyToPlay { get; private set; }
		public bool IsInstalledLocally { get; private set; }
		public bool IsPlayed { get; private set; }
		public bool IsUnplayed { get; private set; }

		public bool IsControllerPreferred { get; private set; }
		public bool IsFullControllerSupport { get; private set; }
		public bool IsVR { get; private set; }
		public string SelectedGamepad { get; private set; } = "Any";

		public bool IsTradingCards { get; private set; }
		public bool IsWorkshop { get; private set; }
		public bool IsAchievements { get; private set; }
		public bool IsRemotePlayTogether { get; private set; }
		public bool IsFamilySharing { get; private set; }

		public bool IsAction { get; private set; }
		public bool IsAdventure { get; private set; }
		public bool IsCasual { get; private set; }
		public bool IsIndie { get; private set; }
		public bool IsRPG { get; private set; }
		public bool IsSimulation { get; private set; }
		public bool IsSports { get; private set; }
		public bool IsStrategy { get; private set; }

		public string StoreTags { get; private set; } = string.Empty;
		public string FriendsName { get; private set; } = string.Empty;

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			// Capture the user’s selections
			IsSinglePlayer = CheckBoxSinglePlayer.IsChecked == true;
			IsMultiplayer = CheckBoxMultiplayer.IsChecked == true;
			IsCooperative = CheckBoxCooperative.IsChecked == true;

			IsReadyToPlay = CheckBoxReadyToPlay.IsChecked == true;
			IsInstalledLocally = CheckBoxInstalledLocally.IsChecked == true;
			IsPlayed = CheckBoxPlayed.IsChecked == true;
			IsUnplayed = CheckBoxUnplayed.IsChecked == true;

			IsControllerPreferred = CheckBoxControllerPreferred.IsChecked == true;
			IsFullControllerSupport = CheckBoxFullControllerSupport.IsChecked == true;
			IsVR = CheckBoxVR.IsChecked == true;

			// Grab selected item from the ComboBox
			if (ComboBoxGamepads.SelectedItem is ComboBoxItem item && item.Content != null)
			{
				SelectedGamepad = item.Content.ToString();
			}

			IsTradingCards = CheckBoxTradingCards.IsChecked == true;
			IsWorkshop = CheckBoxWorkshop.IsChecked == true;
			IsAchievements = CheckBoxAchievements.IsChecked == true;
			IsRemotePlayTogether = CheckBoxRemotePlayTogether.IsChecked == true;
			IsFamilySharing = CheckBoxFamilySharing.IsChecked == true;

			IsAction = CheckBoxAction.IsChecked == true;
			IsAdventure = CheckBoxAdventure.IsChecked == true;
			IsCasual = CheckBoxCasual.IsChecked == true;
			IsIndie = CheckBoxIndie.IsChecked == true;
			IsRPG = CheckBoxRPG.IsChecked == true;
			IsSimulation = CheckBoxSimulation.IsChecked == true;
			IsSports = CheckBoxSports.IsChecked == true;
			IsStrategy = CheckBoxStrategy.IsChecked == true;

			StoreTags = StoreTagsTextBox.Text;
			FriendsName = FriendsTextBox.Text;

			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}

using HelseVestIKT_Dashboard;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Dialogs
{
    public partial class GameCategoryDialog : Window
    {
        public GameGroup gameGroup { get; set; }
        public List<GameElement> SelectedGames { get; set; } = new();
        public List<Game> AllGames { get; set; } = new();
        public event EventHandler<GameGroup> GameGroupChanged;

        public GameCategoryDialog(List<Game> allGames, GameGroup gameGroup)
        {
            InitializeComponent();
            this.gameGroup = gameGroup;
            AllGames = allGames;

            SelectedGames.Clear();
            foreach (Game game in AllGames)
            {
                Console.WriteLine($"Game: {game.Title}");
                SelectedGames.Add(new GameElement(game, gameGroup.HasGame(game)));
            }

            gameItemsControl.ItemsSource = SelectedGames.OrderBy(a => a.Title); 
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            gameGroup.Games = SelectedGames
                .Where(g => g.IsChecked)
                .Select(g => AllGames.FirstOrDefault(game => game.Title == g.Title))
                .ToList();
           
            this.DialogResult = true;
            GameGroupChanged.Invoke(this, gameGroup);
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }

    public class GameElement
    {
        public string Title { get; set; }
        public bool IsChecked { get; set; }

        public GameElement(Game game, bool isChecked)
        {
            this.Title = game.Title;
            this.IsChecked = isChecked;
        }
    }
}

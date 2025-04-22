using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;

namespace HelseVestIKT_Dashboard
{
    public class FilterHandler
    {
        private Dictionary<string, string> GenreTranslation = new(); // Translate Checkbox content to Game.Genre

        public FilterHandler()
        {
            GenreTranslation.Add("Action", "Action");
            GenreTranslation.Add("Eventyr", "Adventure");
            GenreTranslation.Add("Indie", "Indie");
            GenreTranslation.Add("Lettbeint", "Casual");
            GenreTranslation.Add("Massivt flerspill", "Massively Multiplayer");
            GenreTranslation.Add("Racing", "Racing");
            GenreTranslation.Add("Rollespill", "RPG");
            GenreTranslation.Add("Simulering", "Simulation");
            GenreTranslation.Add("Sport", "Sports");
            GenreTranslation.Add("Strategi", "Strategy");
        }

        public bool FilterGame(List<CheckBox> genres, List<CheckBox> types, List<(CheckBox box, GameGroup group)> gameGroups, Game game)
        {
            return FilterGameType(types, game) && FilterGameGenre(genres, game) && FilterGameGroups(gameGroups, game) ;
        }

        private bool FilterGameGenre(List<CheckBox> filters, Game game)
        {
            bool nonchecked = true; // Check if none of the checkboxes are ticked

            foreach (CheckBox box in filters)
            {
                if (box.IsChecked.Value)
                {
                    nonchecked = false;
                    string? key = box.Content.ToString();
                    if (GenreTranslation.ContainsKey(key) && game.Genres.Contains(GenreTranslation[key]))
                    {
                        return true;
                    }
                }
            }

            return nonchecked;
        }

        private static bool FilterGameGroups(List<(CheckBox box, GameGroup group)> gameGroups, Game game)
        {
            bool nonchecked = true;
            bool add = false;

            foreach (var (box, group) in gameGroups)
            {
                if (box.IsChecked == true)
                {
                    nonchecked = false;
                    if (group.HasGame(game))
                    {
                        add = true;
                        break; // No need to continue if we found a match
                    }
                }
            }

            return add || nonchecked;
        }

        private static bool FilterGameType(List<CheckBox> types, Game game)
        {
            bool nonchecked = true;

            foreach (CheckBox box in types)
            {
                if (box.IsChecked.Value)
                {
                    nonchecked = false;
                    switch (box.Content.ToString())
                    {
                        case "Vis kun nylig spilt":
                            if (game.IsRecentlyPlayed) return true;
                            break;
                        case "VR-spill":
                            if (game.IsVR) return true;
                            break;
                        case "Steam spill":
                            if (game.IsSteamGame) return true;
                            break;
                        case "Andre spill":
                            if (!game.IsSteamGame) return true;
                            break;
                        case "Flerspiller":
                            if (!game.IsSinglePlayer) return true;
                            break;
                        case "Vis kun favoritter":
                            if (game.IsFavorite) return true;
                            break;
                        default:
                            break;
                    }
                }
            }

            return nonchecked;
        }
    }
}
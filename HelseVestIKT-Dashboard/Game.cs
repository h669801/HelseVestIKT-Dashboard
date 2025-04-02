using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace HelseVestIKT_Dashboard
{
    public class Game
    {
        public required string AppID { get; set; }
        public required string Title { get; set; } = string.Empty;
		public BitmapImage? GameImage { get; set; }

		public string ProcessName { get; set; } = string.Empty;

		public List<string> Genres { get; set; } = new List<string>();

		// Example booleans for filtering
		public bool IsSinglePlayer { get; set; }
		public bool IsSteamGame { get; set; }
		public bool IsVR { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsRecentlyPlayed { get; set; }

	}
}

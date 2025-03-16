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

		// Example booleans for filtering
		public bool IsSinglePlayer { get; set; }
		public bool IsMultiplayer { get; set; }
		public bool IsCoop { get; set; }
		public bool IsVR { get; set; }

		public string Genre { get; set; } = string.Empty;

	}
}

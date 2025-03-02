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
        public required string Title { get; set; }
        public BitmapImage? GameImage { get; set; }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard
{
    public class GameGroup
    {
        public required string GroupName { get; set; }
        public List<Game> Games { get; set; }

        public GameGroup()
        {
            Games = new List<Game>();
        }

        public bool HasGame(Game game)
        {
            return Games.Contains(game);
        } 
    }
}

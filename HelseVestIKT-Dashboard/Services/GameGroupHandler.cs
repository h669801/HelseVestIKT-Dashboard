// Services/GameGroupHandler.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.ViewModels;

namespace HelseVestIKT_Dashboard.Services
{
	public class GameGroupHandler
	{
		private readonly string _filepath;
		public GameGroupHandler()
		{
			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			_filepath = Path.Combine(baseDir, "Assets", "gameGroups.json");
			Directory.CreateDirectory(Path.GetDirectoryName(_filepath)!);
			if (!File.Exists(_filepath))
				File.WriteAllText(_filepath, "[]");
		}

		// Laster kun modellene, ikke CheckBox
		public List<GameGroup> LoadGroups(List<Game> allGames)
		{
			var json = File.ReadAllText(_filepath);
			var list = JsonConvert.DeserializeObject<List<SerializedGroup>>(json)
					   ?? new List<SerializedGroup>();

			// Oppdater Game-listene basert på AppID
			foreach (var sg in list)
			{
				sg.Group.Games = allGames
					.Where(g => sg.GameAppIDs.Contains(g.AppID))
					.ToList();
			}
			// Returner modellene i sortert rekkefølge
			return list.Select(sg => sg.Group)
					   .OrderBy(g => g.GroupName)
					   .ToList();
		}

		public void SaveGroups(IEnumerable<GameGroup> groups)
		{
			var ser = groups.Select(g => new SerializedGroup
			{
				Group = g,
				GameAppIDs = g.Games.Select(x => x.AppID).ToList()
			}).ToList();
			File.WriteAllText(_filepath, JsonConvert.SerializeObject(ser, Formatting.Indented));
		}

		private class SerializedGroup
		{
			public GameGroup Group { get; set; }
			public List<string> GameAppIDs { get; set; }
		}
	}
}

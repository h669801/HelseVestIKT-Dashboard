using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Services
{
	internal class GameGroupHandler
	{
		//   private string filepath { get; set; } = "gameGroups.json";
		//  private List<(CheckBox box, GameGroup group)> gameGroups;

		private readonly string filepath;
		private List<(CheckBox box, GameGroup group)> gameGroups;

		public GameGroupHandler()
		{

			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			filepath = Path.Combine(baseDir, "Assets", "gameGroups.json");
			Directory.CreateDirectory(Path.GetDirectoryName(filepath)!);
			if (!File.Exists(filepath))
				File.WriteAllText(filepath, JsonConvert.SerializeObject(new List<SerializedGroup>(), Formatting.Indented));
			gameGroups = new();
		}

		public void AddGameGroup(CheckBox box, GameGroup gameGroup)
		{
			gameGroups.Add((box, gameGroup));
		}

		public void RemoveGameGroup(string groupName)
		{
			gameGroups.RemoveAll(g => g.group.GroupName == groupName);
		}

		public List<(CheckBox box, GameGroup group)> GetGameGroups() => gameGroups;

		public GameGroup GetGameGroup(string groupName)
		{
			return gameGroups.FirstOrDefault(g => g.group.GroupName == groupName).group;
		}

		public void SaveGameGroupsToFile(string eventHandler, string styleName)
		{
			var json = JsonConvert.SerializeObject(gameGroups.Select(g => new SerializedGroup
			{
				Group = g.group,
				ClickEventHandlerName = eventHandler,
				StyleName = styleName,
				GameAppIDs = g.group.Games.Select(game => game.AppID).ToList() // Store only AppIDs
			}).OrderBy(g => g.Group.GroupName), Formatting.Indented);
			File.WriteAllText(filepath, json);
		}




		/*  public List<(CheckBox, GameGroup)> LoadGroupsFromFile(RoutedEventHandler clickFunction, Style checkBoxStyle,List<Game> AllGames)
		  {
			  if (!File.Exists(filepath))
			  {
				  return new List<(CheckBox, GameGroup)>();
			  }

			  var json = File.ReadAllText(filepath);
			  if (string.IsNullOrWhiteSpace(json))
			  {
				  return new List<(CheckBox, GameGroup)>();
			  }

			  var loadedGroups = JsonConvert.DeserializeObject<List<SerializedGroup>>(json);
			  if (loadedGroups == null)
			  {
				  return new List<(CheckBox, GameGroup)>();
			  }

			  var result = loadedGroups.Where(g => g != null && g.Group != null).Select(g =>
			  {
				  var checkBox = new CheckBox
				  {
					  Content = g.Group.GroupName,
					  Style = checkBoxStyle
				  };
				  checkBox.Click += clickFunction;

				  // Filter games using AppIDs
				  g.Group.Games = AllGames.Where(game => g.GameAppIDs.Contains(game.AppID)).ToList();

				  return (checkBox, g.Group);
			  }).OrderBy(g => g.Item2.GroupName).ToList();

			  // Add loaded groups to the gameGroups list
			  gameGroups.AddRange(result);

			  Console.WriteLine($"FILELOCATION : {Path.Combine(Environment.CurrentDirectory, filepath)}\n{json}");
			  return result;
		  } */

		/* ORGINAL METODE
		public List<(CheckBox, GameGroup)> LoadGroupsFromFile(RoutedEventHandler clickFunction, Style checkBoxStyle, List<Game> AllGames)
		{
			if (!File.Exists(filepath))
				return new List<(CheckBox, GameGroup)>();

			var json = File.ReadAllText(filepath);
			if (string.IsNullOrWhiteSpace(json))
				return new List<(CheckBox, GameGroup)>();

			var loadedGroups = JsonConvert.DeserializeObject<List<SerializedGroup>>(json);
			if (loadedGroups == null)
				return new List<(CheckBox, GameGroup)>();

			var result = loadedGroups
	.Where(g => g.Group != null)
	.Select(g =>
	{
		// 1) Bygg CheckBox, men sett style og event-handler kun om de ikke er null
		var checkBox = new CheckBox
		{
			Content = g.Group.GroupName
		};
		if (checkBoxStyle != null)
			checkBox.Style = checkBoxStyle;
		if (clickFunction != null)
			checkBox.Click += clickFunction;

		// 2) Gjør GameAppIDs og AllGames “trygge” mot null
		var appIDs = g.GameAppIDs ?? new List<string>();
		var allGamesList = AllGames ?? new List<Game>();

		// 3) Filtrer spill ut fra disse listene
		g.Group.Games = allGamesList
			.Where(game => appIDs.Contains(game.AppID))
			.ToList();

		return (checkBox, g.Group);
	})
	.OrderBy(g => g.Item2.GroupName)
	.ToList();


			gameGroups.AddRange(result);

			// For debugging:
			Console.WriteLine($"Loading groups from: {filepath}\n{json}");

			return result;
		}
}
		*/

		public List<(CheckBox box, GameGroup group)> LoadGroupsFromFile(RoutedEventHandler clickFunction, Style checkBoxStyle, List<Game> allGames)
		{
			// 1) Null-sjekk på liste som holder alle grupper i denne instansen:
			gameGroups ??= new List<(CheckBox, GameGroup)>();
			gameGroups.Clear();

			// 2) Eksisterer filen?
			if (!File.Exists(filepath))
				return new List<(CheckBox, GameGroup)>();

			// 3) Les og valider JSON-innhold
			var json = File.ReadAllText(filepath);
			if (string.IsNullOrWhiteSpace(json))
				return new List<(CheckBox, GameGroup)>();

			List<SerializedGroup>? loadedGroups;
			try
			{
				loadedGroups = JsonConvert.DeserializeObject<List<SerializedGroup>>(json);
			}
			catch (JsonException)
			{
				// Ødelagt JSON → returner tomt
				return new List<(CheckBox, GameGroup)>();
			}
			if (loadedGroups == null)
				return new List<(CheckBox, GameGroup)>();

			// 4) Bygg resultatlisten med null-sikre operasjoner
			var result = new List<(CheckBox, GameGroup)>();
			foreach (var sg in loadedGroups)
			{
				// Hopp over ugyldige elementer
				if (sg?.Group == null)
					continue;

				// Opprett checkbox
				var cb = new CheckBox { Content = sg.Group.GroupName };
				if (checkBoxStyle != null)
					cb.Style = checkBoxStyle;
				if (clickFunction != null)
					cb.Click += clickFunction;

				// Null-sikre lister
				var appIDs = sg.GameAppIDs ?? new List<string>();
				var gamesSrc = allGames ?? new List<Game>();

				// Filtrer spill
				var matched = gamesSrc
					.Where(g => appIDs.Contains(g.AppID))
					.ToList();

				// Oppdater gruppens Games-egenskap
				sg.Group.Games = matched;

				// Legg til i resultat og i intern liste
				result.Add((cb, sg.Group));
				gameGroups.Add((cb, sg.Group));
			}

			return result;
		}


		public class SerializedGroup
		{
			public string ClickEventHandlerName { get; set; }
			public string StyleName { get; set; }
			public GameGroup Group { get; set; }
			public List<string> GameAppIDs { get; set; } // Store only AppIDs
		}
	}
}
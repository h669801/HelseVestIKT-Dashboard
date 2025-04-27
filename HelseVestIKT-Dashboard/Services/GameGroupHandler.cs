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
        private string filepath { get; set; } = "gameGroups.json";
        private List<(CheckBox box, GameGroup group)> gameGroups;

        public GameGroupHandler()
        {
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




        public List<(CheckBox, GameGroup)> LoadGroupsFromFile(RoutedEventHandler clickFunction, Style checkBoxStyle,List<Game> AllGames)
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
        }












    }

    public class SerializedGroup
    {
        public string ClickEventHandlerName { get; set; }
        public string StyleName { get; set; }
        public GameGroup Group { get; set; }
        public List<string> GameAppIDs { get; set; } // Store only AppIDs
    }


    


}
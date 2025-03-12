using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ValveKeyValue;

namespace HelseVestIKT_Dashboard
{
	public class OfflineSteamGamesManager
	{
		/// <summary>
		/// Reads Steam's libraryfolders.vdf and each appmanifest_*.acf file to build a list of installed games.
		/// </summary>
		/// <param name="steamPath">The path to your Steam installation (e.g., "C:\Program Files (x86)\Steam")</param>
		/// <returns>A list of Game objects containing AppID and Title.</returns>
		public List<Game> GetOfflineSteamGames(string steamPath)
		{
			var games = new List<Game>();

			// Build the path to libraryfolders.vdf (usually under steamapps)
			string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(libraryFoldersPath))
			{
				Console.WriteLine("libraryfolders.vdf not found at: " + libraryFoldersPath);
				return games;
			}

			// Read the libraryfolders.vdf file content.
			string vdfText = File.ReadAllText(libraryFoldersPath);
			var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
			Dictionary<string, object> parsedVdf;
			// Use a MemoryStream from the VDF text.
			using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(vdfText)))
			{
				parsedVdf = serializer.Deserialize<Dictionary<string, object>>(ms);
			}

			// The VDF file should contain a "libraryfolders" key with the library data.
			if (parsedVdf.TryGetValue("libraryfolders", out object libraryFoldersObj) &&
				libraryFoldersObj is Dictionary<string, object> libraryFolders)
			{
				foreach (var folderKvp in libraryFolders)
				{
					// Each library folder is represented as a dictionary with a "path" key.
					if (folderKvp.Value is Dictionary<string, object> folderData &&
						folderData.TryGetValue("path", out object pathObj))
					{
						string folderPath = pathObj.ToString();
						if (string.IsNullOrEmpty(folderPath))
							continue;

						string appsPath = Path.Combine(folderPath, "steamapps");
						if (!Directory.Exists(appsPath))
							continue;

						// For each app manifest in this library folder:
						foreach (var manifestFile in Directory.GetFiles(appsPath, "appmanifest_*.acf"))
						{
							string manifestContent = File.ReadAllText(manifestFile);
							Dictionary<string, object> manifestData;
							// Again, use a MemoryStream for the manifest content.
							using (var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestContent)))
							{
								manifestData = serializer.Deserialize<Dictionary<string, object>>(manifestStream);
							}

							if (manifestData.TryGetValue("AppState", out object appStateObj) &&
								appStateObj is Dictionary<string, object> appState)
							{
								string? appId = appState.TryGetValue("appid", out object appIdObj) ? appIdObj.ToString() : "";
								string? name = appState.TryGetValue("name", out object nameObj) ? nameObj.ToString() : "";

								games.Add(new Game
								{
									AppID = appId,
									Title = name,
									// Optionally, you can load a local image here.
								});
							}
						}
					}
				}
			}
			else
			{
				Console.WriteLine("Could not find 'libraryfolders' key in the VDF file.");
			}
			return games;
		}
	}
}

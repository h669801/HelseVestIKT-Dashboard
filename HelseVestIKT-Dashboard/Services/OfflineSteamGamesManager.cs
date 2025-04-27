using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HelseVestIKT_Dashboard;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ValveKeyValue;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Services
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
			string libraryFoldersPath = System.IO.Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
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

						string appsPath = System.IO.Path.Combine(folderPath, "steamapps");
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

        public List<Game> GetNonSteamGames(string steamPath)
        {
            var games = new List<Game>();
            string shortcutsPath = GetShortcutsPath(steamPath);
            if (!string.IsNullOrEmpty(shortcutsPath) && File.Exists(shortcutsPath))
            {
                byte[] bytes = File.ReadAllBytes(shortcutsPath);
        // Prøv både UTF8 og Encoding.Default hvis UTF8 ikke fungerer
        string content = Encoding.UTF8.GetString(bytes);
        Console.WriteLine("Contents of shortcuts.vdf:");
                Console.WriteLine(content);

                // Preprosesser filen ved å erstatte kontrolltegn med linjeskift
                // 0x01 og 0x02 er vanlige avgrenser i denne filen
                string processedContent = content.Replace("\x01", "\n").Replace("\x02", "\n");
        Console.WriteLine("Processed shortcuts.vdf content:");
                Console.WriteLine(processedContent);

                // Split opp i linjer
                var lines = processedContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        string currentAppName = null;
        string currentExe = null;
        int shortcutIndex = 0;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Sjekk etter "AppName"
                    if (trimmed.StartsWith("AppName", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fjern "AppName" og eventuelle anførselstegn
                        currentAppName = trimmed.Substring("AppName".Length).Trim().Trim('"');
    }
                    // Sjekk etter "Exe"
                    else if (trimmed.StartsWith("Exe", StringComparison.OrdinalIgnoreCase))
                    {
                        currentExe = trimmed.Substring("Exe".Length).Trim().Trim('"');
}

// Hvis vi har både appName og exe, legg til spillet og nullstill variablene
if (!string.IsNullOrEmpty(currentAppName) && !string.IsNullOrEmpty(currentExe))
{
    // Fjern eventuelle anførselstegn og null-tegn
    currentExe = currentExe.Replace("\0", "").Trim().Trim('"');
    Console.WriteLine($"Clean exe path: {currentExe}");
    Console.WriteLine($"Found non-steam game: {currentAppName} with exe: {currentExe}");

    BitmapImage? icon = GameImage.LoadIconFromExe(currentExe);
    if (icon == null)
    {
        Console.WriteLine($"No icon extracted for exe: {currentExe}");
    }
    else
    {
        Console.WriteLine($"Icon extracted for exe: {currentExe}");
    }
    games.Add(new Game
    {
        AppID = "NonSteam-" + shortcutIndex,
        Title = currentAppName,
        InstallPath = currentExe,
        IsSteamGame = false,
        GameImage = icon
    });
    shortcutIndex++;
    currentAppName = null;
    currentExe = null;
}

                }
            }
            else
{
    Console.WriteLine("No shortcuts.vdf found.");
}


return games;


        }

        private string GetShortcutsPath(string steamPath)
{
    string userdataPath = System.IO.Path.Combine(steamPath, "userdata");
    Console.WriteLine("Sjekker userdata-mappen: " + userdataPath);

    if (!Directory.Exists(userdataPath))
    {
        Console.WriteLine("Userdata-mappen finnes ikke.");
        return "";
    }

    // Iterer gjennom alle undermapper (brukerkontoer)
    foreach (var userFolder in Directory.GetDirectories(userdataPath))
    {
        Console.WriteLine("Fant brukermappe: " + userFolder);
        string configFolder = System.IO.Path.Combine(userFolder, "config");
        if (!Directory.Exists(configFolder))
        {
            Console.WriteLine("  Config-mappen finnes ikke i: " + userFolder);
            continue;
        }

        string shortcutsFile = System.IO.Path.Combine(configFolder, "shortcuts.vdf");
        Console.WriteLine("  Sjekker: " + shortcutsFile);
        if (File.Exists(shortcutsFile))
        {
            Console.WriteLine("  Fant shortcuts.vdf i: " + shortcutsFile);
            return shortcutsFile;
        }
    }

    Console.WriteLine("Fant ingen shortcuts.vdf i userdata-mappen.");
    return "";
}



    }
}

using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ValveKeyValue;
using Newtonsoft.Json.Linq;
using System.Windows.Media.Imaging;

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

            // Finn alle bibliotekene (fra libraryfolders.vdf)
            string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            Console.WriteLine($"Looking for libraryfolders.vdf at: {libraryFoldersPath}");
            if (!File.Exists(libraryFoldersPath))
            {
                Console.WriteLine("❌ libraryfolders.vdf not found.");
                return games;
            }

            // Les inn innholdet i libraryfolders.vdf
            string vdfText = File.ReadAllText(libraryFoldersPath);
            Console.WriteLine("Contents of libraryfolders.vdf:");
            Console.WriteLine(vdfText);

            // Regex for å finne bibliotekene og deres stier
            //var regex = new Regex(@"\s*(\d+)\s*\{\s*""path""\s*""([^""]+)""\s*.*\}");
            var regex = new Regex(@"""(\d+)""\s*\{\s*""path""\s*""([^""]+)""", RegexOptions.Multiline);
            var matches = regex.Matches(vdfText);

            // Gå gjennom hvert bibliotek
            foreach (Match match in matches)
            {
                var index = match.Groups[1].Value;
                var path = match.Groups[2].Value;

                Console.WriteLine($"Found library at index {index}: {path}");

                // Søk etter alle appmanifest-filer i biblioteket
                string appManifestPath = Path.Combine(path, "steamapps");
                if (Directory.Exists(appManifestPath))
                {
                    var appManifestFiles = Directory.GetFiles(appManifestPath, "appmanifest_*.acf");
                    foreach (var file in appManifestFiles)
                    {
                        try
                        {
                            // Les inn hver appmanifest-fil
                            var manifestContent = File.ReadAllText(file);

                            // Ekstraher AppID fra filen (AppID er alltid i form av en "appid" nøkkel)
                            var appIdMatch = Regex.Match(manifestContent, @"\s*""appid""\s*""(\d+)""");
                            if (appIdMatch.Success)
                            {
                                var appId = appIdMatch.Groups[1].Value;

                                // Ekstraher spillnavnet fra filen (spillnavnet finnes ofte under "name"-nøkkelen)
                                var nameMatch = Regex.Match(manifestContent, @"\s*""name""\s*""([^""]+)""");
                                var name = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown";

                                Console.WriteLine($"Found game: AppID = {appId}, Name = {name}");

                                games.Add(new Game { AppID = appId, Title = name, IsSteamGame = true });
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading {file}: {ex.Message}");
                        }
                    }
                }
            }

            if (games.Count == 0)
            {
                Console.WriteLine("No games found.");
            }

            //return games;



            // 4️⃣ Hent ikke-Steam-spill fra shortcuts.vdf
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
            string userdataPath = Path.Combine(steamPath, "userdata");
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
                string configFolder = Path.Combine(userFolder, "config");
                if (!Directory.Exists(configFolder))
                {
                    Console.WriteLine("  Config-mappen finnes ikke i: " + userFolder);
                    continue;
                }

                string shortcutsFile = Path.Combine(configFolder, "shortcuts.vdf");
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

using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard;
using HelseVestIKT_Dashboard.Models;

public class GameLoadService
{
	private readonly SteamApi _steamApi;
	private readonly GameDetailsFetcher _detailsFetcher;
	private readonly OfflineSteamGamesManager _offlineMgr;

	public GameLoadService(
		SteamApi steamApi,
		GameDetailsFetcher detailsFetcher,
		OfflineSteamGamesManager offlineMgr)
	{
		_steamApi = steamApi ?? throw new ArgumentNullException(nameof(steamApi));
		_detailsFetcher = detailsFetcher ?? throw new ArgumentNullException(nameof(detailsFetcher));
		_offlineMgr = offlineMgr ?? throw new ArgumentNullException(nameof(offlineMgr));
	}

	public async Task<List<Game>> LoadAllGamesAsync(SteamProfile profile)
	{
		var allGames = new List<Game>();

		// 1) Hent alle Steam-spill med robust feil-håndtering
		List<Game> steamGames;
		try
		{
			steamGames = await _steamApi.GetSteamGamesAsync()
						 ?? new List<Game>();
			Console.WriteLine($"[GameLoadService] Fant {steamGames.Count} Steam-spill.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GameLoadService] Feilet i GetSteamGamesAsync:\n{ex}");
			steamGames = new List<Game>();
		}

		// 2) Berik Steam-spillene i parallell (hvis noen)
		if (steamGames.Any())
		{
			var detailTasks = steamGames.Select(g => _detailsFetcher.AddDetailsAsync(g));
			await Task.WhenAll(detailTasks);
			allGames.AddRange(steamGames);
		}
		else
		{
			Console.WriteLine("[GameLoadService] Hopper over berikelse av Steam-spill (ingen spill).");
		}

		// 3) Les offline-spill uansett
		try
		{
			string steamPath = GameProcessService.GetSteamInstallPathFromRegistry();
			var offline = _offlineMgr
				.GetNonSteamGames(steamPath)
				.Where(g => !allGames.Any(s => s.AppID == g.AppID))
				.ToList();

			Console.WriteLine($"[GameLoadService] Fant {offline.Count} lokale spill.");
			allGames.AddRange(offline);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GameLoadService] Feil ved lasting av offline-spill: {ex.Message}");
		}

		// 4) Returner samlet liste
		Console.WriteLine($"[GameLoadService] Returnerer totalt {allGames.Count} spill.");
		return allGames;
	}
}

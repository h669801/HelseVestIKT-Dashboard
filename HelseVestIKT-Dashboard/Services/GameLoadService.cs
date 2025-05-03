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
		// 1) Hent alle Steam‐spill
		var steamGames = await _steamApi.GetSteamGamesAsync();

		// 2) Berik i parallell
		var detailTasks = steamGames.Select(g => _detailsFetcher.AddDetailsAsync(g));
		await Task.WhenAll(detailTasks);

		// 3) Les offline‐spill
		string steamPath = GameProcessService.GetSteamInstallPathFromRegistry();
		var offline = _offlineMgr
			.GetNonSteamGames(steamPath)
			.Where(g => !steamGames.Any(s => s.AppID == g.AppID))
			.ToList();

		// 4) Slå sammen og returner
		return steamGames.Concat(offline).ToList();
	}
}

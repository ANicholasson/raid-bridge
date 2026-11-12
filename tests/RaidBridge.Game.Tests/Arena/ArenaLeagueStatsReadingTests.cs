using RaidBridge.Domain.Champions;
using RaidBridge.Domain.Stats;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Arena;

namespace RaidBridge.Game.Tests.Arena;

/// <summary>
/// What the Account's Classic Arena league adds to each Champion's stats, read end to end through the fake memory port
/// (https://github.com/ANicholasson/raid-bridge/issues/20). The synthetic static data gives SilverIII, the default
/// Account's league, 10% of Health, Attack and Defence, and GoldII 16% of those and 5% of Speed, Resistance and
/// Accuracy.
/// </summary>
public sealed class ArenaLeagueStatsReadingTests : IDisposable
{
	private const double Unit = 1L << 32;

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>Every Champion gets its league's share of its own Base stats, whether it defends or not.</summary>
	[Fact]
	public void Adds_the_leagues_share_of_each_champions_base_stats()
	{
		var champions = _game.Source().Read().Champions;

		Assert.All(champions, champion => Assert.Equal(Shares(champion, (Stat.Health, 0.1), (Stat.Attack, 0.1), (Stat.Defence, 0.1)), champion.Stats.ArenaLeague));
	}

	/// <summary>A league's bonus to any of the six stats it can raise is taken.</summary>
	[Fact]
	public void Adds_each_stat_the_league_raises()
	{
		using var game = WithClassic(SyntheticArenaData.Default.Classic with { DefenceTeam = new(22, [3]), DefenseHeroIds = [3] });

		var knight = game.Source().Read().Champions.Single(champion => champion.Id == 3);

		Assert.Equal(
			Shares(knight, (Stat.Health, 0.16), (Stat.Attack, 0.16), (Stat.Defence, 0.16), (Stat.Speed, 0.05), (Stat.Resistance, 0.05), (Stat.Accuracy, 0.05)),
			knight.Stats.ArenaLeague);
	}

	/// <summary>The game counts the league only while DefenseHeroIds names a Champion, whatever the defence team holds.</summary>
	[Theory]
	[MemberData(nameof(NoDefence))]
	public void Adds_nothing_without_a_defence(int[]? defenseHeroIds)
	{
		using var game = WithClassic(SyntheticArenaData.Default.Classic with { DefenseHeroIds = defenseHeroIds });

		var account = game.Source().Read();

		Assert.Equal("SilverIII", account.ClassicArena.League);
		Assert.All(account.Champions, champion => Assert.Empty(champion.Stats.ArenaLeague));
	}

	public static TheoryData<int[]?> NoDefence() => new([null!, [], [-1, -1, -1, -1]]);

	/// <summary>A defence in no league adds nothing.</summary>
	[Fact]
	public void Adds_nothing_in_no_league()
	{
		using var game = WithClassic(SyntheticArenaData.Default.Classic with { DefenceTeam = new(0, [3]), DefenseHeroIds = [3] });

		Assert.All(game.Source().Read().Champions, champion => Assert.Empty(champion.Stats.ArenaLeague));
	}

	/// <summary>A league the static data holds no bonuses for is refused, not taken as none.</summary>
	[Fact]
	public void Refuses_a_league_the_static_data_does_not_hold()
	{
		using var game = WithClassic(SyntheticArenaData.Default.Classic with { DefenceTeam = new(30, [3]), DefenseHeroIds = [3] });

		Assert.Matches(
			@"^SharedModel\.Battle\.Core\.Setup\.TeamSetup\.ArenaLeagueId at 0x[0-9A-F]+ is Platinum, in the Classic Arena, which SharedModel\.Meta\.Battle\.Arena\.StaticStandardArenaData\.LeagueInfos holds 0 times, where raid-bridge expects once\.$",
			Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
	}

	/// <summary>A defence the game counts, but with no defence team setup to hold its league, is refused, not taken as no league.</summary>
	[Fact]
	public void Refuses_a_defence_with_no_team_setup()
	{
		using var game = WithClassic(SyntheticArenaData.Default.Classic with { DefenceTeam = null });

		Assert.Equal(
			"The Classic Arena's SharedModel.Meta.Battle.Arena.UserArenaData.DefenseHeroIds names a Champion, but its ArenaDefenseTeamSetup is null, so raid-bridge has no league to count.",
			Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
	}

	public void Dispose() => _game.Dispose();

	private static SyntheticGameClient WithClassic(SyntheticClassicArena classic) =>
		SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { Classic = classic });

	/// <summary>
	/// Each share of the Champion's Base stat, as the Fixed the synthetic static data holds: a BattleStats holds its
	/// values truncated, as the game's (Fixed)double makes them, where Q32 rounds.
	/// </summary>
	private static Dictionary<Stat, double> Shares(Champion champion, params (Stat Stat, double Share)[] shares) =>
		shares.ToDictionary(share => share.Stat, share => champion.Stats.Base[share.Stat] * ((long)(share.Share * Unit) / Unit));
}

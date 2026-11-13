using RaidBridge.Domain.Arena;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Arena;

namespace RaidBridge.Game.Tests.Arena;

/// <summary>
/// The Account's Live Arena, read end to end through the fake memory port: a synthetic Game client in, its league
/// worked out from its points as the game works it out, its points, wins and losses, or a named refusal, out
/// (https://github.com/ANicholasson/raid-bridge/issues/32). Nothing here looks at the reader's internals.
/// </summary>
public sealed class LiveArenaReadingTests : IDisposable
{
	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	[Fact]
	public void Reads_the_live_arena()
	{
		Assert.Equivalent(new LiveArena("SilverII", 1520, 14, 9), _game.Source().Read().LiveArena, strict: true);
	}

	/// <summary>An Account that has never fought in the Live Arena holds its starting 900 points, BronzeI's, as the main Account did live (2026-11-13).</summary>
	[Fact]
	public void Reads_a_live_arena_never_fought_in()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.None);

		Assert.Equivalent(new LiveArena("BronzeI", 900, 0, 0), game.Source().Read().LiveArena, strict: true);
	}

	/// <summary>A league's MinPoints is the first point of it, and one point fewer is the league before.</summary>
	[Theory]
	[InlineData(949, "BronzeI")]
	[InlineData(950, "BronzeII")]
	[InlineData(1299, "BronzeII")]
	[InlineData(1300, "SilverI")]
	[InlineData(4800, "GoldIV")]
	[InlineData(250000, "GoldIV")]
	public void Works_out_the_league_from_the_points(long points, string league)
	{
		using var game = Live(points);

		Assert.Equal(league, game.Source().Read().LiveArena.League);
	}

	/// <summary>The league comes from the static data's MinPoints, not from a table raid-bridge holds.</summary>
	[Fact]
	public void Takes_the_leagues_from_the_static_data()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { LiveLeagues = [(1, 900), (2, 1000), (11, 1520)] });

		Assert.Equal("SilverI", game.Source().Read().LiveArena.League);
	}

	/// <summary>The game walks the leagues in the order it holds them, so a league that needs no more points than the one before would be read differently by it.</summary>
	[Fact]
	public void Refuses_leagues_that_do_not_need_more_points_in_turn()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { LiveLeagues = [(1, 900), (11, 1300), (2, 950)] });

		RefusesMatching(game, @"^SharedModel\.Meta\.LiveArena\.LiveArenaLeagueInfo\.MinPoints at 0x[0-9A-F]+ is 950, for BronzeII, but the league before it in SharedModel\.Meta\.LiveArena\.StaticLiveArenaData\.LeagueInfoById needs 1300, where raid-bridge expects each league to need more points than the one before it\.$");
	}

	/// <summary>The game starts its walk at BronzeI, so points below every league's are BronzeI to it, and to raid-bridge.</summary>
	[Theory]
	[InlineData(899)]
	[InlineData(0)]
	public void Reads_points_below_every_league_as_bronze_i(long points)
	{
		using var game = Live(points);

		Assert.Equal("BronzeI", game.Source().Read().LiveArena.League);
	}

	[Fact]
	public void Refuses_negative_points()
	{
		using var game = Live(-1);

		RefusesMatching(game, @"^SharedModel\.Meta\.LiveArena\.UserLiveArenaData\.Points at 0x[0-9A-F]+ is -1, in the Live Arena, where raid-bridge expects 0 or more\.$");
	}

	[Theory]
	[InlineData(-1, 0, "Victories", -1)]
	[InlineData(0, -3, "Defeats", -3)]
	public void Refuses_a_negative_count(int wins, int losses, string field, int count)
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { Live = SyntheticArenaData.Default.Live with { Wins = wins, Losses = losses } });

		RefusesMatching(game, $@"^SharedModel\.Meta\.LiveArena\.UserLiveArenaData\.{field} at 0x[0-9A-F]+ is {count}, in the Live Arena, where raid-bridge expects 0 or more\.$");
	}

	[Fact]
	public void Refuses_a_league_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { LiveLeagues = [(1, 900), (99, 1000)] });

		RefusesMatching(game, @"^System\.Collections\.Generic\.Dictionary`2\+Entry\.key at 0x[0-9A-F]+ is 99, which isn't a member of the game's SharedModel\.Meta\.LiveArena\.LiveArenaLeagueId\.$");
	}

	[Fact]
	public void Refuses_live_arena_data_that_is_not_an_object()
	{
		_game.PointField("Client.Model.Guard.UserWrapper", "LiveArena", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^Client\.Model\.Guard\.UserWrapper\.LiveArena at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_static_live_arena_data_that_is_not_an_object()
	{
		_game.PointField("SharedModel.Meta.StaticData", "LiveArenaData", SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.StaticData\.LiveArenaData at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_a_live_arena_field_missing_from_the_metadata()
	{
		_game.UndeclareField("SharedModel.Meta.LiveArena.UserLiveArenaData", "Points");

		Refuses(_game, "The game's class Client.Model.Gameplay.LiveArena.UpdatableUserLiveArenaData has no field Points, base classes included.");
	}

	/// <summary>A game update that made the points anything but a long is refused rather than read as one.</summary>
	[Fact]
	public void Refuses_points_of_another_type()
	{
		_game.RetypeField("SharedModel.Meta.LiveArena.UserLiveArenaData", "Points", "System.Int32");

		RefusesMatching(_game, @"^The game's SharedModel\.Meta\.LiveArena\.UserLiveArenaData\.Points is a System\.Int32, where raid-bridge expects a System\.Int64\.$");
	}

	[Fact]
	public void Refuses_a_live_arena_class_missing_from_the_metadata()
	{
		_game.RenameClass("Client.Model.Gameplay.LiveArena.LiveArenaWrapper", "Client.Model.Gameplay.LiveArena.PvpWrapper");

		Refuses(_game, "The game's metadata has no class Client.Model.Gameplay.LiveArena.LiveArenaWrapper.");
	}

	public void Dispose() => _game.Dispose();

	private static SyntheticGameClient Live(long points) =>
		SyntheticGameClient.Create(arenaData: SyntheticArenaData.Default with { Live = SyntheticArenaData.Default.Live with { Points = points } });

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}

using RaidBridge.Domain.Artifacts;
using RaidBridge.Game.Testing;
using RaidBridge.Game.Testing.Artifacts;

namespace RaidBridge.Game.Tests.Artifacts;

/// <summary>
/// The Account's Artifacts, read end to end through the fake memory port: a synthetic Game client in, each Champion's
/// Artifacts and the Unworn Artifacts, or a named refusal, out (spec #20, #26, #33). Nothing here looks at the reader's
/// internals.
/// </summary>
public sealed class ArtifactReadingTests : IDisposable
{
	private const string Resolver = "SharedModel.Meta.Artifacts.ArtifactStorage.ArtifactStorageResolver";
	private const string Fixed = "Plarium.Common.Numerics.Fixed";

	private static readonly SyntheticArtifactData Artifacts = SyntheticArtifactData.Default;

	/// <summary>The knight's Artifacts, in the game's order of slots, its empty chest slot left out.</summary>
	private static readonly Artifact[] KnightWears =
	[
		new(102, "Helmet", "Hp", "Synthetic Life", "Five", "Epic", 12, 2, null, new("Health", 2120, true, 0, 0), new("Defence", Q32(0.04), false, 0, 0), []),
		new(101, "Weapon", "AttackSpeed", "Synthetic Speed", "Six", "Legendary", 16, null, null, new("Attack", 265, true, 0, 0), null, [new("Speed", 12, true, 3, 2), new("CriticalChance", Q32(0.06), false, 0, 1)]),
		new(103, "Ring", "Hp", "Synthetic Life", "Six", "Legendary", 8, null, "DarkElves", new("Health", 1640, true, 0, 0), null, [new("Attack", 22, true, 0, 0)]),
	];

	/// <summary>Plain boots, and a cloak locked to the High Elves with a Glyph on its Substat, by id.</summary>
	private static readonly Artifact[] WornByNobody =
	[
		new(104, "Boots", "AttackSpeed", "Synthetic Speed", "Six", "Legendary", 16, null, null, new("Speed", 45, true, 0, 0), null, []),
		new(105, "Cloak", "Hp", "Synthetic Life", "Five", "Epic", 4, null, "HighElves", new("Attack", 120, true, 0, 0), null, [new("Speed", 5, true, 2, 1)]),
	];

	/// <summary>The default Artifacts, with no Champion wearing any, so only the reading of Unworn Artifacts meets them.</summary>
	private static readonly SyntheticArtifactData NoneWorn = Artifacts with { SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>() };

	private readonly SyntheticGameClient _game = SyntheticGameClient.Create();

	/// <summary>
	/// The Artifacts come from where the storage resolver says, and the other place is left empty, so only the right
	/// one can give them. Set names come from a key's default text.
	/// </summary>
	[Theory]
	[InlineData(SyntheticArtifactStorage.AccountList)]
	[InlineData(SyntheticArtifactStorage.ExternalStorage)]
	public void Reads_the_artifacts_each_champion_wears_from_either_storage(SyntheticArtifactStorage storage)
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with { Storage = storage });

		var artifacts = game.Source().Read().Champions.ToDictionary(champion => champion.Id, champion => champion.Artifacts);

		Assert.Equivalent(KnightWears, artifacts[3], strict: true);
		Assert.Empty(artifacts[7]);
		Assert.Empty(artifacts[12]);
	}

	/// <summary>Whichever storage holds them, the Artifacts no Champion wears are read by the same code as the worn ones.</summary>
	[Theory]
	[InlineData(SyntheticArtifactStorage.AccountList)]
	[InlineData(SyntheticArtifactStorage.ExternalStorage)]
	public void Reads_the_artifacts_no_champion_wears_from_either_storage(SyntheticArtifactStorage storage)
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with { Storage = storage });

		Assert.Equivalent(WornByNobody, game.Source().Read().UnwornArtifacts, strict: true);
	}

	[Theory]
	[InlineData(SyntheticArtifactStorage.AccountList)]
	[InlineData(SyntheticArtifactStorage.ExternalStorage)]
	public void Reads_every_stored_artifact_exactly_once(SyntheticArtifactStorage storage)
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with { Storage = storage });

		var account = game.Source().Read();

		Assert.Equal(
			[101, 102, 103, 104, 105],
			account.Champions.SelectMany(champion => champion.Artifacts).Concat(account.UnwornArtifacts).Select(artifact => artifact.Id).Order());
	}

	[Fact]
	public void Reads_no_unworn_artifacts_when_every_artifact_is_worn()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with { Artifacts = [.. Artifacts.Artifacts.Where(artifact => artifact.Id < 104)] });

		Assert.Empty(game.Source().Read().UnwornArtifacts);
	}

	[Fact]
	public void Reads_every_artifact_as_unworn_when_no_champion_wears_one()
	{
		using var game = SyntheticGameClient.Create(artifactData: NoneWorn);

		Assert.Equal([101, 102, 103, 104, 105], game.Source().Read().UnwornArtifacts.Select(artifact => artifact.Id));
	}

	/// <summary>The secondary Account holds a Ring whose _setKindId is None: it is in no set (live check, #33).</summary>
	[Fact]
	public void Reads_an_artifact_in_no_set_as_having_none()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts.Select(artifact => artifact.Id == 105 ? artifact with { SetKind = 0 } : artifact)],
		});

		var cloak = game.Source().Read().UnwornArtifacts.Single(artifact => artifact.Id == 105);

		Assert.Equal((null, null), (cloak.SetId, cloak.SetName));
	}

	/// <summary>A Fixed is its raw long over 2^32, exactly: 0.06 is held as the nearest step, not as 0.06 (#26).</summary>
	[Fact]
	public void Writes_a_fixed_value_exactly_as_the_game_holds_it()
	{
		var weapon = _game.Source().Read().Champions.Single(champion => champion.Id == 3).Artifacts.Single(artifact => artifact.Slot == "Weapon");

		Assert.Equal(257698038 / 4294967296.0, weapon.Substats[1].Value);
	}

	/// <summary>The secondary Account wears Artifacts whose _secondaryBonuses is null: they have no Substats (live check, #26).</summary>
	[Fact]
	public void Reads_an_artifact_without_a_substat_list_as_having_none()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts.Select(artifact => artifact.Id == 101 ? artifact with { Secondary = null } : artifact)],
		});

		var weapon = game.Source().Read().Champions.Single(champion => champion.Id == 3).Artifacts.Single(artifact => artifact.Slot == "Weapon");

		Assert.Empty(weapon.Substats);
	}

	[Theory]
	[InlineData(SyntheticArtifactStorage.AccountList, @"SharedModel\.Meta\.Artifacts\.UserArtifactData\.Artifacts")]
	[InlineData(SyntheticArtifactStorage.ExternalStorage, @"Client\.Model\.Gameplay\.Artifacts\.CachedArtifacts\._artifacts")]
	public void Refuses_a_slot_holding_an_artifact_the_account_lacks(SyntheticArtifactStorage storage, string where)
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Storage = storage,
			SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>> { [3] = new Dictionary<int, int> { [5] = 999 } },
		});

		RefusesMatching(game, $@"^Champion 3 wears Artifact 999 in its Weapon slot, but {where} has no Artifact 999\.$");
	}

	/// <summary>A value of a dictionary is named by the dictionary and its key (#38).</summary>
	[Fact]
	public void Refuses_a_negative_artifact_id_in_a_slot_naming_the_slot()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>> { [3] = new Dictionary<int, int> { [5] = -4 } },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.HeroArtifactData\.ArtifactIdByKind\[Weapon\] at 0x[0-9A-F]+ is -4, where raid-bridge expects an Artifact id, or 0 for an empty slot\.$");
	}

	[Fact]
	public void Refuses_an_artifact_storage_it_does_not_know()
	{
		_game.PointField(Resolver, "_implementation", _game.Target("Client.Model.Guard.UserWrapper", "Artifacts"));

		RefusesMatching(
			_game,
			@"^SharedModel\.Meta\.Artifacts\.ArtifactStorage\.ArtifactStorageResolver\._implementation points to a Client\.Model\.Gameplay\.Artifacts\.EquipmentWrapper at 0x[0-9A-F]+, which raid-bridge doesn't know as a place the game keeps Artifacts\.$");
	}

	/// <summary>Until the game's code first uses a class, its TypeInfo slot holds the encoded reference the binary shipped with.</summary>
	[Fact]
	public void Refuses_a_class_the_game_client_has_not_set_up()
	{
		_game.SetTypeInfoSlot(Resolver, 0x2001_0A13);

		RefusesMatching(
			_game,
			@"^The Game client hasn't set up SharedModel\.Meta\.Artifacts\.ArtifactStorage\.ArtifactStorageResolver yet: its TypeInfo slot at 0x[0-9A-F]+ holds 0x20010A13, not a runtime class\.$");
	}

	[Fact]
	public void Refuses_a_type_info_slot_holding_another_class()
	{
		_game.SetTypeInfoSlot(Fixed, _game.RuntimeClass("SharedModel.Meta.Artifacts.Artifact"));

		RefusesMatching(
			_game,
			@"^Plarium\.Common\.Numerics\.Fixed's TypeInfo slot at 0x[0-9A-F]+ holds the runtime class SharedModel\.Meta\.Artifacts\.Artifact at 0x[0-9A-F]+: the TypeInfo slots of this Game client aren't where its files say\.$");
	}

	[Fact]
	public void Refuses_an_artifact_worn_in_another_slot_than_its_own()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>> { [3] = new Dictionary<int, int> { [5] = 102 } },
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._kindId at 0x[0-9A-F]+ is Helmet, where raid-bridge expects the Weapon slot Champion 3 wears it in\.$");
	}

	[Fact]
	public void Refuses_artifacts_worn_by_a_champion_the_account_lacks()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>> { [99] = new Dictionary<int, int> { [5] = 101 } },
		});

		Refuses(game, "SharedModel.Meta.Artifacts.UserArtifactData.ArtifactDataByHeroId gives Artifacts to Champion 99, which HeroById doesn't hold.");
	}

	[Fact]
	public void Refuses_an_artifact_whose_set_the_static_data_lacks()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts.Select(artifact => artifact.Id == 101 ? artifact with { SetKind = 2 } : artifact)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._setKindId at 0x[0-9A-F]+ is AttackPower, which isn't a set in the game's StaticArtifactData\.SetInfos\.$");
	}

	[Fact]
	public void Refuses_an_artifact_id_the_account_list_holds_twice()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts, new SyntheticArtifact(Id: 101, Kind: 5)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._id at 0x[0-9A-F]+ is 101, an id SharedModel\.Meta\.Artifacts\.UserArtifactData\.Artifacts holds twice\.$");
	}

	/// <summary>One Artifact under two Champions would be written twice (#33).</summary>
	[Fact]
	public void Refuses_an_artifact_worn_by_two_champions()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			SlotsByHeroId = new Dictionary<int, IReadOnlyDictionary<int, int>>
			{
				[7] = new Dictionary<int, int> { [5] = 101 },
				[3] = new Dictionary<int, int> { [5] = 101 },
			},
		});

		Refuses(game, "SharedModel.Meta.Artifacts.UserArtifactData.ArtifactDataByHeroId gives Artifact 101 to both Champion 3 and Champion 7.");
	}

	[Fact]
	public void Refuses_an_unworn_artifact_whose_slot_the_game_does_not_name()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts.Select(artifact => artifact.Id == 105 ? artifact with { Kind = 42 } : artifact)],
		});

		RefusesMatching(game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._kindId at 0x[0-9A-F]+ is 42, which isn't a member of the game's SharedModel\.Meta\.Artifacts\.ArtifactKindId\.$");
	}

	[Fact]
	public void Refuses_an_unworn_artifact_whose_main_stat_is_not_an_object()
	{
		_game.PointPrimaryBonus(105, SyntheticGameClient.Unmapped);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._primaryBonus at 0x[0-9A-F]+ points to 0xDEAD00000000, which isn't an object\. ");
	}

	[Fact]
	public void Refuses_an_unworn_artifact_whose_substat_list_runs_past_its_array()
	{
		_game.SetSubstatCount(105, 9);

		RefusesMatching(_game, @"^SharedModel\.Meta\.Artifacts\.Artifact\._secondaryBonuses at 0x[0-9A-F]+ is malformed: its _size is 9, but its array holds 2\.$");
	}

	[Fact]
	public void Refuses_an_artifact_field_missing_from_the_metadata()
	{
		using var game = SyntheticGameClient.Create(artifactData: NoneWorn);
		game.UndeclareField("SharedModel.Meta.Artifacts.Artifact", "_requiredFraction");

		Refuses(game, "The game's class SharedModel.Meta.Artifacts.Artifact has no field _requiredFraction, base classes included.");
	}

	[Fact]
	public void Refuses_an_artifact_cache_class_missing_from_the_metadata()
	{
		using var game = SyntheticGameClient.Create(artifactData: NoneWorn with { Storage = SyntheticArtifactStorage.ExternalStorage });
		game.RenameClass("Client.Model.Gameplay.Artifacts.CachedArtifacts", "Client.Model.Gameplay.Artifacts.ArtifactCache");

		Refuses(game, "The game's metadata has no class Client.Model.Gameplay.Artifacts.CachedArtifacts.");
	}

	/// <summary>Only a power of two keeps the scaling exact.</summary>
	[Fact]
	public void Refuses_a_fixed_scale_that_is_not_a_power_of_two()
	{
		_game.SetConstant(Fixed, "ONE", 1000);

		Refuses(_game, "The game's Plarium.Common.Numerics.Fixed.ONE is 1000, where raid-bridge expects a power of two.");
	}

	/// <summary>A raw value past 2^53 would be rounded on its way into a double, so it is refused rather than written wrong.</summary>
	[Fact]
	public void Refuses_a_fixed_value_it_cannot_write_exactly()
	{
		using var game = SyntheticGameClient.Create(artifactData: Artifacts with
		{
			Artifacts = [.. Artifacts.Artifacts.Select(artifact => artifact.Id == 101 ? artifact with { Primary = new(Stat: 2, Value: 1L << 60, Absolute: true) } : artifact)],
		});

		RefusesMatching(
			game,
			@"^SharedModel\.Meta\.Artifacts\.BonusValue\._value holds a Plarium\.Common\.Numerics\.Fixed whose Plarium\.Common\.Numerics\.Fixed\.m_rawValue at 0x[0-9A-F]+ is 1152921504606846976, which raid-bridge can't write exactly\.$");
	}

	public void Dispose() => _game.Dispose();

	/// <summary>The value a Fixed holds for <paramref name="value"/>.</summary>
	private static double Q32(double value) => SyntheticArtifactData.Q32(value) / 4294967296.0;

	private static void Refuses(SyntheticGameClient game, string message) =>
		Assert.Equal(message, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);

	private static void RefusesMatching(SyntheticGameClient game, string pattern) =>
		Assert.Matches(pattern, Assert.ThrowsAny<GameClientException>(() => game.Source().Read()).Message);
}

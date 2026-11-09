namespace RaidBridge.Game.Testing.FactionGuardians;

/// <summary>
/// The Faction Guardians a synthetic Game client holds, in the game's own terms: each Faction's guardian slots by its
/// HeroFraction, then by HeroRarity, each a list of slots. Keys hold the game's numbers, such as 11 for DarkElves, and
/// are laid out in the order given here.
/// </summary>
public sealed record SyntheticGuardianData(IReadOnlyDictionary<int, IReadOnlyDictionary<int, IReadOnlyList<SyntheticGuardianSlot>>> SlotsByFaction)
{
	/// <summary>
	/// DarkElves laid out before BannerLords, so only the reader can put them in the game's order. BannerLords' Rare slots
	/// hold the default Champions 7 and 12 in the first and nothing in the second; DarkElves' Legendary slots hold nothing
	/// in the first and Champion 3 in the second position of the second; HighElves have an Epic list with no slots.
	/// </summary>
	public static SyntheticGuardianData Default { get; } = new(new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<SyntheticGuardianSlot>>>
	{
		[11] = new Dictionary<int, IReadOnlyList<SyntheticGuardianSlot>> { [5] = [new(null, null), new(null, 3)] },
		[1] = new Dictionary<int, IReadOnlyList<SyntheticGuardianSlot>> { [3] = [new(7, 12), new(null, null)] },
		[2] = new Dictionary<int, IReadOnlyList<SyntheticGuardianSlot>> { [4] = [] },
	});

	/// <summary>No Faction Guardians: the player hasn't placed a Champion, and every slot the game holds is empty.</summary>
	public static SyntheticGuardianData None { get; } = new(new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<SyntheticGuardianSlot>>>
	{
		[1] = new Dictionary<int, IReadOnlyList<SyntheticGuardianSlot>> { [3] = [new(null, null)], [5] = [] },
	});

	/// <summary>One Faction's slots of one rarity, and nothing else.</summary>
	public static SyntheticGuardianData Only(int faction, int rarity, IReadOnlyList<SyntheticGuardianSlot> slots) =>
		new(new Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<SyntheticGuardianSlot>>>
		{
			[faction] = new Dictionary<int, IReadOnlyList<SyntheticGuardianSlot>> { [rarity] = slots },
		});
}

/// <summary>A guardian slot, as SharedModel.Meta.Academy.User.AcademyGuardiansSlot holds it: the hero id in each position, or null for none.</summary>
public sealed record SyntheticGuardianSlot(int? FirstHero, int? SecondHero);

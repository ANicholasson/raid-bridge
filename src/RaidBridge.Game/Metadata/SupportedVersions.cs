using RaidBridge.Game.Il2Cpp;

namespace RaidBridge.Game.Metadata;

/// <summary>
/// The only game builds the reader knows. Any other is refused by name rather than read wrongly (spec #20; ADR-0003:
/// exact Unity version, not a 6000.3 range).
/// </summary>
internal static class SupportedVersions
{
	public const int Metadata = 39;

	/// <summary>The Unity versions with an IL2CPP runtime layout, so the two lists can't drift apart (code review, #23).</summary>
	public static IReadOnlyCollection<string> Unity => Il2CppLayout.UnityVersions;
}

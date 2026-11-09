using System.Runtime.Versioning;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Linux.Memory;

/// <summary>
/// Who raid-bridge runs as. It reads the Game client with the permission any program the player runs already has in
/// Steam's container (#13), so it refuses to start with more: as root, or with any effective capability.
/// </summary>
internal sealed record LinuxUser(uint Id, uint EffectiveId, ulong EffectiveCapabilities)
{
	[SupportedOSPlatform("linux")]
	public static LinuxUser Current(ProcFileSystem proc) =>
		new(Libc.GetUserId(), Libc.GetEffectiveUserId(), proc.OwnStatus().EffectiveCapabilities);

	public void RefuseMoreThanThePlayersOwnRights()
	{
		if (Id == 0 || EffectiveId == 0)
		{
			throw new GameMemoryException("raid-bridge refuses to run as root: it reads the Game client with your own user's rights and needs nothing more. Run it as the user who plays RAID.");
		}

		if (EffectiveCapabilities != 0)
		{
			throw new GameMemoryException($"raid-bridge refuses to run with capabilities (CapEff {EffectiveCapabilities:x16}): it reads the Game client with your own user's rights and needs nothing more. Run it as the user who plays RAID.");
		}
	}
}

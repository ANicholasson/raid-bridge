using RaidBridge.Cli.Snapshots;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Export.Accounts;
using RaidBridge.Game.Linux.Memory;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Snapshots;

namespace RaidBridge.Cli;

/// <summary>The composition root: the one place that picks the adapters, wired by hand (ADR-0004).</summary>
internal static class Program
{
	private static int Main(string[] args) =>
		SnapshotCommand.Run(
			args,
			OpenGame,
			SaltFile.InConfigDirectory(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
			TimeProvider.System,
			Console.Out,
			Console.Error,
			Environment.CurrentDirectory);

	/// <summary>
	/// The reader over the running Game client, with the --game-dir folder if any. Only Linux has a memory adapter so
	/// far (ADR-0002).
	/// </summary>
	internal static ISnapshotSource OpenGame(string? gameDirectory) =>
		OperatingSystem.IsLinux()
			? new GameSnapshotSource(LinuxGameMemory.Open(), gameDirectory)
			: throw new GameMemoryException("There is no memory adapter for this platform yet, so the Game client can't be read.");
}

using RaidBridge.Cli.Snapshots;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Export.Accounts;
using RaidBridge.Game.Linux.Memory;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Snapshots;
using RaidBridge.Game.Windows.Memory;

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
	/// The reader over the running Game client, with the --game-dir folder if any, through this platform's memory
	/// adapter (ADR-0002). Linux and Windows have one so far.
	/// </summary>
	internal static ISnapshotSource OpenGame(string? gameDirectory) =>
		new GameSnapshotSource(OpenMemory(), gameDirectory);

	private static IGameMemory OpenMemory() =>
		OperatingSystem.IsLinux() ? LinuxGameMemory.Open()
		: OperatingSystem.IsWindows() ? WindowsGameMemory.Open()
		: throw new GameMemoryException("There is no memory adapter for this platform yet, so the Game client can't be read.");
}

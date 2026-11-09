using System.Reflection;
using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Export.Snapshots;
using RaidBridge.Game;

namespace RaidBridge.Cli.Snapshots;

/// <summary>
/// <c>raid-bridge snapshot [--out PATH] [--game-dir DIR]</c>: takes one Snapshot, writes it to a new file and prints
/// the file's path. A failure is one line naming the cause, with exit code 1, or 2 for a bad command line.
/// </summary>
internal static class SnapshotCommand
{
	public const string Usage = "usage: raid-bridge snapshot [--out PATH] [--game-dir DIR]";

	private static readonly Generator Generator = new(
		"raid-bridge",
		typeof(SnapshotCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? throw new InvalidOperationException("raid-bridge's assembly has no informational version."));

	/// <summary>Runs the command. <paramref name="openGame"/> opens the reader over the running Game client, given the --game-dir folder if any.</summary>
	public static int Run(
		IReadOnlyList<string> args,
		Func<string?, ISnapshotSource> openGame,
		ISaltStore salts,
		TimeProvider clock,
		TextWriter output,
		TextWriter error,
		string workingDirectory)
	{
		if (args is ["--help" or "-h"] or ["snapshot", "--help" or "-h"])
		{
			output.WriteLine(Usage);
			return 0;
		}

		var (options, problem) = Options.Parse(args);
		if (options is null)
		{
			error.WriteLine($"raid-bridge: {problem}; {Usage}");
			return 2;
		}

		try
		{
			var gameDirectory = options.GameDirectory is { } directory ? Path.GetFullPath(directory, workingDirectory) : null;
			var snapshot = new TakeSnapshot(openGame(gameDirectory), salts, clock).Run();
			var path = Path.GetFullPath(options.Out ?? SnapshotFile.DefaultName(snapshot.CapturedAt), workingDirectory);
			output.WriteLine(SnapshotFile.Write(snapshot, Generator, path));
			return 0;
		}
		catch (Exception exception) when (exception is GameClientException or IOException or UnauthorizedAccessException or InvalidDataException)
		{
			error.WriteLine($"raid-bridge: {exception.Message.ReplaceLineEndings(" ")}");
			return 1;
		}
	}

	private sealed record Options(string? Out, string? GameDirectory)
	{
		/// <summary>The options, or what is wrong with the command line.</summary>
		public static (Options? Options, string? Problem) Parse(IReadOnlyList<string> args)
		{
			if (args.Count == 0 || args[0] != "snapshot")
			{
				return (null, args.Count == 0 ? "no command" : $"unknown command {args[0]}");
			}

			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			for (var index = 1; index < args.Count; index += 2)
			{
				var option = args[index];
				if (option is not ("--out" or "--game-dir"))
				{
					return (null, $"unknown option {option}");
				}

				// A forgotten value is never taken from the next option (code review, #23).
				if (index + 1 >= args.Count || args[index + 1].Length == 0 || args[index + 1].StartsWith("--", StringComparison.Ordinal))
				{
					return (null, $"{option} needs a value");
				}

				if (!values.TryAdd(option, args[index + 1]))
				{
					return (null, $"{option} is given twice");
				}
			}

			return (new(values.GetValueOrDefault("--out"), values.GetValueOrDefault("--game-dir")), null);
		}
	}
}

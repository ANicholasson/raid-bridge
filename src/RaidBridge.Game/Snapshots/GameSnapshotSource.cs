using System.Reflection.PortableExecutable;
using RaidBridge.Domain.Accounts;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Game.Accounts;
using RaidBridge.Game.Il2Cpp;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Snapshots;

/// <summary>
/// Reads the Account from the running Game client through a memory adapter, with the installed game's files to say
/// where everything is. Nothing is cached: every read resolves everything afresh (spec #20).
/// </summary>
public sealed class GameSnapshotSource : ISnapshotSource
{
	/// <summary>Far more than any PE file's headers take, so a corrupt SizeOfHeaders ends with an error, not a huge read.</summary>
	private const int MaxHeaders = 1 << 16;

	private readonly IGameMemory _memory;
	private readonly string? _gameDirectory;
	private readonly Func<GameFiles, IGameMetadata> _loadMetadata;

	/// <param name="memory">The running Game client.</param>
	/// <param name="gameDirectory">The folder holding Raid.exe, or null for the folder the Game client loaded GameAssembly.dll from.</param>
	public GameSnapshotSource(IGameMemory memory, string? gameDirectory)
		: this(memory, gameDirectory, GameMetadata.Load)
	{
	}

	internal GameSnapshotSource(IGameMemory memory, string? gameDirectory, Func<GameFiles, IGameMetadata> loadMetadata)
	{
		_memory = memory;
		_gameDirectory = gameDirectory;
		_loadMetadata = loadMetadata;
	}

	public Account Read()
	{
		var files = GameFiles.In(_gameDirectory ?? LoadedFrom(_memory.GameAssembly));
		RefuseFilesOfAnotherBuild(files);
		var metadata = _loadMetadata(files);
		// Again, so an update between the check and the parse can't slip through (code review, #38).
		RefuseFilesOfAnotherBuild(files);
		return AccountReader.Read(new Il2CppRuntime(_memory, metadata));
	}

	private static string LoadedFrom(GameModule module) =>
		Path.GetDirectoryName(module.FilePath) is { Length: > 0 } directory
			? directory
			: throw new GameMemoryException($"The Game client loaded GameAssembly.dll from {module.FilePath}, which has no folder.");

	/// <summary>
	/// The files must be the build the running Game client is, or the read would mix two builds (#38).
	/// GameAssembly.dll's headers on disk must be the headers of the image the Game client loaded, apart from the image
	/// base, which the loader rewrites when it moves the image. That catches an update whatever it does to the file's
	/// time, and a game folder holding another build. The other files have no copy in memory to compare with, so none of
	/// them may have changed after the Game client started (code review, #23).
	/// </summary>
	private void RefuseFilesOfAnotherBuild(GameFiles files)
	{
		var (path, changedAt) = new[] { files.GlobalMetadata, files.DataBundle, files.UnityPlayer }
			.Select(path => (Path: path, ChangedAt: new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)))
			.MaxBy(file => file.ChangedAt);
		if (changedAt > _memory.StartedAt)
		{
			throw new GameMetadataException(
				$"{path} changed at {changedAt:u}, after the Game client started at {_memory.StartedAt.ToUniversalTime():u}: the game updated under the running Game client. Restart the Game client and try again.");
		}

		var (onDisk, imageBase) = Headers(files.GameAssembly);
		var loaded = new byte[onDisk.Length];
		_memory.Read(_memory.GameAssembly.ImageBase, loaded);
		onDisk.AsSpan(imageBase).Clear();
		loaded.AsSpan(imageBase).Clear();

		var same = onDisk.AsSpan().CommonPrefixLength(loaded);
		if (same < onDisk.Length)
		{
			throw new GameMetadataException(
				$"{files.GameAssembly} isn't the build the Game client is running: its headers differ from those of the image it loaded from {_memory.GameAssembly.FilePath}, from byte 0x{same:X}. If the game updated, restart the Game client; if a game folder was given, give the running game's. Then try again.");
		}
	}

	/// <summary>A PE file's headers, as many bytes as its SizeOfHeaders says, and where among them it keeps its image base.</summary>
	private static (byte[] Headers, Range ImageBase) Headers(string path)
	{
		try
		{
			using var file = File.OpenRead(path);
			var headers = new PEHeaders(file);
			var optional = headers.PEHeader ?? throw new BadImageFormatException("It has no optional header.");
			// IMAGE_OPTIONAL_HEADER keeps ImageBase 24 bytes in as a ulong in PE32+, 28 bytes in as a uint in PE32.
			var imageBase = (headers.PEHeaderStartOffset + (optional.Magic == PEMagic.PE32Plus ? 24 : 28))..(headers.PEHeaderStartOffset + 32);
			// SizeOfHeaders must take in the image base, which a corrupt one might not (code review, #38).
			if (optional.SizeOfHeaders < imageBase.End.Value || optional.SizeOfHeaders > MaxHeaders)
			{
				throw new BadImageFormatException($"Its SizeOfHeaders is {optional.SizeOfHeaders}.");
			}

			var bytes = new byte[optional.SizeOfHeaders];
			file.Position = 0;
			file.ReadExactly(bytes);
			return (bytes, imageBase);
		}
		catch (Exception exception) when (exception is BadImageFormatException or EndOfStreamException)
		{
			throw new GameMetadataException($"{path} isn't a PE image: {exception.Message}", exception);
		}
	}
}

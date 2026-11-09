using System.Security.Cryptography;
using RaidBridge.Domain.Accounts;

namespace RaidBridge.Export.Accounts;

/// <summary>
/// The player's salt in a file only they can read: 32 random bytes made on the first run, at
/// <c>$XDG_CONFIG_HOME/raid-bridge/salt</c> (<c>~/.config</c> when unset), mode 0600 (spec #20). Losing the file
/// changes every Account id hash, so it is never replaced; its bytes are never printed. An existing salt that other
/// users may read, or that sits in a folder they may change, is refused, as ssh refuses such a private key (#38).
/// </summary>
public sealed class SaltFile(string path) : ISaltStore
{
	private const UnixFileMode OthersMayAccess =
		UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
		| UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

	private const UnixFileMode OthersMayWrite = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

	public string Path { get; } = path;

	/// <summary>The salt file's place, from the XDG base directory rules: a relative or empty XDG_CONFIG_HOME is ignored.</summary>
	public static SaltFile InConfigDirectory(string? xdgConfigHome, string homeDirectory)
	{
		var configHome = xdgConfigHome is { Length: > 0 } && System.IO.Path.IsPathFullyQualified(xdgConfigHome)
			? xdgConfigHome
			: System.IO.Path.Combine(homeDirectory, ".config");
		return new(System.IO.Path.Combine(configHome, "raid-bridge", "salt"));
	}

	public Salt LoadOrCreate()
	{
		// A relative HOME would give each working folder its own salt, and so the same Account its own hashes (code review, #23).
		if (!System.IO.Path.IsPathFullyQualified(Path))
		{
			throw new InvalidDataException($"The salt file's place, {Path}, isn't an absolute path: set HOME or XDG_CONFIG_HOME to an absolute folder.");
		}

		if (File.Exists(Path))
		{
			return Load();
		}

		var bytes = RandomNumberGenerator.GetBytes(Salt.Length);
		var directory = System.IO.Path.GetDirectoryName(Path)!;
		if (OperatingSystem.IsWindows())
		{
			Directory.CreateDirectory(directory);
		}
		else
		{
			Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}

		try
		{
			NewFile.Write(Path, bytes, ownerOnly: true);
		}
		catch (NewFile.AlreadyExistsException)
		{
			// Another run made it first; that salt is the one.
			return Load();
		}

		return new(bytes);
	}

	private Salt Load()
	{
		if (!OperatingSystem.IsWindows())
		{
			if (File.GetUnixFileMode(Path) is var mode && (mode & OthersMayAccess) != 0)
			{
				throw new InvalidDataException(
					$"{Path} has mode {Octal(mode)}, so other users may read or change the salt. Make it the player's only with chmod 600 {Path}, then try again.");
			}

			// In a folder others may write to, they could put their own salt in its place (code review, #38).
			var directory = System.IO.Path.GetDirectoryName(Path)!;
			if (File.GetUnixFileMode(directory) is var folderMode && (folderMode & OthersMayWrite) != 0)
			{
				throw new InvalidDataException(
					$"{directory} has mode {Octal(folderMode)}, so other users may replace the salt in it. Make it the player's only with chmod go-w {directory}, then try again.");
			}
		}

		var bytes = File.ReadAllBytes(Path);
		return bytes.Length == Salt.Length
			? new(bytes)
			: throw new InvalidDataException(
				$"{Path} isn't a raid-bridge salt: it holds {bytes.Length} bytes, not {Salt.Length}. raid-bridge won't replace it, because every Account id hash depends on it.");
	}

	private static string Octal(UnixFileMode mode) => Convert.ToString((int)mode & 0x1FF, 8).PadLeft(4, '0');
}

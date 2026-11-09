namespace RaidBridge.Export;

/// <summary>
/// Writes a file that must not exist yet, whole or not at all: the bytes go to a temporary file beside it, which is
/// then moved into place without overwriting. A crash, a full disk or a second run can't leave a partial file, and
/// never an existing one replaced (code review, #23).
/// </summary>
internal static class NewFile
{
	private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

	/// <summary>Throws <see cref="AlreadyExistsException"/> when the path is taken, and leaves nothing behind on any failure.</summary>
	public static void Write(string path, ReadOnlySpan<byte> bytes, bool ownerOnly)
	{
		var directory = Path.GetDirectoryName(path);
		if (!Directory.Exists(directory))
		{
			throw new DirectoryNotFoundException($"{directory} doesn't exist, so {path} can't be written.");
		}

		var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
		try
		{
			var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
			if (ownerOnly && !OperatingSystem.IsWindows())
			{
				options.UnixCreateMode = OwnerOnly;
			}

			using (var file = new FileStream(temporary, options))
			{
				file.Write(bytes);
				file.Flush(flushToDisk: true);
			}

			File.Move(temporary, path, overwrite: false);
		}
		catch (IOException exception) when (File.Exists(path) || Directory.Exists(path))
		{
			throw new AlreadyExistsException(path, exception);
		}
		finally
		{
			if (File.Exists(temporary))
			{
				File.Delete(temporary);
			}
		}
	}

	public sealed class AlreadyExistsException(string path, Exception innerException)
		: IOException($"{path} already exists.", innerException);
}

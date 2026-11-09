namespace RaidBridge.Guardrails.Tests;

/// <summary>Locates the checkout these tests run in, so the guardrails read the real tree.</summary>
public static class Repository
{
	public static DirectoryInfo Root { get; } = FindRoot();

	/// <summary>
	/// Every file that can put code in a build: C# sources and CsWin32's NativeMethods.txt, anywhere in the tree.
	/// Only this project is left out, because its lists name what they forbid.
	/// </summary>
	public static IEnumerable<FileInfo> CodeFiles() =>
		Files("*.cs").Concat(Files("NativeMethods.txt")).Where(file => !IsInside(file, GuardrailsProject));

	public static IEnumerable<FileInfo> Files(string searchPattern) =>
		Root.EnumerateFiles(searchPattern, SearchOption.AllDirectories).Where(file => !IsBuildOutputOrGit(file));

	public static string RelativePath(FileInfo file) => Path.GetRelativePath(Root.FullName, file.FullName);

	private static readonly string GuardrailsProject = Path.Combine("tests", "RaidBridge.Guardrails.Tests");

	private static bool IsInside(FileInfo file, string relativeDirectory) =>
		RelativePath(file).StartsWith(relativeDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);

	private static bool IsBuildOutputOrGit(FileInfo file) =>
		RelativePath(file).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".git");

	private static DirectoryInfo FindRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "RaidBridge.slnx")))
			{
				return directory;
			}
		}

		throw new InvalidOperationException($"No RaidBridge.slnx above {AppContext.BaseDirectory}.");
	}
}

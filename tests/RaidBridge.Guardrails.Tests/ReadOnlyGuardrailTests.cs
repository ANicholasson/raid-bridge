namespace RaidBridge.Guardrails.Tests;

/// <summary>ADR-0001 enforced on the real tree: the code reads the game and nothing more.</summary>
public class ReadOnlyGuardrailTests
{
	private static readonly string[] BuildFilePatterns = ["*.csproj", "*.props", "*.targets"];

	[Fact]
	public void No_code_names_a_forbidden_api()
	{
		var violations = Repository.CodeFiles()
			.SelectMany(file => ForbiddenApis.FindIn(File.ReadAllText(file.FullName))
				.Select(hit => $"{Repository.RelativePath(file)}: {hit}"))
			.ToList();

		Assert.Empty(violations);
	}

	[Fact]
	public void Every_native_import_is_on_the_allowlist()
	{
		var violations = Repository.CodeFiles()
			.SelectMany(file => (file.Name == "NativeMethods.txt" ? NativeImports.FindInNativeMethods(File.ReadAllText(file.FullName)) : NativeImports.FindIn(File.ReadAllText(file.FullName)))
				.Select(hit => $"{Repository.RelativePath(file)}: {hit}"))
			.ToList();

		Assert.Empty(violations);
	}

	/// <summary>The allowlist sees the Linux adapter's own imports, so passing it means "allowed", not "blind".</summary>
	[Fact]
	public void The_allowlist_sees_every_import_the_linux_adapter_declares()
	{
		var declared = Repository.CodeFiles()
			.Where(file => Repository.RelativePath(file).StartsWith(Path.Combine("src", "RaidBridge.Game.Linux") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			.SelectMany(file => NativeImports.Declared(File.ReadAllText(file.FullName)))
			.Order(StringComparer.Ordinal)
			.ToList();

		Assert.Equal(["geteuid", "getuid", "process_vm_readv"], declared);
	}

	/// <summary>
	/// The /proc paths live in the Linux adapter, which takes its root as a parameter, so the adapter writes no file at
	/// all rather than relying on each file naming /proc (code review, #24).
	/// </summary>
	[Fact]
	public void The_linux_adapter_writes_no_file()
	{
		var adapter = Path.Combine("src", "RaidBridge.Game.Linux") + Path.DirectorySeparatorChar;
		var violations = Repository.CodeFiles()
			.Where(file => Repository.RelativePath(file).StartsWith(adapter, StringComparison.Ordinal))
			.SelectMany(file => ForbiddenApis.FindWrites(File.ReadAllText(file.FullName)).Select(hit => $"{Repository.RelativePath(file)}: {hit}"))
			.ToList();

		Assert.Empty(violations);
	}

	[Fact]
	public void No_build_file_references_an_injecting_package()
	{
		var violations = BuildFilePatterns
			.SelectMany(Repository.Files)
			.SelectMany(file => ForbiddenPackages.FindInMsBuild(File.ReadAllText(file.FullName))
				.Select(id => $"{Repository.RelativePath(file)}: {id}"))
			.ToList();

		Assert.Empty(violations);
	}

	[Fact]
	public void No_lock_file_resolves_an_injecting_package()
	{
		var lockFiles = Repository.Files("packages.lock.json").ToList();

		var violations = lockFiles
			.SelectMany(file => ForbiddenPackages.FindInLockFile(File.ReadAllText(file.FullName))
				.Select(id => $"{Repository.RelativePath(file)}: {id}"))
			.ToList();

		Assert.NotEmpty(lockFiles);
		Assert.Empty(violations);
	}
}

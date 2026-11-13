using System.Text.RegularExpressions;

namespace RaidBridge.Guardrails.Tests;

/// <summary>ADR-0001 enforced on the real tree: the code reads the game and nothing more.</summary>
public class ReadOnlyGuardrailTests
{
	private static readonly string[] BuildFilePatterns = ["*.csproj", "*.props", "*.targets"];

	/// <summary>The Win32 value of each right an OpenProcess call may ask for (ForbiddenApis.AllowedOpenProcessRights), as the adapter spells it.</summary>
	private static readonly Dictionary<string, string> OpenProcessRights = new()
	{
		["PROCESS_VM_READ"] = "0x0010",
		["PROCESS_QUERY_LIMITED_INFORMATION"] = "0x1000",
	};

	// Whatever is assigned, so a value spelled any other way fails rather than going unseen.
	private static readonly Regex OpenProcessRightDefinition = new(
		@"\b(?<name>PROCESS_VM_READ|PROCESS_QUERY_LIMITED_INFORMATION)\s*=(?!=)\s*(?<value>[^;,}\r\n]+)",
		RegexOptions.CultureInvariant);

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

	/// <summary>The allowlist sees the Windows adapter's own imports too.</summary>
	[Fact]
	public void The_allowlist_sees_every_import_the_windows_adapter_declares()
	{
		var declared = Repository.CodeFiles()
			.Where(file => Repository.RelativePath(file).StartsWith(Path.Combine("src", "RaidBridge.Game.Windows") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			.SelectMany(file => NativeImports.Declared(File.ReadAllText(file.FullName)))
			.Order(StringComparer.Ordinal)
			.ToList();

		Assert.Equal(["GetExitCodeProcess", "GetProcessTimes", "K32EnumProcessModulesEx", "OpenProcess", "QueryFullProcessImageNameW", "ReadProcessMemory"], declared);
	}

	/// <summary>
	/// The OpenProcess check reads the rights by name, so a name must carry its Win32 value: PROCESS_VM_READ defined as
	/// all access would otherwise pass it.
	/// </summary>
	[Fact]
	public void Each_allowed_open_process_right_carries_its_win32_value()
	{
		var definitions = Repository.CodeFiles()
			.SelectMany(file => OpenProcessRightDefinition.Matches(File.ReadAllText(file.FullName))
				.Select(match => (Path: Repository.RelativePath(file), Name: match.Groups["name"].Value, Value: match.Groups["value"].Value.Trim())))
			.ToList();

		Assert.Contains(definitions, definition => definition.Name == "PROCESS_VM_READ");
		Assert.All(definitions, definition => Assert.Equal($"{definition.Path}: {definition.Name} = {OpenProcessRights[definition.Name]}", $"{definition.Path}: {definition.Name} = {definition.Value}"));
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

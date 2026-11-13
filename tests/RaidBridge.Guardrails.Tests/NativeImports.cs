using System.Text.RegularExpressions;

namespace RaidBridge.Guardrails.Tests;

/// <summary>
/// The native functions raid-bridge may call (spec #20): process_vm_readv to read the Game client, and getuid and
/// geteuid to know who it runs as. Every P/Invoke declaration and CsWin32 NativeMethods.txt entry must name one, and
/// nothing may look up a native function at run time, which would get round the list. Extend it, never trim it.
/// </summary>
public static class NativeImports
{
	public static readonly IReadOnlyList<string> Allowed = ["process_vm_readv", "getuid", "geteuid"];

	/// <summary>Ways to reach a native function without declaring it.</summary>
	public static readonly IReadOnlyList<string> RuntimeLookups = ["NativeLibrary", "GetDelegateForFunctionPointer", "delegate* unmanaged"];

	// Anywhere it is applied, qualified or in a list of attributes (code review, #24).
	private static readonly Regex ImportAttributePattern = new(@"\b(?:LibraryImport|DllImport)(?:Attribute)?\s*\(", RegexOptions.CultureInvariant);

	// The attribute, in a list or qualified, any further attributes, the modifiers and return type, then the method's name.
	private static readonly Regex DeclarationPattern = new(
		@"\[[^\]]*?\b(?:LibraryImport|DllImport)(?:Attribute)?\s*\((?<arguments>[^)]*)\)[^\]]*\](?:\s*\[[^\]]*\])*\s*(?:[\w<>?*,.\[\]]+\s+)+(?<name>\w+)\s*\(",
		RegexOptions.CultureInvariant);

	// One literal and nothing more, so "getuid" + "_other" can't pass as getuid (PR #29).
	private static readonly Regex EntryPointPattern = new(@"\bEntryPoint\s*=\s*""(?<entry>[^""]+)""\s*(?:,|$)", RegexOptions.CultureInvariant);

	private static readonly Regex RuntimeLookupPattern = new(@"\bNativeLibrary\b|\bGetDelegateForFunctionPointer\b|\bdelegate\s*\*\s*unmanaged\b", RegexOptions.CultureInvariant);

	/// <summary>The native functions the source declares: each declaration's EntryPoint, or its method's name.</summary>
	public static IReadOnlyList<string> Declared(string source) =>
		[.. DeclarationPattern.Matches(source).Select(EntryPoint)];

	/// <summary>Declarations of functions off the list, declarations it can't read, and run-time lookups.</summary>
	public static IReadOnlyList<string> FindIn(string source)
	{
		var declared = Declared(source);
		var unreadable = ImportAttributePattern.Count(source) - declared.Count;
		return
		[
			.. declared.Where(entry => !Allowed.Contains(entry))
				.Concat(Enumerable.Repeat("a native import declaration the guardrail can't read", Math.Max(unreadable, 0)))
				.Concat(RuntimeLookupPattern.Matches(source).Select(match => match.Value))
				.Distinct(),
		];
	}

	/// <summary>Entries of a CsWin32 NativeMethods.txt off the list. Each line names one function, and // starts a comment.</summary>
	public static IReadOnlyList<string> FindInNativeMethods(string nativeMethods) =>
	[
		.. nativeMethods.Split('\n')
			.Select(line => line.Split("//")[0].Trim())
			.Where(entry => entry.Length > 0 && !Allowed.Contains(entry))
			.Distinct(),
	];

	private static string EntryPoint(Match declaration)
	{
		var arguments = declaration.Groups["arguments"].Value;
		return EntryPointPattern.Match(arguments) is { Success: true } entryPoint ? entryPoint.Groups["entry"].Value
			: arguments.Contains("EntryPoint", StringComparison.Ordinal) ? "an EntryPoint the guardrail can't read"
			: declaration.Groups["name"].Value;
	}
}

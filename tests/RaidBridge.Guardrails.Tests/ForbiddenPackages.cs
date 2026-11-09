using System.Text.Json;
using System.Xml.Linq;

namespace RaidBridge.Guardrails.Tests;

/// <summary>
/// Packages that inject into the game process (ADR-0001). Raid Toolkit's SDK sits on Il2CppToolkit, whose runtime
/// loads a DLL into Raid.exe for every type lookup.
/// </summary>
public static class ForbiddenPackages
{
	public static readonly IReadOnlyList<string> Prefixes =
	[
		"Il2CppToolkit",
		"Raid.Toolkit",
	];

	private static readonly string[] ReferenceElements =
		["PackageReference", "PackageVersion", "GlobalPackageReference", "PackageDownload", "Reference"];

	/// <summary>Forbidden ids referenced directly by an MSBuild file (.csproj, .props, .targets).</summary>
	public static IReadOnlyList<string> FindInMsBuild(string msbuildXml) =>
		Forbidden(XDocument.Parse(msbuildXml)
			.Descendants()
			.Where(element => ReferenceElements.Contains(element.Name.LocalName))
			.Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
			.OfType<string>());

	/// <summary>Forbidden ids anywhere in a packages.lock.json, transitive dependencies included.</summary>
	public static IReadOnlyList<string> FindInLockFile(string lockFileJson)
	{
		using var document = JsonDocument.Parse(lockFileJson);
		return Forbidden(document.RootElement.GetProperty("dependencies")
			.EnumerateObject()
			.SelectMany(targetFramework => targetFramework.Value.EnumerateObject())
			.Select(package => package.Name)
			.ToList());
	}

	private static List<string> Forbidden(IEnumerable<string> ids) =>
		[.. ids.Where(id => Prefixes.Any(prefix => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).Distinct()];
}

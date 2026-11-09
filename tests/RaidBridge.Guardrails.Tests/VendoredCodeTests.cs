using System.Xml.Linq;

namespace RaidBridge.Guardrails.Tests;

/// <summary>AGENTS.md's vendoring rule on the real tree: vendored code is scanned like ours and wrapped by one module.</summary>
public class VendoredCodeTests
{
	private static readonly string ThirdParty = "third_party" + Path.DirectorySeparatorChar;

	[Fact]
	public void The_guardrails_scan_vendored_code() =>
		Assert.Contains(Repository.CodeFiles(), file => Repository.RelativePath(file).StartsWith(ThirdParty + "LibCpp2IL", StringComparison.Ordinal));

	[Fact]
	public void Only_the_game_project_references_libcpp2il()
	{
		var referencing = Repository.Files("*.csproj")
			.Where(project => ProjectReferences(project).Any(reference => Path.GetFileName(reference) == "LibCpp2IL.csproj"))
			.Select(Repository.RelativePath)
			.ToList();

		Assert.Equal([Path.Combine("src", "RaidBridge.Game", "RaidBridge.Game.csproj")], referencing);
	}

	/// <summary>Inside RaidBridge.Game too, only Metadata/ sees the parser (spec #20).</summary>
	[Fact]
	public void Only_the_metadata_folder_names_libcpp2il_in_production_code()
	{
		var source = "src" + Path.DirectorySeparatorChar;
		var metadataFolder = Path.Combine("src", "RaidBridge.Game", "Metadata") + Path.DirectorySeparatorChar;
		var users = Repository.Files("*.cs")
			.Select(Repository.RelativePath)
			.Where(path => path.StartsWith(source, StringComparison.Ordinal) && !path.StartsWith(metadataFolder, StringComparison.Ordinal))
			.Where(path => File.ReadAllText(Path.Combine(Repository.Root.FullName, path)).Contains("LibCpp2IL", StringComparison.Ordinal))
			.ToList();

		Assert.Empty(users);
	}

	private static IEnumerable<string> ProjectReferences(FileInfo project) =>
		XDocument.Load(project.FullName).Descendants("ProjectReference")
			.Select(reference => (string?)reference.Attribute("Include"))
			.OfType<string>()
			.Select(include => include.Replace('\\', '/'));
}

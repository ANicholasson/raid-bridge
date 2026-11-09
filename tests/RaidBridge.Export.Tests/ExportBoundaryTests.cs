using System.Reflection;

namespace RaidBridge.Export.Tests;

/// <summary>ADR-0004: dependencies point inward, so Export sees the Domain and the runtime, never the Game layer.</summary>
public class ExportBoundaryTests
{
	private static readonly string RuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

	[Fact]
	public void Export_references_only_the_domain_and_the_runtime()
	{
		var references = Assembly.Load("RaidBridge.Export").GetReferencedAssemblies()
			.Select(reference => reference.Name!)
			.Where(name => !File.Exists(Path.Combine(RuntimeDirectory, $"{name}.dll")))
			.ToList();

		Assert.Equal(["RaidBridge.Domain"], references);
	}
}

using System.Reflection;

namespace RaidBridge.Domain.Tests;

/// <summary>ADR-0004: the Domain is the centre of the hexagon and references nothing but the runtime.</summary>
public class DomainBoundaryTests
{
	/// <summary>The shared framework's own folder, so a package named System.Something doesn't pass as the runtime.</summary>
	private static readonly string RuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

	[Fact]
	public void The_domain_references_only_the_runtime()
	{
		var references = Assembly.Load("RaidBridge.Domain").GetReferencedAssemblies()
			.Select(reference => reference.Name!)
			.Where(name => !File.Exists(Path.Combine(RuntimeDirectory, $"{name}.dll")))
			.ToList();

		Assert.Empty(references);
	}
}

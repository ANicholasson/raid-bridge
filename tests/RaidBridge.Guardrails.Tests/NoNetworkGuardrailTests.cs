namespace RaidBridge.Guardrails.Tests;

/// <summary>The network constraint enforced on the real tree, and the scanner checked so that "clean" isn't "blind".</summary>
public class NoNetworkGuardrailTests
{
	public static TheoryData<string> Namespaces => [.. ForbiddenNetworkApis.Namespaces];

	public static TheoryData<string> Types => [.. ForbiddenNetworkApis.Types];

	public static TheoryData<string> Calls => [.. ForbiddenNetworkApis.Calls];

	private static readonly string[] ProjectFiles = ["*.csproj", "*.props", "*.targets"];

	[Fact]
	public void No_code_names_a_network_api()
	{
		var violations = Repository.CodeFiles()
			.SelectMany(file => ForbiddenNetworkApis.FindIn(File.ReadAllText(file.FullName))
				.Select(hit => $"{Repository.RelativePath(file)}: {hit}"))
			.ToList();

		Assert.Empty(violations);
	}

	/// <summary>A project file's <c>&lt;Using&gt;</c> imports a namespace into every file without naming it there (PR #61).</summary>
	[Fact]
	public void No_project_file_imports_a_network_namespace()
	{
		var violations = ProjectFiles
			.SelectMany(Repository.Files)
			.SelectMany(file => ForbiddenNetworkApis.FindIn(File.ReadAllText(file.FullName))
				.Select(hit => $"{Repository.RelativePath(file)}: {hit}"))
			.ToList();

		Assert.Empty(violations);
	}

	[Theory]
	[MemberData(nameof(Namespaces))]
	public void Finds_each_network_namespace_in_a_using_directive(string name) =>
		Assert.Equal([name], ForbiddenNetworkApis.FindIn($"using {name};"));

	[Theory]
	[MemberData(nameof(Types))]
	public void Finds_each_network_type_in_use(string name) =>
		Assert.Equal([name], ForbiddenNetworkApis.FindIn($"using var client = new {name}();"));

	[Theory]
	[MemberData(nameof(Calls))]
	public void Finds_each_xml_load_that_can_resolve_a_uri(string call) =>
		Assert.Equal([call], ForbiddenNetworkApis.FindIn($"var document = {call}(\"https://example.org/file.xml\");"));

	[Theory]
	[MemberData(nameof(Calls))]
	public void Finds_each_xml_load_with_whitespace_around_the_dot(string call) =>
		Assert.Equal([call], ForbiddenNetworkApis.FindIn($"var document = {call.Replace(".", "\n\t. ", StringComparison.Ordinal)}(\"https://example.org/file.xml\");"));

	[Theory]
	[InlineData("using Doc = System.Xml.Linq.XDocument;")]
	[InlineData("using static System.Xml.Linq.XDocument;")]
	[InlineData("using static System . Xml.Linq.XDocument;")]
	[InlineData("using static System\n\t.Xml.Linq.XDocument;")]
	[InlineData("<Using Include=\"System.Xml.Linq\" />")]
	public void Finds_an_xml_loader_imported_under_another_name(string import) =>
		Assert.Equal(["System.Xml"], ForbiddenNetworkApis.FindIn(import));

	[Theory]
	[InlineData("new System.Data.DataSet().ReadXml(\"https://example.org/file.xml\");")]
	[InlineData("using System.Data;")]
	public void Finds_xml_read_through_system_data(string source) =>
		Assert.Equal(["System.Data"], ForbiddenNetworkApis.FindIn(source));

	[Theory]
	[InlineData("using var connection = new Microsoft.Data.SqlClient.SqlConnection(text);")]
	[InlineData("<PackageReference Include=\"Microsoft.Data.SqlClient\" />")]
	public void Finds_a_sql_client_in_code_or_a_package_reference(string source) =>
		Assert.Equal(["Microsoft.Data.SqlClient"], ForbiddenNetworkApis.FindIn(source));

	[Fact]
	public void Allows_xml_parsed_from_text() =>
		Assert.Empty(ForbiddenNetworkApis.FindIn("var document = XDocument.Parse(text);"));

	[Fact]
	public void Finds_a_fully_qualified_client() =>
		Assert.Equal(
			["System.Net.Http", "HttpClient"],
			ForbiddenNetworkApis.FindIn("var client = new System.Net.Http.HttpClient();"));

	[Fact]
	public void Ignores_names_that_only_contain_a_network_name() =>
		Assert.Empty(ForbiddenNetworkApis.FindIn("var socketCount = DnsName.Length; Pinged(); HttpClientFactoryName();"));
}

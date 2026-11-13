using System.Text.RegularExpressions;

namespace RaidBridge.Guardrails.Tests;

/// <summary>
/// .NET's ways to reach the network (AGENTS.md: the only network destination is the endpoint the user configures, and
/// no telemetry, update check or third-party service). raid-bridge sends nothing yet, so nothing may name these. When
/// sending to the configured endpoint lands, that one feature folder is let through here and nothing else. A match is
/// a violation even in a comment: rename the code rather than trimming these lists. It's a tripwire for reaching the
/// network by accident, reading names the way C# writes them, not a parser that code written to dodge it can't fool.
/// </summary>
public static class ForbiddenNetworkApis
{
	/// <summary>
	/// Namespaces that hold nothing but network clients and listeners, and System.Xml, whose readers fetch a URI over
	/// HTTP. Nothing here reads XML, and refusing the namespace also catches an alias or a <c>using static</c> of a
	/// loader (PR #61). System.Data likewise, whose DataSet.ReadXml and DataTable.ReadXml open a URI
	/// (PR #61). Microsoft.Data.SqlClient connects to a database server, and naming it here also catches its package
	/// reference in a project file (PR #61). Matched as a prefix, so System.Xml covers System.Xml.Linq.
	/// </summary>
	public static readonly IReadOnlyList<string> Namespaces =
	[
		"System.Net.Http",
		"System.Net.Sockets",
		"System.Net.WebSockets",
		"System.Net.Quic",
		"System.Net.Mail",
		"System.Net.NetworkInformation",
		"System.Net.Security",
		"System.Xml",
		"System.Data",
		"Microsoft.Data.SqlClient",
	];

	/// <summary>Types that open a connection, look up a host, or listen, matched as whole words.</summary>
	public static readonly IReadOnlyList<string> Types =
	[
		"HttpClient",
		"HttpClientHandler",
		"HttpMessageHandler",
		"HttpMessageInvoker",
		"SocketsHttpHandler",
		"WebClient",
		"WebRequest",
		"HttpWebRequest",
		"FtpWebRequest",
		"HttpListener",
		"Socket",
		"TcpClient",
		"TcpListener",
		"UdpClient",
		"ClientWebSocket",
		"QuicConnection",
		"QuicListener",
		"SmtpClient",
		"Dns",
		"Ping",
		"SslStream",
		"NegotiateStream",
		// A named pipe on Windows can name another machine (PR #61).
		"NamedPipeClientStream",
		"NamedPipeServerStream",
		// XML readers that resolve a URI, over HTTP among others (PR #61).
		"XmlDocument",
		"XmlTextReader",
		"XPathDocument",
		"XmlUrlResolver",
		"XslCompiledTransform",
	];

	/// <summary>
	/// Calls that load XML from a URI when given a string (PR #61). They're refused whatever the argument,
	/// and stay refused if a feature ever needs System.Xml: XDocument.Parse takes text without resolving anything.
	/// </summary>
	public static readonly IReadOnlyList<string> Calls = ["XDocument.Load", "XElement.Load", "XmlReader.Create"];

	// C# allows whitespace and line breaks around a dot, so "System . Xml" and "XDocument\n\t.Load" name the same
	// things as "System.Xml" and "XDocument.Load" (PR #61).
	private static readonly Regex NamespacePattern = new(
		$@"\b(?:{string.Join('|', Namespaces.Select(Dotted))})\b",
		RegexOptions.CultureInvariant);

	private static readonly Regex TypePattern = new(
		$@"\b(?:{string.Join('|', Types.Select(Regex.Escape))})\b",
		RegexOptions.CultureInvariant);

	private static readonly Regex CallPattern = new(
		$@"\b(?:{string.Join('|', Calls.Select(Dotted))})\b",
		RegexOptions.CultureInvariant);

	private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

	public static IReadOnlyList<string> FindIn(string source) =>
	[
		.. NamespacePattern.Matches(source).Select(match => Whitespace.Replace(match.Value, string.Empty))
			.Concat(TypePattern.Matches(source).Select(match => match.Value))
			.Concat(CallPattern.Matches(source).Select(match => Whitespace.Replace(match.Value, string.Empty)))
			.Distinct(),
	];

	private static string Dotted(string name) => string.Join(@"\s*\.\s*", name.Split('.').Select(Regex.Escape));
}

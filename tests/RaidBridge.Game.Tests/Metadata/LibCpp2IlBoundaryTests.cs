using System.Reflection;
using RaidBridge.Game.Metadata;

namespace RaidBridge.Game.Tests.Metadata;

/// <summary>LibCpp2IL stays behind Metadata/: no type other projects can see shows one of its types (spec #20).</summary>
public class LibCpp2IlBoundaryTests
{
	private static readonly string[] HiddenAssemblies = ["LibCpp2IL", "AssetRipper.Primitives"];

	[Fact]
	public void No_public_type_of_the_game_project_exposes_a_parser_type() =>
		Assert.Empty(typeof(GameMetadata).Assembly.GetExportedTypes().SelectMany(Exposed));

	/// <summary>The scanner itself, so a pass means "hidden", not "blind".</summary>
	[Fact]
	public void Finds_a_parser_type_in_a_public_signature() =>
		Assert.Equal(
			["LibCpp2IL.LibCpp2IlContext", "LibCpp2IL.Metadata.Il2CppTypeDefinition", "AssetRipper.Primitives.UnityVersion"],
			Exposed(typeof(LeakyExample)));

	public sealed class LeakyExample
	{
		public LibCpp2IL.LibCpp2IlContext? Context { get; set; }

		public static IReadOnlyList<LibCpp2IL.Metadata.Il2CppTypeDefinition> Types() => [];

		public static void Parse(AssetRipper.Primitives.UnityVersion[] versions) => _ = versions;
	}

	private static IEnumerable<string> Exposed(Type type) =>
		SignatureTypes(type).SelectMany(HiddenParts).Select(hidden => hidden.FullName!).Distinct();

	/// <summary>The hidden types inside a signature type: itself, its element type or its generic arguments.</summary>
	private static IEnumerable<Type> HiddenParts(Type type) =>
		type.HasElementType ? HiddenParts(type.GetElementType()!)
		: HiddenAssemblies.Contains(type.Assembly.GetName().Name) ? [type]
		: type.IsGenericType ? type.GetGenericArguments().SelectMany(HiddenParts)
		: [];

	private static IEnumerable<Type> SignatureTypes(Type type)
	{
		const BindingFlags visible = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
		var members = type.GetMembers(visible | BindingFlags.NonPublic)
			.Where(member => member switch
			{
				MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
				FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
				_ => true,
			});

		return new[] { type.BaseType }
			.Concat(type.GetInterfaces())
			.Concat(members.SelectMany(member => member switch
			{
				MethodInfo method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType),
				ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
				PropertyInfo property => [property.PropertyType],
				FieldInfo field => [field.FieldType],
				EventInfo @event => [@event.EventHandlerType!],
				_ => [],
			}))
			.OfType<Type>();
	}
}

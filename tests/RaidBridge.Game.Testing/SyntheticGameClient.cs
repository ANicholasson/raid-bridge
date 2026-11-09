using System.Buffers.Binary;
using System.Text;
using RaidBridge.Domain.Snapshots;
using RaidBridge.Game.Memory;
using RaidBridge.Game.Metadata;
using RaidBridge.Game.Snapshots;
using RaidBridge.Game.Testing.Arena;
using RaidBridge.Game.Testing.Artifacts;
using RaidBridge.Game.Testing.Champions;
using RaidBridge.Game.Testing.FactionGuardians;
using RaidBridge.Game.Testing.GreatHall;
using RaidBridge.Game.Testing.Il2Cpp;
using RaidBridge.Game.Testing.Memory;
using RaidBridge.Game.Testing.Resources;
using static RaidBridge.Game.Testing.Il2Cpp.Unity6000Layout;

namespace RaidBridge.Game.Testing;

/// <summary>
/// A running Game client made up for tests: its install folder in a temporary directory, its memory in a
/// <see cref="FakeGameMemory"/> with the game's objects laid out as Unity 6000.3.18f1's IL2CPP runtime lays them out,
/// the heroes of a <see cref="SyntheticHeroData"/>, the Artifacts of a <see cref="SyntheticArtifactData"/> and the
/// Resources, Shards and Items of a <see cref="SyntheticResourceData"/>, the Great Hall of a
/// <see cref="SyntheticGreatHallData"/>, the Faction Guardians of a <see cref="SyntheticGuardianData"/> and the Classic
/// and Tag Arena of a <see cref="SyntheticArenaData"/> among them, and a matching description of its metadata. Every id
/// and name is synthetic (spec #20). The methods that break it stand for what a game update or a bad read can do.
/// Each feature's layout, and the methods that break it, are in a partial file in that feature's folder.
/// </summary>
public sealed partial class SyntheticGameClient : IDisposable
{
	/// <summary>An address with nothing mapped at it.</summary>
	public const ulong Unmapped = 0xDEAD_0000_0000;

	public const long DefaultAccountId = 4_000_123_456;

	private const ulong ImageBase = 0x1_8000_0000;
	private const ulong PreferredImageBase = 0x1_0000_0000;
	private const uint ThisBuild = 0x6A2F_1C2B;
	private const uint AnotherBuild = 0x6A30_8D4E;
	private const ulong GenericClassRva = 0x1000;
	private const ulong FixedTypeInfoRva = 0x1100;
	private const ulong ResolverTypeInfoRva = 0x1108;
	private const string Root = "Client.App.SingleInstance`1<Client.Model.AppModel>";
	private const string Fixed = "Plarium.Common.Numerics.Fixed";
	private const string Resolver = "SharedModel.Meta.Artifacts.ArtifactStorage.ArtifactStorageResolver";

	private static readonly DateTimeOffset Started = new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);

	private readonly SyntheticHeap _image;
	private readonly SyntheticHeap _heap;
	private readonly SyntheticRuntime _runtime;
	private readonly SyntheticMetadata _metadata;
	private readonly Dictionary<string, ulong> _slots = new(StringComparer.Ordinal);
	private readonly List<(ulong Address, int Length)> _socialData = [];
	private readonly Dictionary<string, ulong> _typeInfoSlots = new(StringComparer.Ordinal);
	private ulong _clientStaticData;

	private SyntheticGameClient(
		long userId,
		long userIdOnUser,
		string unityVersion,
		SyntheticHeroData heroData,
		SyntheticArtifactData artifactData,
		SyntheticResourceData resourceData,
		SyntheticGreatHallData greatHallData,
		SyntheticGuardianData guardianData,
		SyntheticArenaData arenaData)
	{
		Directory = System.IO.Directory.CreateTempSubdirectory("raid-bridge-client-").FullName;
		var gameAssembly = WriteInstallFolder(Directory, ThisBuild);
		Memory = new FakeGameMemory(new GameModule(ImageBase, gameAssembly), Started);
		_image = new SyntheticHeap(Memory, ImageBase, 0x2000);
		_heap = new SyntheticHeap(Memory, 0x2_0000_0000, 0x40000);
		_runtime = new SyntheticRuntime(_heap);
		_metadata = new SyntheticMetadata(new GameVersions(39, unityVersion));
		LayOutTheAccount(userId, userIdOnUser, heroData, artifactData, resourceData, greatHallData, guardianData, arenaData);
	}

	/// <summary>The install folder, holding GameAssembly.dll and the other files the reader looks for.</summary>
	public string Directory { get; }

	public FakeGameMemory Memory { get; }

	/// <summary>The GameAssembly.dll the reader took the installed game's files from, once it has.</summary>
	public string? MetadataReadFrom { get; private set; }

	public bool SocialDataWasRead => _socialData.Any(range => Memory.WasRead(range.Address, range.Length));

	public static SyntheticGameClient Create(
		long userId = DefaultAccountId,
		long userIdOnUser = DefaultAccountId,
		string unityVersion = UnityVersion,
		SyntheticHeroData? heroData = null,
		SyntheticArtifactData? artifactData = null,
		SyntheticResourceData? resourceData = null,
		SyntheticGreatHallData? greatHallData = null,
		SyntheticGuardianData? guardianData = null,
		SyntheticArenaData? arenaData = null) =>
		new(
			userId,
			userIdOnUser,
			unityVersion,
			heroData ?? SyntheticHeroData.Default,
			artifactData ?? SyntheticArtifactData.Default,
			resourceData ?? SyntheticResourceData.Default,
			greatHallData ?? SyntheticGreatHallData.Default,
			guardianData ?? SyntheticGuardianData.Default,
			arenaData ?? SyntheticArenaData.Default);

	/// <summary>The reader over this Game client, as the command line makes it, with this game's metadata in place of the parsed files.</summary>
	public ISnapshotSource Source(string? gameDirectory = null) =>
		new GameSnapshotSource(Memory, gameDirectory, files =>
		{
			MetadataReadFrom = files.GameAssembly;
			WhileParsing?.Invoke();
			return _metadata;
		});

	/// <summary>A game update renamed a class: the metadata has it under the new name only.</summary>
	public void RenameClass(string fullName, string newName) => _metadata.Rename(fullName, newName);

	/// <summary>A game update dropped a field from the metadata.</summary>
	public void UndeclareField(string className, string fieldName) => _metadata.Undeclare(className, fieldName);

	/// <summary>A game update changed a field's declared type.</summary>
	public void RetypeField(string className, string fieldName, string type) => _metadata.Retype(className, fieldName, type);

	/// <summary>A game update dropped a member from an enum.</summary>
	public void RemoveEnumMember(string enumName, string memberName) => _metadata.RemoveEnumMember(enumName, memberName);

	/// <summary>A runtime class names another as the class it is nested in, as a corrupt read might see it.</summary>
	public void SetDeclaringType(string className, ulong declaringType) => _heap.Pointer(_runtime.RuntimeClass(className) + ClassDeclaringType, declaringType);

	/// <summary>A runtime class names another as its base class, as a corrupt read might see it.</summary>
	public void SetBaseClass(string className, ulong parent) => _heap.Pointer(_runtime.RuntimeClass(className) + ClassParent, parent);

	/// <summary>The runtime class names the field differently from the metadata.</summary>
	public void RenameRuntimeField(string className, string fieldName, string newName) =>
		_heap.Pointer(_runtime.FieldInfo($"{className}.{fieldName}") + FieldInfoName, _heap.CString(newName));

	/// <summary>A field's FieldInfo points at nothing past its name, as a field the reader doesn't use might.</summary>
	public void BreakRuntimeFieldType(string className, string fieldName) =>
		_heap.Pointer(_runtime.FieldInfo($"{className}.{fieldName}") + FieldInfoType, Unmapped);

	/// <summary>The runtime class gives the field another type code than its declaration.</summary>
	public void SetRuntimeTypeCode(string className, string fieldName, byte typeCode)
	{
		_heap.Pointer(_runtime.FieldInfo($"{className}.{fieldName}") + FieldInfoType, _runtime.Type(typeCode));
	}

	/// <summary>A reference field, or the static root, points somewhere else.</summary>
	public void PointField(string className, string fieldName, ulong address) => _heap.Pointer(_slots[$"{className}.{fieldName}"], address);

	/// <summary>The object a reference field, or the static root, points to.</summary>
	public ulong Target(string className, string fieldName)
	{
		var slot = new byte[8];
		Memory.Read(_slots[$"{className}.{fieldName}"], slot);
		return BitConverter.ToUInt64(slot);
	}

	public ulong RuntimeClass(string fullName) => _runtime.RuntimeClass(fullName);

	/// <summary>
	/// The root's generic class record holds another runtime class: 0 when the IL2CPP runtime hasn't set it up yet,
	/// or one that doesn't lead back to the record when the layout table doesn't fit.
	/// </summary>
	public void SetRootClass(ulong runtimeClass) => _image.Pointer(ImageBase + GenericClassRva + GenericClassCachedClass, runtimeClass);

	/// <summary>
	/// A class's TypeInfo slot holds something else: the encoded reference the binary shipped with, when the IL2CPP
	/// runtime hasn't set the class up yet, or another runtime class.
	/// </summary>
	public void SetTypeInfoSlot(string fullName, ulong value) => _image.Pointer(_typeInfoSlots[fullName], value);

	/// <summary>A game update changed one of the game's constants, such as Fixed.ONE.</summary>
	public void SetConstant(string className, string fieldName, long value) => _metadata.Constant(className, fieldName, value);

	/// <summary>
	/// The launcher put another build's GameAssembly.dll in the install folder after the Game client started, keeping
	/// the file's old time, as an update that restores the times from its archive does.
	/// </summary>
	public void ReplaceGameAssemblyAfterStart() => WriteGameAssembly(GameAssemblyHeaders(PreferredImageBase, AnotherBuild));

	/// <summary>GameAssembly.dll's SizeOfHeaders says another size, as a corrupt file might.</summary>
	public void SetSizeOfHeaders(uint size)
	{
		var headers = GameAssemblyHeaders(PreferredImageBase, ThisBuild);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(0x58 + 60), size);
		WriteGameAssembly(headers);
	}

	/// <summary>The launcher wrote one of the game's files after the Game client started: its time changed, and nothing else.</summary>
	public void TouchAfterStart(string relativePath) =>
		File.SetLastWriteTimeUtc(Path.Combine(Directory, relativePath), Started.AddHours(1).UtcDateTime);

	/// <summary>What happens while the reader parses the installed game's files, such as the launcher replacing one.</summary>
	public Action? WhileParsing { get; set; }

	/// <summary>A second install folder inside this one, holding this build's files, or another build's.</summary>
	public string AnotherInstall(bool sameBuild)
	{
		var directory = Path.Combine(Directory, sameBuild ? "copy" : "other-build");
		WriteInstallFolder(directory, sameBuild ? ThisBuild : AnotherBuild);
		return directory;
	}

	public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);

	private void WriteGameAssembly(byte[] bytes)
	{
		var path = Path.Combine(Directory, "GameAssembly.dll");
		File.WriteAllBytes(path, bytes);
		File.SetLastWriteTimeUtc(path, Started.AddDays(-1).UtcDateTime);
	}

	private static IEnumerable<string> InstallFiles(string directory) =>
		new[] { "GameAssembly.dll", "UnityPlayer.dll", Path.Combine("Raid_Data", "data.unity3d"), Path.Combine("Raid_Data", "il2cpp_data", "Metadata", "global-metadata.dat") }
			.Select(file => Path.Combine(directory, file));

	/// <summary>The install folder's files, GameAssembly.dll with the headers of the build stamped <paramref name="build"/>, at the image base it prefers.</summary>
	private static string WriteInstallFolder(string directory, uint build)
	{
		foreach (var path in InstallFiles(directory))
		{
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, Path.GetFileName(path) == "GameAssembly.dll" ? GameAssemblyHeaders(PreferredImageBase, build) : Encoding.ASCII.GetBytes("synthetic"));
			File.SetLastWriteTimeUtc(path, Started.AddDays(-1).UtcDateTime);
		}

		return Path.Combine(directory, "GameAssembly.dll");
	}

	/// <summary>
	/// A 64-bit PE DLL's headers and nothing more: the DOS header, the PE signature, the file header stamped with the
	/// build, and an optional header with no sections, taking 0x200 bytes as its SizeOfHeaders says.
	/// </summary>
	private static byte[] GameAssemblyHeaders(ulong imageBase, uint build)
	{
		const int optionalHeader = 0x58;
		var headers = new byte[0x200];
		BinaryPrimitives.WriteUInt16LittleEndian(headers.AsSpan(0x00), 0x5A4D);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(0x3C), 0x40);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(0x40), 0x0000_4550);
		BinaryPrimitives.WriteUInt16LittleEndian(headers.AsSpan(0x44), 0x8664);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(0x48), build);
		BinaryPrimitives.WriteUInt16LittleEndian(headers.AsSpan(0x54), 240);
		BinaryPrimitives.WriteUInt16LittleEndian(headers.AsSpan(0x56), 0x2022);
		BinaryPrimitives.WriteUInt16LittleEndian(headers.AsSpan(optionalHeader), 0x20B);
		BinaryPrimitives.WriteUInt64LittleEndian(headers.AsSpan(optionalHeader + 24), imageBase);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(optionalHeader + 32), 0x1000);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(optionalHeader + 36), 0x200);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(optionalHeader + 56), 0x2000);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(optionalHeader + 60), (uint)headers.Length);
		BinaryPrimitives.WriteUInt32LittleEndian(headers.AsSpan(optionalHeader + 108), 16);
		return headers;
	}

	/// <summary>
	/// spec #20's path to the Account: the static root on the generic instance SingleInstance&lt;AppModel&gt;,
	/// AppModel's id, and the fallback through _userWrapper.User, with Social beside it holding the UserSocialData
	/// the reader must never read; then the heroes, and the static data that names them.
	/// </summary>
	private void LayOutTheAccount(
		long userId,
		long userIdOnUser,
		SyntheticHeroData heroData,
		SyntheticArtifactData artifactData,
		SyntheticResourceData resourceData,
		SyntheticGreatHallData greatHallData,
		SyntheticGuardianData guardianData,
		SyntheticArenaData arenaData)
	{
		// The loader moved the image from the base it prefers and wrote the new one into its headers.
		_image.Bytes(ImageBase, GameAssemblyHeaders(ImageBase, ThisBuild));
		var systemObject = _runtime.SystemObject;
		DeclareTheClassLibrary();

		var singleInstance = _runtime.Class("Client.App", "SingleInstance`1", systemObject, new SyntheticField("_instance", ClassTypeCode, 0, IsStatic: true));
		_metadata.Class("Client.App.SingleInstance`1", "System.Object", ("_instance", "T", true));
		var record = ImageBase + GenericClassRva;
		_image.Pointer(record + GenericClassType, _runtime.Type(ClassTypeCode));
		_image.Pointer(record + GenericClassClassInstance, _heap.Allocate(16));
		_image.Pointer(record + GenericClassCachedClass, singleInstance);
		_heap.Pointer(singleInstance + ClassGenericClass, record);
		_metadata.GenericClass(Root, GenericClassRva);

		var appModel = Declare(
			"Client.Model",
			"AppModel",
			singleInstance,
			"Client.App.SingleInstance`1",
			("_userWrapper", "Client.Model.Guard.UserWrapper", ClassTypeCode, 0x18),
			("<UserId>k__BackingField", "System.Int64", Int64TypeCode, 0x48),
			("<StaticDataManager>k__BackingField", "Client.Model.Gameplay.StaticData.IClientStaticDataManager", ClassTypeCode, 0x50));
		var userWrapper = Declare(
			"Client.Model.Guard",
			"UserWrapper",
			systemObject,
			"System.Object",
			("Social", "Client.Model.Gameplay.Social.SocialWrapper", ClassTypeCode, 0x10),
			("User", "Client.Model.Gameplay.User.User", ClassTypeCode, 0x18),
			("Heroes", "Client.Model.Gameplay.Heroes.HeroesWrapper", ClassTypeCode, 0x20),
			("Artifacts", "Client.Model.Gameplay.Artifacts.EquipmentWrapper", ClassTypeCode, 0x28),
			("Account", "Client.Model.Gameplay.Account.AccountWrapper", ClassTypeCode, 0x30),
			("Shards", "Client.Model.Gameplay.Shards.ShardWrapper", ClassTypeCode, 0x38),
			("BlackMarket", "Client.Model.Gameplay.BlackMarket.BlackMarketWrapper", ClassTypeCode, 0x40),
			("Village", "Client.Model.Gameplay.Village.VillageWrapper", ClassTypeCode, 0x48),
			("Academy", "Client.Model.Gameplay.Academy.AcademyWrapper", ClassTypeCode, 0x50),
			("Arena", "Client.Model.Gameplay.Battle.ArenaWrapper", ClassTypeCode, 0x58),
			("Arena3x3", "Client.Model.Gameplay.Arena3x3.Arena3x3Wrapper", ClassTypeCode, 0x60));
		var user = Declare("Client.Model.Gameplay.User", "User", systemObject, "System.Object", ("Id", "System.Int64", Int64TypeCode, 0x20));
		var socialWrapper = Declare(
			"Client.Model.Gameplay.Social",
			"SocialWrapper",
			systemObject,
			"System.Object",
			("SocialData", "Client.Model.Gameplay.Social.Data.UserSocialData", ClassTypeCode, 0x10));
		var socialData = Declare("Client.Model.Gameplay.Social.Data", "UserSocialData", systemObject, "System.Object");

		var statics = _heap.Allocate(8);
		_heap.Pointer(singleInstance + ClassStaticFields, statics);
		var appModelObject = _runtime.Object(appModel, 0x58);
		Slot("Client.App.SingleInstance`1._instance", statics, appModelObject);
		_heap.Int64(appModelObject + 0x48, userId);

		var userWrapperObject = _runtime.Object(userWrapper, 0x68);
		Slot("Client.Model.AppModel._userWrapper", appModelObject + 0x18, userWrapperObject);
		var userObject = _runtime.Object(user, 0x28);
		Slot("Client.Model.Guard.UserWrapper.User", userWrapperObject + 0x18, userObject);
		_heap.Int64(userObject + 0x20, userIdOnUser);

		var socialWrapperObject = _runtime.Object(socialWrapper, 0x18);
		Slot("Client.Model.Guard.UserWrapper.Social", userWrapperObject + 0x10, socialWrapperObject);
		var socialDataObject = _runtime.Object(socialData, 0x40);
		Slot("Client.Model.Gameplay.Social.SocialWrapper.SocialData", socialWrapperObject + 0x10, socialDataObject);
		_socialData.Add((socialWrapperObject, 0x18));
		_socialData.Add((socialDataObject, 0x40));

		Slot("Client.Model.AppModel.<StaticDataManager>k__BackingField", appModelObject + 0x50, LayOutTheStaticData(heroData, artifactData));
		Slot("Client.Model.Guard.UserWrapper.Heroes", userWrapperObject + 0x20, LayOutTheHeroes(heroData));
		Slot("Client.Model.Guard.UserWrapper.Artifacts", userWrapperObject + 0x28, LayOutTheArtifacts(artifactData));
		LayOutTheResources(userWrapperObject, resourceData);
		LayOutTheGreatHall(userWrapperObject, greatHallData);
		LayOutTheFactionGuardians(userWrapperObject, guardianData);
		LayOutTheArena(userWrapperObject, arenaData);
	}

	/// <summary>The class library's classes the reader looks into, and the game's enums, as the metadata declares them.</summary>
	private void DeclareTheClassLibrary()
	{
		_metadata.Class("System.Object", baseClass: null);
		_metadata.Class("System.Collections.Generic.List`1", "System.Object", ("_items", "T[]", false), ("_size", "System.Int32", false));
		_metadata.Class(
			"System.Collections.Generic.Dictionary`2",
			"System.Object",
			("_entries", "System.Collections.Generic.Dictionary`2+Entry<TKey, TValue>[]", false),
			("_count", "System.Int32", false),
			("_freeCount", "System.Int32", false));
		_metadata.Class(
			"System.Collections.Generic.Dictionary`2+Entry",
			"System.ValueType",
			("hashCode", "System.Int32", false),
			("next", "System.Int32", false),
			("key", "TKey", false),
			("value", "TValue", false));
		_metadata.Class("System.Nullable`1", "System.ValueType", ("hasValue", "System.Boolean", false), ("value", "T", false));

		_metadata.Enum("SharedModel.Meta.Heroes.HeroGrade", ("Stars1", 1), ("Stars2", 2), ("Stars3", 3), ("Stars4", 4), ("Stars5", 5), ("Stars6", 6));
		_metadata.Enum("SharedModel.Meta.Heroes.HeroRarity", ("Common", 1), ("Uncommon", 2), ("Rare", 3), ("Epic", 4), ("Legendary", 5), ("Mythical", 6));
		_metadata.Enum("SharedModel.Meta.Heroes.HeroFraction", ("Unknown", 0), ("BannerLords", 1), ("HighElves", 2), ("DarkElves", 11));
		_metadata.Enum("SharedModel.Meta.Heroes.Element", ("Magic", 1), ("Force", 2), ("Spirit", 3), ("Void", 4));
		_metadata.Enum("SharedModel.Meta.Heroes.HeroMarker", ("None", 0), ("Favorite", 1));
		_metadata.Enum("SharedModel.Meta.DoubleAscend.User.DoubleAscendGrade", ("Stars1", 1), ("Stars2", 2), ("Stars3", 3), ("Stars4", 4), ("Stars5", 5), ("Stars6", 6));
		_metadata.Enum("SharedModel.Meta.DoubleAscend.Static.BlessingTypeId", ("Necromancy", 1101), ("TimeSlowdown", 1102));
		_metadata.Enum(
			"SharedModel.Meta.Artifacts.ArtifactKindId",
			("UnknownArtifact", 0), ("Helmet", 1), ("Chest", 2), ("Gloves", 3), ("Boots", 4), ("Weapon", 5), ("Shield", 6), ("Ring", 7), ("Cloak", 8), ("Banner", 9));
		_metadata.Enum("SharedModel.Meta.Artifacts.ArtifactRankId", ("Unknown", 0), ("One", 1), ("Two", 2), ("Three", 3), ("Four", 4), ("Five", 5), ("Six", 6));
		_metadata.Enum("SharedModel.Meta.ItemRarity", ("None", 0), ("Common", 1), ("Uncommon", 2), ("Rare", 3), ("Epic", 4), ("Legendary", 5), ("Mythical", 6));
		_metadata.Enum("SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", ("None", 0), ("Hp", 1), ("AttackPower", 2), ("Defense", 3), ("AttackSpeed", 4));
		_metadata.Enum(
			"SharedModel.Battle.Effects.StatKindId",
			("Health", 1), ("Attack", 2), ("Defence", 3), ("Speed", 4), ("Resistance", 5), ("Accuracy", 6), ("CriticalChance", 7), ("CriticalDamage", 8));
		_metadata.Enum("SharedModel.Meta.Account.ResourceTypeId", ("Energy", 1), ("Silver", 2), ("Token", 3), ("Gem", 4));
		_metadata.Enum("SharedModel.Meta.Shards.ShardTypeId", ("Mystery", 1), ("Ancient", 3), ("Legendary", 5), ("Void", 6), ("Mythical", 7));
		_metadata.Enum(
			"SharedModel.Meta.BlackMarket.BlackMarketItemId",
			("HeroXpBooster100P_1Day", 3001), ("SkillUpgradeItemRare", 5001), ("SkillUpgradeItemEpic", 5002), ("HeroRankUpItemGrade2", 6501));
		_metadata.Enum(
			"SharedModel.Meta.Battle.Arena.ArenaLeagueId",
			("None", 0), ("BronzeI", 1), ("BronzeIV", 4), ("SilverIII", 13), ("GoldII", 22), ("Platinum", 30));
	}

	/// <summary>
	/// AppModel's static data manager, holding ClientStaticData: hero types by id, skill types by id, and the two
	/// localization tables, from string to string.
	/// </summary>
	private ulong LayOutTheStaticData(SyntheticHeroData heroData, SyntheticArtifactData artifactData)
	{
		const string Table = "System.Collections.Generic.Dictionary`2<System.String, System.String>";
		var systemObject = _runtime.SystemObject;

		var textKey = Declare(
			"SharedModel.Common.Localization",
			"SharedLTextKey",
			systemObject,
			"System.Object",
			("Key", "System.String", StringTypeCode, 0x10),
			("DefaultValue", "System.String", StringTypeCode, 0x18));
		var heroForm = Declare("SharedModel.Meta.Heroes", "HeroForm", systemObject, "System.Object", ("Element", "SharedModel.Meta.Heroes.Element", ValueTypeTypeCode, 0x10));
		var heroType = Declare(
			"SharedModel.Meta.Heroes",
			"HeroType",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("Name", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x18),
			("Fraction", "SharedModel.Meta.Heroes.HeroFraction", ValueTypeTypeCode, 0x20),
			("Rarity", "SharedModel.Meta.Heroes.HeroRarity", ValueTypeTypeCode, 0x24),
			("Forms", "SharedModel.Meta.Heroes.HeroForm[]", SingleDimensionArrayTypeCode, 0x28));
		var skillType = Declare(
			"SharedModel.Meta.Skills",
			"SkillType",
			systemObject,
			"System.Object",
			("Id", "System.Int32", Int32TypeCode, 0x10),
			("Name", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x18));
		var staticHeroData = Declare(
			"SharedModel.Meta.Heroes",
			"StaticHeroData",
			systemObject,
			"System.Object",
			("HeroTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Heroes.HeroType>", GenericInstanceTypeCode, 0x10));
		var staticSkillData = Declare(
			"SharedModel.Meta.Skills",
			"StaticSkillData",
			systemObject,
			"System.Object",
			("_skillTypeById", "System.Collections.Generic.Dictionary`2<System.Int32, SharedModel.Meta.Skills.SkillType>", GenericInstanceTypeCode, 0x10));
		var staticData = Declare(
			"SharedModel.Meta",
			"StaticData",
			systemObject,
			"System.Object",
			("HeroData", "SharedModel.Meta.Heroes.StaticHeroData", ClassTypeCode, 0x10),
			("SkillData", "SharedModel.Meta.Skills.StaticSkillData", ClassTypeCode, 0x18));
		var clientStaticData = Declare(
			"SharedModel.Meta",
			"ClientStaticData",
			staticData,
			"SharedModel.Meta.StaticData",
			("StaticDataLocalization", Table, GenericInstanceTypeCode, 0x20),
			("ClientLocalization", Table, GenericInstanceTypeCode, 0x28),
			("ArtifactData", "SharedModel.Meta.Artifacts.StaticArtifactData", ClassTypeCode, 0x30),
			("VillageData", "SharedModel.Meta.Village.StaticVillageData", ClassTypeCode, 0x38));
		var manager = Declare(
			"Client.Model.Gameplay.StaticData",
			"ClientStaticDataManager",
			systemObject,
			"System.Object",
			("<StaticData>k__BackingField", "SharedModel.Meta.ClientStaticData", ClassTypeCode, 0x10));

		ulong Text(SyntheticText text)
		{
			var key = _runtime.Object(textKey, 0x20);
			_heap.Pointer(key + 0x10, _runtime.String(text.Key));
			_heap.Pointer(key + 0x18, _runtime.String(text.DefaultValue));
			return key;
		}

		var forms = _runtime.ArrayClass(heroForm, "HeroForm", sizeof(ulong));
		var heroTypes = heroData.HeroTypes.Select(type =>
		{
			var heroTypeObject = _runtime.Object(heroType, 0x30);
			_heap.Int32(heroTypeObject + 0x10, type.Id);
			_heroTypes[type.Id] = heroTypeObject;
			_heap.Pointer(heroTypeObject + 0x18, Text(type.Name));
			_heap.Int32(heroTypeObject + 0x20, type.Fraction);
			_heap.Int32(heroTypeObject + 0x24, type.Rarity);
			_heap.Pointer(heroTypeObject + 0x28, _runtime.ReferenceArray(forms, [.. type.FormElements.Select(element =>
			{
				var form = _runtime.Object(heroForm, 0x18);
				_heap.Int32(form + 0x10, element);
				return form;
			})]));
			return ((ulong)type.Id, heroTypeObject);
		});
		var staticHeroDataObject = _runtime.Object(staticHeroData, 0x18);
		_heap.Pointer(staticHeroDataObject + 0x10, _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. heroTypes]));

		var skillTypes = heroData.SkillTypes.Select(type =>
		{
			var skillTypeObject = _runtime.Object(skillType, 0x20);
			_heap.Int32(skillTypeObject + 0x10, type.Id);
			_skillTypes[type.Id] = skillTypeObject;
			_heap.Pointer(skillTypeObject + 0x18, Text(type.Name));
			return ((ulong)type.Id, skillTypeObject);
		});
		var staticSkillDataObject = _runtime.Object(staticSkillData, 0x18);
		_heap.Pointer(staticSkillDataObject + 0x10, _runtime.Dictionary(_runtime.DictionaryType(Int32TypeCode, ClassTypeCode), [.. skillTypes]));

		var table = _runtime.DictionaryType(StringTypeCode, StringTypeCode);
		ulong Localization(IReadOnlyDictionary<string, string> texts) =>
			_runtime.Dictionary(table, [.. texts.Select(text => (_runtime.String(text.Key), _runtime.String(text.Value)))]);

		var setInfo = Declare(
			"SharedModel.Meta.Artifacts.Sets",
			"ArtifactSetInfo",
			systemObject,
			"System.Object",
			("ArtifactSetKindId", "SharedModel.Meta.Artifacts.Sets.ArtifactSetKindId", ValueTypeTypeCode, 0x10),
			("Name", "SharedModel.Common.Localization.SharedLTextKey", ClassTypeCode, 0x18));
		var staticArtifactData = Declare(
			"SharedModel.Meta.Artifacts",
			"StaticArtifactData",
			systemObject,
			"System.Object",
			("SetInfos", "System.Collections.Generic.List`1<SharedModel.Meta.Artifacts.Sets.ArtifactSetInfo>", GenericInstanceTypeCode, 0x10));
		var staticArtifactDataObject = _runtime.Object(staticArtifactData, 0x18);
		_heap.Pointer(staticArtifactDataObject + 0x10, _runtime.List(_runtime.ListType(setInfo, "ArtifactSetInfo", sizeof(ulong)), [.. artifactData.Sets.Select(set =>
		{
			var setInfoObject = _runtime.Object(setInfo, 0x20);
			_heap.Int32(setInfoObject + 0x10, set.Kind);
			_heap.Pointer(setInfoObject + 0x18, Text(set.Name));
			return setInfoObject;
		})]));

		var clientStaticDataObject = _runtime.Object(clientStaticData, 0x40);
		_clientStaticData = clientStaticDataObject;
		_heap.Pointer(clientStaticDataObject + 0x10, staticHeroDataObject);
		_heap.Pointer(clientStaticDataObject + 0x18, staticSkillDataObject);
		_heap.Pointer(clientStaticDataObject + 0x20, Localization(heroData.StaticDataLocalization));
		_heap.Pointer(clientStaticDataObject + 0x28, Localization(heroData.ClientLocalization));
		_heap.Pointer(clientStaticDataObject + 0x30, staticArtifactDataObject);

		var managerObject = _runtime.Object(manager, 0x18);
		_heap.Pointer(managerObject + 0x10, clientStaticDataObject);
		return managerObject;
	}

	/// <summary>The TypeInfo slot the binary keeps for a class, holding the runtime class as the IL2CPP runtime leaves it once it has set the class up.</summary>
	private void TypeInfoSlot(string fullName, ulong rva, ulong runtimeClass)
	{
		_metadata.TypeInfo(fullName, rva);
		_typeInfoSlots[fullName] = ImageBase + rva;
		_image.Pointer(ImageBase + rva, runtimeClass);
	}

	private static byte Bool(bool value) => value ? (byte)1 : (byte)0;

	/// <summary>A class of the game, declared alike in the runtime and in the metadata.</summary>
	private ulong Declare(string ns, string name, ulong parent, string? baseClass, params (string Name, string Type, byte TypeCode, int Offset)[] fields)
	{
		_metadata.Class($"{ns}.{name}", baseClass, [.. fields.Select(field => (field.Name, field.Type, false))]);
		return _runtime.Class(ns, name, parent, [.. fields.Select(field => new SyntheticField(field.Name, field.TypeCode, field.Offset))]);
	}

	private void Slot(string field, ulong slot, ulong value)
	{
		_heap.Pointer(slot, value);
		_slots[field] = slot;
	}
}

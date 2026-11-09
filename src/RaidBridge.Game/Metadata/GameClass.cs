namespace RaidBridge.Game.Metadata;

/// <summary>A class as the installed game's metadata declares it. Types are named in full, generic arity included: <c>System.Collections.Generic.Dictionary`2&lt;System.Int32, SharedModel.Meta.Heroes.Hero&gt;</c>.</summary>
internal sealed record GameClass(string FullName, string? BaseType, IReadOnlyList<string> Interfaces, IReadOnlyList<GameField> Fields);

/// <summary>A field as the installed game's metadata declares it, and the class that declares it.</summary>
internal sealed record GameField(string DeclaringClass, string Name, string Type, bool IsStatic);

/// <summary>An enum as the installed game's metadata declares it: its underlying type, and each member's name and value.</summary>
internal sealed record GameEnum(string FullName, string UnderlyingType, IReadOnlyList<GameEnumMember> Members);

/// <summary>One member of a game enum.</summary>
internal sealed record GameEnumMember(string Name, long Value);

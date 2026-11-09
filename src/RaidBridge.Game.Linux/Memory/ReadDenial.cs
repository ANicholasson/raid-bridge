using System.Globalization;
using RaidBridge.Game.Memory;

namespace RaidBridge.Game.Linux.Memory;

/// <summary>
/// Why Linux won't let raid-bridge read the Game client (#13): another user's process, Yama's ptrace_scope 3, or a game
/// outside Steam's container, whose user namespace would otherwise give raid-bridge the right to read it. It names the
/// cause and never suggests a fix that needs root.
/// </summary>
internal static class ReadDenial
{
	public static GameMemoryException Explain(ProcFileSystem proc, int pid, LinuxUser user)
	{
		var notAllowed = $"raid-bridge isn't allowed to read the Game client (pid {pid})";

		ProcessStatus game;
		try
		{
			game = proc.Status(pid);
		}
		catch (IOException)
		{
			return new($"The Game client (pid {pid}) has exited.");
		}

		// Linux compares all three of the game's user ids with raid-bridge's (code review, #24).
		uint[] gameIds = [game.UserId, game.EffectiveUserId, game.SavedUserId];
		if (gameIds.Any(id => id != user.Id))
		{
			return new($"{notAllowed}: it runs as user {gameIds.First(id => id != user.Id)}, and raid-bridge as user {user.Id}. Run raid-bridge as the user who plays RAID.");
		}

		var scope = proc.PtraceScope();
		if (scope == 3)
		{
			return new($"{notAllowed}: kernel.yama.ptrace_scope is 3 on this system, so no process may read another's memory.");
		}

		var gameNamespace = proc.UserNamespace(pid);
		var sharesNamespace = gameNamespace is not null && gameNamespace == proc.OwnUserNamespace();
		if (scope is 1 or 2 && sharesNamespace)
		{
			var allowed = scope == 1 ? "the program that started it" : "an administrator";
			return new($"{notAllowed}: it isn't in Steam's container, so kernel.yama.ptrace_scope {scope} lets only {allowed} read it. Launch RAID from Steam with Proton, then try again.");
		}

		var where = gameNamespace is null ? "in a user namespace raid-bridge can't see" : sharesNamespace ? "in raid-bridge's user namespace" : "in its own user namespace";
		return new($"{notAllowed}, for a reason raid-bridge doesn't know: kernel.yama.ptrace_scope is {scope?.ToString(CultureInfo.InvariantCulture) ?? "not set"}, and the game runs as user {game.EffectiveUserId} {where}.");
	}
}

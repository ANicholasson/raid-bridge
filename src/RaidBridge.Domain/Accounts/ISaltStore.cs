namespace RaidBridge.Domain.Accounts;

/// <summary>Keeps the player's salt on their machine. The first load makes it; every later load returns the same one.</summary>
public interface ISaltStore
{
	Salt LoadOrCreate();
}

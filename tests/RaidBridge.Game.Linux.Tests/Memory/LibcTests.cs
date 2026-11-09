using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using RaidBridge.Game.Linux.Memory;

namespace RaidBridge.Game.Linux.Tests.Memory;

/// <summary>The three imports on this machine, pointed at the test's own process and never at the game.</summary>
[SupportedOSPlatform("linux")]
public class LibcTests
{
	private const int Efault = 14;

	[Fact]
	public void Process_vm_readv_copies_from_a_process_at_an_address()
	{
		Assert.SkipUnless(OperatingSystem.IsLinux(), "libc's imports are Linux's.");
		var source = "raid-bridge"u8.ToArray();
		var pinned = GCHandle.Alloc(source, GCHandleType.Pinned);
		try
		{
			var copy = new byte[source.Length];

			var result = Libc.ReadProcessVm(Environment.ProcessId, (ulong)pinned.AddrOfPinnedObject(), copy);

			Assert.Equal((source.Length, 0), result);
			Assert.Equal(source, copy);
		}
		finally
		{
			pinned.Free();
		}
	}

	[Fact]
	public void Process_vm_readv_gives_efault_for_an_address_with_nothing_mapped()
	{
		Assert.SkipUnless(OperatingSystem.IsLinux(), "libc's imports are Linux's.");

		Assert.Equal((-1, Efault), Libc.ReadProcessVm(Environment.ProcessId, 0, new byte[8]));
	}

	[Fact]
	public void Getuid_and_geteuid_agree_with_the_kernel()
	{
		Assert.SkipUnless(OperatingSystem.IsLinux(), "libc's imports are Linux's.");
		var status = ProcFileSystem.Host.OwnStatus();

		Assert.Equal((status.UserId, status.EffectiveUserId), (Libc.GetUserId(), Libc.GetEffectiveUserId()));
	}
}

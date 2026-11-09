namespace RaidBridge.Guardrails.Tests;

/// <summary>The scanners themselves, so a passing guardrail means "clean", not "blind".</summary>
public class ForbiddenApisTests
{
	public static TheoryData<string> Names => [.. ForbiddenApis.Names];

	[Theory]
	[MemberData(nameof(Names))]
	public void Finds_each_forbidden_name_in_a_call(string name) =>
		Assert.Equal([name], ForbiddenApis.FindIn($"Native.{name}(handle, address);"));

	[Theory]
	[InlineData("SendMessageW")]
	[InlineData("PostMessageA")]
	[InlineData("SetWindowsHookExW")]
	public void Finds_the_ansi_and_wide_variants(string name) =>
		Assert.Equal([name], ForbiddenApis.FindIn($"[LibraryImport(\"user32\")] static partial nint {name}(nint hWnd);"));

	[Fact]
	public void Finds_an_access_right_inside_a_flags_expression() =>
		Assert.Contains(
			"PROCESS_VM_WRITE",
			ForbiddenApis.FindIn("var access = PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ | PROCESS_ACCESS_RIGHTS.PROCESS_VM_WRITE;"));

	[Theory]
	[InlineData("var handle = Process.GetProcessesByName(\"Raid\")[0].Handle;", ".Handle")]
	[InlineData("using var handle = raid.SafeHandle;", ".SafeHandle")]
	[InlineData("raid.Kill(entireProcessTree: true);", ".Kill")]
	[InlineData("raid.CloseMainWindow();", ".CloseMainWindow")]
	public void Finds_the_process_members_that_open_with_full_access_or_end_the_process(string source, string hit) =>
		Assert.Equal([hit], ForbiddenApis.FindIn(source));

	[Theory]
	[InlineData("PInvoke.OpenProcess(0x1F0FFF, false, pid);", "OpenProcess(0x1F0FFF)")]
	[InlineData("PInvoke.OpenProcess_SafeHandle(access, false, pid);", "OpenProcess(access)")]
	[InlineData("OpenProcess((PROCESS_ACCESS_RIGHTS)0x10, false, pid);", "OpenProcess((PROCESS_ACCESS_RIGHTS)0x10)")]
	[InlineData("OpenProcess(PROCESS_VM_READ, false, pid); OpenProcess(0x1F0FFF, false, pid);", "OpenProcess(0x1F0FFF)")]
	public void Finds_an_open_process_call_that_does_not_name_read_rights_inline(string source, string hit) =>
		Assert.Equal([hit], ForbiddenApis.FindIn(source));

	[Fact]
	public void Allows_the_read_only_calls_the_reader_needs() =>
		Assert.Empty(ForbiddenApis.FindIn(
			"""
			using var handle = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
			var other = OpenProcess(PROCESS_VM_READ, false, pid);
			PInvoke.ReadProcessMemory(handle, address, buffer, size, null);
			"""));

	[Fact]
	public void Allows_an_open_process_declaration() =>
		Assert.Empty(ForbiddenApis.FindIn("private static extern nint OpenProcess(uint access, bool inherit, uint pid);"));

	[Fact]
	public void Finds_a_call_that_follows_a_declaration_on_the_same_line() =>
		Assert.Equal(
			["OpenProcess(0x1F0FFF)"],
			ForbiddenApis.FindIn("static extern nint OpenProcess(uint access, bool inherit, uint pid); static nint Open() => OpenProcess(0x1F0FFF, false, 1);"));

	[Fact]
	public void Ignores_names_that_only_contain_a_forbidden_name() =>
		Assert.Empty(ForbiddenApis.FindIn("var lastSendMessageAt = clock.Now; ParseSendInputs(); var handled = window.HandleCount; var killed = stats.KillCount;"));

	public static TheoryData<string> LinuxNames => [.. ForbiddenApis.LinuxNames];

	[Theory]
	[MemberData(nameof(LinuxNames))]
	public void Finds_each_forbidden_linux_name_in_a_call(string name) =>
		Assert.Equal([name], ForbiddenApis.FindIn($"Native.{name}(pid, address);"));

	[Theory]
	[InlineData("ptrace(PTRACE_ATTACH, pid, 0, 0);", "PTRACE_ATTACH")]
	[InlineData("const int Request = PTRACE_SEIZE;", "PTRACE_SEIZE")]
	[InlineData("sched_setaffinity(pid, size, mask);", "sched_setaffinity")]
	public void Finds_every_ptrace_request_and_scheduler_call(string source, string hit) =>
		Assert.Contains(hit, ForbiddenApis.FindIn(source));

	[Theory]
	[InlineData("""File.OpenRead($"/proc/{pid}/mem");""", "/proc/<pid>/mem (/mem)")]
	[InlineData("""File.OpenRead(Path.Combine(root, pid.ToString(), "mem"));""", "/proc/<pid>/mem (\"mem\")")]
	public void Finds_a_processs_memory_file(string source, string hit) =>
		Assert.Equal([hit], ForbiddenApis.FindIn(source));

	[Theory]
	[InlineData("""File.WriteAllText("/proc/sys/kernel/yama/ptrace_scope", "0");""", "File.WriteAllText in a file that names /proc")]
	[InlineData("""var root = "/proc"; using var stream = new FileStream(Path.Combine(root, "self", "oom_score_adj"), FileMode.Open, FileAccess.Write);""", "FileAccess.Write in a file that names /proc")]
	[InlineData("""var path = Path.Combine("/", "proc", "self", "coredump_filter"); using var writer = new StreamWriter(path);""", "StreamWriter in a file that names /proc")]
	[InlineData("""var fd = open("/proc/self/attr/current", O_WRONLY);""", "O_WRONLY in a file that names /proc")]
	[InlineData("""using var stream = File.Open("/proc/self/oom_score_adj", FileMode.Open);""", "File.Open without FileAccess.Read in a file that names /proc")]
	[InlineData("""using var stream = new FileStream("/proc/self/oom_score_adj", FileMode.Open, FileAccess.ReadWrite);""", "FileStream without FileAccess.Read in a file that names /proc")]
	[InlineData("""using var stream = File.Open("/proc/self/oom_score_adj", FileMode.Open, FileAccess.Read | extraAccess);""", "File.Open without FileAccess.Read in a file that names /proc")]
	[InlineData("""using var stream = File.Open("/proc/self/oom_score_adj, FileAccess.Read,", FileMode.Open);""", "File.Open without FileAccess.Read in a file that names /proc")]
	[InlineData("""using var stream = File.Open("/proc/self/oom_score_adj(", FileMode.Open);""", "File.Open without FileAccess.Read in a file that names /proc")]
	[InlineData("""using var stream = new FileInfo("/proc/self/oom_score_adj").OpenWrite();""", ".OpenWrite in a file that names /proc")]
	[InlineData("""using var writer = new FileInfo("/proc/self/oom_score_adj").CreateText();""", ".CreateText in a file that names /proc")]
	[InlineData("""var info = new FileInfo("/proc/self/oom_score_adj"); using var writer = info.AppendText();""", ".AppendText in a file that names /proc")]
	[InlineData("""var info = new FileInfo("/proc/self/oom_score_adj"); using var stream = info.Open(FileMode.Open);""", "info.Open without FileAccess.Read in a file that names /proc")]
	[InlineData("""var streams = new[] { File.Open("/proc/self/oom_score_adj", FileMode.Open), File.Open(Path.Combine("/proc", "self", "maps"), FileMode.Open, FileAccess.Read) };""", "File.Open without FileAccess.Read in a file that names /proc")]
	public void Finds_a_write_in_a_file_that_names_proc(string source, string hit) =>
		Assert.Contains(hit, ForbiddenApis.FindIn(source));

	[Fact]
	public void Allows_the_reads_the_linux_adapter_needs() =>
		Assert.Empty(ForbiddenApis.FindIn(
			"""
			public static ProcFileSystem Host { get; } = new("/proc");
			var maps = File.ReadAllText(Path.Combine(root, pid.ToString(), "maps"));
			var scope = File.ReadAllText(Path.Combine(root, "sys", "kernel", "yama", "ptrace_scope"));
			var read = ProcessVmReadv(pid, &local, 1, &remote, 1, 0);
			var user = Libc.GetUserId();
			using var stream = File.Open("/proc/self/maps", FileMode.Open, FileAccess.Read);
			var memory = new FakeGameMemory(module, startedAt);
			"""));

	[Fact]
	public void Allows_writes_in_a_file_that_does_not_name_proc() =>
		Assert.Empty(ForbiddenApis.FindIn("""File.WriteAllText(Path.Combine(directory, "salt"), text); using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);"""));
}

public class NativeImportsTests
{
	[Fact]
	public void Reads_the_entry_point_of_each_declaration() =>
		Assert.Equal(
			["process_vm_readv", "getuid", "geteuid", "getpid"],
			NativeImports.Declared(
				"""
				[LibraryImport("libc", EntryPoint = "process_vm_readv", SetLastError = true)]
				private static partial nint ProcessVmReadv(int pid, IoVec* localVectors, nuint localCount, IoVec* remoteVectors, nuint remoteCount, nuint flags);

				[LibraryImport("libc", EntryPoint = "getuid")]
				public static partial uint GetUserId();

				[DllImport("libc")]
				[return: MarshalAs(UnmanagedType.U4)]
				private static extern uint geteuid();

				[DllImport("libc", SetLastError = true), SuppressGCTransition] static extern int getpid();
				"""));

	[Theory]
	[InlineData("""[LibraryImport("libc", EntryPoint = "process_vm_writev")] private static partial nint ProcessVmRead(int pid);""", "process_vm_writev")]
	[InlineData("""[DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);""", "open")]
	[InlineData("""[DllImportAttribute("kernel32")] static extern bool ReadProcessMemory(nint process, nint address, byte[] buffer, nint size, out nint read);""", "ReadProcessMemory")]
	[InlineData("""[System.Runtime.InteropServices.DllImport("libc")] static extern nint write(int fd, byte* buffer, nint count);""", "write")]
	[InlineData("""[SuppressGCTransition, DllImport("libc")] static extern int open(string path, int flags);""", "open")]
	public void Finds_a_declaration_off_the_list(string source, string hit) =>
		Assert.Equal([hit], NativeImports.FindIn(source));

	[Theory]
	[InlineData("[LibraryImport(\"libc\")] // the process id\nprivate static partial int getpid();", "a native import declaration the guardrail can't read")]
	[InlineData("[LibraryImport(\"libc\", EntryPoint = Names.Open)] private static partial int Open(string path);", "an EntryPoint the guardrail can't read")]
	[InlineData("[LibraryImport(\"libc\", EntryPoint = \"getuid\" + \"_other\")] private static partial int Other();", "an EntryPoint the guardrail can't read")]
	public void Finds_a_declaration_it_cannot_read(string source, string hit) =>
		Assert.Equal([hit], NativeImports.FindIn(source));

	[Theory]
	[InlineData("""var library = NativeLibrary.Load("libc.so.6");""", "NativeLibrary")]
	[InlineData("""var call = Marshal.GetDelegateForFunctionPointer<Getter>(address);""", "GetDelegateForFunctionPointer")]
	[InlineData("""var call = (delegate* unmanaged<int, int>)address;""", "delegate* unmanaged")]
	public void Finds_a_native_function_looked_up_at_run_time(string source, string hit) =>
		Assert.Equal([hit], NativeImports.FindIn(source));

	[Fact]
	public void Allows_the_three_functions_on_the_list() =>
		Assert.Empty(NativeImports.FindIn(
			"""
			[LibraryImport("libc", EntryPoint = "process_vm_readv", SetLastError = true)]
			private static partial nint ProcessVmReadv(int pid, IoVec* localVectors, nuint localCount, IoVec* remoteVectors, nuint remoteCount, nuint flags);
			[LibraryImport("libc", EntryPoint = "getuid")]
			public static partial uint GetUserId();
			[LibraryImport("libc", EntryPoint = "geteuid")]
			public static partial uint GetEffectiveUserId();
			"""));

	[Fact]
	public void Finds_a_native_methods_entry_off_the_list() =>
		Assert.Equal(["OpenProcess", "ReadProcessMemory"], NativeImports.FindInNativeMethods("// CsWin32\nOpenProcess\nReadProcessMemory // the read\ngetuid\n"));
}

public class ForbiddenPackagesTests
{
	[Theory]
	[InlineData("""<Project><ItemGroup><PackageReference Include="Il2CppToolkit.Runtime" /></ItemGroup></Project>""", "Il2CppToolkit.Runtime")]
	[InlineData("""<Project><ItemGroup><PackageVersion Include="il2cpptoolkit.injection.client" Version="2.0.145-alpha" /></ItemGroup></Project>""", "il2cpptoolkit.injection.client")]
	[InlineData("""<Project><ItemGroup><PackageReference Update="Raid.Toolkit.Extensibility" /></ItemGroup></Project>""", "Raid.Toolkit.Extensibility")]
	[InlineData("""<Project><ItemGroup><GlobalPackageReference Include="Il2CppToolkit.Runtime" Version="2.0.134-alpha" /></ItemGroup></Project>""", "Il2CppToolkit.Runtime")]
	[InlineData("""<Project><ItemGroup><PackageDownload Include="Il2CppToolkit.Common" Version="[2.0.134-alpha]" /></ItemGroup></Project>""", "Il2CppToolkit.Common")]
	[InlineData("""<Project><ItemGroup><Reference Include="Il2CppToolkit.Runtime"><HintPath>lib/Il2CppToolkit.Runtime.dll</HintPath></Reference></ItemGroup></Project>""", "Il2CppToolkit.Runtime")]
	public void Finds_an_injecting_package_in_a_build_file(string msbuildXml, string id) =>
		Assert.Equal([id], ForbiddenPackages.FindInMsBuild(msbuildXml));

	[Fact]
	public void Finds_an_injecting_package_pulled_in_transitively() =>
		Assert.Equal(
			["Il2CppToolkit.Injection.Client"],
			ForbiddenPackages.FindInLockFile(
				"""
				{
				  "version": 1,
				  "dependencies": {
				    "net10.0": {
				      "Community.Raid.Interop": { "type": "Direct", "requested": "[1.0.0, )", "resolved": "1.0.0" },
				      "Il2CppToolkit.Injection.Client": { "type": "Transitive", "resolved": "2.0.145-alpha" }
				    }
				  }
				}
				"""));

	[Fact]
	public void Allows_other_packages() =>
		Assert.Empty(ForbiddenPackages.FindInMsBuild(
			"""<Project><ItemGroup><PackageVersion Include="Samboy063.LibCpp2IL" Version="2022.1.0-pre-release.21" /></ItemGroup></Project>"""));
}

using System.Text.RegularExpressions;

namespace RaidBridge.Guardrails.Tests;

/// <summary>
/// Code that would break ADR-0001: writing to the game, running code in it, debugging, ending or driving it, opening
/// it with more than read access, or changing what the system allows. A match is a violation even in a comment or in
/// an unrelated method: rename the code rather than trimming these lists.
/// </summary>
public static class ForbiddenApis
{
	/// <summary>Win32 and ntdll entry points and access rights. The same name with an A or W suffix matches too.</summary>
	public static readonly IReadOnlyList<string> Names =
	[
		// Writing to or changing the game's memory
		"WriteProcessMemory",
		"NtWriteVirtualMemory",
		"ZwWriteVirtualMemory",
		"VirtualAllocEx",
		"NtAllocateVirtualMemory",
		"ZwAllocateVirtualMemory",
		"VirtualProtectEx",
		"NtProtectVirtualMemory",
		"ZwProtectVirtualMemory",
		// Running code inside the game
		"CreateRemoteThread",
		"CreateRemoteThreadEx",
		"NtCreateThreadEx",
		"RtlCreateUserThread",
		"QueueUserAPC",
		"QueueUserAPC2",
		"NtQueueApcThread",
		"SetThreadContext",
		"SetWindowsHookEx",
		// Debugging or suspending the game
		"DebugActiveProcess",
		"NtDebugActiveProcess",
		"NtSuspendProcess",
		// Ending the game (PR #1)
		"TerminateProcess",
		"NtTerminateProcess",
		"ZwTerminateProcess",
		// Driving the game
		"SendInput",
		"keybd_event",
		"mouse_event",
		"SendKeys",
		"PostMessage",
		"PostThreadMessage",
		"SendMessage",
		"SendMessageTimeout",
		"SendNotifyMessage",
		// Access rights beyond reading, as CsWin32 and pinvoke.net name them
		"PROCESS_ALL_ACCESS",
		"PROCESS_VM_WRITE",
		"PROCESS_VM_OPERATION",
		"PROCESS_CREATE_THREAD",
		"PROCESS_DUP_HANDLE",
		"PROCESS_SET_INFORMATION",
		"PROCESS_SUSPEND_RESUME",
		"PROCESS_TERMINATE",
		"VirtualMemoryWrite",
		"VirtualMemoryOperation",
	];

	/// <summary>
	/// Members of <see cref="System.Diagnostics.Process"/> that open the process with PROCESS_ALL_ACCESS, or end it
	/// (PR #1).
	/// </summary>
	public static readonly IReadOnlyList<string> ForbiddenProcessMembers = ["Handle", "SafeHandle", "Kill", "CloseMainWindow"];

	/// <summary>
	/// Linux system calls, libc functions and tools that write to another process, attach to it, signal it or change
	/// its memory, limits or scheduling, reach into the kernel or another namespace, or change raid-bridge's own
	/// privileges (spec #20, from #6 and #13). Matched as whole words, case included.
	/// </summary>
	public static readonly IReadOnlyList<string> LinuxNames =
	[
		// Writing to or attaching to the game
		"process_vm_writev",
		"ptrace",
		"pidfd_getfd",
		// Ending or signalling the game
		"kill",
		"tkill",
		"tgkill",
		"killpg",
		"pidfd_send_signal",
		"sigqueue",
		"rt_sigqueueinfo",
		"rt_tgsigqueueinfo",
		// Changing the game's memory, limits or placement
		"process_madvise",
		"process_mrelease",
		"prlimit",
		"prlimit64",
		"move_pages",
		"migrate_pages",
		// Reaching into the kernel or another namespace
		"bpf",
		"perf_event_open",
		"setns",
		// A system call by number, which would get round every name here
		"syscall",
		// Changing raid-bridge's own privileges or the system's protection
		"prctl",
		"capset",
		"setcap",
	];

	/// <summary>Families matched by prefix: every ptrace request and every scheduler call.</summary>
	public static readonly IReadOnlyList<string> LinuxPrefixes = ["PTRACE_", "sched_"];

	/// <summary>The only rights an OpenProcess call may ask for, and it must name them inline.</summary>
	public static readonly IReadOnlyList<string> AllowedOpenProcessRights = ["PROCESS_VM_READ", "PROCESS_QUERY_LIMITED_INFORMATION"];

	private static readonly Regex NamePattern = new(
		$@"\b(?:{string.Join('|', Names.Select(Regex.Escape))})[AW]?\b",
		RegexOptions.CultureInvariant);

	private static readonly Regex LinuxNamePattern = new(
		$@"\b(?:{string.Join('|', LinuxNames.Select(Regex.Escape))})\b|\b(?:{string.Join('|', LinuxPrefixes.Select(Regex.Escape))})\w+",
		RegexOptions.CultureInvariant);

	/// <summary>A process's memory file, /proc/&lt;pid&gt;/mem, as a path or as the last segment of one.</summary>
	private static readonly Regex ProcMemPattern = new(@"/mem\b|""mem""", RegexOptions.CultureInvariant);

	/// <summary>A string that names /proc, as a path or as a segment of one.</summary>
	private static readonly Regex ProcPattern = new(@"""/proc\b|""proc""", RegexOptions.CultureInvariant);

	/// <summary>Opening, creating or replacing a file for writing, by .NET or by open(2)'s flags.</summary>
	private static readonly Regex FileWritePattern = new(
		// The last alternative is FileInfo's members, on any receiver (PR #29).
		@"\b(?:FileAccess\.\w*Write|FileMode\.(?!Open\b)\w+|File\.(?:OpenWrite|Create\w*|WriteAll\w*|AppendAll\w*|AppendText|Copy|Move|Replace)|StreamWriter|O_(?:WRONLY|RDWR|CREAT|TRUNC|APPEND))\b|\.(?:OpenWrite|CreateText|AppendText)\s*\(",
		RegexOptions.CultureInvariant);

	/// <summary>
	/// A File.Open or FileStream call and its own arguments, nested parentheses balanced, so another call's
	/// FileAccess.Read can't vouch for it. Its overloads without an access open the file for reading and writing
	/// (PR #29).
	/// </summary>
	private static readonly Regex OpenCallPattern = new(
		// Any receiver's Open, FileInfo's included, and string literals taken whole, so a parenthesis in a path can't
		// unbalance the arguments (PR #29).
		@"(?<call>\w*\.Open\b|\bFileStream)\s*\((?<arguments>(?>""(?:[^""\\]|\\.)*""|[^()""]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))\)",
		RegexOptions.CultureInvariant);

	// FileAccess.Read as a whole argument, so "FileAccess.Read | extraAccess" can't pass as read-only (PR #29).
	private static readonly Regex ReadAccessPattern = new(@"(?:^|,)\s*(?:access\s*:\s*)?(?:System\.IO\.)?FileAccess\.Read\s*(?:,|$)", RegexOptions.CultureInvariant);

	// String literals are emptied before the access is looked for, so a path can't spell it (PR #29).
	private static readonly Regex StringLiteralPattern = new(@"""(?:[^""\\]|\\.)*""", RegexOptions.CultureInvariant);

	private static readonly Regex ProcessMemberPattern = new(
		$@"\.(?:{string.Join('|', ForbiddenProcessMembers.Select(Regex.Escape))})\b",
		RegexOptions.CultureInvariant);

	// Unanchored, so every call on a line is checked, not just the first (PR #1).
	private static readonly Regex OpenProcessCallPattern = new(
		@"\bOpenProcess\w*\s*\(\s*(?<access>[^,\r\n]*)",
		RegexOptions.CultureInvariant);

	public static IReadOnlyList<string> FindIn(string source) =>
	[
		.. NamePattern.Matches(source).Select(match => match.Value)
			.Concat(ProcessMemberPattern.Matches(source).Select(match => match.Value))
			.Concat(OpenProcessViolations(source))
			.Concat(LinuxNamePattern.Matches(source).Select(match => match.Value))
			.Concat(ProcMemPattern.Matches(source).Select(match => $"/proc/<pid>/mem ({match.Value})"))
			.Concat(WritableProcOpens(source))
			.Distinct(),
	];

	/// <summary>Every place the source opens, creates or replaces a file for writing.</summary>
	public static IReadOnlyList<string> FindWrites(string source) =>
	[
		.. FileWritePattern.Matches(source).Select(match => match.Value.TrimEnd('(', ' '))
			.Concat(OpenCallPattern.Matches(source)
				.Where(call => !ReadAccessPattern.IsMatch(StringLiteralPattern.Replace(call.Groups["arguments"].Value, "\"\"")))
				.Select(call => $"{call.Groups["call"].Value} without FileAccess.Read"))
			.Distinct(),
	];

	/// <summary>Nothing under /proc is opened to write, so a file that names /proc writes no file at all.</summary>
	private static IEnumerable<string> WritableProcOpens(string source) =>
		ProcPattern.IsMatch(source)
			? FindWrites(source).Select(write => $"{write} in a file that names /proc")
			: [];

	private static IEnumerable<string> OpenProcessViolations(string source) =>
		OpenProcessCallPattern.Matches(source)
			.Where(match => !IsDeclaration(StatementBefore(source, match.Index)))
			.Select(match => match.Groups["access"].Value.Trim())
			.Where(access => !access.Split('|').All(right => AllowedOpenProcessRights.Contains(UnqualifiedName(right))))
			.Select(access => $"OpenProcess({access})");

	// Only the statement the match is in, so a declaration can't hide a call after it on the same line
	// (PR #1).
	private static string StatementBefore(string source, int index)
	{
		var start = source.LastIndexOfAny(['\n', ';', '{', '}'], Math.Max(index - 1, 0)) + 1;
		return source[start..index];
	}

	private static bool IsDeclaration(string textBeforeName) =>
		textBeforeName.Contains("extern ", StringComparison.Ordinal) || textBeforeName.Contains("partial ", StringComparison.Ordinal);

	private static string UnqualifiedName(string right)
	{
		var trimmed = right.Trim();
		return trimmed[(trimmed.LastIndexOf('.') + 1)..];
	}
}

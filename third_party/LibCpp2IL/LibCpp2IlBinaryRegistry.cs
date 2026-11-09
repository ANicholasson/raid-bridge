using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibCpp2IL.Logging;

namespace LibCpp2IL;

public static class LibCpp2IlBinaryRegistry
{
    private static List<RegisteredBinary> _binaries = [];

    public static void RegisterBuiltInBinarySupport()
    {
        Register("Portable Executable", "LibCpp2IL",
            bytes => BitConverter.ToInt16(bytes, 0) == 0x5A4D, //MZ
            (memStream) => new PE.PE(memStream));

        // raid-bridge patch: the Game client ships a Windows PE binary only, so ELF, Nintendo Switch, WebAssembly and Mach-O support is dropped.
    }

    public static void Register<T>(string name, string source, Func<byte[], bool> isValid, Func<MemoryStream, T> factory) where T : Il2CppBinary
    {
        _binaries.Add(new(name, source, isValid, factory));
    }

    internal static Il2CppBinary CreateAndInit(byte[] buffer, LibCpp2IlContext context)
    {
        if (_binaries.Count == 0)
            RegisterBuiltInBinarySupport();

        var match = _binaries.Find(b => b.IsValid(buffer));

        if (match == null)
            throw new($"Unknown binary type, no binary handling header bytes {string.Join(" ", buffer.SubArray(0, 4).Select(b => $"{b:X2}"))} has been registered");

        LibLogger.InfoNewline($"Using binary type {match.Name} (from {match.Source})");

        var memStream = new MemoryStream(buffer, 0, buffer.Length, true, true);
        var binary = match.FactoryFunc(memStream);

        binary.Init(context);

        return binary;
    }

    private class RegisteredBinary(
        string name,
        string source,
        Func<byte[], bool> verificationFunc,
        Func<MemoryStream, Il2CppBinary> factoryFunc)
    {
        public string Name = name;
        public string Source = source;
        public Func<byte[], bool> IsValid = verificationFunc;
        public Func<MemoryStream, Il2CppBinary> FactoryFunc = factoryFunc;
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SteamManagerDesktop
{
    public static class MonoConnector
    {
        [DllImport("kernel32", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32", SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32", SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32", SetLastError=true)] static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32", SetLastError=true)] static extern bool VirtualProtectEx(IntPtr process, IntPtr address, UIntPtr size, uint protect, out uint old);
        [DllImport("kernel32", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] bytes, UIntPtr size, out UIntPtr written);
        [DllImport("kernel32", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] bytes, UIntPtr size, out UIntPtr read);
        [DllImport("kernel32", SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr attributes, UIntPtr stack, IntPtr start, IntPtr parameter, uint flags, out uint id);
        [DllImport("kernel32", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32", SetLastError=true)] static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32", CharSet=CharSet.Ansi, ExactSpelling=true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32")] static extern bool FreeLibrary(IntPtr module);
        static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        public static Process FindGame()
        {
            var games = Process.GetProcessesByName("valheim");
            if (games.Length != 1) throw new InvalidOperationException(games.Length == 0 ? "Launch Valheim through Steam first." : "Multiple Valheim processes found. Leave one running.");
            return games[0];
        }
        public static void Verify(Process game)
        {
            var root = Path.GetDirectoryName(game.MainModule.FileName);
            string assembly = Path.Combine(root, "valheim_Data", "Managed", "assembly_valheim.dll");
            string expected = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-assembly.sha256")).Trim();
            using (var stream = File.OpenRead(assembly))
            using (var sha = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("This Valheim build differs from the tested assembly fingerprint. Update and rebuild the trainer before attaching.");
        }
        public static void Attach(Process game)
        { LoadHelper(game,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SteamManager.Runtime.dll"),"SteamManagerRuntime","Bootstrap","Start"); }
        public static void LoadHelper(Process game,string assemblyPath,string nameSpace,string className,string methodName)
        {
            Verify(game);
            var module = game.Modules.Cast<ProcessModule>().SingleOrDefault(m => m.ModuleName.Equals("mono-2.0-bdwgc.dll", StringComparison.OrdinalIgnoreCase));
            if (module == null) throw new InvalidOperationException("Valheim's Mono runtime is not ready.");
            IntPtr local = LoadLibraryEx(module.FileName, IntPtr.Zero, 1);
            if (local == IntPtr.Zero) throw new Win32Exception();
            IntPtr process = IntPtr.Zero, data = IntPtr.Zero, code = IntPtr.Zero, thread = IntPtr.Zero;
            bool threadFinished = true;
            try
            {
                Func<string,long> export = name => { var p = GetProcAddress(local, name); if (p == IntPtr.Zero) throw new MissingMethodException("Mono export " + name); return module.BaseAddress.ToInt64() + p.ToInt64() - local.ToInt64(); };
                process = OpenProcess(0x0002 | 0x0008 | 0x0010 | 0x0020 | 0x0400, false, game.Id);
                if (process == IntPtr.Zero) throw new Win32Exception();
                data = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)16384, 0x3000, 0x04);
                if (data == IntPtr.Zero) throw new Win32Exception();
                long baseAddr = data.ToInt64(); int cursor = 256;
                Func<string,long> str = value => { byte[] bytes = Encoding.UTF8.GetBytes(value + "\0"); long addr = baseAddr + cursor; cursor += bytes.Length; if (cursor > 16384) throw new InvalidOperationException("Runtime path too long."); Write(process, new IntPtr(addr), bytes); return addr; };
                long harmonyPath = str(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"));
                long runtimePath = str(assemblyPath);
                long ns = str(nameSpace), cls = str(className), method = str(methodName);
                var stub = new Stub();
                stub.Bytes(0x48,0x83,0xEC,0x28); // x64 shadow space and stack alignment
                stub.Call(export("mono_get_root_domain")); stub.Store(baseAddr); stub.RequireResult();
                stub.LoadArg(0, baseAddr); stub.Call(export("mono_thread_attach")); stub.Store(baseAddr+8); stub.RequireResult();
                stub.LoadArg(0, baseAddr); stub.Arg(1, harmonyPath); stub.Call(export("mono_domain_assembly_open")); stub.RequireResult();
                stub.LoadArg(0, baseAddr); stub.Arg(1, runtimePath); stub.Call(export("mono_domain_assembly_open")); stub.RequireResult();
                stub.Bytes(0x48,0x89,0xC1); stub.Call(export("mono_assembly_get_image")); stub.RequireResult();
                stub.Bytes(0x48,0x89,0xC1); stub.Arg(1, ns); stub.Arg(2, cls); stub.Call(export("mono_class_from_name")); stub.RequireResult();
                stub.Bytes(0x48,0x89,0xC1); stub.Arg(1, method); stub.Arg(2,0); stub.Call(export("mono_class_get_method_from_name")); stub.RequireResult();
                stub.Bytes(0x48,0x89,0xC1); stub.Arg(1,0); stub.Arg(2,0); stub.Arg(3,baseAddr+16); stub.Call(export("mono_runtime_invoke"));
                stub.Arg(0,1); stub.Bytes(0x48,0x89,0xC8); stub.Store(baseAddr+24);
                stub.ResolveFailures();
                stub.LoadArg(0,baseAddr+8); stub.Bytes(0x48,0x85,0xC9,0x74,0x0C); stub.Call(export("mono_thread_detach"));
                stub.Bytes(0x48,0x83,0xC4,0x28,0x31,0xC0,0xC3);
                byte[] bytesCode = stub.ToArray();
                code = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)bytesCode.Length, 0x3000, 0x04);
                if (code == IntPtr.Zero) throw new Win32Exception();
                Write(process, code, bytesCode); uint old;
                Check(VirtualProtectEx(process,code,(UIntPtr)bytesCode.Length,0x20,out old)); Check(FlushInstructionCache(process,code,(UIntPtr)bytesCode.Length));
                uint tid; thread = CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,code,IntPtr.Zero,0,out tid);
                if (thread == IntPtr.Zero) throw new Win32Exception();
                threadFinished = false;
                if (WaitForSingleObject(thread,15000) != 0) throw new TimeoutException("Runtime attach has not finished. Do not retry attachment in this game session; restart Valheim first.");
                threadFinished = true;
                byte[] result = new byte[32]; UIntPtr read;
                Check(ReadProcessMemory(process,data,result,(UIntPtr)result.Length,out read));
                if (BitConverter.ToInt64(result,16) != 0) throw new InvalidOperationException("The runtime helper reported an exception. Check runtime.log beside the app.");
                if (BitConverter.ToInt64(result,24) != 1) throw new InvalidOperationException("Mono could not resolve or load the runtime helper.");
            }
            finally
            {
                // Never free code still executing in the target process after a timeout.
                if (threadFinished && process != IntPtr.Zero) { if (data != IntPtr.Zero) VirtualFreeEx(process,data,UIntPtr.Zero,0x8000); if (code != IntPtr.Zero) VirtualFreeEx(process,code,UIntPtr.Zero,0x8000); }
                if (thread != IntPtr.Zero) CloseHandle(thread); if (process != IntPtr.Zero) CloseHandle(process); FreeLibrary(local);
            }
        }
        static void Write(IntPtr process, IntPtr address, byte[] bytes) { UIntPtr count; Check(WriteProcessMemory(process,address,bytes,(UIntPtr)bytes.Length,out count)); if (count.ToUInt64() != (ulong)bytes.Length) throw new IOException("Incomplete runtime write."); }
        sealed class Stub
        {
            readonly List<byte> code = new List<byte>(); readonly List<int> failures = new List<int>();
            public void Bytes(params byte[] bytes) { code.AddRange(bytes); }
            void Imm(long n) { code.AddRange(BitConverter.GetBytes(n)); }
            public void Arg(int arg,long n) { Bytes(arg < 2 ? (byte)0x48 : (byte)0x49, new byte[]{0xB9,0xBA,0xB8,0xB9}[arg]); Imm(n); }
            public void LoadArg(int arg,long addr) { Bytes(0x48,0xB8); Imm(addr); if (arg == 0) Bytes(0x48,0x8B,0x08); else throw new NotSupportedException(); }
            public void Call(long addr) { Bytes(0x48,0xB8); Imm(addr); Bytes(0xFF,0xD0); }
            public void Store(long addr) { Bytes(0x49,0xBB); Imm(addr); Bytes(0x49,0x89,0x03); }
            public void RequireResult() { Bytes(0x48,0x85,0xC0,0x0F,0x84); failures.Add(code.Count); Bytes(0,0,0,0); }
            public void ResolveFailures() { foreach (int p in failures) { byte[] b = BitConverter.GetBytes(code.Count-p-4); for(int i=0;i<4;i++)code[p+i]=b[i]; } }
            public byte[] ToArray() { return code.ToArray(); }
        }
    }
}

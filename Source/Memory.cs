using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using vmmsharp;

namespace eft_dma_radar
{
    internal static class Memory
    {
        /// <summary>
        /// Adjust this to achieve desired mem/sec performance. Higher = slower, Lower = faster.
        /// </summary>
        private static Vmm vmmInstance;

        private static volatile bool _running = false;
        private static volatile bool _restart = false;
        private static volatile bool _ready = false;
        private static Thread _workerThread;
        private static CancellationTokenSource _workerCancellationTokenSource;
        private static uint _pid;
        private static ulong _unityBase;
        private static Game _game;
        private static int _ticksCounter = 0;
        private static volatile int _ticks = 0;
        private static readonly Stopwatch _tickSw = new();

        public static Game.GameStatus GameStatus = Game.GameStatus.NotFound;

        #region Getters
        public static int Ticks
        {
            get => _ticks;
        }
        public static bool InGame
        {
            get => _game?.InGame ?? false;
        }
        public static bool Ready
        {
            get => _ready;
        }
        public static bool InHideout
        {
            get => _game?.InHideout ?? false;
        }
        public static bool IsScav
        {
            get => _game?.IsScav ?? false;
        }

        public static string MapName
        {
            get => _game?.MapName;
        }

        public static string MapNameFormatted
        {
            get
            {
                var name = Memory.MapName;

                return name switch
                {
                    "factory4_day" or "factory4_night" => "Factory",
                    "bigmap" => "Customs",
                    "RezervBase" => "Reserve",
                    "TarkovStreets" => "Streets of Tarkov",
                    "laboratory" => "The Lab",
                    "Sandbox" or "Sandbox_high" => "Ground Zero",
                    _ => name
                };
            }
        }

        public static ReadOnlyDictionary<string, Player> Players
        {
            get => _game?.Players;
        }

        public static LootManager Loot
        {
            get => _game?.Loot;
        }

        public static ReadOnlyCollection<Grenade> Grenades
        {
            get => _game?.Grenades;
        }

        public static bool LoadingLoot
        {
            get => _game?.LoadingLoot ?? false;
        }

        public static ReadOnlyCollection<Exfil> Exfils
        {
            get => _game?.Exfils;
        }

        public static PlayerManager PlayerManager
        {
            get => _game?.PlayerManager;
        }

        public static QuestManager QuestManager
        {
            get => _game?.QuestManager;
        }

        public static CameraManager CameraManager
        {
            get => _game?.CameraManager;
        }

        public static Toolbox Toolbox
        {
            get => _game?.Toolbox;
        }

        public static Chams Chams
        {
            get => _game?.Chams;
        }

        public static ReadOnlyCollection<PlayerCorpse> Corpses
        {
            get => _game?.Corpses;
        }

        public static Player LocalPlayer
        {
            get
            {
                var game = Memory._game;
                if (game?.Players == null)
                {
                    return null;
                }

                return game.Players.FirstOrDefault((KeyValuePair<string, Player> x) => x.Value.Type == PlayerType.LocalPlayer).Value;
            }
        }
        #endregion

        #region Startup
        /// <summary>
        /// Constructor
        /// </summary>
        static Memory()
        {
            try
            {
                Program.Log("Loading memory module...");

                if (!File.Exists("mmap.txt"))
                {
                    Program.Log("No MemMap, attempting to generate...");
                    GenerateMMap();
                }
                else
                {
                    Program.Log("MemMap found, loading...");
                    vmmInstance = new Vmm("-printf", "-v", "-device", "fpga", "-memmap", "mmap.txt");
                }

                InitiateMemoryWorker();
            }
            catch (Exception ex)
            {
                try
                {
                    Program.Log("attempting to regenerate mmap...");
                    
                    if (File.Exists("mmap.txt"))
                        File.Delete("mmap.txt");

                    GenerateMMap();
                    InitiateMemoryWorker();
                }
                catch
                {
                    MessageBox.Show(ex.ToString(), "DMA Init", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }
            }
        }

        private static void InitiateMemoryWorker()
        {
            Program.Log("Starting Memory worker thread...");
            Memory.StartMemoryWorker();
            Program.HideConsole();
            Memory._tickSw.Start();
        }

        private static void GenerateMMap()
        {
            vmmInstance = new Vmm("-printf", "-v", "-device", "fpga", "-waitinitialize");
            GetMemMap();
        }

        /// <summary>
        /// Generates a Physical Memory Map (mmap.txt) to enhance performance/safety.
        /// </summary>
        private static void GetMemMap()
        {
            try
            {
                var map = vmmInstance.Map_GetPhysMem();
                if (map.Length == 0) throw new Exception("Map_GetPhysMem() returned no entries!");
                var sb = new StringBuilder();
                for (int i = 0; i < map.Length; i++)
                {
                    sb.AppendLine($"{i.ToString("D4")}  {map[i].pa.ToString("x")}  -  {(map[i].pa + map[i].cb - 1).ToString("x")}  ->  {map[i].pa.ToString("x")}");
                }
                File.WriteAllText("mmap.txt", sb.ToString());
            }
            catch (Exception ex)
            {
                throw new DMAException("Unable to generate MemMap!", ex);
            }
        }

        /// <summary>
        /// Gets EFT Process ID.
        /// </summary>
        private static bool GetPid()
        {
            try
            {
                ThrowIfDMAShutdown();
                if (!vmmInstance.PidGetFromName("EscapeFromTarkov.exe", out _pid))
                    throw new DMAException("Unable to obtain PID. Game is not running.");
                else
                {
                    Program.Log($"EscapeFromTarkov.exe is running at PID {_pid}");
                    return true;
                }
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Program.Log($"ERROR getting PID: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets module base entry address for UnityPlayer.dll
        /// </summary>
        private static bool GetModuleBase()
        {
            try
            {
                ThrowIfDMAShutdown();
                _unityBase = vmmInstance.ProcessGetModuleBase(_pid, "UnityPlayer.dll");
                if (_unityBase == 0) throw new DMAException("Unable to obtain Base Module Address. Game may not be running");
                else
                {
                    Program.Log($"Found UnityPlayer.dll at 0x{_unityBase.ToString("x")}");
                    return true;
                }
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Program.Log($"ERROR getting module base: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Returns the Module Base Address of mono-2.0-bdwgc.dll
        /// </summary>
        /// <returns>Module Base Address of mono-2.0-bdwgc.dll</returns>
        /// <exception cref="NotImplementedException"></exception>
        public static ulong GetMonoModule()
        {
            ulong monoBase = 0;
            try
            {
                ThrowIfDMAShutdown();
                monoBase = vmmInstance.ProcessGetModuleBase(_pid, "mono-2.0-bdwgc.dll");
                if (monoBase == 0) throw new DMAException("Unable to obtain Module Base Address. Game may not be running");
                else
                {
                    Program.Log($"Found mono-2.0-bdwgc.dll at 0x{monoBase:x}");
                    return monoBase;
                }
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                Program.Log($"ERROR getting module base: {ex}");
            }
            return monoBase;
        }
        #endregion

        #region MemoryThread
        private static void StartMemoryWorker()
        {
            if (Memory._workerThread is not null && Memory._workerThread.IsAlive)
            {
                return;
            }

            Memory._workerCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = Memory._workerCancellationTokenSource.Token;

            Memory._workerThread = new Thread(() => Memory.MemoryWorkerThread(cancellationToken))
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            Memory._running = true;
            Memory._workerThread.Start();
        }

        public static async void StopMemoryWorker()
        {
            await Task.Run(() =>
            {
                if (Memory._workerCancellationTokenSource is not null)
                {
                    Memory._workerCancellationTokenSource.Cancel();
                    Memory._workerCancellationTokenSource.Dispose();
                    Memory._workerCancellationTokenSource = null;
                }

                if (Memory._workerThread is not null)
                {
                    Memory._workerThread.Join();
                    Memory._workerThread = null;
                }
            });
        }

        private static void MemoryWorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Memory.MemoryWorker();
                }
                catch { }

            }
            Program.Log("[Memory] Refresh thread stopped.");
        }

        /// <summary>
        /// Main worker to perform DMA Reads on.
        /// </summary>
        private static void MemoryWorker()
        {
            try
            {
                while (true)
                {
                    Program.Log("Attempting to find EFT Process...");
                    while (!Memory.GetPid() || !Memory.GetModuleBase())
                    {
                        Program.Log("EFT startup failed, trying again in 15 seconds...");
                        Memory.GameStatus = Game.GameStatus.NotFound;
                        Thread.Sleep(15000);
                    }
                    Program.Log("EFT process located! Startup successful.");
                    while (true)
                    {
                        Memory._game = new Game(Memory._unityBase);
                        Player.Reset();
                        try
                        {
                            Program.Log("Ready -- Waiting for raid...");
                            Memory.GameStatus = Game.GameStatus.Menu;
                            Memory._ready = true;
                            Memory._game.WaitForGame();
                            while (Memory.GameStatus == Game.GameStatus.InGame)
                            {
                                if (Memory._tickSw.ElapsedMilliseconds >= 1000)
                                {
                                    Memory._ticks = _ticksCounter;
                                    Memory._ticksCounter = 0;
                                    Memory._tickSw.Restart();
                                }
                                else
                                {
                                    Memory._ticksCounter++;
                                }

                                if (Memory._restart)
                                {
                                    Memory.GameStatus = Game.GameStatus.Menu;
                                    Program.Log("Restarting game... getting fresh GameWorld instance");
                                    Memory._restart = false;
                                    break;
                                }

                                Memory._game.GameLoop();
                                Thread.SpinWait(1000);
                            }
                        }
                        catch (GameNotRunningException) { break; }
                        catch (ThreadInterruptedException) { throw; }
                        catch (DMAShutdown) { throw; }
                        catch (Exception ex)
                        {
                            Program.Log($"CRITICAL ERROR in Game Loop: {ex}");
                        }
                        finally
                        {
                            Memory._ready = false;
                            Thread.Sleep(100);
                        }
                    }
                    Program.Log("Game is no longer running! Attempting to restart...");
                }
            }
            catch (ThreadInterruptedException) { }
            catch (DMAShutdown) { }
            catch (Exception ex)
            {
                Environment.FailFast($"FATAL ERROR on Memory Thread: {ex}");
            }
            finally
            {
                Program.Log("Uninitializing DMA Device...");
                Memory.vmmInstance.Dispose();
                Program.Log("Memory Thread closing down gracefully...");
            }
        }
        #endregion

        #region ScatterRead
        /// <summary>
        /// (Base)
        /// Performs multiple reads in one sequence, significantly faster than single reads.
        /// Designed to run without throwing unhandled exceptions, which will ensure the maximum amount of
        /// reads are completed OK even if a couple fail.
        /// </summary>
        /// <param name="pid">Process ID to read from.</param>
        /// <param name="entries">Scatter Read Entries to read from for this round.</param>
        /// <param name="useCache">Use caching for this read (recommended).</param>
        internal static void ReadScatter(ReadOnlySpan<IScatterEntry> entries)
        {
            var pagesToRead = new HashSet<ulong>(); // Will contain each unique page only once to prevent reading the same page multiple times
            foreach (var entry in entries) // First loop through all entries - GET INFO
            {
                if (entry is null)
                    continue;
                // Parse Address and Size properties
                ulong addr = entry.ParseAddr();
                uint size = (uint)entry.ParseSize();

                // INTEGRITY CHECK - Make sure the read is valid
                if (addr == 0x0 || size == 0)
                {
                    entry.IsFailed = true;
                    continue;
                }
                // location of object
                ulong readAddress = addr + entry.Offset;
                // get the number of pages
                uint numPages = ADDRESS_AND_SIZE_TO_SPAN_PAGES(readAddress, size);
                ulong basePage = PAGE_ALIGN(readAddress);

                //loop all the pages we would need
                for (int p = 0; p < numPages; p++)
                {
                    ulong page = basePage + PAGE_SIZE * (uint)p;
                    pagesToRead.Add(page);
                }
            }
            var scatters = vmmInstance.MemReadScatter(_pid, Vmm.FLAG_NOCACHE, pagesToRead.ToArray()); // execute scatter read

            foreach (var entry in entries) // Second loop through all entries - PARSE RESULTS
            {
                if (entry is null || entry.IsFailed) // Skip this entry, leaves result as null
                    continue;

                ulong readAddress = (ulong)entry.Addr + entry.Offset; // location of object
                uint pageOffset = BYTE_OFFSET(readAddress); // Get object offset from the page start address

                uint size = (uint)(int)entry.Size;
                var buffer = new byte[size]; // Alloc result buffer on heap
                int bytesCopied = 0; // track number of bytes copied to ensure nothing is missed
                uint cb = Math.Min(size, (uint)PAGE_SIZE - pageOffset); // bytes to read this page

                uint numPages = ADDRESS_AND_SIZE_TO_SPAN_PAGES(readAddress, size); // number of pages to read from (in case result spans multiple pages)
                ulong basePage = PAGE_ALIGN(readAddress);

                for (int p = 0; p < numPages; p++)
                {
                    ulong page = basePage + PAGE_SIZE * (uint)p; // get current page addr
                    var scatter = scatters.FirstOrDefault(x => x.qwA == page); // retrieve page of mem needed
                    if (scatter.f) // read succeeded -> copy to buffer
                    {
                        scatter.pb
                            .AsSpan((int)pageOffset, (int)cb)
                            .CopyTo(buffer.AsSpan(bytesCopied, (int)cb)); // Copy bytes to buffer
                        bytesCopied += (int)cb;
                    }
                    else // read failed -> set failed flag
                    {
                        entry.IsFailed = true;
                        break;
                    }

                    cb = (uint)PAGE_SIZE; // set bytes to read next page
                    if (bytesCopied + cb > size) // partial chunk last page
                        cb = size - (uint)bytesCopied;

                    pageOffset = 0x0; // Next page (if any) should start at 0x0
                }
                if (bytesCopied != size)
                    entry.IsFailed = true;
                entry.SetResult(buffer);
            }
        }
        #endregion

        #region ReadMethods
        /// <summary>
        /// Read memory into a Span.
        /// </summary>
        public static Span<byte> ReadBuffer(ulong addr, int size)
        {
            if ((uint)size > PAGE_SIZE * 1500) throw new DMAException("Buffer length outside expected bounds!");
            ThrowIfDMAShutdown();
            var buf = vmmInstance.MemRead(_pid, addr, (uint)size, Vmm.FLAG_NOCACHE);
            if (buf.Length != size) throw new DMAException("Incomplete memory read!");
            return buf;
        }

        /// <summary>
        /// Read a chain of pointers and get the final result.
        /// </summary>
        public static ulong ReadPtrChain(ulong ptr, uint[] offsets)
        {
            ulong addr = 0;
            try { addr = ReadPtr(ptr + offsets[0]); }
            catch (Exception ex) { throw new DMAException($"ERROR reading pointer chain at index 0, addr 0x{ptr.ToString("X")} + 0x{offsets[0].ToString("X")}", ex); }
            for (int i = 1; i < offsets.Length; i++)
            {
                try { addr = ReadPtr(addr + offsets[i]); }
                catch (Exception ex) { throw new DMAException($"ERROR reading pointer chain at index {i}, addr 0x{addr.ToString("X")} + 0x{offsets[i].ToString("X")}", ex); }
            }
            return addr;
        }
        /// <summary>
        /// Resolves a pointer and returns the memory address it points to.
        /// </summary>
        public static ulong ReadPtr(ulong ptr)
        {
            var addr = ReadValue<ulong>(ptr);
            if (addr == 0x0) throw new NullPtrException();
            else return addr;
        }

        /// <summary>
        /// Resolves a pointer and returns the memory address it points to.
        /// </summary>
        public static ulong ReadPtrNullable(ulong ptr)
        {
            var addr = ReadValue<ulong>(ptr);
            return addr;
        }

        /// <summary>
        /// Read value type/struct from specified address.
        /// </summary>
        /// <typeparam name="T">Specified Value Type.</typeparam>
        /// <param name="addr">Address to read from.</param>
        public static T ReadValue<T>(ulong addr)
            where T : struct
        {
            try
            {
                int size = Marshal.SizeOf(typeof(T));
                ThrowIfDMAShutdown();
                var buf = vmmInstance.MemRead(_pid, addr, (uint)size, Vmm.FLAG_NOCACHE);
                return MemoryMarshal.Read<T>(buf);
            }
            catch (Exception ex)
            {
                throw new DMAException($"ERROR reading {typeof(T)} value at 0x{addr.ToString("X")}", ex);
            }
        }

        /// <summary>
        /// Read null terminated string.
        /// </summary>
        /// <param name="length">Number of bytes to read.</param>
        /// <exception cref="DMAException"></exception>
        public static string ReadString(ulong addr, uint length) // read n bytes (string)
        {
            try
            {
                if (length > PAGE_SIZE) throw new DMAException("String length outside expected bounds!");
                ThrowIfDMAShutdown();
                var buf = vmmInstance.MemRead(_pid, addr, length, Vmm.FLAG_NOCACHE);
                return Encoding.Default.GetString(buf).Split('\0')[0];
            }
            catch (Exception ex)
            {
                throw new DMAException($"ERROR reading string at 0x{addr.ToString("X")}", ex);
            }
        }

        /// <summary>
        /// Read UnityEngineString structure
        /// </summary>
        public static string ReadUnityString(ulong addr)
        {
            try
            {
                var length = (uint)ReadValue<int>(addr + Offsets.UnityString.Length);
                if (length > PAGE_SIZE) throw new DMAException("String length outside expected bounds!");
                ThrowIfDMAShutdown();
                var buf = vmmInstance.MemRead(_pid, addr + Offsets.UnityString.Value, length * 2, Vmm.FLAG_NOCACHE);
                return Encoding.Unicode.GetString(buf).TrimEnd('\0'); ;
            }
            catch (Exception ex)
            {
                throw new DMAException($"ERROR reading UnityString at 0x{addr.ToString("X")}", ex);
            }
        }
        #endregion

        #region WriteMethods

        /// <summary>
        /// (Base)
        /// Write value type/struct to specified address.
        /// </summary>
        /// <typeparam name="T">Value Type to write.</typeparam>
        /// <param name="pid">Process ID to write to.</param>
        /// <param name="addr">Virtual Address to write to.</param>
        /// <param name="value"></param>
        /// <exception cref="DMAException"></exception>
        public static void WriteValue<T>(ulong addr, T value)
            where T : unmanaged
        {
            try
            {
                if (!vmmInstance.MemWriteStruct(_pid, addr, value))
                    throw new Exception("Memory Write Failed!");
            }
            catch (Exception ex)
            {
                throw new DMAException($"[DMA] ERROR writing {typeof(T)} value at 0x{addr.ToString("X")}", ex);
            }
        }

        /// <summary>
        /// Performs multiple memory write operations in a single call
        /// </summary>
        /// <param name="entries">A collection of entries defining the memory writes.</param>
        public static void WriteScatter(IEnumerable<IScatterWriteEntry> entries)
        {
            using (var scatter = vmmInstance.Scatter_Initialize(_pid, Vmm.FLAG_NOCACHE))
            {
                if (scatter == null)
                    throw new InvalidOperationException("Failed to initialize scatter.");

                foreach (var entry in entries)
                {
                    bool success = entry switch
                    {
                        IScatterWriteDataEntry<int> intEntry => scatter.PrepareWriteStruct(intEntry.Address, intEntry.Data),
                        IScatterWriteDataEntry<float> floatEntry => scatter.PrepareWriteStruct(floatEntry.Address, floatEntry.Data),
                        IScatterWriteDataEntry<ulong> ulongEntry => scatter.PrepareWriteStruct(ulongEntry.Address, ulongEntry.Data),
                        IScatterWriteDataEntry<bool> boolEntry => scatter.PrepareWriteStruct(boolEntry.Address, boolEntry.Data),
                        IScatterWriteDataEntry<byte> byteEntry => scatter.PrepareWriteStruct(byteEntry.Address, byteEntry.Data),
                        _ => throw new NotSupportedException($"Unsupported data type: {entry.GetType()}")
                    };

                    if (!success)
                    {
                        Program.Log($"Failed to prepare scatter write for address: {entry.Address}");
                        continue;
                    }
                }

                if (!scatter.Execute())
                    throw new Exception("Scatter write execution failed.");

                scatter.Close();
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sets restart flag to re-initialize the game/pointers from the bottom up.
        /// </summary>
        public static void Restart()
        {
            if (InGame)
            {
                _restart = true;
            }
        }

        /// <summary>
        /// Refresh loot only.
        /// </summary>
        public static void RefreshLoot()
        {
            _game?.RefreshLoot();
        }
        /// <summary>
        /// Close down DMA Device Connection.
        /// </summary>
        public static void Shutdown()
        {
            if (_running)
            {
                Program.Log("Closing down Memory Thread...");
                _running = false;
                Memory.StopMemoryWorker();
            }
        }

        private static void ThrowIfDMAShutdown()
        {
            if (!_running) throw new DMAShutdown("Memory Thread/DMA is shutting down!");
        }


        /// Mem Align Functions Ported from Win32 (C Macros)
        private const ulong PAGE_SIZE = 0x1000;
        private const int PAGE_SHIFT = 12;

        /// <summary>
        /// The PAGE_ALIGN macro takes a virtual address and returns a page-aligned
        /// virtual address for that page.
        /// </summary>
        private static ulong PAGE_ALIGN(ulong va)
        {
            return (va & ~(PAGE_SIZE - 1));
        }
        /// <summary>
        /// The ADDRESS_AND_SIZE_TO_SPAN_PAGES macro takes a virtual address and size and returns the number of pages spanned by the size.
        /// </summary>
        private static uint ADDRESS_AND_SIZE_TO_SPAN_PAGES(ulong va, uint size)
        {
            return (uint)((BYTE_OFFSET(va) + (size) + (PAGE_SIZE - 1)) >> PAGE_SHIFT);
        }

        /// <summary>
        /// The BYTE_OFFSET macro takes a virtual address and returns the byte offset
        /// of that address within the page.
        /// </summary>
        private static uint BYTE_OFFSET(ulong va)
        {
            return (uint)(va & (PAGE_SIZE - 1));
        }
        #endregion
    }

    #region Exceptions
    public class DMAException : Exception
    {
        public DMAException()
        {
        }

        public DMAException(string message)
            : base(message)
        {
        }

        public DMAException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class NullPtrException : Exception
    {
        public NullPtrException()
        {
        }

        public NullPtrException(string message)
            : base(message)
        {
        }

        public NullPtrException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class DMAShutdown : Exception
    {
        public DMAShutdown()
        {
        }

        public DMAShutdown(string message)
            : base(message)
        {
        }

        public DMAShutdown(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    #endregion
}
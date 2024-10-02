using System.Text;
using Vmmsharp;

namespace eft_dma_radar
{
    public class InputManager
    {
        private static bool keyboardInitialized = false;

        private static long lastUpdateTicks = 0;
        private static ulong gafAsyncKeyStateExport;

        private static byte[] currentStateBitmap = new byte[64];
        private static byte[] previousStateBitmap = new byte[64];
        private static readonly HashSet<int> pressedKeys = new HashSet<int>();

        private static Vmm vmmInstance;
        private static VmmProcess winlogon;

        private static int initAttempts = 0;
        private const int MAX_ATTEMPTS = 3;
        private const int DELAY = 500;

        private static int currentBuild;
        private static int updateBuildRevision;

        public static bool IsManagerLoaded => InputManager.keyboardInitialized;

        static InputManager()
        {

        }

        public static void SetVmmInstance(Vmm vmmInstance)
        {
            InputManager.vmmInstance = vmmInstance;
        }

        public static bool InitInputManager()
        {
            while (InputManager.initAttempts < InputManager.MAX_ATTEMPTS)
            {
                if (InputManager.InitKeyboard())
                    return true;

                Thread.Sleep(DELAY);
                Program.Log($"Failed to load keyboard manager. Retrying in {DELAY}ms.");
            }

            Program.Log($"Failed to initialize keyboard manager after {InputManager.MAX_ATTEMPTS} attempts");
            return false;
        }

        private static bool InitKeyboard()
        {
            if (InputManager.keyboardInitialized)
                return true;

            try
            {
                var currentBuild = InputManager.vmmInstance.RegValueRead("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\CurrentBuild", out _);
                InputManager.currentBuild = int.Parse(Encoding.Unicode.GetString(currentBuild));

                var UBR = InputManager.vmmInstance.RegValueRead("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\UBR", out _);
                InputManager.updateBuildRevision = BitConverter.ToInt32(UBR);

                var tmpProcess = InputManager.vmmInstance.Process("winlogon.exe");
                InputManager.winlogon = InputManager.vmmInstance.Process(tmpProcess.PID | Vmm.PID_PROCESS_WITH_KERNELMEMORY);

                if (InputManager.winlogon == null)
                {
                    Program.Log("Winlogon process not found");
                    InputManager.initAttempts++;
                    return false;
                }

                return InputManager.currentBuild > 22000 ? InputManager.InitKeyboardForNewWindows() : InputManager.InitKeyboardForOldWindows();
            }
            catch (Exception ex)
            {
                Program.Log($"Error initializing keyboard: {ex.Message}");
                InputManager.initAttempts++;
                return false;
            }
        }

        private static bool InitKeyboardForNewWindows()
        {
            Program.Log("Windows version > 22000, attempting to read with offset");

            var csrssProcesses = InputManager.vmmInstance.Processes.Where(p => p.Name.Equals("csrss.exe", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var csrss in csrssProcesses)
            {
                try
                {
                    var win32ksgdBase = csrss.GetModuleBase("win32ksgd.sys");

                    if (win32ksgdBase == 0)
                        continue;

                    var gSessionGlobalSlots = win32ksgdBase + 0x3110;
                    var t1 = csrss.MemReadAs<ulong>(gSessionGlobalSlots);
                    var t2 = csrss.MemReadAs<ulong>(t1.Value);
                    var t3 = csrss.MemReadAs<ulong>(t2.Value);
                    var userSessionState = t3.Value;

                    if (userSessionState == 0)
                        continue;

                    InputManager.gafAsyncKeyStateExport = userSessionState + (InputManager.currentBuild >= 22631 && InputManager.updateBuildRevision >= 3810 ? 0x36A8U : 0x3690U);

                    if (InputManager.gafAsyncKeyStateExport > 0x7FFFFFFFFFFF)
                    {
                        InputManager.keyboardInitialized = true;
                        Console.WriteLine("Keyboard handler initialized");
                        return true;
                    }
                }
                catch { }
            }

            InputManager.initAttempts++;
            Program.Log("Failed to initialize keyboard handler for new Windows version");
            return false;
        }

        private static bool InitKeyboardForOldWindows()
        {
            Program.Log("Older Windows version detected, attempting to resolve via EAT");

            var exports = InputManager.winlogon.MapModuleEAT("win32kbase.sys");
            var gafAsyncKeyStateExport = exports.FirstOrDefault(e => e.sFunction == "gafAsyncKeyState");

            if (!string.IsNullOrEmpty(gafAsyncKeyStateExport.sFunction) && gafAsyncKeyStateExport.vaFunction >= 0x7FFFFFFFFFFF)
            {
                InputManager.gafAsyncKeyStateExport = gafAsyncKeyStateExport.vaFunction;
                InputManager.keyboardInitialized = true;
                Program.Log("Resolved export via EAT");
                return true;
            }

            Program.Log("Failed to resolve via EAT, attempting to resolve with PDB");

            var pdb = InputManager.winlogon.Pdb("win32kbase.sys");

            if (pdb != null && pdb.SymbolAddress("gafAsyncKeyState", out ulong gafAsyncKeyState))
            {
                if (gafAsyncKeyState >= 0x7FFFFFFFFFFF)
                {
                    InputManager.gafAsyncKeyStateExport = gafAsyncKeyState;
                    InputManager.keyboardInitialized = true;
                    Program.Log("Resolved export via PDB");
                    return true;
                }
            }

            Program.Log("Failed to find export");
            return false;
        }

        public static unsafe void UpdateKeys()
        {
            if (!InputManager.keyboardInitialized)
                return;

            Array.Copy(InputManager.currentStateBitmap, InputManager.previousStateBitmap, 64);

            fixed (byte* pb = InputManager.currentStateBitmap)
            {
                var success = InputManager.winlogon.MemRead(
                    InputManager.gafAsyncKeyStateExport,
                    pb,
                    64,
                    out _,
                    Vmm.FLAG_NOCACHE
                );

                if (!success)
                    return;

                InputManager.pressedKeys.Clear();

                for (int vk = 0; vk < 256; ++vk)
                {
                    if ((InputManager.currentStateBitmap[(vk * 2 / 8)] & 1 << vk % 4 * 2) != 0)
                        InputManager.pressedKeys.Add(vk);
                }
            }

            InputManager.lastUpdateTicks = DateTime.UtcNow.Ticks;
        }

        public static bool IsKeyDown(Keys key)
        {
            if (!InputManager.keyboardInitialized || InputManager.gafAsyncKeyStateExport < 0x7FFFFFFFFFFF)
                return false;

            if (DateTime.UtcNow.Ticks - InputManager.lastUpdateTicks > TimeSpan.TicksPerMillisecond)
                InputManager.UpdateKeys();

            var virtualKeyCode = (int)key;

            return InputManager.pressedKeys.Contains(virtualKeyCode);
        }

        public static bool IsKeyPressed(Keys key)
        {
            if (!InputManager.keyboardInitialized || InputManager.gafAsyncKeyStateExport < 0x7FFFFFFFFFFF)
                return false;

            if (DateTime.UtcNow.Ticks - InputManager.lastUpdateTicks > TimeSpan.TicksPerMillisecond)
                InputManager.UpdateKeys();

            var virtualKeyCode = (int)key;

            return InputManager.pressedKeys.Contains(virtualKeyCode) &&
                   (InputManager.previousStateBitmap[(virtualKeyCode * 2 / 8)] & (1 << (virtualKeyCode % 4 * 2))) == 0;
        }
    }
}

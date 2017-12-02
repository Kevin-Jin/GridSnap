using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GridSnap
{
    internal class ShortcutListener : IDisposable
    {
        private const int WH_KEYBOARD_LL = 0x000D, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
        private const int HC_ACTION = 0;
        private const uint INPUT_KEYBOARD = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            internal Keys vkCode;
            internal uint scanCode;
            internal uint flags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            internal uint type;
            internal ushort wVk;
            internal ushort wScan;
            internal uint dwFlags;
            internal uint time;
            internal IntPtr dwExtraInfo;

            internal KEYBDINPUT(Keys vkCode, uint scanCode, uint flags, uint time)
            {
                type = INPUT_KEYBOARD;
                wVk = (ushort)vkCode;
                wScan = (ushort)scanCode;
                dwFlags = flags;
                this.time = time;
                dwExtraInfo = IntPtr.Zero;
            }

            internal KEYBDINPUT(Keys vkCode, uint scanCode, uint flags)
                : this(vkCode, scanCode, flags, (uint)Environment.TickCount)
            { }
        }

        private delegate IntPtr LOWLEVELKEYBOARDHOOKPROC(int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LOWLEVELKEYBOARDHOOKPROC lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint numberOfInputs, KEYBDINPUT[] inputs, int sizeOfInputStructure);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        private readonly IntPtr hookHandle;
        private bool hasWin, hasShift;
        private bool[] hasArrow = new bool[4];
        private Model model;
        
        private IntPtr OnLowLevelKeyboardEvent(int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam)
        {
            if (nCode == HC_ACTION)
            {
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    switch (lParam.vkCode)
                    {
                        case Keys.Left:
                        case Keys.Up:
                        case Keys.Right:
                        case Keys.Down:
                            if (hasWin)
                            {
                                hasArrow[lParam.vkCode - Keys.Left] = true;

                                if (!hasShift)
                                    model.Move(lParam.vkCode);
                                else
                                    model.Resize(lParam.vkCode);
                                return (IntPtr)1;
                            }
                            break;
                        case Keys.LShiftKey:
                        case Keys.RShiftKey:
                        case Keys.ShiftKey:
                            hasShift = true;
                            break;
                        case Keys.LWin:
                        case Keys.RWin:
                            hasWin = true;
                            break;
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    switch (lParam.vkCode)
                    {
                        case Keys.Left:
                        case Keys.Up:
                        case Keys.Right:
                        case Keys.Down:
                            if (hasArrow[lParam.vkCode - Keys.Left])
                            {
                                hasArrow[lParam.vkCode - Keys.Left] = false;
                                //return (IntPtr)1;
                            }
                            break;
                        case Keys.LShiftKey:
                        case Keys.RShiftKey:
                        case Keys.ShiftKey:
                            hasShift = false;
                            break;
                        case Keys.LWin:
                        case Keys.RWin:
                            if (!hasWin)
                                throw new Exception();

                            if (hasArrow[0] || hasArrow[1] || hasArrow[2] || hasArrow[3])
                            {
                                // Prevent start menu from showing up if Win key is released before the arrow key is.
                                // TODO: this still doesn't work perfectly.
                                //SendInput(1, new KEYBDINPUT[] { new KEYBDINPUT(Keys.Escape, 1, 0) }, Marshal.SizeOf(typeof(KEYBDINPUT)));
                                //System.Threading.Thread.Sleep(10);
                                //SendInput(1, new KEYBDINPUT[] { new KEYBDINPUT(Keys.Escape, 1, 2) }, Marshal.SizeOf(typeof(KEYBDINPUT)));
                            }
                            hasWin = false;
                            break;
                    }
                }
            }

            return CallNextHookEx(hookHandle, nCode, wParam, ref lParam);
        }

        internal ShortcutListener(Model m)
        {
            model = m;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                // Marshal.GetHINSTANCE(GetType().Module) == GetModuleHandle(curModule.ModuleName)
                hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, OnLowLevelKeyboardEvent, GetModuleHandle(curModule.ModuleName), 0);
                if (hookHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != 0)
                        throw new Win32Exception(err);
                }
            }
        }

        public void Dispose()
        {
            // Make the listener RAII-esque in the context of a "using" block.
            UnhookWindowsHookEx(hookHandle);
        }
    }

    internal static class Controller
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        private static Model m;

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            m.LogMessage(e.Exception.ToString(), true);
            m.Stop();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            m.LogMessage(e.ExceptionObject.ToString(), true);
            m.Stop();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            m.Stop();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        internal static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            //AllocConsole();
            m = new Model();
            // Default handler for exceptions on UI thread (recoverable).
            Application.ThreadException += Application_ThreadException;
            // Default handler for all other exceptions (non-recoverable).
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            // Graceful shutdown hook.
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            m.Start();
            using (new ShortcutListener(m))
                Application.Run(new View(m));
        }
    }
}

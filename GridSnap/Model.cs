using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GridSnap
{
    internal class ScreenSettings
    {
        private int gridRows, gridCols;
        private Rectangle screenArea;

        private int gridCellHeight, gridCellWidth;
        private int gridOddRow, gridOddCol;

        internal int GridRows
        {
            get { return gridRows; }
            set { gridRows = value; gridCellHeight = screenArea.Height / gridRows; gridOddRow = screenArea.Height % gridRows; }
        }

        internal int GridCols
        {
            get { return gridCols; }
            set { gridCols = value; gridCellWidth = screenArea.Width / gridCols; gridOddCol = screenArea.Width % gridCols; }
        }

        internal Rectangle ScreenArea { get { return screenArea; } }

        internal int GridCellWidth { get { return gridCellWidth; } }
        internal int GridCellHeight { get { return gridCellHeight; } }

        internal int GridOddRow { get { return gridOddRow; } }
        internal int GridOddCol { get { return gridOddCol; } }

        private void Reset(Rectangle newScreenArea)
        {
            screenArea = newScreenArea;
            GridRows = 3;
            GridCols = 3;
        }

        internal bool Refresh(Screen screen)
        {
            Rectangle newScreenArea = screen.WorkingArea;
            if (gridRows != 0 && gridCols != 0 && screenArea.Equals(newScreenArea))
            {
                return false;
            }
            else
            {
                Reset(newScreenArea);
                return true;
            }
        }

        internal int GridCellXOffset(int screenX)
        {
            // Ensure the left operand of the remainder operation is positive by adding
            //  some multiple of GridCellWidth to screenX.
            return (screenX + GridCellWidth * (GridCols + 1)) % GridCellWidth;
        }

        internal int GridCellYOffset(int screenY)
        {
            // Ensure the left operand of the remainder operation is positive by adding
            //  some multiple of GridCellHeight to screenY.
            return (screenY + GridCellHeight * (GridRows + 1)) % GridCellHeight;
        }
    }

    internal class Model
    {
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int SW_SHOWNORMAL = 1, SW_SHOWMAXIMIZED = 3;
        private const int S_OK = 0;
        private const int ERROR_INVALID_HANDLE = 6;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;

            public override string ToString()
            {
                return "{left=" + left + ",top=" + top + ",right=" + right + ",bottom=" + bottom + "}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            internal int x;
            internal int y;

            public override string ToString()
            {
                return "{x=" + x + ",y=" + y + "}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            internal uint length;
            internal uint flags;
            internal uint showCmd;
            internal POINT ptMinPosition;
            internal POINT ptMaxPosition;
            internal RECT rcNormalPosition;

            public override string ToString()
            {
                return "{length=" + length + ",flags=" + flags + ",showCmd=" + showCmd + ",ptMinPosition=" + ptMinPosition + ",ptMaxPosition=" + ptMaxPosition + ",rcNormalPosition=" + rcNormalPosition + "}";
            }
        }

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern uint DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        
        private Dictionary<string, ScreenSettings> screenSettings;
        private TextWriter log;

        internal Model()
        {
            screenSettings = new Dictionary<string, ScreenSettings>();
        }

        internal void LogMessage(string message, bool flush = false)
        {
            log.WriteLine("[" + DateTime.Now.ToString("yy-MM-dd HH:mm:ss") + "] " + message);
            if (flush)
                log.Flush();
        }

        internal void Start()
        {
            log = new StreamWriter(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "log.txt"), true);
            LogMessage("Started", true);
        }

        internal void Stop()
        {
            LogMessage("Stopped", true);
            log.Dispose();
        }

        internal ScreenSettings GetScreenSettings(Screen s)
        {
            if (!screenSettings.TryGetValue(s.DeviceName, out ScreenSettings settings))
            {
                settings = new ScreenSettings();
                screenSettings[s.DeviceName] = settings;
            }
            settings.Refresh(s);
            return settings;
        }

        private RECT GetMargins(IntPtr window, RECT rect)
        {
            try
            {
                var frame = new RECT();
                // In all versions of Windows, there is a 7px border around the client area of each non-maximized window.
                // Windows 10 also reports the 7px and allows the user to hover the mouse over it in order to resize
                //  the window, except the border is invisible to the user.
                // Make the window slightly larger so that the user doesn't see gaps between the window and screen edges.
                if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, ref frame, Marshal.SizeOf(typeof(RECT))) == S_OK)
                {
                    rect.left -= frame.left;
                    rect.top -= frame.top;
                    rect.right -= frame.right;
                    rect.bottom -= frame.bottom;
                }
                else
                {
                    if (Marshal.GetLastWin32Error() == ERROR_INVALID_HANDLE)
                        // Set all margins to 0 for versions of Windows that don't support DWMWA_EXTENDED_FRAME_BOUNDS.
                        rect.left = rect.top = rect.right = rect.bottom = 0;
                    else
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Set all margins to 0 for versions of Windows that don't support DwmGetWindowAttribute().
                rect.left = rect.top = rect.right = rect.bottom = 0;
            }
            return rect;
        }

        private int MoveLeft(ScreenSettings settings, int x)
        {
            // If window is currently snapped to the edge of a grid cell, make sure to snap it to the
            //  adjacent grid cell on the left.
            x -= 1 + settings.GridOddCol;
            // Get x coordinate of the grid cell the window should be snapped to.
            x -= settings.GridCellXOffset(x);
            return x;
        }

        private int MoveUp(ScreenSettings settings, int y)
        {
            // If window is currently snapped to the edge of a grid cell, make sure to snap it to the
            //  adjacent grid cell on the top.
            y -= 1 + settings.GridOddRow;
            // Get y coordinate of the grid cell the window should be snapped to.
            y -= settings.GridCellYOffset(y);
            return y;
        }

        private int MoveRight(ScreenSettings settings, int x)
        {
            // Get x coordinate after moving the window one grid cell to the right.
            x += settings.GridCellWidth;
            // Get x coordinate of the grid cell the window should be snapped to.
            x -= settings.GridCellXOffset(x);
            return x;
        }

        private int MoveDown(ScreenSettings settings, int y)
        {
            // Get y coordinate after moving the window one grid cell to the bottom.
            y += settings.GridCellHeight;
            // Get y coordinate of the grid cell the window should be snapped to.
            y -= settings.GridCellYOffset(y);
            return y;
        }

        private int SnapLeft(ScreenSettings settings, int x = 0)
        {
            return x;
        }

        private int SnapUp(ScreenSettings settings, int y = 0)
        {
            return y;
        }

        private int SnapRight(ScreenSettings settings, int x = 0)
        {
            // Set left edge of window to to an off-screen grid cell at the right edge of screen.
            x += settings.ScreenArea.Width;
            x = MoveLeft(settings, x);
            return x;
        }

        private int SnapDown(ScreenSettings settings, int y = 0)
        {
            // Set top edge of window to to an off-screen grid cell at the bottom edge of screen.
            y += settings.ScreenArea.Height;
            y = MoveUp(settings, y);
            return y;
        }

        private void MoveLeft(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int x;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get x coordinate of left edge of the window, relative to the left edge of its screen.
                    x = rect.left - margins.left - settings.ScreenArea.Left;
                    x = MoveLeft(settings, x);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to leftmost grid cell on the screen.
                    x = SnapLeft(settings);
                    break;
                default:
                    throw new Exception();
            }

            if (x < 0)
            {
                // Window has moved to the adjacent screen on the left.
                var adjScreen = Screen.FromPoint(new Point(settings.ScreenArea.Left - 1, (settings.ScreenArea.Top + settings.ScreenArea.Bottom) / 2));
                if (adjScreen.Equals(Screen.FromHandle(window)))
                {
                    // No more screens to the left. Keep the window on the same screen.
                    x = SnapLeft(settings);
                }
                else
                {
                    // Snap the window to the rightmost cell of the adjacent screen on the left.
                    settings = GetScreenSettings(adjScreen);
                    x = SnapRight(settings);
                }
            }

            rect.left = settings.ScreenArea.Left + x + margins.left;
            rect.right = rect.left + settings.GridCellWidth - margins.left + margins.right;
            // Expand the window to take the full height of the screen.
            //rect.top = settings.ScreenArea.Top + margins.top;
            //rect.bottom = settings.ScreenArea.Height - margins.top + margins.bottom;
        }

        private void MoveUp(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int y;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get y coordinate of top edge of the window, relative to the top edge of its screen.
                    y = rect.top - margins.top - settings.ScreenArea.Top;
                    y = MoveUp(settings, y);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to topmost grid cell on the screen.
                    y = SnapUp(settings);
                    break;
                default:
                    throw new Exception();
            }
            
            if (y < 0)
            {
                // Window has moved to the adjacent screen on the top.
                var adjScreen = Screen.FromPoint(new Point((settings.ScreenArea.Left + settings.ScreenArea.Right) / 2, settings.ScreenArea.Top - 1));
                if (adjScreen.Equals(Screen.FromHandle(window)))
                {
                    // No more screens to the top. Keep the window on the same screen.
                    y = SnapUp(settings);
                }
                else
                {
                    // Snap the window to the bottommost cell of the adjacent screen on the top.
                    settings = GetScreenSettings(adjScreen);
                    y = SnapDown(settings);
                }
            }

            rect.top = settings.ScreenArea.Top + y + margins.top;
            rect.bottom = rect.top + settings.GridCellHeight - margins.top + margins.bottom;
            // Expand the window to take the full width of the screen.
            //rect.left = settings.ScreenArea.Left + margins.left;
            //rect.right = settings.ScreenArea.Width - margins.left + margins.right;
        }

        private void MoveRight(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int x;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get x coordinate of left edge of the window, relative to the left edge of its screen.
                    x = rect.left - margins.left - settings.ScreenArea.Left;
                    x = MoveRight(settings, x);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to rightmost grid cell on the screen.
                    x = SnapRight(settings);
                    break;
                default:
                    throw new Exception();
            }

            if (x >= settings.ScreenArea.Width - settings.GridOddCol)
            {
                // Window has moved to the adjacent screen on the right.
                var adjScreen = Screen.FromPoint(new Point(settings.ScreenArea.Right + 1, (settings.ScreenArea.Top + settings.ScreenArea.Bottom) / 2));
                if (adjScreen.Equals(Screen.FromHandle(window)))
                {
                    // No more screens to the right. Keep the window on the same screen.
                    x = SnapRight(settings);
                }
                else
                {
                    // Snap the window to the leftmost cell of the adjacent screen on the right.
                    settings = GetScreenSettings(adjScreen);
                    x = SnapLeft(settings);
                }
            }

            rect.left = settings.ScreenArea.Left + x + margins.left;
            rect.right = rect.left + settings.GridCellWidth - margins.left + margins.right;
            // Expand the window to take the full height of the screen.
            //rect.top = settings.ScreenArea.Top + margins.top;
            //rect.bottom = settings.ScreenArea.Height - margins.top + margins.bottom;
        }

        private void MoveDown(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int y;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get y coordinate of top edge of the window, relative to the top edge of its screen.
                    y = rect.top - margins.top - settings.ScreenArea.Top;
                    y = MoveDown(settings, y);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to bottommost grid cell on the screen.
                    y = SnapDown(settings);
                    break;
                default:
                    throw new Exception();
            }
            
            if (y >= settings.ScreenArea.Height - settings.GridOddRow)
            {
                // Window has moved to the adjacent screen on the bottom.
                var adjScreen = Screen.FromPoint(new Point((settings.ScreenArea.Left + settings.ScreenArea.Right) / 2, settings.ScreenArea.Bottom + 1));
                if (adjScreen.Equals(Screen.FromHandle(window)))
                {
                    // No more screens to the bottom. Keep the window on the same screen.
                    y = SnapDown(settings);
                }
                else
                {
                    // Snap the window to the topmost cell of the adjacent screen on the bottom.
                    settings = GetScreenSettings(adjScreen);
                    y = SnapUp(settings);
                }
            }

            rect.top = settings.ScreenArea.Top + y + margins.top;
            rect.bottom = rect.top + settings.GridCellHeight - margins.top + margins.bottom;
            // Expand the window to take the full height of the screen.
            //rect.top = settings.ScreenArea.Top + margins.top;
            //rect.bottom = settings.ScreenArea.Height - margins.top + margins.bottom;
        }

        internal void Move(Keys dir)
        {
            var window = GetForegroundWindow();

            var wndPl = new WINDOWPLACEMENT();
            GetWindowPlacement(window, ref wndPl);
            if (wndPl.showCmd == SW_SHOWMAXIMIZED)
            {
                // If the window is maximized, restore it right now so that
                //  its borders aren't messed up and the restore button becomes a maximize button.
                ShowWindow(window, SW_SHOWNORMAL);
            }

            // Can't use wndPl.rcNormalPosition because it misbehaves when window was aero snapped.
            // Additionally, rcNormalPosition gives the working area position whereas GetWindowRect()
            //  gets the screen position. We want the screen position when using MoveWindow().
            // Screen position and working area position can differ when the taskbar is docked to the
            //  left, or the top, edge of the screen.
            var rect = new RECT();
            GetWindowRect(window, ref rect);
            var margins = GetMargins(window, rect);
            var settings = GetScreenSettings(Screen.FromHandle(window));

            switch (dir)
            {
                case Keys.Left:
                    MoveLeft(window, wndPl, ref rect, margins, settings);
                    break;
                case Keys.Up:
                    MoveUp(window, wndPl, ref rect, margins, settings);
                    break;
                case Keys.Right:
                    MoveRight(window, wndPl, ref rect, margins, settings);
                    break;
                case Keys.Down:
                    MoveDown(window, wndPl, ref rect, margins, settings);
                    break;
            }

            // TODO: figure out how aero snap makes wndPl.rcNormalPosition sticky despite the window being repositioned and resized.
            //  https://social.msdn.microsoft.com/Forums/vstudio/en-US/1c3b7b79-8bb7-4824-95f9-e70f215e9313/remembering-window-placements-and-aero-snap?forum=wpf
            //  https://stackoverflow.com/questions/18313673/programmatically-maximize-window-on-half-of-screen
            //  https://stackoverflow.com/questions/8368540/save-and-restore-aero-snap-position-on-windows-7
            //  https://stackoverflow.com/questions/5673242/getwindowplacement-gives-the-incorrect-window-position
            // It seems like once aero snap is used once on a window, any further resizes on that window will not overwrite
            //  wndPl.rcNormalPosition. Maybe we can just aero snap the window wherever first (e.g. SendInput Win+Left) and then
            //  move the window to where we want it to go.
            // Observe rcNormalPosition on windows snapped by AquaSnap and see if AquaSnap merely remembers rcNormalPosition itself
            //  or actually freezes it like Aero Snap does.

            // "By convention, the right and bottom edges of the rectangle are normally considered exclusive."
            MoveWindow(window, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, true);
        }

        private void ShrinkLeft(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int x;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get x coordinate of right edge of the window, relative to the left edge of its screen.
                    x = rect.right - margins.right - settings.ScreenArea.Left;
                    x = MoveLeft(settings, x);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to second-rightmost grid cell on the screen.
                    x = SnapRight(settings);
                    break;
                default:
                    throw new Exception();
            }
            
            if ((rect.right - margins.right) - (rect.left - margins.left) <= settings.GridCellWidth)
            {
                // Make sure the window has a positive width.
                return;
            }

            rect.right = settings.ScreenArea.Left + x + margins.right;
        }

        private void ShrinkUp(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int y;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get y coordinate of bottom edge of the window, relative to the top edge of its screen.
                    y = rect.bottom - margins.bottom - settings.ScreenArea.Top;
                    y = MoveUp(settings, y);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to second-bottommost grid cell on the screen.
                    y = SnapDown(settings);
                    break;
                default:
                    throw new Exception();
            }

            if ((rect.bottom - margins.bottom) - (rect.top - margins.top) <= settings.GridCellHeight)
            {
                // Make sure the window has a positive height.
                return;
            }

            rect.bottom = settings.ScreenArea.Top + y + margins.bottom;
        }

        private void ExpandRight(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int x;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get x coordinate of right edge of the window, relative to the left edge of its screen.
                    x = rect.right - margins.right - settings.ScreenArea.Left;
                    // If window is currently snapped to the edge of a grid cell, make sure to snap it to the
                    //  adjacent grid cell on the right.
                    x += 1 + settings.GridOddCol;
                    x = MoveRight(settings, x);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to rightmost grid cell on the screen.
                    x = 1 + settings.GridOddCol;
                    x = SnapRight(settings, x);
                    break;
                default:
                    throw new Exception();
            }

            if (x > settings.ScreenArea.Width - settings.GridOddCol)
            {
                if (rect.left - margins.left - settings.ScreenArea.Left >= settings.ScreenArea.Width - settings.GridOddCol)
                {
                    // Window is entirely off screen. Don't do anything.
                    return;
                }
                x = settings.ScreenArea.Width - settings.GridOddCol;
            }
            
            rect.right = settings.ScreenArea.Left + x + margins.right;
        }

        private void ExpandDown(IntPtr window, WINDOWPLACEMENT wndPl, ref RECT rect, RECT margins, ScreenSettings settings)
        {
            int y;
            switch (wndPl.showCmd)
            {
                case SW_SHOWNORMAL:
                    // Get y coordinate of bottom edge of the window, relative to the top edge of its screen.
                    y = rect.bottom - margins.bottom - settings.ScreenArea.Top;
                    // If window is currently snapped to the edge of a grid cell, make sure to snap it to the
                    //  adjacent grid cell on the bottom.
                    y += 1 + settings.GridOddRow;
                    y = MoveDown(settings, y);
                    break;
                case SW_SHOWMAXIMIZED:
                    // Snap window to rightmost grid cell on the screen.
                    y = 1 + settings.GridOddRow;
                    y = SnapDown(settings, y);
                    break;
                default:
                    throw new Exception();
            }

            if (y > settings.ScreenArea.Height - settings.GridOddRow)
            {
                if (rect.top - margins.top - settings.ScreenArea.Top >= settings.ScreenArea.Height - settings.GridOddRow)
                {
                    // Window is entirely off screen. Don't do anything.
                    return;
                }
                y = settings.ScreenArea.Height - settings.GridOddRow;
            }

            rect.bottom = settings.ScreenArea.Top + y + margins.bottom;
        }

        internal void Resize(Keys dir)
        {
            var window = GetForegroundWindow();

            var wndPl = new WINDOWPLACEMENT();
            GetWindowPlacement(window, ref wndPl);
            if (wndPl.showCmd == SW_SHOWMAXIMIZED)
            {
                // If the window is maximized, restore it right now so that
                //  its borders aren't messed up and the restore button becomes a maximize button.
                ShowWindow(window, SW_SHOWNORMAL);
            }

            // Can't use wndPl.rcNormalPosition because it misbehaves when window was aero snapped.
            // Additionally, rcNormalPosition gives the working area position whereas GetWindowRect()
            //  gets the screen position. We want the screen position when using MoveWindow().
            // Screen position and working area position can differ when the taskbar is docked to the
            //  left, or the top, edge of the screen.
            var rect = new RECT();
            GetWindowRect(window, ref rect);
            var margins = GetMargins(window, rect);
            var settings = GetScreenSettings(Screen.FromHandle(window));

            switch (dir)
            {
                case Keys.Left:
                    ShrinkLeft(window, wndPl, ref rect, margins, settings);
                    break;
                case Keys.Up:
                    ShrinkUp(window, wndPl, ref rect, margins, settings);
                    break;
                case Keys.Right:
                    ExpandRight(window, wndPl, ref rect, margins, settings);
                    break;
                case Keys.Down:
                    ExpandDown(window, wndPl, ref rect, margins, settings);
                    break;
            }

            // "By convention, the right and bottom edges of the rectangle are normally considered exclusive."
            MoveWindow(window, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, true);

            // TODO: if window now takes up entire working area, maximize the window.
        }

        // TODO: shortcuts for instant maximize (Ctrl-Win-Up), restore to pre-maximized position if SW_SHOWMAXIMIZED (Ctrl-Win-Down), and instant minimize if SW_SHOWNORMAL (Ctrl-Down).
        // TODO: shortcuts for instant move window to adjacent screen on left (Ctrl-Alt-Left) and right (Ctrl-Alt-Right).
        // TODO: shortcuts for cycling windows between existing grid tiles of customized width and height. See awesome.
        // TODO: resize grid tiles by resizing one of the two window forming the edge using NativeWindow or WH_CALLWNDPROC or WH_CBT, like in https://www.codeproject.com/Articles/6045/Sticky-Windows-How-to-make-your-top-level-forms-to. See AquaSnap. GridLayout -> GridBagLayout.
        // TODO: drag and drop windows while holding Win-Space to expand windows in the grid cell the mouse cursor is in. Show a translucent overlay on top of the grid cell.
    }
}

#if CAPTURAS_MANUAL
using System.Runtime.InteropServices;
using System.Text;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>Interop mínimo del build de capturas para los manuales (PrintWindow, ventanas del proceso, botones de MessageBox).</summary>
internal static class Win32Capturas
{
    internal const uint PW_RENDERFULLCONTENT = 2;
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWCP_DONOTROUND = 1;
    internal const uint GW_OWNER = 4;
    internal const uint WM_COMMAND = 0x0111;
    internal const uint WM_CLOSE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern IntPtr GetFocus();
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern void mouse_event(uint flags, int dx, int dy, int datos, UIntPtr extra);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool HideCaret(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out Rect value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr hwnd, EnumWindowsProc proc, IntPtr lParam);

    /// <summary>Ventanas hijas (controles nativos de un cuadro de mensaje): clase, texto y rectángulo de pantalla.</summary>
    internal static List<(string Clase, string Texto, Rect Rect)> Hijos(IntPtr padre)
    {
        var lista = new List<(string, string, Rect)>();
        EnumChildWindows(padre, (h, _) =>
        {
            GetWindowRect(h, out var r);
            lista.Add((Clase(h), Titulo(h), r));
            return true;
        }, IntPtr.Zero);
        return lista;
    }

    internal static string Clase(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static string Titulo(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Ventanas visibles de nivel superior de este proceso, de arriba hacia abajo (orden Z).</summary>
    internal static List<IntPtr> VentanasDelProceso()
    {
        var lista = new List<IntPtr>();
        var pid = (uint)Environment.ProcessId;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var p);
            if (p == pid && IsWindowVisible(h))
            {
                lista.Add(h);
            }

            return true;
        }, IntPtr.Zero);
        return lista;
    }
}
#endif

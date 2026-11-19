#if CAPTURAS_MANUAL
using System.Runtime.InteropServices;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>
/// Gancho CBT del hilo de interfaz (solo build de capturas): los cuadros de mensaje estándar de Windows toman los textos de
/// sus botones del idioma del sistema (esta PC está en inglés: «Yes/No/OK/Cancel»). El cliente usa Windows en español, así
/// que al activarse cada cuadro de diálogo (#32770) se renombran sus botones estándar.
/// </summary>
internal static class BotonesEnEspanol
{
    private const int WH_CBT = 5;
    private const int HCBT_ACTIVATE = 5;

    private delegate IntPtr HookProc(int codigo, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int codigo, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr hDlg, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(IntPtr hwnd, string texto);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private static readonly (int Id, string Texto)[] Textos =
    [
        (6, "&Sí"), (7, "&No"), (1, "Aceptar"), (2, "Cancelar"), (4, "&Reintentar"), (3, "&Anular"), (5, "&Omitir")
    ];

    private static HookProc? _proc; // referencia viva: si el recolector la libera, el gancho se cae
    private static IntPtr _gancho;

    public static void Instalar()
    {
        if (_gancho != IntPtr.Zero)
        {
            return;
        }

        _proc = Procesar;
        _gancho = SetWindowsHookEx(WH_CBT, _proc, IntPtr.Zero, GetCurrentThreadId());
    }

    public static void Quitar()
    {
        if (_gancho != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_gancho);
            _gancho = IntPtr.Zero;
        }
    }

    private static IntPtr Procesar(int codigo, IntPtr wParam, IntPtr lParam)
    {
        if (codigo == HCBT_ACTIVATE && Win32Capturas.Clase(wParam) == "#32770")
        {
            foreach (var (id, texto) in Textos)
            {
                var boton = GetDlgItem(wParam, id);
                if (boton != IntPtr.Zero && Win32Capturas.Clase(boton) == "Button")
                {
                    SetWindowText(boton, texto);
                }
            }
        }

        return CallNextHookEx(_gancho, codigo, wParam, lParam);
    }
}
#endif

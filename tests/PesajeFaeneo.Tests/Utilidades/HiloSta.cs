using System.Runtime.ExceptionServices;

namespace PesajeFaeneo.Tests.Utilidades;

/// <summary>
/// Ejecuta una acción en un hilo con apartamento STA, requerido por cualquier prueba que construya
/// o manipule un <see cref="System.Windows.Forms.Form"/> (WinForms exige STA para el portapapeles,
/// diálogos comunes, drag&amp;drop, etc.).
/// </summary>
public static class HiloSta
{
    public static void Ejecutar(Action accion)
    {
        ExceptionDispatchInfo? errorCapturado = null;

        var hilo = new Thread(() =>
        {
            try
            {
                accion();
            }
            catch (Exception ex)
            {
                errorCapturado = ExceptionDispatchInfo.Capture(ex);
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();

        errorCapturado?.Throw();
    }
}

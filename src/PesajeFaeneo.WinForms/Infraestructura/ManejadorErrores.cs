using Serilog;

namespace PesajeFaeneo.WinForms.Infraestructura;

public static class ManejadorErrores
{
    public static void Mostrar(Exception ex, IWin32Window? owner = null)
    {
        var clasificado = ClasificadorErrores.Clasificar(ex);

        if (clasificado.DebeRegistrarLog)
        {
            Log.Error(ex, "Error no controlado capturado por el manejador global. Código de incidente: {CodigoIncidente}", clasificado.CodigoIncidente);
        }

        var icono = clasificado.Tipo switch
        {
            TipoErrorClasificado.Validacion => MessageBoxIcon.Warning,
            TipoErrorClasificado.Negocio => MessageBoxIcon.Warning,
            TipoErrorClasificado.NoAutorizado => MessageBoxIcon.Warning,
            _ => MessageBoxIcon.Error
        };

        if (owner != null)
        {
            MessageBox.Show(owner, clasificado.Mensaje, clasificado.Titulo, MessageBoxButtons.OK, icono);
        }
        else
        {
            MessageBox.Show(clasificado.Mensaje, clasificado.Titulo, MessageBoxButtons.OK, icono);
        }
    }
}

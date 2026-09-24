using Serilog;

namespace PesajeFaeneo.WinForms.Infraestructura;

public static class ManejadorErrores
{
    public static void Mostrar(Exception ex, IWin32Window? owner = null)
    {
        var clasificado = ClasificadorErrores.Clasificar(ex);

        // Una cancelación (por ejemplo, el usuario cerró un diálogo o abortó una operación con Esc)
        // no es un error: no se registra en el log ni se interrumpe al usuario con un MessageBox.
        if (clasificado.Tipo == TipoErrorClasificado.Cancelado)
        {
            return;
        }

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

using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios;

public class FormBase : Form
{
    // Protege contra reentrada: sin esto, un doble clic o un doble Enter sobre el mismo botón (por
    // ejemplo "Grabar pesaje" en F4) podría disparar la acción dos veces antes de que la primera
    // termine, generando boletas duplicadas u otros efectos repetidos.
    private bool _ocupado;

    public FormBase()
    {
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected async Task EjecutarAsync(Func<Task> accion, Control? botonDisparador = null)
    {
        if (_ocupado)
        {
            return;
        }

        var estadoBotonPrevio = botonDisparador?.Enabled ?? true;

        try
        {
            _ocupado = true;
            // UseWaitCursor (a diferencia de Cursor.Current) se mantiene durante toda la operación:
            // Cursor.Current se restablece apenas el bucle de mensajes procesa el siguiente evento,
            // por lo que en una operación con await quedaba inútil desde el primer punto de suspensión.
            UseWaitCursor = true;
            if (botonDisparador != null)
            {
                botonDisparador.Enabled = false;
            }

            await accion();
        }
        catch (Exception ex)
        {
            ManejadorErrores.Mostrar(ex, this);
        }
        finally
        {
            UseWaitCursor = false;
            if (botonDisparador != null)
            {
                botonDisparador.Enabled = estadoBotonPrevio;
            }

            _ocupado = false;
        }
    }

    protected void Ejecutar(Action accion, Control? botonDisparador = null)
    {
        if (_ocupado)
        {
            return;
        }

        var estadoBotonPrevio = botonDisparador?.Enabled ?? true;

        try
        {
            _ocupado = true;
            UseWaitCursor = true;
            if (botonDisparador != null)
            {
                botonDisparador.Enabled = false;
            }

            accion();
        }
        catch (Exception ex)
        {
            ManejadorErrores.Mostrar(ex, this);
        }
        finally
        {
            UseWaitCursor = false;
            if (botonDisparador != null)
            {
                botonDisparador.Enabled = estadoBotonPrevio;
            }

            _ocupado = false;
        }
    }
}

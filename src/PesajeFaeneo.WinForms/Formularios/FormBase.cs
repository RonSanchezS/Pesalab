using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios;

public class FormBase : Form
{
    public FormBase()
    {
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected async Task EjecutarAsync(Func<Task> accion, Control? botonDisparador = null)
    {
        var cursorPrevio = Cursor.Current;
        var estadoBotonPrevio = botonDisparador?.Enabled ?? true;

        try
        {
            Cursor.Current = Cursors.WaitCursor;
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
            Cursor.Current = cursorPrevio;
            if (botonDisparador != null)
            {
                botonDisparador.Enabled = estadoBotonPrevio;
            }
        }
    }

    protected void Ejecutar(Action accion, Control? botonDisparador = null)
    {
        var cursorPrevio = Cursor.Current;
        var estadoBotonPrevio = botonDisparador?.Enabled ?? true;

        try
        {
            Cursor.Current = Cursors.WaitCursor;
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
            Cursor.Current = cursorPrevio;
            if (botonDisparador != null)
            {
                botonDisparador.Enabled = estadoBotonPrevio;
            }
        }
    }
}

using PesajeFaeneo.Domain.Excepciones;
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
        Icon = Infraestructura.MarcaVisual.Icono ?? Icon;
    }

    protected override void OnLoad(EventArgs e)
    {
        // Los tamaños y posiciones del código se expresan a 96 DPI; AutoScaleMode.Dpi los escala a la escala
        // real de Windows (125 %, 150 %…). Sin esto la fuente crece con el escalado y los controles no, y los
        // textos quedan cortados. Se activa aquí y no en el constructor: WinForms escala en el siguiente
        // layout, y en el constructor ese layout ocurre con el formulario todavía vacío (los controles los
        // agrega después el constructor derivado), con lo que no se escalaría nada.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        base.OnLoad(e);
    }

    /// <summary>True mientras un <see cref="EjecutarAsync"/> está en curso (para impedir cerrar o relanzar acciones).</summary>
    protected bool Ocupado => _ocupado;

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

    protected bool MostrarErroresValidacion(
        Exception ex,
        ErrorProvider proveedor,
        IReadOnlyDictionary<string, Control> controlesPorCampo)
    {
        ArgumentNullException.ThrowIfNull(proveedor);
        ArgumentNullException.ThrowIfNull(controlesPorCampo);

        proveedor.Clear();

        if (ex is not ValidacionException valEx)
        {
            return false;
        }

        Control? primerControlConError = null;
        var erroresSinControl = new List<string>();

        foreach (var error in valEx.Errores)
        {
            if (controlesPorCampo.TryGetValue(error.Campo, out var control))
            {
                proveedor.SetError(control, error.Mensaje);
                primerControlConError ??= control;
            }
            else
            {
                erroresSinControl.Add(error.Mensaje);
            }
        }

        primerControlConError?.Focus();

        if (erroresSinControl.Count > 0)
        {
            var mensaje = string.Join(Environment.NewLine, erroresSinControl);
            MessageBox.Show(this, mensaje, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return true;
    }
}

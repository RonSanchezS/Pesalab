using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FormBaseTests
{
    [Fact]
    public void EjecutarAsync_InvocacionConcurrente_SoloEjecutaLaAccionUnaVez()
    {
        HiloSta.Ejecutar(() =>
        {
            using var formulario = new FormularioPruebaReentrada();
            var tcs = new TaskCompletionSource();
            var vecesEjecutada = 0;

            Task Accion()
            {
                Interlocked.Increment(ref vecesEjecutada);
                return tcs.Task;
            }

            // La primera invocación queda suspendida esperando tcs.Task (simula una operación en curso,
            // como grabar un pesaje). La segunda, mientras la primera sigue "ocupada", debe retornar de
            // inmediato sin ejecutar la acción: así se evita el doble clic/doble Enter duplicando boletas.
            var primeraInvocacion = formulario.InvocarAsync(Accion);
            var segundaInvocacion = formulario.InvocarAsync(Accion);

            segundaInvocacion.GetAwaiter().GetResult();
            Assert.Equal(1, vecesEjecutada);

            tcs.SetResult();
            primeraInvocacion.GetAwaiter().GetResult();

            Assert.Equal(1, vecesEjecutada);
        });
    }

    [Fact]
    public void Ejecutar_InvocacionConcurrente_SoloEjecutaLaAccionUnaVez()
    {
        HiloSta.Ejecutar(() =>
        {
            using var formulario = new FormularioPruebaReentrada();
            var vecesEjecutada = 0;

            // La versión síncrona no puede "quedar en curso" real (no hay punto de suspensión), pero
            // el mismo campo _ocupado debe seguir protegiendo cualquier invocación reentrante disparada
            // desde dentro de la propia acción (por ejemplo, un evento que se vuelve a disparar).
            formulario.InvocarSincrono(() =>
            {
                vecesEjecutada++;
                formulario.InvocarSincrono(() => vecesEjecutada++);
            });

            Assert.Equal(1, vecesEjecutada);
        });
    }

    private class FormularioPruebaReentrada : FormBase
    {
        public Task InvocarAsync(Func<Task> accion) => EjecutarAsync(accion);

        public void InvocarSincrono(Action accion) => Ejecutar(accion);
    }

    [Fact]
    public void MostrarErroresValidacion_ConValidacionExceptionDosCampos_AsignaErroresYEnfocaPrimero()
    {
        HiloSta.Ejecutar(() =>
        {
            using var formulario = new FormularioPruebaValidacion();
            formulario.Show();

            var ex = new PesajeFaeneo.Domain.Excepciones.ValidacionException([
                new PesajeFaeneo.Domain.Excepciones.ErrorValidacion("Campo1", "Error campo 1"),
                new PesajeFaeneo.Domain.Excepciones.ErrorValidacion("Campo2", "Error campo 2")
            ]);

            var manejado = formulario.ProbarValidacion(ex);

            Assert.True(manejado);
            Assert.Equal("Error campo 1", formulario.ErrorProvider.GetError(formulario.Txt1));
            Assert.Equal("Error campo 2", formulario.ErrorProvider.GetError(formulario.Txt2));
            Assert.True(formulario.Txt1.Focused);
        });
    }

    private class FormularioPruebaValidacion : FormBase
    {
        public TextBox Txt1 { get; } = new();
        public TextBox Txt2 { get; } = new();
        public ErrorProvider ErrorProvider { get; }

        public FormularioPruebaValidacion()
        {
            Controls.Add(Txt1);
            Controls.Add(Txt2);
            ErrorProvider = new ErrorProvider { ContainerControl = this };
        }

        public bool ProbarValidacion(Exception ex)
        {
            var mapa = new Dictionary<string, Control>
            {
                { "Campo1", Txt1 },
                { "Campo2", Txt2 }
            };
            return MostrarErroresValidacion(ex, ErrorProvider, mapa);
        }
    }
}

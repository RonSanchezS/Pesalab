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
}

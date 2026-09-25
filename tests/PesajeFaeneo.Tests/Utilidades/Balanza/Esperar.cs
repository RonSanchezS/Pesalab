using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace PesajeFaeneo.Tests.Utilidades.Balanza;

public static class Esperar
{
    public static async Task HastaAsync(Func<bool> condicion, string descripcion, TimeSpan? limite = null)
    {
        var max = limite ?? TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < max)
        {
            if (condicion()) return;
            await Task.Delay(10).ConfigureAwait(false);
        }
        Assert.Fail($"No se cumplió: {descripcion} tras {max.TotalSeconds} s.");
    }

    public static async Task AvanzandoHastaAsync(FakeTimeProvider reloj, TimeSpan paso, Func<bool> condicion, string descripcion)
    {
        var max = TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < max)
        {
            if (condicion()) return;
            reloj.Advance(paso);
            await Task.Delay(10).ConfigureAwait(false);
        }
        Assert.Fail($"No se cumplió: {descripcion} tras avanzar el reloj.");
    }
}

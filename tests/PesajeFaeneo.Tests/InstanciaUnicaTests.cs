namespace PesajeFaeneo.Tests;

using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

public class InstanciaUnicaTests
{
    [Fact]
    public void Test01_Adquirir_SinEsperar_DevuelveNullSiYaExiste()
    {
        var nombre = $@"Local\pesaje_test_{Guid.NewGuid():N}";
        using var aAdquirido = new ManualResetEventSlim(false);
        using var liberarA = new ManualResetEventSlim(false);

        var hiloA = new Thread(() =>
        {
            using var a = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero);
            aAdquirido.Set();
            liberarA.Wait();
        });
        hiloA.Start();

        aAdquirido.Wait();

        using var instB = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero);
        Assert.Null(instB);

        liberarA.Set();
        hiloA.Join();
    }

    [Fact]
    public void Test02_Adquirir_EsperarTrue_AdquiereCuandoInstanciaAnteriorLibera()
    {
        var nombre = $@"Local\pesaje_test_{Guid.NewGuid():N}";
        using var aAdquirido = new ManualResetEventSlim(false);
        using var liberarA = new ManualResetEventSlim(false);
        InstanciaUnica? instB = null;

        var hiloA = new Thread(() =>
        {
            using var a = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero);
            aAdquirido.Set();
            liberarA.Wait();
        });
        hiloA.Start();

        aAdquirido.Wait();

        var hiloB = new Thread(() =>
        {
            instB = InstanciaUnica.Adquirir(nombre, esperar: true, TimeSpan.FromSeconds(10));
        });
        hiloB.Start();

        // Pequeño retardo para asegurar que B entró a esperar, luego indicar a A que libere
        Thread.Sleep(50);
        liberarA.Set();

        hiloA.Join();
        hiloB.Join();

        Assert.NotNull(instB);
        instB?.Dispose();
    }

    [Fact]
    public void Test03_Adquirir_EsperarTrue_MutexAbandonado_AdquiereCorrectamente()
    {
        var nombre = $@"Local\pesaje_test_{Guid.NewGuid():N}";
        using var aAdquirido = new ManualResetEventSlim(false);

        var hiloA = new Thread(() =>
        {
            var m = new Mutex(true, nombre, out _);
            aAdquirido.Set();
            // Termina el hilo SIN hacer Dispose ni ReleaseMutex (abandona el mutex)
        });
        hiloA.Start();
        hiloA.Join();

        aAdquirido.Wait();

        using var instB = InstanciaUnica.Adquirir(nombre, esperar: true, TimeSpan.FromSeconds(5));
        Assert.NotNull(instB);
    }

    [Fact]
    public void Test04_Adquirir_EsperarTrue_TimeoutExpira_DevuelveNull()
    {
        var nombre = $@"Local\pesaje_test_{Guid.NewGuid():N}";
        using var aAdquirido = new ManualResetEventSlim(false);
        using var liberarA = new ManualResetEventSlim(false);

        var hiloA = new Thread(() =>
        {
            using var a = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero);
            aAdquirido.Set();
            liberarA.Wait();
        });
        hiloA.Start();

        aAdquirido.Wait();

        using var instB = InstanciaUnica.Adquirir(nombre, esperar: true, TimeSpan.FromMilliseconds(200));
        Assert.Null(instB);

        liberarA.Set();
        hiloA.Join();
    }

    [Fact]
    public void Test05_Dispose_DosVecesNoLanza()
    {
        var nombre = $@"Local\pesaje_test_{Guid.NewGuid():N}";
        var inst = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero);
        Assert.NotNull(inst);

        var ex = Record.Exception(() =>
        {
            inst!.Dispose();
            inst!.Dispose();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Test06_Adquirir_UnauthorizedAccessException_DevuelveNullSinExcepcion()
    {
        var resSinEsperar = InstanciaUnica.Adquirir("Global\\test", esperar: false, TimeSpan.Zero, _ => throw new UnauthorizedAccessException());
        Assert.Null(resSinEsperar);

        var resConEsperar = InstanciaUnica.Adquirir("Global\\test", esperar: true, TimeSpan.FromSeconds(5), _ => throw new UnauthorizedAccessException());
        Assert.Null(resConEsperar);
    }

    [Fact]
    public void Test07_Adquirir_MismoHilo_SegundoIntentoDevuelveNull()
    {
        var nombre = $@"Local\pesaje_test_{Guid.NewGuid():N}";
        using var inst1 = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero, n =>
        {
            var m = new Mutex(true, n, out var esNueva);
            return (m, esNueva);
        });

        Assert.NotNull(inst1);

        using var inst2 = InstanciaUnica.Adquirir(nombre, esperar: false, TimeSpan.Zero);
        Assert.Null(inst2);
    }
}

namespace PesajeFaeneo.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades.Backup;
using Xunit;

public class ProgramadorBackupAutomaticoTests
{
    [Fact]
    public async Task Test01_EjecutarCicloAsync_ActivoYSinAutomaticoDelDia_CreaBackup()
    {
        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso
        {
            Configuracion = new ConfiguracionBackup(true, false, 40, @"D:\Secundaria")
        };
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));

        var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        var resultado = await programador.EjecutarCicloAsync(CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.True(resultado.Exito);
        var sol = Assert.Single(motor.Solicitudes);
        Assert.Equal(TipoBackup.Automatico, sol.Tipo);
        Assert.Equal(40, sol.DiasRetencion);
        Assert.Equal(@"D:\Secundaria", sol.CarpetaSecundaria);
        Assert.True(programador.UltimoResultado?.Exito);
    }

    [Fact]
    public async Task Test02_EjecutarCicloAsync_ConZonaHoraria_ConsultaFechaLocalYNoCreaSiExiste()
    {
        var motor = new MotorBackupFalso { ExisteDelDia = true };
        var almacen = new AlmacenConfiguracionBackupFalso();
        // 2026-10-02 02:00:00 UTC en UTC-4 es 2026-10-01 22:00:00 local
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));

        var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        var resultado = await programador.EjecutarCicloAsync(CancellationToken.None);

        Assert.Null(resultado);
        Assert.Empty(motor.Solicitudes);
    }

    [Fact]
    public async Task Test03_EjecutarCicloAsync_AutomaticoInactivo_DevuelveNullYSinLlamadas()
    {
        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso
        {
            Configuracion = new ConfiguracionBackup(false, true, 30, null)
        };
        var tiempo = new FakeTimeProvider();

        var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        var resultado = await programador.EjecutarCicloAsync(CancellationToken.None);

        Assert.Null(resultado);
        Assert.Empty(motor.Solicitudes);
    }

    [Fact]
    public async Task Test04_EjecutarCicloAsync_ExcepcionesEnMotor_ManejaErroresSinPropagar()
    {
        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso();
        var tiempo = new FakeTimeProvider();

        var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        // 1. ReglaNegocioException
        motor.ErrorCrear = new ReglaNegocioException("BAK_ESPACIO", "Espacio insuficiente en disco.");
        var r1 = await programador.EjecutarCicloAsync(CancellationToken.None);
        Assert.NotNull(r1);
        Assert.False(r1.Exito);
        Assert.Equal("Espacio insuficiente en disco.", r1.Mensaje);

        // 2. Error inesperado
        motor.ErrorCrear = new InvalidOperationException("Disco desconectado.");
        var r2 = await programador.EjecutarCicloAsync(CancellationToken.None);
        Assert.NotNull(r2);
        Assert.False(r2.Exito);
        Assert.Equal("Error inesperado; revise el log.", r2.Mensaje);
    }

    [Fact]
    public async Task Test05_EjecutarAlCerrarAsync_ComportamientoSegunConfiguracion()
    {
        var motor = new MotorBackupFalso { ExisteDelDia = true };
        var almacen = new AlmacenConfiguracionBackupFalso();
        var tiempo = new FakeTimeProvider();

        var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        // AlCerrar = false -> null
        almacen.Configuracion = new ConfiguracionBackup(true, false, 30, null);
        var r1 = await programador.EjecutarAlCerrarAsync();
        Assert.Null(r1);
        Assert.Empty(motor.Solicitudes);

        // AlCerrar = true aunque exista automático del día -> crea backup igual
        almacen.Configuracion = new ConfiguracionBackup(true, true, 30, null);
        var r2 = await programador.EjecutarAlCerrarAsync();
        Assert.NotNull(r2);
        Assert.True(r2.Exito);
        Assert.Single(motor.Solicitudes);
    }

    [Fact]
    public async Task Test06_IniciarYDetener_EjecutaCicloConTemporizador()
    {
        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso();
        var tiempo = new FakeTimeProvider();

        using var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        programador.Iniciar();
        programador.Iniciar(); // Iniciar dos veces no duplica bucle

        await programador.TemporizadorListoParaTest.WaitAsync(TimeSpan.FromSeconds(5));

        tiempo.Advance(TimeSpan.FromMinutes(2));
        await motor.PrimeraCreacion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(motor.Solicitudes);

        await programador.DetenerAsync();
    }

    [Fact]
    public async Task Test07_DetenerAsync_SinIniciar_NoLanza()
    {
        var motor = new MotorBackupFalso();
        var almacen = new AlmacenConfiguracionBackupFalso();
        var tiempo = new FakeTimeProvider();

        using var programador = new ProgramadorBackupAutomatico(
            motor,
            almacen,
            tiempo,
            NullLogger<ProgramadorBackupAutomatico>.Instance);

        var ex = await Record.ExceptionAsync(() => programador.DetenerAsync());
        Assert.Null(ex);
    }
}

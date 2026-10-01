using System.Text;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Logs;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioLogsTests
{
    private sealed class LectorArchivosLogFalso : ILectorArchivosLog
    {
        public string Carpeta { get; set; } = @"C:\FakeLogs";
        public List<ArchivoLog> Archivos { get; set; } = new();
        public string TextoALeer { get; set; } = string.Empty;
        public bool TruncadoALeer { get; set; }
        public long TamanoALeer { get; set; } = 100;

        public IReadOnlyList<ArchivoLog> Listar() => Archivos;

        public (string Texto, bool Truncado, long TamanoBytes) LeerFinal(string nombreArchivo, long maximoBytes) =>
            (TextoALeer, TruncadoALeer, TamanoALeer);
    }

    [Fact]
    public async Task Test01_Supervisor_LanzaNoAutorizadoExceptionEnAmbosMetodos()
    {
        var lector = new LectorArchivosLogFalso();
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(2, "supervisor", "Supervisor", Rol.Supervisor, false));

        var servicio = new ServicioLogs(lector, sesion);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarArchivosAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.LeerAsync("pesaje-20261001.log"));
    }

    [Fact]
    public async Task Test02_LeerAsync_InvierteOrden_MasRecientePrimero()
    {
        var lector = new LectorArchivosLogFalso();
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));

        lector.TextoALeer =
            "2026-10-01 10:00:00.000 -04:00 [INF] Mensaje 1\n" +
            "2026-10-01 10:01:00.000 -04:00 [INF] Mensaje 2\n" +
            "2026-10-01 10:02:00.000 -04:00 [INF] Mensaje 3\n";

        var servicio = new ServicioLogs(lector, sesion);
        var contenido = await servicio.LeerAsync("pesaje-20261001.log");

        Assert.Equal(3, contenido.Entradas.Count);
        Assert.Equal("Mensaje 3", contenido.Entradas[0].Mensaje);
        Assert.Equal("Mensaje 2", contenido.Entradas[1].Mensaje);
        Assert.Equal("Mensaje 1", contenido.Entradas[2].Mensaje);
        Assert.False(contenido.Truncado);
    }

    [Fact]
    public async Task Test03_LeerAsync_ConMasDe10000Entradas_TruncaA10000YMarcaTruncado()
    {
        var lector = new LectorArchivosLogFalso();
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));

        var sb = new StringBuilder(10001 * 60);
        for (int i = 1; i <= 10001; i++)
        {
            sb.Append($"2026-10-01 10:00:00.000 -04:00 [INF] Mensaje {i}\n");
        }
        lector.TextoALeer = sb.ToString();

        var servicio = new ServicioLogs(lector, sesion);
        var contenido = await servicio.LeerAsync("pesaje-20261001.log");

        Assert.Equal(10000, contenido.Entradas.Count);
        Assert.True(contenido.Truncado);
        // La primera devuelta es la última generada (10001)
        Assert.Equal("Mensaje 10001", contenido.Entradas[0].Mensaje);
    }
}

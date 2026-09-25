using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class LectorTcpTests
{
    private static ConfiguracionBalanzaDto CrearConfigTcp(int puerto) => new(
        TipoConexion: TipoConexion.Tcp,
        PuertoCom: "COM1",
        BaudRate: 9600,
        DataBits: 8,
        Paridad: ParidadSerial.None,
        BitsParada: BitsParada.One,
        HostTcp: "127.0.0.1",
        PuertoTcp: puerto,
        PatronRegex: @"(?<peso>\d+(?:\.\d+)?)",
        LecturasParaEstable: 5,
        ToleranciaKg: 0.5m,
        TimeoutMs: 1500);

    [Fact]
    public async Task LecturaTramasMultiples_EntregaCorrectamente()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cts.Token);
            using var stream = client.GetStream();
            var bytes = Encoding.Latin1.GetBytes("12.3\r\n45.6\r\n");
            await stream.WriteAsync(bytes, cts.Token);
            await stream.FlushAsync(cts.Token);
            await Task.Delay(200, cts.Token);
        }, cts.Token);

        var lector = new LectorTcp(CrearConfigTcp(port), NullLogger.Instance);
        await lector.ConectarAsync(cts.Token);

        var trama1 = await lector.LeerTramaAsync(cts.Token);
        var trama2 = await lector.LeerTramaAsync(cts.Token);

        Assert.Equal("12.3", trama1);
        Assert.Equal("45.6", trama2);

        await lector.DisposeAsync();
        listener.Stop();
        await serverTask;
    }

    [Fact]
    public async Task ServidorCierraSocket_LanzaConexionBalanzaException()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cts.Token);
            // Cierra inmediatamente
            client.Close();
        }, cts.Token);

        var lector = new LectorTcp(CrearConfigTcp(port), NullLogger.Instance);
        await lector.ConectarAsync(cts.Token);

        var ex = await Assert.ThrowsAsync<ConexionBalanzaException>(async () => await lector.LeerTramaAsync(cts.Token));
        Assert.Contains("La balanza cerró la conexión.", ex.Message);

        await lector.DisposeAsync();
        listener.Stop();
        await serverTask;
    }

    [Fact]
    public async Task PuertoSinEscucha_LanzaConexionBalanzaExceptionConConnectionRefused()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop(); // Cerrado antes de conectar

        var lector = new LectorTcp(CrearConfigTcp(port), NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<ConexionBalanzaException>(() => lector.ConectarAsync(cts.Token));
        Assert.Contains(SocketError.ConnectionRefused.ToString(), ex.Message);

        await lector.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ConLecturaPendiente_TerminaEnMenosDeDosSegundos()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var lector = new LectorTcp(CrearConfigTcp(port), NullLogger.Instance);
        await lector.ConectarAsync(cts.Token);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await lector.DisposeAsync();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"DisposeAsync tardó {sw.ElapsedMilliseconds} ms, más de 2 segundos.");
        listener.Stop();
    }
}

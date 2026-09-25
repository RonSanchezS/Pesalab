using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class LectorSerialTests
{
    private class PuertoSerieFalso : IPuertoSerie
    {
        private readonly Queue<object> _acciones = new();
        public string Nombre { get; set; } = "COM1";
        public bool Abierto { get; private set; }
        public bool Cerrado { get; private set; }
        public bool Desechado { get; private set; }
        public Exception? ExcepcionAlAbrir { get; set; }
        public bool ExcepcionAlCerrar { get; set; }

        public void EncolarBytes(byte[] bytes) => _acciones.Enqueue(bytes);
        public void EncolarExcepcion(Exception ex) => _acciones.Enqueue(ex);

        public void Abrir()
        {
            if (ExcepcionAlAbrir != null)
            {
                throw ExcepcionAlAbrir;
            }
            Abierto = true;
        }

        public int Leer(byte[] buffer, int offset, int count)
        {
            if (Cerrado)
            {
                throw new InvalidOperationException("Puerto cerrado.");
            }

            if (_acciones.TryDequeue(out var accion))
            {
                if (accion is Exception ex)
                {
                    throw ex;
                }
                if (accion is byte[] bytes)
                {
                    int n = Math.Min(bytes.Length, count);
                    Array.Copy(bytes, 0, buffer, offset, n);
                    return n;
                }
            }

            Thread.Sleep(5);
            throw new TimeoutException();
        }

        public void Cerrar()
        {
            Cerrado = true;
            if (ExcepcionAlCerrar)
            {
                throw new IOException("Fallo forzado al cerrar.");
            }
        }

        public void Dispose()
        {
            Desechado = true;
        }
    }

    private static ConfiguracionBalanzaDto CrearConfigSerial(string puerto = "COM1") => new(
        TipoConexion: TipoConexion.Serial,
        PuertoCom: puerto,
        BaudRate: 9600,
        DataBits: 8,
        Paridad: ParidadSerial.None,
        BitsParada: BitsParada.One,
        HostTcp: "127.0.0.1",
        PuertoTcp: 4001,
        PatronRegex: @"(?<peso>\d+(?:\.\d+)?)",
        LecturasParaEstable: 5,
        ToleranciaKg: 0.5m,
        TimeoutMs: 1500);

    [Fact]
    public async Task LecturaPartidaConTimeout_DevuelveTramaCorrecta()
    {
        var puerto = new PuertoSerieFalso();
        puerto.EncolarBytes(System.Text.Encoding.Latin1.GetBytes("12."));
        puerto.EncolarExcepcion(new TimeoutException());
        puerto.EncolarBytes(System.Text.Encoding.Latin1.GetBytes("3\r\n"));

        var lector = new LectorSerial(CrearConfigSerial(), NullLogger.Instance, () => puerto);
        await lector.ConectarAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var trama = await lector.LeerTramaAsync(cts.Token);

        Assert.Equal("12.3", trama);
        await lector.DisposeAsync();
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), "El puerto COM1 está en uso por otra aplicación o no hay permiso para abrirlo.")]
    [InlineData(typeof(IOException), "No se pudo abrir el puerto COM1. Verifique que el cable o adaptador esté conectado.")]
    [InlineData(typeof(ArgumentException), "El nombre de puerto 'COM1' no es válido.")]
    [InlineData(typeof(InvalidOperationException), "El puerto COM1 ya está abierto.")]
    public async Task Abrir_ConExcepcionEspecifica_TraduceAMensajeExacto(Type tipoExcepcion, string mensajeEsperado)
    {
        var puerto = new PuertoSerieFalso
        {
            ExcepcionAlAbrir = (Exception)Activator.CreateInstance(tipoExcepcion, "Error original")!
        };

        var lector = new LectorSerial(CrearConfigSerial(), NullLogger.Instance, () => puerto);

        var ex = await Assert.ThrowsAsync<ConexionBalanzaException>(() => lector.ConectarAsync(CancellationToken.None));
        Assert.Equal(mensajeEsperado, ex.Message);
        await lector.DisposeAsync();
    }

    [Fact]
    public async Task ErrorIoDuranteLectura_LanzaConexionBalanzaException()
    {
        var puerto = new PuertoSerieFalso();
        puerto.EncolarExcepcion(new IOException("Cable desconectado"));

        var lector = new LectorSerial(CrearConfigSerial(), NullLogger.Instance, () => puerto);
        await lector.ConectarAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var ex = await Assert.ThrowsAsync<ConexionBalanzaException>(async () => await lector.LeerTramaAsync(cts.Token));

        Assert.Contains("Se perdió la comunicación con el puerto COM1.", ex.Message);
        await lector.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CierraYDesechaPuerto_SinLanzarSiCerrarFalla()
    {
        var puerto = new PuertoSerieFalso
        {
            ExcepcionAlCerrar = true
        };

        var lector = new LectorSerial(CrearConfigSerial(), NullLogger.Instance, () => puerto);
        await lector.ConectarAsync(CancellationToken.None);

        await lector.DisposeAsync();

        Assert.True(puerto.Cerrado);
        Assert.True(puerto.Desechado);
    }

    [Fact]
    public async Task ConectarAsync_ConTokenCancelado_LanzaYNoAbrePuerto()
    {
        var puerto = new PuertoSerieFalso();
        var lector = new LectorSerial(CrearConfigSerial(), NullLogger.Instance, () => puerto);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lector.ConectarAsync(cts.Token));
        Assert.False(puerto.Abierto);
        await lector.DisposeAsync();
    }

    [Fact]
    public async Task TramasRx8SeparadasPorEspacio_SinCrLf_SeEntreganUnaPorUna()
    {
        // Formato real capturado del indicador RX8 Accurex en COM4: "=000000 " repetido, sin CR/LF.
        var puerto = new PuertoSerieFalso();
        puerto.EncolarBytes(System.Text.Encoding.Latin1.GetBytes("=012345 =012"));
        puerto.EncolarBytes(System.Text.Encoding.Latin1.GetBytes("350 "));

        var config = CrearConfigSerial() with { DelimitadorTrama = DelimitadorTrama.Espacio };
        var lector = new LectorSerial(config, NullLogger.Instance, () => puerto);
        await lector.ConectarAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal("=012345", await lector.LeerTramaAsync(cts.Token));
        Assert.Equal("=012350", await lector.LeerTramaAsync(cts.Token));
        await lector.DisposeAsync();
    }

    [Fact]
    public async Task RafagaRx8UnidaPorGuiones_ConSignoIgual_CadaLecturaLlegaYSeConvierteAKg()
    {
        var puerto = new PuertoSerieFalso();
        puerto.EncolarBytes(System.Text.Encoding.Latin1.GetBytes("=650000-=381000-=161000 "));

        var config = CrearConfigSerial() with
        {
            DelimitadorTrama = DelimitadorTrama.SignoIgual,
            UnidadPeso = UnidadPesoTrama.Gramos,
            PatronRegex = @"^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)"
        };
        var lector = new LectorSerial(config, NullLogger.Instance, () => puerto);
        var parser = new ParserTramas(config.PatronRegex, config.UnidadPeso);
        await lector.ConectarAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var pesos = new List<decimal?>();
        for (int i = 0; i < 3; i++)
        {
            pesos.Add(parser.Parsear(await lector.LeerTramaAsync(cts.Token)).PesoKg);
        }

        Assert.Equal(new decimal?[] { 650.0m, 381.0m, 161.0m }, pesos);
        await lector.DisposeAsync();
    }
}

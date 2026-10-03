using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using PesajeFaeneo.Tests.Utilidades.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests.Infrastructure.Utilitarios.Importacion;

public class LectorOrigenDatosTests : IDisposable
{
    private readonly string _directorioTemporal;
    private readonly LectorOrigenDatos _lector;

    public LectorOrigenDatosTests()
    {
        _directorioTemporal = Path.Combine(Path.GetTempPath(), "LectorOrigenDatosTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directorioTemporal);
        _lector = new LectorOrigenDatos(NullLogger<LectorOrigenDatos>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directorioTemporal))
            {
                Directory.Delete(_directorioTemporal, true);
            }
        }
        catch
        {
            // Limpieza de mejor esfuerzo
        }
    }

    [Fact]
    public async Task LeerAsync_ArchivoInexistente_LanzaValidacionException()
    {
        var ruta = Path.Combine(_directorioTemporal, "no_existe.csv");
        var solicitud = new SolicitudLectura(ruta, FormatoOrigen.Csv, null, null);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => _lector.LeerAsync(solicitud));
        Assert.Contains("El archivo no existe.", ex.Message);
    }

    [Fact]
    public async Task LeerAsync_MaximoBytesSuperado_LanzaValidacionException()
    {
        var ruta = Path.Combine(_directorioTemporal, "grande.csv");
        File.WriteAllText(ruta, "Col1;Col2\n12345;67890\n"); // > 10 bytes
        _lector.MaximoBytesParaTest = 10;

        var solicitud = new SolicitudLectura(ruta, FormatoOrigen.Csv, null, null);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => _lector.LeerAsync(solicitud));
        Assert.Contains("pesa más de", ex.Message);
    }

    [Fact]
    public async Task LeerAsync_MaximoFilasSuperado_LanzaValidacionException()
    {
        var ruta = Path.Combine(_directorioTemporal, "filas.csv");
        File.WriteAllText(ruta, "Col1\n1\n2\n3\n4\n");
        _lector.MaximoFilasParaTest = 3;

        var solicitud = new SolicitudLectura(ruta, FormatoOrigen.Csv, null, null);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => _lector.LeerAsync(solicitud));
        Assert.Contains("filas; divídalo en partes", ex.Message);
    }

    [Fact]
    public async Task LeerAsync_ArchivoBloqueadoSinFileShare_LanzaValidacionExceptionConCierrelo()
    {
        var ruta = Path.Combine(_directorioTemporal, "bloqueado.csv");
        File.WriteAllText(ruta, "Col1\n1\n");

        using var fs = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var solicitud = new SolicitudLectura(ruta, FormatoOrigen.Csv, null, null);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => _lector.LeerAsync(solicitud));
        Assert.Contains("Ciérrelo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeerAsync_CalculoHuella_MismoContenidoMismaHuella64HexYDistintoDifiere()
    {
        var ruta1 = Path.Combine(_directorioTemporal, "archivo1.csv");
        var ruta2 = Path.Combine(_directorioTemporal, "archivo2.csv");
        var ruta3 = Path.Combine(_directorioTemporal, "archivo3.csv");

        File.WriteAllText(ruta1, "Col1\n1\n");
        File.WriteAllText(ruta2, "Col1\n1\n");
        File.WriteAllText(ruta3, "Col1\n2\n");

        var sol1 = new SolicitudLectura(ruta1, FormatoOrigen.Csv, null, null);
        var sol2 = new SolicitudLectura(ruta2, FormatoOrigen.Csv, null, null);
        var sol3 = new SolicitudLectura(ruta3, FormatoOrigen.Csv, null, null);

        var t1 = await _lector.LeerAsync(sol1);
        var t2 = await _lector.LeerAsync(sol2);
        var t3 = await _lector.LeerAsync(sol3);

        Assert.Equal(64, t1.Huella.Length);
        Assert.Equal(t1.Huella, t2.Huella);
        Assert.NotEqual(t1.Huella, t3.Huella);
    }

    [Fact]
    public async Task LeerAsync_DbfDesdeGanaderos850YPesajesVfp1252_LeeCorrectamenteConAdvertencias()
    {
        var rutaGan = Path.Combine(_directorioTemporal, "ganaderos.dbf");
        await File.WriteAllBytesAsync(rutaGan, ArchivosDbfDePrueba.Ganaderos850);

        var solGan = new SolicitudLectura(rutaGan, FormatoOrigen.Dbf, null, null);
        var tGan = await _lector.LeerAsync(solGan);

        Assert.Equal(3, tGan.Filas.Count);
        Assert.Equal(1, tGan.Filas[0].Numero);
        Assert.Equal(2, tGan.Filas[1].Numero);
        Assert.Equal(4, tGan.Filas[2].Numero);
        Assert.Equal("DOS 850", tGan.Codificacion);
        Assert.Contains(tGan.Advertencias, a => a.Contains("1 registro(s) marcados como borrados"));

        var rutaPes = Path.Combine(_directorioTemporal, "pesajes.dbf");
        await File.WriteAllBytesAsync(rutaPes, ArchivosDbfDePrueba.PesajesVfp1252);

        var solPes = new SolicitudLectura(rutaPes, FormatoOrigen.Dbf, null, null);
        var tPes = await _lector.LeerAsync(solPes);

        Assert.Contains(tPes.Advertencias, a => a.Contains("«NOTA»"));
    }

    [Fact]
    public async Task LeerAsync_PaginaCodigosNoPermitida_LanzaValidacionException()
    {
        var ruta = Path.Combine(_directorioTemporal, "test.csv");
        File.WriteAllText(ruta, "Col1\n1\n");
        var solicitud = new SolicitudLectura(ruta, FormatoOrigen.Csv, null, 1234);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => _lector.LeerAsync(solicitud));
        Assert.Contains("Codificación no permitida.", ex.Message);
    }

    [Fact]
    public async Task LeerAsync_MdbFalso_LanzaDomainException()
    {
        var ruta = Path.Combine(_directorioTemporal, "falso.mdb");
        File.WriteAllText(ruta, "Esto no es una base de datos de Access");
        var solicitud = new SolicitudLectura(ruta, FormatoOrigen.Access, "Tabla1", null);

        await Assert.ThrowsAnyAsync<DomainException>(() => _lector.LeerAsync(solicitud));
    }
}

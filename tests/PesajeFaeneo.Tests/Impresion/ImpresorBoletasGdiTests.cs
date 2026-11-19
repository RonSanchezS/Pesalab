using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class ImpresorBoletasGdiTests
{
    private static DatosBoleta CrearDatosPrueba()
    {
        return new DatosBoleta(
            PesajeId: 1,
            NumeroBoleta: 100,
            FechaHoraLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            Empresa: new EmpresaBoleta("EMPRESA TEST", "CALLE 1", "CIUDAD", "70000000", "REG-1", "80000000", null),
            CodigoGanadero: "AH",
            NombreGanadero: "GANADERO TEST",
            Secuencia: 1,
            CodigoCliente: 10,
            NombreCliente: "CLIENTE TEST",
            Direccion: "DIRECCION TEST",
            ColorRaza: "NEGRO",
            Sexo: 'M',
            TipoDesposte: TipoDesposte.ResCompleta,
            PesoIzquierdo: 120.0m,
            PesoDerecho: 118.5m,
            PesoTotal: 238.5m,
            Observacion: "OBSERVACION DE PRUEBA",
            Origen: OrigenPeso.Simulador,
            Estado: EstadoPesaje.Vigente,
            MotivoAnulacion: null,
            NombreOperador: "OPERADOR TEST",
            Fletero: null,
            ImpresionesPrevias: 0,
            GeneradoEnLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            EsMuestra: false);
    }

    [Fact]
    public void Imprimir_ImpresoraInexistente_LanzaReglaNegocioException_IMP_IMPRESORA_NO_VALIDA()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = new NullLogger<ImpresorBoletasGdi>();
            var impresor = new ImpresorBoletasGdi(logger);
            var datos = CrearDatosPrueba();
            var nombreInexistente = "IMPRESORA-INEXISTENTE-" + Guid.NewGuid().ToString("N");
            var config = new ConfiguracionImpresion(nombreInexistente, FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);

            var ex = Assert.Throws<ReglaNegocioException>(() => impresor.Imprimir(datos, config));

            Assert.Equal("IMP_IMPRESORA_NO_VALIDA", ex.Codigo);
            Assert.Contains(nombreInexistente, ex.Message);
        });
    }

    [Fact]
    public void CrearVistaPrevia_ImpresoraInexistente_LanzaReglaNegocioException_IMP_IMPRESORA_NO_VALIDA()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = new NullLogger<ImpresorBoletasGdi>();
            var impresor = new ImpresorBoletasGdi(logger);
            var datos = CrearDatosPrueba();
            var nombreInexistente = "IMPRESORA-INEXISTENTE-" + Guid.NewGuid().ToString("N");
            var config = new ConfiguracionImpresion(nombreInexistente, FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);

            var ex = Assert.Throws<ReglaNegocioException>(() => impresor.CrearVistaPrevia(datos, config));

            Assert.Equal("IMP_IMPRESORA_NO_VALIDA", ex.Codigo);
        });
    }

    [Fact]
    public void ImprimirCalibracion_ImpresoraInexistente_LanzaReglaNegocioException_IMP_IMPRESORA_NO_VALIDA()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = new NullLogger<ImpresorBoletasGdi>();
            var impresor = new ImpresorBoletasGdi(logger);
            var nombreInexistente = "IMPRESORA-INEXISTENTE-" + Guid.NewGuid().ToString("N");
            var config = new ConfiguracionImpresion(nombreInexistente, FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);

            var ex = Assert.Throws<ReglaNegocioException>(() => impresor.ImprimirCalibracion(config));

            Assert.Equal("IMP_IMPRESORA_NO_VALIDA", ex.Codigo);
        });
    }

    [Fact]
    public void ListarPapeles_ImpresoraInexistente_DevuelveListaVacia()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = new NullLogger<ImpresorBoletasGdi>();
            var impresor = new ImpresorBoletasGdi(logger);
            var nombreInexistente = "IMPRESORA-INEXISTENTE-" + Guid.NewGuid().ToString("N");

            var papeles = impresor.ListarPapeles(nombreInexistente);

            Assert.Empty(papeles);
        });
    }

    [Fact]
    public void ListarImpresoras_Y_ObtenerImpresoraPredeterminada_NoLanzanExcepcion()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = new NullLogger<ImpresorBoletasGdi>();
            var impresor = new ImpresorBoletasGdi(logger);

            var impresoras = impresor.ListarImpresoras();
            Assert.NotNull(impresoras);

            var predeterminada = impresor.ObtenerImpresoraPredeterminada();
            // Puede ser null o string válido según la máquina donde se ejecute
            if (predeterminada is not null)
            {
                Assert.NotEmpty(predeterminada);
            }
        });
    }
}

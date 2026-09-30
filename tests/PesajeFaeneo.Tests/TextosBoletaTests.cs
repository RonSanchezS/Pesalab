using System.Globalization;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class TextosBoletaTests
{
    private static DatosBoleta CrearDatos(
        int impresionesPrevias = 0,
        bool esMuestra = false,
        EstadoPesaje estado = EstadoPesaje.Vigente,
        string? motivoAnulacion = null,
        OrigenPeso origen = OrigenPeso.Balanza)
    {
        return new DatosBoleta(
            PesajeId: esMuestra ? 0 : 1,
            NumeroBoleta: esMuestra ? 0 : 32779,
            FechaHoraLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            Empresa: new EmpresaBoleta("FRIGORÍFICO CENTRAL", "CALLE 1", "SANTA CRUZ", "70000000", "REG-1", "80000000", null),
            MarcaGanadero: "AH",
            NombreGanadero: "GANADERO EJEMPLO",
            Secuencia: 1,
            CodigoCliente: 110,
            NombreCliente: "CLIENTE EJEMPLO",
            Direccion: "DIRECCIÓN EJEMPLO",
            ColorRaza: "BLANCO",
            Sexo: 'M',
            TipoDesposte: TipoDesposte.ResCompleta,
            PesoIzquierdo: 120.0m,
            PesoDerecho: 118.5m,
            PesoTotal: 238.5m,
            Observacion: "OBSERVACIÓN EJEMPLO",
            Origen: origen,
            Estado: estado,
            MotivoAnulacion: motivoAnulacion,
            NombreOperador: "OPERADOR",
            Fleteros: Array.Empty<FleteroBoleta>(),
            ImpresionesPrevias: impresionesPrevias,
            GeneradoEnLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            EsMuestra: esMuestra);
    }

    [Fact]
    public void Peso_FormateaCorrectamenteConComaYUnidad()
    {
        Assert.Equal("238,5 kg", TextosBoleta.Peso(238.5m));
        Assert.Equal("—", TextosBoleta.Peso(0m));
        Assert.Equal("100,0 kg", TextosBoleta.Peso(100m));
    }

    [Fact]
    public void Sexo_DevuelveHembraOMacho()
    {
        Assert.Equal("HEMBRA", TextosBoleta.Sexo('H'));
        Assert.Equal("HEMBRA", TextosBoleta.Sexo('h'));
        Assert.Equal("MACHO", TextosBoleta.Sexo('M'));
        Assert.Equal("MACHO", TextosBoleta.Sexo('X'));
    }

    [Fact]
    public void Desposte_DevuelveNombreEnMayusculas()
    {
        Assert.Equal("RES COMPLETA", TextosBoleta.Desposte(TipoDesposte.ResCompleta));
        Assert.Equal("MEDIA RES", TextosBoleta.Desposte(TipoDesposte.MediaRes));
    }

    [Fact]
    public void FechaHora_EsInmuneACurrentCulture_PruebaR42()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // nl-NL o ar-SA suelen usar separadores distintos a la barra
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

            var fecha = new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified);
            var resultado = TextosBoleta.FechaHora(fecha);

            Assert.Equal("26/09/2026 22:30", resultado);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Numero_MuestraOMuestraONumero()
    {
        var muestra = CrearDatos(esMuestra: true);
        Assert.Equal("MUESTRA", TextosBoleta.Numero(muestra));

        var normal = CrearDatos(esMuestra: false);
        Assert.Equal("N° 32779", TextosBoleta.Numero(normal));
    }

    [Fact]
    public void Fletero_ConY认SinCelular()
    {
        var f1 = new FleteroBoleta(1, "JUAN PEREZ", "71234567");
        Assert.Equal("F1: JUAN PEREZ - 71234567", TextosBoleta.Fletero(f1));

        var f2 = new FleteroBoleta(2, "PEDRO GOMEZ", "");
        Assert.Equal("F2: PEDRO GOMEZ", TextosBoleta.Fletero(f2));

        var f3 = new FleteroBoleta(3, "MARIO LOPEZ", "   ");
        Assert.Equal("F3: MARIO LOPEZ", TextosBoleta.Fletero(f3));
    }

    [Fact]
    public void UnirNoVacios_OmiteNulosEspaciosYRecorta()
    {
        var res = TextosBoleta.UnirNoVacios(" - ", "  A  ", null, "   ", "B", "");
        Assert.Equal("A - B", res);
    }

    [Fact]
    public void Leyendas_OrdenExactoYPrioiridades()
    {
        // 1. Muestra tiene prioridad 1
        var datosMuestra = CrearDatos(esMuestra: true);
        var leyendasMuestra = TextosBoleta.Leyendas(datosMuestra, 1, 1);
        Assert.Equal("MUESTRA — SIN VALOR", Assert.Single(leyendasMuestra));

        // 2. Anulada
        var datosAnulada = CrearDatos(estado: EstadoPesaje.Anulado, motivoAnulacion: "Error de peso");
        var leyendasAnulada = TextosBoleta.Leyendas(datosAnulada, 1, 1);
        Assert.Equal("ANULADA: Error de peso", Assert.Single(leyendasAnulada));

        // 3. Simulado
        var datosSimulado = CrearDatos(origen: OrigenPeso.Simulador);
        var leyendasSimulado = TextosBoleta.Leyendas(datosSimulado, 1, 1);
        Assert.Equal("PESO SIMULADO — SIN VALOR COMERCIAL", Assert.Single(leyendasSimulado));

        // 4. Importado
        var datosImportado = CrearDatos(origen: OrigenPeso.Importado);
        var leyendasImportado = TextosBoleta.Leyendas(datosImportado, 1, 1);
        Assert.Equal("IMPORTADA DEL SISTEMA ANTERIOR", Assert.Single(leyendasImportado));

        // 5. Reimpresión + Copia combinadas
        var datosReimpresion = CrearDatos(impresionesPrevias: 2);
        var leyendasReimpresion = TextosBoleta.Leyendas(datosReimpresion, numeroCopia: 1, totalCopias: 3);
        Assert.Equal(2, leyendasReimpresion.Count);
        Assert.Equal("REIMPRESIÓN 2 — 26/09/2026 22:30", leyendasReimpresion[0]);
        Assert.Equal("ORIGINAL", leyendasReimpresion[1]);

        var leyendasCopia2 = TextosBoleta.Leyendas(datosReimpresion, numeroCopia: 3, totalCopias: 3);
        Assert.Equal(2, leyendasCopia2.Count);
        Assert.Equal("REIMPRESIÓN 2 — 26/09/2026 22:30", leyendasCopia2[0]);
        Assert.Equal("COPIA 2", leyendasCopia2[1]);

        // Sin copias adicionales no hay leyenda de copia
        var leyendasUnaCopia = TextosBoleta.Leyendas(CrearDatos(), 1, 1);
        Assert.Empty(leyendasUnaCopia);
    }

    [Fact]
    public void Leyendas_TodasActivas_SalenJuntasEnElOrdenExacto()
    {
        // Las leyendas de estado no se excluyen entre sí (6.2): con todas activas deben salir todas.
        var todas = CrearDatos(
            impresionesPrevias: 2,
            esMuestra: true,
            estado: EstadoPesaje.Anulado,
            motivoAnulacion: "Error de peso",
            origen: OrigenPeso.Simulador);

        var leyendas = TextosBoleta.Leyendas(todas, numeroCopia: 2, totalCopias: 3);

        Assert.Equal(
            new[]
            {
                "MUESTRA — SIN VALOR",
                "ANULADA: Error de peso",
                "PESO SIMULADO — SIN VALOR COMERCIAL",
                "REIMPRESIÓN 2 — 26/09/2026 22:30",
                "COPIA 1"
            },
            leyendas);

        var anuladaImportada = CrearDatos(estado: EstadoPesaje.Anulado, motivoAnulacion: "Duplicada", origen: OrigenPeso.Importado);
        Assert.Equal(
            new[] { "ANULADA: Duplicada", "IMPORTADA DEL SISTEMA ANTERIOR" },
            TextosBoleta.Leyendas(anuladaImportada, 1, 1));
    }

    [Fact]
    public void MarcaDeAgua_RespetaPrioridadEImportadoEsNull()
    {
        Assert.Equal("MUESTRA", TextosBoleta.MarcaDeAgua(CrearDatos(esMuestra: true)));
        Assert.Equal("ANULADA", TextosBoleta.MarcaDeAgua(CrearDatos(estado: EstadoPesaje.Anulado, motivoAnulacion: "X")));
        Assert.Equal("SIMULADO", TextosBoleta.MarcaDeAgua(CrearDatos(origen: OrigenPeso.Simulador)));
        Assert.Null(TextosBoleta.MarcaDeAgua(CrearDatos(origen: OrigenPeso.Importado)));
        Assert.Null(TextosBoleta.MarcaDeAgua(CrearDatos(origen: OrigenPeso.Balanza)));
    }
}

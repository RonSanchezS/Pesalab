using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DisenoBoletaTests
{
    private static string TextosDe(PaginaBoleta pagina)
    {
        return string.Join(" ", pagina.Elementos.OfType<TextoBoleta>().Select(t => t.Texto));
    }

    private static DatosBoleta CrearDatosPrueba(
        string? rutaLogo = null,
        TipoDesposte desposte = TipoDesposte.ResCompleta,
        decimal izq = 120.0m,
        decimal der = 118.5m,
        decimal total = 238.5m,
        OrigenPeso origen = OrigenPeso.Balanza,
        EstadoPesaje estado = EstadoPesaje.Vigente,
        IReadOnlyList<FleteroBoleta>? fleteros = null,
        string observacion = "SIN NOVEDAD",
        string nombreGanadero = "GANADERO EJEMPLO",
        string nombreCliente = "CLIENTE EJEMPLO",
        string direccion = "CALLE EJEMPLO 123")
    {
        return new DatosBoleta(
            PesajeId: 1,
            NumeroBoleta: 32779,
            FechaHoraLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            Empresa: new EmpresaBoleta("FRIGORÍFICO CENTRAL", "CALLE 1", "SANTA CRUZ", "70000000", "REG-1", "80000000", rutaLogo),
            MarcaGanadero: "AH",
            NombreGanadero: nombreGanadero,
            Secuencia: 1,
            CodigoCliente: 110,
            NombreCliente: nombreCliente,
            Direccion: direccion,
            ColorRaza: "BLANCO",
            Sexo: 'M',
            TipoDesposte: desposte,
            PesoIzquierdo: izq,
            PesoDerecho: der,
            PesoTotal: total,
            Observacion: observacion,
            Origen: origen,
            Estado: estado,
            MotivoAnulacion: null,
            NombreOperador: "JUAN OPERADOR",
            Fleteros: fleteros ?? Array.Empty<FleteroBoleta>(),
            ImpresionesPrevias: 0,
            GeneradoEnLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            EsMuestra: false);
    }

    [Fact]
    public void MediaCarta_ContieneDatosPrincipales()
    {
        var datos = CrearDatosPrueba();
        var pagina = DisenoBoleta.Crear(datos, FormatoBoleta.MediaCarta, 1, 1);
        var texto = TextosDe(pagina);

        Assert.Contains("N° 32779", texto);
        Assert.Contains("AH - GANADERO EJEMPLO", texto);
        Assert.Contains("110 - CLIENTE EJEMPLO", texto);
        Assert.Contains("120,0 kg", texto);
        Assert.Contains("118,5 kg", texto);
        Assert.Contains("238,5 kg", texto);
        Assert.Contains("26/09/2026", texto);
        Assert.Contains("FRIGORÍFICO CENTRAL", texto);
    }

    [Fact]
    public void MediaRes_ValorDerechoMuestraGuion()
    {
        var datos = CrearDatosPrueba(desposte: TipoDesposte.MediaRes, izq: 106.0m, der: 0m, total: 106.0m);
        var pagina = DisenoBoleta.Crear(datos, FormatoBoleta.MediaCarta, 1, 1);

        var textosPesos = pagina.Elementos.OfType<TextoBoleta>()
            .Where(t => t.Y == 80f)
            .Select(t => t.Texto)
            .ToList();

        Assert.Equal("106,0 kg", textosPesos[0]);
        Assert.Equal("—", textosPesos[1]);
        Assert.Equal("106,0 kg", textosPesos[2]);
    }

    [Fact]
    public void Simulado_MarcaDeAguaGrisEsPrimerElemento_VigenteNoTieneGris()
    {
        var simulado = CrearDatosPrueba(origen: OrigenPeso.Simulador);
        var pagSimulada = DisenoBoleta.Crear(simulado, FormatoBoleta.MediaCarta, 1, 1);

        var primerElemento = Assert.IsType<TextoBoleta>(pagSimulada.Elementos[0]);
        Assert.Equal("SIMULADO", primerElemento.Texto);
        Assert.Equal(TonoTinta.Gris, primerElemento.Tono);
        Assert.Contains("PESO SIMULADO — SIN VALOR COMERCIAL", TextosDe(pagSimulada));

        var balanza = CrearDatosPrueba(origen: OrigenPeso.Balanza);
        var pagBalanza = DisenoBoleta.Crear(balanza, FormatoBoleta.MediaCarta, 1, 1);
        Assert.DoesNotContain(pagBalanza.Elementos.OfType<TextoBoleta>(), t => t.Tono == TonoTinta.Gris);
    }

    [Fact]
    public void Logo_PresenteOAusenteSegunRuta()
    {
        var sinLogo = CrearDatosPrueba(rutaLogo: null);
        var pagSinLogo = DisenoBoleta.Crear(sinLogo, FormatoBoleta.MediaCarta, 1, 1);
        Assert.Empty(pagSinLogo.Elementos.OfType<LogoBoleta>());

        var conLogo = CrearDatosPrueba(rutaLogo: @"C:\logo.png");
        var pagConLogo = DisenoBoleta.Crear(conLogo, FormatoBoleta.MediaCarta, 1, 1);
        Assert.Single(pagConLogo.Elementos.OfType<LogoBoleta>());
    }

    [Fact]
    public void Fleteros_ZeroNoMuestraEtiqueta_CuatroMuestraCuatro()
    {
        var sinFleteros = CrearDatosPrueba(fleteros: Array.Empty<FleteroBoleta>());
        var pagSinFleteros = DisenoBoleta.Crear(sinFleteros, FormatoBoleta.MediaCarta, 1, 1);
        Assert.DoesNotContain("Fleteros:", TextosDe(pagSinFleteros));

        var cuatro = new[]
        {
            new FleteroBoleta(1, "FL1", "701"),
            new FleteroBoleta(2, "FL2", "702"),
            new FleteroBoleta(3, "FL3", "703"),
            new FleteroBoleta(4, "FL4", "704")
        };
        var conFleteros = CrearDatosPrueba(fleteros: cuatro);
        var pagConFleteros = DisenoBoleta.Crear(conFleteros, FormatoBoleta.MediaCarta, 1, 1);
        var texto = TextosDe(pagConFleteros);

        Assert.Contains("Fleteros:", texto);
        Assert.Contains("F1: FL1 - 701", texto);
        Assert.Contains("F2: FL2 - 702", texto);
        Assert.Contains("F3: FL3 - 703", texto);
        Assert.Contains("F4: FL4 - 704", texto);
    }

    [Theory]
    [InlineData(FormatoBoleta.MediaCarta)]
    [InlineData(FormatoBoleta.Ticket80mm)]
    public void Limites_ConTextosMaximos_TodosLosElementosDentroDeLaPagina_PruebaR18(FormatoBoleta formato)
    {
        var cuatro = new[]
        {
            new FleteroBoleta(1, new string('F', 30), "70000000"),
            new FleteroBoleta(2, new string('F', 30), "70000000"),
            new FleteroBoleta(3, new string('F', 30), "70000000"),
            new FleteroBoleta(4, new string('F', 30), "70000000")
        };

        var datos = CrearDatosPrueba(
            rutaLogo: @"C:\logo.png",
            nombreGanadero: new string('G', 100),
            nombreCliente: new string('C', 100),
            direccion: new string('D', 150),
            observacion: new string('O', 250),
            origen: OrigenPeso.Simulador,
            fleteros: cuatro);

        var pagina = DisenoBoleta.Crear(datos, formato, 1, 2);

        foreach (var el in pagina.Elementos)
        {
            switch (el)
            {
                case TextoBoleta t:
                    Assert.True(t.X >= 0, $"Texto X < 0: {t.Texto}");
                    Assert.True(t.Y >= 0, $"Texto Y < 0: {t.Texto}");
                    Assert.True(t.X + t.Ancho <= pagina.AnchoMm + 0.01f, $"Texto excede ancho: {t.Texto} (X={t.X}, Ancho={t.Ancho}, Pagina={pagina.AnchoMm})");
                    Assert.True(t.Y + t.Alto <= pagina.AltoMm + 0.01f, $"Texto excede alto: {t.Texto} (Y={t.Y}, Alto={t.Alto}, Pagina={pagina.AltoMm})");
                    break;
                case RectanguloBoleta r:
                    Assert.True(r.X >= 0, "Rectangulo X < 0");
                    Assert.True(r.Y >= 0, "Rectangulo Y < 0");
                    Assert.True(r.X + r.Ancho <= pagina.AnchoMm + 0.01f, "Rectangulo excede ancho");
                    Assert.True(r.Y + r.Alto <= pagina.AltoMm + 0.01f, "Rectangulo excede alto");
                    break;
                case LineaBoleta l:
                    Assert.True(l.X1 >= 0 && l.X1 <= pagina.AnchoMm + 0.01f, "Linea X1 fuera de límites");
                    Assert.True(l.X2 >= 0 && l.X2 <= pagina.AnchoMm + 0.01f, "Linea X2 fuera de límites");
                    Assert.True(l.Y1 >= 0 && l.Y1 <= pagina.AltoMm + 0.01f, "Linea Y1 fuera de límites");
                    Assert.True(l.Y2 >= 0 && l.Y2 <= pagina.AltoMm + 0.01f, "Linea Y2 fuera de límites");
                    break;
                case LogoBoleta logo:
                    Assert.True(logo.X >= 0, "Logo X < 0");
                    Assert.True(logo.Y >= 0, "Logo Y < 0");
                    Assert.True(logo.X + logo.Ancho <= pagina.AnchoMm + 0.01f, "Logo excede ancho");
                    Assert.True(logo.Y + logo.Alto <= pagina.AltoMm + 0.01f, "Logo excede alto");
                    break;
            }
        }
    }

    [Fact]
    public void Ticket80mm_AltoCreceConFleterosYTextosTienenAncho72()
    {
        var sinFleteros = CrearDatosPrueba(fleteros: Array.Empty<FleteroBoleta>());
        var pagSin = DisenoBoleta.Crear(sinFleteros, FormatoBoleta.Ticket80mm, 1, 1);

        var cuatro = new[]
        {
            new FleteroBoleta(1, "FL1", "701"),
            new FleteroBoleta(2, "FL2", "702"),
            new FleteroBoleta(3, "FL3", "703"),
            new FleteroBoleta(4, "FL4", "704")
        };
        var conCuatro = CrearDatosPrueba(fleteros: cuatro);
        var pagCon = DisenoBoleta.Crear(conCuatro, FormatoBoleta.Ticket80mm, 1, 1);

        Assert.True(pagCon.AltoMm > pagSin.AltoMm, "El alto con 4 fleteros debe ser mayor que sin fleteros");

        foreach (var t in pagCon.Elementos.OfType<TextoBoleta>())
        {
            Assert.Equal(4f, t.X);
            Assert.Equal(72f, t.Ancho);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(-1, 2)]
    [InlineData(3, 2)]
    public void Crear_CopiaFueraDeRango_LanzaArgumentException(int copia, int total)
    {
        var datos = CrearDatosPrueba();
        Assert.Throws<ArgumentException>(() => DisenoBoleta.Crear(datos, FormatoBoleta.MediaCarta, copia, total));
    }
}

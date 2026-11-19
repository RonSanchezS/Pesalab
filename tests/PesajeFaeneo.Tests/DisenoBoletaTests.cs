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

    private static readonly FleteroBoleta FleteroDePrueba = new(7, "FLETERO SIETE", "70012345");

    private static DatosBoleta CrearDatosPrueba(
        string? rutaLogo = null,
        TipoDesposte desposte = TipoDesposte.ResCompleta,
        decimal izq = 120.0m,
        decimal der = 118.5m,
        decimal total = 238.5m,
        OrigenPeso origen = OrigenPeso.Balanza,
        EstadoPesaje estado = EstadoPesaje.Vigente,
        FleteroBoleta? fletero = null,
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
            CodigoGanadero: "AH",
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
            Fletero: fletero,
            ImpresionesPrevias: 0,
            GeneradoEnLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            EsMuestra: false);
    }

    private static DatosBoleta CrearDatosAnuladaSimulada(string motivo, int impresionesPrevias, bool esMuestra)
    {
        return new DatosBoleta(
            PesajeId: 1,
            NumeroBoleta: 32779,
            FechaHoraLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            Empresa: new EmpresaBoleta("FRIGORÍFICO CENTRAL", "CALLE 1", "SANTA CRUZ", "70000000", "REG-1", "80000000", null),
            CodigoGanadero: "AH",
            NombreGanadero: "GANADERO",
            Secuencia: 1,
            CodigoCliente: 110,
            NombreCliente: "CLIENTE",
            Direccion: "DIR",
            ColorRaza: "BLANCO",
            Sexo: 'M',
            TipoDesposte: TipoDesposte.MediaRes,
            PesoIzquierdo: 100m,
            PesoDerecho: 0m,
            PesoTotal: 100m,
            Observacion: "OBS",
            Origen: OrigenPeso.Simulador,
            Estado: EstadoPesaje.Anulado,
            MotivoAnulacion: motivo,
            NombreOperador: "OP",
            Fletero: null,
            ImpresionesPrevias: impresionesPrevias,
            GeneradoEnLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            EsMuestra: esMuestra);
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
    public void FondoLogo_SoloEnMediaCartaConLogo_PrimeroYDentroDeLaPagina()
    {
        var conLogo = CrearDatosPrueba(rutaLogo: @"C:\logo.png");

        var media = DisenoBoleta.Crear(conLogo, FormatoBoleta.MediaCarta, 1, 1);
        var fondo = Assert.IsType<FondoLogoBoleta>(media.Elementos[0]);
        Assert.InRange(fondo.Opacidad, 0.05f, 0.3f);
        Assert.True(fondo.X >= 0 && fondo.X + fondo.Ancho <= media.AnchoMm);
        Assert.True(fondo.Y >= 0 && fondo.Y + fondo.Alto <= media.AltoMm);

        Assert.Empty(DisenoBoleta.Crear(conLogo, FormatoBoleta.Ticket80mm, 1, 1).Elementos.OfType<FondoLogoBoleta>());
        Assert.Empty(DisenoBoleta.Crear(CrearDatosPrueba(), FormatoBoleta.MediaCarta, 1, 1).Elementos.OfType<FondoLogoBoleta>());
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
    public void MediaCarta_FleteroEnLaZonaDeDatos_AlLadoDeLaDireccion()
    {
        var pagina = DisenoBoleta.Crear(CrearDatosPrueba(fletero: FleteroDePrueba), FormatoBoleta.MediaCarta, 1, 1);
        var textos = pagina.Elementos.OfType<TextoBoleta>().ToList();

        var etiqueta = Assert.Single(textos, t => t.Texto == "Fletero:");
        var valor = Assert.Single(textos, t => t.Texto == "FLETERO SIETE — Cel. 70012345");
        var etiquetaDireccion = Assert.Single(textos, t => t.Texto == "Dirección:");

        // Misma fila que la dirección (fila 4 de datos), en la columna derecha, antes de los recuadros de pesos.
        Assert.Equal(etiquetaDireccion.Y, etiqueta.Y);
        Assert.Equal(etiqueta.Y, valor.Y);
        Assert.True(etiqueta.X >= 126f);
        Assert.True(valor.X >= etiqueta.X + etiqueta.Ancho);
        var recuadroPesos = pagina.Elementos.OfType<RectanguloBoleta>().First(r => r.Y == 73f);
        Assert.True(valor.Y + valor.Alto <= recuadroPesos.Y, "El fletero no debe invadir los recuadros de pesos");
    }

    [Fact]
    public void MediaCarta_SinFletero_NoImprimeLaFilaDelFletero_PeroLaFirmaDelFleteroSiempreEsta()
    {
        var pagina = DisenoBoleta.Crear(CrearDatosPrueba(fletero: null), FormatoBoleta.MediaCarta, 1, 1);
        var texto = TextosDe(pagina);

        Assert.DoesNotContain("Fletero:", texto);
        Assert.Contains(TextosBoleta.FirmaFletero, texto);
    }

    [Fact]
    public void MediaCarta_BloqueInferior_TresFirmas_ElCentroEsLaFirmaDelFleteroConSuNombre()
    {
        var pagina = DisenoBoleta.Crear(CrearDatosPrueba(fletero: FleteroDePrueba), FormatoBoleta.MediaCarta, 1, 1);

        var recuadros = pagina.Elementos.OfType<RectanguloBoleta>().Where(r => r.Y == 107f).OrderBy(r => r.X).ToList();
        Assert.Equal(3, recuadros.Count);

        var centro = recuadros[1];
        var textosCentro = pagina.Elementos.OfType<TextoBoleta>()
            .Where(t => t.X >= centro.X && t.X + t.Ancho <= centro.X + centro.Ancho + 0.01f && t.Y >= centro.Y && t.Y < centro.Y + centro.Alto)
            .Select(t => t.Texto)
            .ToList();

        Assert.Contains(TextosBoleta.FirmaFletero, textosCentro);
        Assert.Contains("FLETERO SIETE", textosCentro); // nombre sobre la línea de firma
        // Ya no hay lista de fleteros con casillas ni "Otros".
        var texto = TextosDe(pagina);
        Assert.DoesNotContain("Otros:", texto);
        Assert.DoesNotContain("Fleteros:", texto);
        Assert.DoesNotContain(pagina.Elementos.OfType<RectanguloBoleta>(), r => r.Ancho == 3f && r.Alto == 3f);
    }

    [Theory]
    [InlineData(FormatoBoleta.MediaCarta)]
    [InlineData(FormatoBoleta.Ticket80mm)]
    public void FormatoLegado_FirmasJefeDeDespachoFleteroYMedicoVeterinario(FormatoBoleta formato)
    {
        var pagina = DisenoBoleta.Crear(CrearDatosPrueba(), formato, 1, 1);
        var texto = TextosDe(pagina);

        Assert.Contains(TextosBoleta.FirmaJefeDespacho, texto);
        Assert.Contains(TextosBoleta.FirmaFletero, texto);
        Assert.Contains(TextosBoleta.FirmaMedicoVeterinario, texto);
        Assert.DoesNotContain("Recibí conforme", texto);
    }

    [Fact]
    public void Ticket80mm_FleteroEnLaZonaDeDatos_YSinListaNiCasillas()
    {
        var pagina = DisenoBoleta.Crear(CrearDatosPrueba(fletero: new FleteroBoleta(3, "FL3", string.Empty)), FormatoBoleta.Ticket80mm, 1, 1);
        var textos = pagina.Elementos.OfType<TextoBoleta>().ToList();

        var fleteroDatos = Assert.Single(textos, t => t.Texto.StartsWith("Fletero: ", StringComparison.Ordinal));
        Assert.Equal("Fletero: FL3", fleteroDatos.Texto);

        // Entre "Dirección" y "Color/Raza", dentro de los datos del pesaje.
        var direccion = Assert.Single(textos, t => t.Texto.StartsWith("Dirección:", StringComparison.Ordinal));
        var color = Assert.Single(textos, t => t.Texto.StartsWith("Color/Raza:", StringComparison.Ordinal));
        Assert.True(direccion.Y < fleteroDatos.Y && fleteroDatos.Y < color.Y);

        var texto = TextosDe(pagina);
        Assert.DoesNotContain("[   ]", texto);
        Assert.DoesNotContain("Otros:", texto);
        Assert.DoesNotContain("Fleteros:", texto);
    }

    [Fact]
    public void Ticket80mm_ConCelular_ImprimeNombreYCelular_SinFletero_NoImprimeLaLinea()
    {
        var con = TextosDe(DisenoBoleta.Crear(CrearDatosPrueba(fletero: FleteroDePrueba), FormatoBoleta.Ticket80mm, 1, 1));
        Assert.Contains("Fletero: FLETERO SIETE — Cel. 70012345", con);

        var sin = TextosDe(DisenoBoleta.Crear(CrearDatosPrueba(fletero: null), FormatoBoleta.Ticket80mm, 1, 1));
        Assert.DoesNotContain("Fletero: ", sin);
        Assert.Contains(TextosBoleta.FirmaFletero, sin); // la firma del fletero siempre figura
    }

    [Theory]
    [InlineData(FormatoBoleta.MediaCarta)]
    [InlineData(FormatoBoleta.Ticket80mm)]
    public void Limites_ConTextosMaximos_TodosLosElementosDentroDeLaPagina_PruebaR18(FormatoBoleta formato)
    {
        var datos = CrearDatosPrueba(
            rutaLogo: @"C:\logo.png",
            nombreGanadero: new string('G', 100),
            nombreCliente: new string('C', 100),
            direccion: new string('D', 150),
            observacion: new string('O', 250),
            origen: OrigenPeso.Simulador,
            fletero: new FleteroBoleta(99, new string('F', 100), "70000000"));

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

    // F10 (O9): ningún texto debe pisar a otro (la marca de agua gris va debajo de los pesos a propósito).
    [Theory]
    [InlineData(FormatoBoleta.MediaCarta, false)]
    [InlineData(FormatoBoleta.MediaCarta, true)]
    [InlineData(FormatoBoleta.Ticket80mm, false)]
    [InlineData(FormatoBoleta.Ticket80mm, true)]
    public void SinSolapes_NingunTextoPisaAOtroNiAUnaLinea(FormatoBoleta formato, bool conFletero)
    {
        var datos = CrearDatosPrueba(rutaLogo: @"C:\logo.png", fletero: conFletero ? FleteroDePrueba : null);
        var pagina = DisenoBoleta.Crear(datos, formato, 1, 2);
        var textos = pagina.Elementos.OfType<TextoBoleta>().Where(t => t.Tono != TonoTinta.Gris && !string.IsNullOrWhiteSpace(t.Texto)).ToList();

        for (var i = 0; i < textos.Count; i++)
        {
            for (var j = i + 1; j < textos.Count; j++)
            {
                var a = textos[i];
                var b = textos[j];
                var ancho = Math.Min(a.X + a.Ancho, b.X + b.Ancho) - Math.Max(a.X, b.X);
                var alto = Math.Min(a.Y + a.Alto, b.Y + b.Alto) - Math.Max(a.Y, b.Y);
                Assert.False(ancho > 0.01f && alto > 0.01f, $"Se solapan '{a.Texto}' ({a.X},{a.Y},{a.Ancho},{a.Alto}) y '{b.Texto}' ({b.X},{b.Y},{b.Ancho},{b.Alto})");
            }
        }

        // Las líneas horizontales (separadores y firmas) tampoco cortan una caja de texto.
        foreach (var l in pagina.Elementos.OfType<LineaBoleta>().Where(l => l.Y1 == l.Y2))
        {
            foreach (var t in textos)
            {
                var cruzaEnX = Math.Min(l.X2, t.X + t.Ancho) - Math.Max(l.X1, t.X) > 0.01f;
                Assert.False(cruzaEnX && l.Y1 > t.Y + 0.01f && l.Y1 < t.Y + t.Alto - 0.01f, $"La línea y={l.Y1} corta el texto '{t.Texto}'");
            }
        }
    }

    // F10 (O9): una caja de una línea con alto menor que la línea de la fuente no se dibuja (LineLimit): texto cortado.
    [Theory]
    [InlineData(FormatoBoleta.MediaCarta)]
    [InlineData(FormatoBoleta.Ticket80mm)]
    public void TodoTexto_TieneAltoDeCajaSuficienteParaSuFuente(FormatoBoleta formato)
    {
        var datos = CrearDatosPrueba(rutaLogo: @"C:\logo.png", fletero: FleteroDePrueba, origen: OrigenPeso.Simulador);
        var pagina = DisenoBoleta.Crear(datos, formato, 1, 2);

        foreach (var t in pagina.Elementos.OfType<TextoBoleta>().Where(t => !string.IsNullOrWhiteSpace(t.Texto)))
        {
            var alturaLineaMm = t.TamanoPt * 0.3528f * 1.15f; // alto de línea de Arial
            Assert.True(t.Alto >= alturaLineaMm - 0.01f, $"'{t.Texto}': caja de {t.Alto} mm < línea de {alturaLineaMm:0.00} mm ({t.TamanoPt} pt)");
        }
    }

    [Fact]
    public void Ticket80mm_AltoCreceConFleteroYTextosTienenAncho72()
    {
        var pagSin = DisenoBoleta.Crear(CrearDatosPrueba(fletero: null), FormatoBoleta.Ticket80mm, 1, 1);
        var pagCon = DisenoBoleta.Crear(CrearDatosPrueba(fletero: FleteroDePrueba), FormatoBoleta.Ticket80mm, 1, 1);

        Assert.True(pagCon.AltoMm > pagSin.AltoMm, "El alto con fletero debe ser mayor que sin fletero");

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

    [Fact]
    public void MediaCarta_Leyendas_TamanoAdaptativoSegunLongitud()
    {
        // 1. Solo simulado (35 caracteres <= 85): TamanoPt == 9
        var datosSimulado = CrearDatosPrueba(origen: OrigenPeso.Simulador);
        var pagSimulado = DisenoBoleta.Crear(datosSimulado, FormatoBoleta.MediaCarta, 1, 1);
        var txtSimulado = pagSimulado.Elementos.OfType<TextoBoleta>().Single(t => t.Y == 31f);
        Assert.Equal(9f, txtSimulado.TamanoPt);

        // 2. Rango medio (86..110 caracteres): anulada con motivo 30 + simulado + original (total ~92 chars): TamanoPt == 7
        var datosMedio = CrearDatosAnuladaSimulada(new string('A', 30), impresionesPrevias: 0, esMuestra: false);
        var pagMedio = DisenoBoleta.Crear(datosMedio, FormatoBoleta.MediaCarta, 1, 2);
        var txtMedio = pagMedio.Elementos.OfType<TextoBoleta>().Single(t => t.Y == 31f);
        Assert.Equal(7f, txtMedio.TamanoPt);

        // 3. Más de 110 caracteres (las 5 leyendas activas): TamanoPt == 6
        var datosCinco = CrearDatosAnuladaSimulada(new string('A', 30), impresionesPrevias: 1, esMuestra: true);
        var pagCinco = DisenoBoleta.Crear(datosCinco, FormatoBoleta.MediaCarta, 2, 2);
        var txtCinco = pagCinco.Elementos.OfType<TextoBoleta>().Single(t => t.Y == 31f);
        Assert.Equal(6f, txtCinco.TamanoPt);
    }

    [Fact]
    public void Ticket80mm_MultiplesLeyendas_SigueConUnaFilaPorLeyenda()
    {
        var datos = CrearDatosAnuladaSimulada("MOTIVO CORTO", impresionesPrevias: 0, esMuestra: false);

        var pag = DisenoBoleta.Crear(datos, FormatoBoleta.Ticket80mm, 1, 1);
        var leyendas = pag.Elementos.OfType<TextoBoleta>()
            .Where(t => t.Texto.StartsWith("ANULADA:") || t.Texto.StartsWith("PESO SIMULADO"))
            .ToList();

        Assert.Equal(2, leyendas.Count);
        Assert.NotEqual(leyendas[0].Y, leyendas[1].Y);
    }

    // ---- F11 (D6): boleta sin desposte cuando la empresa pesa un solo tipo ----

    [Theory]
    [InlineData(FormatoBoleta.MediaCarta)]
    [InlineData(FormatoBoleta.Ticket80mm)]
    public void SinDesposte_NoImprimeElRotuloNiElValor(FormatoBoleta formato)
    {
        var con = TextosDe(DisenoBoleta.Crear(CrearDatosPrueba(), formato, 1, 1));
        var sin = TextosDe(DisenoBoleta.Crear(CrearDatosPrueba() with { MostrarDesposte = false }, formato, 1, 1));

        Assert.Contains("Desposte", con);
        Assert.Contains("RES COMPLETA", con);
        Assert.DoesNotContain("Desposte", sin);
        Assert.DoesNotContain("RES COMPLETA", sin);
    }

    [Fact]
    public void MediaCarta_SinDesposte_LasDemasFilasConservanSusCoordenadas()
    {
        var con = DisenoBoleta.Crear(CrearDatosPrueba(fletero: FleteroDePrueba), FormatoBoleta.MediaCarta, 1, 1);
        var sin = DisenoBoleta.Crear(CrearDatosPrueba(fletero: FleteroDePrueba) with { MostrarDesposte = false }, FormatoBoleta.MediaCarta, 1, 1);

        var esperados = con.Elementos.OfType<TextoBoleta>()
            .Where(t => t.Texto != "Desposte:" && t.Texto != "RES COMPLETA")
            .Select(t => (t.Texto, t.X, t.Y))
            .ToList();
        var reales = sin.Elementos.OfType<TextoBoleta>().Select(t => (t.Texto, t.X, t.Y)).ToList();

        Assert.Equal(esperados, reales);
        Assert.Equal(con.Elementos.OfType<LineaBoleta>().Count(), sin.Elementos.OfType<LineaBoleta>().Count());
    }

    [Fact]
    public void Ticket80mm_SinDesposte_EsMasCortoYSinSolapes()
    {
        var con = DisenoBoleta.Crear(CrearDatosPrueba(), FormatoBoleta.Ticket80mm, 1, 1);
        var sin = DisenoBoleta.Crear(CrearDatosPrueba() with { MostrarDesposte = false }, FormatoBoleta.Ticket80mm, 1, 1);

        Assert.True(sin.AltoMm < con.AltoMm);

        var textos = sin.Elementos.OfType<TextoBoleta>().Where(t => !string.IsNullOrWhiteSpace(t.Texto)).ToList();
        for (var i = 0; i < textos.Count; i++)
        {
            for (var j = i + 1; j < textos.Count; j++)
            {
                var a = textos[i];
                var b = textos[j];
                var ancho = Math.Min(a.X + a.Ancho, b.X + b.Ancho) - Math.Max(a.X, b.X);
                var alto = Math.Min(a.Y + a.Alto, b.Y + b.Alto) - Math.Max(a.Y, b.Y);
                Assert.False(ancho > 0.01f && alto > 0.01f, $"Se solapan '{a.Texto}' y '{b.Texto}'");
            }
        }
    }
}

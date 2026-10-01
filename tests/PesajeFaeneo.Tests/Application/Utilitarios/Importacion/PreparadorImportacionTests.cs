using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using Xunit;

namespace PesajeFaeneo.Tests.Application.Utilitarios.Importacion;

public class PreparadorImportacionTests
{
    private static readonly DateTime AhoraUtc = new(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo ZonaUtcMenos4 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");

    private static readonly ClavesExistentes ClavesPrueba = new(
        new Dictionary<string, int>(StringComparer.Ordinal) { ["AB"] = 1 },
        new Dictionary<int, ClienteExistente> { [10] = new(2, "AV. BANZER") },
        new Dictionary<long, OrigenPeso>
        {
            [500] = OrigenPeso.Balanza,
            [600] = OrigenPeso.Importado
        });

    [Fact]
    public void Preparar_ValidacionMapeo_FaltaObligatorioIndiceInvalidoYRepetido_LanzaUnaSolaValidacionExceptionConTresErrores()
    {
        var cols = new[] { "C1", "C2" };
        var filas = Array.Empty<FilaOrigen>();
        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());

        // Clientes: Codigo (oblig), Nombre (oblig), Direccion (opc), Activo (opc)
        // Falta obligatorio: Nombre = null (M1)
        // Índice fuera de rango: Activo = 99 (M2)
        // Columna repetida: Codigo = 0 y Direccion = 0 (M3)
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.Codigo] = 0,
            [CamposImportacion.Direccion] = 0, // M3
            [CamposImportacion.Activo] = 99,   // M2
            [CamposImportacion.Nombre] = null  // M1
        };

        var ex = Assert.Throws<ValidacionException>(() =>
            PreparadorImportacion.Preparar(TipoImportacion.Clientes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4));

        Assert.Equal(3, ex.Errores.Count);
        Assert.Contains(ex.Errores, e => e.Campo == CamposImportacion.Nombre);
        Assert.Contains(ex.Errores, e => e.Campo == CamposImportacion.Activo);
        Assert.Contains(ex.Errores, e => e.Campo == CamposImportacion.Mapeo);
    }

    [Fact]
    public void Preparar_Ganaderos_NormalizacionOmitidasErroresYConteosExactos()
    {
        var cols = new[] { "Marca", "Nombre", "Activo" };
        var nombre120 = new string('A', 120);

        var filas = new List<FilaOrigen>
        {
            new(2, new[] { " ab ", "Ganadero Uno", "SI" }), // Ya existe AB -> Omitida
            new(3, new[] { "cd", nombre120, "SI" }),        // Válida con recorte de nombre -> Advertencia
            new(4, new[] { "ABCDEF", "Nombre Ok", "SI" }),  // Marca > 5 caracteres -> Error
            new(5, new[] { "EF", "Nombre Ok", "quizá" }),   // Activo no reconocido -> Error
            new(6, new[] { "CD", "Otro Nombre", "SI" })     // Marca CD repetida en archivo -> Error en la segunda
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.Marca] = 0,
            [CamposImportacion.Nombre] = 1,
            [CamposImportacion.Activo] = 2
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Ganaderos, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Single(plan.Ganaderos);
        Assert.Equal("CD", plan.Ganaderos[0].Marca);
        Assert.Equal(100, plan.Ganaderos[0].Nombre.Length);

        Assert.Equal(3, plan.FilasConError);      // Filas 4, 5, 6
        Assert.Equal(1, plan.FilasOmitidas);    // Fila 2
        Assert.Equal(1, plan.FilasConAdvertencia); // Fila 3
        Assert.Contains(plan.Incidencias, i => i.Fila == 6 && i.Mensaje.Contains("primera vez en la fila 3"));
    }

    [Fact]
    public void Preparar_Clientes_CodigosValidosInvalidosOmitidosYDireccionRecortada()
    {
        var cols = new[] { "Codigo", "Nombre", "Direccion", "Activo" };
        var dir160 = new string('D', 160);

        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "12.0", "Cliente Doce", "Calle 1", "SI" }), // Válido -> 12
            new(3, new[] { "abc", "Cliente Inv", "Calle 2", "SI" }),   // Código inválido -> Error
            new(4, new[] { "0", "Cliente Cero", "Calle 3", "SI" }),     // Código fuera de 1..999999 -> Error
            new(5, new[] { "10", "Cliente Diez", "Calle 4", "SI" }),    // Ya existe 10 -> Omitida
            new(6, new[] { "15", "Cliente Quince", dir160, "SI" })      // Dirección > 150 -> Advertencia
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.Codigo] = 0,
            [CamposImportacion.Nombre] = 1,
            [CamposImportacion.Direccion] = 2,
            [CamposImportacion.Activo] = 3
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Clientes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Equal(2, plan.Clientes.Count);
        Assert.Equal(12, plan.Clientes[0].Codigo);
        Assert.Equal(15, plan.Clientes[1].Codigo);
        Assert.Equal(150, plan.Clientes[1].Direccion.Length);

        Assert.Equal(2, plan.FilasConError);
        Assert.Equal(1, plan.FilasOmitidas);
        Assert.Equal(1, plan.FilasConAdvertencia);
    }

    [Fact]
    public void Preparar_Pesajes_CasoCompletoValido_DeduceValoresCorrectamente()
    {
        var cols = new[]
        {
            "Num", "Fecha", "Hora", "Marca", "Cli", "Sec", "Dir", "Col", "Sex", "Desp",
            "Izq", "Der", "Tot", "Obs", "Est", "Mot"
        };

        var fila = new FilaOrigen(2, new[]
        {
            "700", "01/09/2026", "22:30", "ab", "10", "", "", "", "", "",
            "109,3", "106,7", "216,0", "", "", ""
        });

        var tabla = new TablaOrigen(cols, new[] { fila }, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.Hora] = 2,
            [CamposImportacion.MarcaGanadero] = 3,
            [CamposImportacion.CodigoCliente] = 4,
            [CamposImportacion.Secuencia] = 5,
            [CamposImportacion.Direccion] = 6,
            [CamposImportacion.ColorRaza] = 7,
            [CamposImportacion.Sexo] = 8,
            [CamposImportacion.TipoDesposte] = 9,
            [CamposImportacion.PesoIzquierdo] = 10,
            [CamposImportacion.PesoDerecho] = 11,
            [CamposImportacion.PesoTotal] = 12,
            [CamposImportacion.Observacion] = 13,
            [CamposImportacion.Estado] = 14,
            [CamposImportacion.MotivoAnulacion] = 15
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Single(plan.Pesajes);
        var p = plan.Pesajes[0];

        Assert.Equal(700L, p.NumeroBoleta);
        Assert.Equal(new DateTime(2026, 9, 2, 2, 30, 0, DateTimeKind.Utc), p.FechaHoraUtc);
        Assert.Equal(1, p.GanaderoId);
        Assert.Equal(2, p.ClienteId);
        Assert.Equal("AV. BANZER", p.Direccion);
        Assert.Equal(TipoDesposte.ResCompleta, p.TipoDesposte);
        Assert.Equal(216.0m, p.PesoTotal);
        Assert.Equal('M', p.Sexo);
        Assert.Equal(EstadoPesaje.Vigente, p.Estado);
        Assert.Equal(1, p.Secuencia);
    }

    [Fact]
    public void Preparar_Pesajes_BoletasExistentesYRepetidas()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "500", "01/09/2026", "AB", "10", "100", "100" }), // Existente origen Balanza -> Error
            new(3, new[] { "600", "01/09/2026", "AB", "10", "100", "100" }), // Existente origen Importado -> Omitida
            new(4, new[] { "701", "01/09/2026", "AB", "10", "100", "100" }), // Primera 701 -> Ok
            new(5, new[] { "701", "01/09/2026", "AB", "10", "100", "100" })  // Repetida 701 -> Error
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Single(plan.Pesajes);
        Assert.Equal(701L, plan.Pesajes[0].NumeroBoleta);
        Assert.Equal(2, plan.FilasConError);   // Filas 2 y 5
        Assert.Equal(1, plan.FilasOmitidas); // Fila 3
    }

    [Fact]
    public void Preparar_Pesajes_ValidacionFechasYRangos()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "1", "31/02/2026", "AB", "10", "100", "100" }), // Fecha inválida -> Error
            new(3, new[] { "2", "01/01/1985", "AB", "10", "100", "100" }), // < 1990 -> Error
            new(4, new[] { "3", "03/10/2026", "AB", "10", "100", "100" }), // Pasado mañana -> Error
            new(5, new[] { "4", "02/10/2026", "AB", "10", "100", "100" })  // Mañana -> Válida
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Single(plan.Pesajes);
        Assert.Equal(4L, plan.Pesajes[0].NumeroBoleta);
        Assert.Equal(3, plan.FilasConError);
    }

    [Fact]
    public void Preparar_Pesajes_ValidacionPesosYDiscrepancias()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der", "Tot", "Desp" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "1", "01/09/2026", "AB", "10", "-1", "100", "", "" }),           // Negativo -> Error
            new(3, new[] { "2", "01/09/2026", "AB", "10", "", "", "", "" }),                // Ambos vacíos -> Error
            new(4, new[] { "3", "01/09/2026", "AB", "10", "1300", "100", "", "" }),         // > 1200 -> Error
            new(5, new[] { "4", "01/09/2026", "AB", "10", "109.3", "106.7", "300", "" }),   // Discrepancia total -> Error
            new(6, new[] { "5", "01/09/2026", "AB", "10", "100", "100", "200", "MEDIA" }),  // Media res con dos lados -> Advertencia
            new(7, new[] { "6", "01/09/2026", "AB", "10", "5.0", "0", "5.0", "MEDIA" })     // Lado 5.0 kg (<10) -> Advertencia
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5,
            [CamposImportacion.PesoTotal] = 6,
            [CamposImportacion.TipoDesposte] = 7
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Equal(2, plan.Pesajes.Count); // Filas 6 y 7
        Assert.Equal(4, plan.FilasConError);  // Filas 2, 3, 4, 5
        Assert.Equal(2, plan.FilasConAdvertencia);
    }

    [Fact]
    public void Preparar_Pesajes_DesposteYSexoCasosEspeciales()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der", "Desp", "Sex" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "1", "01/09/2026", "AB", "10", "100", "100", "X", "M" }),  // Desposte X -> Error
            new(3, new[] { "2", "01/09/2026", "AB", "10", "100", "100", "RC", "Z" }), // Sexo Z -> Error
            new(4, new[] { "3", "01/09/2026", "AB", "10", "100", "100", "RC", "" })   // Sexo mapeado y vacío -> Advertencia (asume M)
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5,
            [CamposImportacion.TipoDesposte] = 6,
            [CamposImportacion.Sexo] = 7
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Single(plan.Pesajes);
        Assert.Equal('M', plan.Pesajes[0].Sexo);
        Assert.Equal(2, plan.FilasConError);
        Assert.Equal(1, plan.FilasConAdvertencia);
    }

    [Fact]
    public void Preparar_Pesajes_EstadoAnuladoYMotivo()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der", "ANULADA", "ESTADO", "Motivo" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "1", "01/09/2026", "AB", "10", "100", "100", "S", "", "" }),                // Columna ANULADA "S" -> Anulado con motivo por defecto
            new(3, new[] { "2", "01/09/2026", "AB", "10", "100", "100", "", "2", "ERROR DE PESO" }),   // Columna ESTADO "2" -> Anulado con motivo explícito
            new(4, new[] { "3", "01/09/2026", "AB", "10", "100", "100", "", "X", "" })                 // Estado "X" -> Error
        };

        // Probamos primero con columna ANULADA mapeada
        var tabla1 = new TablaOrigen(cols, new[] { filas[0] }, "H", "UTF-8", Array.Empty<string>());
        var mapeo1 = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5,
            [CamposImportacion.Estado] = 6, // ANULADA
            [CamposImportacion.MotivoAnulacion] = 8
        };
        var plan1 = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla1, mapeo1, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);
        Assert.Equal(EstadoPesaje.Anulado, plan1.Pesajes[0].Estado);
        Assert.Equal(ReglasImportacion.MotivoAnulacionPorDefecto, plan1.Pesajes[0].MotivoAnulacion);

        // Probamos con columna ESTADO mapeada
        var tabla2 = new TablaOrigen(cols, new[] { filas[1], filas[2] }, "H", "UTF-8", Array.Empty<string>());
        var mapeo2 = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5,
            [CamposImportacion.Estado] = 7, // ESTADO
            [CamposImportacion.MotivoAnulacion] = 8
        };
        var plan2 = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla2, mapeo2, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);
        Assert.Single(plan2.Pesajes);
        Assert.Equal(EstadoPesaje.Anulado, plan2.Pesajes[0].Estado);
        Assert.Equal("ERROR DE PESO", plan2.Pesajes[0].MotivoAnulacion);
        Assert.Equal(1, plan2.FilasConError); // Fila con "X"
    }

    [Fact]
    public void Preparar_Pesajes_CalculoSecuenciasMismoDiaYDiaSiguiente()
    {
        var cols = new[] { "Num", "Fecha", "Hora", "Marca", "Cli", "Izq", "Der", "Sec" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "1", "01/09/2026", "10:00", "AB", "10", "100", "100", "" }),  // -> Sec 1
            new(3, new[] { "2", "01/09/2026", "11:00", "AB", "10", "100", "100", "" }),  // -> Sec 2
            new(4, new[] { "3", "01/09/2026", "12:00", "AB", "10", "100", "100", "5" }), // Explícita 5
            new(5, new[] { "4", "01/09/2026", "13:00", "AB", "10", "100", "100", "" }),  // -> Sec 6
            new(6, new[] { "5", "02/09/2026", "08:00", "AB", "10", "100", "100", "" }),  // Día siguiente -> Sec 1
            new(7, new[] { "6", "02/09/2026", "09:00", "AB", "10", "100", "100", "0" })  // Secuencia 0 -> Error
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.Hora] = 2,
            [CamposImportacion.MarcaGanadero] = 3,
            [CamposImportacion.CodigoCliente] = 4,
            [CamposImportacion.PesoIzquierdo] = 5,
            [CamposImportacion.PesoDerecho] = 6,
            [CamposImportacion.Secuencia] = 7
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Equal(5, plan.Pesajes.Count);
        Assert.Equal(1, plan.Pesajes[0].Secuencia);
        Assert.Equal(2, plan.Pesajes[1].Secuencia);
        Assert.Equal(5, plan.Pesajes[2].Secuencia);
        Assert.Equal(6, plan.Pesajes[3].Secuencia);
        Assert.Equal(1, plan.Pesajes[4].Secuencia);
        Assert.Equal(1, plan.FilasConError);
    }

    [Fact]
    public void Preparar_Pesajes_GanaderoOClienteInexistentes_GeneranError()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der" };
        var filas = new List<FilaOrigen>
        {
            new(2, new[] { "1", "01/09/2026", "ZZ", "10", "100", "100" }), // Ganadero ZZ inexistente
            new(3, new[] { "2", "01/09/2026", "AB", "99", "100", "100" })  // Cliente 99 inexistente
        };

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5
        };

        var plan = PreparadorImportacion.Preparar(TipoImportacion.Pesajes, tabla, mapeo, ClavesPrueba, AhoraUtc, ZonaUtcMenos4);

        Assert.Empty(plan.Pesajes);
        Assert.Equal(2, plan.FilasConError);
        Assert.Contains(plan.Incidencias, i => i.Mensaje.Contains("Importe primero los ganaderos"));
        Assert.Contains(plan.Incidencias, i => i.Mensaje.Contains("Importe primero los clientes"));
    }

    [Fact]
    public void Preparar_MaximoIncidenciasTruncadas_LimitaColeccionPeroConservaConteos()
    {
        var cols = new[] { "Num", "Fecha", "Marca", "Cli", "Izq", "Der" };
        var filas = new List<FilaOrigen>();
        for (int i = 1; i <= 5; i++)
        {
            filas.Add(new FilaOrigen(i + 1, new[] { "invalido", "01/09/2026", "AB", "10", "100", "100" }));
        }

        var tabla = new TablaOrigen(cols, filas, "H", "UTF-8", Array.Empty<string>());
        var mapeo = new Dictionary<string, int?>
        {
            [CamposImportacion.NumeroBoleta] = 0,
            [CamposImportacion.Fecha] = 1,
            [CamposImportacion.MarcaGanadero] = 2,
            [CamposImportacion.CodigoCliente] = 3,
            [CamposImportacion.PesoIzquierdo] = 4,
            [CamposImportacion.PesoDerecho] = 5
        };

        var plan = PreparadorImportacion.Preparar(
            TipoImportacion.Pesajes,
            tabla,
            mapeo,
            ClavesPrueba,
            AhoraUtc,
            ZonaUtcMenos4,
            maximoIncidencias: 3);

        Assert.Equal(3, plan.Incidencias.Count);
        Assert.True(plan.IncidenciasTruncadas);
        Assert.Equal(5, plan.FilasConError);
    }
}

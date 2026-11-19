using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Boletas.Diseno;

public static class DisenoBoleta
{
    public const float AnchoMediaCartaMm = 216f;
    public const float AltoMediaCartaMm = 140f;
    public const float AnchoTicketMm = 80f;

    /// <summary>Perfil fijo del cliente: dibuja el logo de la empresa como marca de agua en la media carta.</summary>
    public const bool LogoComoFondo = true;

    /// <summary>Opacidad del logo de fondo (0 a 1); baja para no estorbar la lectura ni la tinta.</summary>
    public const float OpacidadFondoLogo = 0.13f;

    public static PaginaBoleta Crear(DatosBoleta datos, FormatoBoleta formato, int numeroCopia, int totalCopias)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (totalCopias < ReglasImpresion.CopiasMinimas || totalCopias > ReglasImpresion.CopiasMaximas)
        {
            throw new ArgumentException("Total de copias fuera de rango permitido.", nameof(totalCopias));
        }

        if (numeroCopia < 1 || numeroCopia > totalCopias)
        {
            throw new ArgumentException("Número de copia inválido.", nameof(numeroCopia));
        }

        return formato switch
        {
            FormatoBoleta.MediaCarta => CrearMediaCarta(datos, numeroCopia, totalCopias),
            FormatoBoleta.Ticket80mm => CrearTicket80mm(datos, numeroCopia, totalCopias),
            _ => throw new ArgumentException("Formato de boleta no definido.", nameof(formato))
        };
    }

    private static PaginaBoleta CrearMediaCarta(DatosBoleta datos, int numeroCopia, int totalCopias)
    {
        var elementos = new List<ElementoBoleta>();

        // 0. Logo de la empresa como fondo atenuado (solo media carta; perfil fijo de este cliente). Va primero
        //    para quedar detrás de todo; la marca de agua de texto (ANULADA, etc.) se dibuja encima.
        if (LogoComoFondo && datos.Empresa.RutaLogo is not null)
        {
            elementos.Add(new FondoLogoBoleta(48f, 37f, 120f, 92f, OpacidadFondoLogo));
        }

        // 1. Marca de agua (si aplica)
        var marca = TextosBoleta.MarcaDeAgua(datos);
        if (marca is not null)
        {
            elementos.Add(new TextoBoleta(
                X: 8f,
                Y: 70f,
                Ancho: 200f,
                Alto: 26f,
                Texto: marca,
                TamanoPt: 44f,
                Negrita: true,
                Alineacion: AlineacionTexto.Centro,
                Tono: TonoTinta.Gris));
        }

        // 2. Logo
        if (datos.Empresa.RutaLogo is not null)
        {
            elementos.Add(new LogoBoleta(8f, 8f, 30f, 20f));
        }

        // 3. Nombre de empresa
        if (!string.IsNullOrWhiteSpace(datos.Empresa.Nombre))
        {
            elementos.Add(new TextoBoleta(
                X: 40f,
                Y: 8f,
                Ancho: 108f,
                Alto: 7f,
                Texto: datos.Empresa.Nombre.Trim(),
                TamanoPt: 13f,
                Negrita: true,
                Alineacion: AlineacionTexto.Centro));
        }

        // 4. Dirección - ciudad
        var dirCiudad = TextosBoleta.UnirNoVacios(" — ", datos.Empresa.Direccion, datos.Empresa.Ciudad);
        if (!string.IsNullOrWhiteSpace(dirCiudad))
        {
            elementos.Add(new TextoBoleta(
                X: 40f,
                Y: 15f,
                Ancho: 108f,
                Alto: 5f,
                Texto: dirCiudad,
                TamanoPt: 8f,
                Alineacion: AlineacionTexto.Centro));
        }

        // 5. Tel. / Reg. Sanitario
        var tel = string.IsNullOrWhiteSpace(datos.Empresa.Telefono) ? null : "Tel.: " + datos.Empresa.Telefono.Trim();
        var reg = string.IsNullOrWhiteSpace(datos.Empresa.RegistroSanitario) ? null : "Reg. Sanitario: " + datos.Empresa.RegistroSanitario.Trim();
        var telReg = TextosBoleta.UnirNoVacios("   ", tel, reg);
        if (!string.IsNullOrWhiteSpace(telReg))
        {
            elementos.Add(new TextoBoleta(
                X: 40f,
                Y: 20f,
                Ancho: 108f,
                Alto: 5f,
                Texto: telReg,
                TamanoPt: 8f,
                Alineacion: AlineacionTexto.Centro));
        }

        // 6. Recuadro del número
        elementos.Add(new RectanguloBoleta(152f, 8f, 56f, 21f));

        // 7. "BOLETA DE PESAJE"
        elementos.Add(new TextoBoleta(
            X: 152f,
            Y: 9f,
            Ancho: 56f,
            Alto: 6f,
            Texto: "BOLETA DE PESAJE",
            TamanoPt: 10f,
            Negrita: true,
            Alineacion: AlineacionTexto.Centro));

        // 8. Número de boleta
        elementos.Add(new TextoBoleta(
            X: 152f,
            Y: 16f,
            Ancho: 56f,
            Alto: 11f,
            Texto: TextosBoleta.Numero(datos),
            TamanoPt: 18f,
            Negrita: true,
            Alineacion: AlineacionTexto.Centro));

        // 9. Leyendas unidas con "  ·  "
        var leyendas = TextosBoleta.Leyendas(datos, numeroCopia, totalCopias);
        if (leyendas.Count > 0)
        {
            var textoLeyendas = string.Join("  ·  ", leyendas);
            var tamanoLeyendas = textoLeyendas.Length <= 85 ? 9f : textoLeyendas.Length <= 110 ? 7f : 6f;
            elementos.Add(new TextoBoleta(
                X: 8f,
                Y: 31f,
                Ancho: 200f,
                Alto: 5f,
                Texto: textoLeyendas,
                TamanoPt: tamanoLeyendas,
                Negrita: true,
                Alineacion: AlineacionTexto.Centro));
        }

        // 10. Línea divisoria
        elementos.Add(new LineaBoleta(8f, 37f, 208f, 37f));

        // 11. Filas de datos
        // Fila 0: Y = 39
        elementos.Add(new TextoBoleta(8f, 39f, 26f, 5.5f, "Fecha:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 39f, 88f, 5.5f, TextosBoleta.Fecha(datos.FechaHoraLocal) + "  " + TextosBoleta.Hora(datos.FechaHoraLocal), 10f));
        elementos.Add(new TextoBoleta(126f, 39f, 24f, 5.5f, "Color/Raza:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(150f, 39f, 58f, 5.5f, datos.ColorRaza ?? string.Empty, 10f));

        // Fila 1: Y = 45.5
        elementos.Add(new TextoBoleta(8f, 45.5f, 26f, 5.5f, "Ganadero:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 45.5f, 88f, 5.5f, TextosBoleta.Ganadero(datos), 10f));
        elementos.Add(new TextoBoleta(126f, 45.5f, 24f, 5.5f, "Sexo:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(150f, 45.5f, 58f, 5.5f, TextosBoleta.Sexo(datos.Sexo), 10f));

        // Fila 2: Y = 52
        elementos.Add(new TextoBoleta(8f, 52f, 26f, 5.5f, "Secuencia:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 52f, 88f, 5.5f, datos.Secuencia.ToString(TextosBoleta.Cultura), 10f));
        // F11 (D6): sin desposte (la empresa pesa un solo tipo) queda el hueco; no se mueven las demás filas para no
        // alterar la calibración del papel.
        if (datos.MostrarDesposte)
        {
            elementos.Add(new TextoBoleta(126f, 52f, 24f, 5.5f, "Desposte:", 9f, Negrita: true));
            elementos.Add(new TextoBoleta(150f, 52f, 58f, 5.5f, TextosBoleta.Desposte(datos.TipoDesposte), 10f));
        }

        // Fila 3: Y = 58.5
        elementos.Add(new TextoBoleta(8f, 58.5f, 26f, 5.5f, "Cliente:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 58.5f, 88f, 5.5f, TextosBoleta.Cliente(datos), 10f));
        elementos.Add(new TextoBoleta(126f, 58.5f, 24f, 5.5f, "Operador:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(150f, 58.5f, 58f, 5.5f, datos.NombreOperador ?? string.Empty, 10f));

        // Fila 4: Y = 65
        elementos.Add(new TextoBoleta(8f, 65f, 26f, 5.5f, "Dirección:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 65f, 88f, 5.5f, datos.Direccion ?? string.Empty, 10f));

        // Fletero (F10, O9): en la zona de datos, junto a la dirección. Si el pesaje no tiene fletero no se imprime la fila.
        if (datos.Fletero is not null)
        {
            elementos.Add(new TextoBoleta(126f, 65f, 24f, 5.5f, "Fletero:", 9f, Negrita: true));
            elementos.Add(new TextoBoleta(150f, 65f, 58f, 7.5f, TextosBoleta.Fletero(datos.Fletero), 8f, VariasLineas: true));
        }

        // 12. Recuadros y títulos de pesos
        elementos.Add(new RectanguloBoleta(8f, 73f, 64f, 20f));
        elementos.Add(new RectanguloBoleta(76f, 73f, 64f, 20f));
        elementos.Add(new RectanguloBoleta(144f, 73f, 64f, 20f));

        elementos.Add(new TextoBoleta(8f, 74f, 64f, 5f, "IZQUIERDO", 9f, Negrita: true, Alineacion: AlineacionTexto.Centro));
        elementos.Add(new TextoBoleta(76f, 74f, 64f, 5f, "DERECHO", 9f, Negrita: true, Alineacion: AlineacionTexto.Centro));
        elementos.Add(new TextoBoleta(144f, 74f, 64f, 5f, "TOTAL", 9f, Negrita: true, Alineacion: AlineacionTexto.Centro));

        elementos.Add(new TextoBoleta(8f, 80f, 64f, 11f, TextosBoleta.Peso(datos.PesoIzquierdo), 16f, Negrita: true, Alineacion: AlineacionTexto.Centro));
        elementos.Add(new TextoBoleta(76f, 80f, 64f, 11f, TextosBoleta.Peso(datos.PesoDerecho), 16f, Negrita: true, Alineacion: AlineacionTexto.Centro));
        elementos.Add(new TextoBoleta(144f, 80f, 64f, 11f, TextosBoleta.Peso(datos.PesoTotal), 18f, Negrita: true, Alineacion: AlineacionTexto.Centro));

        // 13. Observación
        elementos.Add(new TextoBoleta(8f, 96f, 26f, 5f, "Observación:", 9f, Negrita: true));
        if (!string.IsNullOrWhiteSpace(datos.Observacion))
        {
            elementos.Add(new TextoBoleta(34f, 96f, 174f, 10f, datos.Observacion.Trim(), 9f, VariasLineas: true));
        }

        // 14. Bloque inferior del formato legado: firma del Jefe de Despacho | firma del Fletero (con su nombre sobre la
        //     línea si el pesaje lo tiene) | firma del Médico Veterinario. El fletero también figura en la zona de datos.
        const float yBloque = 107f;
        const float altoBloque = 23f;
        elementos.Add(new RectanguloBoleta(8f, yBloque, 60f, altoBloque));
        elementos.Add(new RectanguloBoleta(68f, yBloque, 80f, altoBloque));
        elementos.Add(new RectanguloBoleta(148f, yBloque, 60f, altoBloque));

        AgregarFirma(elementos, 8f, yBloque, 60f, altoBloque, TextosBoleta.FirmaJefeDespacho);
        AgregarFirma(elementos, 68f, yBloque, 80f, altoBloque, TextosBoleta.FirmaFletero, datos.Fletero?.Nombre);
        AgregarFirma(elementos, 148f, yBloque, 60f, altoBloque, TextosBoleta.FirmaMedicoVeterinario);

        // 15. Reclamos
        if (!string.IsNullOrWhiteSpace(datos.Empresa.TelefonosReclamos))
        {
            elementos.Add(new TextoBoleta(8f, 131f, 200f, 4f, "Reclamos: " + datos.Empresa.TelefonosReclamos.Trim(), 8f, Alineacion: AlineacionTexto.Centro));
        }

        // 16. Pie
        elementos.Add(new TextoBoleta(8f, 135.5f, 100f, 4f, TextosBoleta.PieImpreso(datos), 7f, Alineacion: AlineacionTexto.Izquierda));
        elementos.Add(new TextoBoleta(108f, 135.5f, 100f, 4f, "Capataz", 7f, Alineacion: AlineacionTexto.Derecha));

        return new PaginaBoleta(AnchoMediaCartaMm, AltoMediaCartaMm, elementos);
    }

    // Línea de firma y cargo al pie de un recuadro del bloque inferior; el nombre (si hay) va sobre la línea.
    private static void AgregarFirma(List<ElementoBoleta> elementos, float x, float y, float ancho, float alto, string cargo, string? nombre = null)
    {
        var yLinea = y + alto - 6f;
        elementos.Add(new LineaBoleta(x + 6f, yLinea, x + ancho - 6f, yLinea));
        elementos.Add(new TextoBoleta(x, yLinea + 1f, ancho, 4.5f, cargo, 9f, Negrita: true, Alineacion: AlineacionTexto.Centro));

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            elementos.Add(new TextoBoleta(x + 2f, yLinea - 5f, ancho - 4f, 4.5f, nombre.Trim(), 9f, Alineacion: AlineacionTexto.Centro));
        }
    }

    // El diseño no usa System.Drawing (no puede medir texto): el alto de una caja de varias líneas se estima por
    // caracteres, con un ancho medio de 0,65 em (Arial en mayúsculas, más ancho que el promedio) para sobrar alto
    // antes que recortar una línea. Una sola línea cuesta "alto de línea" y cada línea extra lo suma.
    private static (float Alto, bool VariasLineas) CajaTexto(string texto, float tamanoPt, float anchoMm, bool negrita = false, int maximoLineas = 3)
    {
        var emMm = tamanoPt * 0.3528f;
        var caracteresPorLinea = Math.Max(1, (int)Math.Floor(anchoMm / (emMm * (negrita ? 0.7f : 0.65f))));
        var lineas = Math.Clamp((int)Math.Ceiling((texto?.Length ?? 0) / (double)caracteresPorLinea), 1, maximoLineas);
        return (lineas * emMm * 1.25f + 0.5f, lineas > 1);
    }

    // Agrega un texto del ticket con el alto justo y devuelve la posición vertical siguiente.
    private static float AgregarTextoTicket(List<ElementoBoleta> elementos, float y, string texto, float tamanoPt, bool negrita = false, AlineacionTexto alineacion = AlineacionTexto.Izquierda, int maximoLineas = 3)
    {
        var (alto, varias) = CajaTexto(texto, tamanoPt, 72f, negrita, maximoLineas);
        elementos.Add(new TextoBoleta(4f, y, 72f, alto, texto, tamanoPt, negrita, alineacion, varias));
        return y + alto + 0.8f;
    }

    private static float AgregarSeparadorTicket(List<ElementoBoleta> elementos, float y)
    {
        y += 0.7f;
        elementos.Add(new LineaBoleta(4f, y, 76f, y));
        return y + 2f;
    }

    private static PaginaBoleta CrearTicket80mm(DatosBoleta datos, int numeroCopia, int totalCopias)
    {
        var elementos = new List<ElementoBoleta>();
        float y = 3f;

        // 1. Logo
        if (datos.Empresa.RutaLogo is not null)
        {
            elementos.Add(new LogoBoleta(20f, y, 40f, 18f));
            y += 20f;
        }

        // 2. Empresa
        if (!string.IsNullOrWhiteSpace(datos.Empresa.Nombre))
        {
            y = AgregarTextoTicket(elementos, y, datos.Empresa.Nombre.Trim(), 12f, negrita: true, alineacion: AlineacionTexto.Centro, maximoLineas: 2);
        }

        var dirCiudad = TextosBoleta.UnirNoVacios(" — ", datos.Empresa.Direccion, datos.Empresa.Ciudad);
        if (!string.IsNullOrWhiteSpace(dirCiudad))
        {
            y = AgregarTextoTicket(elementos, y, dirCiudad, 8f, alineacion: AlineacionTexto.Centro, maximoLineas: 2);
        }

        var tel = string.IsNullOrWhiteSpace(datos.Empresa.Telefono) ? null : "Tel.: " + datos.Empresa.Telefono.Trim();
        var reg = string.IsNullOrWhiteSpace(datos.Empresa.RegistroSanitario) ? null : "Reg. Sanitario: " + datos.Empresa.RegistroSanitario.Trim();
        var telReg = TextosBoleta.UnirNoVacios("   ", tel, reg);
        if (!string.IsNullOrWhiteSpace(telReg))
        {
            y = AgregarTextoTicket(elementos, y, telReg, 8f, alineacion: AlineacionTexto.Centro, maximoLineas: 2);
        }

        // 3. Línea separadora
        y = AgregarSeparadorTicket(elementos, y);

        // 4. Título y número
        y = AgregarTextoTicket(elementos, y, "BOLETA DE PESAJE", 10f, negrita: true, alineacion: AlineacionTexto.Centro, maximoLineas: 1);
        y = AgregarTextoTicket(elementos, y, TextosBoleta.Numero(datos), 16f, negrita: true, alineacion: AlineacionTexto.Centro, maximoLineas: 1);

        // 5. Leyendas
        var leyendas = TextosBoleta.Leyendas(datos, numeroCopia, totalCopias);
        foreach (var ley in leyendas)
        {
            y = AgregarTextoTicket(elementos, y, ley, 9f, negrita: true, alineacion: AlineacionTexto.Centro, maximoLineas: 3);
        }

        // 6. Línea separadora
        y = AgregarSeparadorTicket(elementos, y);

        // 7. Datos
        y = AgregarTextoTicket(elementos, y, "Fecha y hora: " + TextosBoleta.FechaHora(datos.FechaHoraLocal), 9f);
        y = AgregarTextoTicket(elementos, y, "Ganadero: " + TextosBoleta.Ganadero(datos), 9f);
        y = AgregarTextoTicket(elementos, y, "Secuencia: " + datos.Secuencia.ToString(TextosBoleta.Cultura), 9f);
        y = AgregarTextoTicket(elementos, y, "Cliente: " + TextosBoleta.Cliente(datos), 9f);
        y = AgregarTextoTicket(elementos, y, "Dirección: " + (datos.Direccion ?? string.Empty), 9f);

        // El fletero va con los datos del pesaje (F10, O9); sin fletero no se imprime la línea.
        if (datos.Fletero is not null)
        {
            y = AgregarTextoTicket(elementos, y, "Fletero: " + TextosBoleta.Fletero(datos.Fletero), 9f);
        }

        y = AgregarTextoTicket(elementos, y, "Color/Raza: " + (datos.ColorRaza ?? string.Empty), 9f);
        y = AgregarTextoTicket(elementos, y, "Sexo: " + TextosBoleta.Sexo(datos.Sexo), 9f);
        if (datos.MostrarDesposte)
        {
            y = AgregarTextoTicket(elementos, y, "Desposte: " + TextosBoleta.Desposte(datos.TipoDesposte), 9f);
        }

        // 8. Línea separadora
        y = AgregarSeparadorTicket(elementos, y);

        // 9. Pesos
        y = AgregarTextoTicket(elementos, y, "Izquierdo: " + TextosBoleta.Peso(datos.PesoIzquierdo), 10f, maximoLineas: 1);
        y = AgregarTextoTicket(elementos, y, "Derecho: " + TextosBoleta.Peso(datos.PesoDerecho), 10f, maximoLineas: 1);
        y = AgregarTextoTicket(elementos, y, "TOTAL: " + TextosBoleta.Peso(datos.PesoTotal), 14f, negrita: true, maximoLineas: 1);

        // 10. Marca como texto destacado
        var marca = TextosBoleta.MarcaDeAgua(datos);
        if (marca is not null)
        {
            y = AgregarTextoTicket(elementos, y, "*** " + marca + " ***", 12f, negrita: true, alineacion: AlineacionTexto.Centro, maximoLineas: 1);
        }

        // 11. Línea separadora
        y = AgregarSeparadorTicket(elementos, y);

        // 12. Observación
        y = AgregarTextoTicket(elementos, y, "Observación: " + (datos.Observacion ?? string.Empty), 9f);

        // 13. Reclamos y operador
        if (!string.IsNullOrWhiteSpace(datos.Empresa.TelefonosReclamos))
        {
            y = AgregarTextoTicket(elementos, y, "Reclamos: " + datos.Empresa.TelefonosReclamos.Trim(), 8f, maximoLineas: 2);
        }

        y = AgregarTextoTicket(elementos, y, "Operador: " + (datos.NombreOperador ?? string.Empty), 8f, maximoLineas: 2);

        // 14. Firmas (espacio para firmar sobre cada línea): Jefe de Despacho, Fletero y Médico Veterinario.
        foreach (var cargo in new[] { TextosBoleta.FirmaJefeDespacho, TextosBoleta.FirmaFletero, TextosBoleta.FirmaMedicoVeterinario })
        {
            y += 11f;
            elementos.Add(new LineaBoleta(12f, y, 68f, y));
            y += 0.6f;
            elementos.Add(new TextoBoleta(4f, y, 72f, 4.2f, cargo, 8f, Negrita: true, Alineacion: AlineacionTexto.Centro));
            y += 4.2f;

            // El nombre del fletero va bajo el cargo cuando el pesaje lo tiene.
            if (cargo == TextosBoleta.FirmaFletero && !string.IsNullOrWhiteSpace(datos.Fletero?.Nombre))
            {
                y = AgregarTextoTicket(elementos, y, datos.Fletero!.Nombre.Trim(), 8f, alineacion: AlineacionTexto.Centro, maximoLineas: 2);
            }
        }

        y += 1.5f;
        elementos.Add(new TextoBoleta(4f, y, 72f, 3.5f, TextosBoleta.PieImpreso(datos), 7f));
        y += 4.0f;

        // 15. Margen de corte
        float altoMm = y + 10f;

        return new PaginaBoleta(AnchoTicketMm, altoMm, elementos);
    }
}

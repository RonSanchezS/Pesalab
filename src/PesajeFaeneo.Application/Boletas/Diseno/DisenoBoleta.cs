using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Boletas.Diseno;

public static class DisenoBoleta
{
    public const float AnchoMediaCartaMm = 216f;
    public const float AltoMediaCartaMm = 140f;
    public const float AnchoTicketMm = 80f;

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

        // 1. Marca de agua (si aplica) - primer elemento en el orden de dibujo
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
        elementos.Add(new TextoBoleta(126f, 52f, 24f, 5.5f, "Desposte:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(150f, 52f, 58f, 5.5f, TextosBoleta.Desposte(datos.TipoDesposte), 10f));

        // Fila 3: Y = 58.5
        elementos.Add(new TextoBoleta(8f, 58.5f, 26f, 5.5f, "Cliente:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 58.5f, 88f, 5.5f, TextosBoleta.Cliente(datos), 10f));
        elementos.Add(new TextoBoleta(126f, 58.5f, 24f, 5.5f, "Operador:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(150f, 58.5f, 58f, 5.5f, datos.NombreOperador ?? string.Empty, 10f));

        // Fila 4: Y = 65
        elementos.Add(new TextoBoleta(8f, 65f, 26f, 5.5f, "Dirección:", 9f, Negrita: true));
        elementos.Add(new TextoBoleta(34f, 65f, 88f, 5.5f, datos.Direccion ?? string.Empty, 10f));

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

        // 14. Bloque inferior del formato legado: firma del Jefe de Despacho | fleteros con casilla para
        //     marcar quién lleva la carne | firma del Médico Veterinario.
        const float yBloque = 107f;
        const float altoBloque = 23f;
        elementos.Add(new RectanguloBoleta(8f, yBloque, 60f, altoBloque));
        elementos.Add(new RectanguloBoleta(68f, yBloque, 80f, altoBloque));
        elementos.Add(new RectanguloBoleta(148f, yBloque, 60f, altoBloque));

        AgregarFirma(elementos, 8f, yBloque, altoBloque, TextosBoleta.FirmaJefeDespacho);
        AgregarFirma(elementos, 148f, yBloque, altoBloque, TextosBoleta.FirmaMedicoVeterinario);

        // 15. Fleteros (F1..F4) y "Otros"
        if (datos.Fleteros.Count > 0)
        {
            elementos.Add(new TextoBoleta(70f, 107.5f, 76f, 3.5f, "Fleteros:", 7.5f, Negrita: true));

            var fleteros = datos.Fleteros.Take(4).ToList();
            for (int i = 0; i < fleteros.Count; i++)
            {
                var y = 111f + i * 3.5f;
                elementos.Add(new TextoBoleta(70f, y, 68f, 3.5f, TextosBoleta.Fletero(fleteros[i]), 8f));
                elementos.Add(new RectanguloBoleta(141f, y + 0.25f, 3f, 3f));
            }

            elementos.Add(new TextoBoleta(70f, 125.5f, 76f, 3.5f, TextosBoleta.OtrosFleteros, 8f));
        }
        else
        {
            elementos.Add(new TextoBoleta(70f, 111f, 76f, 3.5f, TextosBoleta.SinFletero, 8f));
        }

        // 16. Reclamos
        if (!string.IsNullOrWhiteSpace(datos.Empresa.TelefonosReclamos))
        {
            elementos.Add(new TextoBoleta(8f, 131f, 200f, 4f, "Reclamos: " + datos.Empresa.TelefonosReclamos.Trim(), 8f, Alineacion: AlineacionTexto.Centro));
        }

        // 17. Pie
        elementos.Add(new TextoBoleta(8f, 135.5f, 100f, 4f, TextosBoleta.PieImpreso(datos), 7f, Alineacion: AlineacionTexto.Izquierda));
        elementos.Add(new TextoBoleta(108f, 135.5f, 100f, 4f, "Sistema de Pesaje para Faeneo", 7f, Alineacion: AlineacionTexto.Derecha));

        return new PaginaBoleta(AnchoMediaCartaMm, AltoMediaCartaMm, elementos);
    }

    // Línea de firma y nombre del cargo al pie de un recuadro del bloque inferior.
    private static void AgregarFirma(List<ElementoBoleta> elementos, float x, float y, float alto, string cargo)
    {
        var yLinea = y + alto - 6f;
        elementos.Add(new LineaBoleta(x + 6f, yLinea, x + 54f, yLinea));
        elementos.Add(new TextoBoleta(x, yLinea + 1f, 60f, 4.5f, cargo, 9f, Negrita: true, Alineacion: AlineacionTexto.Centro));
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
            elementos.Add(new TextoBoleta(4f, y, 72f, 12f, datos.Empresa.Nombre.Trim(), 12f, Negrita: true, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            y += 12.5f;
        }

        var dirCiudad = TextosBoleta.UnirNoVacios(" — ", datos.Empresa.Direccion, datos.Empresa.Ciudad);
        if (!string.IsNullOrWhiteSpace(dirCiudad))
        {
            elementos.Add(new TextoBoleta(4f, y, 72f, 8f, dirCiudad, 8f, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            y += 8.5f;
        }

        var tel = string.IsNullOrWhiteSpace(datos.Empresa.Telefono) ? null : "Tel.: " + datos.Empresa.Telefono.Trim();
        var reg = string.IsNullOrWhiteSpace(datos.Empresa.RegistroSanitario) ? null : "Reg. Sanitario: " + datos.Empresa.RegistroSanitario.Trim();
        var telReg = TextosBoleta.UnirNoVacios("   ", tel, reg);
        if (!string.IsNullOrWhiteSpace(telReg))
        {
            elementos.Add(new TextoBoleta(4f, y, 72f, 8f, telReg, 8f, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            y += 8.5f;
        }

        // 3. Línea separadora
        elementos.Add(new LineaBoleta(4f, y, 76f, y));
        y += 1.5f;

        // 4. Título y número
        elementos.Add(new TextoBoleta(4f, y, 72f, 5f, "BOLETA DE PESAJE", 10f, Negrita: true, Alineacion: AlineacionTexto.Centro));
        y += 5.5f;
        elementos.Add(new TextoBoleta(4f, y, 72f, 8f, TextosBoleta.Numero(datos), 16f, Negrita: true, Alineacion: AlineacionTexto.Centro));
        y += 8.5f;

        // 5. Leyendas
        var leyendas = TextosBoleta.Leyendas(datos, numeroCopia, totalCopias);
        foreach (var ley in leyendas)
        {
            elementos.Add(new TextoBoleta(4f, y, 72f, 9f, ley, 9f, Negrita: true, Alineacion: AlineacionTexto.Centro, VariasLineas: true));
            y += 9.5f;
        }

        // 6. Línea separadora
        elementos.Add(new LineaBoleta(4f, y, 76f, y));
        y += 1.5f;

        // 7. Datos
        elementos.Add(new TextoBoleta(4f, y, 72f, 4.5f, "Fecha y hora: " + TextosBoleta.FechaHora(datos.FechaHoraLocal), 9f));
        y += 5.0f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 9f, "Ganadero: " + TextosBoleta.Ganadero(datos), 9f, VariasLineas: true));
        y += 9.5f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 4.5f, "Secuencia: " + datos.Secuencia.ToString(TextosBoleta.Cultura), 9f));
        y += 5.0f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 9f, "Cliente: " + TextosBoleta.Cliente(datos), 9f, VariasLineas: true));
        y += 9.5f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 9f, "Dirección: " + (datos.Direccion ?? string.Empty), 9f, VariasLineas: true));
        y += 9.5f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 4.5f, "Color/Raza: " + (datos.ColorRaza ?? string.Empty), 9f));
        y += 5.0f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 4.5f, "Sexo: " + TextosBoleta.Sexo(datos.Sexo), 9f));
        y += 5.0f;

        elementos.Add(new TextoBoleta(4f, y, 72f, 4.5f, "Desposte: " + TextosBoleta.Desposte(datos.TipoDesposte), 9f));
        y += 5.0f;

        // 8. Línea separadora
        elementos.Add(new LineaBoleta(4f, y, 76f, y));
        y += 1.5f;

        // 9. Pesos
        elementos.Add(new TextoBoleta(4f, y, 72f, 5f, "Izquierdo: " + TextosBoleta.Peso(datos.PesoIzquierdo), 10f));
        y += 5.5f;
        elementos.Add(new TextoBoleta(4f, y, 72f, 5f, "Derecho: " + TextosBoleta.Peso(datos.PesoDerecho), 10f));
        y += 5.5f;
        elementos.Add(new TextoBoleta(4f, y, 72f, 7f, "TOTAL: " + TextosBoleta.Peso(datos.PesoTotal), 14f, Negrita: true));
        y += 7.5f;

        // 10. Marca como texto destacado
        var marca = TextosBoleta.MarcaDeAgua(datos);
        if (marca is not null)
        {
            elementos.Add(new TextoBoleta(4f, y, 72f, 6f, "*** " + marca + " ***", 12f, Negrita: true, Alineacion: AlineacionTexto.Centro));
            y += 6.5f;
        }

        // 11. Línea separadora
        elementos.Add(new LineaBoleta(4f, y, 76f, y));
        y += 1.5f;

        // 12. Observación
        elementos.Add(new TextoBoleta(4f, y, 72f, 13.5f, "Observación: " + (datos.Observacion ?? string.Empty), 9f, VariasLineas: true));
        y += 14.0f;

        // 13. Fleteros
        if (datos.Fleteros.Count > 0)
        {
            elementos.Add(new TextoBoleta(4f, y, 72f, 4f, "Fleteros:", 8f, Negrita: true));
            y += 4.5f;
            foreach (var f in datos.Fleteros)
            {
                elementos.Add(new TextoBoleta(4f, y, 72f, 4f, TextosBoleta.FleteroConCasilla(f), 8f));
                y += 4.5f;
            }

            elementos.Add(new TextoBoleta(4f, y, 72f, 4f, TextosBoleta.OtrosFleteros, 8f));
            y += 4.5f;
        }

        // 14. Reclamos y pie
        if (!string.IsNullOrWhiteSpace(datos.Empresa.TelefonosReclamos))
        {
            elementos.Add(new TextoBoleta(4f, y, 72f, 8f, "Reclamos: " + datos.Empresa.TelefonosReclamos.Trim(), 8f, VariasLineas: true));
            y += 8.5f;
        }

        elementos.Add(new TextoBoleta(4f, y, 72f, 4f, "Operador: " + (datos.NombreOperador ?? string.Empty), 8f));
        y += 4.5f;

        // 15. Firmas (espacio para firmar sobre cada línea)
        foreach (var cargo in new[] { TextosBoleta.FirmaJefeDespacho, TextosBoleta.FirmaMedicoVeterinario })
        {
            y += 12f;
            elementos.Add(new LineaBoleta(12f, y, 68f, y));
            y += 0.5f;
            elementos.Add(new TextoBoleta(4f, y, 72f, 4f, cargo, 8f, Negrita: true, Alineacion: AlineacionTexto.Centro));
            y += 4.5f;
        }


        elementos.Add(new TextoBoleta(4f, y, 72f, 3.5f, TextosBoleta.PieImpreso(datos), 7f));
        y += 4.0f;

        // 16. Margen de corte
        float altoMm = y + 10f;

        return new PaginaBoleta(AnchoTicketMm, altoMm, elementos);
    }
}

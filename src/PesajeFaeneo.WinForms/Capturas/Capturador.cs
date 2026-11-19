#if CAPTURAS_MANUAL
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PesajeFaeneo.WinForms.Capturas;

internal sealed record ControlCapturado(string Nombre, string Tipo, string Texto, int X, int Y, int Ancho, int Alto);

internal sealed record EntradaCaptura(
    string Id,
    string Archivo,
    string Titulo,
    string Descripcion,
    int Ancho,
    int Alto,
    int EscalaDpi,
    List<ControlCapturado> Controles);

/// <summary>
/// Captura ventanas reales del proceso con PrintWindow (marco incluido) y escribe un PNG por paso más capturas.json
/// con los límites de los controles (relativos a la imagen) para los círculos numerados de los manuales.
/// </summary>
internal sealed class Capturador
{
    private readonly string _carpetaSalida;
    private readonly List<EntradaCaptura> _entradas = new();

    public Capturador(string carpetaSalida)
    {
        _carpetaSalida = carpetaSalida;
        Directory.CreateDirectory(carpetaSalida);

        // Con --solo se conservan las capturas de corridas anteriores.
        var indice = Path.Combine(carpetaSalida, "capturas.json");
        if (File.Exists(indice))
        {
            try
            {
                var previas = JsonSerializer.Deserialize<List<EntradaCaptura>>(File.ReadAllText(indice), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                if (previas is not null)
                {
                    _entradas.AddRange(previas);
                }
            }
            catch (JsonException)
            {
                // Índice dañado: se vuelve a generar.
            }
        }
    }

    public IReadOnlyList<EntradaCaptura> Entradas => _entradas;

    /// <summary>Primer DPI distinto de 96 detectado (0 si todas las capturas fueron a 96).</summary>
    public int DpiDistinto { get; private set; }

    public static void QuitarEsquinasRedondeadas(Form f)
    {
        try
        {
            var valor = Win32Capturas.DWMWCP_DONOTROUND;
            Win32Capturas.DwmSetWindowAttribute(f.Handle, Win32Capturas.DWMWA_WINDOW_CORNER_PREFERENCE, ref valor, sizeof(int));
        }
        catch
        {
            // Solo estético.
        }
    }

    /// <summary>Captura la ventana <paramref name="ventana"/> (y los desplegables abiertos que son ventanas aparte) en id.png.</summary>
    public EntradaCaptura Capturar(
        string id,
        string titulo,
        string descripcion,
        Form ventana,
        IEnumerable<object>? destacados = null,
        bool soloDestacados = false,
        Control? recortarA = null,
        int margen = 6,
        Rectangle? regionPantalla = null)
    {
        var hwnd = ventana.Handle;
        using var imagenVentana = CapturarVentana(hwnd, out var origen, out var recorte);
        using var lienzo = new Bitmap(imagenVentana.Width, imagenVentana.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(lienzo))
        {
            g.DrawImageUnscaled(imagenVentana, 0, 0);

            // Desplegables (menús, listas de combo) que son ventanas aparte, de abajo hacia arriba.
            var propias = Win32Capturas.VentanasDelProceso();
            for (var i = propias.Count - 1; i >= 0; i--)
            {
                var h = propias[i];
                if (h == hwnd || !EsDesplegable(h))
                {
                    continue;
                }

                Win32Capturas.GetWindowRect(h, out var r);
                using var sub = CapturarVentanaSimple(h, r);
                if (sub is not null)
                {
                    g.DrawImageUnscaled(sub, r.Left - origen.X, r.Top - origen.Y);
                }
            }
        }

        using var completa = Opaco(lienzo, recorte);
        var controles = ListarControles(ventana, destacados, soloDestacados, origen, recorte);
        var final = completa;
        if (recortarA is not null || regionPantalla is not null)
        {
            // Recorte a un control o a una región de pantalla (por ejemplo, la barra de estado o la barra de menús con su desplegable):
            // las coordenadas de los controles pasan a ser relativas al recorte.
            var rs = regionPantalla ?? recortarA!.RectangleToScreen(recortarA.ClientRectangle);
            var region = Rectangle.Inflate(new Rectangle(rs.X - origen.X - recorte.X, rs.Y - origen.Y - recorte.Y, rs.Width, rs.Height), margen, margen);
            region.Intersect(new Rectangle(0, 0, completa.Width, completa.Height));
            final = Opaco(completa, region);
            controles = controles
                .Select(c => c with { X = c.X - region.X, Y = c.Y - region.Y })
                .Where(c => c.X >= 0 && c.Y >= 0 && c.X + c.Ancho <= region.Width + 1 && c.Y + c.Alto <= region.Height + 1)
                .ToList();
        }

        var dpi = ventana.DeviceDpi;
        var archivo = id + ".png";
        final.Save(Path.Combine(_carpetaSalida, archivo), ImageFormat.Png);
        var entrada = new EntradaCaptura(id, archivo, titulo, descripcion, final.Width, final.Height, dpi, controles);
        if (!ReferenceEquals(final, completa))
        {
            final.Dispose();
        }

        _entradas.RemoveAll(e => e.Id == id);
        _entradas.Add(entrada);
        if (dpi != 96 && DpiDistinto == 0)
        {
            DpiDistinto = dpi;
        }

        return entrada;
    }

    /// <summary>Captura una ventana nativa (cuadro de mensaje) con los botones indicados como controles.</summary>
    public EntradaCaptura CapturarNativa(string id, string titulo, string descripcion, IntPtr hwnd)
    {
        using var imagen = CapturarVentana(hwnd, out var origen, out var recorte);
        using var final = Opaco(imagen, recorte);
        var archivo = id + ".png";
        final.Save(Path.Combine(_carpetaSalida, archivo), ImageFormat.Png);
        var dpi = (int)Win32Capturas.GetDpiForWindow(hwnd);
        var controles = new List<ControlCapturado>();
        foreach (var (clase, texto, rect) in Win32Capturas.Hijos(hwnd))
        {
            if (clase == "Button")
            {
                controles.Add(new ControlCapturado("Boton", "Button", texto, rect.Left - origen.X - recorte.X, rect.Top - origen.Y - recorte.Y, rect.Width, rect.Height));
            }
        }

        var entrada = new EntradaCaptura(id, archivo, titulo, descripcion, final.Width, final.Height, dpi, controles);
        _entradas.RemoveAll(e => e.Id == id);
        _entradas.Add(entrada);
        return entrada;
    }

    public void GuardarIndice()
    {
        var opciones = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        var ordenadas = _entradas.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        File.WriteAllText(Path.Combine(_carpetaSalida, "capturas.json"), JsonSerializer.Serialize(ordenadas, opciones));
    }

    private static bool EsDesplegable(IntPtr h)
    {
        var clase = Win32Capturas.Clase(h);
        if (clase.StartsWith("ComboLBox", StringComparison.Ordinal))
        {
            return true;
        }

        return Control.FromHandle(h) is ToolStripDropDown;
    }

    /// <summary>
    /// Devuelve la imagen de la ventana completa (tamaño de GetWindowRect), el origen de pantalla de esa imagen y el
    /// rectángulo visible (sin el borde invisible de las ventanas de Windows 10/11).
    /// </summary>
    private static Bitmap CapturarVentana(IntPtr hwnd, out Point origen, out Rectangle recorte)
    {
        Win32Capturas.GetWindowRect(hwnd, out var r);
        origen = new Point(r.Left, r.Top);
        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            var hdc = g.GetHdc();
            try
            {
                Win32Capturas.PrintWindow(hwnd, hdc, Win32Capturas.PW_RENDERFULLCONTENT);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        // El borde invisible de las ventanas de Windows 10/11 sale negro: se recorta por píxeles (DwmGetWindowAttribute devuelve
        // píxeles físicos y no coincide con GetWindowRect en un proceso sin escalado).
        recorte = RecorteVisible(bmp);

        return bmp;
    }

    /// <summary>Copia opaca (sin transparencia) de la región indicada.</summary>
    private static Bitmap Opaco(Bitmap origen, Rectangle region)
    {
        var resultado = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(resultado);
        g.Clear(Color.White);
        g.DrawImage(origen, new Rectangle(0, 0, region.Width, region.Height), region, GraphicsUnit.Pixel);
        return resultado;
    }

    /// <summary>
    /// Rectángulo sin las filas y columnas de los bordes que son (casi) todas negras: el borde invisible de las ventanas de
    /// Windows 10/11 sale negro, salvo la barra de título, que se pinta de lado a lado.
    /// </summary>
    private static Rectangle RecorteVisible(Bitmap bmp)
    {
        var datos = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = datos.Stride;
            var bytes = new byte[Math.Abs(stride) * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(datos.Scan0, bytes, 0, bytes.Length);
            var ancho = bmp.Width;
            var alto = bmp.Height;

            bool Negro(int x, int y)
            {
                var i = y * stride + x * 4;
                return bytes[i + 3] == 0 || (bytes[i] == 0 && bytes[i + 1] == 0 && bytes[i + 2] == 0);
            }

            bool ColumnaNegra(int x)
            {
                var negros = 0;
                for (var y = Math.Min(40, alto / 2); y < alto; y++)
                {
                    if (Negro(x, y))
                    {
                        negros++;
                    }
                }

                return negros >= (alto - Math.Min(40, alto / 2)) * 0.95;
            }

            bool FilaNegra(int y)
            {
                var negros = 0;
                for (var x = 12; x < ancho - 12; x++)
                {
                    if (Negro(x, y))
                    {
                        negros++;
                    }
                }

                return negros >= (ancho - 24) * 0.95;
            }

            int izq = 0, der = ancho, arriba = 0, abajo = alto;
            while (izq < der - 1 && ColumnaNegra(izq)) { izq++; }
            while (der - 1 > izq && ColumnaNegra(der - 1)) { der--; }
            while (arriba < abajo - 1 && FilaNegra(arriba)) { arriba++; }
            while (abajo - 1 > arriba && FilaNegra(abajo - 1)) { abajo--; }
            return Rectangle.FromLTRB(izq, arriba, der, abajo);
        }
        finally
        {
            bmp.UnlockBits(datos);
        }
    }

    private static Bitmap? CapturarVentanaSimple(IntPtr hwnd, Win32Capturas.Rect r)
    {
        if (r.Width <= 0 || r.Height <= 0)
        {
            return null;
        }

        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var hdc = g.GetHdc();
        try
        {
            Win32Capturas.PrintWindow(hwnd, hdc, Win32Capturas.PW_RENDERFULLCONTENT);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }

        return bmp;
    }

    private static List<ControlCapturado> ListarControles(Form ventana, IEnumerable<object>? destacados, bool soloDestacados, Point origen, Rectangle recorte)
    {
        var resultado = new List<ControlCapturado>();
        var nombres = NombresDeCampos(ventana);
        var yaVistos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var dx = origen.X + recorte.X;
        var dy = origen.Y + recorte.Y;

        void Agregar(object o)
        {
            if (!yaVistos.Add(o))
            {
                return;
            }

            nombres.TryGetValue(o, out var nombre);
            switch (o)
            {
                case Control c when c.IsHandleCreated && c.Visible && c.Width > 0 && c.Height > 0:
                {
                    var rs = c.RectangleToScreen(c.ClientRectangle);
                    resultado.Add(new ControlCapturado(nombre ?? c.Name ?? string.Empty, c.GetType().Name, TextoDe(c), rs.X - dx, rs.Y - dy, rs.Width, rs.Height));
                    break;
                }
                case ToolStripItem t when t.Available && t.Visible && t.Owner is not null:
                {
                    var rs = t.Owner.RectangleToScreen(t.Bounds);
                    resultado.Add(new ControlCapturado(nombre ?? t.Name ?? string.Empty, t.GetType().Name, t.Text ?? string.Empty, rs.X - dx, rs.Y - dy, rs.Width, rs.Height));
                    break;
                }
            }
        }

        if (destacados is not null)
        {
            foreach (var o in destacados)
            {
                if (o is not null)
                {
                    Agregar(o);
                }
            }
        }

        if (soloDestacados)
        {
            return resultado;
        }

        var todos = Descendientes(ventana).ToList();
        foreach (var c in todos)
        {
            if (c is Button or TextBox or ComboBox or CheckBox or RadioButton or DateTimePicker or NumericUpDown or DataGridView
                or ListBox or ListView or TabControl or Controles.ControlBusquedaCatalogo or Controles.TarjetaAccion or PictureBox)
            {
                Agregar(c);
            }
        }

        foreach (var m in todos.OfType<MenuStrip>())
        {
            foreach (ToolStripItem item in m.Items)
            {
                Agregar(item);
            }
        }

        foreach (var s in todos.OfType<StatusStrip>())
        {
            foreach (ToolStripItem item in s.Items)
            {
                Agregar(item);
            }
        }

        return resultado;
    }

    internal static IEnumerable<Control> Descendientes(Control raiz)
    {
        foreach (Control c in raiz.Controls)
        {
            yield return c;
            foreach (var d in Descendientes(c))
            {
                yield return d;
            }
        }
    }

    private static string TextoDe(Control c) => c switch
    {
        Controles.ControlBusquedaCatalogo b => b.CajaTexto.Text,
        DataGridView => string.Empty,
        _ => c.Text ?? string.Empty
    };

    /// <summary>Mapa objeto → nombre del campo del formulario (los controles se crean por código y no tienen Name).</summary>
    internal static Dictionary<object, string> NombresDeCampos(Form ventana)
    {
        var mapa = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        var tipo = ventana.GetType();
        while (tipo is not null && tipo != typeof(object))
        {
            foreach (var campo in tipo.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (typeof(Control).IsAssignableFrom(campo.FieldType) || typeof(ToolStripItem).IsAssignableFrom(campo.FieldType))
                {
                    var v = campo.GetValue(ventana);
                    if (v is not null)
                    {
                        mapa.TryAdd(v, campo.Name);
                    }
                }
            }

            tipo = tipo.BaseType;
        }

        return mapa;
    }
}
#endif

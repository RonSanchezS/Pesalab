using System.Drawing.Drawing2D;

namespace PesajeFaeneo.WinForms.Controles;

public enum IconoTarjeta
{
    Balanza,
    Ticket,
    Historial
}

/// <summary>
/// Tarjeta grande de acción para la pantalla de inicio: icono vectorial, título, descripción y atajo de teclado.
/// Es un <see cref="Button"/> (Enter/Espacio, Tab y lectores de pantalla funcionan sin trabajo extra) con dibujo propio.
/// Todas las medidas se escriben a 96 DPI y se escalan con <see cref="Control.DeviceDpi"/>.
/// </summary>
public sealed class TarjetaAccion : Button
{
    private static readonly Color Acento = Color.FromArgb(0x1F, 0x4E, 0x79);
    private static readonly Color AcentoHover = Color.FromArgb(0x2A, 0x66, 0xA0);
    private static readonly Color AcentoPresionado = Color.FromArgb(0x17, 0x3B, 0x5C);
    private static readonly Color Foco = Color.FromArgb(0xF5, 0xA6, 0x23);
    private static readonly Color TextoOscuro = Color.FromArgb(0x1B, 0x27, 0x33);
    private static readonly Color TextoSecundario = Color.FromArgb(0x4B, 0x55, 0x63);
    private static readonly Color TextoSobreAcento = Color.FromArgb(0xDC, 0xE8, 0xF5);
    private static readonly Color FondoDeshabilitado = Color.FromArgb(0xE5, 0xE7, 0xEB);
    private static readonly Color TextoDeshabilitado = Color.FromArgb(0x6B, 0x72, 0x80);

    private bool _encima;
    private bool _presionada;
    private string _descripcion = string.Empty;
    private string _atajo = string.Empty;
    private IconoTarjeta _icono;
    private bool _principal;

    public TarjetaAccion()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        Size = new Size(340, 230);
    }

    public string Descripcion
    {
        get => _descripcion;
        set { _descripcion = value ?? string.Empty; Invalidate(); }
    }

    /// <summary>Texto del atajo (por ejemplo "F2"); vacío = sin insignia.</summary>
    public string Atajo
    {
        get => _atajo;
        set { _atajo = value ?? string.Empty; Invalidate(); }
    }

    public IconoTarjeta Icono
    {
        get => _icono;
        set { _icono = value; Invalidate(); }
    }

    /// <summary>La acción principal de la pantalla va rellena; las secundarias, con contorno.</summary>
    public bool Principal
    {
        get => _principal;
        set { _principal = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _encima = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _encima = false;
        _presionada = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _presionada = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _presionada = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var s = DeviceDpi / 96f;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (Parent is not null)
        {
            using var fondoPadre = new SolidBrush(Parent.BackColor);
            g.FillRectangle(fondoPadre, ClientRectangle);
        }

        Color fondo, borde, titulo, detalle, icono;
        if (!Enabled)
        {
            fondo = FondoDeshabilitado;
            borde = FondoDeshabilitado;
            titulo = detalle = icono = TextoDeshabilitado;
        }
        else if (_principal)
        {
            fondo = _presionada ? AcentoPresionado : _encima ? AcentoHover : Acento;
            borde = fondo;
            titulo = icono = Color.White;
            detalle = TextoSobreAcento;
        }
        else
        {
            fondo = _presionada ? Color.FromArgb(0xD6, 0xE4, 0xF2) : _encima ? Color.FromArgb(0xEA, 0xF1, 0xF8) : Color.White;
            borde = Acento;
            titulo = TextoOscuro;
            detalle = TextoSecundario;
            icono = Acento;
        }

        var margen = (int)(3 * s);
        var rect = new Rectangle(margen, margen, Width - 2 * margen - 1, Height - 2 * margen - 1);
        var radio = 14 * s;

        using (var ruta = RectanguloRedondeado(rect, radio))
        using (var pincel = new SolidBrush(fondo))
        using (var lapiz = new Pen(borde, 2 * s))
        {
            g.FillPath(pincel, ruta);
            g.DrawPath(lapiz, ruta);
        }

        // Icono: caja de 72 unidades escalada a 72 * s px, arriba a la izquierda.
        var pad = 24 * s;
        var estado = g.Save();
        g.TranslateTransform(rect.Left + pad, rect.Top + pad);
        g.ScaleTransform(s, s);
        using (var lapizIcono = new Pen(icono, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            switch (_icono)
            {
                case IconoTarjeta.Balanza:
                    DibujarBalanza(g, lapizIcono);
                    break;
                case IconoTarjeta.Historial:
                    DibujarHistorial(g, lapizIcono);
                    break;
                default:
                    DibujarTicket(g, lapizIcono);
                    break;
            }
        }

        g.Restore(estado);

        // Insignia del atajo, arriba a la derecha.
        if (_atajo.Length > 0)
        {
            using var fuenteAtajo = new Font(Font.FontFamily, 11f, FontStyle.Bold, GraphicsUnit.Point);
            var tam = TextRenderer.MeasureText(g, _atajo, fuenteAtajo, Size.Empty, TextFormatFlags.NoPadding);
            var ancho = tam.Width + 2 * 12 * s;
            var alto = tam.Height + 2 * 4 * s;
            var insignia = new RectangleF(rect.Right - pad - ancho, rect.Top + pad, ancho, alto);
            var colorInsignia = _principal && Enabled ? Color.FromArgb(0x4A, 0x7B, 0xAA) : Color.FromArgb(0xE5, 0xEC, 0xF4);
            var colorTextoInsignia = !Enabled ? TextoDeshabilitado : _principal ? Color.White : Acento;
            using (var rutaInsignia = RectanguloRedondeado(Rectangle.Round(insignia), 6 * s))
            using (var pincel = new SolidBrush(colorInsignia))
            {
                g.FillPath(pincel, rutaInsignia);
            }

            TextRenderer.DrawText(
                g, _atajo, fuenteAtajo, Rectangle.Round(insignia), colorTextoInsignia,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // Título y descripción bajo el icono.
        var izquierda = (int)(rect.Left + pad);
        var ancho2 = (int)(rect.Width - 2 * pad);
        var arribaTitulo = (int)(rect.Top + pad + 72 * s + 14 * s);
        using (var fuenteTitulo = new Font(Font.FontFamily, 20f, FontStyle.Bold, GraphicsUnit.Point))
        {
            var altoTitulo = fuenteTitulo.Height;
            TextRenderer.DrawText(
                g, Text, fuenteTitulo, new Rectangle(izquierda, arribaTitulo, ancho2, altoTitulo), titulo,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            using var fuenteDetalle = new Font(Font.FontFamily, 11f, FontStyle.Regular, GraphicsUnit.Point);
            var arribaDetalle = arribaTitulo + altoTitulo + (int)(4 * s);
            var altoDetalle = Math.Max(0, (int)(rect.Bottom - pad / 2 - arribaDetalle));
            TextRenderer.DrawText(
                g, _descripcion, fuenteDetalle, new Rectangle(izquierda, arribaDetalle, ancho2, altoDetalle), detalle,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        // Anillo de foco bien visible (teclado): ámbar, por fuera del contorno de la tarjeta.
        if (Focused && ShowFocusCues)
        {
            var anillo = Rectangle.Inflate(rect, (int)(-4 * s), (int)(-4 * s));
            using var rutaFoco = RectanguloRedondeado(anillo, 10 * s);
            using var lapizFoco = new Pen(Foco, 3 * s);
            g.DrawPath(lapizFoco, rutaFoco);
        }
    }

    // Dibujo en una caja de 72 x 72 unidades.
    private static void DibujarBalanza(Graphics g, Pen lapiz)
    {
        // Plato superior con su soporte.
        g.DrawLine(lapiz, 12, 14, 60, 14);
        g.DrawLine(lapiz, 36, 14, 36, 24);
        // Cuerpo con el indicador.
        using (var cuerpo = RectanguloRedondeado(new Rectangle(8, 24, 56, 40), 8))
        {
            g.DrawPath(lapiz, cuerpo);
        }

        // Esfera: semicírculo y aguja.
        g.DrawArc(lapiz, 22, 34, 28, 28, 180, 180);
        g.DrawLine(lapiz, 36, 48, 43, 40);
    }

    private static void DibujarTicket(Graphics g, Pen lapiz)
    {
        // Papel que entra.
        g.DrawRectangle(lapiz, 20, 6, 32, 18);
        // Cuerpo de la impresora.
        using (var cuerpo = RectanguloRedondeado(new Rectangle(6, 24, 60, 26), 6))
        {
            g.DrawPath(lapiz, cuerpo);
        }

        // Papel que sale, con líneas de texto.
        g.DrawRectangle(lapiz, 18, 42, 36, 24);
        g.DrawLine(lapiz, 24, 51, 48, 51);
        g.DrawLine(lapiz, 24, 58, 42, 58);
    }

    private static void DibujarHistorial(Graphics g, Pen lapiz)
    {
        // Hoja con renglones (la lista de pesajes) y un punto en cada renglón.
        using (var hoja = RectanguloRedondeado(new Rectangle(10, 6, 52, 60), 6))
        {
            g.DrawPath(lapiz, hoja);
        }

        for (var i = 0; i < 3; i++)
        {
            var y = 22 + i * 14;
            g.DrawEllipse(lapiz, 18, y - 3, 6, 6);
            g.DrawLine(lapiz, 30, y, 54, y);
        }
    }

    private static GraphicsPath RectanguloRedondeado(Rectangle r, float radio)
    {
        var d = Math.Max(1f, radio * 2);
        var ruta = new GraphicsPath();
        ruta.AddArc(r.Left, r.Top, d, d, 180, 90);
        ruta.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        ruta.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        ruta.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        ruta.CloseFigure();
        return ruta;
    }
}

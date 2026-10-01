using System.Drawing;
using System.Drawing.Printing;

namespace PesajeFaeneo.WinForms.Impresion;

internal sealed class DocumentoPaginasReporte : PrintDocument
{
    private readonly IReadOnlyList<byte[]> _paginas;
    private readonly bool _paraVistaPrevia;
    private int _indice;

    public DocumentoPaginasReporte(IReadOnlyList<byte[]> paginas, bool paraVistaPrevia, string nombreDocumento)
    {
        ArgumentNullException.ThrowIfNull(paginas);
        if (paginas.Count == 0)
        {
            throw new ArgumentException("El reporte no tiene páginas.", nameof(paginas));
        }

        _paginas = paginas;
        _paraVistaPrevia = paraVistaPrevia;

        PrintController = new StandardPrintController();
        OriginAtMargins = false;
        DocumentName = string.IsNullOrWhiteSpace(nombreDocumento) ? "Reporte" : nombreDocumento;
    }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        base.OnBeginPrint(e);
        _indice = 0;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);
        float mx = 0f, my = 0f;
        if (!_paraVistaPrevia)
        {
            mx = e.PageSettings.HardMarginX;
            my = e.PageSettings.HardMarginY;
        }

        e.HasMorePages = ProcesarPagina(e.Graphics!, new SizeF(e.PageBounds.Width, e.PageBounds.Height), mx, my);
    }

    internal bool ProcesarPagina(Graphics g, SizeF pagina, float margenFisicoX, float margenFisicoY)
    {
        using var ms = new MemoryStream(_paginas[_indice], writable: false);
        using var imagen = Image.FromStream(ms);
        g.DrawImage(imagen, CalcularDestino(pagina, imagen.Size, margenFisicoX, margenFisicoY));
        _indice++;
        return _indice < _paginas.Count;
    }

    internal static RectangleF CalcularDestino(SizeF pagina, Size imagenPx, float margenFisicoX, float margenFisicoY)
    {
        var escala = Math.Min(pagina.Width / imagenPx.Width, pagina.Height / imagenPx.Height);
        var ancho = imagenPx.Width * escala;
        var alto = imagenPx.Height * escala;
        return new RectangleF(
            (pagina.Width - ancho) / 2f - margenFisicoX,
            (pagina.Height - alto) / 2f - margenFisicoY,
            ancho,
            alto);
    }

    internal void SimularInicioParaTest() => OnBeginPrint(new PrintEventArgs());
}

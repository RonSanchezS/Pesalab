using System.Drawing;
using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas.Diseno;

namespace PesajeFaeneo.WinForms.Impresion;

internal sealed class DocumentoBoleta : PrintDocument
{
    private readonly Func<int, PaginaBoleta> _crearPagina;
    private readonly int _totalCopias;
    private readonly Image? _logo;
    private readonly float _desplX;
    private readonly float _desplY;
    private readonly bool _paraVistaPrevia;
    private int _copiaActual = 1;

    public DocumentoBoleta(
        Func<int, PaginaBoleta> crearPagina,
        int totalCopias,
        Image? logo,
        float desplazamientoXMm,
        float desplazamientoYMm,
        bool paraVistaPrevia,
        string nombreDocumento)
    {
        ArgumentNullException.ThrowIfNull(crearPagina);
        if (totalCopias is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCopias), totalCopias, "El total de copias debe estar entre 1 y 3.");
        }

        _crearPagina = crearPagina;
        _totalCopias = totalCopias;
        _logo = logo;
        _desplX = desplazamientoXMm;
        _desplY = desplazamientoYMm;
        _paraVistaPrevia = paraVistaPrevia;

        PrintController = new StandardPrintController();
        OriginAtMargins = false;
        DocumentName = string.IsNullOrWhiteSpace(nombreDocumento) ? "Boleta de pesaje" : nombreDocumento;
    }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        base.OnBeginPrint(e);
        _copiaActual = 1;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);
        float mx = 0f, my = 0f;
        if (!_paraVistaPrevia)
        {
            mx = e.PageSettings.HardMarginX * 0.254f;
            my = e.PageSettings.HardMarginY * 0.254f;
        }

        e.HasMorePages = ProcesarPagina(e.Graphics!, mx, my);
    }

    internal bool ProcesarPagina(Graphics g, float margenFisicoXMm, float margenFisicoYMm)
    {
        var pagina = _crearPagina(_copiaActual);
        PintorBoleta.Pintar(g, pagina, _logo, _desplX - margenFisicoXMm, _desplY - margenFisicoYMm);
        var hayMas = _copiaActual < _totalCopias;
        _copiaActual++;
        return hayMas;
    }

    internal void SimularInicioParaTest()
    {
        OnBeginPrint(new PrintEventArgs());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logo?.Dispose();
        }
        base.Dispose(disposing);
    }
}

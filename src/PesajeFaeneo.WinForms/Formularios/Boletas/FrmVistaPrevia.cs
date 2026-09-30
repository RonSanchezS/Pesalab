using System.Drawing.Printing;
using PesajeFaeneo.WinForms.Formularios;

namespace PesajeFaeneo.WinForms.Formularios.Boletas;

public class FrmVistaPrevia : FormBase
{
    private readonly PrintDocument _documento;
    internal PrintPreviewControl ppcVista;
    internal Button btnCerrar;

    public FrmVistaPrevia(PrintDocument documento, string titulo)
    {
        _documento = documento ?? throw new ArgumentNullException(nameof(documento));

        Text = titulo;
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;

        ppcVista = new PrintPreviewControl
        {
            Dock = DockStyle.Fill,
            Document = _documento,
            AutoZoom = true,
            UseAntiAlias = true
        };

        var panelInferior = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10)
        };

        btnCerrar = new Button
        {
            Text = "Cerrar (Esc)",
            Size = new Size(120, 32),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(panelInferior.Width - 130, 9)
        };
        btnCerrar.Click += (_, _) => Close();

        panelInferior.Controls.Add(btnCerrar);

        Controls.Add(ppcVista);
        Controls.Add(panelInferior);

        CancelButton = btnCerrar;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _documento.Dispose();
        }
        base.Dispose(disposing);
    }
}

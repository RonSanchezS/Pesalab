namespace PesajeFaeneo.WinForms.Formularios;

public partial class FrmPrincipal : FormBase
{
    public FrmPrincipal()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Sistema de Pesaje para Faeneo";
        ClientSize = new Size(1024, 700);
        MinimumSize = new Size(800, 600);
        WindowState = FormWindowState.Maximized;

        var lblBienvenida = new Label
        {
            Text = "Sistema de Pesaje para Faeneo\nFundaciones iniciadas exitosamente.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold)
        };

        Controls.Add(lblBienvenida);
    }
}

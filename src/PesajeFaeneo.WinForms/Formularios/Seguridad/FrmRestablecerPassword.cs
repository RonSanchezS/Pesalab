using PesajeFaeneo.Application.Seguridad;

namespace PesajeFaeneo.WinForms.Formularios.Seguridad;

public class FrmRestablecerPassword : FormBase
{
    private readonly IServicioUsuarios _servicioUsuarios;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;

    private Label lblTitulo = null!;
    private Label lblNueva = null!;
    private TextBox txtNueva = null!;
    private Label lblConfirmacion = null!;
    private TextBox txtConfirmacion = null!;
    private Button btnAceptar = null!;
    private Button btnCancelar = null!;
    private ErrorProvider _errorProvider = null!;

    public FrmRestablecerPassword(IServicioUsuarios servicioUsuarios)
    {
        _servicioUsuarios = servicioUsuarios ?? throw new ArgumentNullException(nameof(servicioUsuarios));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposUsuario.Password, txtNueva },
            { CamposUsuario.Confirmacion, txtConfirmacion }
        };
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Restablecer contraseña de usuario";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 220);

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(20, 15, 25, 15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lblTitulo = new Label
        {
            Text = "Defina la nueva contraseña temporal:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainLayout.SetColumnSpan(lblTitulo, 2);
        mainLayout.Controls.Add(lblTitulo, 0, 0);

        lblNueva = new Label
        {
            Text = "Nueva contraseña:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtNueva = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            MaxLength = 100
        };
        mainLayout.Controls.Add(lblNueva, 0, 1);
        mainLayout.Controls.Add(txtNueva, 1, 1);

        lblConfirmacion = new Label
        {
            Text = "Confirmación:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtConfirmacion = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            MaxLength = 100
        };
        mainLayout.Controls.Add(lblConfirmacion, 0, 2);
        mainLayout.Controls.Add(txtConfirmacion, 1, 2);

        var pnlBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        btnCancelar = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 35)
        };
        btnCancelar.Click += (_, _) => Close();

        btnAceptar = new Button
        {
            Text = "Aceptar",
            Size = new Size(100, 35)
        };
        btnAceptar.Click += async (_, _) => await RestablecerClickAsync();

        pnlBotones.Controls.Add(btnCancelar);
        pnlBotones.Controls.Add(btnAceptar);

        mainLayout.SetColumnSpan(pnlBotones, 2);
        mainLayout.Controls.Add(pnlBotones, 0, 3);

        Controls.Add(mainLayout);

        AcceptButton = btnAceptar;
        CancelButton = btnCancelar;

        ResumeLayout(false);
    }

    private async Task RestablecerClickAsync()
    {
        await EjecutarAsync(async () =>
        {
            try
            {
                await _servicioUsuarios.RestablecerPasswordAsync(UsuarioId, txtNueva.Text, txtConfirmacion.Text);
                MessageBox.Show(this,
                    "La contraseña fue restablecida. El usuario deberá cambiarla al ingresar.",
                    "Contraseña restablecida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
                txtNueva.Clear();
                txtConfirmacion.Clear();
            }
        }, btnAceptar);
    }
}

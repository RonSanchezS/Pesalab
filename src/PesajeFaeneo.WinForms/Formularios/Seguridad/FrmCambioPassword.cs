using PesajeFaeneo.Application.Seguridad;

namespace PesajeFaeneo.WinForms.Formularios.Seguridad;

public class FrmCambioPassword : FormBase
{
    private readonly IServicioAutenticacion _autenticacion;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private Label lblTitulo = null!;
    private Label lblActual = null!;
    private TextBox txtActual = null!;
    private Label lblNueva = null!;
    private TextBox txtNueva = null!;
    private Label lblConfirmacion = null!;
    private TextBox txtConfirmacion = null!;
    private Button btnAceptar = null!;
    private Button btnCancelar = null!;
    private ErrorProvider _errorProvider = null!;

    private bool _obligatorio;

    public bool Obligatorio
    {
        get => _obligatorio;
        set
        {
            _obligatorio = value;
            ActualizarTitulo();
        }
    }

    public FrmCambioPassword(IServicioAutenticacion autenticacion)
    {
        _autenticacion = autenticacion ?? throw new ArgumentNullException(nameof(autenticacion));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposUsuario.PasswordActual, txtActual },
            { CamposUsuario.Password, txtNueva },
            { CamposUsuario.Confirmacion, txtConfirmacion }
        };

        ActualizarTitulo();
    }

    private void ActualizarTitulo()
    {
        if (lblTitulo == null) return;

        if (_obligatorio)
        {
            Text = "Cambio de contraseña requerido";
            lblTitulo.Text = "Debe cambiar su contraseña para continuar";
        }
        else
        {
            Text = "Cambiar contraseña";
            lblTitulo.Text = "Cambio de contraseña";
        }
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 260);

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(20, 15, 25, 15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lblTitulo = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainLayout.SetColumnSpan(lblTitulo, 2);
        mainLayout.Controls.Add(lblTitulo, 0, 0);

        lblActual = new Label
        {
            Text = "Contraseña actual:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtActual = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            MaxLength = 100
        };
        mainLayout.Controls.Add(lblActual, 0, 1);
        mainLayout.Controls.Add(txtActual, 1, 1);

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
        mainLayout.Controls.Add(lblNueva, 0, 2);
        mainLayout.Controls.Add(txtNueva, 1, 2);

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
        mainLayout.Controls.Add(lblConfirmacion, 0, 3);
        mainLayout.Controls.Add(txtConfirmacion, 1, 3);

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
        btnAceptar.Click += btnAceptar_Click;

        pnlBotones.Controls.Add(btnCancelar);
        pnlBotones.Controls.Add(btnAceptar);

        mainLayout.SetColumnSpan(pnlBotones, 2);
        mainLayout.Controls.Add(pnlBotones, 0, 4);

        Controls.Add(mainLayout);

        AcceptButton = btnAceptar;
        CancelButton = btnCancelar;

        ResumeLayout(false);
    }

    private async void btnAceptar_Click(object? sender, EventArgs e)
    {
        await EjecutarAsync(async () =>
        {
            try
            {
                await _autenticacion.CambiarPasswordPropiaAsync(
                    txtActual.Text,
                    txtNueva.Text,
                    txtConfirmacion.Text);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                txtActual.Clear();
                txtNueva.Clear();
                txtConfirmacion.Clear();

                if (!MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
                {
                    throw;
                }
            }
        }, btnAceptar);
    }

    // Helper interno para tests
    internal void SetCampos(string actual, string nueva, string confirmacion)
    {
        txtActual.Text = actual;
        txtNueva.Text = nueva;
        txtConfirmacion.Text = confirmacion;
    }

    internal Task AceptarParaTestAsync()
    {
        btnAceptar_Click(btnAceptar, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

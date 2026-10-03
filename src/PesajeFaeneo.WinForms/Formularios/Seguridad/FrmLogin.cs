using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Application.Seguridad;

namespace PesajeFaeneo.WinForms.Formularios.Seguridad;

public class FrmLogin : FormBase
{
    private readonly IServicioAutenticacion _autenticacion;

    internal TextBox txtUsuario = null!;
    internal TextBox txtPassword = null!;
    internal Button btnIngresar = null!;
    internal Button btnSalir = null!;
    internal Label lblMensaje = null!;
    internal Label lblBloqMayus = null!;
    internal Label lblVersion = null!;

    public bool DebeCambiarPassword { get; private set; }

    public FrmLogin(IServicioAutenticacion autenticacion)
    {
        _autenticacion = autenticacion ?? throw new ArgumentNullException(nameof(autenticacion));

        InitializeComponent();

        Activated += (_, _) => ActualizarEstadoBloqMayus();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Sistema de Pesaje para Faeneo — Iniciar sesión";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 260);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(25, 20, 25, 15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblUsuario = new Label
        {
            Text = "Usuario:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtUsuario = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = 30
        };
        txtUsuario.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                txtPassword.Focus();
            }
        };

        mainLayout.Controls.Add(lblUsuario, 0, 0);
        mainLayout.Controls.Add(txtUsuario, 1, 0);

        var lblPassword = new Label
        {
            Text = "Contraseña:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtPassword = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            MaxLength = 100
        };
        txtPassword.KeyUp += (_, _) => ActualizarEstadoBloqMayus();

        mainLayout.Controls.Add(lblPassword, 0, 1);
        mainLayout.Controls.Add(txtPassword, 1, 1);

        lblBloqMayus = new Label
        {
            Text = "Bloq Mayús está activado",
            ForeColor = Color.DarkOrange,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        mainLayout.Controls.Add(lblBloqMayus, 1, 2);

        lblMensaje = new Label
        {
            Text = string.Empty,
            ForeColor = Color.Crimson,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        mainLayout.SetColumnSpan(lblMensaje, 2);
        mainLayout.Controls.Add(lblMensaje, 0, 3);

        var pnlBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        btnSalir = new Button
        {
            Text = "Salir",
            DialogResult = DialogResult.Cancel,
            Size = new Size(95, 35)
        };
        btnSalir.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        btnIngresar = new Button
        {
            Text = "Ingresar",
            Size = new Size(95, 35)
        };
        btnIngresar.Click += async (_, _) => await IniciarSesionClickAsync();

        pnlBotones.Controls.Add(btnSalir);
        pnlBotones.Controls.Add(btnIngresar);

        mainLayout.SetColumnSpan(pnlBotones, 2);
        mainLayout.Controls.Add(pnlBotones, 0, 4);

        lblVersion = new Label
        {
            Text = $"v{InfoVersion.Version}",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomRight
        };
        mainLayout.SetColumnSpan(lblVersion, 2);
        mainLayout.Controls.Add(lblVersion, 0, 5);

        Controls.Add(mainLayout);

        AcceptButton = btnIngresar;
        CancelButton = btnSalir;

        ResumeLayout(false);
    }

    private void ActualizarEstadoBloqMayus()
    {
        lblBloqMayus.Visible = IsKeyLocked(Keys.CapsLock);
    }

    private async Task IniciarSesionClickAsync()
    {
        await EjecutarAsync(async () =>
        {
            var resultado = await _autenticacion.IniciarSesionAsync(txtUsuario.Text, txtPassword.Text);
            if (resultado.Tipo != ResultadoLoginTipo.Exitoso)
            {
                lblMensaje.Text = resultado.Mensaje;
                txtPassword.Clear();
                txtPassword.Focus();
                return;
            }

            DebeCambiarPassword = resultado.DebeCambiarPassword;
            DialogResult = DialogResult.OK;
            Close();
        }, btnIngresar);
    }

    internal async Task IngresarAsync()
    {
        await IniciarSesionClickAsync();
    }
}

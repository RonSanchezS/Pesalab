using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.PuestaEnMarcha;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.WinForms.Infraestructura;
using Serilog;

namespace PesajeFaeneo.WinForms.Formularios.Seguridad;

public class FrmLogin : FormBase
{
    private readonly IServicioAutenticacion _autenticacion;
    private readonly IServicioParametros _parametros;

    private const string TituloPorDefecto = "Capataz";

    internal TextBox txtUsuario = null!;
    internal TextBox txtPassword = null!;
    internal Button btnIngresar = null!;
    internal Button btnSalir = null!;
    internal Label lblMensaje = null!;
    internal Label lblBloqMayus = null!;
    internal Label lblVersion = null!;
    internal PictureBox picLogo = null!;
    internal Label lblEmpresa = null!;

    public bool DebeCambiarPassword { get; private set; }

    public FrmLogin(IServicioAutenticacion autenticacion, IServicioParametros parametros)
    {
        _autenticacion = autenticacion ?? throw new ArgumentNullException(nameof(autenticacion));
        _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));

        InitializeComponent();

        Activated += (_, _) => ActualizarEstadoBloqMayus();
        // El encabezado se carga sin bloquear el inicio de sesión: no pasa por EjecutarAsync (cuya protección contra
        // reentrada descartaría un clic en "Ingresar" hecho mientras carga) y un error solo se registra en el log.
        Shown += async (_, _) => await CargarEncabezadoAsync();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Capataz — Iniciar sesión";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 360);

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

        // Encabezado de marca (F10, O7): logo y nombre de la empresa. Crece con el texto (AutoSize) en vez de usar
        // alturas fijas; sin logo solo se ve el nombre.
        picLogo = new PictureBox
        {
            Size = new Size(72, 72),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 14, 0),
            Anchor = AnchorStyles.Left,
            Visible = false,
            AccessibleName = "Logo de la empresa",
            TabStop = false
        };

        lblEmpresa = new Label
        {
            Text = TituloPorDefecto,
            AutoSize = true,
            MaximumSize = new Size(300, 0),
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x1B, 0x27, 0x33),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };

        var encabezado = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Padding = new Padding(25, 16, 25, 12)
        };
        encabezado.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        encabezado.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        encabezado.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        encabezado.Controls.Add(picLogo, 0, 0);
        encabezado.Controls.Add(lblEmpresa, 1, 0);

        var separador = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(0xD5, 0xDA, 0xE0) };

        // El orden de Controls.Add define el acople: el último agregado se acopla primero (arriba).
        Controls.Add(mainLayout);
        Controls.Add(separador);
        Controls.Add(encabezado);

        AcceptButton = btnIngresar;
        CancelButton = btnSalir;

        ResumeLayout(false);
    }

    private async Task CargarEncabezadoAsync()
    {
        try
        {
            var identidad = await _parametros.ObtenerIdentidadEmpresaAsync();
            if (IsDisposed)
            {
                return;
            }

            var nombre = identidad.NombreEmpresa?.Trim() ?? string.Empty;
            lblEmpresa.Text = nombre.Length == 0 || !ReglasPuestaEnMarcha.EmpresaConfigurada(nombre)
                ? TituloPorDefecto
                : nombre;

            var logo = CargadorLogoUi.Cargar(identidad.RutaLogoCompleta, "la pantalla de inicio de sesión");
            var anterior = picLogo.Image;
            picLogo.Image = logo;
            picLogo.Visible = logo is not null;
            anterior?.Dispose();
        }
        catch (Exception ex)
        {
            // El encabezado es decorativo: si falla, el inicio de sesión sigue usable con el título genérico.
            Log.Warning(ex, "No se pudo cargar el nombre y logo de la empresa para la pantalla de inicio de sesión.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            picLogo?.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    internal Task CargarEncabezadoParaTestAsync() => CargarEncabezadoAsync();

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

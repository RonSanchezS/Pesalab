using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;

namespace PesajeFaeneo.WinForms.Formularios.Administracion;

public class FrmParametros : FormBase
{
    private readonly IServicioParametros _servicioParametros;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private long _siguienteNumeroBoletaOriginal;
    private AccionLogo _accionLogo = AccionLogo.SinCambio;
    private string? _rutaLogoNueva;

    internal Label lblTitulo = null!;
    internal Label lblNombreEmpresa = null!;
    internal TextBox txtNombreEmpresa = null!;
    internal Label lblDireccion = null!;
    internal TextBox txtDireccion = null!;
    internal Label lblCiudad = null!;
    internal TextBox txtCiudad = null!;
    internal Label lblTelefono = null!;
    internal TextBox txtTelefono = null!;
    internal Label lblRegistroSanitario = null!;
    internal TextBox txtRegistroSanitario = null!;
    internal Label lblTelefonosReclamos = null!;
    internal TextBox txtTelefonosReclamos = null!;
    internal Label lblObservacionPorDefecto = null!;
    internal TextBox txtObservacionPorDefecto = null!;

    internal GroupBox grpBoleta = null!;
    internal Label lblSiguienteBoleta = null!;
    internal NumericUpDown numSiguienteBoleta = null!;
    internal Label lblUltimaBoleta = null!;

    internal GroupBox grpLogo = null!;
    internal PictureBox picLogo = null!;
    internal Button btnCambiarLogo = null!;
    internal Button btnQuitarLogo = null!;

    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;
    internal ErrorProvider _errorProvider = null!;

    public FrmParametros(IServicioParametros servicioParametros)
    {
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposParametros.NombreEmpresa, txtNombreEmpresa },
            { CamposParametros.Direccion, txtDireccion },
            { CamposParametros.Ciudad, txtCiudad },
            { CamposParametros.Telefono, txtTelefono },
            { CamposParametros.RegistroSanitario, txtRegistroSanitario },
            { CamposParametros.TelefonosReclamos, txtTelefonosReclamos },
            { CamposParametros.ObservacionPorDefecto, txtObservacionPorDefecto },
            { CamposParametros.SiguienteNumeroBoleta, numSiguienteBoleta },
            { CamposParametros.Logo, picLogo }
        };

        Shown += async (_, _) => await EjecutarAsync(CargarAsync);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Parámetros del Sistema";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 560);

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20, 15, 20, 15)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        lblTitulo = new Label
        {
            Text = "Configuración de la Empresa y Parámetros del Sistema",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainLayout.SetColumnSpan(lblTitulo, 2);
        mainLayout.Controls.Add(lblTitulo, 0, 0);

        // Panel Izquierdo: Datos de la Empresa
        var pnlEmpresa = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(0, 0, 15, 0)
        };
        pnlEmpresa.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        pnlEmpresa.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < 6; i++)
        {
            pnlEmpresa.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }
        pnlEmpresa.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); // Observación (multilínea)
        pnlEmpresa.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lblNombreEmpresa = new Label { Text = "Nombre Empresa:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtNombreEmpresa = new TextBox { Dock = DockStyle.Fill, MaxLength = ReglasParametros.LongitudMaximaNombreEmpresa };
        pnlEmpresa.Controls.Add(lblNombreEmpresa, 0, 0);
        pnlEmpresa.Controls.Add(txtNombreEmpresa, 1, 0);

        lblDireccion = new Label { Text = "Dirección:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtDireccion = new TextBox { Dock = DockStyle.Fill, MaxLength = ReglasParametros.LongitudMaximaDireccion };
        pnlEmpresa.Controls.Add(lblDireccion, 0, 1);
        pnlEmpresa.Controls.Add(txtDireccion, 1, 1);

        lblCiudad = new Label { Text = "Ciudad:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtCiudad = new TextBox { Dock = DockStyle.Fill, MaxLength = ReglasParametros.LongitudMaximaCiudad };
        pnlEmpresa.Controls.Add(lblCiudad, 0, 2);
        pnlEmpresa.Controls.Add(txtCiudad, 1, 2);

        lblTelefono = new Label { Text = "Teléfono:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtTelefono = new TextBox { Dock = DockStyle.Fill, MaxLength = ReglasParametros.LongitudMaximaTelefono };
        pnlEmpresa.Controls.Add(lblTelefono, 0, 3);
        pnlEmpresa.Controls.Add(txtTelefono, 1, 3);

        lblRegistroSanitario = new Label { Text = "Reg. Sanitario:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtRegistroSanitario = new TextBox { Dock = DockStyle.Fill, MaxLength = ReglasParametros.LongitudMaximaRegistroSanitario };
        pnlEmpresa.Controls.Add(lblRegistroSanitario, 0, 4);
        pnlEmpresa.Controls.Add(txtRegistroSanitario, 1, 4);

        lblTelefonosReclamos = new Label { Text = "Tel. Reclamos:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtTelefonosReclamos = new TextBox { Dock = DockStyle.Fill, MaxLength = ReglasParametros.LongitudMaximaTelefonosReclamos };
        pnlEmpresa.Controls.Add(lblTelefonosReclamos, 0, 5);
        pnlEmpresa.Controls.Add(txtTelefonosReclamos, 1, 5);

        lblObservacionPorDefecto = new Label { Text = "Observación:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight };
        txtObservacionPorDefecto = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 60,
            ScrollBars = ScrollBars.Vertical,
            MaxLength = ReglasParametros.LongitudMaximaObservacionPorDefecto
        };
        pnlEmpresa.Controls.Add(lblObservacionPorDefecto, 0, 6);
        pnlEmpresa.Controls.Add(txtObservacionPorDefecto, 1, 6);

        mainLayout.Controls.Add(pnlEmpresa, 0, 1);

        // Panel Derecho: Boleta y Logo
        var pnlDerecho = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        pnlDerecho.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        pnlDerecho.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // GroupBox Boleta
        grpBoleta = new GroupBox
        {
            Text = "Numeración de Boleta",
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        var pnlBoleta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        pnlBoleta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        pnlBoleta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlBoleta.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        pnlBoleta.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lblSiguienteBoleta = new Label { Text = "Siguiente:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        numSiguienteBoleta = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = ReglasParametros.SiguienteNumeroBoletaMinimo,
            Maximum = ReglasParametros.SiguienteNumeroBoletaMaximo,
            ThousandsSeparator = false
        };
        pnlBoleta.Controls.Add(lblSiguienteBoleta, 0, 0);
        pnlBoleta.Controls.Add(numSiguienteBoleta, 1, 0);

        lblUltimaBoleta = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Aún no hay boletas emitidas"
        };
        pnlBoleta.SetColumnSpan(lblUltimaBoleta, 2);
        pnlBoleta.Controls.Add(lblUltimaBoleta, 0, 1);
        grpBoleta.Controls.Add(pnlBoleta);

        pnlDerecho.Controls.Add(grpBoleta, 0, 0);

        // GroupBox Logo
        grpLogo = new GroupBox
        {
            Text = "Logo de la Empresa",
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        var pnlLogo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        pnlLogo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pnlLogo.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        picLogo = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        pnlLogo.Controls.Add(picLogo, 0, 0);

        var pnlBotonesLogo = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        btnCambiarLogo = new Button
        {
            Text = "Cambiar...",
            Size = new Size(95, 30)
        };
        btnCambiarLogo.Click += (_, _) => SeleccionarLogo();

        btnQuitarLogo = new Button
        {
            Text = "Quitar",
            Size = new Size(80, 30)
        };
        btnQuitarLogo.Click += (_, _) => QuitarLogo();

        pnlBotonesLogo.Controls.Add(btnCambiarLogo);
        pnlBotonesLogo.Controls.Add(btnQuitarLogo);
        pnlLogo.Controls.Add(pnlBotonesLogo, 0, 1);

        grpLogo.Controls.Add(pnlLogo);
        pnlDerecho.Controls.Add(grpLogo, 0, 1);

        mainLayout.Controls.Add(pnlDerecho, 1, 1);

        // Panel de botones inferiores
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 5, 0, 0)
        };

        btnCancelar = new Button
        {
            Text = "Cancelar",
            Size = new Size(95, 34),
            DialogResult = DialogResult.Cancel
        };

        btnGuardar = new Button
        {
            Text = "Guardar",
            Size = new Size(95, 34)
        };
        btnGuardar.Click += async (_, _) => await GuardarAsync();

        buttonPanel.Controls.Add(btnCancelar);
        buttonPanel.Controls.Add(btnGuardar);
        mainLayout.SetColumnSpan(buttonPanel, 2);
        mainLayout.Controls.Add(buttonPanel, 0, 2);

        Controls.Add(mainLayout);

        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;

        ResumeLayout(false);
    }

    private async Task CargarAsync()
    {
        var dto = await _servicioParametros.ObtenerAsync();

        txtNombreEmpresa.Text = dto.NombreEmpresa;
        txtDireccion.Text = dto.Direccion;
        txtCiudad.Text = dto.Ciudad;
        txtTelefono.Text = dto.Telefono;
        txtRegistroSanitario.Text = dto.RegistroSanitario;
        txtTelefonosReclamos.Text = dto.TelefonosReclamos;
        txtObservacionPorDefecto.Text = dto.ObservacionPorDefecto;

        var boletaValida = Math.Clamp(dto.SiguienteNumeroBoleta, ReglasParametros.SiguienteNumeroBoletaMinimo, ReglasParametros.SiguienteNumeroBoletaMaximo);
        numSiguienteBoleta.Value = boletaValida;
        _siguienteNumeroBoletaOriginal = boletaValida;

        lblUltimaBoleta.Text = dto.UltimoNumeroBoletaEmitido.HasValue
            ? $"Último número emitido: {dto.UltimoNumeroBoletaEmitido.Value}"
            : "Aún no hay boletas emitidas";

        _accionLogo = AccionLogo.SinCambio;
        _rutaLogoNueva = null;
        CargarVistaPreviaLogo(dto.RutaLogoCompleta);
    }

    private void SeleccionarLogo()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Seleccionar logo de la empresa",
            Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            RestoreDirectory = true
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _rutaLogoNueva = ofd.FileName;
            _accionLogo = AccionLogo.Reemplazar;
            CargarVistaPreviaLogo(_rutaLogoNueva);
        }
    }

    private void QuitarLogo()
    {
        _accionLogo = AccionLogo.Quitar;
        _rutaLogoNueva = null;
        picLogo.Image?.Dispose();
        picLogo.Image = null;
    }

    private void CargarVistaPreviaLogo(string? ruta)
    {
        picLogo.Image?.Dispose();
        picLogo.Image = null;

        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            return;
        }

        try
        {
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var original = Image.FromStream(fs);
            picLogo.Image = new Bitmap(original);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
        {
            picLogo.Image = null;
        }
    }

    private async Task GuardarAsync()
    {
        var nuevoSiguiente = (long)numSiguienteBoleta.Value;
        if (nuevoSiguiente != _siguienteNumeroBoletaOriginal)
        {
            var confirmacion = MessageBox.Show(
                this,
                $"Cambiará el siguiente número de boleta de {_siguienteNumeroBoletaOriginal} a {nuevoSiguiente}. Esta acción afecta la numeración oficial. ¿Continuar?",
                "Confirmación de numeración",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmacion != DialogResult.Yes)
            {
                return;
            }
        }

        await GuardarInternoAsync();
    }

    private Task GuardarInternoAsync()
    {
        return EjecutarAsync(async () =>
        {
            try
            {
                var dto = new ActualizarParametros(
                    txtNombreEmpresa.Text,
                    txtDireccion.Text,
                    txtTelefono.Text,
                    txtCiudad.Text,
                    txtRegistroSanitario.Text,
                    txtTelefonosReclamos.Text,
                    txtObservacionPorDefecto.Text,
                    (long)numSiguienteBoleta.Value,
                    _accionLogo,
                    _rutaLogoNueva);

                await _servicioParametros.ActualizarAsync(dto);

                MessageBox.Show(
                    this,
                    "Parámetros guardados.",
                    "Éxito",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
            }
        }, btnGuardar);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            picLogo?.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    internal Task CargarParaTestAsync() => CargarAsync();
    internal Task GuardarSinConfirmarParaTestAsync() => GuardarInternoAsync();

    internal TextBox TxtNombreEmpresa => txtNombreEmpresa;
    internal TextBox TxtDireccion => txtDireccion;
    internal TextBox TxtCiudad => txtCiudad;
    internal TextBox TxtTelefono => txtTelefono;
    internal TextBox TxtRegistroSanitario => txtRegistroSanitario;
    internal TextBox TxtTelefonosReclamos => txtTelefonosReclamos;
    internal TextBox TxtObservacionPorDefecto => txtObservacionPorDefecto;
    internal NumericUpDown NumSiguienteBoleta => numSiguienteBoleta;
    internal Label LblUltimaBoleta => lblUltimaBoleta;
    internal PictureBox PicLogo => picLogo;
    internal Button BtnCambiarLogo => btnCambiarLogo;
    internal Button BtnQuitarLogo => btnQuitarLogo;
    internal Button BtnGuardar => btnGuardar;
    internal Button BtnCancelar => btnCancelar;
    internal ErrorProvider ProveedorError => _errorProvider;
    internal AccionLogo AccionLogoParaTest { get => _accionLogo; set => _accionLogo = value; }
    internal string? RutaLogoParaTest { get => _rutaLogoNueva; set => _rutaLogoNueva = value; }
}

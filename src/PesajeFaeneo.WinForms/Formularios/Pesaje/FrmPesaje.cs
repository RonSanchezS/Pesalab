using System.Globalization;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Impresion;
using PesajeFaeneo.WinForms.Infraestructura;
using PesajeFaeneo.WinForms.Presentacion.Pesaje;

namespace PesajeFaeneo.WinForms.Formularios.Pesaje;

public class FrmPesaje : FormBase
{
    private readonly IServicioPesajes _servicioPesajes;
    private readonly IServicioGanaderos _servicioGanaderos;
    private readonly IServicioClientes _servicioClientes;
    private readonly IServicioParametros _servicioParametros;
    private readonly IServicioBalanza _servicioBalanza;
    private readonly ISimuladorBalanza _simuladorBalanza;
    private readonly ISesionActual _sesionActual;
    private readonly IFormFactory _formFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IServicioImpresionBoletas _servicioImpresion;
    private readonly MonitorBalanzaUi _monitor;
    private readonly ControlCapturas _capturas;
    private readonly bool _puedeUsarSimulador;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private int _versionSugerencia;
    private string _observacionPorDefecto = string.Empty;
    private bool _grabando;
    private bool _cargando;
    private bool _imprimiendo;
    private string? _mensajePostGrabado;
    private NivelMensajeGuia _nivelPostGrabado;

    // Display
    internal Panel pnlDisplay = null!;
    internal Label lblPesoVivo = null!;
    internal Label lblEstable = null!;
    internal Label lblEstadoBalanza = null!;
    internal Label lblSimulador = null!;
    internal Label lblProximaBoleta = null!;

    // Datos
    internal ControlBusquedaCatalogo ctlGanadero = null!;
    internal NumericUpDown numSecuencia = null!;
    internal Label lblInfoSecuencia = null!;
    internal ControlBusquedaCatalogo ctlCliente = null!;
    internal TextBox txtDireccion = null!;
    internal TextBox txtColorRaza = null!;
    internal ComboBox cmbSexo = null!;
    internal ComboBox cmbDesposte = null!;
    internal TextBox txtObservacion = null!;

    // Pesos
    internal TextBox txtPesoIzquierdo = null!;
    internal TextBox txtPesoDerecho = null!;
    internal TextBox txtPesoTotal = null!;
    internal Button btnCapturarIzquierdo = null!;
    internal Button btnCapturarDerecho = null!;
    internal Button btnBorrarPesos = null!;

    // Acciones y Guía
    internal Button btnGrabar = null!;
    internal Button btnImprimir = null!;
    internal Button btnCerrar = null!;
    internal Label lblGuia = null!;

    // Simulador
    internal GroupBox grpSimulador = null!;
    internal NumericUpDown numPesoSimulado = null!;
    internal Button btnSimularCarga = null!;
    internal Button btnSimularVaciar = null!;

    // Grilla
    internal DataGridView dgvHoy = null!;
    internal ErrorProvider _errorProvider = null!;

    // Ganchos para tests
    internal bool ConfirmarCierre { get; set; } = true;
    internal Func<Form, DialogResult>? MostrarDialogoParaTest { get; set; }
    internal EstadoUiPesaje? EstadoUiActual { get; private set; }
    internal ControlCapturas CapturasParaTest => _capturas;

    public FrmPesaje(
        IServicioPesajes servicioPesajes,
        IServicioGanaderos servicioGanaderos,
        IServicioClientes servicioClientes,
        IServicioParametros servicioParametros,
        IServicioBalanza servicioBalanza,
        ISimuladorBalanza simuladorBalanza,
        ISesionActual sesionActual,
        IFormFactory formFactory,
        TimeProvider timeProvider,
        IServicioImpresionBoletas servicioImpresion)
    {
        _servicioPesajes = servicioPesajes ?? throw new ArgumentNullException(nameof(servicioPesajes));
        _servicioGanaderos = servicioGanaderos ?? throw new ArgumentNullException(nameof(servicioGanaderos));
        _servicioClientes = servicioClientes ?? throw new ArgumentNullException(nameof(servicioClientes));
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));
        _servicioBalanza = servicioBalanza ?? throw new ArgumentNullException(nameof(servicioBalanza));
        _simuladorBalanza = simuladorBalanza ?? throw new ArgumentNullException(nameof(simuladorBalanza));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _servicioImpresion = servicioImpresion ?? throw new ArgumentNullException(nameof(servicioImpresion));

        _puedeUsarSimulador = _sesionActual.TienePermiso(Permiso.ConfigurarBalanza);
        _capturas = new ControlCapturas(_puedeUsarSimulador);
        _monitor = new MonitorBalanzaUi(_servicioBalanza);
        _monitor.Actualizado += (_, i) => AlActualizarBalanza(i);

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposPesaje.Ganadero, ctlGanadero },
            { CamposPesaje.Cliente, ctlCliente },
            { CamposPesaje.Direccion, txtDireccion },
            { CamposPesaje.Secuencia, numSecuencia },
            { CamposPesaje.ColorRaza, txtColorRaza },
            { CamposPesaje.Sexo, cmbSexo },
            { CamposPesaje.TipoDesposte, cmbDesposte },
            { CamposPesaje.PesoIzquierdo, txtPesoIzquierdo },
            { CamposPesaje.PesoDerecho, txtPesoDerecho },
            { CamposPesaje.Observacion, txtObservacion }
        };

        var puedeAltaRapida = _sesionActual.TienePermiso(Permiso.AltaRapidaCatalogos);
        ctlGanadero.PermitirAltaRapida = puedeAltaRapida;
        ctlCliente.PermitirAltaRapida = puedeAltaRapida;

        ctlGanadero.ElementoSeleccionado += (_, e) =>
        {
            _mensajePostGrabado = null;
            _ = ActualizarSugerenciaAsync(e.Id);
            ActualizarEstadoUi();
        };

        ctlGanadero.SeleccionBorrada += (_, _) =>
        {
            _mensajePostGrabado = null;
            lblInfoSecuencia.Text = string.Empty;
            ActualizarEstadoUi();
        };

        ctlGanadero.AltaRapidaSolicitada += async (_, texto) => await AltaRapidaGanaderoAsync(texto);

        ctlCliente.ElementoSeleccionado += (_, e) =>
        {
            _mensajePostGrabado = null;
            txtDireccion.Text = e.Detalle ?? string.Empty;
            ActualizarEstadoUi();
        };

        ctlCliente.SeleccionBorrada += (_, _) =>
        {
            _mensajePostGrabado = null;
            ActualizarEstadoUi();
        };

        ctlCliente.AltaRapidaSolicitada += async (_, texto) => await AltaRapidaClienteAsync(texto);

        cmbDesposte.SelectedIndexChanged += (_, _) =>
        {
            _mensajePostGrabado = null;
            ActualizarEstadoUi();
        };

        Shown += async (_, _) =>
        {
            _monitor.Iniciar();
            await EjecutarAsync(CargarAsync);
        };
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Registro de pesaje";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1024, 700);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = false;

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // Display
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Guía
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Contenido principal

        // 1. Display
        pnlDisplay = ConstruirPanelDisplay();
        mainLayout.SetColumnSpan(pnlDisplay, 2);
        mainLayout.Controls.Add(pnlDisplay, 0, 0);

        // Guía
        lblGuia = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 0, 4)
        };
        AplicarColorGuia(NivelMensajeGuia.Info);
        mainLayout.SetColumnSpan(lblGuia, 2);
        mainLayout.Controls.Add(lblGuia, 0, 1);

        // Panel Izquierdo: Datos, Pesos, Acciones
        var pnlIzquierdo = ConstruirPanelIzquierdo();
        mainLayout.Controls.Add(pnlIzquierdo, 0, 2);

        // Panel Derecho: Grilla y Simulador
        var pnlDerecho = ConstruirPanelDerecho();
        mainLayout.Controls.Add(pnlDerecho, 1, 2);

        Controls.Add(mainLayout);
        ResumeLayout(false);
    }

    private Panel ConstruirPanelDisplay()
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        lblPesoVivo = new Label
        {
            Text = "---- kg",
            Font = new Font("Segoe UI", 48F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        };

        lblEstable = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            TextAlign = ContentAlignment.TopRight,
            Dock = DockStyle.Bottom,
            Height = 25
        };

        lblEstadoBalanza = new Label
        {
            Text = "Balanza: detenida",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            TextAlign = ContentAlignment.BottomLeft,
            Dock = DockStyle.Bottom,
            Height = 25
        };

        lblSimulador = new Label
        {
            Text = "MODO SIMULADOR — los pesos no son reales",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.DarkOrange,
            BackColor = PresentacionBalanza.ColorFondoSimulador,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 24,
            Visible = false
        };

        lblProximaBoleta = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.TopLeft,
            Dock = DockStyle.Top,
            Height = 24
        };

        pnl.Controls.Add(lblPesoVivo);
        pnl.Controls.Add(lblEstable);
        pnl.Controls.Add(lblEstadoBalanza);
        pnl.Controls.Add(lblSimulador);
        pnl.Controls.Add(lblProximaBoleta);

        return pnl;
    }

    private Control ConstruirPanelIzquierdo()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 12,
            Padding = new Padding(0, 6, 12, 6)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Ganadero
        var lblG = new Label { Text = "Ganadero:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        ctlGanadero = new ControlBusquedaCatalogo { Dock = DockStyle.Fill, TabIndex = 0 };
        layout.Controls.Add(lblG, 0, 0);
        layout.Controls.Add(ctlGanadero, 1, 0);

        // Secuencia
        var lblSec = new Label { Text = "Secuencia:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        var pnlSec = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        numSecuencia = new NumericUpDown
        {
            Minimum = ReglasPesaje.SecuenciaMinima,
            Maximum = ReglasPesaje.SecuenciaMaxima,
            Value = 1,
            Width = 90,
            TabIndex = 1
        };
        lblInfoSecuencia = new Label { Text = string.Empty, AutoSize = true, Padding = new Padding(6, 4, 0, 0) };
        pnlSec.Controls.Add(numSecuencia);
        pnlSec.Controls.Add(lblInfoSecuencia);
        layout.Controls.Add(lblSec, 0, 1);
        layout.Controls.Add(pnlSec, 1, 1);

        // Cliente
        var lblC = new Label { Text = "Cliente:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        ctlCliente = new ControlBusquedaCatalogo { Dock = DockStyle.Fill, TabIndex = 2 };
        layout.Controls.Add(lblC, 0, 2);
        layout.Controls.Add(ctlCliente, 1, 2);

        // Dirección
        var lblDir = new Label { Text = "Dirección:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtDireccion = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasPesaje.LongitudMaximaDireccion,
            CharacterCasing = CharacterCasing.Upper,
            TabIndex = 3
        };
        layout.Controls.Add(lblDir, 0, 3);
        layout.Controls.Add(txtDireccion, 1, 3);

        // Color/Raza
        var lblCol = new Label { Text = "Color/Raza:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtColorRaza = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasPesaje.LongitudMaximaColorRaza,
            CharacterCasing = CharacterCasing.Upper,
            TabIndex = 4
        };
        layout.Controls.Add(lblCol, 0, 4);
        layout.Controls.Add(txtColorRaza, 1, 4);

        // Sexo
        var lblSex = new Label { Text = "Sexo:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        cmbSexo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            TabIndex = 5
        };
        cmbSexo.Items.Add("M - MACHO");
        cmbSexo.Items.Add("H - HEMBRA");
        cmbSexo.SelectedIndex = 0;
        layout.Controls.Add(lblSex, 0, 5);
        layout.Controls.Add(cmbSexo, 1, 5);

        // Tipo Desposte
        var lblDesp = new Label { Text = "Desposte:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        cmbDesposte = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            TabIndex = 6
        };
        cmbDesposte.Items.Add("RES COMPLETA");
        cmbDesposte.Items.Add("MEDIA RES");
        cmbDesposte.SelectedIndex = 0;
        layout.Controls.Add(lblDesp, 0, 6);
        layout.Controls.Add(cmbDesposte, 1, 6);

        // Observación
        var lblObs = new Label { Text = "Observación:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtObservacion = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasPesaje.LongitudMaximaObservacion,
            CharacterCasing = CharacterCasing.Upper,
            TabIndex = 7
        };
        layout.Controls.Add(lblObs, 0, 7);
        layout.Controls.Add(txtObservacion, 1, 7);

        // Separador / Panel Pesos
        var pnlPesos = ConstruirPanelPesos();
        layout.SetColumnSpan(pnlPesos, 2);
        layout.Controls.Add(pnlPesos, 0, 8);

        // Botones de acción
        var pnlBotones = ConstruirPanelAcciones();
        layout.SetColumnSpan(pnlBotones, 2);
        layout.Controls.Add(pnlBotones, 0, 9);

        scroll.Controls.Add(layout);
        return scroll;
    }

    private Control ConstruirPanelPesos()
    {
        var grp = new GroupBox
        {
            Text = "Pesos Capturados",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

        var fontPeso = new Font("Segoe UI", 16F, FontStyle.Bold);

        var lblI = new Label { Text = "Izquierdo (kg):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        var lblD = new Label { Text = "Derecho (kg):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        var lblT = new Label { Text = "Total (kg):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };

        txtPesoIzquierdo = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false,
            TextAlign = HorizontalAlignment.Center,
            Font = fontPeso
        };
        txtPesoDerecho = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false,
            TextAlign = HorizontalAlignment.Center,
            Font = fontPeso
        };
        txtPesoTotal = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false,
            TextAlign = HorizontalAlignment.Center,
            Font = fontPeso,
            BackColor = Color.FromArgb(240, 248, 255)
        };

        btnCapturarIzquierdo = new Button
        {
            Text = "Capturar izq. (F5)",
            Dock = DockStyle.Fill,
            Height = 36,
            TabStop = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnCapturarIzquierdo.Click += (_, _) => CapturarLado(LadoPesaje.Izquierdo);

        btnCapturarDerecho = new Button
        {
            Text = "Capturar der. (F6)",
            Dock = DockStyle.Fill,
            Height = 36,
            TabStop = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnCapturarDerecho.Click += (_, _) => CapturarLado(LadoPesaje.Derecho);

        btnBorrarPesos = new Button
        {
            Text = "Borrar pesos (F8)",
            Dock = DockStyle.Fill,
            Height = 36,
            TabStop = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnBorrarPesos.Click += (_, _) => BorrarPesos();

        layout.Controls.Add(lblI, 0, 0);
        layout.Controls.Add(lblD, 1, 0);
        layout.Controls.Add(lblT, 2, 0);

        layout.Controls.Add(txtPesoIzquierdo, 0, 1);
        layout.Controls.Add(txtPesoDerecho, 1, 1);
        layout.Controls.Add(txtPesoTotal, 2, 1);

        layout.Controls.Add(btnCapturarIzquierdo, 0, 2);
        layout.Controls.Add(btnCapturarDerecho, 1, 2);
        layout.Controls.Add(btnBorrarPesos, 2, 2);

        grp.Controls.Add(layout);
        return grp;
    }

    private Panel ConstruirPanelAcciones()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0),
            AutoSize = true
        };

        btnCerrar = new Button
        {
            Text = "Cerrar (Esc)",
            Size = new Size(120, 42),
            TabStop = false,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular)
        };
        btnCerrar.Click += (_, _) => Close();

        btnImprimir = new Button
        {
            Text = "Imprimir (F9)",
            Size = new Size(140, 42),
            TabStop = false,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            Enabled = false
        };
        btnImprimir.Click += async (_, _) => await ImprimirSeleccionadaAsync();

        btnGrabar = new Button
        {
            Text = "&Grabar (F12)",
            Size = new Size(150, 42),
            TabStop = false,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.FromArgb(46, 139, 87),
            ForeColor = Color.White
        };
        btnGrabar.Click += async (_, _) => await GrabarAsync();

        pnl.Controls.Add(btnCerrar);
        pnl.Controls.Add(btnImprimir);
        pnl.Controls.Add(btnGrabar);

        return pnl;
    }

    private Control ConstruirPanelDerecho()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grilla
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Simulador

        // Grilla hoy
        dgvHoy = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            TabStop = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        dgvHoy.SelectionChanged += (_, _) => ActualizarEstadoUi();

        dgvHoy.Columns.Add("Boleta", "Boleta");
        dgvHoy.Columns.Add("Hora", "Hora");
        dgvHoy.Columns.Add("Marca", "Marca");
        dgvHoy.Columns.Add("Sec", "Sec.");
        dgvHoy.Columns.Add("Cliente", "Cliente");
        dgvHoy.Columns.Add("Desposte", "Desposte");
        dgvHoy.Columns.Add("Izq", "Izq.");
        dgvHoy.Columns.Add("Der", "Der.");
        dgvHoy.Columns.Add("Total", "Total");
        dgvHoy.Columns.Add("Origen", "Origen");
        dgvHoy.Columns.Add("Estado", "Estado");

        layout.Controls.Add(dgvHoy, 0, 0);

        // Simulador
        grpSimulador = new GroupBox
        {
            Text = "Panel de Simulación de Balanza (Admin)",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Visible = false
        };

        var pnlSim = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6)
        };

        numPesoSimulado = new NumericUpDown
        {
            Minimum = 0,
            Maximum = ReglasBalanza.PesoMaximoSimuladorKg,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = 120.0m,
            Width = 90,
            TabStop = false
        };

        btnSimularCarga = new Button
        {
            Text = "Cargar",
            Width = 80,
            TabStop = false
        };
        btnSimularCarga.Click += (_, _) => _simuladorBalanza.EstablecerPesoObjetivo(numPesoSimulado.Value);

        btnSimularVaciar = new Button
        {
            Text = "Vaciar",
            Width = 80,
            TabStop = false
        };
        btnSimularVaciar.Click += (_, _) => _simuladorBalanza.EstablecerPesoObjetivo(0m);

        pnlSim.Controls.Add(new Label { Text = "Peso kg:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
        pnlSim.Controls.Add(numPesoSimulado);
        pnlSim.Controls.Add(btnSimularCarga);
        pnlSim.Controls.Add(btnSimularVaciar);

        grpSimulador.Controls.Add(pnlSim);
        layout.Controls.Add(grpSimulador, 0, 1);

        return layout;
    }

    private TipoDesposte TipoSeleccionado =>
        cmbDesposte.SelectedIndex == 1 ? TipoDesposte.MediaRes : TipoDesposte.ResCompleta;

    private char SexoSeleccionado =>
        cmbSexo.SelectedIndex == 1 ? ReglasPesaje.SexoHembra : ReglasPesaje.SexoMacho;

    private void AplicarColorGuia(NivelMensajeGuia nivel)
    {
        lblGuia.BackColor = nivel switch
        {
            NivelMensajeGuia.Info => SystemColors.Info,
            NivelMensajeGuia.Exito => Color.FromArgb(220, 255, 220),
            NivelMensajeGuia.Advertencia => Color.FromArgb(255, 255, 200),
            NivelMensajeGuia.Error => Color.FromArgb(255, 220, 220),
            _ => SystemColors.Control
        };
        lblGuia.ForeColor = Color.Black;
    }

    private async Task CargarAsync()
    {
        _cargando = true;
        try
        {
            var ganaderos = await _servicioGanaderos.ListarParaBusquedaAsync();
            ctlGanadero.EstablecerElementos(ganaderos);

            var clientes = await _servicioClientes.ListarParaBusquedaAsync();
            ctlCliente.EstablecerElementos(clientes);

            var parametros = await _servicioParametros.ObtenerAsync();
            _observacionPorDefecto = parametros.ObservacionPorDefecto ?? string.Empty;
            txtObservacion.Text = _observacionPorDefecto;
            lblProximaBoleta.Text = $"Próxima boleta: {parametros.SiguienteNumeroBoleta}";

            await CargarGrillaHoyAsync();

            ActiveControl = ctlGanadero;
        }
        finally
        {
            _cargando = false;
            ActualizarEstadoUi();
        }
    }

    private async Task CargarGrillaHoyAsync()
    {
        var pesajes = await _servicioPesajes.ListarDelDiaAsync();
        dgvHoy.Rows.Clear();
        var zonaLocal = _timeProvider.LocalTimeZone;
        foreach (var p in pesajes)
        {
            var localHora = TimeZoneInfo.ConvertTimeFromUtc(p.FechaHoraUtc, zonaLocal).ToString("HH:mm");
            var origenStr = p.Origen == OrigenPeso.Simulador ? "SIMULADO" : string.Empty;
            var desposteStr = p.TipoDesposte == TipoDesposte.ResCompleta ? "RES COMPLETA" : "MEDIA RES";
            var izqStr = p.PesoIzquierdo > 0 ? p.PesoIzquierdo.ToString("0.0", CultureInfo.CurrentCulture) : "-";
            var derStr = p.PesoDerecho > 0 ? p.PesoDerecho.ToString("0.0", CultureInfo.CurrentCulture) : "-";
            var totalStr = p.PesoTotal.ToString("0.0", CultureInfo.CurrentCulture);

            var i = dgvHoy.Rows.Add(
                p.NumeroBoleta,
                localHora,
                p.Marca,
                p.Secuencia,
                p.NombreCliente,
                desposteStr,
                izqStr,
                derStr,
                totalStr,
                origenStr,
                p.Estado.ToString()
            );
            dgvHoy.Rows[i].Tag = p.Id;
        }
    }

    private int? IdBoletaParaImprimir()
    {
        var fila = dgvHoy.CurrentRow ?? (dgvHoy.Rows.Count > 0 ? dgvHoy.Rows[0] : null);
        return fila?.Tag as int?;
    }

    private async Task ActualizarSugerenciaAsync(int ganaderoId)
    {
        var version = ++_versionSugerencia;
        try
        {
            var s = await _servicioPesajes.SugerirSecuenciaAsync(ganaderoId);
            if (IsDisposed || version != _versionSugerencia || ctlGanadero.Seleccion?.Id != ganaderoId)
            {
                return;
            }
            numSecuencia.Value = s.Siguiente;
            lblInfoSecuencia.Text = $"Hoy: {s.PesajesVigentesHoy} pesaje(s) de este ganadero";
        }
        catch (Exception)
        {
            if (!IsDisposed)
            {
                lblInfoSecuencia.Text = "No se pudo sugerir la secuencia; ingrésela manualmente.";
            }
        }
    }

    private void AlActualizarBalanza(InstantaneaBalanza i)
    {
        if (IsDisposed) return;

        _capturas.Observar(i);

        lblPesoVivo.Text = PresentacionBalanza.TextoPeso(i);
        lblPesoVivo.ForeColor = PresentacionBalanza.ColorEstado(i);

        if (i.Estado == EstadoBalanza.Conectada && i.PesoKg.HasValue)
        {
            lblEstable.Text = i.Estable ? "ESTABLE" : "INESTABLE";
            lblEstable.ForeColor = i.Estable ? PresentacionBalanza.ColorVerdeOscuro : PresentacionBalanza.ColorGris;
        }
        else
        {
            lblEstable.Text = string.Empty;
        }

        lblEstadoBalanza.Text = PresentacionBalanza.TextoEstado(i);
        lblSimulador.Visible = i.EsSimulador;
        grpSimulador.Visible = _puedeUsarSimulador && i.EsSimulador;

        ActualizarEstadoUi();
    }

    private void CapturarLado(LadoPesaje lado)
    {
        var i = _servicioBalanza.Instantanea;
        _capturas.Observar(i);
        var motivo = _capturas.Capturar(lado, TipoSeleccionado, i, _timeProvider.GetUtcNow());
        if (motivo != MotivoRechazoCaptura.Ninguno)
        {
            try { System.Media.SystemSounds.Beep.Play(); } catch { }
            ActualizarEstadoUi();
            return;
        }

        _mensajePostGrabado = null;
        MostrarPesos();
        ActualizarEstadoUi();
    }

    private void BorrarPesos()
    {
        _capturas.BorrarPesos();
        _mensajePostGrabado = null;
        MostrarPesos();
        ActualizarEstadoUi();
    }

    private void MostrarPesos()
    {
        txtPesoIzquierdo.Text = _capturas.PesoIzquierdo.HasValue
            ? _capturas.PesoIzquierdo.Value.ToString("0.0", CultureInfo.CurrentCulture)
            : string.Empty;

        txtPesoDerecho.Text = _capturas.PesoDerecho.HasValue
            ? _capturas.PesoDerecho.Value.ToString("0.0", CultureInfo.CurrentCulture)
            : string.Empty;

        if (_capturas.TienePesos)
        {
            var total = ReglasPesaje.CalcularTotal(
                _capturas.PesoIzquierdo ?? 0m,
                _capturas.PesoDerecho ?? 0m);
            txtPesoTotal.Text = total.ToString("0.0", CultureInfo.CurrentCulture);
        }
        else
        {
            txtPesoTotal.Text = string.Empty;
        }
    }

    private void ActualizarEstadoUi()
    {
        if (IsDisposed) return;

        var i = _servicioBalanza.Instantanea;
        var ahora = _timeProvider.GetUtcNow();
        var ocupado = _grabando || _cargando || _imprimiendo;

        var datos = new DatosPantallaPesaje(
            Instantanea: i,
            Ahora: ahora,
            GanaderoSeleccionado: ctlGanadero.Seleccion is not null,
            ClienteSeleccionado: ctlCliente.Seleccion is not null,
            TipoDesposte: TipoSeleccionado,
            Capturas: _capturas,
            PuedeUsarSimulador: _puedeUsarSimulador,
            Ocupado: ocupado);

        var estado = EvaluadorPantallaPesaje.Evaluar(datos);
        EstadoUiActual = estado;

        btnCapturarIzquierdo.Enabled = estado.PuedeCapturarIzquierdo;
        btnCapturarDerecho.Enabled = estado.PuedeCapturarDerecho;
        btnBorrarPesos.Enabled = estado.PuedeBorrarPesos;
        btnGrabar.Enabled = estado.PuedeGrabar;
        btnImprimir.Enabled = !ocupado && IdBoletaParaImprimir() is not null;

        if (_mensajePostGrabado is not null)
        {
            lblGuia.Text = _mensajePostGrabado;
            AplicarColorGuia(_nivelPostGrabado);
        }
        else
        {
            lblGuia.Text = estado.Mensaje;
            AplicarColorGuia(estado.Nivel);
        }
    }

    private Control ControlEnfocado()
    {
        Control c = ActiveControl ?? this;
        while (c is ContainerControl cc && cc.ActiveControl is not null)
        {
            c = cc.ActiveControl;
        }
        return c;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var enfocado = ControlEnfocado();

        switch (keyData)
        {
            case Keys.F5:
                if (btnCapturarIzquierdo.Enabled)
                {
                    CapturarLado(LadoPesaje.Izquierdo);
                }
                return true;

            case Keys.F6:
                if (btnCapturarDerecho.Enabled)
                {
                    CapturarLado(LadoPesaje.Derecho);
                }
                return true;

            case Keys.F8:
                if (btnBorrarPesos.Enabled)
                {
                    BorrarPesos();
                }
                return true;

            case Keys.F9:
                if (btnImprimir.Enabled)
                {
                    _ = ImprimirSeleccionadaAsync();
                }
                return true;

            case Keys.F12:
                if (btnGrabar.Enabled)
                {
                    _ = GrabarAsync();
                }
                return true;

            case Keys.Escape:
                if (enfocado is ComboBox cbEsc && cbEsc.DroppedDown)
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }
                Close();
                return true;

            case Keys.Enter:
                if (enfocado is Button)
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }
                if (enfocado is ComboBox cbEnter && cbEnter.DroppedDown)
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }
                SelectNextControl(enfocado, true, true, true, true);
                return true;

            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private async Task GrabarAsync()
    {
        PesajeRegistrado? registrado = null;
        _grabando = true;
        ActualizarEstadoUi();
        try
        {
            await EjecutarAsync(async () =>
            {
                try
                {
                    registrado = await _servicioPesajes.RegistrarAsync(LeerDto(), CancellationToken.None);
                }
                catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
                {
                }
            }, btnGrabar);
        }
        finally
        {
            _grabando = false;
        }

        if (registrado is not null)
        {
            AplicarPostGrabado(registrado);
            await ImprimirTrasGrabarAsync(registrado);
            await EjecutarAsync(RecargarTrasGrabarAsync);
            if (ctlGanadero.Seleccion != null)
            {
                await ActualizarSugerenciaAsync(ctlGanadero.Seleccion.Id);
            }
        }

        ActualizarEstadoUi();
    }

    private static string TextoBaseGrabado(PesajeRegistrado r)
    {
        var extra = r.Origen == OrigenPeso.Simulador ? " (SIMULADO)" : string.Empty;
        var totalStr = r.PesoTotal.ToString("0.0", CultureInfo.CurrentCulture);
        return $"Boleta N° {r.NumeroBoleta} grabada — {totalStr} kg{extra}";
    }

    private async Task ImprimirTrasGrabarAsync(PesajeRegistrado r)
    {
        ResultadoImpresion? resultado = null;
        _imprimiendo = true;
        ActualizarEstadoUi();
        try
        {
            await EjecutarAsync(async () =>
                resultado = await _servicioImpresion.ImprimirAsync(r.Id, SolicitudImpresion.AutomaticaAlGrabar));
        }
        finally
        {
            _imprimiendo = false;
        }
        AplicarResultadoImpresionTrasGrabar(r, resultado);
    }

    private void AplicarResultadoImpresionTrasGrabar(PesajeRegistrado r, ResultadoImpresion? resultado)
    {
        var baseTxt = TextoBaseGrabado(r);
        if (resultado is null)
        {
            _mensajePostGrabado = $"Boleta N° {r.NumeroBoleta} grabada, pero NO se imprimió. Pulse F9 para reintentar.";
            _nivelPostGrabado = NivelMensajeGuia.Error;
            return;
        }

        switch (resultado.Estado)
        {
            case EstadoImpresion.Impresa:
                _mensajePostGrabado = $"{baseTxt} — impresa.";
                _nivelPostGrabado = NivelMensajeGuia.Exito;
                break;

            case EstadoImpresion.Omitida:
                _mensajePostGrabado = $"{baseTxt} — pulse F9 para imprimir.";
                _nivelPostGrabado = NivelMensajeGuia.Exito;
                break;

            case EstadoImpresion.Fallida:
                _mensajePostGrabado = $"Boleta N° {r.NumeroBoleta} grabada, pero NO se imprimió: {resultado.Mensaje} Pulse F9 para reintentar.";
                _nivelPostGrabado = NivelMensajeGuia.Error;
                try { System.Media.SystemSounds.Beep.Play(); } catch { }
                break;
        }
    }

    private async Task ImprimirSeleccionadaAsync()
    {
        var id = IdBoletaParaImprimir();
        if (id is null) { try { System.Media.SystemSounds.Beep.Play(); } catch { } return; }
        ResultadoImpresion? resultado = null;
        _imprimiendo = true;
        ActualizarEstadoUi();
        try
        {
            await EjecutarAsync(async () =>
                resultado = await _servicioImpresion.ImprimirAsync(id.Value, SolicitudImpresion.Manual), btnImprimir);
        }
        finally
        {
            _imprimiendo = false;
        }
        if (resultado is not null)
        {
            _mensajePostGrabado = resultado.Mensaje;
            _nivelPostGrabado = resultado.Estado == EstadoImpresion.Impresa ? NivelMensajeGuia.Exito : NivelMensajeGuia.Error;
        }
        ActualizarEstadoUi();
    }

    private RegistrarPesaje LeerDto()
    {
        var ganaderoId = ctlGanadero.Seleccion?.Id ?? 0;
        var clienteId = ctlCliente.Seleccion?.Id ?? 0;
        var secuencia = (int)numSecuencia.Value;
        var sexo = SexoSeleccionado;
        var tipo = TipoSeleccionado;
        var pesoIzq = _capturas.PesoIzquierdo ?? 0m;
        var pesoDer = _capturas.PesoDerecho ?? 0m;

        return new RegistrarPesaje(
            GanaderoId: ganaderoId,
            ClienteId: clienteId,
            Direccion: txtDireccion.Text,
            Secuencia: secuencia,
            ColorRaza: txtColorRaza.Text,
            Sexo: sexo,
            TipoDesposte: tipo,
            PesoIzquierdo: pesoIzq,
            PesoDerecho: pesoDer,
            Observacion: txtObservacion.Text,
            CapturadoConSimulador: _capturas.CapturadoConSimulador);
    }

    private void AplicarPostGrabado(PesajeRegistrado r)
    {
        _capturas.ReiniciarTrasGrabar();
        MostrarPesos();
        txtColorRaza.Clear();
        txtObservacion.Text = _observacionPorDefecto;
        _errorProvider.Clear();

        _mensajePostGrabado = TextoBaseGrabado(r);
        _nivelPostGrabado = NivelMensajeGuia.Exito;

        try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        txtColorRaza.Focus();

        OnPesajeGrabado(r);
    }

    private async Task RecargarTrasGrabarAsync()
    {
        _cargando = true;
        try
        {
            await CargarGrillaHoyAsync();
            var parametros = await _servicioParametros.ObtenerAsync();
            lblProximaBoleta.Text = $"Próxima boleta: {parametros.SiguienteNumeroBoleta}";
        }
        finally
        {
            _cargando = false;
        }
    }

    protected virtual void OnPesajeGrabado(PesajeRegistrado r)
    {
    }

    private async Task AltaRapidaGanaderoAsync(string texto)
    {
        int? idNuevo = null;
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmGanaderoEdicion>();
            frm.ConfigurarAltaRapida(texto);
            var resultado = MostrarDialogoParaTest != null
                ? MostrarDialogoParaTest(frm)
                : frm.ShowDialog(this);
            if (resultado == DialogResult.OK)
            {
                idNuevo = frm.IdGuardado;
            }
        });

        if (idNuevo is null) return;

        await EjecutarAsync(async () =>
        {
            ctlGanadero.EstablecerElementos(await _servicioGanaderos.ListarParaBusquedaAsync());
        });
        ctlGanadero.Seleccionar(idNuevo.Value);
        ctlGanadero.Focus();
    }

    private async Task AltaRapidaClienteAsync(string texto)
    {
        int? idNuevo = null;
        int codigoSugerido = 1;
        await EjecutarAsync(async () =>
        {
            codigoSugerido = await _servicioClientes.SugerirSiguienteCodigoAsync();
        });

        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmClienteEdicion>();
            frm.ConfigurarAltaRapida(codigoSugerido, texto);
            var resultado = MostrarDialogoParaTest != null
                ? MostrarDialogoParaTest(frm)
                : frm.ShowDialog(this);
            if (resultado == DialogResult.OK)
            {
                idNuevo = frm.IdGuardado;
            }
        });

        if (idNuevo is null) return;

        await EjecutarAsync(async () =>
        {
            ctlCliente.EstablecerElementos(await _servicioClientes.ListarParaBusquedaAsync());
        });
        ctlCliente.Seleccionar(idNuevo.Value);
        ctlCliente.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_grabando || _imprimiendo)
        {
            e.Cancel = true;
            return;
        }

        if (_capturas.TienePesos && ConfirmarCierre)
        {
            var respuesta = MessageBox.Show(
                this,
                "Hay pesos capturados sin grabar. ¿Desea cerrar de todos modos?",
                "Confirmar cierre",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (respuesta != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor.Dispose();
        }
        base.Dispose(disposing);
    }

    // Ganchos para tests
    internal Task CargarParaTestAsync() => CargarAsync();
    internal void SondearBalanzaParaTest() => _monitor.Sondear();
    internal void CapturarParaTest(LadoPesaje lado) => CapturarLado(lado);
    internal Task GrabarParaTestAsync() => GrabarAsync();
    internal Task ImprimirParaTestAsync() => ImprimirSeleccionadaAsync();
    internal int? IdBoletaParaImprimirParaTest => IdBoletaParaImprimir();
    internal void SimularFormClosingParaTest(FormClosingEventArgs e) => OnFormClosing(e);
    internal bool ProcesarTeclaParaTest(Keys keyData)
    {
        Message msg = default;
        return ProcessCmdKey(ref msg, keyData);
    }
}

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
    private readonly IServicioFleteros _servicioFleteros;
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
    private ModoDesposte _modoDesposte = ModoDesposte.Ambos;
    private bool _imprimiendo;
    private int? _ultimoPesajeGrabadoId;
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
    internal ControlBusquedaCatalogo ctlFletero = null!;
    internal TextBox txtColorRaza = null!;
    internal ComboBox cmbSexo = null!;
    internal ComboBox cmbDesposte = null!;
    internal Label lblDesposte = null!;
    internal TextBox txtObservacion = null!;

    // Pesos
    internal TextBox txtPesoIzquierdo = null!;
    internal TextBox txtPesoDerecho = null!;
    internal TextBox txtPesoTotal = null!;
    internal Button btnCapturarIzquierdo = null!;
    internal Button btnCapturarDerecho = null!;
    internal Button btnBorrarPesaje = null!;

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

    // Validación
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
        IServicioFleteros servicioFleteros,
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
        _servicioFleteros = servicioFleteros ?? throw new ArgumentNullException(nameof(servicioFleteros));
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
            { CamposPesaje.Fletero, ctlFletero },
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

        // F12 (E1): un fletero escrito y no elegido bloquea el grabado; la guía lo dice en cuanto se escribe.
        ctlFletero.CajaTexto.TextChanged += (_, _) =>
        {
            if (ctlFletero.TextoSinElegir)
            {
                _mensajePostGrabado = null;
            }
            ActualizarEstadoUi();
        };

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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, AltoDisplay)); // Display
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

        // Panel Derecho: pesos capturados, acciones y simulador
        var pnlDerecho = ConstruirPanelDerecho();
        mainLayout.Controls.Add(pnlDerecho, 1, 2);

        Controls.Add(mainLayout);
        ResumeLayout(false);
    }

    // Alto del display a 96 DPI (se escala con el formulario). Debe alojar la franja del simulador y, debajo,
    // una línea completa del peso en vivo (48 pt ≈ 85 px) más "ESTABLE": un Label que no puede mostrar la
    // línea entera no dibuja nada.
    private const int AltoDisplay = 150;

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
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Bottom,
            Height = 28
        };

        lblEstadoBalanza = new Label
        {
            Text = "Balanza: detenida",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            TextAlign = ContentAlignment.BottomLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 0, 0, 4)
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
            Height = 28,
            Padding = new Padding(4, 4, 0, 0)
        };

        // Izquierda: próxima boleta arriba y estado de la balanza abajo. Derecha: peso en vivo y "ESTABLE".
        // Van en columnas para que las franjas de texto no le quiten alto al peso.
        var pnlInfo = new Panel { Dock = DockStyle.Fill };
        pnlInfo.Controls.Add(lblEstadoBalanza);
        pnlInfo.Controls.Add(lblProximaBoleta);

        var pnlPeso = new Panel { Dock = DockStyle.Fill };
        pnlPeso.Controls.Add(lblPesoVivo);
        pnlPeso.Controls.Add(lblEstable);

        var columnas = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        columnas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        columnas.Controls.Add(pnlInfo, 0, 0);
        columnas.Controls.Add(pnlPeso, 1, 0);

        pnl.Controls.Add(columnas);
        pnl.Controls.Add(lblSimulador);

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
            RowCount = 9,
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

        // Fletero (F10, O5): opcional; se elige al pesar y la boleta imprime solo este fletero.
        var lblFle = new Label { Text = "Fletero:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        ctlFletero = new ControlBusquedaCatalogo
        {
            Dock = DockStyle.Fill,
            TabIndex = 4,
            Placeholder = "Sin fletero (opcional)"
        };
        layout.Controls.Add(lblFle, 0, 4);
        layout.Controls.Add(ctlFletero, 1, 4);

        // Color/Raza
        var lblCol = new Label { Text = "Color/Raza:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtColorRaza = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasPesaje.LongitudMaximaColorRaza,
            CharacterCasing = CharacterCasing.Upper,
            TabIndex = 5
        };
        layout.Controls.Add(lblCol, 0, 5);
        layout.Controls.Add(txtColorRaza, 1, 5);

        // Sexo
        var lblSex = new Label { Text = "Sexo:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        cmbSexo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            TabIndex = 6
        };
        cmbSexo.Items.Add("M - MACHO");
        cmbSexo.Items.Add("H - HEMBRA");
        cmbSexo.SelectedIndex = 0;
        layout.Controls.Add(lblSex, 0, 6);
        layout.Controls.Add(cmbSexo, 1, 6);

        // Tipo Desposte
        lblDesposte = new Label { Text = "Desposte:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        cmbDesposte = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            TabIndex = 7
        };
        cmbDesposte.Items.Add("RES COMPLETA");
        cmbDesposte.Items.Add("MEDIA RES");
        cmbDesposte.SelectedIndex = 0;
        layout.Controls.Add(lblDesposte, 0, 7);
        layout.Controls.Add(cmbDesposte, 1, 7);

        // Observación
        var lblObs = new Label { Text = "Observación:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        txtObservacion = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasPesaje.LongitudMaximaObservacion,
            CharacterCasing = CharacterCasing.Upper,
            TabIndex = 8
        };
        layout.Controls.Add(lblObs, 0, 8);
        layout.Controls.Add(txtObservacion, 1, 8);

        scroll.Controls.Add(layout);
        return scroll;
    }

    private Control ConstruirPanelPesos()
    {
        var grp = new GroupBox
        {
            Text = "Pesos Capturados",
            Dock = DockStyle.Fill,
            // Sin AutoSize el GroupBox conserva su alto por defecto (100 px) dentro de la fila AutoSize y
            // recorta los pesos y los botones de captura.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
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

        btnBorrarPesaje = new Button
        {
            Text = "Borrar pesaje (F8)",
            Dock = DockStyle.Fill,
            Height = 36,
            TabStop = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnBorrarPesaje.Click += (_, _) => BorrarPesaje();

        layout.Controls.Add(lblI, 0, 0);
        layout.Controls.Add(lblD, 1, 0);
        layout.Controls.Add(lblT, 2, 0);

        layout.Controls.Add(txtPesoIzquierdo, 0, 1);
        layout.Controls.Add(txtPesoDerecho, 1, 1);
        layout.Controls.Add(txtPesoTotal, 2, 1);

        layout.Controls.Add(btnCapturarIzquierdo, 0, 2);
        layout.Controls.Add(btnCapturarDerecho, 1, 2);
        layout.Controls.Add(btnBorrarPesaje, 2, 2);

        grp.Controls.Add(layout);
        return grp;
    }

    private static readonly Color ColorGrabarHabilitado = Color.FromArgb(46, 139, 87);

    private void AplicarColoresGrabar()
    {
        btnGrabar.BackColor = btnGrabar.Enabled ? ColorGrabarHabilitado : SystemColors.Control;
        btnGrabar.ForeColor = btnGrabar.Enabled ? Color.White : SystemColors.GrayText;
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
            Text = "Imprimir último (F9)",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(185, 42),
            TabStop = false,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            Enabled = false
        };
        btnImprimir.Click += async (_, _) => await ImprimirUltimoAsync();

        btnGrabar = new Button
        {
            Text = "&Grabar (F12)",
            Size = new Size(150, 42),
            TabStop = false,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = ColorGrabarHabilitado,
            ForeColor = Color.White
        };
        btnGrabar.Click += async (_, _) => await GrabarAsync();
        // F12 (E2): WinForms no cambia el fondo de un botón deshabilitado; sin esto se ve verde aunque no responda.
        btnGrabar.EnabledChanged += (_, _) => AplicarColoresGrabar();

        pnl.Controls.Add(btnCerrar);
        pnl.Controls.Add(btnImprimir);
        pnl.Controls.Add(btnGrabar);

        return pnl;
    }

    private Control ConstruirPanelDerecho()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12, 6, 0, 6)
        };
        // Sin estilo la columna se dimensiona por su contenido: al escalar por DPI crece y deja de caber en el panel.
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Pesos capturados y acciones (antes compartían el panel izquierdo con los datos; el historial del día
        // pasó a su propio formulario, FrmHistorialPesajes, y esa columna quedó libre para ellos).
        var pnlPesos = ConstruirPanelPesos();
        layout.Controls.Add(pnlPesos, 0, 0);

        var pnlBotones = ConstruirPanelAcciones();
        layout.Controls.Add(pnlBotones, 0, 1);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); // Simulador (solo Administrador con balanza simulada)


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
        layout.Controls.Add(grpSimulador, 0, 2);

        scroll.Controls.Add(layout);
        return scroll;
    }

    private TipoDesposte TipoSeleccionado =>
        cmbDesposte.SelectedIndex == 1 ? TipoDesposte.MediaRes : TipoDesposte.ResCompleta;

    internal ModoDesposte ModoDesposteActual => _modoDesposte;

    /// <summary>
    /// F11 (D8): con un modo de un solo tipo, la fila "Desposte" se oculta (la fila AutoSize colapsa) y el combo queda
    /// fijo en ese tipo; con Ambos se elige en cada pesaje como siempre.
    /// </summary>
    private void AplicarModoDesposte(ModoDesposte modo)
    {
        _modoDesposte = modo;
        var unico = ReglasDesposte.TipoUnico(modo);
        if (unico is { } tipo)
        {
            cmbDesposte.SelectedIndex = tipo == TipoDesposte.MediaRes ? 1 : 0;
        }

        lblDesposte.Visible = unico is null;
        cmbDesposte.Visible = unico is null;
    }

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

            ctlFletero.EstablecerElementos(await _servicioFleteros.ListarParaBusquedaAsync());

            var parametros = await _servicioParametros.ObtenerAsync();
            _observacionPorDefecto = parametros.ObservacionPorDefecto ?? string.Empty;
            txtObservacion.Text = _observacionPorDefecto;
            lblProximaBoleta.Text = $"Próxima boleta: {parametros.SiguienteNumeroBoleta}";
            AplicarModoDesposte(await _servicioParametros.ObtenerModoDesposteAsync());

            ActiveControl = ctlGanadero;
        }
        finally
        {
            _cargando = false;
            ActualizarEstadoUi();
        }
    }

    // F9 imprime el último pesaje grabado en esta pantalla (ya no hay grilla con filas seleccionables):
    // para cualquier otro, el historial de pesajes o la reimpresión por número.
    private int? IdBoletaParaImprimir() => _ultimoPesajeGrabadoId;

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

    private void BorrarPesaje()
    {
        _capturas.BorrarPesaje();
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
            Ocupado: ocupado,
            DesposteFijo: ReglasDesposte.TipoUnico(_modoDesposte) is not null,
            FleteroSinElegir: ctlFletero.TextoSinElegir);

        var estado = EvaluadorPantallaPesaje.Evaluar(datos);
        EstadoUiActual = estado;

        btnCapturarIzquierdo.Enabled = estado.PuedeCapturarIzquierdo;
        btnCapturarDerecho.Enabled = estado.PuedeCapturarDerecho;
        btnBorrarPesaje.Enabled = estado.PuedeBorrarPesaje;
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
                if (btnBorrarPesaje.Enabled)
                {
                    BorrarPesaje();
                }
                return true;

            case Keys.F9:
                if (btnImprimir.Enabled)
                {
                    _ = ImprimirUltimoAsync();
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

    private async Task ImprimirUltimoAsync()
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
            FleteroId: ctlFletero.Seleccion?.Id,
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
        _ultimoPesajeGrabadoId = r.Id;
        _capturas.ReiniciarTrasGrabar();
        MostrarPesos();
        txtColorRaza.Clear();
        ctlFletero.Limpiar(); // el fletero cambia de un animal a otro: se elige en cada pesaje
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
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmClienteEdicion>();
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
    internal Task ImprimirParaTestAsync() => ImprimirUltimoAsync();
    internal int? IdBoletaParaImprimirParaTest => IdBoletaParaImprimir();
    internal void SimularFormClosingParaTest(FormClosingEventArgs e) => OnFormClosing(e);
    internal bool ProcesarTeclaParaTest(Keys keyData)
    {
        Message msg = default;
        return ProcessCmdKey(ref msg, keyData);
    }
}

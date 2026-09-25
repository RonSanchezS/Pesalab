using System.Drawing;
using System.Globalization;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Administracion;

public class FrmConfiguracionBalanza : FormBase
{
    private readonly IServicioConfiguracionBalanza _servicioConfig;
    private readonly IServicioBalanza _servicioBalanza;
    private readonly ISimuladorBalanza _simulador;
    private readonly MonitorBalanzaUi _monitor;
    private readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private ConfiguracionBalanzaDto? _configuracionCargada;
    private CancellationTokenSource? _ctsPrueba;
    private bool _pruebaEnCurso;
    private bool _cerrarAlTerminar;

    // Controles internos accesibles para pruebas
    internal ComboBox cmbTipoConexion = null!;
    internal GroupBox grpConexion = null!;
    internal GroupBox grpSerie = null!;
    internal ComboBox cmbPuerto = null!;
    internal Button btnRefrescarPuertos = null!;
    internal ComboBox cmbBaudios = null!;
    internal ComboBox cmbBitsDatos = null!;
    internal ComboBox cmbParidad = null!;
    internal ComboBox cmbBitsParada = null!;
    internal GroupBox grpRed = null!;
    internal TextBox txtHost = null!;
    internal NumericUpDown numPuertoTcp = null!;

    internal GroupBox grpLectura = null!;
    internal TextBox txtPatron = null!;
    internal Label lblAyudaPatron = null!;
    internal NumericUpDown numLecturas = null!;
    internal NumericUpDown numTolerancia = null!;
    internal NumericUpDown numTimeout = null!;
    internal ComboBox cmbDelimitador = null!;
    internal ComboBox cmbUnidad = null!;

    internal GroupBox grpProbarPatron = null!;
    internal TextBox txtTramaEjemplo = null!;
    internal Button btnProbarPatron = null!;
    internal Label lblResultadoPatron = null!;

    internal GroupBox grpDiagnostico = null!;
    internal Button btnProbarConexion = null!;
    internal Button btnCancelarPrueba = null!;
    internal Label lblEstadoPrueba = null!;
    internal Label lblPesoPrueba = null!;
    internal ListBox lstTramas = null!;

    internal GroupBox grpBalanzaEnUso = null!;
    internal Label lblEstadoActual = null!;
    internal Label lblPesoActual = null!;
    internal Button btnReconectar = null!;

    internal GroupBox grpSimulador = null!;
    internal NumericUpDown numPesoObjetivo = null!;
    internal Button btnAplicarPeso = null!;
    internal Button btnCargaAleatoria = null!;
    internal Button btnVaciar = null!;
    internal CheckBox chkDesconexion = null!;

    internal Button btnGuardar = null!;
    internal Button btnCerrar = null!;

    internal bool ConfirmarCierre { get; set; } = true;
    internal bool EstaPruebaEnCurso => _pruebaEnCurso;
    internal bool PruebaEnCursoParaTest { get => _pruebaEnCurso; set => _pruebaEnCurso = value; }
    internal ErrorProvider ProveedorError => _errorProvider;

    public FrmConfiguracionBalanza(
        IServicioConfiguracionBalanza servicioConfig,
        IServicioBalanza servicioBalanza,
        ISimuladorBalanza simulador)
    {
        _servicioConfig = servicioConfig ?? throw new ArgumentNullException(nameof(servicioConfig));
        _servicioBalanza = servicioBalanza ?? throw new ArgumentNullException(nameof(servicioBalanza));
        _simulador = simulador ?? throw new ArgumentNullException(nameof(simulador));

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposBalanza.TipoConexion, cmbTipoConexion },
            { CamposBalanza.PuertoCom, cmbPuerto },
            { CamposBalanza.BaudRate, cmbBaudios },
            { CamposBalanza.DataBits, cmbBitsDatos },
            { CamposBalanza.Paridad, cmbParidad },
            { CamposBalanza.BitsParada, cmbBitsParada },
            { CamposBalanza.HostTcp, txtHost },
            { CamposBalanza.PuertoTcp, numPuertoTcp },
            { CamposBalanza.PatronRegex, txtPatron },
            { CamposBalanza.LecturasParaEstable, numLecturas },
            { CamposBalanza.ToleranciaKg, numTolerancia },
            { CamposBalanza.TimeoutMs, numTimeout },
            { CamposBalanza.DelimitadorTrama, cmbDelimitador },
            { CamposBalanza.UnidadPeso, cmbUnidad }
        };

        _monitor = new MonitorBalanzaUi(_servicioBalanza);
        _monitor.Actualizado += (_, inst) => ActualizarEstadoBalanzaEnUso(inst);

        Shown += async (_, _) => await EjecutarAsync(CargarAsync);
        Shown += (_, _) => _monitor.Iniciar();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        // Tamaños expresados a 96 DPI; AutoScaleMode.Dpi los escala a la escala real de Windows (125%, 150%…).
        // El resto del layout se dimensiona solo a partir de las fuentes (TableLayoutPanel + AutoSize).
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = "Configuración y Diagnóstico de Balanza";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 720);
        MinimumSize = new Size(900, 560);

        var panelPrincipal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10)
        };
        panelPrincipal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
        panelPrincipal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        panelPrincipal.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panelPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // --- Columna izquierda: desplazamiento solo vertical (el contenido sigue el ancho de la columna) ---
        var desplazamientoIzquierdo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0, 0, 8, 0) };
        var columnaIzquierda = CrearTabla(new ColumnStyle(SizeType.Percent, 100F));
        columnaIzquierda.Dock = DockStyle.Top;
        desplazamientoIzquierdo.Controls.Add(columnaIzquierda);

        // 1. Conexión
        cmbTipoConexion = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTipoConexion.Items.Add(new ItemTipoConexion("Serie (RS-232 / USB)", TipoConexion.Serial));
        cmbTipoConexion.Items.Add(new ItemTipoConexion("Red (TCP/IP)", TipoConexion.Tcp));
        cmbTipoConexion.Items.Add(new ItemTipoConexion("Simulador", TipoConexion.Simulador));
        cmbTipoConexion.SelectedIndexChanged += (_, _) => ActualizarHabilitacionSubgrupos();

        cmbPuerto = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        btnRefrescarPuertos = CrearBoton("Refrescar");
        btnRefrescarPuertos.Click += (_, _) => RefrescarPuertosClick();
        var celdaPuerto = CrearTabla(new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
        AgregarFila(celdaPuerto, cmbPuerto, btnRefrescarPuertos);

        cmbBaudios = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var b in ReglasBalanza.BaudiosPermitidos) cmbBaudios.Items.Add(b);

        cmbBitsDatos = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var bd in ReglasBalanza.BitsDatosPermitidos) cmbBitsDatos.Items.Add(bd);

        cmbParidad = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbParidad.Items.Add(new ItemEnum<ParidadSerial>("Ninguna", ParidadSerial.None));
        cmbParidad.Items.Add(new ItemEnum<ParidadSerial>("Impar", ParidadSerial.Odd));
        cmbParidad.Items.Add(new ItemEnum<ParidadSerial>("Par", ParidadSerial.Even));
        cmbParidad.Items.Add(new ItemEnum<ParidadSerial>("Marca", ParidadSerial.Mark));
        cmbParidad.Items.Add(new ItemEnum<ParidadSerial>("Espacio", ParidadSerial.Space));

        cmbBitsParada = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbBitsParada.Items.Add(new ItemEnum<BitsParada>("1", BitsParada.One));
        cmbBitsParada.Items.Add(new ItemEnum<BitsParada>("2", BitsParada.Two));

        var tablaSerie = CrearTabla(
            new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 55F),
            new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 45F));
        AgregarFila(tablaSerie, CrearEtiqueta("Puerto:"), celdaPuerto, CrearEtiqueta("Baudios:"), cmbBaudios);
        AgregarFila(tablaSerie, CrearEtiqueta("Bits de datos:"), cmbBitsDatos, CrearEtiqueta("Paridad:"), cmbParidad);
        AgregarFila(tablaSerie, CrearEtiqueta("Bits de parada:"), cmbBitsParada);
        grpSerie = CrearGrupo("Parámetros serie", tablaSerie);

        txtHost = new TextBox { Dock = DockStyle.Fill };
        numPuertoTcp = new NumericUpDown { Anchor = AnchorStyles.Left, Width = 90, MinimumSize = new Size(90, 0), Minimum = 1, Maximum = 65535, Value = 4001 };
        var tablaRed = CrearTabla(
            new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.AutoSize));
        AgregarFila(tablaRed, CrearEtiqueta("Host / IP:"), txtHost, CrearEtiqueta("Puerto:"), numPuertoTcp);
        grpRed = CrearGrupo("Parámetros de red TCP/IP", tablaRed);

        var tablaConexion = CrearTabla(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
        AgregarFila(tablaConexion, CrearEtiqueta("Tipo:"), cmbTipoConexion);
        AgregarFilaCompleta(tablaConexion, grpSerie);
        AgregarFilaCompleta(tablaConexion, grpRed);
        grpConexion = CrearGrupo("Tipo de conexión y parámetros de puerto", tablaConexion);

        // 2. Lectura
        txtPatron = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9.5F) };
        lblAyudaPatron = new Label
        {
            Text = "Debe incluir (?<peso>...). Opcional: (?<estable>...) si la balanza indica estabilidad.",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.DimGray,
            Font = new Font(Font.FontFamily, 8.25F)
        };
        numLecturas = new NumericUpDown { Anchor = AnchorStyles.Left, Width = 90, MinimumSize = new Size(90, 0), Minimum = 1, Maximum = 50, Value = 5 };
        numTolerancia = new NumericUpDown { Anchor = AnchorStyles.Left, Width = 90, MinimumSize = new Size(90, 0), Minimum = 0, Maximum = 10, DecimalPlaces = 1, Increment = 0.1M, Value = 0.5M };
        numTimeout = new NumericUpDown { Anchor = AnchorStyles.Left, Width = 90, MinimumSize = new Size(90, 0), Minimum = 500, Maximum = 60000, Increment = 500, Value = 3000 };

        cmbDelimitador = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbDelimitador.Items.Add(new ItemEnum<DelimitadorTrama>("Fin de línea (CR/LF)", DelimitadorTrama.FinDeLinea));
        cmbDelimitador.Items.Add(new ItemEnum<DelimitadorTrama>("Espacio", DelimitadorTrama.Espacio));
        cmbDelimitador.Items.Add(new ItemEnum<DelimitadorTrama>("Inicio con '=' (RX8 / XK3190)", DelimitadorTrama.SignoIgual));

        cmbUnidad = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbUnidad.Items.Add(new ItemEnum<UnidadPesoTrama>("Kilogramos (kg)", UnidadPesoTrama.Kilogramos));
        cmbUnidad.Items.Add(new ItemEnum<UnidadPesoTrama>("Gramos (g)", UnidadPesoTrama.Gramos));

        var tablaLectura = CrearTabla(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
        AgregarFila(tablaLectura, CrearEtiqueta("Patrón (regex):"), txtPatron);
        AgregarFila(tablaLectura, null, lblAyudaPatron);
        AgregarFila(tablaLectura, CrearEtiqueta("Separación de tramas:"), cmbDelimitador);
        AgregarFila(tablaLectura, CrearEtiqueta("Unidad que transmite:"), cmbUnidad);
        AgregarFila(tablaLectura, CrearEtiqueta("Lecturas para estabilidad:"), numLecturas);
        AgregarFila(tablaLectura, CrearEtiqueta("Tolerancia (kg):"), numTolerancia);
        AgregarFila(tablaLectura, CrearEtiqueta("Tiempo sin datos (ms):"), numTimeout);
        grpLectura = CrearGrupo("Lectura y detección de estabilidad", tablaLectura);

        // 3. Probar patrón
        txtTramaEjemplo = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9F) };
        btnProbarPatron = CrearBoton("Probar");
        btnProbarPatron.Click += (_, _) => ProbarPatronClick();
        lblResultadoPatron = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold)
        };
        var tablaProbar = CrearTabla(
            new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
        AgregarFila(tablaProbar, CrearEtiqueta("Trama de ejemplo:"), txtTramaEjemplo, btnProbarPatron);
        AgregarFilaCompleta(tablaProbar, lblResultadoPatron);
        grpProbarPatron = CrearGrupo("Probar patrón con una trama de ejemplo (sin conexión)", tablaProbar);

        AgregarFila(columnaIzquierda, grpConexion);
        AgregarFila(columnaIzquierda, grpLectura);
        AgregarFila(columnaIzquierda, grpProbarPatron);

        // --- Columna derecha ---
        var columnaDerecha = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
        columnaDerecha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        columnaDerecha.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        columnaDerecha.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        columnaDerecha.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // 4. Balanza en uso
        lblEstadoActual = new Label
        {
            Text = "Balanza: verificando…",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
        };
        lblPesoActual = new Label
        {
            Text = "---- kg",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold)
        };
        btnReconectar = CrearBoton("Reconectar");
        btnReconectar.Anchor = AnchorStyles.Right;
        btnReconectar.Click += async (_, _) => await EjecutarAsync(() => _servicioBalanza.ReiniciarAsync(), btnReconectar);
        var tablaEnUso = CrearTabla(new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
        AgregarFilaCompleta(tablaEnUso, lblEstadoActual);
        AgregarFila(tablaEnUso, lblPesoActual, btnReconectar);
        grpBalanzaEnUso = CrearGrupo("Balanza en uso (servicio compartido)", tablaEnUso);

        // 5. Simulador
        numPesoObjetivo = new NumericUpDown
        {
            Anchor = AnchorStyles.Left,
            Width = 100,
            MinimumSize = new Size(100, 0),
            Minimum = 0,
            Maximum = ReglasBalanza.PesoMaximoSimuladorKg,
            DecimalPlaces = 1,
            Increment = 1M
        };
        btnAplicarPeso = CrearBoton("Aplicar");
        btnAplicarPeso.Click += (_, _) => AplicarPesoClick();
        btnCargaAleatoria = CrearBoton("Simular carga");
        btnCargaAleatoria.Click += (_, _) => CargaAleatoriaClick();
        btnVaciar = CrearBoton("Vaciar");
        btnVaciar.Click += (_, _) => VaciarClick();
        chkDesconexion = new CheckBox
        {
            Text = "Simular desconexión física de la balanza",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = PresentacionBalanza.ColorRojoOscuro
        };
        chkDesconexion.CheckedChanged += ChkDesconexion_CheckedChanged;

        var filaPesoObjetivo = CrearFlujo();
        filaPesoObjetivo.Controls.AddRange(new Control[] { CrearEtiqueta("Peso objetivo (kg):"), numPesoObjetivo, btnAplicarPeso, btnCargaAleatoria, btnVaciar });
        var tablaSimulador = CrearTabla(new ColumnStyle(SizeType.Percent, 100F));
        AgregarFila(tablaSimulador, filaPesoObjetivo);
        AgregarFila(tablaSimulador, chkDesconexion);
        grpSimulador = CrearGrupo("Control del modo simulador", tablaSimulador);

        // 6. Diagnóstico: ocupa el alto restante; la lista de tramas crece con la ventana.
        btnProbarConexion = CrearBoton("Probar conexión");
        btnProbarConexion.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        btnProbarConexion.Click += async (_, _) => await EjecutarAsync(ProbarConexionInternoAsync, btnProbarConexion);
        btnCancelarPrueba = CrearBoton("Cancelar");
        btnCancelarPrueba.Enabled = false;
        btnCancelarPrueba.Click += (_, _) => CancelarPruebaClick();
        var filaBotonesPrueba = CrearFlujo();
        filaBotonesPrueba.Controls.AddRange(new Control[] { btnProbarConexion, btnCancelarPrueba });

        lblEstadoPrueba = new Label
        {
            Text = "Listo para probar conexión.",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        lblPesoPrueba = new Label
        {
            Text = "---- kg",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold)
        };
        lstTramas = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            MinimumSize = new Size(0, 120),
            Font = new Font("Consolas", 8.5F)
        };

        var tablaDiagnostico = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
        tablaDiagnostico.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tablaDiagnostico.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tablaDiagnostico.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tablaDiagnostico.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tablaDiagnostico.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tablaDiagnostico.Controls.Add(filaBotonesPrueba, 0, 0);
        tablaDiagnostico.Controls.Add(lblEstadoPrueba, 0, 1);
        tablaDiagnostico.Controls.Add(lblPesoPrueba, 0, 2);
        tablaDiagnostico.Controls.Add(lstTramas, 0, 3);
        grpDiagnostico = new GroupBox
        {
            Text = "Diagnóstico en vivo de la configuración",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 8),
            Margin = new Padding(0)
        };
        grpDiagnostico.Controls.Add(tablaDiagnostico);

        columnaDerecha.Controls.Add(grpBalanzaEnUso, 0, 0);
        columnaDerecha.Controls.Add(grpSimulador, 0, 1);
        columnaDerecha.Controls.Add(grpDiagnostico, 0, 2);

        // --- Botones inferiores ---
        btnCerrar = CrearBoton("Cerrar");
        btnCerrar.DialogResult = DialogResult.Cancel;
        btnCerrar.Click += (_, _) => Close();
        btnGuardar = CrearBoton("Guardar");
        btnGuardar.Click += async (_, _) => await EjecutarAsync(GuardarInternoAsync, btnGuardar);

        var panelBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        panelBotones.Controls.AddRange(new Control[] { btnCerrar, btnGuardar });

        panelPrincipal.Controls.Add(desplazamientoIzquierdo, 0, 0);
        panelPrincipal.Controls.Add(columnaDerecha, 1, 0);
        panelPrincipal.Controls.Add(panelBotones, 0, 1);
        panelPrincipal.SetColumnSpan(panelBotones, 2);

        Controls.Add(panelPrincipal);

        AcceptButton = btnGuardar;
        CancelButton = btnCerrar;

        FormClosing += FrmConfiguracionBalanza_FormClosing;

        // Un GroupBox con AutoSize mide sus etiquetas sin límite de ancho (en un solo renglón), así que
        // un texto que luego se parte en dos renglones desbordaría el grupo. Fijar MaximumSize.Width al
        // ancho disponible hace que la medición y el dibujo coincidan.
        txtPatron.SizeChanged += (_, _) => FijarAnchoMaximo(lblAyudaPatron, txtPatron.Width);
        tablaProbar.SizeChanged += (_, _) => FijarAnchoMaximo(lblResultadoPatron, tablaProbar.ClientSize.Width - lblResultadoPatron.Margin.Horizontal);
        tablaEnUso.SizeChanged += (_, _) => FijarAnchoMaximo(lblEstadoActual, tablaEnUso.ClientSize.Width - lblEstadoActual.Margin.Horizontal);

        ResumeLayout(false);
        PerformLayout();
    }

    private static void FijarAnchoMaximo(Label etiqueta, int ancho)
    {
        var maximo = new Size(Math.Max(ancho, 50), 0);
        if (etiqueta.MaximumSize != maximo)
        {
            etiqueta.MaximumSize = maximo;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // En pantallas chicas o con escala alta, la ventana nunca excede el área de trabajo del monitor.
        var area = Screen.FromControl(this).WorkingArea;
        if (Width > area.Width || Height > area.Height)
        {
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(
                area.Left + ((area.Width - Width) / 2),
                area.Top + ((area.Height - Height) / 2));
        }
    }

    private static TableLayoutPanel CrearTabla(params ColumnStyle[] columnas)
    {
        var tabla = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = columnas.Length,
            Margin = new Padding(0)
        };
        foreach (var columna in columnas)
        {
            tabla.ColumnStyles.Add(columna);
        }
        return tabla;
    }

    private static void AgregarFila(TableLayoutPanel tabla, params Control?[] celdas)
    {
        int fila = tabla.RowCount;
        tabla.RowCount = fila + 1;
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (int columna = 0; columna < celdas.Length; columna++)
        {
            if (celdas[columna] is { } control)
            {
                tabla.Controls.Add(control, columna, fila);
            }
        }
    }

    private static void AgregarFilaCompleta(TableLayoutPanel tabla, Control control)
    {
        AgregarFila(tabla, control);
        tabla.SetColumnSpan(control, tabla.ColumnCount);
    }

    private static GroupBox CrearGrupo(string titulo, Control contenido)
    {
        var grupo = new GroupBox
        {
            Text = titulo,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 6, 8, 8),
            Margin = new Padding(0, 0, 0, 10)
        };
        contenido.Dock = DockStyle.Fill;
        grupo.Controls.Add(contenido);
        return grupo;
    }

    private static Label CrearEtiqueta(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        Anchor = AnchorStyles.Right,
        TextAlign = ContentAlignment.MiddleRight
    };

    private static Button CrearBoton(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowOnly,
        MinimumSize = new Size(90, 28),
        Padding = new Padding(6, 0, 6, 0)
    };

    private static FlowLayoutPanel CrearFlujo() => new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Margin = new Padding(0)
    };

    private void ActualizarHabilitacionSubgrupos()
    {
        var tipo = (cmbTipoConexion.SelectedItem as ItemTipoConexion)?.Tipo;
        grpSerie.Enabled = tipo == TipoConexion.Serial;
        grpRed.Enabled = tipo == TipoConexion.Tcp;
    }

    private void ActualizarHabilitacionSimulador()
    {
        // D16: el subgrupo siempre permanece visible; solo se deshabilita cuando la configuración
        // guardada no es Simulador, para no ocultar errores de forma invisible al usuario.
        grpSimulador.Enabled = _configuracionCargada?.TipoConexion == TipoConexion.Simulador;
        numPesoObjetivo.Value = Math.Clamp(_simulador.PesoObjetivoKg, 0m, ReglasBalanza.PesoMaximoSimuladorKg);
        chkDesconexion.Checked = _simulador.DesconexionSimulada;
    }

    private void ActualizarEstadoBalanzaEnUso(InstantaneaBalanza inst)
    {
        lblEstadoActual.Text = PresentacionBalanza.TextoEstado(inst);
        lblEstadoActual.ForeColor = PresentacionBalanza.ColorEstado(inst);
        lblPesoActual.Text = PresentacionBalanza.TextoPeso(inst);
        lblPesoActual.ForeColor = PresentacionBalanza.ColorEstado(inst);
    }

    private void PoblarPuertos(IReadOnlyList<string> puertos, string puertoSeleccionado)
    {
        cmbPuerto.Items.Clear();
        var conjunto = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in puertos)
        {
            if (conjunto.Add(p)) cmbPuerto.Items.Add(p);
        }

        if (!string.IsNullOrWhiteSpace(puertoSeleccionado) && conjunto.Add(puertoSeleccionado.Trim().ToUpperInvariant()))
        {
            cmbPuerto.Items.Add(puertoSeleccionado.Trim().ToUpperInvariant());
        }

        cmbPuerto.Text = puertoSeleccionado.Trim().ToUpperInvariant();
    }

    private void PoblarControles(ConfiguracionBalanzaDto dto)
    {
        foreach (ItemTipoConexion item in cmbTipoConexion.Items)
        {
            if (item.Tipo == dto.TipoConexion)
            {
                cmbTipoConexion.SelectedItem = item;
                break;
            }
        }

        cmbBaudios.SelectedItem = dto.BaudRate;
        cmbBitsDatos.SelectedItem = dto.DataBits;

        foreach (ItemEnum<ParidadSerial> item in cmbParidad.Items)
        {
            if (item.Valor == dto.Paridad)
            {
                cmbParidad.SelectedItem = item;
                break;
            }
        }

        foreach (ItemEnum<BitsParada> item in cmbBitsParada.Items)
        {
            if (item.Valor == dto.BitsParada)
            {
                cmbBitsParada.SelectedItem = item;
                break;
            }
        }

        txtHost.Text = dto.HostTcp;
        numPuertoTcp.Value = Math.Clamp(dto.PuertoTcp, 1, 65535);

        txtPatron.Text = dto.PatronRegex;
        numLecturas.Value = Math.Clamp(dto.LecturasParaEstable, 1, 50);
        numTolerancia.Value = Math.Clamp(dto.ToleranciaKg, 0m, 10m);
        numTimeout.Value = Math.Clamp(dto.TimeoutMs, 500, 60000);
        SeleccionarItem(cmbDelimitador, dto.DelimitadorTrama);
        SeleccionarItem(cmbUnidad, dto.UnidadPeso);
    }

    private static void SeleccionarItem<T>(ComboBox combo, T valor)
    {
        foreach (ItemEnum<T> item in combo.Items)
        {
            if (EqualityComparer<T>.Default.Equals(item.Valor, valor))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    internal ConfiguracionBalanzaDto LeerDtoDeControles()
    {
        var tipo = cmbTipoConexion.SelectedItem is ItemTipoConexion it ? it.Tipo : TipoConexion.Serial;
        int baudios = cmbBaudios.SelectedItem is int b ? b : 9600;
        int bitsDatos = cmbBitsDatos.SelectedItem is int bd ? bd : 8;
        var paridad = cmbParidad.SelectedItem is ItemEnum<ParidadSerial> ip ? ip.Valor : ParidadSerial.None;
        var parada = cmbBitsParada.SelectedItem is ItemEnum<BitsParada> is_ ? is_.Valor : BitsParada.One;
        var delimitador = cmbDelimitador.SelectedItem is ItemEnum<DelimitadorTrama> id ? id.Valor : DelimitadorTrama.FinDeLinea;
        var unidad = LeerUnidadSeleccionada();

        return new ConfiguracionBalanzaDto(
            TipoConexion: tipo,
            PuertoCom: cmbPuerto.Text,
            BaudRate: baudios,
            DataBits: bitsDatos,
            Paridad: paridad,
            BitsParada: parada,
            HostTcp: txtHost.Text,
            PuertoTcp: (int)numPuertoTcp.Value,
            PatronRegex: txtPatron.Text,
            LecturasParaEstable: (int)numLecturas.Value,
            ToleranciaKg: numTolerancia.Value,
            TimeoutMs: (int)numTimeout.Value,
            DelimitadorTrama: delimitador,
            UnidadPeso: unidad);
    }

    private UnidadPesoTrama LeerUnidadSeleccionada() =>
        cmbUnidad.SelectedItem is ItemEnum<UnidadPesoTrama> iu ? iu.Valor : UnidadPesoTrama.Kilogramos;

    private async Task CargarAsync()
    {
        var leida = await _servicioConfig.ObtenerAsync();
        var puertos = _servicioConfig.ListarPuertosSerie();

        _configuracionCargada = leida.Configuracion;
        PoblarPuertos(puertos, leida.Configuracion.PuertoCom);
        PoblarControles(leida.Configuracion);
        ActualizarHabilitacionSubgrupos();
        ActualizarHabilitacionSimulador();
    }

    private void RefrescarPuertosClick()
    {
        Ejecutar(() =>
        {
            var puertos = _servicioConfig.ListarPuertosSerie();
            PoblarPuertos(puertos, cmbPuerto.Text);
        });
    }

    private void ProbarPatronClick()
    {
        Ejecutar(() =>
        {
            string patron = txtPatron.Text;
            var error = ParserTramas.ValidarPatron(patron);
            if (error != null)
            {
                lblResultadoPatron.Text = $"Error: {error}";
                lblResultadoPatron.ForeColor = PresentacionBalanza.ColorRojoOscuro;
                return;
            }

            var parser = new ParserTramas(patron, LeerUnidadSeleccionada());
            var resultado = parser.Parsear(txtTramaEjemplo.Text);

            if (!resultado.Valida)
            {
                lblResultadoPatron.Text = $"Trama no reconocida: {resultado.Error}";
                lblResultadoPatron.ForeColor = PresentacionBalanza.ColorRojoOscuro;
                return;
            }

            string estableTexto = resultado.EstableReportado.HasValue
                ? (resultado.EstableReportado.Value ? "sí" : "no")
                : "no informado";

            lblResultadoPatron.Text = $"Peso: {resultado.PesoKg:0.0} kg — estable: {estableTexto}";
            lblResultadoPatron.ForeColor = PresentacionBalanza.ColorVerdeOscuro;
        });
    }

    private async Task GuardarInternoAsync()
    {
        _errorProvider.Clear();
        var dto = LeerDtoDeControles();

        try
        {
            bool cambio = await _servicioConfig.ActualizarAsync(dto);
            if (cambio)
            {
                _configuracionCargada = dto;
                MessageBox.Show(
                    this,
                    "Configuración guardada. La balanza se reinició con la nueva configuración.",
                    "Configuración de Balanza",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ActualizarHabilitacionSimulador();
            }
            else
            {
                MessageBox.Show(
                    this,
                    "No hay cambios para guardar.",
                    "Configuración de Balanza",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (ValidacionException ex)
        {
            MostrarErroresValidacion(ex);
        }
    }

    private async Task ProbarConexionInternoAsync()
    {
        _errorProvider.Clear();
        var dto = LeerDtoDeControles();

        _ctsPrueba = new CancellationTokenSource();
        _pruebaEnCurso = true;
        lstTramas.Items.Clear();

        grpConexion.Enabled = false;
        grpLectura.Enabled = false;
        btnGuardar.Enabled = false;
        btnProbarPatron.Enabled = false;
        btnReconectar.Enabled = false;
        btnCancelarPrueba.Enabled = true;
        lblEstadoPrueba.Text = "Probando…";
        lblPesoPrueba.Text = "---- kg";
        lblPesoPrueba.ForeColor = SystemColors.ControlText;

        ResultadoPruebaBalanza? resultado = null;
        try
        {
            var progreso = new Progress<AvancePruebaBalanza>(MostrarAvance);
            resultado = await _servicioBalanza.ProbarAsync(
                dto,
                TimeSpan.FromSeconds(ReglasBalanza.DuracionPruebaSegundos),
                progreso,
                _ctsPrueba.Token);
        }
        catch (ValidacionException ex)
        {
            MostrarErroresValidacion(ex);
        }
        catch (OperationCanceledException)
        {
            // Defensa adicional (D17): si la cancelación escapa sin traducirse a Cancelada = true
            // (p. ej. contención del turno interno del servicio), se trata igual como prueba cancelada
            // en vez de dejar la excepción sin manejar.
        }
        finally
        {
            _pruebaEnCurso = false;
            grpConexion.Enabled = true;
            grpLectura.Enabled = true;
            btnGuardar.Enabled = true;
            btnProbarPatron.Enabled = true;
            btnReconectar.Enabled = true;
            btnCancelarPrueba.Enabled = false;
            _ctsPrueba.Dispose();
            _ctsPrueba = null;
            ActualizarHabilitacionSubgrupos();
        }

        // D17: el cierre diferido se honra siempre, sin importar por cuál camino terminó la prueba.
        if (_cerrarAlTerminar)
        {
            Close();
            return;
        }

        if (resultado is null)
        {
            return;
        }

        if (resultado.Cancelada)
        {
            lblEstadoPrueba.Text = "Prueba cancelada.";
            lblEstadoPrueba.ForeColor = PresentacionBalanza.ColorGris;
        }
        else if (resultado.Conecto)
        {
            string estabilidad = resultado.AlcanzoEstabilidad ? "alcanzada" : "no alcanzada";
            string msg = $"Conexión correcta: {resultado.TramasValidas} de {resultado.TramasRecibidas} tramas reconocidas. Estabilidad: {estabilidad}.";
            if (resultado.TramasRecibidas > 0 && resultado.TramasValidas == 0)
            {
                msg += " Revise el patrón de lectura.";
            }
            lblEstadoPrueba.Text = msg;
            lblEstadoPrueba.ForeColor = resultado.AlcanzoEstabilidad ? PresentacionBalanza.ColorVerdeOscuro : SystemColors.ControlText;
        }
        else
        {
            lblEstadoPrueba.Text = $"No se pudo conectar: {resultado.Error}";
            lblEstadoPrueba.ForeColor = PresentacionBalanza.ColorRojoOscuro;
        }
    }

    private void CancelarPruebaClick()
    {
        _ctsPrueba?.Cancel();
    }

    private void MostrarAvance(AvancePruebaBalanza a)
    {
        if (!_pruebaEnCurso || IsDisposed) return;

        if (lstTramas.Items.Count >= 50)
        {
            lstTramas.Items.RemoveAt(0);
        }
        lstTramas.Items.Add(a.TramaVisible);
        lstTramas.TopIndex = lstTramas.Items.Count - 1;

        lblPesoPrueba.Text = a.Resultado.Valida && a.Resultado.PesoKg.HasValue
            ? $"{a.Resultado.PesoKg.Value.ToString("0.0", CultureInfo.CurrentCulture)} kg"
            : "---- kg";
        lblPesoPrueba.ForeColor = a.Estable ? PresentacionBalanza.ColorVerdeOscuro : SystemColors.ControlText;
    }

    private void AplicarPesoClick()
    {
        _simulador.EstablecerPesoObjetivo(numPesoObjetivo.Value);
    }

    private void CargaAleatoriaClick()
    {
        var random = new Random();
        decimal aleatorio = 80.0m + (decimal)random.NextDouble() * 80.0m;
        numPesoObjetivo.Value = Math.Round(aleatorio, 1);
        _simulador.EstablecerPesoObjetivo(numPesoObjetivo.Value);
    }

    private void VaciarClick()
    {
        numPesoObjetivo.Value = 0m;
        _simulador.EstablecerPesoObjetivo(0m);
    }

    private void ChkDesconexion_CheckedChanged(object? sender, EventArgs e)
    {
        _simulador.DesconexionSimulada = chkDesconexion.Checked;
    }

    private void FrmConfiguracionBalanza_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_pruebaEnCurso)
        {
            _ctsPrueba?.Cancel();
            _cerrarAlTerminar = true;
            e.Cancel = true;
            return;
        }

        if (DialogResult != DialogResult.OK && _configuracionCargada != null && ConfirmarCierre)
        {
            var actual = LeerDtoDeControles();
            if (actual != _configuracionCargada)
            {
                var res = MessageBox.Show(
                    this,
                    "Hay cambios sin guardar. ¿Desea cerrar de todos modos?",
                    "Configuración de Balanza",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }
    }

    private void MostrarErroresValidacion(ValidacionException ex)
    {
        _errorProvider.Clear();
        Control? primerControl = null;

        foreach (var error in ex.Errores)
        {
            if (_controlesPorCampo.TryGetValue(error.Campo, out var control))
            {
                _errorProvider.SetError(control, error.Mensaje);
                primerControl ??= control;
            }
        }

        primerControl?.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor.Dispose();
            _errorProvider.Dispose();
        }
        base.Dispose(disposing);
    }

    // Ganchos para pruebas unitarias
    internal Task CargarParaTestAsync() => CargarAsync();
    internal Task GuardarParaTestAsync() => GuardarInternoAsync();
    internal Task ProbarConexionParaTestAsync() => ProbarConexionInternoAsync();
    internal void CancelarPruebaParaTest() => CancelarPruebaClick();
    internal void ProbarPatronParaTest() => ProbarPatronClick();
    internal void MostrarAvanceParaTest(AvancePruebaBalanza a) => MostrarAvance(a);

    private record ItemTipoConexion(string Texto, TipoConexion Tipo)
    {
        public override string ToString() => Texto;
    }

    private record ItemEnum<T>(string Texto, T Valor)
    {
        public override string ToString() => Texto;
    }
}

using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.WinForms.Formularios.Ayuda;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Licencias;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
using PesajeFaeneo.WinForms.Formularios.PuestaEnMarcha;
using PesajeFaeneo.WinForms.Formularios.Reportes;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using PesajeFaeneo.WinForms.Infraestructura;
using Serilog;

namespace PesajeFaeneo.WinForms.Formularios;

public enum SolicitudCierreShell
{
    SalirAplicacion,
    CerrarSesion,
    ReiniciarAplicacion
}

public partial class FrmPrincipal : FormBase
{
    private readonly ISesionActual _sesion;
    private readonly IFormFactory _formFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IServicioParametros _servicioParametros;
    private readonly PesajeFaeneo.Application.Balanza.IServicioBalanza _servicioBalanza;
    private readonly MonitorBalanzaUi _monitorBalanza;
    private readonly IServicioLicencia _licencia;
    private readonly EventHandler<EstadoLicencia> _alCambiarLicencia;

    private MenuStrip menuStrip = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatusEmpresa = null!;
    private ToolStripStatusLabel lblStatusUsuario = null!;
    private ToolStripStatusLabel lblStatusRol = null!;
    private ToolStripStatusLabel lblStatusBalanza = null!;
    private ToolStripStatusLabel lblStatusLicencia = null!;
    private ToolStripMenuItem itemActivarLicencia = null!;
    private ToolStripMenuItem itemRegistrarPesaje = null!;
    private ToolStripMenuItem itemImportarLegado = null!;
    private ToolStripStatusLabel lblStatusFechaHora = null!;
    private ToolStripStatusLabel lblStatusVersion = null!;
    private System.Windows.Forms.Timer? _relojTimer;

    private const string TituloPorDefecto = "Capataz";
    private PictureBox picLogoInicio = null!;
    private Label lblEmpresaInicio = null!;
    private Label lblDetalleInicio = null!;
    private TarjetaAccion tarjetaPesaje = null!;
    internal Label lblAtajos = null!;
    private TarjetaAccion tarjetaTicket = null!;
    private TarjetaAccion tarjetaHistorial = null!;
    private string _descripcionPesajeOriginal = string.Empty;

    /// <summary>F13 (D4): texto de los controles de pesaje e importación cuando el sistema está en modo consulta.</summary>
    internal const string TextoModoConsulta = "Modo consulta: active el sistema en Ayuda › Activar licencia.";

    public SolicitudCierreShell Solicitud { get; private set; } = SolicitudCierreShell.SalirAplicacion;

    internal MenuStrip MenuPrincipal => menuStrip;
    internal ToolStripStatusLabel LblStatusEmpresa => lblStatusEmpresa;
    internal ToolStripStatusLabel LblStatusBalanza => lblStatusBalanza;
    internal ToolStripStatusLabel LblStatusLicencia => lblStatusLicencia;
    internal ToolStripMenuItem ItemActivarLicencia => itemActivarLicencia;
    internal ToolStripMenuItem ItemRegistrarPesaje => itemRegistrarPesaje;
    internal ToolStripMenuItem ItemImportarLegado => itemImportarLegado;
    internal MonitorBalanzaUi MonitorBalanza => _monitorBalanza;
    internal Label LblEmpresaInicio => lblEmpresaInicio;
    internal Label LblDetalleInicio => lblDetalleInicio;
    internal PictureBox PicLogoInicio => picLogoInicio;
    internal TarjetaAccion TarjetaPesaje => tarjetaPesaje;
    internal TarjetaAccion TarjetaTicket => tarjetaTicket;
    internal TarjetaAccion TarjetaHistorial => tarjetaHistorial;

    public FrmPrincipal(
        ISesionActual sesion,
        IFormFactory formFactory,
        TimeProvider timeProvider,
        IServicioParametros servicioParametros,
        PesajeFaeneo.Application.Balanza.IServicioBalanza servicioBalanza,
        IServicioLicencia licencia)
    {
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));
        _servicioBalanza = servicioBalanza ?? throw new ArgumentNullException(nameof(servicioBalanza));
        _licencia = licencia ?? throw new ArgumentNullException(nameof(licencia));

        _monitorBalanza = new MonitorBalanzaUi(_servicioBalanza);
        _monitorBalanza.Actualizado += (_, i) => ActualizarBalanza(i);

        InitializeComponent();
        ConfigurarMenu();
        ConfigurarStatusStrip();

        var u = _sesion.Usuario;
        if (u != null)
        {
            Text = $"Capataz — {u.NombreCompleto} ({u.Rol})";
        }
        else
        {
            Text = "Capataz";
        }

        AutorizadorMenu.Aplicar(menuStrip.Items, _sesion.TienePermiso);

        // F13 (D10): la licencia puede cambiar con la ventana abierta (activación, sincronización, prueba que vence).
        _alCambiarLicencia = (_, estado) => AlCambiarLicencia(estado);
        _licencia.EstadoCambiado += _alCambiarLicencia;
        AplicarEstadoLicencia(_licencia.ObtenerEstado());

        Shown += async (_, _) => await EjecutarAsync(ActualizarEmpresaAsync);
        Shown += (_, _) => _monitorBalanza.Iniciar();
        // Foco inicial en la acción más frecuente: Enter abre el pesaje de inmediato.
        Shown += (_, _) => ActiveControl = tarjetaPesaje.Enabled ? tarjetaPesaje : tarjetaTicket;
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        ClientSize = new Size(1024, 700);
        MinimumSize = new Size(800, 600);
        WindowState = FormWindowState.Maximized;

        menuStrip = new MenuStrip();
        statusStrip = new StatusStrip();

        MainMenuStrip = menuStrip;
        // El panel de inicio (Fill) va primero: WinForms acomoda los controles acoplados del último al primero,
        // así el menú y la barra de estado toman su borde antes y el inicio ocupa el resto.
        Controls.Add(ConstruirInicio());
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>
    /// Pantalla de inicio: encabezado de marca (logo + empresa) y las dos acciones del día a día como tarjetas grandes.
    /// La principal (Control de peso) va rellena y con el foco inicial; la secundaria (Imprimir ticket), con contorno.
    /// </summary>
    private Control ConstruirInicio()
    {
        var colorFondo = Color.FromArgb(0xF3, 0xF4, 0xF6);

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = colorFondo
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Encabezado de marca.
        var encabezado = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Padding = new Padding(32, 16, 32, 16)
        };
        encabezado.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        encabezado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        encabezado.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        picLogoInicio = new PictureBox
        {
            Size = new Size(88, 88),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 20, 0),
            Anchor = AnchorStyles.Left,
            Visible = false,
            AccessibleName = "Logo de la empresa",
            TabStop = false
        };

        var bloqueNombre = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        lblEmpresaInicio = new Label
        {
            Text = TituloPorDefecto,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 24F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x1B, 0x27, 0x33),
            Margin = new Padding(0)
        };
        lblDetalleInicio = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x4B, 0x55, 0x63),
            Margin = new Padding(0, 2, 0, 0),
            Visible = false
        };
        bloqueNombre.Controls.Add(lblEmpresaInicio);
        bloqueNombre.Controls.Add(lblDetalleInicio);

        encabezado.Controls.Add(picLogoInicio, 0, 0);
        encabezado.Controls.Add(bloqueNombre, 1, 0);

        var separador = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0xD5, 0xDA, 0xE0),
            Margin = new Padding(0)
        };

        // Cuerpo: bloque centrado (pregunta, tarjetas, atajos).
        var cuerpo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = colorFondo,
            AutoScroll = true
        };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var centro = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(24)
        };
        centro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centro.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblPregunta = new Label
        {
            Text = "¿Qué desea hacer?",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x1B, 0x27, 0x33),
            Margin = new Padding(0, 0, 0, 14)
        };

        var tarjetas = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };

        tarjetaPesaje = new TarjetaAccion
        {
            Text = "Control de peso",
            Descripcion = "Registrar el pesaje de una res: ganadero, cliente y lecturas de la balanza.",
            Atajo = "F2",
            Icono = IconoTarjeta.Balanza,
            Principal = true,
            Margin = new Padding(0, 0, 24, 0),
            AccessibleDescription = "Abre el registro de pesaje. Atajo F2."
        };
        tarjetaPesaje.Click += (_, _) => AbrirPesaje();
        _descripcionPesajeOriginal = tarjetaPesaje.Descripcion;

        tarjetaTicket = new TarjetaAccion
        {
            Text = "Imprimir ticket",
            Descripcion = "Imprimir de nuevo una boleta ya registrada, buscándola por su número.",
            Atajo = "F3",
            Icono = IconoTarjeta.Ticket,
            Margin = new Padding(0),
            AccessibleDescription = "Abre la reimpresión de boletas. Atajo F3."
        };
        tarjetaTicket.Click += (_, _) => AbrirReimpresion();
        tarjetaTicket.Margin = new Padding(0, 0, 24, 0);

        // F10 (O3): el historial del día dejó de ser una grilla dentro del pesaje y tiene su propio apartado.
        tarjetaHistorial = new TarjetaAccion
        {
            Text = "Historial de pesajes",
            Descripcion = "Ver los pesajes de un día, vigentes y anuladas, y reimprimir una boleta.",
            Atajo = "F4",
            Icono = IconoTarjeta.Historial,
            Margin = new Padding(0),
            AccessibleDescription = "Abre el historial de pesajes. Atajo F4."
        };
        tarjetaHistorial.Click += (_, _) => AbrirHistorial();

        // Sin permiso la tarjeta no desaparece: queda deshabilitada y lo explica en su propio texto
        // (un control deshabilitado no muestra ToolTip).
        ConfigurarPermisoTarjeta(tarjetaPesaje, Permiso.Pesar);
        ConfigurarPermisoTarjeta(tarjetaTicket, Permiso.ReimprimirBoleta);
        ConfigurarPermisoTarjeta(tarjetaHistorial, Permiso.Pesar);

        tarjetas.Controls.Add(tarjetaPesaje);
        tarjetas.Controls.Add(tarjetaTicket);
        tarjetas.Controls.Add(tarjetaHistorial);

        var textoAtajos = TextoAtajos();
        lblAtajos = new Label
        {
            Text = textoAtajos,
            Visible = textoAtajos.Length > 0,
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(0x4B, 0x55, 0x63),
            Margin = new Padding(0, 16, 0, 0)
        };

        centro.Controls.Add(lblPregunta, 0, 0);
        centro.Controls.Add(tarjetas, 0, 1);
        centro.Controls.Add(lblAtajos, 0, 2);
        cuerpo.Controls.Add(centro, 0, 0);

        raiz.Controls.Add(encabezado, 0, 0);
        raiz.Controls.Add(separador, 0, 1);
        raiz.Controls.Add(cuerpo, 0, 2);

        return raiz;
    }

    // F12 (E3): solo las teclas que el usuario puede usar (F6 exige EditarBoleta, que el Operador no tiene).
    private string TextoAtajos()
    {
        var atajos = new List<string>();
        if (_sesion.TienePermiso(Permiso.Pesar)) atajos.Add("F2 Control de peso");
        if (_sesion.TienePermiso(Permiso.ReimprimirBoleta)) atajos.Add("F3 Imprimir ticket");
        if (_sesion.TienePermiso(Permiso.Pesar)) atajos.Add("F4 Historial");
        if (_sesion.TienePermiso(Permiso.EditarBoleta)) atajos.Add("F6 Buscar boletas");
        return atajos.Count == 0 ? string.Empty : "Atajos: " + string.Join("   ·   ", atajos);
    }

    private void ConfigurarPermisoTarjeta(TarjetaAccion tarjeta, Permiso permiso)
    {
        if (_sesion.TienePermiso(permiso))
        {
            return;
        }

        tarjeta.Enabled = false;
        tarjeta.Descripcion = "Su rol no tiene permiso para esta acción.";
    }

    private void ConfigurarMenu()
    {
        // 1. Pesaje
        var mnuPesaje = new ToolStripMenuItem("&Pesaje");
        itemRegistrarPesaje = new ToolStripMenuItem("&Registrar pesaje")
        {
            ShortcutKeys = Keys.F2,
            Tag = new OpcionMenu(Permiso.Pesar, null)
        };
        itemRegistrarPesaje.Click += (_, _) => AbrirPesaje();
        var itemHistorial = new ToolStripMenuItem("&Historial de pesajes")
        {
            ShortcutKeys = Keys.F4,
            Tag = new OpcionMenu(Permiso.Pesar, null)
        };
        itemHistorial.Click += (_, _) => AbrirHistorial();
        mnuPesaje.DropDownItems.AddRange(new ToolStripItem[] { itemRegistrarPesaje, itemHistorial });

        // 2. Boletas
        var mnuBoletas = new ToolStripMenuItem("&Boletas");
        var itemReimprimir = new ToolStripMenuItem("&Reimprimir boleta")
        {
            ShortcutKeys = Keys.F3,
            Tag = new OpcionMenu(Permiso.ReimprimirBoleta, null)
        };
        itemReimprimir.Click += (_, _) => AbrirReimpresion();
        var itemBuscarEditar = new ToolStripMenuItem("&Buscar / editar boleta")
        {
            ShortcutKeys = Keys.F6,
            Tag = new OpcionMenu(Permiso.EditarBoleta, null)
        };
        itemBuscarEditar.Click += (_, _) => AbrirGestionBoletas(Permiso.EditarBoleta);

        var itemAnular = new ToolStripMenuItem("&Anular boleta")
        {
            Tag = new OpcionMenu(Permiso.AnularBoleta, null)
        };
        itemAnular.Click += (_, _) => AbrirGestionBoletas(Permiso.AnularBoleta);
        mnuBoletas.DropDownItems.AddRange(new ToolStripItem[] { itemReimprimir, itemBuscarEditar, itemAnular });

        // 3. Catálogos
        var mnuCatalogos = new ToolStripMenuItem("&Catálogos");
        var itemGanaderos = new ToolStripMenuItem("&Ganaderos")
        {
            Tag = new OpcionMenu(Permiso.GestionarCatalogos, null)
        };
        itemGanaderos.Click += (_, _) => AbrirGanaderos();

        var itemClientes = new ToolStripMenuItem("&Clientes")
        {
            Tag = new OpcionMenu(Permiso.GestionarCatalogos, null)
        };
        itemClientes.Click += (_, _) => AbrirClientes();

        var itemFleteros = new ToolStripMenuItem("&Fleteros")
        {
            Tag = new OpcionMenu(Permiso.GestionarCatalogos, null)
        };
        itemFleteros.Click += (_, _) => AbrirFleteros();

        mnuCatalogos.DropDownItems.AddRange(new ToolStripItem[] { itemGanaderos, itemClientes, itemFleteros });

        // 4. Reportes
        var mnuReportes = new ToolStripMenuItem("&Reportes");
        var itemReportesPesaje = new ToolStripMenuItem("&Reportes de pesaje")
        {
            Tag = new OpcionMenu(Permiso.VerReportes, null)
        };
        itemReportesPesaje.Click += (_, _) => AbrirReportes(TipoReporte.PesajesDelDia);

        var itemReportesRaza = new ToolStripMenuItem("Pesajes por &raza")
        {
            Tag = new OpcionMenu(Permiso.VerReportes, null)
        };
        itemReportesRaza.Click += (_, _) => AbrirReportes(TipoReporte.PorRaza);

        var itemReportesAnuladas = new ToolStripMenuItem("Boletas &anuladas")
        {
            Tag = new OpcionMenu(Permiso.VerReportes, null)
        };
        itemReportesAnuladas.Click += (_, _) => AbrirReportes(TipoReporte.Anuladas);

        mnuReportes.DropDownItems.AddRange(new ToolStripItem[] { itemReportesPesaje, itemReportesRaza, itemReportesAnuladas });

        // 5. Administración
        var mnuAdministracion = new ToolStripMenuItem("&Administración");
        var itemUsuarios = new ToolStripMenuItem("&Usuarios")
        {
            Tag = new OpcionMenu(Permiso.GestionarUsuarios, null)
        };
        itemUsuarios.Click += (_, _) => AbrirUsuarios();

        var itemParametros = new ToolStripMenuItem("&Parámetros del sistema")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, null)
        };
        itemParametros.Click += async (_, _) => await AbrirParametrosAsync();

        var itemTipoPesaje = new ToolStripMenuItem("&Tipo de pesaje")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, null)
        };
        itemTipoPesaje.Click += (_, _) => AbrirTipoPesaje();

        var itemBalanza = new ToolStripMenuItem("Configuración de &balanza")
        {
            Tag = new OpcionMenu(Permiso.ConfigurarBalanza, null)
        };
        itemBalanza.Click += (_, _) => AbrirConfiguracionBalanza();

        var itemImpresion = new ToolStripMenuItem("Configuración de &impresión")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, null)
        };
        itemImpresion.Click += (_, _) => AbrirConfiguracionImpresion();

        var itemAuditoria = new ToolStripMenuItem("Visor de &auditoría")
        {
            Tag = new OpcionMenu(Permiso.VerAuditoria, null)
        };
        itemAuditoria.Click += (_, _) => AbrirVisorAuditoria();
        mnuAdministracion.DropDownItems.AddRange(new ToolStripItem[] { itemUsuarios, itemParametros, itemTipoPesaje, itemBalanza, itemImpresion, itemAuditoria });

        // 6. Utilitarios
        var mnuUtilitarios = new ToolStripMenuItem("&Utilitarios");
        var itemBackup = new ToolStripMenuItem("&Backup / Restaurar")
        {
            Tag = new OpcionMenu(Permiso.BackupRestore, null)
        };
        itemBackup.Click += (_, _) => AbrirBackupRestaurar();

        var itemImportar = itemImportarLegado = new ToolStripMenuItem("&Importar datos legados")
        {
            Tag = new OpcionMenu(Permiso.ImportarLegado, null)
        };
        itemImportar.Click += (_, _) => AbrirImportarLegado();

        var itemLogs = new ToolStripMenuItem("Visor de &logs")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, null)
        };
        itemLogs.Click += (_, _) => AbrirVisorLogs();

        mnuUtilitarios.DropDownItems.AddRange(new ToolStripItem[] { itemBackup, itemImportar, itemLogs });

        // 7. Sesión
        var mnuSesion = new ToolStripMenuItem("&Sesión");
        var itemCambiarPassword = new ToolStripMenuItem("&Cambiar contraseña…")
        {
            Tag = new OpcionMenu(null, null)
        };
        itemCambiarPassword.Click += (_, _) => AbrirCambioPassword();

        var itemCerrarSesion = new ToolStripMenuItem("Cerrar &sesión")
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.L,
            Tag = new OpcionMenu(null, null)
        };
        itemCerrarSesion.Click += (_, _) => CerrarSesionClick();

        var itemSalir = new ToolStripMenuItem("&Salir")
        {
            Tag = new OpcionMenu(null, null)
        };
        itemSalir.Click += (_, _) => SalirClick();

        mnuSesion.DropDownItems.AddRange(new ToolStripItem[] { itemCambiarPassword, itemCerrarSesion, new ToolStripSeparator(), itemSalir });

        // 8. Ayuda
        var mnuAyuda = new ToolStripMenuItem("A&yuda");
        var itemAsistente = new ToolStripMenuItem("Asistente de &puesta en marcha…")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, null)
        };
        itemAsistente.Click += async (_, _) => await AbrirAsistenteAsync();

        // F13 (D10): sin Tag, así AutorizadorMenu no la toca; se muestra solo mientras el sistema no esté activado.
        itemActivarLicencia = new ToolStripMenuItem("Activar &licencia…");
        itemActivarLicencia.Click += (_, _) => AbrirActivacion();

        var itemAcercaDe = new ToolStripMenuItem("&Acerca de…");
        itemAcercaDe.Click += (_, _) => AbrirAcercaDe();
        mnuAyuda.DropDownItems.AddRange(new ToolStripItem[] { itemAsistente, itemActivarLicencia, itemAcercaDe });

        menuStrip.Items.AddRange(new ToolStripItem[]
        {
            mnuPesaje,
            mnuBoletas,
            mnuCatalogos,
            mnuReportes,
            mnuAdministracion,
            mnuUtilitarios,
            mnuSesion,
            mnuAyuda
        });
    }

    private void ConfigurarStatusStrip()
    {
        lblStatusEmpresa = new ToolStripStatusLabel
        {
            Text = string.Empty,
            BorderSides = ToolStripStatusLabelBorderSides.Right,
            BorderStyle = Border3DStyle.SunkenOuter
        };

        var u = _sesion.Usuario;
        lblStatusUsuario = new ToolStripStatusLabel
        {
            Text = u != null ? $"Usuario: {u.NombreUsuario} ({u.NombreCompleto})" : "Sin sesión",
            BorderSides = ToolStripStatusLabelBorderSides.Right,
            BorderStyle = Border3DStyle.SunkenOuter
        };

        lblStatusRol = new ToolStripStatusLabel
        {
            Text = u != null ? $"Rol: {u.Rol}" : string.Empty,
            BorderSides = ToolStripStatusLabelBorderSides.Right,
            BorderStyle = Border3DStyle.SunkenOuter
        };

        lblStatusBalanza = new ToolStripStatusLabel
        {
            BorderSides = ToolStripStatusLabelBorderSides.Right,
            BorderStyle = Border3DStyle.SunkenOuter,
            Text = "Balanza: iniciando…"
        };

        // F13 (D10): enlace que abre la activación; oculto con el sistema activado.
        lblStatusLicencia = new ToolStripStatusLabel
        {
            IsLink = true,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Visible = false,
            BorderSides = ToolStripStatusLabelBorderSides.Right,
            BorderStyle = Border3DStyle.SunkenOuter
        };
        lblStatusLicencia.Click += (_, _) => AbrirActivacion();

        var lblSeparador = new ToolStripStatusLabel
        {
            Spring = true
        };

        lblStatusFechaHora = new ToolStripStatusLabel
        {
            BorderSides = ToolStripStatusLabelBorderSides.Right,
            BorderStyle = Border3DStyle.SunkenOuter
        };
        ActualizarReloj();

        lblStatusVersion = new ToolStripStatusLabel
        {
            Text = $"v{InfoVersion.Version}"
        };

        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            lblStatusEmpresa,
            lblStatusUsuario,
            lblStatusRol,
            lblStatusBalanza,
            lblStatusLicencia,
            lblSeparador,
            lblStatusFechaHora,
            lblStatusVersion
        });

        _relojTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _relojTimer.Tick += (_, _) => ActualizarReloj();
        _relojTimer.Start();
    }

    private void ActualizarReloj()
    {
        var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);
        lblStatusFechaHora.Text = ahoraLocal.ToString("dd/MM/yyyy HH:mm:ss");
    }

    private async Task ActualizarEmpresaAsync()
    {
        try
        {
            var parametros = await _servicioParametros.ObtenerAsync();
            var nombre = string.IsNullOrWhiteSpace(parametros.NombreEmpresa)
                ? string.Empty
                : parametros.NombreEmpresa.Trim();
            lblStatusEmpresa.Text = nombre;
            PintarMarcaInicio(nombre, parametros);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo obtener el nombre de la empresa para la barra de estado.");
            lblStatusEmpresa.Text = string.Empty;
            PintarMarcaInicio(string.Empty, null);
        }
    }

    private void PintarMarcaInicio(string nombre, PesajeFaeneo.Application.Parametros.Dtos.ParametrosDto? parametros)
    {
        lblEmpresaInicio.Text = nombre.Length > 0 ? nombre : TituloPorDefecto;

        var detalle = string.Join(
            "  ·  ",
            new[] { parametros?.Direccion, parametros?.Ciudad }
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim()));
        lblDetalleInicio.Text = detalle;
        lblDetalleInicio.Visible = detalle.Length > 0;

        var logo = CargadorLogoUi.Cargar(parametros?.RutaLogoCompleta, "la pantalla de inicio");
        var anterior = picLogoInicio.Image;
        picLogoInicio.Image = logo;
        picLogoInicio.Visible = logo is not null;
        anterior?.Dispose();
    }


    private void AbrirPesaje()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.Pesar);
            _licencia.ExigirOperacion();
            using var frm = _formFactory.Crear<FrmPesaje>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirHistorial()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.Pesar);
            using var frm = _formFactory.Crear<FrmHistorialPesajes>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirReimpresion()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.ReimprimirBoleta);
            using var frm = _formFactory.Crear<FrmReimprimirBoleta>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirGestionBoletas(Permiso permiso)
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(permiso);
            using var frm = _formFactory.Crear<FrmBuscarBoletas>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirReportes(TipoReporte tipo)
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.VerReportes);
            using var frm = _formFactory.Crear<FrmReportes>();
            frm.Configurar(tipo);
            frm.ShowDialog(this);
        });
    }

    private void AbrirVisorAuditoria()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.VerAuditoria);
            using var frm = _formFactory.Crear<FrmVisorAuditoria>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirBackupRestaurar()
    {
        var reiniciar = false;
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.BackupRestore);
            using var frm = _formFactory.Crear<FrmBackupRestaurar>();
            frm.ShowDialog(this);
            reiniciar = frm.ReinicioSolicitado;
        });

        if (reiniciar)
        {
            Solicitud = SolicitudCierreShell.ReiniciarAplicacion;
            Close();
        }
    }

    private void AbrirImportarLegado()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.ImportarLegado);
            _licencia.ExigirOperacion();
            using var frm = _formFactory.Crear<FrmImportarLegado>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirVisorLogs()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarParametros);
            using var frm = _formFactory.Crear<FrmVisorLogs>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirGanaderos()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarCatalogos);
            using var frm = _formFactory.Crear<FrmGanaderos>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirClientes()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarCatalogos);
            using var frm = _formFactory.Crear<FrmClientes>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirFleteros()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarCatalogos);
            using var frm = _formFactory.Crear<FrmFleteros>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirUsuarios()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarUsuarios);
            using var frm = _formFactory.Crear<FrmUsuarios>();
            frm.ShowDialog(this);
        });
    }

    private async Task AbrirParametrosAsync()
    {
        DialogResult resultado = DialogResult.None;
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarParametros);
            using var frm = _formFactory.Crear<FrmParametros>();
            resultado = frm.ShowDialog(this);
        });

        if (resultado == DialogResult.OK)
        {
            await EjecutarAsync(ActualizarEmpresaAsync);
        }
    }

    private void AbrirTipoPesaje()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarParametros);
            using var frm = _formFactory.Crear<FrmTipoPesaje>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirConfiguracionBalanza()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.ConfigurarBalanza);
            using var frm = _formFactory.Crear<FrmConfiguracionBalanza>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirConfiguracionImpresion()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarParametros);
            using var frm = _formFactory.Crear<FrmConfiguracionImpresion>();
            frm.ShowDialog(this);
        });
    }

    private void AbrirCambioPassword()
    {
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmCambioPassword>();
            frm.Obligatorio = false;
            frm.ShowDialog(this);
        });
    }

    private async Task AbrirAsistenteAsync()
    {
        var reiniciar = false;
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarParametros);
            using var frm = _formFactory.Crear<FrmAsistentePuestaEnMarcha>();
            frm.ShowDialog(this);
            reiniciar = frm.ReinicioSolicitado;
        });

        if (reiniciar)
        {
            Solicitud = SolicitudCierreShell.ReiniciarAplicacion;
            Close();
            return;
        }

        await EjecutarAsync(ActualizarEmpresaAsync);
    }

    private void AbrirActivacion()
    {
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmActivacion>();
            frm.Configurar(ModoActivacion.DesdeMenu);
            frm.ShowDialog(this);
        });

        // El evento EstadoCambiado ya refresca la pantalla; esto cubre el caso de un estado sin cambios.
        AplicarEstadoLicencia(_licencia.ObtenerEstado());
    }

    private void AbrirAcercaDe()
    {
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmAcercaDe>();
            frm.ShowDialog(this);
        });
    }

    private void CerrarSesionClick()
    {
        Ejecutar(() =>
        {
            var respuesta = MessageBox.Show(this,
                "¿Está seguro de que desea cerrar la sesión?",
                "Cerrar sesión",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (respuesta == DialogResult.Yes)
            {
                Solicitud = SolicitudCierreShell.CerrarSesion;
                Close();
            }
        });
    }

    private void SalirClick()
    {
        Ejecutar(() =>
        {
            Solicitud = SolicitudCierreShell.SalirAplicacion;
            Close();
        });
    }

    private void ActualizarBalanza(PesajeFaeneo.Application.Balanza.Dtos.InstantaneaBalanza i)
    {
        lblStatusBalanza.Text = $"{PresentacionBalanza.TextoEstado(i)}  {PresentacionBalanza.TextoPeso(i)}";
        lblStatusBalanza.ForeColor = PresentacionBalanza.ColorEstado(i);
        lblStatusBalanza.BackColor = i.EsSimulador ? PresentacionBalanza.ColorFondoSimulador : SystemColors.Control;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _licencia.EstadoCambiado -= _alCambiarLicencia;
            _monitorBalanza?.Dispose();
            _relojTimer?.Stop();
            _relojTimer?.Dispose();
            _relojTimer = null;
            picLogoInicio?.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    internal Task ActualizarEmpresaParaTestAsync() => ActualizarEmpresaAsync();

    internal void AplicarEstadoLicenciaParaTest(EstadoLicencia estado) => AplicarEstadoLicencia(estado);

    private void AlCambiarLicencia(EstadoLicencia estado)
    {
        if (IsDisposed)
        {
            return;
        }

        // El servicio puede notificar desde el hilo de la sincronización en segundo plano.
        if (IsHandleCreated && InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => AplicarEstadoLicencia(estado)));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // El formulario se está cerrando.
            }

            return;
        }

        AplicarEstadoLicencia(estado);
    }

    /// <summary>
    /// F13 (D4, D10): barra de estado, menú Ayuda y bloqueo de Control de peso (menú, tarjeta y F2) e Importar datos legados.
    /// </summary>
    private void AplicarEstadoLicencia(EstadoLicencia estado)
    {
        if (IsDisposed)
        {
            return;
        }

        var activada = estado.Tipo == TipoEstadoLicencia.Activada;
        itemActivarLicencia.Available = !activada;
        lblStatusLicencia.Visible = !activada;
        if (!activada)
        {
            var (texto, color) = estado.Tipo switch
            {
                TipoEstadoLicencia.Prueba => (estado.DiasRestantes == 1 ? "Prueba: 1 día" : $"Prueba: {estado.DiasRestantes} días", Color.DarkOrange),
                TipoEstadoLicencia.NoIniciada => ("Sin activar", Color.Firebrick),
                _ => ("Modo consulta", Color.Firebrick)
            };
            lblStatusLicencia.Text = texto;
            lblStatusLicencia.ForeColor = color;
            lblStatusLicencia.LinkColor = color;
            lblStatusLicencia.ActiveLinkColor = color;
            lblStatusLicencia.VisitedLinkColor = color;
            lblStatusLicencia.ToolTipText = "Haga clic para activar el sistema con una clave.";
        }

        var permite = estado.PermiteOperar;
        var puedePesar = _sesion.TienePermiso(Permiso.Pesar);
        itemRegistrarPesaje.Enabled = permite;
        itemRegistrarPesaje.ToolTipText = permite ? string.Empty : TextoModoConsulta;
        tarjetaPesaje.Enabled = permite && puedePesar;
        if (puedePesar)
        {
            tarjetaPesaje.Descripcion = permite ? _descripcionPesajeOriginal : TextoModoConsulta;
        }

        itemImportarLegado.Enabled = permite;
        itemImportarLegado.ToolTipText = permite ? string.Empty : TextoModoConsulta;
    }
}

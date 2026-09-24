using System.Reflection;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios;

public enum SolicitudCierreShell
{
    SalirAplicacion,
    CerrarSesion
}

public partial class FrmPrincipal : FormBase
{
    private readonly ISesionActual _sesion;
    private readonly IFormFactory _formFactory;
    private readonly TimeProvider _timeProvider;

    private MenuStrip menuStrip = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatusUsuario = null!;
    private ToolStripStatusLabel lblStatusRol = null!;
    private ToolStripStatusLabel lblStatusFechaHora = null!;
    private ToolStripStatusLabel lblStatusVersion = null!;
    private System.Windows.Forms.Timer? _relojTimer;

    public SolicitudCierreShell Solicitud { get; private set; } = SolicitudCierreShell.SalirAplicacion;

    internal MenuStrip MenuPrincipal => menuStrip;

    public FrmPrincipal(
        ISesionActual sesion,
        IFormFactory formFactory,
        TimeProvider timeProvider)
    {
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        InitializeComponent();
        ConfigurarMenu();
        ConfigurarStatusStrip();

        var u = _sesion.Usuario;
        if (u != null)
        {
            Text = $"Sistema de Pesaje para Faeneo — {u.NombreCompleto} ({u.Rol})";
        }
        else
        {
            Text = "Sistema de Pesaje para Faeneo";
        }

        AutorizadorMenu.Aplicar(menuStrip.Items, _sesion.TienePermiso);
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
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);

        ResumeLayout(false);
        PerformLayout();
    }

    private void ConfigurarMenu()
    {
        // 1. Pesaje
        var mnuPesaje = new ToolStripMenuItem("&Pesaje");
        var itemRegistrarPesaje = new ToolStripMenuItem("&Registrar pesaje")
        {
            ShortcutKeys = Keys.F2,
            Tag = new OpcionMenu(Permiso.Pesar, "F4")
        };
        mnuPesaje.DropDownItems.Add(itemRegistrarPesaje);

        // 2. Boletas
        var mnuBoletas = new ToolStripMenuItem("&Boletas");
        var itemReimprimir = new ToolStripMenuItem("&Reimprimir boleta")
        {
            Tag = new OpcionMenu(Permiso.ReimprimirBoleta, "F5")
        };
        var itemBuscarEditar = new ToolStripMenuItem("&Buscar / editar boleta")
        {
            Tag = new OpcionMenu(Permiso.EditarBoleta, "F6")
        };
        var itemAnular = new ToolStripMenuItem("&Anular boleta")
        {
            Tag = new OpcionMenu(Permiso.AnularBoleta, "F6")
        };
        mnuBoletas.DropDownItems.AddRange(new ToolStripItem[] { itemReimprimir, itemBuscarEditar, itemAnular });

        // 3. Catálogos
        var mnuCatalogos = new ToolStripMenuItem("&Catálogos");
        var itemGanaderos = new ToolStripMenuItem("&Ganaderos")
        {
            Tag = new OpcionMenu(Permiso.GestionarCatalogos, "F2")
        };
        var itemClientes = new ToolStripMenuItem("&Clientes")
        {
            Tag = new OpcionMenu(Permiso.GestionarCatalogos, "F2")
        };
        var itemFleteros = new ToolStripMenuItem("&Fleteros")
        {
            Tag = new OpcionMenu(Permiso.GestionarCatalogos, "F2")
        };
        mnuCatalogos.DropDownItems.AddRange(new ToolStripItem[] { itemGanaderos, itemClientes, itemFleteros });

        // 4. Reportes
        var mnuReportes = new ToolStripMenuItem("&Reportes");
        var itemReportesPesaje = new ToolStripMenuItem("&Reportes de pesaje")
        {
            Tag = new OpcionMenu(Permiso.VerReportes, "F7")
        };
        var itemReportesAnuladas = new ToolStripMenuItem("Boletas &anuladas")
        {
            Tag = new OpcionMenu(Permiso.VerReportes, "F7")
        };
        mnuReportes.DropDownItems.AddRange(new ToolStripItem[] { itemReportesPesaje, itemReportesAnuladas });

        // 5. Administración
        var mnuAdministracion = new ToolStripMenuItem("&Administración");
        var itemUsuarios = new ToolStripMenuItem("&Usuarios")
        {
            Tag = new OpcionMenu(Permiso.GestionarUsuarios, null)
        };
        itemUsuarios.Click += (_, _) => AbrirUsuarios();

        var itemParametros = new ToolStripMenuItem("&Parámetros del sistema")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, "F2")
        };
        var itemBalanza = new ToolStripMenuItem("Configuración de &balanza")
        {
            Tag = new OpcionMenu(Permiso.ConfigurarBalanza, "F3")
        };
        var itemAuditoria = new ToolStripMenuItem("Visor de &auditoría")
        {
            Tag = new OpcionMenu(Permiso.VerAuditoria, "F6")
        };
        mnuAdministracion.DropDownItems.AddRange(new ToolStripItem[] { itemUsuarios, itemParametros, itemBalanza, itemAuditoria });

        // 6. Utilitarios
        var mnuUtilitarios = new ToolStripMenuItem("&Utilitarios");
        var itemBackup = new ToolStripMenuItem("&Backup / Restaurar")
        {
            Tag = new OpcionMenu(Permiso.BackupRestore, "F8")
        };
        var itemImportar = new ToolStripMenuItem("&Importar datos legados")
        {
            Tag = new OpcionMenu(Permiso.ImportarLegado, "F8")
        };
        var itemLogs = new ToolStripMenuItem("Visor de &logs")
        {
            Tag = new OpcionMenu(Permiso.GestionarParametros, "F8")
        };
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

        menuStrip.Items.AddRange(new ToolStripItem[]
        {
            mnuPesaje,
            mnuBoletas,
            mnuCatalogos,
            mnuReportes,
            mnuAdministracion,
            mnuUtilitarios,
            mnuSesion
        });
    }

    private void ConfigurarStatusStrip()
    {
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

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        lblStatusVersion = new ToolStripStatusLabel
        {
            Text = $"v{version}"
        };

        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            lblStatusUsuario,
            lblStatusRol,
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

    private void AbrirUsuarios()
    {
        Ejecutar(() =>
        {
            _sesion.ExigirPermiso(Permiso.GestionarUsuarios);
            using var frm = _formFactory.Crear<FrmUsuarios>();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _relojTimer?.Stop();
            _relojTimer?.Dispose();
            _relojTimer = null;
        }
        base.Dispose(disposing);
    }
}

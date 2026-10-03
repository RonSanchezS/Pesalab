using PesajeFaeneo.Application.PuestaEnMarcha;
using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.PuestaEnMarcha;

public enum PasoPuestaEnMarcha
{
    CopiasSeguridad = 1,
    Empresa = 2,
    Balanza = 3,
    Impresion = 4,
    Fleteros = 5,
    Usuarios = 6,
    Importacion = 7
}

/// <summary>
/// Centro de pasos de la puesta en marcha (D13): cada paso muestra su estado y abre el formulario existente
/// que ya lo resuelve. "Finalizar" marca ConfiguracionInicialCompleta; "Omitir por ahora" cierra sin marcar.
/// </summary>
public sealed class FrmAsistentePuestaEnMarcha : FormBase
{
    private sealed record DefinicionPaso(PasoPuestaEnMarcha Paso, string Titulo, string Detalle, Permiso Permiso);

    private static readonly DefinicionPaso[] Pasos =
    [
        new(PasoPuestaEnMarcha.CopiasSeguridad, "1. Copias de seguridad (opcional)",
            "Si este equipo reemplaza a otro, restaure aquí la copia del equipo anterior. Configure también una carpeta secundaria (pendrive o red).",
            Permiso.BackupRestore),
        new(PasoPuestaEnMarcha.Empresa, "2. Datos de la empresa (obligatorio)",
            "Nombre, dirección y teléfonos que salen en la boleta; logo opcional.",
            Permiso.GestionarParametros),
        new(PasoPuestaEnMarcha.Balanza, "3. Balanza (obligatorio)",
            "Tipo de conexión, puerto y protocolo del indicador. Use \"Probar conexión\" antes de guardar.",
            Permiso.ConfigurarBalanza),
        new(PasoPuestaEnMarcha.Impresion, "4. Impresión de boletas (recomendado)",
            "Impresora, papel y calibración de este equipo.",
            Permiso.GestionarParametros),
        new(PasoPuestaEnMarcha.Fleteros, "5. Fleteros (opcional)",
            "Hasta 4 fleteros que salen en la boleta.",
            Permiso.GestionarCatalogos),
        new(PasoPuestaEnMarcha.Usuarios, "6. Usuarios (recomendado)",
            "Cree las cuentas de operadores y supervisores; no comparta la cuenta de administrador.",
            Permiso.GestionarUsuarios),
        new(PasoPuestaEnMarcha.Importacion, "7. Datos del sistema anterior (opcional)",
            "Importe ganaderos, clientes y boletas desde CSV, DBF o Access.",
            Permiso.ImportarLegado)
    ];

    private readonly IServicioPuestaEnMarcha _servicio;
    private readonly ISesionActual _sesion;
    private readonly IFormFactory _formFactory;
    private readonly Dictionary<PasoPuestaEnMarcha, Label> _estados = new();
    private readonly Dictionary<PasoPuestaEnMarcha, Button> _botones = new();

    private EstadoPuestaEnMarcha? _estado;

    private CheckBox chkSimulador = null!;
    private Label lblResumen = null!;
    private Button btnFinalizar = null!;
    private Button btnOmitir = null!;

    public bool ReinicioSolicitado { get; private set; }

    internal Func<PasoPuestaEnMarcha, bool>? AbrirPasoParaTest { get; set; }
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }
    internal CheckBox ChkSimulador => chkSimulador;
    internal Button BtnFinalizar => btnFinalizar;
    internal Button BtnOmitir => btnOmitir;
    internal Label LblResumen => lblResumen;
    internal Button BotonPaso(PasoPuestaEnMarcha paso) => _botones[paso];
    internal Label EstadoPaso(PasoPuestaEnMarcha paso) => _estados[paso];
    internal Task CargarParaTestAsync() => EjecutarAsync(CargarAsync);

    public FrmAsistentePuestaEnMarcha(IServicioPuestaEnMarcha servicio, ISesionActual sesion, IFormFactory formFactory)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));

        Text = "Asistente de puesta en marcha";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 600);
        Size = new Size(860, 640);

        InicializarComponentes();

        Shown += async (_, _) => await EjecutarAsync(CargarAsync);
    }

    private void InicializarComponentes()
    {
        SuspendLayout();

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblIntro = new Label
        {
            Text = "Complete los pasos obligatorios para dejar el sistema listo para pesar. Puede cerrar este asistente y volver más tarde desde Ayuda › Asistente de puesta en marcha.",
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        raiz.Controls.Add(lblIntro, 0, 0);

        var tlpPasos = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 3,
            RowCount = Pasos.Length
        };
        tlpPasos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlpPasos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        tlpPasos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (var i = 0; i < Pasos.Length; i++)
        {
            var def = Pasos[i];
            tlpPasos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblEstado = new Label
            {
                Text = string.Empty,
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 8, 16, 8),
                MinimumSize = new Size(110, 0)
            };
            var lblDescripcion = new Label
            {
                Text = def.Titulo + Environment.NewLine + def.Detalle,
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Margin = new Padding(0, 8, 16, 8)
            };
            var boton = new Button
            {
                Text = "Abrir…",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(90, 32),
                Margin = new Padding(0, 6, 0, 6)
            };
            var paso = def.Paso;
            boton.Click += async (_, _) => await AbrirPasoAsync(paso);

            _estados[paso] = lblEstado;
            _botones[paso] = boton;
            tlpPasos.Controls.Add(lblEstado, 0, i);
            tlpPasos.Controls.Add(lblDescripcion, 1, i);
            tlpPasos.Controls.Add(boton, 2, i);
        }

        raiz.Controls.Add(tlpPasos, 0, 1);

        var pie = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 8, 0, 0)
        };
        pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pie.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pie.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pie.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        chkSimulador = new CheckBox
        {
            Text = "Continuar con la balanza en modo SIMULADOR (solo pruebas: los pesos no tienen valor comercial)",
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Visible = false,
            Margin = new Padding(0, 0, 0, 6)
        };
        chkSimulador.CheckedChanged += (_, _) => Pintar();
        pie.Controls.Add(chkSimulador, 0, 0);

        lblResumen = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            ForeColor = Color.DarkOrange,
            Margin = new Padding(0, 0, 0, 6)
        };
        pie.Controls.Add(lblResumen, 0, 1);

        var flpBotones = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        btnOmitir = new Button
        {
            Text = "Omitir por ahora",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(130, 32),
            Margin = new Padding(0)
        };
        btnFinalizar = new Button
        {
            Text = "Finalizar",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(110, 32),
            Enabled = false,
            Margin = new Padding(0, 0, 10, 0)
        };
        btnFinalizar.Click += async (_, _) => await FinalizarAsync();

        flpBotones.Controls.Add(btnOmitir);
        flpBotones.Controls.Add(btnFinalizar);
        pie.Controls.Add(flpBotones, 0, 2);

        raiz.Controls.Add(pie, 0, 2);
        Controls.Add(raiz);

        // Sin AcceptButton: Enter no debe finalizar la puesta en marcha por accidente.
        CancelButton = btnOmitir;

        ResumeLayout(false);
        PerformLayout();
    }

    private async Task CargarAsync()
    {
        _estado = await _servicio.ObtenerEstadoAsync();
        Pintar();
    }

    private void Pintar()
    {
        var estado = _estado;

        foreach (var def in Pasos)
        {
            _botones[def.Paso].Enabled = _sesion.TienePermiso(def.Permiso);
        }

        if (estado is null)
        {
            btnFinalizar.Enabled = false;
            return;
        }

        PintarEstado(PasoPuestaEnMarcha.CopiasSeguridad, "Opcional", SystemColors.GrayText);
        PintarEstado(PasoPuestaEnMarcha.Importacion, "Opcional", SystemColors.GrayText);

        if (estado.EmpresaConfigurada)
        {
            PintarEstado(PasoPuestaEnMarcha.Empresa, "Listo", Color.ForestGreen);
        }
        else
        {
            PintarEstado(PasoPuestaEnMarcha.Empresa, "Pendiente", Color.DarkOrange);
        }

        switch (estado.TipoConexionBalanza)
        {
            case TipoConexion.Simulador:
                PintarEstado(PasoPuestaEnMarcha.Balanza, "SIMULADOR", Color.DarkOrange);
                break;
            case TipoConexion.Serial:
                PintarEstado(PasoPuestaEnMarcha.Balanza, "Listo (Serie)", Color.ForestGreen);
                break;
            default:
                PintarEstado(PasoPuestaEnMarcha.Balanza, "Listo (Red)", Color.ForestGreen);
                break;
        }

        if (estado.ImpresionConfigurada)
        {
            PintarEstado(PasoPuestaEnMarcha.Impresion, "Listo", Color.ForestGreen);
        }
        else
        {
            PintarEstado(PasoPuestaEnMarcha.Impresion, "Pendiente", Color.DarkOrange);
        }

        PintarEstado(PasoPuestaEnMarcha.Fleteros, $"{estado.FleterosActivos} activos", SystemColors.ControlText);

        if (estado.UsuariosNoAdministradoresActivos == 0)
        {
            PintarEstado(PasoPuestaEnMarcha.Usuarios, "Pendiente", Color.DarkOrange);
        }
        else
        {
            PintarEstado(PasoPuestaEnMarcha.Usuarios, $"{estado.UsuariosNoAdministradoresActivos} usuarios", Color.ForestGreen);
        }

        var esSimulador = estado.TipoConexionBalanza == TipoConexion.Simulador;
        chkSimulador.Visible = esSimulador;
        if (!esSimulador && chkSimulador.Checked)
        {
            chkSimulador.Checked = false;
        }

        var puedeFinalizar = ReglasPuestaEnMarcha.PuedeFinalizar(estado, chkSimulador.Checked);
        btnFinalizar.Enabled = puedeFinalizar;

        if (!estado.EmpresaConfigurada)
        {
            lblResumen.Text = "Falta: datos de la empresa.";
        }
        else if (!puedeFinalizar)
        {
            lblResumen.Text = "Falta: configurar la balanza o confirmar el simulador.";
        }
        else
        {
            lblResumen.Text = string.Empty;
        }
    }

    private void PintarEstado(PasoPuestaEnMarcha paso, string texto, Color color)
    {
        var etiqueta = _estados[paso];
        etiqueta.Text = texto;
        etiqueta.ForeColor = color;
    }

    private async Task AbrirPasoAsync(PasoPuestaEnMarcha paso)
    {
        var reinicio = false;
        Ejecutar(() => reinicio = AbrirPaso(paso));

        if (reinicio)
        {
            ReinicioSolicitado = true;
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        await EjecutarAsync(CargarAsync);
    }

    private bool AbrirPaso(PasoPuestaEnMarcha paso)
    {
        if (AbrirPasoParaTest is not null)
        {
            return AbrirPasoParaTest(paso);
        }

        var permiso = Pasos.Single(p => p.Paso == paso).Permiso;
        _sesion.ExigirPermiso(permiso);

        switch (paso)
        {
            case PasoPuestaEnMarcha.CopiasSeguridad:
                {
                    using var frm = _formFactory.Crear<FrmBackupRestaurar>();
                    frm.ShowDialog(this);
                    return frm.ReinicioSolicitado;
                }
            case PasoPuestaEnMarcha.Empresa:
                {
                    using var frm = _formFactory.Crear<FrmParametros>();
                    frm.ShowDialog(this);
                    return false;
                }
            case PasoPuestaEnMarcha.Balanza:
                {
                    using var frm = _formFactory.Crear<FrmConfiguracionBalanza>();
                    frm.ShowDialog(this);
                    return false;
                }
            case PasoPuestaEnMarcha.Impresion:
                {
                    using var frm = _formFactory.Crear<FrmConfiguracionImpresion>();
                    frm.ShowDialog(this);
                    return false;
                }
            case PasoPuestaEnMarcha.Fleteros:
                {
                    using var frm = _formFactory.Crear<FrmFleteros>();
                    frm.ShowDialog(this);
                    return false;
                }
            case PasoPuestaEnMarcha.Usuarios:
                {
                    using var frm = _formFactory.Crear<FrmUsuarios>();
                    frm.ShowDialog(this);
                    return false;
                }
            case PasoPuestaEnMarcha.Importacion:
                {
                    using var frm = _formFactory.Crear<FrmImportarLegado>();
                    frm.ShowDialog(this);
                    return false;
                }
            default:
                return false;
        }
    }

    private async Task FinalizarAsync()
    {
        ValidacionException? error = null;
        var completado = false;

        await EjecutarAsync(async () =>
        {
            try
            {
                await _servicio.MarcarCompletaAsync(chkSimulador.Checked);
                completado = true;
            }
            catch (ValidacionException ex)
            {
                error = ex;   // se muestra fuera (regla 0.10); nunca llega a ManejadorErrores
            }
        }, btnFinalizar);

        if (error is not null)
        {
            MostrarAdvertencia(error.Message);
            await EjecutarAsync(CargarAsync);
            return;
        }

        if (completado)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void MostrarAdvertencia(string texto)
    {
        if (MostrarAdvertenciaParaTest is not null)
        {
            MostrarAdvertenciaParaTest(texto);
            return;
        }

        MessageBox.Show(this, texto, "Asistente de puesta en marcha", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

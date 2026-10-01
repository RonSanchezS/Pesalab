using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.WinForms.Formularios.Boletas;

public class FrmReimprimirBoleta : FormBase
{
    private readonly IServicioBoletas _boletas;
    private readonly IServicioImpresionBoletas _impresion;
    private readonly IServicioParametros _parametros;

    private readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controles;
    private DatosBoleta? _datos;
    private int _versionNumero;

    internal NumericUpDown numBoleta;
    internal Button btnBuscar;

    internal Label lblFecha;
    internal Label lblGanadero;
    internal Label lblCliente;
    internal Label lblDesposte;
    internal Label lblTotal;
    internal Label lblEstado;
    internal Label lblImpresiones;

    internal Button btnVistaPrevia;
    internal Button btnImprimir;
    internal Button btnCerrar;
    internal Label lblMensaje;

    internal Action<Form>? MostrarDialogoParaTest { get; set; }
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }

    public FrmReimprimirBoleta(
        IServicioBoletas boletas,
        IServicioImpresionBoletas impresion,
        IServicioParametros parametros)
    {
        _boletas = boletas ?? throw new ArgumentNullException(nameof(boletas));
        _impresion = impresion ?? throw new ArgumentNullException(nameof(impresion));
        _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));

        Text = "Reimprimir boleta";
        // El diálogo se dimensiona por su contenido (etiquetas AutoSize): con escalado de Windows o nombres
        // largos no se corta ningún texto.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _errorProvider = new ErrorProvider { ContainerControl = this };

        // 1. Selector de número
        var lblNumero = new Label
        {
            Text = "Número de boleta:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 6, 3)
        };

        numBoleta = new NumericUpDown
        {
            Width = 150,
            Anchor = AnchorStyles.Left,
            Minimum = 1,
            Maximum = 999999999,
            ThousandsSeparator = false
        };
        // Si el número cambia (con las flechas o tecleando), el resumen ya no corresponde: F4/F9 no
        // deben actuar sobre la boleta buscada antes (Enter vuelve a buscar).
        numBoleta.ValueChanged += (_, _) => InvalidarBoleta();
        numBoleta.TextChanged += (_, _) => InvalidarBoleta();

        btnBuscar = new Button
        {
            Text = "&Buscar (Enter)",
            AutoSize = true,
            Size = new Size(130, 30),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(12, 3, 3, 3)
        };
        btnBuscar.Click += async (_, _) => await EjecutarAsync(BuscarInternoAsync, btnBuscar);

        _controles = new Dictionary<string, Control>
        {
            [CamposBoleta.Numero] = numBoleta
        };

        var pnlNumero = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        pnlNumero.Controls.AddRange(new Control[] { lblNumero, numBoleta, btnBuscar });

        // 2. Grupo de datos: una fila por dato (etiqueta | valor)
        var grpDatos = new GroupBox
        {
            Text = "Datos de la boleta",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8)
        };

        var tblDatos = new TableLayoutPanel
        {
            // Fill (no Top): con Top el GroupBox toma el ancho actual de la tabla en lugar del que piden sus
            // columnas, y los valores largos se salen del grupo.
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2
        };
        tblDatos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tblDatos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoValores));

        lblFecha = AgregarFila(tblDatos, "Fecha:", negrita: true);
        lblGanadero = AgregarFila(tblDatos, "Ganadero:");
        lblCliente = AgregarFila(tblDatos, "Cliente:");
        lblDesposte = AgregarFila(tblDatos, "Desposte:");
        lblTotal = AgregarFila(tblDatos, "Peso total:", negrita: true);
        lblEstado = AgregarFila(tblDatos, "Estado:");
        lblEstado.ForeColor = Color.DarkRed;
        lblImpresiones = AgregarFila(tblDatos, "Impresiones:");

        grpDatos.Controls.Add(tblDatos);

        // 3. Mensaje de estado
        lblMensaje = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(AnchoValores + 120, 0),
            ForeColor = Color.DarkBlue,
            Margin = new Padding(0, 8, 0, 4)
        };

        // 4. Botones
        btnVistaPrevia = new Button
        {
            Text = "Vista previa (F4)",
            AutoSize = true,
            Size = new Size(140, 34),
            Enabled = false
        };
        btnVistaPrevia.Click += async (_, _) => await VistaPreviaAsync();

        btnImprimir = new Button
        {
            Text = "&Imprimir (F9)",
            AutoSize = true,
            Size = new Size(140, 34),
            Enabled = false
        };
        btnImprimir.Click += async (_, _) => await ImprimirAsync();

        btnCerrar = new Button
        {
            Text = "Cerrar (Esc)",
            AutoSize = true,
            Size = new Size(130, 34)
        };
        btnCerrar.Click += (_, _) => Close();

        var pnlBotones = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 0)
        };
        pnlBotones.Controls.AddRange(new Control[] { btnVistaPrevia, btnImprimir, btnCerrar });

        var raiz = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Location = new Point(12, 12),
            // Margen derecho/inferior: el AutoSize del formulario lo suma al borde del contenido.
            Margin = new Padding(0, 0, 12, 12)
        };
        raiz.Controls.Add(pnlNumero, 0, 0);
        raiz.Controls.Add(grpDatos, 0, 1);
        raiz.Controls.Add(lblMensaje, 0, 2);
        raiz.Controls.Add(pnlBotones, 0, 3);

        Controls.Add(raiz);

        AcceptButton = btnBuscar;
        CancelButton = btnCerrar;

        Shown += async (_, _) => await EjecutarAsync(CargarInternoAsync);
    }

    // Ancho de la columna de valores: los textos largos (ganadero, cliente, motivo de anulación) pasan a
    // varias líneas en lugar de ensanchar el diálogo.
    private const int AnchoValores = 380;

    private static Label AgregarFila(TableLayoutPanel tabla, string etiqueta, bool negrita = false)
    {
        var fila = tabla.RowCount++;
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Margin = new Padding(0, 4, 12, 4) }, 0, fila);

        var valor = new Label
        {
            Text = "-",
            AutoSize = true,
            MaximumSize = new Size(AnchoValores, 0),
            Margin = new Padding(0, 4, 0, 4)
        };
        if (negrita)
        {
            valor.Font = new Font(tabla.Font, FontStyle.Bold);
        }

        tabla.Controls.Add(valor, 1, fila);
        return valor;
    }

    private async Task CargarInternoAsync()
    {
        var p = await _parametros.ObtenerAsync();
        if (p.UltimoNumeroBoletaEmitido is long u)
        {
            numBoleta.Value = u;
            await BuscarInternoAsync();
        }
        else
        {
            lblMensaje.Text = "Aún no hay boletas emitidas.";
        }

        numBoleta.Focus();
        numBoleta.Select(0, numBoleta.Text.Length);
    }

    private async Task BuscarInternoAsync()
    {
        _errorProvider.Clear();
        try
        {
            // Leer Value valida lo tecleado (y puede disparar InvalidarBoleta): la versión se toma después.
            var numero = (long)numBoleta.Value;
            var version = _versionNumero;
            var datos = await _boletas.ObtenerPorNumeroAsync(numero);
            if (version != _versionNumero)
            {
                // El número cambió mientras se buscaba: la respuesta es de otra boleta y se descarta.
                return;
            }

            _datos = datos;
            MostrarResumen(datos);
        }
        catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
        {
            _datos = null;
            LimpiarResumen();
        }
        finally
        {
            ActualizarBotones();
        }
    }

    private void MostrarResumen(DatosBoleta d)
    {
        lblFecha.Text = TextosBoleta.FechaHora(d.FechaHoraLocal);
        lblGanadero.Text = TextosBoleta.Ganadero(d);
        lblCliente.Text = TextosBoleta.Cliente(d);
        lblDesposte.Text = TextosBoleta.Desposte(d.TipoDesposte);
        lblTotal.Text = TextosBoleta.Peso(d.PesoTotal);

        var partesEstado = new List<string>();
        if (d.Estado == EstadoPesaje.Anulado)
        {
            partesEstado.Add($"ANULADA: {d.MotivoAnulacion}");
        }
        else
        {
            partesEstado.Add("VIGENTE");
        }

        if (d.Origen == OrigenPeso.Simulador)
        {
            partesEstado.Add("(SIMULADO)");
        }

        lblEstado.Text = string.Join(" ", partesEstado);
        lblImpresiones.Text = d.ImpresionesPrevias > 0
            ? $"Impresiones previas: {d.ImpresionesPrevias}"
            : "Aún no se imprimió";
    }

    private void LimpiarResumen()
    {
        lblFecha.Text = "-";
        lblGanadero.Text = "-";
        lblCliente.Text = "-";
        lblDesposte.Text = "-";
        lblTotal.Text = "-";
        lblEstado.Text = "-";
        lblImpresiones.Text = "-";
    }

    private void InvalidarBoleta()
    {
        _versionNumero++;
        if (_datos is null)
        {
            return;
        }

        _datos = null;
        LimpiarResumen();
        ActualizarBotones();
    }

    private void ActualizarBotones()
    {
        btnVistaPrevia.Enabled = btnImprimir.Enabled = _datos is not null;
    }

    private async Task ImprimirAsync()
    {
        // Copia local: _datos puede invalidarse (cambio de número) mientras se espera.
        var datos = _datos;
        if (datos is null) return;

        ResultadoImpresion? r = null;
        await EjecutarAsync(async () =>
            r = await _impresion.ImprimirAsync(datos.PesajeId, SolicitudImpresion.Manual), btnImprimir);

        if (r is null) return;

        lblMensaje.Text = r.Mensaje;
        if (r.Estado == EstadoImpresion.Fallida)
        {
            MostrarAdvertencia(r.Mensaje);
        }

        await EjecutarAsync(BuscarInternoAsync);
    }

    private void MostrarAdvertencia(string texto)
    {
        if (MostrarAdvertenciaParaTest is not null)
        {
            MostrarAdvertenciaParaTest(texto);
            return;
        }

        MessageBox.Show(this, texto, "Impresión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private async Task VistaPreviaAsync()
    {
        var datos = _datos;
        if (datos is null) return;

        PrintDocument? doc = null;
        await EjecutarAsync(async () =>
            doc = await _impresion.CrearVistaPreviaAsync(datos.PesajeId), btnVistaPrevia);

        if (doc is null) return;

        Ejecutar(() =>
        {
            using var frm = new FrmVistaPrevia(doc, $"Vista previa — boleta N° {datos.NumeroBoleta}");
            if (MostrarDialogoParaTest is not null)
            {
                MostrarDialogoParaTest(frm);
            }
            else
            {
                frm.ShowDialog(this);
            }
        });
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F9)
        {
            if (btnImprimir.Enabled) _ = ImprimirAsync();
            return true;
        }

        if (keyData == Keys.F4)
        {
            if (btnVistaPrevia.Enabled) _ = VistaPreviaAsync();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task BuscarParaTestAsync() => BuscarInternoAsync();
    internal Task ImprimirParaTestAsync() => ImprimirAsync();
    internal Task VistaPreviaParaTestAsync() => VistaPreviaAsync();
    internal bool ProcesarTeclaParaTest(Keys key)
    {
        var msg = default(Message);
        return ProcessCmdKey(ref msg, key);
    }
}

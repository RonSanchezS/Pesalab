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
        Size = new Size(580, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _errorProvider = new ErrorProvider { ContainerControl = this };

        // 1. Selector de número
        var lblNumero = new Label
        {
            Text = "Número de boleta:",
            Location = new Point(20, 20),
            AutoSize = true
        };

        numBoleta = new NumericUpDown
        {
            Location = new Point(160, 18),
            Width = 150,
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
            Location = new Point(330, 16),
            Size = new Size(130, 30)
        };
        btnBuscar.Click += async (_, _) => await EjecutarAsync(BuscarInternoAsync, btnBuscar);

        _controles = new Dictionary<string, Control>
        {
            [CamposBoleta.Numero] = numBoleta
        };

        // 2. Grupo de datos
        var grpDatos = new GroupBox
        {
            Text = "Datos de la boleta",
            Location = new Point(20, 60),
            Size = new Size(520, 220)
        };

        var lblEtqFecha = new Label { Text = "Fecha:", Location = new Point(20, 30), AutoSize = true };
        lblFecha = new Label { Text = "-", Location = new Point(130, 30), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };

        var lblEtqGanadero = new Label { Text = "Ganadero:", Location = new Point(20, 60), AutoSize = true };
        lblGanadero = new Label { Text = "-", Location = new Point(130, 60), AutoSize = true };

        var lblEtqCliente = new Label { Text = "Cliente:", Location = new Point(20, 90), AutoSize = true };
        lblCliente = new Label { Text = "-", Location = new Point(130, 90), AutoSize = true };

        var lblEtqDesposte = new Label { Text = "Desposte:", Location = new Point(20, 120), AutoSize = true };
        lblDesposte = new Label { Text = "-", Location = new Point(130, 120), AutoSize = true };

        var lblEtqTotal = new Label { Text = "Peso total:", Location = new Point(20, 150), AutoSize = true };
        lblTotal = new Label { Text = "-", Location = new Point(130, 150), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };

        var lblEtqEstado = new Label { Text = "Estado:", Location = new Point(20, 180), AutoSize = true };
        lblEstado = new Label { Text = "-", Location = new Point(130, 180), AutoSize = true, ForeColor = Color.DarkRed };

        var lblEtqImpresiones = new Label { Text = "Impresiones:", Location = new Point(300, 30), AutoSize = true };
        lblImpresiones = new Label { Text = "-", Location = new Point(390, 30), AutoSize = true };

        grpDatos.Controls.AddRange(new Control[]
        {
            lblEtqFecha, lblFecha,
            lblEtqGanadero, lblGanadero,
            lblEtqCliente, lblCliente,
            lblEtqDesposte, lblDesposte,
            lblEtqTotal, lblTotal,
            lblEtqEstado, lblEstado,
            lblEtqImpresiones, lblImpresiones
        });

        // 3. Mensaje de estado
        lblMensaje = new Label
        {
            Location = new Point(20, 290),
            Size = new Size(520, 30),
            ForeColor = Color.DarkBlue
        };

        // 4. Botones
        btnVistaPrevia = new Button
        {
            Text = "Vista previa (F4)",
            Location = new Point(80, 330),
            Size = new Size(140, 34),
            Enabled = false
        };
        btnVistaPrevia.Click += async (_, _) => await VistaPreviaAsync();

        btnImprimir = new Button
        {
            Text = "&Imprimir (F9)",
            Location = new Point(230, 330),
            Size = new Size(140, 34),
            Enabled = false
        };
        btnImprimir.Click += async (_, _) => await ImprimirAsync();

        btnCerrar = new Button
        {
            Text = "Cerrar (Esc)",
            Location = new Point(380, 330),
            Size = new Size(130, 34)
        };
        btnCerrar.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            lblNumero, numBoleta, btnBuscar,
            grpDatos,
            lblMensaje,
            btnVistaPrevia, btnImprimir, btnCerrar
        });

        AcceptButton = btnBuscar;
        CancelButton = btnCerrar;

        Shown += async (_, _) => await EjecutarAsync(CargarInternoAsync);
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

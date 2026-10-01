using System.Drawing;
using System.Windows.Forms;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Formularios;

namespace PesajeFaeneo.WinForms.Formularios.Boletas;

public class FrmAnularBoleta : FormBase
{
    private readonly IServicioGestionPesajes _servicio;
    internal readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controles;

    private BoletaEncontrada? _boleta;

    internal Label lblTitulo = null!;
    internal Label lblResumen = null!;
    internal Label lblAdvertencia = null!;
    internal TextBox txtMotivo = null!;
    internal Button btnAnular = null!;
    internal Button btnCancelar = null!;

    internal Func<string, bool>? ConfirmarParaTest { get; set; }
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }

    public FrmAnularBoleta(IServicioGestionPesajes servicio)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));

        Text = "Anular boleta";
        ClientSize = new Size(560, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        _errorProvider = new ErrorProvider { ContainerControl = this };
        _controles = new Dictionary<string, Control>();

        ConstruirControles();
    }

    private void ConstruirControles()
    {
        lblTitulo = new Label
        {
            Location = new Point(20, 16),
            Size = new Size(520, 28),
            Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
            ForeColor = Color.DarkRed,
            Text = "Boleta N° —"
        };

        lblResumen = new Label
        {
            Location = new Point(20, 52),
            Size = new Size(520, 50),
            Font = new Font(Font.FontFamily, 9f),
            Text = "Resumen de la boleta..."
        };

        var pnlAdvertencia = new Panel
        {
            Location = new Point(20, 110),
            Size = new Size(520, 60),
            BackColor = Color.FromArgb(255, 245, 230),
            BorderStyle = BorderStyle.FixedSingle
        };

        lblAdvertencia = new Label
        {
            Location = new Point(10, 8),
            Size = new Size(500, 44),
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
            ForeColor = Color.DarkRed,
            Text = "Esta acción NO se puede deshacer. La boleta quedará anulada, conservará su número y no volverá a estar vigente."
        };
        pnlAdvertencia.Controls.Add(lblAdvertencia);

        var lblMotivoTitle = new Label
        {
            Location = new Point(20, 185),
            Size = new Size(520, 20),
            Text = "Motivo de anulación (* obligatorio, 5 a 100 caracteres):"
        };

        txtMotivo = new TextBox
        {
            Location = new Point(20, 210),
            Size = new Size(520, 24),
            MaxLength = 100,
            TabIndex = 0
        };

        btnAnular = new Button
        {
            Location = new Point(290, 290),
            Size = new Size(130, 36),
            Text = "&Anular boleta",
            BackColor = Color.FromArgb(220, 53, 69),
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            TabIndex = 1
        };
        btnAnular.Click += async (_, _) => await AnularAsync();

        btnCancelar = new Button
        {
            Location = new Point(430, 290),
            Size = new Size(110, 36),
            Text = "Cancelar (Esc)",
            TabIndex = 2,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = btnAnular;
        CancelButton = btnCancelar;

        Controls.AddRange(new Control[]
        {
            lblTitulo, lblResumen, pnlAdvertencia,
            lblMotivoTitle, txtMotivo,
            btnAnular, btnCancelar
        });

        _controles[CamposGestionPesajes.Motivo] = txtMotivo;
    }

    public void Configurar(BoletaEncontrada b)
    {
        ArgumentNullException.ThrowIfNull(b);
        _boleta = b;

        lblTitulo.Text = $"Boleta N° {b.NumeroBoleta}";
        lblResumen.Text = $"{TextosBoleta.FechaHora(b.FechaHoraLocal)}  |  Ganadero: {b.Marca} - {b.NombreGanadero}  |  Cliente: {b.NombreCliente}\r\nDesposte: {TextosBoleta.Desposte(b.TipoDesposte)}  |  Total: {TextosBoleta.Peso(b.PesoTotal)}";
        txtMotivo.Text = string.Empty;
        txtMotivo.Focus();
    }

    private async Task AnularAsync()
    {
        if (_boleta is null) return;

        string pregunta = $"Se anulará la boleta N° {_boleta.NumeroBoleta}.\r\n\r\n¿Desea continuar?";
        bool confirmado;
        if (ConfirmarParaTest != null)
        {
            confirmado = ConfirmarParaTest(pregunta);
        }
        else
        {
            confirmado = MessageBox.Show(
                this,
                pregunta,
                "Anular boleta",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        if (!confirmado)
        {
            return;
        }

        bool ok = false;
        bool desfasado = false;

        await EjecutarAsync(async () =>
        {
            _errorProvider.Clear();

            try
            {
                await _servicio.AnularAsync(new AnularPesaje(_boleta.Id, _boleta.Version, txtMotivo.Text));
                ok = true;
            }
            catch (Exception ex) when (EsEstadoDesfasado(ex))
            {
                desfasado = true;
                MostrarAdvertencia(ex.Message);
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
            {
            }
        }, btnAnular);

        if (ok)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else if (desfasado)
        {
            DialogResult = DialogResult.Retry;
            Close();
        }
    }

    private static bool EsEstadoDesfasado(Exception ex)
    {
        return ex is ConcurrenciaException
            || (ex is ReglaNegocioException r && (r.Codigo is "PES_ANULADA" or "PES_YA_ANULADA"))
            || ex is EntidadNoEncontradaException;
    }

    private void MostrarAdvertencia(string texto)
    {
        if (MostrarAdvertenciaParaTest != null)
        {
            MostrarAdvertenciaParaTest(texto);
        }
        else
        {
            MessageBox.Show(this, texto, "Boleta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal Task AnularParaTestAsync() => AnularAsync();
}

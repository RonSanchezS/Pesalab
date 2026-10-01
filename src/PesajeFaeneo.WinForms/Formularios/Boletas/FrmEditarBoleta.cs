using System.Drawing;
using System.Windows.Forms;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios;

namespace PesajeFaeneo.WinForms.Formularios.Boletas;

public class FrmEditarBoleta : FormBase
{
    private readonly IServicioGestionPesajes _servicio;
    internal readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private BoletaParaEdicion? _dto;

    // Controles solo lectura
    internal Label lblTitulo = null!;
    internal Label lblFechaHora = null!;
    internal Label lblDesposte = null!;
    internal TextBox txtPesoIzquierdo = null!;
    internal TextBox txtPesoDerecho = null!;
    internal TextBox txtPesoTotal = null!;
    internal Label lblOrigen = null!;
    internal Label lblOperador = null!;
    internal Label lblAviso = null!;
    internal Label lblMensaje = null!;

    // Controles editables
    internal ControlBusquedaCatalogo ctlGanadero = null!;
    internal NumericUpDown numSecuencia = null!;
    internal ControlBusquedaCatalogo ctlCliente = null!;
    internal TextBox txtDireccion = null!;
    internal TextBox txtColorRaza = null!;
    internal ComboBox cmbSexo = null!;
    internal TextBox txtObservacion = null!;
    internal TextBox txtMotivo = null!;

    // Botones
    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;

    // Ganchos para tests
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }
    public IReadOnlyList<string> CamposModificados { get; private set; } = Array.Empty<string>();

    private sealed record SexoOpcion(char Valor, string Texto);

    public FrmEditarBoleta(IServicioGestionPesajes servicio)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));

        Text = "Editar boleta";
        ClientSize = new Size(640, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = false;

        _errorProvider = new ErrorProvider { ContainerControl = this };
        _controlesPorCampo = new Dictionary<string, Control>();

        ConstruirControles();
    }

    private void ConstruirControles()
    {
        // Panel superior: Solo lectura
        var pnlEncabezado = new GroupBox
        {
            Text = "Datos de origen (solo lectura)",
            Location = new Point(16, 12),
            Size = new Size(608, 140),
            Font = new Font(Font.FontFamily, 8.5f)
        };

        lblTitulo = new Label
        {
            Location = new Point(16, 22),
            Size = new Size(250, 24),
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            Text = "Boleta N° —"
        };

        lblFechaHora = new Label
        {
            Location = new Point(270, 24),
            Size = new Size(200, 20),
            Text = "Fecha: —"
        };

        lblOrigen = new Label
        {
            Location = new Point(480, 24),
            Size = new Size(115, 20),
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.TopRight,
            Text = string.Empty
        };

        lblDesposte = new Label
        {
            Location = new Point(16, 52),
            Size = new Size(250, 20),
            Text = "Desposte: —"
        };

        lblOperador = new Label
        {
            Location = new Point(270, 52),
            Size = new Size(320, 20),
            Text = "Operador: —"
        };

        var lblPesosTitle = new Label
        {
            Location = new Point(16, 80),
            Size = new Size(70, 20),
            Text = "Pesos (kg):"
        };

        txtPesoIzquierdo = new TextBox
        {
            Location = new Point(90, 78),
            Size = new Size(90, 22),
            ReadOnly = true,
            TabStop = false,
            TextAlign = HorizontalAlignment.Right
        };

        txtPesoDerecho = new TextBox
        {
            Location = new Point(190, 78),
            Size = new Size(90, 22),
            ReadOnly = true,
            TabStop = false,
            TextAlign = HorizontalAlignment.Right
        };

        txtPesoTotal = new TextBox
        {
            Location = new Point(290, 78),
            Size = new Size(100, 22),
            ReadOnly = true,
            TabStop = false,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Right
        };

        lblAviso = new Label
        {
            Location = new Point(16, 108),
            Size = new Size(575, 26),
            ForeColor = Color.DimGray,
            Font = new Font(Font.FontFamily, 7.8f, FontStyle.Italic),
            Text = "Los pesos, el tipo de desposte, el número y la fecha no se pueden modificar. Para corregir un peso, anule la boleta y registre una nueva."
        };

        pnlEncabezado.Controls.AddRange(new Control[]
        {
            lblTitulo, lblFechaHora, lblOrigen,
            lblDesposte, lblOperador,
            lblPesosTitle, txtPesoIzquierdo, txtPesoDerecho, txtPesoTotal,
            lblAviso
        });

        // Sección editable
        int y = 162;
        int lblWidth = 140;
        int inputX = 160;
        int inputWidth = 460;
        int spacing = 32;

        var lblGan = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Ganadero (*):" };
        ctlGanadero = new ControlBusquedaCatalogo { Location = new Point(inputX, y), Size = new Size(inputWidth, 26), TabIndex = 0, Placeholder = "Buscar ganadero..." };
        y += spacing;

        var lblSec = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Secuencia (*):" };
        numSecuencia = new NumericUpDown { Location = new Point(inputX, y), Size = new Size(120, 22), Minimum = 1, Maximum = 9999, Value = 1, TabIndex = 1 };
        y += spacing;

        var lblCli = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Cliente (*):" };
        ctlCliente = new ControlBusquedaCatalogo { Location = new Point(inputX, y), Size = new Size(inputWidth, 26), TabIndex = 2, Placeholder = "Buscar cliente..." };
        y += spacing;

        var lblDir = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Dirección (*):" };
        txtDireccion = new TextBox { Location = new Point(inputX, y), Size = new Size(inputWidth, 22), MaxLength = 150, TabIndex = 3 };
        y += spacing;

        var lblColor = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Color / raza (*):" };
        txtColorRaza = new TextBox { Location = new Point(inputX, y), Size = new Size(200, 22), MaxLength = 30, TabIndex = 4 };

        var lblSex = new Label { Location = new Point(370, y + 4), Size = new Size(60, 20), Text = "Sexo (*):" };
        cmbSexo = new ComboBox
        {
            Location = new Point(430, y),
            Size = new Size(190, 22),
            DropDownStyle = ComboBoxStyle.DropDownList,
            TabIndex = 5,
            DisplayMember = "Texto",
            ValueMember = "Valor"
        };
        cmbSexo.Items.Add(new SexoOpcion('M', "MACHO"));
        cmbSexo.Items.Add(new SexoOpcion('H', "HEMBRA"));
        cmbSexo.SelectedIndex = 0;
        y += spacing;

        var lblObs = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Observación (*):" };
        txtObservacion = new TextBox { Location = new Point(inputX, y), Size = new Size(inputWidth, 22), MaxLength = 250, TabIndex = 6 };
        y += spacing;

        var lblMotivo = new Label { Location = new Point(16, y + 4), Size = new Size(lblWidth, 20), Text = "Motivo del cambio:" };
        txtMotivo = new TextBox { Location = new Point(inputX, y), Size = new Size(inputWidth, 22), MaxLength = 100, TabIndex = 7 };
        var lblMotivoPista = new Label { Location = new Point(inputX, y + 24), Size = new Size(inputWidth, 16), ForeColor = Color.Gray, Font = new Font(Font.FontFamily, 7.5f), Text = "Opcional. Breve explicación para el registro de auditoría." };
        y += spacing + 12;

        lblMensaje = new Label
        {
            Location = new Point(16, y),
            Size = new Size(380, 24),
            ForeColor = Color.DarkBlue,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
            Text = string.Empty
        };

        btnGuardar = new Button
        {
            Location = new Point(400, y),
            Size = new Size(110, 32),
            Text = "&Guardar (F12)",
            TabIndex = 8
        };
        btnGuardar.Click += async (_, _) => await GuardarAsync();

        btnCancelar = new Button
        {
            Location = new Point(518, y),
            Size = new Size(102, 32),
            Text = "Cancelar (Esc)",
            TabIndex = 9,
            DialogResult = DialogResult.Cancel
        };

        CancelButton = btnCancelar;
        AcceptButton = null; // Enter no guarda, el flujo es Tab

        Controls.AddRange(new Control[]
        {
            pnlEncabezado,
            lblGan, ctlGanadero,
            lblSec, numSecuencia,
            lblCli, ctlCliente,
            lblDir, txtDireccion,
            lblColor, txtColorRaza,
            lblSex, cmbSexo,
            lblObs, txtObservacion,
            lblMotivo, txtMotivo, lblMotivoPista,
            lblMensaje, btnGuardar, btnCancelar
        });

        _controlesPorCampo[CamposPesaje.Ganadero] = ctlGanadero;
        _controlesPorCampo[CamposPesaje.Cliente] = ctlCliente;
        _controlesPorCampo[CamposPesaje.Direccion] = txtDireccion;
        _controlesPorCampo[CamposPesaje.Secuencia] = numSecuencia;
        _controlesPorCampo[CamposPesaje.ColorRaza] = txtColorRaza;
        _controlesPorCampo[CamposPesaje.Sexo] = cmbSexo;
        _controlesPorCampo[CamposPesaje.Observacion] = txtObservacion;
        _controlesPorCampo[CamposGestionPesajes.Motivo] = txtMotivo;
    }

    public void Configurar(BoletaParaEdicion dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _dto = dto;

        // Solo lectura
        lblTitulo.Text = $"Boleta N° {dto.NumeroBoleta}";
        lblFechaHora.Text = $"Fecha: {TextosBoleta.FechaHora(dto.FechaHoraLocal)}";
        lblDesposte.Text = $"Desposte: {TextosBoleta.Desposte(dto.TipoDesposte)}";
        txtPesoIzquierdo.Text = $"Izq: {TextosBoleta.Peso(dto.PesoIzquierdo)}";
        txtPesoDerecho.Text = $"Der: {TextosBoleta.Peso(dto.PesoDerecho)}";
        txtPesoTotal.Text = $"Total: {TextosBoleta.Peso(dto.PesoTotal)}";
        lblOperador.Text = $"Operador: {dto.NombreOperador}";

        if (dto.Origen == OrigenPeso.Simulador)
        {
            lblOrigen.Text = "SIMULADO";
            lblOrigen.ForeColor = Color.Red;
        }
        else if (dto.Origen == OrigenPeso.Importado)
        {
            lblOrigen.Text = "IMPORTADA";
            lblOrigen.ForeColor = Color.DarkOrange;
        }
        else
        {
            lblOrigen.Text = string.Empty;
        }

        // Editables
        ctlGanadero.EstablecerElementos(dto.Ganaderos);
        ctlGanadero.Seleccionar(dto.GanaderoId);

        numSecuencia.Value = Math.Clamp(dto.Secuencia, numSecuencia.Minimum, numSecuencia.Maximum);

        ctlCliente.EstablecerElementos(dto.Clientes);
        ctlCliente.Seleccionar(dto.ClienteId);

        txtDireccion.Text = dto.Direccion;
        txtColorRaza.Text = dto.ColorRaza;

        for (int i = 0; i < cmbSexo.Items.Count; i++)
        {
            if (cmbSexo.Items[i] is SexoOpcion op && op.Valor == dto.Sexo)
            {
                cmbSexo.SelectedIndex = i;
                break;
            }
        }

        txtObservacion.Text = dto.Observacion;
        txtMotivo.Text = string.Empty;
        lblMensaje.Text = string.Empty;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F12)
        {
            if (btnGuardar.Enabled)
            {
                _ = GuardarAsync();
            }
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task GuardarAsync()
    {
        if (_dto is null) return;

        char sexo = 'M';
        if (cmbSexo.SelectedItem is SexoOpcion opcion)
        {
            sexo = opcion.Valor;
        }
        else if (cmbSexo.SelectedValue is char c)
        {
            sexo = c;
        }

        var dtoEdicion = new EditarPesaje(
            PesajeId: _dto.Id,
            VersionEsperada: _dto.Version,
            GanaderoId: ctlGanadero.Seleccion?.Id ?? 0,
            ClienteId: ctlCliente.Seleccion?.Id ?? 0,
            Direccion: txtDireccion.Text,
            Secuencia: (int)numSecuencia.Value,
            ColorRaza: txtColorRaza.Text,
            Sexo: sexo,
            Observacion: txtObservacion.Text,
            Motivo: string.IsNullOrWhiteSpace(txtMotivo.Text) ? null : txtMotivo.Text);

        ResultadoEdicion? resultado = null;
        bool desfasado = false;

        await EjecutarAsync(async () =>
        {
            _errorProvider.Clear();
            lblMensaje.Text = string.Empty;

            try
            {
                resultado = await _servicio.EditarAsync(dtoEdicion);
            }
            catch (Exception ex) when (EsEstadoDesfasado(ex))
            {
                desfasado = true;
                MostrarAdvertencia(ex.Message);
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
            }
        }, btnGuardar);

        if (desfasado)
        {
            DialogResult = DialogResult.Retry;
            Close();
            return;
        }

        if (resultado is null)
        {
            return;
        }

        if (resultado.HuboCambios)
        {
            CamposModificados = resultado.CamposModificados;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            lblMensaje.Text = "No se modificó ningún dato.";
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

    internal Task GuardarParaTestAsync() => GuardarAsync();

    internal bool ProcesarTeclaParaTest(Keys key)
    {
        var msg = new Message();
        return ProcessCmdKey(ref msg, key);
    }
}

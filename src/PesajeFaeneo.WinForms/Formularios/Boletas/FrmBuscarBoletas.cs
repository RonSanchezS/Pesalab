using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Boletas;

public class FrmBuscarBoletas : FormBase
{
    private readonly IServicioGestionPesajes _servicio;
    private readonly IFormFactory _formFactory;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;

    internal readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controles;

    private int _versionBusqueda;
    private FiltroBoletas? _ultimoFiltro;

    // Filtros
    internal DateTimePicker dtpDesde = null!;
    internal DateTimePicker dtpHasta = null!;
    internal TextBox txtNumero = null!;
    internal ComboBox cmbEstado = null!;
    internal ControlBusquedaCatalogo ctlGanadero = null!;
    internal ControlBusquedaCatalogo ctlCliente = null!;
    internal Button btnBuscar = null!;
    internal Button btnLimpiar = null!;

    // Resultados
    internal DataGridView dgvResultados = null!;
    internal Label lblResumen = null!;

    // Acciones
    internal Button btnEditar = null!;
    internal Button btnAnular = null!;
    internal Button btnCerrar = null!;

    // Ganchos para tests
    internal Func<Form, DialogResult>? MostrarDialogoParaTest { get; set; }
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }
    internal IReadOnlyList<BoletaEncontrada> FilasParaTest =>
        dgvResultados.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is BoletaEncontrada)
            .Select(r => (BoletaEncontrada)r.Tag!)
            .ToList();

    internal BoletaEncontrada? BoletaSeleccionada
    {
        get
        {
            if (dgvResultados.SelectedRows.Count > 0 && dgvResultados.SelectedRows[0].Tag is BoletaEncontrada bSel)
            {
                return bSel;
            }

            if (dgvResultados.CurrentRow != null && dgvResultados.CurrentRow.Selected && dgvResultados.CurrentRow.Tag is BoletaEncontrada bCur)
            {
                return bCur;
            }

            return null;
        }
    }

    public FrmBuscarBoletas(
        IServicioGestionPesajes servicio,
        IFormFactory formFactory,
        ISesionActual sesion,
        TimeProvider timeProvider)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        Text = "Buscar / editar boletas";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 600);

        _errorProvider = new ErrorProvider { ContainerControl = this };
        _controles = new Dictionary<string, Control>();

        ConstruirControles();

        Shown += async (_, _) => await EjecutarAsync(CargarInternoAsync);
    }

    private void ConstruirControles()
    {
        var fechaHoyLocal = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);

        // Fila 1 de filtros
        var lblDesde = DisenoFiltros.Etiqueta("Desde:");
        dtpDesde = DisenoFiltros.Fecha(fechaHoyLocal);

        var lblHasta = DisenoFiltros.Etiqueta("Hasta:");
        dtpHasta = DisenoFiltros.Fecha(fechaHoyLocal);

        var lblNum = DisenoFiltros.Etiqueta("N° boleta:");
        txtNumero = new TextBox
        {
            Width = 90,
            Anchor = AnchorStyles.Left,
            MaxLength = 9
        };
        txtNumero.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        };

        var lblEst = DisenoFiltros.Etiqueta("Estado:");
        cmbEstado = new ComboBox
        {
            Width = 120,
            Anchor = AnchorStyles.Left,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbEstado.Items.AddRange(new object[] { "Todos", "Vigente", "Anulado" });
        cmbEstado.SelectedIndex = 0;

        btnBuscar = DisenoFiltros.Boton("&Buscar (Enter)", 120);
        btnBuscar.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
        btnBuscar.Click += async (_, _) => await EjecutarAsync(() => BuscarInternoAsync());

        btnLimpiar = DisenoFiltros.Boton("&Limpiar", 90);
        btnLimpiar.Click += (_, _) => LimpiarFiltros();

        // Fila 2 de filtros
        var lblGan = DisenoFiltros.Etiqueta("Ganadero:");
        ctlGanadero = new ControlBusquedaCatalogo
        {
            Width = 300,
            Anchor = AnchorStyles.Left,
            Placeholder = "Todos"
        };

        var lblCli = DisenoFiltros.Etiqueta("Cliente:");
        ctlCliente = new ControlBusquedaCatalogo
        {
            Width = 300,
            Anchor = AnchorStyles.Left,
            Placeholder = "Todos"
        };

        var flujo = DisenoFiltros.CrearPanel();
        flujo.Controls.AddRange(new Control[]
        {
            lblDesde, dtpDesde, lblHasta, dtpHasta,
            lblNum, txtNumero, lblEst, cmbEstado,
            btnBuscar, btnLimpiar,
            lblGan, ctlGanadero, lblCli, ctlCliente
        });
        flujo.SetFlowBreak(btnLimpiar, true);
        var pnlFiltros = DisenoFiltros.CrearGrupo("Filtros de búsqueda", flujo);

        // Panel inferior de acciones
        var pnlInferior = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(12, 8, 12, 8)
        };

        lblResumen = new Label
        {
            Location = new Point(16, 16),
            Size = new Size(500, 22),
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            ForeColor = Color.DarkSlateBlue,
            Text = "Listo."
        };

        btnEditar = new Button
        {
            Location = new Point(540, 10),
            Size = new Size(130, 32),
            Text = "&Editar (F4)",
            Enabled = false
        };
        btnEditar.Click += async (_, _) => await EditarSeleccionadoAsync();

        btnAnular = new Button
        {
            Location = new Point(680, 10),
            Size = new Size(130, 32),
            Text = "A&nular (F8)",
            Enabled = false
        };
        btnAnular.Click += async (_, _) => await AnularSeleccionadoAsync();

        btnCerrar = new Button
        {
            Location = new Point(820, 10),
            Size = new Size(110, 32),
            Text = "Cerrar (Esc)",
            DialogResult = DialogResult.Cancel
        };
        btnCerrar.Click += (_, _) => Close();

        pnlInferior.Controls.AddRange(new Control[] { lblResumen, btnEditar, btnAnular, btnCerrar });

        // DataGridView Central
        dgvResultados = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D
        };

        ConfigurarColumnas();

        dgvResultados.SelectionChanged += (_, _) => ActualizarBotones();
        dgvResultados.CurrentCellChanged += (_, _) => ActualizarBotones();
        dgvResultados.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && btnEditar.Enabled)
            {
                await EditarSeleccionadoAsync();
            }
        };

        Controls.Add(dgvResultados);
        Controls.Add(pnlInferior);
        Controls.Add(pnlFiltros);

        AcceptButton = btnBuscar;
        CancelButton = btnCerrar;

        _controles[CamposGestionPesajes.NumeroBoleta] = txtNumero;
        _controles[CamposGestionPesajes.FechaDesde] = dtpDesde;
        _controles[CamposGestionPesajes.FechaHasta] = dtpHasta;
    }

    private void ConfigurarColumnas()
    {
        dgvResultados.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "colNumero", HeaderText = "N°", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { Name = "colFechaHora", HeaderText = "Fecha y hora", Width = 135 },
            new DataGridViewTextBoxColumn { Name = "colMarca", HeaderText = "Marca", Width = 70 },
            new DataGridViewTextBoxColumn { Name = "colGanadero", HeaderText = "Ganadero", Width = 150 },
            new DataGridViewTextBoxColumn { Name = "colSecuencia", HeaderText = "Sec.", Width = 55, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
            new DataGridViewTextBoxColumn { Name = "colCliente", HeaderText = "Cliente", Width = 160 },
            new DataGridViewTextBoxColumn { Name = "colDesposte", HeaderText = "Desposte", Width = 110 },
            new DataGridViewTextBoxColumn { Name = "colIzq", HeaderText = "Izq.", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { Name = "colDer", HeaderText = "Der.", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Total", Width = 85, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold) } },
            new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 85, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
            new DataGridViewTextBoxColumn { Name = "colEstado", HeaderText = "Estado", Width = 85, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold) } }
        });
    }

    private async Task CargarInternoAsync()
    {
        var opciones = await _servicio.ObtenerOpcionesBusquedaAsync();
        ctlGanadero.EstablecerElementos(opciones.Ganaderos);
        ctlCliente.EstablecerElementos(opciones.Clientes);

        await BuscarInternoAsync();
    }

    private FiltroBoletas ConstruirFiltro()
    {
        long? numero = null;
        if (!string.IsNullOrWhiteSpace(txtNumero.Text))
        {
            if (!long.TryParse(txtNumero.Text.Trim(), out var n) || n <= 0)
            {
                throw new ValidacionException(CamposGestionPesajes.NumeroBoleta, "Ingrese un número de boleta válido.");
            }
            numero = n;
        }

        DateOnly? desde = null;
        DateOnly? hasta = null;

        // D16: Si txtNumero tiene texto, Desde y Hasta van nulos
        if (!numero.HasValue)
        {
            if (dtpDesde.Checked) desde = DateOnly.FromDateTime(dtpDesde.Value);
            if (dtpHasta.Checked) hasta = DateOnly.FromDateTime(dtpHasta.Value);
        }

        EstadoPesaje? estado = cmbEstado.SelectedIndex switch
        {
            1 => EstadoPesaje.Vigente,
            2 => EstadoPesaje.Anulado,
            _ => null
        };

        int? ganaderoId = ctlGanadero.Seleccion?.Id;
        int? clienteId = ctlCliente.Seleccion?.Id;

        return new FiltroBoletas(desde, hasta, numero, ganaderoId, clienteId, estado);
    }

    private async Task BuscarInternoAsync(FiltroBoletas? filtro = null, int? reseleccionarId = null)
    {
        _errorProvider.Clear();

        FiltroBoletas filtroEfectivo;
        try
        {
            filtroEfectivo = filtro ?? ConstruirFiltro();
        }
        catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
        {
            return;
        }

        int version = ++_versionBusqueda;
        ResultadoBusquedaBoletas resultado;

        try
        {
            resultado = await _servicio.BuscarAsync(filtroEfectivo);
        }
        catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
        {
            return;
        }

        if (version != _versionBusqueda)
        {
            return;
        }

        _ultimoFiltro = filtroEfectivo;

        dgvResultados.Rows.Clear();
        int filaAReseleccionar = -1;

        foreach (var b in resultado.Boletas)
        {
            string origenTexto = b.Origen switch
            {
                OrigenPeso.Simulador => "SIMULADO",
                OrigenPeso.Importado => "IMPORTADA",
                _ => string.Empty
            };

            string estadoTexto = b.Estado == EstadoPesaje.Vigente ? "VIGENTE" : "ANULADA";

            int index = dgvResultados.Rows.Add(
                b.NumeroBoleta.ToString(),
                TextosBoleta.FechaHora(b.FechaHoraLocal),
                b.Marca,
                b.NombreGanadero,
                b.Secuencia.ToString(),
                b.NombreCliente,
                TextosBoleta.Desposte(b.TipoDesposte),
                TextosBoleta.Peso(b.PesoIzquierdo),
                TextosBoleta.Peso(b.PesoDerecho),
                TextosBoleta.Peso(b.PesoTotal),
                origenTexto,
                estadoTexto);

            var row = dgvResultados.Rows[index];
            row.Tag = b;

            if (b.Estado == EstadoPesaje.Anulado)
            {
                row.DefaultCellStyle.ForeColor = Color.Gray;
            }

            if (reseleccionarId.HasValue && b.Id == reseleccionarId.Value)
            {
                filaAReseleccionar = index;
            }
        }

        if (filaAReseleccionar >= 0)
        {
            dgvResultados.ClearSelection();
            dgvResultados.Rows[filaAReseleccionar].Selected = true;
            dgvResultados.CurrentCell = dgvResultados.Rows[filaAReseleccionar].Cells[0];
        }
        else if (dgvResultados.Rows.Count > 0)
        {
            dgvResultados.ClearSelection();
            dgvResultados.Rows[0].Selected = true;
            dgvResultados.CurrentCell = dgvResultados.Rows[0].Cells[0];
        }

        // Resumen
        if (resultado.Truncado)
        {
            lblResumen.Text = "Se muestran las primeras 200 boletas; refine la búsqueda.";
            lblResumen.ForeColor = Color.DarkOrange;
        }
        else if (resultado.Boletas.Count == 0)
        {
            lblResumen.Text = "No se encontraron boletas.";
            lblResumen.ForeColor = Color.DimGray;
        }
        else
        {
            lblResumen.Text = $"{resultado.Boletas.Count} boleta(s) encontrada(s).";
            lblResumen.ForeColor = Color.DarkSlateBlue;
        }

        ActualizarBotones();
    }

    private void ActualizarBotones()
    {
        var fila = BoletaSeleccionada;
        btnEditar.Enabled = fila is not null && fila.Estado == EstadoPesaje.Vigente && _sesion.TienePermiso(Permiso.EditarBoleta);
        btnAnular.Enabled = fila is not null && fila.Estado == EstadoPesaje.Vigente && _sesion.TienePermiso(Permiso.AnularBoleta);
    }

    private void LimpiarFiltros()
    {
        txtNumero.Text = string.Empty;
        var hoy = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);
        dtpDesde.Checked = true;
        dtpDesde.Value = hoy;
        dtpHasta.Checked = true;
        dtpHasta.Value = hoy;
        cmbEstado.SelectedIndex = 0;
        ctlGanadero.Limpiar();
        ctlCliente.Limpiar();
        _errorProvider.Clear();

        _ = EjecutarAsync(() => BuscarInternoAsync());
    }

    private async Task EditarSeleccionadoAsync()
    {
        var fila = BoletaSeleccionada;
        if (fila is null) return;

        BoletaParaEdicion? dto = null;
        bool desfasado = false;

        await EjecutarAsync(async () =>
        {
            try
            {
                dto = await _servicio.ObtenerParaEdicionAsync(fila.Id);
            }
            catch (Exception ex) when (EsEstadoDesfasado(ex))
            {
                desfasado = true;
                MostrarAdvertencia(ex.Message);
            }
        }, btnEditar);

        if (desfasado)
        {
            await EjecutarAsync(() => BuscarInternoAsync(_ultimoFiltro, fila.Id));
            return;
        }

        if (dto is null)
        {
            return;
        }

        DialogResult r = DialogResult.None;
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmEditarBoleta>();
            frm.Configurar(dto);
            r = MostrarDialogo(frm);
        });

        if (r is DialogResult.OK or DialogResult.Retry)
        {
            await EjecutarAsync(() => BuscarInternoAsync(_ultimoFiltro, fila.Id));
        }
    }

    private async Task AnularSeleccionadoAsync()
    {
        var fila = BoletaSeleccionada;
        if (fila is null) return;

        DialogResult r = DialogResult.None;
        Ejecutar(() =>
        {
            using var frm = _formFactory.Crear<FrmAnularBoleta>();
            frm.Configurar(fila);
            r = MostrarDialogo(frm);
        });

        if (r is DialogResult.OK or DialogResult.Retry)
        {
            await EjecutarAsync(() => BuscarInternoAsync(_ultimoFiltro, fila.Id));
        }
    }

    private static bool EsEstadoDesfasado(Exception ex)
    {
        return ex is ConcurrenciaException
            || (ex is ReglaNegocioException r && (r.Codigo is "PES_ANULADA" or "PES_YA_ANULADA"))
            || ex is EntidadNoEncontradaException;
    }

    private DialogResult MostrarDialogo(Form form)
    {
        if (MostrarDialogoParaTest != null)
        {
            return MostrarDialogoParaTest(form);
        }
        return form.ShowDialog(this);
    }

    private void MostrarAdvertencia(string mensaje)
    {
        if (MostrarAdvertenciaParaTest != null)
        {
            MostrarAdvertenciaParaTest(mensaje);
        }
        else
        {
            MessageBox.Show(this, mensaje, "Boleta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private bool GrillaTieneFoco()
    {
        if (dgvResultados.Focused)
        {
            return true;
        }

        var enfocado = ControlEnfocado();
        return enfocado == dgvResultados || dgvResultados.Contains(enfocado);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F4)
        {
            if (btnEditar.Enabled)
            {
                _ = EditarSeleccionadoAsync();
            }
            return true;
        }

        if (keyData == Keys.F8)
        {
            if (btnAnular.Enabled)
            {
                _ = AnularSeleccionadoAsync();
            }
            return true;
        }

        if (keyData == Keys.Enter && GrillaTieneFoco())
        {
            if (btnEditar.Enabled)
            {
                _ = EditarSeleccionadoAsync();
            }
            return true; // No disparar AcceptButton
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Ganchos para test
    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task BuscarParaTestAsync() => BuscarInternoAsync();
    internal Task EditarParaTestAsync() => EditarSeleccionadoAsync();
    internal Task AnularParaTestAsync() => AnularSeleccionadoAsync();
    internal bool ProcesarTeclaParaTest(Keys key)
    {
        var msg = new Message();
        return ProcessCmdKey(ref msg, key);
    }
}

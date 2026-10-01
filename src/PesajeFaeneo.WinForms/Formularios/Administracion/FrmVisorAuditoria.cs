using System.Drawing;
using System.Windows.Forms;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Auditoria.Dtos;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Administracion;

public class FrmVisorAuditoria : FormBase
{
    private readonly IServicioAuditoria _servicio;
    private readonly TimeProvider _timeProvider;
    private readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controles;

    private int _versionBusqueda;
    private bool _llenando;

    // Filtros
    internal DateTimePicker dtpDesde = null!;
    internal DateTimePicker dtpHasta = null!;
    internal ComboBox cmbEntidad = null!;
    internal ComboBox cmbAccion = null!;
    internal ComboBox cmbUsuario = null!;
    internal TextBox txtBoleta = null!;
    internal Button btnBuscar = null!;
    internal Button btnLimpiar = null!;

    // Registros
    internal DataGridView dgvRegistros = null!;

    // Detalle
    internal TabControl tabDetalle = null!;
    internal DataGridView dgvCambios = null!;
    internal TextBox txtAntesJson = null!;
    internal TextBox txtDespuesJson = null!;

    // Acciones y resumen
    internal Label lblResumen = null!;
    internal Button btnCerrar = null!;

    // Ganchos para test
    internal IReadOnlyList<RegistroAuditoria> FilasParaTest =>
        dgvRegistros.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is RegistroAuditoria)
            .Select(r => (RegistroAuditoria)r.Tag!)
            .ToList();

    internal RegistroAuditoria? RegistroSeleccionado =>
        (dgvRegistros.SelectedRows.Count > 0 ? dgvRegistros.SelectedRows[0].Tag as RegistroAuditoria : null)
        ?? (dgvRegistros.CurrentRow != null && dgvRegistros.CurrentRow.Selected ? dgvRegistros.CurrentRow.Tag as RegistroAuditoria : null);

    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task BuscarParaTestAsync() => BuscarInternoAsync();
    internal void MostrarDetalleParaTest(RegistroAuditoria registro) => MostrarDetalle(registro);
    internal bool ProcesarTeclaParaTest(Keys key)
    {
        var msg = new Message();
        return ProcessCmdKey(ref msg, key);
    }

    public FrmVisorAuditoria(IServicioAuditoria servicio, TimeProvider timeProvider)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        Text = "Visor de auditoría";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 650);

        _errorProvider = new ErrorProvider { ContainerControl = this };
        _controles = new Dictionary<string, Control>();

        ConstruirControles();

        Shown += async (_, _) => await EjecutarAsync(CargarInternoAsync);
    }

    private void ConstruirControles()
    {
        // Panel superior de filtros
        var fechaHoyLocal = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);

        // Fila 1 de filtros
        var lblDesde = DisenoFiltros.Etiqueta("Desde:");
        dtpDesde = DisenoFiltros.Fecha(fechaHoyLocal);

        var lblHasta = DisenoFiltros.Etiqueta("Hasta:");
        dtpHasta = DisenoFiltros.Fecha(fechaHoyLocal);

        var lblEntidad = DisenoFiltros.Etiqueta("Entidad:");
        cmbEntidad = new ComboBox
        {
            Width = 170,
            Anchor = AnchorStyles.Left,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbEntidad.Items.Add("(Todas)");
        foreach (var entidad in EntidadesAuditoria.Todas)
        {
            cmbEntidad.Items.Add(entidad);
        }
        cmbEntidad.SelectedIndex = 0;

        var lblAccion = DisenoFiltros.Etiqueta("Acción:");
        cmbAccion = new ComboBox
        {
            Width = 200,
            Anchor = AnchorStyles.Left,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbAccion.Items.Add("(Todas)");
        foreach (var accion in Enum.GetValues<AccionAuditoria>())
        {
            cmbAccion.Items.Add(accion.ToString());
        }
        cmbAccion.SelectedIndex = 0;

        // Fila 2 de filtros
        var lblUsuario = DisenoFiltros.Etiqueta("Usuario:");
        cmbUsuario = new ComboBox
        {
            Width = 300,
            Anchor = AnchorStyles.Left,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbUsuario.Items.Add(new ItemUsuarioCombo(null, "(Todos)"));
        cmbUsuario.SelectedIndex = 0;

        var lblBoleta = DisenoFiltros.Etiqueta("N° boleta:");
        txtBoleta = new TextBox
        {
            Width = 90,
            Anchor = AnchorStyles.Left,
            MaxLength = 9
        };
        txtBoleta.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        };

        btnBuscar = DisenoFiltros.Boton("&Buscar (Enter)", 120);
        btnBuscar.Click += async (_, _) => await EjecutarAsync(() => BuscarInternoAsync(), btnBuscar);

        btnLimpiar = DisenoFiltros.Boton("&Limpiar", 90);
        btnLimpiar.Click += (_, _) => LimpiarFiltros();

        var flujo = DisenoFiltros.CrearPanel();
        flujo.Controls.AddRange(new Control[]
        {
            lblDesde, dtpDesde, lblHasta, dtpHasta, lblEntidad, cmbEntidad, lblAccion, cmbAccion,
            lblUsuario, cmbUsuario, lblBoleta, txtBoleta, btnBuscar, btnLimpiar
        });
        flujo.SetFlowBreak(cmbAccion, true);
        var pnlFiltros = DisenoFiltros.CrearGrupo("Filtros de auditoría", flujo);

        // Panel inferior de acciones
        var pnlInferior = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            Padding = new Padding(10, 5, 10, 5)
        };

        lblResumen = new Label
        {
            Location = new Point(12, 14),
            Size = new Size(550, 22),
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            Text = "Listo."
        };

        btnCerrar = new Button
        {
            // Dock en lugar de Anchor + Location: el anclaje a la derecha se calculaba contra el ancho inicial
            // del panel y, al escalar por DPI, el botón quedaba fuera de la pantalla.
            Dock = DockStyle.Right,
            Width = 110,
            Text = "Cerrar (Esc)",
            DialogResult = DialogResult.Cancel
        };
        btnCerrar.Click += (_, _) => Close();

        pnlInferior.Controls.AddRange(new Control[] { lblResumen, btnCerrar });

        // SplitContainer central (Registros arriba, Detalle abajo)
        var splitCentral = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 330,
            SplitterWidth = 6
        };

        // Grilla superior de Registros
        dgvRegistros = new DataGridView
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

        ConfigurarColumnasRegistros();

        dgvRegistros.SelectionChanged += (_, _) =>
        {
            if (!_llenando)
            {
                MostrarDetalle(RegistroSeleccionado);
            }
        };
        dgvRegistros.CurrentCellChanged += (_, _) =>
        {
            if (!_llenando)
            {
                MostrarDetalle(RegistroSeleccionado);
            }
        };

        splitCentral.Panel1.Controls.Add(dgvRegistros);

        // TabControl inferior de Detalle
        tabDetalle = new TabControl
        {
            Dock = DockStyle.Fill
        };

        // Pestaña 1: Cambios
        var tabCambios = new TabPage { Text = "Cambios" };
        dgvCambios = new DataGridView
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
        ConfigurarColumnasCambios();
        tabCambios.Controls.Add(dgvCambios);

        // Pestaña 2: JSON
        var tabJson = new TabPage { Text = "JSON" };
        var splitJson = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 480,
            SplitterWidth = 5
        };

        var pnlAntes = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        var lblAntes = new Label { Text = "Antes:", Dock = DockStyle.Top, Height = 20, Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold) };
        txtAntesJson = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            BackColor = Color.White
        };
        pnlAntes.Controls.Add(txtAntesJson);
        pnlAntes.Controls.Add(lblAntes);

        var pnlDespues = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        var lblDespues = new Label { Text = "Después:", Dock = DockStyle.Top, Height = 20, Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold) };
        txtDespuesJson = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            BackColor = Color.White
        };
        pnlDespues.Controls.Add(txtDespuesJson);
        pnlDespues.Controls.Add(lblDespues);

        splitJson.Panel1.Controls.Add(pnlAntes);
        splitJson.Panel2.Controls.Add(pnlDespues);
        tabJson.Controls.Add(splitJson);

        tabDetalle.TabPages.AddRange(new[] { tabCambios, tabJson });
        splitCentral.Panel2.Controls.Add(tabDetalle);

        Controls.Add(splitCentral);
        Controls.Add(pnlInferior);
        Controls.Add(pnlFiltros);

        AcceptButton = btnBuscar;
        CancelButton = btnCerrar;

        _controles[CamposGestionPesajes.NumeroBoleta] = txtBoleta;
        _controles[CamposGestionPesajes.FechaDesde] = dtpDesde;
        _controles[CamposGestionPesajes.FechaHasta] = dtpHasta;
    }

    private void ConfigurarColumnasRegistros()
    {
        dgvRegistros.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "colFechaHora", HeaderText = "Fecha y hora", Width = 155 },
            new DataGridViewTextBoxColumn { Name = "colUsuario", HeaderText = "Usuario", Width = 120 },
            new DataGridViewTextBoxColumn { Name = "colAccion", HeaderText = "Acción", Width = 110 },
            new DataGridViewTextBoxColumn { Name = "colEntidad", HeaderText = "Entidad", Width = 130 },
            new DataGridViewTextBoxColumn { Name = "colEntidadId", HeaderText = "ID", Width = 75, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { Name = "colMotivo", HeaderText = "Motivo / Detalle", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }
        });
    }

    private void ConfigurarColumnasCambios()
    {
        dgvCambios.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "colCampo", HeaderText = "Campo", Width = 170 },
            new DataGridViewTextBoxColumn { Name = "colAntes", HeaderText = "Antes", Width = 260 },
            new DataGridViewTextBoxColumn { Name = "colDespues", HeaderText = "Después", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }
        });
    }

    private async Task CargarInternoAsync()
    {
        var usuarios = await _servicio.ListarUsuariosAsync();
        cmbUsuario.Items.Clear();
        cmbUsuario.Items.Add(new ItemUsuarioCombo(null, "(Todos)"));
        foreach (var u in usuarios)
        {
            cmbUsuario.Items.Add(new ItemUsuarioCombo(u.Id, $"{u.NombreUsuario} — {u.NombreCompleto}"));
        }
        cmbUsuario.SelectedIndex = 0;

        await BuscarInternoAsync();
    }

    private FiltroAuditoria ConstruirFiltro()
    {
        long? boleta = null;
        if (!string.IsNullOrWhiteSpace(txtBoleta.Text))
        {
            if (!long.TryParse(txtBoleta.Text.Trim(), out var n) || n <= 0)
            {
                throw new ValidacionException(CamposGestionPesajes.NumeroBoleta, "Ingrese un número de boleta válido.");
            }
            boleta = n;
        }

        DateOnly? desde = null;
        DateOnly? hasta = null;
        if (dtpDesde.Checked) desde = DateOnly.FromDateTime(dtpDesde.Value);
        if (dtpHasta.Checked) hasta = DateOnly.FromDateTime(dtpHasta.Value);

        string? entidad = cmbEntidad.SelectedIndex > 0 && cmbEntidad.SelectedItem != null ? cmbEntidad.SelectedItem.ToString() : null;
        AccionAuditoria? accion = cmbAccion.SelectedIndex > 0 && cmbAccion.SelectedItem != null ? Enum.Parse<AccionAuditoria>(cmbAccion.SelectedItem.ToString()!) : null;
        int? usuarioId = cmbUsuario.SelectedItem is ItemUsuarioCombo item ? item.Id : null;

        return new FiltroAuditoria(desde, hasta, entidad, accion, usuarioId, boleta);
    }

    private async Task BuscarInternoAsync()
    {
        _errorProvider.Clear();

        FiltroAuditoria filtro;
        try
        {
            filtro = ConstruirFiltro();
        }
        catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
        {
            return;
        }

        int version = ++_versionBusqueda;
        ResultadoAuditoria resultado;

        try
        {
            resultado = await _servicio.ConsultarAsync(filtro);
        }
        catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
        {
            return;
        }

        if (version != _versionBusqueda)
        {
            return;
        }

        _llenando = true;
        dgvRegistros.Rows.Clear();

        foreach (var reg in resultado.Registros)
        {
            int index = dgvRegistros.Rows.Add(
                reg.FechaHoraLocal.ToString("dd/MM/yyyy HH:mm:ss", TextosBoleta.Cultura),
                reg.Usuario,
                reg.Accion.ToString(),
                reg.Entidad,
                reg.EntidadId,
                reg.Motivo ?? string.Empty);

            var row = dgvRegistros.Rows[index];
            row.Tag = reg;
        }

        if (dgvRegistros.Rows.Count > 0)
        {
            dgvRegistros.ClearSelection();
            dgvRegistros.Rows[0].Selected = true;
            dgvRegistros.CurrentCell = dgvRegistros.Rows[0].Cells[0];
        }

        _llenando = false;

        if (resultado.Truncado)
        {
            lblResumen.Text = "Se muestran los primeros 500 registros; refine la búsqueda.";
            lblResumen.ForeColor = Color.DarkOrange;
        }
        else if (resultado.Registros.Count == 0)
        {
            lblResumen.Text = "No hay registros para esos filtros.";
            lblResumen.ForeColor = Color.DimGray;
        }
        else
        {
            lblResumen.Text = $"{resultado.Registros.Count} registro(s) encontrado(s).";
            lblResumen.ForeColor = Color.DarkSlateBlue;
        }

        if (dgvRegistros.Rows.Count > 0)
        {
            MostrarDetalle(RegistroSeleccionado);
        }
        else
        {
            LimpiarDetalle();
        }
    }

    private void MostrarDetalle(RegistroAuditoria? reg)
    {
        if (reg is null)
        {
            LimpiarDetalle();
            return;
        }

        var cambios = DiferenciasAuditoria.Calcular(reg.DatosAntesJson, reg.DatosDespuesJson, _timeProvider.LocalTimeZone);
        dgvCambios.Rows.Clear();

        foreach (var c in cambios)
        {
            string campoLegible = DiferenciasAuditoria.EtiquetaCampo(reg.Entidad, c.Campo);
            string antesTexto = c.Antes ?? "—";
            string despuesTexto = c.Despues ?? "—";

            int idx = dgvCambios.Rows.Add(campoLegible, antesTexto, despuesTexto);
            var row = dgvCambios.Rows[idx];

            if (c.Cambio)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 204);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.White;
            }
        }

        txtAntesJson.Text = reg.DatosAntesJson ?? string.Empty;
        txtDespuesJson.Text = reg.DatosDespuesJson ?? string.Empty;
    }

    private void LimpiarDetalle()
    {
        dgvCambios.Rows.Clear();
        txtAntesJson.Text = string.Empty;
        txtDespuesJson.Text = string.Empty;
    }

    private void LimpiarFiltros()
    {
        var hoy = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);
        dtpDesde.Checked = true;
        dtpDesde.Value = hoy;
        dtpHasta.Checked = true;
        dtpHasta.Value = hoy;
        cmbEntidad.SelectedIndex = 0;
        cmbAccion.SelectedIndex = 0;
        cmbUsuario.SelectedIndex = 0;
        txtBoleta.Text = string.Empty;
        _errorProvider.Clear();

        _ = EjecutarAsync(() => BuscarInternoAsync(), btnBuscar);
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
        if (dgvRegistros.Focused || dgvCambios.Focused)
        {
            return true;
        }

        var enfocado = ControlEnfocado();
        return enfocado == dgvRegistros || dgvRegistros.Contains(enfocado)
            || enfocado == dgvCambios || dgvCambios.Contains(enfocado);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter && GrillaTieneFoco())
        {
            return true; // No disparar AcceptButton
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private sealed class ItemUsuarioCombo
    {
        public int? Id { get; }
        public string Texto { get; }

        public ItemUsuarioCombo(int? id, string texto)
        {
            Id = id;
            Texto = texto;
        }

        public override string ToString() => Texto;
    }
}

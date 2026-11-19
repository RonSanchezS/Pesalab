using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Impresion;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Pesaje;

/// <summary>
/// Historial de pesajes de un día (F10, O3): antes vivía como grilla dentro del registro de pesaje. Se elige el
/// día, se ve la lista (vigentes y anuladas) y, con permiso, se reimprime la boleta seleccionada con el mismo
/// flujo de la reimpresión (auditoría <c>Imprimir</c>/<c>Reimprimir</c>).
/// </summary>
public class FrmHistorialPesajes : FormBase
{
    private readonly IServicioPesajes _servicioPesajes;
    private readonly IServicioImpresionBoletas _servicioImpresion;
    private readonly IServicioParametros _servicioParametros;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;

    // Token de versión: una respuesta que llega después de cambiar de día se descarta.
    private int _versionCarga;
    private bool _recargaEnCurso;
    private bool _recargaPendiente;
    private bool _imprimiendo;

    internal DateTimePicker dtpDia = null!;
    internal Button btnActualizar = null!;
    internal Button btnHoy = null!;
    internal DataGridView dgvHistorial = null!;
    internal Label lblResumen = null!;
    internal Button btnReimprimir = null!;
    internal Button btnCerrar = null!;

    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }

    internal IReadOnlyList<PesajeResumen> FilasParaTest =>
        dgvHistorial.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is PesajeResumen)
            .Select(r => (PesajeResumen)r.Tag!)
            .ToList();

    internal PesajeResumen? PesajeSeleccionado
    {
        get
        {
            var fila = dgvHistorial.CurrentRow;
            if (fila is null && dgvHistorial.SelectedRows.Count > 0)
            {
                fila = dgvHistorial.SelectedRows[0];
            }

            return fila?.Tag as PesajeResumen;
        }
    }

    public FrmHistorialPesajes(
        IServicioPesajes servicioPesajes,
        IServicioImpresionBoletas servicioImpresion,
        IServicioParametros servicioParametros,
        ISesionActual sesion,
        TimeProvider timeProvider)
    {
        _servicioPesajes = servicioPesajes ?? throw new ArgumentNullException(nameof(servicioPesajes));
        _servicioImpresion = servicioImpresion ?? throw new ArgumentNullException(nameof(servicioImpresion));
        _servicioParametros = servicioParametros ?? throw new ArgumentNullException(nameof(servicioParametros));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        Text = "Historial de pesajes";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 600);
        ShowInTaskbar = false;

        ConstruirControles();

        Shown += async (_, _) => await RecargarAsync();
    }

    private DateTime HoyLocal() =>
        TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone).Date;

    private void ConstruirControles()
    {
        var lblDia = DisenoFiltros.Etiqueta("Día:");
        dtpDia = DisenoFiltros.Fecha(HoyLocal());
        dtpDia.ShowCheckBox = false;
        dtpDia.ValueChanged += (_, _) => SolicitarRecarga();

        btnActualizar = DisenoFiltros.Boton("&Actualizar (F5)", 130);
        btnActualizar.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
        btnActualizar.Click += (_, _) => SolicitarRecarga();

        btnHoy = DisenoFiltros.Boton("&Hoy", 80);
        btnHoy.Click += (_, _) =>
        {
            var hoy = HoyLocal();
            if (dtpDia.Value.Date == hoy)
            {
                SolicitarRecarga();
            }
            else
            {
                dtpDia.Value = hoy; // dispara la recarga
            }
        };

        var flujo = DisenoFiltros.CrearPanel();
        flujo.Controls.AddRange(new Control[] { lblDia, dtpDia, btnActualizar, btnHoy });
        var grupoFiltros = DisenoFiltros.CrearGrupo("Día a consultar", flujo);

        // Panel inferior: el resumen a la izquierda y los botones a la derecha, sin posiciones fijas.
        btnReimprimir = new Button
        {
            Text = "&Reimprimir (F9)",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(150, 32),
            Enabled = false,
            Visible = _sesion.TienePermiso(Permiso.ReimprimirBoleta)
        };
        btnReimprimir.Click += async (_, _) => await ReimprimirAsync();

        btnCerrar = new Button
        {
            Text = "Cerrar (Esc)",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(120, 32),
            DialogResult = DialogResult.Cancel
        };
        btnCerrar.Click += (_, _) => Close();

        var botones = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        botones.Controls.Add(btnReimprimir);
        botones.Controls.Add(btnCerrar);

        lblResumen = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            ForeColor = Color.DarkSlateBlue,
            Text = "Listo."
        };

        // Resumen a la izquierda (columna que se estira) y botones a la derecha (columna por contenido).
        var pnlInferior = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12, 8, 12, 8)
        };
        pnlInferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlInferior.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlInferior.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlInferior.Controls.Add(lblResumen, 0, 0);
        pnlInferior.Controls.Add(botones, 1, 0);

        dgvHistorial = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D
        };
        ConfigurarColumnas();
        dgvHistorial.SelectionChanged += (_, _) => ActualizarBotones();
        dgvHistorial.CurrentCellChanged += (_, _) => ActualizarBotones();

        Controls.Add(dgvHistorial);
        Controls.Add(pnlInferior);
        Controls.Add(grupoFiltros);

        AcceptButton = btnActualizar;
        CancelButton = btnCerrar;
    }

    private void ConfigurarColumnas()
    {
        // Columnas no ordenables: la fila seleccionada se resuelve por su Tag, pero el orden cronológico
        // (más reciente primero) es parte del contrato de la pantalla.
        var der = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        var centro = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
        var negrita = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

        var columnas = new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "colBoleta", HeaderText = "Boleta", Width = 80, DefaultCellStyle = der },
            new DataGridViewTextBoxColumn { Name = "colHora", HeaderText = "Hora", Width = 65, DefaultCellStyle = centro },
            new DataGridViewTextBoxColumn { Name = "colCodigo", HeaderText = "Código", Width = 70 },
            new DataGridViewTextBoxColumn { Name = "colGanadero", HeaderText = "Ganadero", Width = 170 },
            new DataGridViewTextBoxColumn { Name = "colSecuencia", HeaderText = "Sec.", Width = 55, DefaultCellStyle = centro },
            new DataGridViewTextBoxColumn { Name = "colCliente", HeaderText = "Cliente", Width = 170 },
            new DataGridViewTextBoxColumn { Name = "colFletero", HeaderText = "Fletero", Width = 140 },
            new DataGridViewTextBoxColumn { Name = "colDesposte", HeaderText = "Desposte", Width = 110 },
            new DataGridViewTextBoxColumn { Name = "colIzq", HeaderText = "Izq.", Width = 85, DefaultCellStyle = der },
            new DataGridViewTextBoxColumn { Name = "colDer", HeaderText = "Der.", Width = 85, DefaultCellStyle = der },
            new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Total", Width = 90, DefaultCellStyle = new DataGridViewCellStyle(der) { Font = negrita } },
            new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 90, DefaultCellStyle = centro },
            new DataGridViewTextBoxColumn { Name = "colEstado", HeaderText = "Estado", Width = 90, DefaultCellStyle = new DataGridViewCellStyle(centro) { Font = negrita } }
        };

        foreach (var c in columnas)
        {
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        dgvHistorial.Columns.AddRange(columnas);
    }

    /// <summary>
    /// Pide recargar el día elegido. Si ya hay una carga en curso, la nueva se encadena al terminar (en vez de
    /// perderse por la protección de reentrada de <c>EjecutarAsync</c>) y el token descarta la respuesta vieja.
    /// </summary>
    private void SolicitarRecarga()
    {
        if (_recargaEnCurso)
        {
            _recargaPendiente = true;
            return;
        }

        _ = RecargarAsync();
    }

    private async Task RecargarAsync()
    {
        if (_recargaEnCurso || IsDisposed)
        {
            return;
        }

        _recargaEnCurso = true;
        try
        {
            // El bucle está fuera de EjecutarAsync (regla 0.10): cada vuelta es una llamada independiente.
            do
            {
                _recargaPendiente = false;
                await EjecutarAsync(CargarInternoAsync, btnActualizar);
            }
            while (_recargaPendiente && !IsDisposed);
        }
        finally
        {
            _recargaEnCurso = false;
        }
    }

    private async Task CargarInternoAsync()
    {
        var dia = DateOnly.FromDateTime(dtpDia.Value);
        var version = ++_versionCarga;

        var lista = await _servicioPesajes.ListarPorDiaAsync(dia);
        var modo = await _servicioParametros.ObtenerModoDesposteAsync();
        if (IsDisposed || version != _versionCarga)
        {
            return;
        }

        // F11 (D6): la columna se oculta si todos los pesajes del día son del único tipo que pesa la empresa.
        dgvHistorial.Columns["colDesposte"]!.Visible = ReglasDesposte.MostrarDesposte(modo, lista.Select(p => p.TipoDesposte));
        MostrarFilas(lista);
    }

    private void MostrarFilas(IReadOnlyList<PesajeResumen> lista)
    {
        var zonaLocal = _timeProvider.LocalTimeZone;

        dgvHistorial.SuspendLayout();
        try
        {
            dgvHistorial.Rows.Clear();
            foreach (var p in lista)
            {
                var hora = TimeZoneInfo.ConvertTimeFromUtc(p.FechaHoraUtc, zonaLocal).ToString("HH:mm");
                var origen = p.Origen switch
                {
                    OrigenPeso.Simulador => "SIMULADO",
                    OrigenPeso.Importado => "IMPORTADA",
                    _ => string.Empty
                };

                var i = dgvHistorial.Rows.Add(
                    p.NumeroBoleta.ToString(),
                    hora,
                    p.Codigo,
                    p.NombreGanadero,
                    p.Secuencia.ToString(),
                    p.NombreCliente,
                    p.NombreFletero ?? string.Empty,
                    TextosBoleta.Desposte(p.TipoDesposte),
                    TextosBoleta.Peso(p.PesoIzquierdo),
                    TextosBoleta.Peso(p.PesoDerecho),
                    TextosBoleta.Peso(p.PesoTotal),
                    origen,
                    p.Estado == EstadoPesaje.Vigente ? "VIGENTE" : "ANULADA");

                var fila = dgvHistorial.Rows[i];
                fila.Tag = p;
                if (p.Estado == EstadoPesaje.Anulado)
                {
                    fila.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }

            if (dgvHistorial.Rows.Count > 0)
            {
                dgvHistorial.ClearSelection();
                dgvHistorial.Rows[0].Selected = true;
                dgvHistorial.CurrentCell = dgvHistorial.Rows[0].Cells[0];
            }
        }
        finally
        {
            dgvHistorial.ResumeLayout();
        }

        if (lista.Count >= ReglasPesaje.HistorialMaximo)
        {
            lblResumen.Text = $"Se muestran los últimos {ReglasPesaje.HistorialMaximo} pesajes del día.";
            lblResumen.ForeColor = Color.DarkOrange;
        }
        else if (lista.Count == 0)
        {
            lblResumen.Text = "No hay pesajes en el día elegido.";
            lblResumen.ForeColor = Color.DimGray;
        }
        else
        {
            var vigentes = lista.Count(p => p.Estado == EstadoPesaje.Vigente);
            var anuladas = lista.Count - vigentes;
            lblResumen.Text = anuladas == 0
                ? $"{lista.Count} pesaje(s)."
                : $"{lista.Count} pesaje(s): {vigentes} vigente(s) y {anuladas} anulada(s).";
            lblResumen.ForeColor = Color.DarkSlateBlue;
        }

        ActualizarBotones();
    }

    private void ActualizarBotones()
    {
        btnReimprimir.Enabled = !_imprimiendo && PesajeSeleccionado is not null && _sesion.TienePermiso(Permiso.ReimprimirBoleta);
    }

    private async Task ReimprimirAsync()
    {
        var seleccionado = PesajeSeleccionado;
        if (seleccionado is null || _imprimiendo || !_sesion.TienePermiso(Permiso.ReimprimirBoleta))
        {
            return;
        }

        ResultadoImpresion? resultado = null;
        _imprimiendo = true;
        dtpDia.Enabled = btnActualizar.Enabled = btnHoy.Enabled = false;
        ActualizarBotones();
        try
        {
            await EjecutarAsync(async () =>
                resultado = await _servicioImpresion.ImprimirAsync(seleccionado.Id, SolicitudImpresion.Manual), btnReimprimir);
        }
        finally
        {
            _imprimiendo = false;
            dtpDia.Enabled = btnActualizar.Enabled = btnHoy.Enabled = true;
            ActualizarBotones();
        }

        if (resultado is null)
        {
            return;
        }

        lblResumen.Text = resultado.Mensaje;
        lblResumen.ForeColor = resultado.Estado == EstadoImpresion.Impresa ? Color.DarkGreen : Color.DarkRed;
        if (resultado.Estado == EstadoImpresion.Fallida)
        {
            MostrarAdvertencia(resultado.Mensaje);
        }
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Con una impresión en curso no se cierra: el resultado se mostraría sobre un formulario desechado.
        if (_imprimiendo)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            if (btnActualizar.Enabled)
            {
                SolicitarRecarga();
            }
            return true;
        }

        if (keyData == Keys.F9)
        {
            if (btnReimprimir.Visible && btnReimprimir.Enabled)
            {
                _ = ReimprimirAsync();
            }
            return true;
        }

        // Enter en la grilla no debe disparar "Actualizar" (AcceptButton): se limita a no hacer nada.
        if (keyData == Keys.Enter && dgvHistorial.ContainsFocus)
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Ganchos para tests
    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task ReimprimirParaTestAsync() => ReimprimirAsync();
    internal bool ProcesarTeclaParaTest(Keys key)
    {
        var msg = default(Message);
        return ProcessCmdKey(ref msg, key);
    }
}

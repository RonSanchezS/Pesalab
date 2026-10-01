using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Utilitarios;

public class FrmImportarLegado : FormBase
{
    private readonly IServicioImportacion _servicio;
    private readonly TimeProvider _tiempo;

    private VistaOrigen? _vista;
    private ResultadoImportacion? _simulacro;
    private ResultadoImportacion? _ultimoResultado;
    private bool _cargandoMapeo;

    // Controles internos para pruebas
    internal readonly ComboBox cmbTipo;
    internal readonly TextBox txtArchivo;
    internal readonly Button btnExaminar;
    internal readonly ComboBox cmbTabla;
    internal readonly ComboBox cmbCodificacion;
    internal readonly Button btnLeer;
    internal readonly Label lblInfoArchivo;

    internal readonly DataGridView dgvMapeo;
    internal readonly DataGridView dgvMuestra;

    internal readonly Button btnSimular;
    internal readonly Button btnImportar;
    internal readonly Button btnInforme;
    internal readonly Label lblResumen;
    internal readonly DataGridView dgvIncidencias;

    internal readonly Button btnCerrar;

    // Ganchos de prueba
    public Func<string?>? ElegirArchivoParaTest { get; set; }
    public Func<string, bool>? ConfirmarImportacionParaTest { get; set; }
    public Func<string, string?>? ElegirRutaInformeParaTest { get; set; }
    public Action<string>? MostrarAdvertenciaParaTest { get; set; }
    public Action<string>? MostrarInformacionParaTest { get; set; }

    public FrmImportarLegado(IServicioImportacion servicio, TimeProvider tiempo)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));

        Text = "Importar datos legados";
        MinimumSize = new Size(1000, 700);
        Size = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterParent;

        var pnlPrincipal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        pnlPrincipal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 0: Archivo
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 1: Ayuda
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));  // 2: Columnas
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));  // 3: Simulacro
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 4: Cerrar

        // 1. Grupo Archivo
        var grpArchivo = new GroupBox
        {
            Text = "1. Archivo",
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 6),
            Margin = new Padding(0, 0, 0, 4)
        };

        var pnlArchivo = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3
        };
        pnlArchivo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlArchivo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlArchivo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlArchivo.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var pnlFila0 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        var lblTipo = new Label { Text = "Tipo:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        cmbTipo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Margin = new Padding(2, 4, 10, 4)
        };
        cmbTipo.Items.Add(new OpcionTipo("Ganaderos", TipoImportacion.Ganaderos));
        cmbTipo.Items.Add(new OpcionTipo("Clientes", TipoImportacion.Clientes));
        cmbTipo.Items.Add(new OpcionTipo("Pesajes (boletas)", TipoImportacion.Pesajes));
        cmbTipo.SelectedIndex = 0;

        var lblArchivo = new Label { Text = "Archivo:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        txtArchivo = new TextBox { ReadOnly = true, Width = 260, Margin = new Padding(2, 4, 4, 4) };
        btnExaminar = DisenoFiltros.Boton("&Examinar…", 95);

        pnlFila0.Controls.Add(lblTipo);
        pnlFila0.Controls.Add(cmbTipo);
        pnlFila0.Controls.Add(lblArchivo);
        pnlFila0.Controls.Add(txtArchivo);
        pnlFila0.Controls.Add(btnExaminar);

        var pnlFila1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 0)
        };

        var lblTabla = new Label { Text = "Tabla:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        cmbTabla = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Enabled = false,
            Margin = new Padding(2, 4, 10, 4)
        };

        var lblCod = new Label { Text = "Codificación:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        cmbCodificacion = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Margin = new Padding(2, 4, 10, 4)
        };
        cmbCodificacion.Items.Add(new OpcionCodificacion("Automática", null));
        cmbCodificacion.Items.Add(new OpcionCodificacion("UTF-8", 65001));
        cmbCodificacion.Items.Add(new OpcionCodificacion("Windows-1252", 1252));
        cmbCodificacion.Items.Add(new OpcionCodificacion("DOS 850", 850));
        cmbCodificacion.Items.Add(new OpcionCodificacion("DOS 437", 437));
        cmbCodificacion.SelectedIndex = 0;

        btnLeer = DisenoFiltros.Boton("&Leer archivo", 115);
        btnLeer.Enabled = false;

        pnlFila1.Controls.Add(lblTabla);
        pnlFila1.Controls.Add(cmbTabla);
        pnlFila1.Controls.Add(lblCod);
        pnlFila1.Controls.Add(cmbCodificacion);
        pnlFila1.Controls.Add(btnLeer);

        lblInfoArchivo = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(4, 4, 4, 4)
        };

        pnlArchivo.Controls.Add(pnlFila0, 0, 0);
        pnlArchivo.Controls.Add(pnlFila1, 0, 1);
        pnlArchivo.Controls.Add(lblInfoArchivo, 0, 2);

        grpArchivo.Controls.Add(pnlArchivo);

        void AjustarGrpArchivo()
        {
            grpArchivo.Height = pnlArchivo.Bottom + grpArchivo.Padding.Bottom + 4;
        }
        pnlArchivo.SizeChanged += (_, _) => AjustarGrpArchivo();
        pnlArchivo.LocationChanged += (_, _) => AjustarGrpArchivo();
        AjustarGrpArchivo();

        pnlPrincipal.Controls.Add(grpArchivo, 0, 0);

        // Ayuda fija
        var lblAyuda = new Label
        {
            Text = "Importe en este orden: 1) Ganaderos, 2) Clientes, 3) Pesajes. Antes de importar se crea una copia de seguridad automáticamente.",
            AutoSize = true,
            ForeColor = Color.DarkSlateGray,
            Font = new Font(Font, FontStyle.Italic),
            Margin = new Padding(4, 2, 4, 6)
        };
        pnlPrincipal.Controls.Add(lblAyuda, 0, 1);

        // 2. Grupo Columnas
        var grpColumnas = new GroupBox
        {
            Text = "2. Columnas",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 0, 0, 4)
        };

        var pnlColumnas = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        pnlColumnas.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430f));
        pnlColumnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlColumnas.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        dgvMapeo = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            EditMode = DataGridViewEditMode.EditOnEnter,
            BackgroundColor = SystemColors.Window
        };
        dgvMapeo.DataError += (_, e) => e.ThrowException = false;
        dgvMapeo.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (dgvMapeo.IsCurrentCellDirty)
            {
                dgvMapeo.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        dgvMapeo.CellValueChanged += (_, e) =>
        {
            if (!_cargandoMapeo && e.RowIndex >= 0 && e.ColumnIndex == 2)
            {
                OnMapeoCambiado();
            }
        };

        dgvMuestra = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = SystemColors.Window
        };

        pnlColumnas.Controls.Add(dgvMapeo, 0, 0);
        pnlColumnas.Controls.Add(dgvMuestra, 1, 0);
        grpColumnas.Controls.Add(pnlColumnas);
        pnlPrincipal.Controls.Add(grpColumnas, 0, 2);

        // 3. Grupo Simulacro e importación
        var grpSimulacro = new GroupBox
        {
            Text = "3. Simulacro e importación",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 0, 0, 4)
        };

        var pnlSimulacro = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        pnlSimulacro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlSimulacro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlSimulacro.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var pnlAccionesSimulacro = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4)
        };

        btnSimular = DisenoFiltros.Boton("&Simulacro", 110);
        btnSimular.Enabled = false;
        btnImportar = DisenoFiltros.Boton("&Importar", 110);
        btnImportar.Enabled = false;
        btnInforme = DisenoFiltros.Boton("Guardar &informe…", 150);
        btnInforme.Enabled = false;

        lblResumen = new Label
        {
            Text = "",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(12, 6, 4, 4)
        };

        pnlAccionesSimulacro.Controls.Add(btnSimular);
        pnlAccionesSimulacro.Controls.Add(btnImportar);
        pnlAccionesSimulacro.Controls.Add(btnInforme);
        pnlAccionesSimulacro.Controls.Add(lblResumen);

        dgvIncidencias = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = SystemColors.Window
        };
        ConfigurarColumnasIncidencias();

        pnlSimulacro.Controls.Add(pnlAccionesSimulacro, 0, 0);
        pnlSimulacro.Controls.Add(dgvIncidencias, 0, 1);
        grpSimulacro.Controls.Add(pnlSimulacro);
        pnlPrincipal.Controls.Add(grpSimulacro, 0, 3);

        // Barra inferior: Cerrar
        var pnlInferior = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 2, 0, 0)
        };
        btnCerrar = DisenoFiltros.Boton("Cerrar", 90);
        pnlInferior.Controls.Add(btnCerrar);
        pnlPrincipal.Controls.Add(pnlInferior, 0, 4);

        Controls.Add(pnlPrincipal);
        CancelButton = btnCerrar;

        // Eventos
        cmbTipo.SelectedIndexChanged += (_, _) => LimpiarDatos();
        cmbTabla.SelectedIndexChanged += (_, _) => { LimpiarDatos(); ActualizarAcciones(); };
        cmbCodificacion.SelectedIndexChanged += (_, _) => LimpiarDatos();

        btnExaminar.Click += async (_, _) => await ExaminarInternoAsync();
        btnLeer.Click += async (_, _) => await LeerInternoAsync();
        btnSimular.Click += async (_, _) => await SimularInternoAsync();
        btnImportar.Click += async (_, _) => await ImportarInternoAsync();
        btnInforme.Click += (_, _) => GuardarInformeInterno();
        btnCerrar.Click += (_, _) => Close();
    }

    private void ConfigurarColumnasIncidencias()
    {
        dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFila", HeaderText = "Fila", Width = 65 });
        dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNivel", HeaderText = "Nivel", Width = 95 });
        dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCampo", HeaderText = "Campo", Width = 140 });
        dgvIncidencias.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colMensaje",
            HeaderText = "Mensaje",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    private void OnMapeoCambiado()
    {
        if (_simulacro != null)
        {
            _simulacro = null;
            lblResumen.Text = "El mapeo cambió: vuelva a ejecutar el simulacro.";
            ActualizarAcciones();
        }
    }

    private void LimpiarDatos()
    {
        _vista = null;
        _simulacro = null;
        _ultimoResultado = null;

        dgvMapeo.Rows.Clear();
        dgvMapeo.Columns.Clear();

        dgvMuestra.Rows.Clear();
        dgvMuestra.Columns.Clear();

        dgvIncidencias.Rows.Clear();
        lblInfoArchivo.Text = string.Empty;
        lblResumen.Text = string.Empty;

        ActualizarAcciones();
    }

    private void ActualizarAcciones()
    {
        var tieneArchivo = !string.IsNullOrWhiteSpace(txtArchivo.Text);
        var tablaOk = !cmbTabla.Enabled || cmbTabla.SelectedItem != null;

        btnLeer.Enabled = tieneArchivo && tablaOk;
        btnSimular.Enabled = _vista != null;
        btnImportar.Enabled = _simulacro != null && _simulacro.FilasAImportar > 0;
        btnInforme.Enabled = _ultimoResultado != null;
    }

    private TipoImportacion TipoSeleccionado() =>
        cmbTipo.SelectedItem is OpcionTipo opc ? opc.Valor : TipoImportacion.Ganaderos;

    private int? CodificacionSeleccionada() =>
        cmbCodificacion.SelectedItem is OpcionCodificacion opc ? opc.Codigo : null;

    private async Task ExaminarInternoAsync()
    {
        string? ruta;
        if (ElegirArchivoParaTest != null)
        {
            ruta = ElegirArchivoParaTest();
        }
        else
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Datos legados (*.csv;*.txt;*.dbf;*.mdb;*.accdb)|*.csv;*.txt;*.dbf;*.mdb;*.accdb|Todos los archivos (*.*)|*.*",
                Title = "Seleccionar archivo de datos legados"
            };
            ruta = dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        var formato = FormatosOrigen.DesdeExtension(ruta);
        if (formato == null)
        {
            MostrarAdvertencia("Tipo de archivo no soportado. Use CSV, TXT, DBF, MDB o ACCDB.");
            return;
        }

        LimpiarDatos();
        txtArchivo.Text = ruta;

        if (formato == FormatoOrigen.Access)
        {
            cmbCodificacion.Enabled = false;
            cmbTabla.Enabled = true;
            cmbTabla.Items.Clear();

            IReadOnlyList<string>? tablas = null;
            string? errorAccess = null;
            await EjecutarAsync(async () =>
            {
                try
                {
                    tablas = await _servicio.ListarTablasAccessAsync(ruta);
                }
                catch (DomainException ex)
                {
                    errorAccess = ex.Message;
                }
            });

            if (errorAccess != null)
            {
                MostrarAdvertencia(errorAccess);
                return;
            }

            if (tablas != null)
            {
                foreach (var t in tablas)
                {
                    cmbTabla.Items.Add(t);
                }
                if (cmbTabla.Items.Count > 0)
                {
                    cmbTabla.SelectedIndex = 0;
                }
            }
        }
        else
        {
            cmbTabla.Items.Clear();
            cmbTabla.Enabled = false;
            cmbCodificacion.Enabled = true;
        }

        ActualizarAcciones();
    }

    private async Task LeerInternoAsync()
    {
        var ruta = txtArchivo.Text;
        var formato = FormatosOrigen.DesdeExtension(ruta);
        if (formato == null)
        {
            return;
        }

        var tipo = TipoSeleccionado();
        var codificacion = CodificacionSeleccionada();
        var tabla = cmbTabla.Enabled && cmbTabla.SelectedItem != null ? cmbTabla.SelectedItem.ToString() : null;

        var solicitud = new SolicitudLectura(ruta, formato.Value, tabla, codificacion);

        VistaOrigen? vista = null;
        string? errorLeer = null;
        await EjecutarAsync(async () =>
        {
            try
            {
                vista = await _servicio.LeerOrigenAsync(solicitud, tipo);
            }
            catch (DomainException ex)
            {
                errorLeer = ex.Message;
            }
        });

        if (errorLeer != null)
        {
            MostrarAdvertencia(errorLeer);
            return;
        }

        if (vista == null)
        {
            return;
        }

        _vista = vista;
        _simulacro = null;
        _ultimoResultado = null;
        dgvIncidencias.Rows.Clear();
        lblResumen.Text = string.Empty;

        // Info archivo
        var sbInfo = new StringBuilder();
        sbInfo.Append($"{vista.TotalFilas} filas · codificación {vista.Codificacion}");
        foreach (var adv in vista.Advertencias)
        {
            sbInfo.Append($"\n{adv}");
        }
        lblInfoArchivo.Text = sbInfo.ToString();

        // Mapeo
        _cargandoMapeo = true;
        try
        {
            dgvMapeo.Rows.Clear();
            dgvMapeo.Columns.Clear();

            dgvMapeo.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCampo", HeaderText = "Campo", ReadOnly = true, Width = 140 });
            dgvMapeo.Columns.Add(new DataGridViewTextBoxColumn { Name = "colObligatorio", HeaderText = "Obligatorio", ReadOnly = true, Width = 80 });

            var colCombo = new DataGridViewComboBoxColumn
            {
                Name = "colColumna",
                HeaderText = "Columna",
                Width = 190,
                FlatStyle = FlatStyle.Flat,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
            colCombo.Items.Add("(sin asignar)");
            foreach (var col in vista.Columnas)
            {
                colCombo.Items.Add(col);
            }
            dgvMapeo.Columns.Add(colCombo);

            var campos = CamposImportacion.De(tipo);
            foreach (var campo in campos)
            {
                var rowIdx = dgvMapeo.Rows.Add();
                var row = dgvMapeo.Rows[rowIdx];
                row.Tag = campo.Clave;
                row.Cells["colCampo"].Value = campo.Titulo;
                row.Cells["colObligatorio"].Value = campo.Obligatorio ? "Sí" : "";

                string valorCombo = "(sin asignar)";
                if (vista.MapeoSugerido.TryGetValue(campo.Clave, out var colIdx) && colIdx.HasValue && colIdx.Value >= 0 && colIdx.Value < vista.Columnas.Count)
                {
                    valorCombo = vista.Columnas[colIdx.Value];
                }
                row.Cells["colColumna"].Value = valorCombo;
            }
        }
        finally
        {
            _cargandoMapeo = false;
        }

        // Muestra
        dgvMuestra.Rows.Clear();
        dgvMuestra.Columns.Clear();
        foreach (var col in vista.Columnas)
        {
            dgvMuestra.Columns.Add(new DataGridViewTextBoxColumn { Name = col, HeaderText = col, Width = 120 });
        }
        foreach (var fila in vista.Muestra)
        {
            var vals = new object?[fila.Valores.Count];
            for (var i = 0; i < fila.Valores.Count; i++)
            {
                vals[i] = fila.Valores[i] ?? "";
            }
            dgvMuestra.Rows.Add(vals);
        }

        ActualizarAcciones();
    }

    private async Task SimularInternoAsync()
    {
        if (_vista == null)
        {
            return;
        }

        var ruta = txtArchivo.Text;
        var formato = FormatosOrigen.DesdeExtension(ruta);
        if (formato == null)
        {
            return;
        }

        var tipo = TipoSeleccionado();
        var codificacion = CodificacionSeleccionada();
        var tabla = cmbTabla.Enabled && cmbTabla.SelectedItem != null ? cmbTabla.SelectedItem.ToString() : null;
        var lectura = new SolicitudLectura(ruta, formato.Value, tabla, codificacion);

        var mapeo = MapeoActual();
        var solicitud = new SolicitudImportacion(lectura, tipo, mapeo);

        ResultadoImportacion? resultado = null;
        string? errorSimular = null;
        await EjecutarAsync(async () =>
        {
            try
            {
                resultado = await _servicio.SimularAsync(solicitud);
            }
            catch (ValidacionException vex)
            {
                errorSimular = string.Join("\n", vex.Errores.Select(e => e.Mensaje));
            }
            catch (DomainException dex)
            {
                errorSimular = dex.Message;
            }
        });

        if (errorSimular != null)
        {
            MostrarAdvertencia(errorSimular);
            return;
        }

        if (resultado == null)
        {
            return;
        }

        _simulacro = resultado;
        _ultimoResultado = resultado;

        var sbResumen = new StringBuilder();
        sbResumen.Append($"Simulacro: {resultado.FilasLeidas} filas leídas; se importarían {resultado.FilasAImportar}; {resultado.FilasOmitidas} ya existentes; {resultado.FilasConError} con errores (no se importan); {resultado.FilasConAdvertencia} con advertencias.");
        if (resultado.IncidenciasTruncadas)
        {
            sbResumen.Append(" Se muestran las primeras 5.000 incidencias.");
        }
        lblResumen.Text = sbResumen.ToString();

        PoblarIncidencias(resultado.Incidencias);
        ActualizarAcciones();
    }

    private async Task ImportarInternoAsync()
    {
        if (_simulacro == null || _simulacro.FilasAImportar <= 0)
        {
            return;
        }

        var tipo = TipoSeleccionado();
        var tipoNombre = tipo switch
        {
            TipoImportacion.Ganaderos => "ganaderos",
            TipoImportacion.Clientes => "clientes",
            TipoImportacion.Pesajes => "pesajes (boletas)",
            _ => tipo.ToString().ToLowerInvariant()
        };

        var textoConfirmacion = $"Se importarán {_simulacro.FilasAImportar} fila(s) de {tipoNombre}. {_simulacro.FilasConError} fila(s) con errores y {_simulacro.FilasOmitidas} ya existente(s) no se importarán. Antes se creará una copia de seguridad. ¿Continuar?";

        bool confirma;
        if (ConfirmarImportacionParaTest != null)
        {
            confirma = ConfirmarImportacionParaTest(textoConfirmacion);
        }
        else
        {
            confirma = MessageBox.Show(this, textoConfirmacion, "Confirmar importación", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        if (!confirma)
        {
            return;
        }

        var ruta = txtArchivo.Text;
        var formato = FormatosOrigen.DesdeExtension(ruta);
        if (formato == null)
        {
            return;
        }

        var codificacion = CodificacionSeleccionada();
        var tabla = cmbTabla.Enabled && cmbTabla.SelectedItem != null ? cmbTabla.SelectedItem.ToString() : null;
        var lectura = new SolicitudLectura(ruta, formato.Value, tabla, codificacion);
        var mapeo = MapeoActual();
        var solicitud = new SolicitudImportacion(lectura, tipo, mapeo);

        ResultadoImportacion? resultado = null;
        string? errorImportar = null;
        bool fueCambio = false;
        await EjecutarAsync(async () =>
        {
            try
            {
                resultado = await _servicio.ImportarAsync(solicitud, _simulacro.Huella);
            }
            catch (ReglaNegocioException rex) when (rex.Codigo == "IMP_CAMBIO")
            {
                fueCambio = true;
                errorImportar = rex.Message;
            }
            catch (DomainException dex)
            {
                errorImportar = dex.Message;
            }
        });

        if (fueCambio)
        {
            _simulacro = null;
            ActualizarAcciones();
        }

        if (errorImportar != null)
        {
            MostrarAdvertencia(errorImportar);
            return;
        }

        if (resultado == null)
        {
            return;
        }

        _simulacro = null;
        _ultimoResultado = resultado;

        lblResumen.Text = $"Importación terminada: {resultado.Insertadas} fila(s) importada(s); {resultado.FilasOmitidas} ya existentes; {resultado.FilasConError} con errores. Copia previa: {resultado.BackupPrevio}.";
        PoblarIncidencias(resultado.Incidencias);
        ActualizarAcciones();

        MostrarInformacion(lblResumen.Text);
    }

    private void GuardarInformeInterno()
    {
        if (_ultimoResultado == null)
        {
            return;
        }

        var tipo = TipoSeleccionado();
        var local = _tiempo.GetLocalNow();
        var sugerido = $"Importacion_{tipo}_{local:yyyyMMdd_HHmm}.csv";

        string? ruta;
        if (ElegirRutaInformeParaTest != null)
        {
            ruta = ElegirRutaInformeParaTest(sugerido);
        }
        else
        {
            using var dlg = new SaveFileDialog
            {
                FileName = sugerido,
                Filter = "Archivos CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                Title = "Guardar informe de importación"
            };
            ruta = dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        try
        {
            var nombreOrigen = Path.GetFileName(txtArchivo.Text);
            var bytes = InformeImportacion.GenerarCsv(_ultimoResultado, nombreOrigen);
            File.WriteAllBytes(ruta, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MostrarAdvertencia("No se pudo guardar el informe. Ciérrelo si está abierto en otro programa.");
        }
    }

    private void PoblarIncidencias(IReadOnlyList<Incidencia> incidencias)
    {
        dgvIncidencias.Rows.Clear();
        foreach (var inc in incidencias)
        {
            var idx = dgvIncidencias.Rows.Add(inc.Fila, inc.Nivel.ToString(), inc.Campo ?? "", inc.Mensaje);
            var row = dgvIncidencias.Rows[idx];
            if (inc.Nivel == NivelIncidencia.Error)
            {
                row.DefaultCellStyle.BackColor = Color.MistyRose;
            }
            else if (inc.Nivel == NivelIncidencia.Advertencia)
            {
                row.DefaultCellStyle.BackColor = Color.LightYellow;
            }
        }
    }

    internal IReadOnlyDictionary<string, int?> MapeoActual()
    {
        var resultado = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (DataGridViewRow fila in dgvMapeo.Rows)
        {
            if (fila.Tag is string clave)
            {
                var valor = fila.Cells["colColumna"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(valor) || valor == "(sin asignar)")
                {
                    resultado[clave] = null;
                }
                else if (_vista != null)
                {
                    var idx = -1;
                    for (var i = 0; i < _vista.Columnas.Count; i++)
                    {
                        if (string.Equals(_vista.Columnas[i], valor, StringComparison.Ordinal))
                        {
                            idx = i;
                            break;
                        }
                    }
                    resultado[clave] = idx >= 0 ? idx : null;
                }
                else
                {
                    resultado[clave] = null;
                }
            }
        }
        return resultado;
    }

    private void MostrarAdvertencia(string mensaje)
    {
        if (MostrarAdvertenciaParaTest != null)
        {
            MostrarAdvertenciaParaTest(mensaje);
        }
        else
        {
            MessageBox.Show(this, mensaje, "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MostrarInformacion(string mensaje)
    {
        if (MostrarInformacionParaTest != null)
        {
            MostrarInformacionParaTest(mensaje);
        }
        else
        {
            MessageBox.Show(this, mensaje, "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // Entradas para tests
    internal Task ExaminarParaTestAsync() => ExaminarInternoAsync();
    internal Task LeerParaTestAsync() => LeerInternoAsync();
    internal Task SimularParaTestAsync() => SimularInternoAsync();
    internal Task ImportarParaTestAsync() => ImportarInternoAsync();
    internal void GuardarInformeParaTest() => GuardarInformeInterno();
    internal IReadOnlyDictionary<string, int?> MapeoActualParaTest() => MapeoActual();

    private sealed record OpcionTipo(string Nombre, TipoImportacion Valor)
    {
        public override string ToString() => Nombre;
    }

    private sealed record OpcionCodificacion(string Nombre, int? Codigo)
    {
        public override string ToString() => Nombre;
    }
}

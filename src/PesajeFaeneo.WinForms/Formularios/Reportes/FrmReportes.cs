using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Impresion;
using PesajeFaeneo.WinForms.Infraestructura;
using Serilog;

namespace PesajeFaeneo.WinForms.Formularios.Reportes;

public class FrmReportes : FormBase
{
    private readonly IServicioReportes _servicio;
    private readonly IGeneradorPdfReportes _pdf;
    private readonly IGeneradorExcelReportes _excel;
    private readonly TimeProvider _timeProvider;

    private readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controles;

    private int _versionGeneracion;
    private ReportePesajes? _reporte;
    private bool? _columnasAnuladas;
    private bool? _columnasConDesposte;   // F11: la columna "Tipo Corte" depende de cada reporte generado
    private bool _inicializando = true;
    private bool _sincronizandoFechas;
    private bool _cargandoRazas;

    // Exportar/Vista previa/Imprimir muestran un diálogo ANTES de entrar en EjecutarAsync (regla 0.10). Mientras una de
    // esas acciones sigue en curso, otra no puede abrir su propio diálogo: el EjecutarAsync posterior se descartaría en
    // silencio (FormBase ocupado) y el usuario creería haber guardado un archivo que nunca se escribió.
    private bool _accionEnCurso;

    // Controles internal para pruebas
    internal readonly ComboBox cmbTipo;
    internal readonly DateTimePicker dtpDesde;
    internal readonly DateTimePicker dtpHasta;
    internal readonly Button btnGenerar;
    internal readonly ControlBusquedaCatalogo ctlGanadero;
    internal readonly ControlBusquedaCatalogo ctlCliente;
    internal readonly ComboBox cmbRaza;
    internal readonly DataGridView dgvVista;
    internal readonly Label lblTotales;
    internal readonly Label lblAviso;
    internal readonly Button btnVistaPrevia;
    internal readonly Button btnImprimir;
    internal readonly Button btnPdf;
    internal readonly Button btnExcel;
    internal readonly Button btnCerrar;

    // Ganchos para test
    internal Func<string, string, string?>? ElegirRutaGuardarParaTest { get; set; }
    internal Func<string, bool>? PreguntarAbrirParaTest { get; set; }
    internal Action<string>? AbrirArchivoParaTest { get; set; }
    internal Func<PrintDocument, bool>? ElegirImpresoraParaTest { get; set; }
    internal Action<PrintDocument>? EnviarAImpresoraParaTest { get; set; }
    internal Func<Form, DialogResult>? MostrarDialogoParaTest { get; set; }
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }

    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task GenerarParaTestAsync() => GenerarInternoAsync();
    internal Task ExportarPdfParaTestAsync() => ExportarAsync(pdf: true);
    internal Task ExportarExcelParaTestAsync() => ExportarAsync(pdf: false);
    internal Task VistaPreviaParaTestAsync() => VistaPreviaAsync();
    internal Task ImprimirParaTestAsync() => ImprimirAsync();
    internal ReportePesajes? ReporteActual => _reporte;
    internal string ErrorDe(Control control) => _errorProvider.GetError(control);
    internal bool ProcesarTeclaParaTest(Keys keyData)
    {
        var msg = default(Message);
        return ProcessCmdKey(ref msg, keyData);
    }

    private sealed record OpcionTipo(TipoReporte Tipo, string Texto)
    {
        public override string ToString() => Texto;
    }

    public FrmReportes(
        IServicioReportes servicio,
        IGeneradorPdfReportes pdf,
        IGeneradorExcelReportes excel,
        TimeProvider timeProvider)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
        _excel = excel ?? throw new ArgumentNullException(nameof(excel));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        Text = "Reportes";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 600);

        var hoyLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone));
        var fechaLocalDt = hoyLocal.ToDateTime(TimeOnly.MinValue);

        // Grupo filtros
        var panelFiltros = DisenoFiltros.CrearPanel();
        var grpFiltros = DisenoFiltros.CrearGrupo("Filtros del reporte", panelFiltros);

        cmbTipo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200
        };
        cmbTipo.Items.Add(new OpcionTipo(TipoReporte.PesajesDelDia, TextosReporte.NombreTipo(TipoReporte.PesajesDelDia)));
        cmbTipo.Items.Add(new OpcionTipo(TipoReporte.PesajesEntreFechas, TextosReporte.NombreTipo(TipoReporte.PesajesEntreFechas)));
        cmbTipo.Items.Add(new OpcionTipo(TipoReporte.PorGanadero, TextosReporte.NombreTipo(TipoReporte.PorGanadero)));
        cmbTipo.Items.Add(new OpcionTipo(TipoReporte.PorCliente, TextosReporte.NombreTipo(TipoReporte.PorCliente)));
        cmbTipo.Items.Add(new OpcionTipo(TipoReporte.PorRaza, TextosReporte.NombreTipo(TipoReporte.PorRaza)));
        cmbTipo.Items.Add(new OpcionTipo(TipoReporte.Anuladas, TextosReporte.NombreTipo(TipoReporte.Anuladas)));
        cmbTipo.SelectedIndex = 0;

        dtpDesde = DisenoFiltros.Fecha(fechaLocalDt);
        dtpDesde.ShowCheckBox = false;

        dtpHasta = DisenoFiltros.Fecha(fechaLocalDt);
        dtpHasta.ShowCheckBox = false;

        btnGenerar = DisenoFiltros.Boton("&Generar (Enter)", 130);
        btnGenerar.Font = new Font(btnGenerar.Font, FontStyle.Bold);

        ctlGanadero = new ControlBusquedaCatalogo
        {
            Width = 300,
            Placeholder = "Seleccione…"
        };

        ctlCliente = new ControlBusquedaCatalogo
        {
            Width = 300,
            Placeholder = "Seleccione…"
        };

        // F10 (O8): razas (Color/Raza) existentes en los pesajes; solo se usa en "Pesajes por raza".
        cmbRaza = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Anchor = AnchorStyles.Left
        };

        panelFiltros.Controls.Add(DisenoFiltros.Etiqueta("Tipo de reporte:"));
        panelFiltros.Controls.Add(cmbTipo);
        panelFiltros.Controls.Add(DisenoFiltros.Etiqueta("Desde:"));
        panelFiltros.Controls.Add(dtpDesde);
        panelFiltros.Controls.Add(DisenoFiltros.Etiqueta("Hasta:"));
        panelFiltros.Controls.Add(dtpHasta);
        panelFiltros.Controls.Add(btnGenerar);
        panelFiltros.SetFlowBreak(btnGenerar, true);

        panelFiltros.Controls.Add(DisenoFiltros.Etiqueta("Ganadero:"));
        panelFiltros.Controls.Add(ctlGanadero);
        panelFiltros.Controls.Add(DisenoFiltros.Etiqueta("Cliente:"));
        panelFiltros.Controls.Add(ctlCliente);
        panelFiltros.Controls.Add(DisenoFiltros.Etiqueta("Raza:"));
        panelFiltros.Controls.Add(cmbRaza);

        // Grilla
        dgvVista = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false
        };

        // Panel de totales
        var panelTotales = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 4, 10, 4)
        };

        lblTotales = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = ""
        };

        lblAviso = new Label
        {
            AutoSize = true,
            ForeColor = Color.DarkOrange,
            Text = ""
        };

        panelTotales.Controls.Add(lblTotales);
        panelTotales.Controls.Add(lblAviso);

        // Panel de acciones
        var panelAcciones = DisenoFiltros.CrearPanel();
        panelAcciones.Dock = DockStyle.Bottom;

        btnVistaPrevia = DisenoFiltros.Boton("&Vista previa", 120);
        btnImprimir = DisenoFiltros.Boton("&Imprimir…", 120);
        btnPdf = DisenoFiltros.Boton("Exportar &PDF…", 120);
        btnExcel = DisenoFiltros.Boton("Exportar &Excel…", 120);
        btnCerrar = DisenoFiltros.Boton("Cerrar (Esc)", 120);
        btnCerrar.DialogResult = DialogResult.Cancel;
        btnCerrar.Click += (_, _) => Close();

        panelAcciones.Controls.Add(btnVistaPrevia);
        panelAcciones.Controls.Add(btnImprimir);
        panelAcciones.Controls.Add(btnPdf);
        panelAcciones.Controls.Add(btnExcel);
        panelAcciones.Controls.Add(btnCerrar);

        // ErrorProvider y mapa de controles
        _errorProvider = new ErrorProvider { ContainerControl = this };
        _controles = new Dictionary<string, Control>
        {
            [CamposReportes.Tipo] = cmbTipo,
            [CamposReportes.FechaDesde] = dtpDesde,
            [CamposReportes.FechaHasta] = dtpHasta,
            [CamposReportes.Ganadero] = ctlGanadero,
            [CamposReportes.Cliente] = ctlCliente,
            [CamposReportes.Raza] = cmbRaza
        };

        // Agregar en orden específico (DGV, Totales, Acciones, Filtros)
        Controls.Add(dgvVista);
        Controls.Add(panelTotales);
        Controls.Add(panelAcciones);
        Controls.Add(grpFiltros);

        AcceptButton = btnGenerar;
        CancelButton = btnCerrar;

        // Configurar estado inicial
        ActualizarControlesPorTipo();
        ActualizarAcciones();

        // Eventos
        cmbTipo.SelectedIndexChanged += (_, _) =>
        {
            ActualizarControlesPorTipo();
            if (!_inicializando)
            {
                InvalidarReporte();
            }
        };

        dtpDesde.ValueChanged += (_, _) =>
        {
            if (TipoSeleccionado == TipoReporte.PesajesDelDia)
            {
                _sincronizandoFechas = true;
                try
                {
                    dtpHasta.Value = dtpDesde.Value;
                }
                finally
                {
                    _sincronizandoFechas = false;
                }
            }
            if (!_inicializando)
            {
                InvalidarReporte();
            }
        };

        dtpHasta.ValueChanged += (_, _) =>
        {
            if (!_inicializando && !_sincronizandoFechas)
            {
                InvalidarReporte();
            }
        };

        ctlGanadero.ElementoSeleccionado += (_, _) =>
        {
            if (!_inicializando)
            {
                InvalidarReporte();
            }
        };
        ctlGanadero.SeleccionBorrada += (_, _) =>
        {
            if (!_inicializando)
            {
                InvalidarReporte();
            }
        };

        ctlCliente.ElementoSeleccionado += (_, _) =>
        {
            if (!_inicializando)
            {
                InvalidarReporte();
            }
        };
        ctlCliente.SeleccionBorrada += (_, _) =>
        {
            if (!_inicializando)
            {
                InvalidarReporte();
            }
        };

        cmbRaza.SelectedIndexChanged += (_, _) =>
        {
            if (!_inicializando && !_cargandoRazas)
            {
                InvalidarReporte();
            }
        };

        btnGenerar.Click += async (_, _) => await EjecutarAsync(GenerarInternoAsync, btnGenerar);
        btnPdf.Click += async (_, _) => await ExportarAsync(pdf: true);
        btnExcel.Click += async (_, _) => await ExportarAsync(pdf: false);
        btnVistaPrevia.Click += async (_, _) => await VistaPreviaAsync();
        btnImprimir.Click += async (_, _) => await ImprimirAsync();

        Shown += async (_, _) => await EjecutarAsync(CargarInternoAsync);

        _inicializando = false;
    }

    public void Configurar(TipoReporte tipo)
    {
        for (int i = 0; i < cmbTipo.Items.Count; i++)
        {
            if (cmbTipo.Items[i] is OpcionTipo opc && opc.Tipo == tipo)
            {
                cmbTipo.SelectedIndex = i;
                break;
            }
        }
    }

    private TipoReporte TipoSeleccionado =>
        (cmbTipo.SelectedItem as OpcionTipo)?.Tipo ?? TipoReporte.PesajesDelDia;

    private void ActualizarControlesPorTipo()
    {
        var tipo = TipoSeleccionado;
        dtpHasta.Enabled = tipo != TipoReporte.PesajesDelDia;
        ctlGanadero.Enabled = tipo == TipoReporte.PorGanadero;
        ctlCliente.Enabled = tipo == TipoReporte.PorCliente;
        cmbRaza.Enabled = tipo == TipoReporte.PorRaza;

        if (tipo == TipoReporte.PesajesDelDia)
        {
            // Sincronización interna: no es un cambio de filtro del usuario. Sin esta bandera, el ValueChanged de
            // dtpHasta invalidaba primero (con aviso) y luego el cambio de tipo volvía a invalidar con _reporte ya
            // nulo, borrando el aviso "Los filtros cambiaron…".
            _sincronizandoFechas = true;
            try
            {
                dtpHasta.Value = dtpDesde.Value;
            }
            finally
            {
                _sincronizandoFechas = false;
            }
        }
    }

    private void InvalidarReporte()
    {
        _versionGeneracion++;
        bool habiaReporte = _reporte is not null;
        LimpiarVista();
        if (habiaReporte)
        {
            lblAviso.Text = "Los filtros cambiaron: presione Generar para actualizar el reporte.";
        }
    }

    private void LimpiarVista()
    {
        _reporte = null;
        dgvVista.Rows.Clear();
        lblTotales.Text = "";
        lblAviso.Text = "";
        ActualizarAcciones();
    }

    private void ActualizarAcciones()
    {
        bool hay = _reporte is { Filas.Count: > 0 };
        btnPdf.Enabled = hay;
        btnExcel.Enabled = hay;
        btnVistaPrevia.Enabled = hay && _reporte!.Filas.Count <= ReglasReportes.MaximoFilasImpresion;
        btnImprimir.Enabled = hay && _reporte!.Filas.Count <= ReglasReportes.MaximoFilasImpresion;
    }

    private FiltroReporte ConstruirFiltro()
    {
        var tipo = TipoSeleccionado;
        return new FiltroReporte(
            tipo,
            DateOnly.FromDateTime(dtpDesde.Value),
            DateOnly.FromDateTime(dtpHasta.Value),
            tipo == TipoReporte.PorGanadero ? ctlGanadero.Seleccion?.Id : null,
            tipo == TipoReporte.PorCliente ? ctlCliente.Seleccion?.Id : null,
            tipo == TipoReporte.PorRaza ? cmbRaza.SelectedItem as string : null);
    }

    private void EstablecerRazas(IReadOnlyList<string> razas)
    {
        _cargandoRazas = true;
        try
        {
            var seleccionada = cmbRaza.SelectedItem as string;
            cmbRaza.Items.Clear();
            foreach (var raza in razas)
            {
                cmbRaza.Items.Add(raza);
            }

            // Conserva la raza elegida si sigue existiendo; si no, queda sin seleccionar (el servicio pide elegirla).
            cmbRaza.SelectedItem = seleccionada is not null && razas.Contains(seleccionada) ? seleccionada : null;
        }
        finally
        {
            _cargandoRazas = false;
        }
    }

    private async Task CargarInternoAsync()
    {
        var opciones = await _servicio.ObtenerOpcionesAsync();
        ctlGanadero.EstablecerElementos(opciones.Ganaderos);
        ctlCliente.EstablecerElementos(opciones.Clientes);
        EstablecerRazas(opciones.Razas ?? Array.Empty<string>());
    }

    private async Task GenerarInternoAsync()
    {
        _errorProvider.Clear();
        var filtro = ConstruirFiltro();
        int version = ++_versionGeneracion;
        LimpiarVista();

        ReportePesajes reporte;
        try
        {
            reporte = await _servicio.GenerarAsync(filtro);
        }
        catch (ValidacionException) when (version != _versionGeneracion)
        {
            // Respuesta tardía de una generación ya invalidada (R28): no pintar errores ni robar el foco sobre
            // filtros que el usuario cambió después.
            return;
        }
        catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
        {
            LimpiarVista();
            return;
        }

        if (version != _versionGeneracion)
        {
            return;
        }

        MostrarReporte(reporte);
    }

    private void MostrarReporte(ReportePesajes reporte)
    {
        _reporte = reporte;

        if (_columnasAnuladas != reporte.EsAnuladas || _columnasConDesposte != reporte.MostrarDesposte || dgvVista.Columns.Count == 0)
        {
            ReconstruirColumnas(reporte.EsAnuladas, reporte.MostrarDesposte);
        }

        var columnas = ColumnasReporte.Para(reporte.EsAnuladas, reporte.MostrarDesposte);
        int idxFecha = -1;
        for (int i = 0; i < columnas.Count; i++)
        {
            if (columnas[i].Clave == "Fecha")
            {
                idxFecha = i;
                break;
            }
        }

        dgvVista.SuspendLayout();
        dgvVista.Rows.Clear();

        foreach (var fila in reporte.Filas.Take(ReglasReportes.MaximoFilasVista))
        {
            var textos = ColumnasReporte.Textos(fila, reporte.EsAnuladas, reporte.MostrarDesposte).ToArray();
            if (idxFecha >= 0)
            {
                textos[idxFecha] = TextosReporte.FechaHora(fila.FechaHoraLocal);
            }

            int idxFila = dgvVista.Rows.Add(textos);
            dgvVista.Rows[idxFila].Tag = fila;
        }

        dgvVista.ResumeLayout();

        var t = reporte.Totales;
        if (!reporte.EsAnuladas)
        {
            lblTotales.Text = TextosReporte.TotalPesoNeto(t) + "    " +
                              TextosReporte.CantidadReses(t) + "    " +
                              TextosReporte.DetalleBoletas(t, reporte.MostrarDesposte);
        }
        else
        {
            lblTotales.Text = TextosReporte.BoletasAnuladas(t) + "    " +
                              TextosReporte.PesoNetoAnulado(t);
        }

        var avisos = new List<string>();
        if (reporte.Filas.Count == 0)
        {
            avisos.Add(TextosReporte.SinBoletas);
        }
        if (reporte.Filas.Count > ReglasReportes.MaximoFilasVista)
        {
            avisos.Add("La vista muestra las primeras 1.000 boletas; el PDF y el Excel incluyen las " +
                       TextosReporte.Entero(reporte.Filas.Count) + ".");
        }
        if (reporte.Filas.Count > ReglasReportes.MaximoFilasImpresion)
        {
            avisos.Add("Para imprimir más de 2.000 boletas, exporte a PDF e imprímalo desde el lector de PDF.");
        }
        var notaSimuladas = TextosReporte.NotaSimuladas(reporte.Totales.Simuladas);
        if (notaSimuladas is not null)
        {
            avisos.Add(notaSimuladas);
        }
        if (reporte.RutaLogo is null)
        {
            avisos.Add("No hay un logo configurado: el reporte sale sin logo (Administración › Parámetros del sistema).");
        }

        lblAviso.Text = string.Join("\r\n", avisos);

        ActualizarAcciones();
    }

    private void ReconstruirColumnas(bool anuladas, bool mostrarDesposte)
    {
        _columnasAnuladas = anuladas;
        _columnasConDesposte = mostrarDesposte;
        dgvVista.Columns.Clear();

        var columnasDef = ColumnasReporte.Para(anuladas, mostrarDesposte);
        foreach (var col in columnasDef)
        {
            string headerText = col.Grupo != null
                ? $"{col.Grupo} {col.Titulo.Replace("\n", " ")}"
                : col.Titulo.Replace("\n", " ");

            var gridCol = new DataGridViewTextBoxColumn
            {
                Name = "col" + col.Clave,
                HeaderText = headerText,
                ReadOnly = true
            };

            if (col.AlineadaDerecha)
            {
                gridCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gridCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            else
            {
                gridCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                gridCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }

            switch (col.Clave)
            {
                case "Item": gridCol.Width = 50; break;
                case "Numero": gridCol.Width = 80; break;
                case "Fecha": gridCol.Width = 130; break;
                case "Secuencia": gridCol.Width = 60; break;
                case "Codigo": gridCol.Width = 70; break;
                case "Cliente": gridCol.Width = 65; break;
                case "Fletero": gridCol.Width = 140; break;
                case "Izquierdo": gridCol.Width = 85; break;
                case "Derecho": gridCol.Width = 85; break;
                case "Total": gridCol.Width = 95; break;
                case "Desposte": gridCol.Width = 120; break;
                case "AnuladaEl": gridCol.Width = 130; break;
                case "AnuladaPor": gridCol.Width = 150; break;
                case "Direccion":
                case "Motivo":
                    gridCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    gridCol.MinimumWidth = 150;
                    break;
            }

            dgvVista.Columns.Add(gridCol);
        }
    }

    private bool GrillaTieneFoco()
    {
        if (dgvVista.Focused)
        {
            return true;
        }

        var enfocado = ControlEnfocado();
        return enfocado == dgvVista || dgvVista.Contains(enfocado);
    }

    private Control? ControlEnfocado()
    {
        Control? actual = this;
        while (actual is ContainerControl contenedor && contenedor.ActiveControl != null)
        {
            actual = contenedor.ActiveControl;
        }
        return actual;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter && GrillaTieneFoco())
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task ExportarAsync(bool pdf)
    {
        if (_accionEnCurso)
        {
            return;
        }

        var reporte = _reporte;
        if (reporte is null || reporte.Filas.Count == 0)
        {
            return;
        }

        _accionEnCurso = true;
        try
        {
            var extension = pdf ? ".pdf" : ".xlsx";
            var filtroDialogo = pdf ? "Documento PDF (*.pdf)|*.pdf" : "Libro de Excel (*.xlsx)|*.xlsx";
            var nombreSugerido = TextosReporte.NombreArchivo(reporte, extension);

            var ruta = ElegirRutaGuardar(nombreSugerido, filtroDialogo);
            if (ruta is null)
            {
                return;
            }

            bool guardado = false;
            string? advertencia = null;
            await EjecutarAsync(async () =>
            {
                using var ms = new MemoryStream();
                if (pdf)
                {
                    await _pdf.GenerarPdfAsync(reporte, ms);
                }
                else
                {
                    await _excel.GenerarExcelAsync(reporte, ms);
                }

                try
                {
                    File.WriteAllBytes(ruta, ms.ToArray());
                    guardado = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
                {
                    advertencia = "No se pudo guardar el archivo «" + Path.GetFileName(ruta) + "». Verifique que no esté abierto en otro programa y que tenga permiso para escribir en esa carpeta.";
                }
            }, pdf ? btnPdf : btnExcel);

            // EjecutarAsync restaura el Enabled del botón a su valor previo: si un filtro cambió durante la
            // generación, el botón volvería a quedar habilitado sin reporte (R54).
            ActualizarAcciones();

            // El MessageBox va fuera de EjecutarAsync (regla 0.10).
            if (advertencia is not null)
            {
                MostrarAdvertencia(advertencia);
            }

            if (guardado)
            {
                Log.Information("Reporte {Tipo} exportado a {Formato}: {Filas} boleta(s).", reporte.Tipo, pdf ? "PDF" : "Excel", reporte.Filas.Count);
                if (PreguntarAbrir(ruta))
                {
                    AbrirArchivo(ruta);
                }
            }
        }
        finally
        {
            _accionEnCurso = false;
        }
    }

    private async Task VistaPreviaAsync()
    {
        if (_accionEnCurso)
        {
            return;
        }

        var reporte = _reporte;
        if (reporte is null || reporte.Filas.Count == 0 || reporte.Filas.Count > ReglasReportes.MaximoFilasImpresion)
        {
            return;
        }

        _accionEnCurso = true;
        try
        {
            IReadOnlyList<byte[]>? paginas = null;
            await EjecutarAsync(async () =>
            {
                paginas = await _pdf.GenerarPaginasAsync(reporte, ReglasReportes.ResolucionImpresionDpi);
            }, btnVistaPrevia);
            ActualizarAcciones();

            if (paginas is not { Count: > 0 })
            {
                return;
            }

            Ejecutar(() =>
            {
                using var frm = new FrmVistaPrevia(
                    new DocumentoPaginasReporte(paginas, paraVistaPrevia: true, reporte.Titulo),
                    "Vista previa — " + reporte.Titulo);
                MostrarDialogo(frm);
            });
        }
        finally
        {
            _accionEnCurso = false;
        }
    }

    private async Task ImprimirAsync()
    {
        if (_accionEnCurso)
        {
            return;
        }

        var reporte = _reporte;
        if (reporte is null || reporte.Filas.Count == 0 || reporte.Filas.Count > ReglasReportes.MaximoFilasImpresion)
        {
            return;
        }

        _accionEnCurso = true;
        try
        {
            await ImprimirInternoAsync(reporte);
        }
        finally
        {
            _accionEnCurso = false;
        }
    }

    private async Task ImprimirInternoAsync(ReportePesajes reporte)
    {
        IReadOnlyList<byte[]>? paginas = null;
        await EjecutarAsync(async () =>
        {
            paginas = await _pdf.GenerarPaginasAsync(reporte, ReglasReportes.ResolucionImpresionDpi);
        }, btnImprimir);
        ActualizarAcciones();

        if (paginas is not { Count: > 0 })
        {
            return;
        }

        Ejecutar(() =>
        {
            using var doc = new DocumentoPaginasReporte(paginas, paraVistaPrevia: false, reporte.Titulo);
            try
            {
                if (!ElegirImpresora(doc))
                {
                    return;
                }
                EnviarAImpresora(doc);
            }
            catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception)
            {
                MostrarAdvertencia("No se pudo imprimir el reporte: " + ex.Message);
            }
        });
    }

    private string? ElegirRutaGuardar(string nombre, string filtro)
    {
        if (ElegirRutaGuardarParaTest != null)
        {
            return ElegirRutaGuardarParaTest(nombre, filtro);
        }

        using var dlg = new SaveFileDialog
        {
            FileName = nombre,
            Filter = filtro,
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
    }

    private bool PreguntarAbrir(string ruta)
    {
        if (PreguntarAbrirParaTest != null)
        {
            return PreguntarAbrirParaTest(ruta);
        }

        return MessageBox.Show(
            this,
            "Se guardó el archivo:\r\n" + ruta + "\r\n\r\n¿Desea abrirlo ahora?",
            "Reportes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private void AbrirArchivo(string ruta)
    {
        if (AbrirArchivoParaTest != null)
        {
            AbrirArchivoParaTest(ruta);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            MostrarAdvertencia("No hay un programa asociado para abrir el archivo. Se guardó en:\r\n" + ruta);
        }
    }

    private bool ElegirImpresora(PrintDocument doc)
    {
        if (ElegirImpresoraParaTest != null)
        {
            return ElegirImpresoraParaTest(doc);
        }

        using var dlg = new PrintDialog
        {
            Document = doc,
            UseEXDialog = true,
            AllowSomePages = false,
            AllowSelection = false
        };
        return dlg.ShowDialog(this) == DialogResult.OK;
    }

    private void EnviarAImpresora(PrintDocument doc)
    {
        if (EnviarAImpresoraParaTest != null)
        {
            EnviarAImpresoraParaTest(doc);
            return;
        }

        doc.DefaultPageSettings.Landscape = false;
        doc.Print();
    }

    private DialogResult MostrarDialogo(Form f)
    {
        if (MostrarDialogoParaTest != null)
        {
            return MostrarDialogoParaTest(f);
        }

        return f.ShowDialog(this);
    }

    private void MostrarAdvertencia(string texto)
    {
        if (MostrarAdvertenciaParaTest != null)
        {
            MostrarAdvertenciaParaTest(texto);
            return;
        }

        MessageBox.Show(this, texto, "Reportes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using PesajeFaeneo.Application.Utilitarios.Logs;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Utilitarios;

public class FrmVisorLogs : FormBase
{
    private readonly IServicioLogs _servicio;
    private ContenidoLog? _contenido;
    private List<EntradaLog> _entradasFiltradas = new();

    // Controles internos para pruebas
    internal readonly ComboBox cmbArchivo;
    internal readonly ComboBox cmbNivel;
    internal readonly TextBox txtBuscar;
    internal readonly Button btnFiltrar;
    internal readonly Button btnActualizar;
    internal readonly Button btnAbrirCarpeta;

    internal readonly DataGridView dgvEntradas;
    internal readonly TextBox txtDetalle;
    internal readonly Label lblResumen;
    internal readonly Button btnCerrar;

    // Ganchos de prueba
    public Action<string>? MostrarAdvertenciaParaTest { get; set; }
    public Action<string>? AbrirCarpetaParaTest { get; set; }

    public FrmVisorLogs(IServicioLogs servicio)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));

        Text = "Visor de logs";
        MinimumSize = new Size(1000, 650);
        Size = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterParent;

        var pnlPrincipal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        pnlPrincipal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 0: Filtros
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // 1: Grilla y detalle
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 2: Estado y cerrar

        // 1. Barra de filtros
        var grpFiltros = new GroupBox
        {
            Text = "Filtros y opciones",
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 6),
            Margin = new Padding(0, 0, 0, 6)
        };

        var pnlFiltros = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2
        };
        pnlFiltros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlFiltros.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlFiltros.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var pnlFila0 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        var lblArchivo = new Label { Text = "Archivo:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        cmbArchivo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300,
            Margin = new Padding(2, 4, 12, 4)
        };

        var lblNivel = new Label { Text = "Nivel:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        cmbNivel = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Margin = new Padding(2, 4, 12, 4)
        };
        cmbNivel.Items.Add(new OpcionNivel("Todos", NivelLog.Desconocido));
        cmbNivel.Items.Add(new OpcionNivel("Información o superior", NivelLog.Informacion));
        cmbNivel.Items.Add(new OpcionNivel("Advertencias y errores", NivelLog.Advertencia));
        cmbNivel.Items.Add(new OpcionNivel("Solo errores", NivelLog.Error));
        cmbNivel.SelectedIndex = 0;

        btnActualizar = DisenoFiltros.Boton("Ac&tualizar (F5)", 125);
        btnAbrirCarpeta = DisenoFiltros.Boton("Abrir &carpeta", 115);

        pnlFila0.Controls.Add(lblArchivo);
        pnlFila0.Controls.Add(cmbArchivo);
        pnlFila0.Controls.Add(lblNivel);
        pnlFila0.Controls.Add(cmbNivel);
        pnlFila0.Controls.Add(btnActualizar);
        pnlFila0.Controls.Add(btnAbrirCarpeta);

        var pnlFila1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 0)
        };

        var lblBuscar = new Label { Text = "Buscar:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 2, 4) };
        txtBuscar = new TextBox { Width = 220, Margin = new Padding(2, 4, 6, 4) };
        btnFiltrar = DisenoFiltros.Boton("&Filtrar", 95);

        pnlFila1.Controls.Add(lblBuscar);
        pnlFila1.Controls.Add(txtBuscar);
        pnlFila1.Controls.Add(btnFiltrar);

        pnlFiltros.Controls.Add(pnlFila0, 0, 0);
        pnlFiltros.Controls.Add(pnlFila1, 0, 1);
        grpFiltros.Controls.Add(pnlFiltros);

        void AjustarGrpFiltros()
        {
            grpFiltros.Height = pnlFiltros.Bottom + grpFiltros.Padding.Bottom + 4;
        }
        pnlFiltros.SizeChanged += (_, _) => AjustarGrpFiltros();
        pnlFiltros.LocationChanged += (_, _) => AjustarGrpFiltros();
        AjustarGrpFiltros();

        pnlPrincipal.Controls.Add(grpFiltros, 0, 0);

        // 2. Grilla y detalle
        var pnlCentral = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        pnlCentral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlCentral.RowStyles.Add(new RowStyle(SizeType.Percent, 65f));
        pnlCentral.RowStyles.Add(new RowStyle(SizeType.Percent, 35f));

        dgvEntradas = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = SystemColors.Window
        };
        ConfigurarColumnasGrilla();

        txtDetalle = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point)
        };

        pnlCentral.Controls.Add(dgvEntradas, 0, 0);
        pnlCentral.Controls.Add(txtDetalle, 0, 1);
        pnlPrincipal.Controls.Add(pnlCentral, 0, 1);

        // 3. Barra inferior
        var pnlInferior = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0)
        };
        pnlInferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlInferior.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlInferior.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        lblResumen = new Label
        {
            Text = "",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(Font, FontStyle.Regular),
            Margin = new Padding(4)
        };
        btnCerrar = DisenoFiltros.Boton("Cerrar", 90);

        pnlInferior.Controls.Add(lblResumen, 0, 0);
        pnlInferior.Controls.Add(btnCerrar, 1, 0);
        pnlPrincipal.Controls.Add(pnlInferior, 0, 2);

        Controls.Add(pnlPrincipal);
        CancelButton = btnCerrar;

        // Eventos
        cmbArchivo.SelectedIndexChanged += async (_, _) => await LeerArchivoSeleccionadoAsync();
        cmbNivel.SelectedIndexChanged += (_, _) => AplicarFiltro();
        btnFiltrar.Click += (_, _) => AplicarFiltro();
        txtBuscar.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AplicarFiltro();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        btnActualizar.Click += async (_, _) => await CargarInternoAsync();
        btnAbrirCarpeta.Click += (_, _) => PulsarAbrirCarpetaInterno();
        btnCerrar.Click += (_, _) => Close();
        dgvEntradas.SelectionChanged += (_, _) => ActualizarDetalle();
    }

    private void ConfigurarColumnasGrilla()
    {
        dgvEntradas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHora", HeaderText = "Hora", Width = 150 });
        dgvEntradas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNivel", HeaderText = "Nivel", Width = 90 });
        dgvEntradas.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colMensaje",
            HeaderText = "Mensaje",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = EjecutarAsync(CargarInternoAsync);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            _ = EjecutarAsync(CargarInternoAsync);
            return true;
        }

        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task CargarInternoAsync()
    {
        IReadOnlyList<ArchivoLog>? archivos = null;
        string? error = null;

        await EjecutarAsync(async () =>
        {
            try
            {
                archivos = await _servicio.ListarArchivosAsync();
            }
            catch (DomainException dex)
            {
                error = dex.Message;
            }
        });

        if (error != null)
        {
            MostrarAdvertencia(error);
            return;
        }

        archivos ??= Array.Empty<ArchivoLog>();

        var anteriorSeleccion = (cmbArchivo.SelectedItem as OpcionArchivo)?.Archivo.Nombre;

        cmbArchivo.Items.Clear();
        if (archivos.Count == 0)
        {
            _contenido = null;
            _entradasFiltradas.Clear();
            dgvEntradas.Rows.Clear();
            txtDetalle.Text = string.Empty;
            lblResumen.Text = "No hay archivos de log.";
            return;
        }

        var seleccionadoIdx = 0;
        for (var i = 0; i < archivos.Count; i++)
        {
            var arch = archivos[i];
            var mb = arch.TamanoBytes / (1024.0 * 1024.0);
            var texto = arch.Fecha.HasValue
                ? $"{arch.Fecha.Value:dd/MM/yyyy} — {arch.Nombre} ({mb:0.0} MB)"
                : $"{arch.Nombre} ({mb:0.0} MB)";

            cmbArchivo.Items.Add(new OpcionArchivo(texto, arch));

            if (anteriorSeleccion != null && arch.Nombre == anteriorSeleccion)
            {
                seleccionadoIdx = i;
            }
        }

        cmbArchivo.SelectedIndex = seleccionadoIdx;
    }

    private async Task LeerArchivoSeleccionadoAsync()
    {
        if (cmbArchivo.SelectedItem is not OpcionArchivo opc)
        {
            return;
        }

        ContenidoLog? contenido = null;
        string? error = null;

        await EjecutarAsync(async () =>
        {
            try
            {
                contenido = await _servicio.LeerAsync(opc.Archivo.Nombre);
            }
            catch (DomainException dex)
            {
                error = dex.Message;
            }
        });

        if (error != null)
        {
            MostrarAdvertencia(error);
            return;
        }

        _contenido = contenido;
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        if (_contenido == null)
        {
            return;
        }

        var umbral = cmbNivel.SelectedItem is OpcionNivel opcNivel ? opcNivel.Umbral : NivelLog.Desconocido;
        var textoFiltro = txtBuscar.Text?.Trim();

        var query = _contenido.Entradas.AsEnumerable();

        if (umbral != NivelLog.Desconocido)
        {
            query = query.Where(e => e.Nivel >= umbral);
        }

        if (!string.IsNullOrEmpty(textoFiltro))
        {
            query = query.Where(e =>
                (e.Mensaje != null && e.Mensaje.Contains(textoFiltro, StringComparison.OrdinalIgnoreCase)) ||
                (e.Detalle != null && e.Detalle.Contains(textoFiltro, StringComparison.OrdinalIgnoreCase)));
        }

        _entradasFiltradas = query.Take(5000).ToList();

        dgvEntradas.Rows.Clear();
        foreach (var entrada in _entradasFiltradas)
        {
            var idx = dgvEntradas.Rows.Add(entrada.Marca, FormatearNivel(entrada.Nivel), entrada.Mensaje);
            var row = dgvEntradas.Rows[idx];

            if (entrada.Nivel == NivelLog.Error || entrada.Nivel == NivelLog.Fatal)
            {
                row.DefaultCellStyle.BackColor = Color.MistyRose;
            }
            else if (entrada.Nivel == NivelLog.Advertencia)
            {
                row.DefaultCellStyle.BackColor = Color.LightYellow;
            }
        }

        var sbResumen = new StringBuilder();
        sbResumen.Append($"Mostrando {_entradasFiltradas.Count} de {_contenido.Entradas.Count} entradas.");
        if (_contenido.Truncado)
        {
            sbResumen.Append(" (Solo se leyeron los últimos 4 MB del archivo.)");
        }
        lblResumen.Text = sbResumen.ToString();

        if (dgvEntradas.Rows.Count > 0)
        {
            dgvEntradas.Rows[0].Selected = true;
        }
        ActualizarDetalle();
    }

    private void ActualizarDetalle()
    {
        if (dgvEntradas.SelectedRows.Count == 0)
        {
            txtDetalle.Text = string.Empty;
            return;
        }

        var idx = dgvEntradas.SelectedRows[0].Index;
        if (idx >= 0 && idx < _entradasFiltradas.Count)
        {
            var e = _entradasFiltradas[idx];
            txtDetalle.Text = string.IsNullOrEmpty(e.Detalle)
                ? e.Mensaje
                : $"{e.Mensaje}\r\n\r\n{e.Detalle}";
        }
        else
        {
            txtDetalle.Text = string.Empty;
        }
    }

    private static string FormatearNivel(NivelLog nivel) => nivel switch
    {
        NivelLog.Informacion => "INF",
        NivelLog.Advertencia => "WRN",
        NivelLog.Error => "ERR",
        NivelLog.Fatal => "FTL",
        NivelLog.Depuracion => "DBG",
        NivelLog.Detalle => "VRB",
        _ => "???"
    };

    private void PulsarAbrirCarpetaInterno()
    {
        if (AbrirCarpetaParaTest != null)
        {
            AbrirCarpetaParaTest(_servicio.CarpetaLogs);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_servicio.CarpetaLogs}\"") { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            MostrarAdvertencia("No se pudo abrir la carpeta de logs.");
        }
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

    // Entradas para tests
    internal Task CargarParaTestAsync() => CargarInternoAsync();

    internal async Task SeleccionarArchivoParaTestAsync(int indice)
    {
        if (indice >= 0 && indice < cmbArchivo.Items.Count)
        {
            cmbArchivo.SelectedIndex = indice;
            await LeerArchivoSeleccionadoAsync();
        }
    }

    internal void AplicarFiltroParaTest() => AplicarFiltro();

    internal void SeleccionarEntradaParaTest(int indice)
    {
        if (indice >= 0 && indice < dgvEntradas.Rows.Count)
        {
            dgvEntradas.ClearSelection();
            dgvEntradas.Rows[indice].Selected = true;
            ActualizarDetalle();
        }
    }

    internal void PulsarAbrirCarpetaParaTest() => PulsarAbrirCarpetaInterno();

    internal bool ProcesarTeclaParaTest(Keys k)
    {
        Message m = default;
        return ProcessCmdKey(ref m, k);
    }

    private sealed record OpcionArchivo(string Texto, ArchivoLog Archivo)
    {
        public override string ToString() => Texto;
    }

    private sealed record OpcionNivel(string Texto, NivelLog Umbral)
    {
        public override string ToString() => Texto;
    }
}

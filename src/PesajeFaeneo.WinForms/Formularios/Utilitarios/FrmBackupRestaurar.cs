using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Utilitarios;

public class FrmBackupRestaurar : FormBase
{
    private readonly IServicioBackup _servicio;
    private readonly TimeProvider _tiempo;
    private readonly ErrorProvider _errorProvider;

    private IReadOnlyList<InfoBackup> _backups = Array.Empty<InfoBackup>();

    public bool ReinicioSolicitado { get; private set; }

    internal readonly DataGridView dgvBackups;
    internal readonly Button btnCrear;
    internal readonly Button btnExportar;
    internal readonly Button btnRestaurar;
    internal readonly Button btnRestaurarArchivo;
    internal readonly Button btnAbrirCarpeta;
    internal readonly Button btnActualizar;
    internal readonly Button btnCerrar;

    internal readonly CheckBox chkAutomatico;
    internal readonly CheckBox chkAlCerrar;
    internal readonly NumericUpDown nudDias;
    internal readonly TextBox txtCarpetaSecundaria;
    internal readonly Button btnExaminarCarpeta;
    internal readonly Button btnQuitarCarpeta;
    internal readonly Button btnGuardarConfiguracion;
    internal readonly Label lblUltimoAutomatico;
    internal readonly Label lblEstado;

    // Ganchos para test
    internal Func<string?>? ElegirArchivoBackupParaTest { get; set; }
    internal Func<string, string?>? ElegirDestinoExportarParaTest { get; set; }
    internal Func<string?>? ElegirCarpetaParaTest { get; set; }
    internal Func<ValidacionRestauracion, string?>? ConfirmarRestauracionParaTest { get; set; }
    internal Action<string>? MostrarInformacionParaTest { get; set; }
    internal Action<string>? MostrarAdvertenciaParaTest { get; set; }
    internal Action<string>? AbrirCarpetaParaTest { get; set; }

    public FrmBackupRestaurar(IServicioBackup servicio, TimeProvider tiempo)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));

        Text = "Backup / Restaurar";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 620);
        Size = new Size(950, 650);

        _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var pnlPrincipal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlPrincipal.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. Grilla
        dgvBackups = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window
        };

        ConfigurarColumnasGrilla();
        pnlPrincipal.Controls.Add(dgvBackups, 0, 0);

        // 2. Botones de acción de backups
        var pnlAccionesBackup = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 4)
        };

        btnCrear = DisenoFiltros.Boton("&Crear backup ahora", 155);
        btnExportar = DisenoFiltros.Boton("E&xportar copia…", 130);
        btnExportar.Enabled = false;
        btnRestaurar = DisenoFiltros.Boton("&Restaurar seleccionada…", 185);
        btnRestaurar.Enabled = false;
        btnRestaurarArchivo = DisenoFiltros.Boton("Restaurar desde &archivo…", 195);
        btnAbrirCarpeta = DisenoFiltros.Boton("Abrir &carpeta", 115);
        btnActualizar = DisenoFiltros.Boton("Ac&tualizar (F5)", 125);

        pnlAccionesBackup.Controls.Add(btnCrear);
        pnlAccionesBackup.Controls.Add(btnExportar);
        pnlAccionesBackup.Controls.Add(btnRestaurar);
        pnlAccionesBackup.Controls.Add(btnRestaurarArchivo);
        pnlAccionesBackup.Controls.Add(btnAbrirCarpeta);
        pnlAccionesBackup.Controls.Add(btnActualizar);
        pnlPrincipal.Controls.Add(pnlAccionesBackup, 0, 1);

        // 3. Grupo backup automático
        var grpAuto = new GroupBox
        {
            Text = "Backup automático",
            Dock = DockStyle.Top,
            Padding = new Padding(10, 6, 10, 10),
            Margin = new Padding(0, 4, 0, 6)
        };

        var pnlAuto = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4
        };
        pnlAuto.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var pnlFila0 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        chkAutomatico = new CheckBox { Text = "Crear un backup automático cada día", AutoSize = true, Margin = new Padding(4) };
        chkAlCerrar = new CheckBox { Text = "Crear también al cerrar el sistema", AutoSize = true, Margin = new Padding(16, 4, 4, 4) };
        pnlFila0.Controls.Add(chkAutomatico);
        pnlFila0.Controls.Add(chkAlCerrar);

        var pnlFila1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 4)
        };
        var lblDias = new Label { Text = "Conservar los automáticos (días):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 6, 4, 4) };
        nudDias = new NumericUpDown { Minimum = 7, Maximum = 365, Value = 30, Width = 70, Margin = new Padding(4) };
        btnGuardarConfiguracion = DisenoFiltros.Boton("&Guardar configuración", 175);
        pnlFila1.Controls.Add(lblDias);
        pnlFila1.Controls.Add(nudDias);
        pnlFila1.Controls.Add(btnGuardarConfiguracion);

        var pnlFila2 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        var lblSecundaria = new Label { Text = "Copiar también a (opcional):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 6, 4, 4) };
        txtCarpetaSecundaria = new TextBox { ReadOnly = true, Width = 300, Margin = new Padding(4) };
        btnExaminarCarpeta = DisenoFiltros.Boton("Examinar…", 95);
        btnQuitarCarpeta = DisenoFiltros.Boton("Quitar", 75);
        pnlFila2.Controls.Add(lblSecundaria);
        pnlFila2.Controls.Add(txtCarpetaSecundaria);
        pnlFila2.Controls.Add(btnExaminarCarpeta);
        pnlFila2.Controls.Add(btnQuitarCarpeta);

        lblUltimoAutomatico = new Label
        {
            Text = "Sin backups automáticos en esta sesión.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(4, 6, 4, 4)
        };

        pnlAuto.Controls.Add(pnlFila0, 0, 0);
        pnlAuto.Controls.Add(pnlFila1, 0, 1);
        pnlAuto.Controls.Add(pnlFila2, 0, 2);
        pnlAuto.Controls.Add(lblUltimoAutomatico, 0, 3);

        grpAuto.Controls.Add(pnlAuto);

        void AjustarGrpAuto()
        {
            grpAuto.Height = pnlAuto.Bottom + grpAuto.Padding.Bottom + 4;
        }
        pnlAuto.SizeChanged += (_, _) => AjustarGrpAuto();
        pnlAuto.LocationChanged += (_, _) => AjustarGrpAuto();
        AjustarGrpAuto();

        pnlPrincipal.Controls.Add(grpAuto, 0, 2);

        // 4. Barra inferior: estado y cerrar
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

        lblEstado = new Label { Text = "", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4) };
        btnCerrar = DisenoFiltros.Boton("Cerrar", 90);

        pnlInferior.Controls.Add(lblEstado, 0, 0);
        pnlInferior.Controls.Add(btnCerrar, 1, 0);
        pnlPrincipal.Controls.Add(pnlInferior, 0, 3);

        Controls.Add(pnlPrincipal);
        CancelButton = btnCerrar;

        // Eventos
        chkAutomatico.CheckedChanged += (_, _) => chkAlCerrar.Enabled = chkAutomatico.Checked;
        dgvBackups.SelectionChanged += (_, _) => ActualizarEstadoBotonesSeleccion();
        btnCrear.Click += async (_, _) => await CrearInternoAsync();
        btnExportar.Click += async (_, _) => await ExportarInternoAsync();
        btnRestaurar.Click += async (_, _) => await RestaurarSeleccionadaInternoAsync();
        btnRestaurarArchivo.Click += async (_, _) => await RestaurarDesdeArchivoInternoAsync();
        btnAbrirCarpeta.Click += (_, _) => PulsarAbrirCarpetaInterno();
        btnActualizar.Click += async (_, _) => await CargarInternoAsync();
        btnCerrar.Click += (_, _) => Close();

        btnExaminarCarpeta.Click += (_, _) => ExaminarCarpetaInterno();
        btnQuitarCarpeta.Click += (_, _) => QuitarCarpetaInterno();
        btnGuardarConfiguracion.Click += async (_, _) => await GuardarConfiguracionInternoAsync();
    }

    private void ConfigurarColumnasGrilla()
    {
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFecha", HeaderText = "Fecha", Width = 140 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipo", HeaderText = "Tipo", Width = 120 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBoletas", HeaderText = "Boletas", Width = 75 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUltimaBoleta", HeaderText = "Última boleta", Width = 95 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTamano", HeaderText = "Tamaño", Width = 90 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colVersion", HeaderText = "Versión", Width = 80 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colArchivo", HeaderText = "Archivo", Width = 230 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEstado", HeaderText = "Estado", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
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

        return base.ProcessCmdKey(ref msg, keyData);
    }

    internal async Task CargarInternoAsync()
    {
        _backups = await _servicio.ListarAsync();
        dgvBackups.Rows.Clear();

        foreach (var b in _backups)
        {
            var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(b.CreadoEnUtc, _tiempo.LocalTimeZone);
            var fechaStr = fechaLocal.ToString("dd/MM/yyyy HH:mm", TextosBoleta.Cultura);
            var tamanoMb = (b.TamanoBytes / (1024.0 * 1024.0)).ToString("#,##0.0", TextosBoleta.Cultura) + " MB";
            var estadoStr = b.Valido ? "Correcta" : (b.Problema ?? "Inválida");

            dgvBackups.Rows.Add(
                fechaStr,
                ReglasBackup.TextoTipo(b.Tipo),
                b.Pesajes.ToString(TextosBoleta.Cultura),
                b.UltimoNumeroBoleta?.ToString(TextosBoleta.Cultura) ?? "—",
                tamanoMb,
                b.VersionAplicacion,
                b.NombreArchivo,
                estadoStr);
        }

        var config = await _servicio.ObtenerConfiguracionAsync();
        chkAutomatico.Checked = config.AutomaticoActivo;
        chkAlCerrar.Checked = config.AlCerrar;
        chkAlCerrar.Enabled = config.AutomaticoActivo;
        nudDias.Value = Math.Clamp(config.DiasRetencion, 7, 365);
        txtCarpetaSecundaria.Text = config.CarpetaSecundaria ?? string.Empty;

        var ultimo = _servicio.UltimoAutomatico;
        if (ultimo == null)
        {
            lblUltimoAutomatico.Text = "Sin backups automáticos en esta sesión.";
        }
        else if (ultimo.Exito)
        {
            var fLocal = TimeZoneInfo.ConvertTimeFromUtc(ultimo.EnUtc, _tiempo.LocalTimeZone);
            lblUltimoAutomatico.Text = $"Último backup automático: {fLocal.ToString("dd/MM/yyyy HH:mm", TextosBoleta.Cultura)} ({ultimo.NombreArchivo})";
        }
        else
        {
            var fLocal = TimeZoneInfo.ConvertTimeFromUtc(ultimo.EnUtc, _tiempo.LocalTimeZone);
            lblUltimoAutomatico.Text = $"Último backup automático: FALLÓ el {fLocal.ToString("dd/MM/yyyy HH:mm", TextosBoleta.Cultura)} — {ultimo.Mensaje}";
        }

        ActualizarEstadoBotonesSeleccion();
    }

    private void ActualizarEstadoBotonesSeleccion()
    {
        var sel = ObtenerSeleccionado();
        var valido = sel != null && sel.Valido;
        btnExportar.Enabled = valido;
        btnRestaurar.Enabled = valido;
    }

    private InfoBackup? ObtenerSeleccionado()
    {
        if (dgvBackups.SelectedRows.Count == 0)
        {
            return null;
        }

        var idx = dgvBackups.SelectedRows[0].Index;
        if (idx >= 0 && idx < _backups.Count)
        {
            return _backups[idx];
        }

        return null;
    }

    internal async Task CrearInternoAsync()
    {
        string? error = null;
        ResultadoCreacionBackup? res = null;

        await EjecutarAsync(async () =>
        {
            try
            {
                res = await _servicio.CrearManualAsync();
            }
            catch (DomainException ex)
            {
                error = ex.Message;
            }
        });

        if (error != null)
        {
            MostrarAdvertencia(error);
            return;
        }

        if (res != null)
        {
            var mensaje = $"Copia creada: {res.Backup.NombreArchivo}." +
                          (res.AdvertenciaCopiaSecundaria != null ? " " + res.AdvertenciaCopiaSecundaria : "");
            MostrarInformacion(mensaje);
            await EjecutarAsync(CargarInternoAsync);
        }
    }

    internal async Task ExportarInternoAsync()
    {
        var sel = ObtenerSeleccionado();
        if (sel == null || !sel.Valido)
        {
            return;
        }

        var destino = ElegirDestinoExportar(sel.NombreArchivo);
        if (string.IsNullOrWhiteSpace(destino))
        {
            return;
        }

        string? error = null;
        bool exito = false;

        await EjecutarAsync(async () =>
        {
            try
            {
                await _servicio.ExportarAsync(sel.RutaArchivo, destino);
                exito = true;
            }
            catch (DomainException ex)
            {
                error = ex.Message;
            }
        });

        if (error != null)
        {
            MostrarAdvertencia(error);
        }
        else if (exito)
        {
            MostrarInformacion($"Copia exportada como {Path.GetFileName(destino)}. Guárdela en un lugar seguro: contiene todos los datos del sistema, incluidos los usuarios.");
        }
    }

    internal async Task RestaurarSeleccionadaInternoAsync()
    {
        var sel = ObtenerSeleccionado();
        if (sel == null || !sel.Valido)
        {
            return;
        }

        await RestaurarAsync(sel.RutaArchivo);
    }

    internal async Task RestaurarDesdeArchivoInternoAsync()
    {
        var ruta = ElegirArchivoBackup();
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        await RestaurarAsync(ruta);
    }

    private async Task RestaurarAsync(string ruta)
    {
        ValidacionRestauracion? validacion = null;
        string? errorValidacion = null;

        await EjecutarAsync(async () =>
        {
            try
            {
                validacion = await _servicio.ValidarAsync(ruta);
            }
            catch (DomainException ex)
            {
                errorValidacion = ex.Message;
            }
        });

        if (errorValidacion != null || validacion == null)
        {
            MostrarAdvertencia(errorValidacion ?? "No se pudo validar la copia de seguridad.");
            return;
        }

        var confirmacion = ConfirmarRestauracion(validacion);
        if (confirmacion == null)
        {
            await EjecutarAsync(async () => await _servicio.DescartarValidacionAsync(validacion.Token));
            return;
        }

        InfoBackup? resguardo = null;
        string? errorProgramar = null;

        await EjecutarAsync(async () =>
        {
            try
            {
                resguardo = await _servicio.ProgramarRestauracionAsync(validacion.Token, confirmacion);
            }
            catch (DomainException ex)
            {
                errorProgramar = ex.Message;
            }
        });

        if (errorProgramar != null || resguardo == null)
        {
            MostrarAdvertencia(errorProgramar ?? "No se pudo programar la restauración.");
            return;
        }

        MostrarInformacion($"La restauración quedó programada. Antes se guardó una copia de la base actual ({resguardo.NombreArchivo}). El sistema se cerrará y volverá a abrirse para aplicarla.");
        ReinicioSolicitado = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    internal async Task GuardarConfiguracionInternoAsync()
    {
        _errorProvider.Clear();
        var txt = txtCarpetaSecundaria.Text.Trim();
        var cfg = new ConfiguracionBackup(
            chkAutomatico.Checked,
            chkAlCerrar.Checked,
            (int)nudDias.Value,
            string.IsNullOrWhiteSpace(txt) ? null : txt);

        string? advertencia = null;
        bool guardado = false;

        await EjecutarAsync(async () =>
        {
            try
            {
                await _servicio.GuardarConfiguracionAsync(cfg);
                guardado = true;
            }
            catch (ValidacionException ex)
            {
                var erroresNoCubiertos = new List<string>();
                foreach (var err in ex.Errores)
                {
                    if (err.Campo == CamposBackup.DiasRetencion)
                    {
                        _errorProvider.SetError(nudDias, err.Mensaje);
                    }
                    else if (err.Campo == CamposBackup.CarpetaSecundaria)
                    {
                        _errorProvider.SetError(txtCarpetaSecundaria, err.Mensaje);
                    }
                    else
                    {
                        erroresNoCubiertos.Add(err.Mensaje);
                    }
                }

                if (erroresNoCubiertos.Count > 0)
                {
                    advertencia = string.Join("\n", erroresNoCubiertos);
                }
            }
            catch (DomainException ex)
            {
                advertencia = ex.Message;
            }
        });

        if (advertencia != null)
        {
            MostrarAdvertencia(advertencia);
        }
        else if (guardado)
        {
            lblEstado.Text = "Configuración guardada.";
        }
    }

    internal void ExaminarCarpetaInterno()
    {
        var carpeta = ElegirCarpeta();
        if (!string.IsNullOrWhiteSpace(carpeta))
        {
            txtCarpetaSecundaria.Text = carpeta;
        }
    }

    internal void QuitarCarpetaInterno()
    {
        txtCarpetaSecundaria.Text = string.Empty;
    }

    internal void PulsarAbrirCarpetaInterno()
    {
        AbrirCarpeta(_servicio.CarpetaBackups);
    }

    // Métodos envoltorios para ganchos de test
    private string? ElegirArchivoBackup()
    {
        if (ElegirArchivoBackupParaTest != null)
        {
            return ElegirArchivoBackupParaTest();
        }

        using var dlg = new OpenFileDialog
        {
            Filter = "Copias de seguridad (*.zip)|*.zip",
            InitialDirectory = Directory.Exists(_servicio.CarpetaBackups) ? _servicio.CarpetaBackups : string.Empty
        };

        return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
    }

    private string? ElegirDestinoExportar(string nombreSugerido)
    {
        if (ElegirDestinoExportarParaTest != null)
        {
            return ElegirDestinoExportarParaTest(nombreSugerido);
        }

        using var dlg = new SaveFileDialog
        {
            FileName = nombreSugerido,
            Filter = "Copias de seguridad (*.zip)|*.zip",
            OverwritePrompt = true
        };

        return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
    }

    private string? ElegirCarpeta()
    {
        if (ElegirCarpetaParaTest != null)
        {
            return ElegirCarpetaParaTest();
        }

        using var dlg = new FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description = "Carpeta para la copia secundaria"
        };

        return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedPath : null;
    }

    private string? ConfirmarRestauracion(ValidacionRestauracion v)
    {
        if (ConfirmarRestauracionParaTest != null)
        {
            return ConfirmarRestauracionParaTest(v);
        }

        using var f = new FrmConfirmarRestauracion(v, _tiempo.LocalTimeZone);
        return f.ShowDialog(this) == DialogResult.OK ? f.Confirmacion : null;
    }

    private void MostrarInformacion(string mensaje)
    {
        if (MostrarInformacionParaTest != null)
        {
            MostrarInformacionParaTest(mensaje);
            return;
        }

        MessageBox.Show(this, mensaje, "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MostrarAdvertencia(string mensaje)
    {
        if (MostrarAdvertenciaParaTest != null)
        {
            MostrarAdvertenciaParaTest(mensaje);
            return;
        }

        MessageBox.Show(this, mensaje, "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void AbrirCarpeta(string carpeta)
    {
        if (AbrirCarpetaParaTest != null)
        {
            AbrirCarpetaParaTest(carpeta);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{carpeta}\"") { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            MostrarAdvertencia("No se pudo abrir el explorador de archivos.");
        }
    }

    // Entradas para tests
    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task CrearParaTestAsync() => CrearInternoAsync();
    internal Task ExportarParaTestAsync() => ExportarInternoAsync();
    internal Task RestaurarSeleccionadaParaTestAsync() => RestaurarSeleccionadaInternoAsync();
    internal Task RestaurarDesdeArchivoParaTestAsync() => RestaurarDesdeArchivoInternoAsync();
    internal Task GuardarConfiguracionParaTestAsync() => GuardarConfiguracionInternoAsync();
    internal void ExaminarCarpetaParaTest() => ExaminarCarpetaInterno();
    internal void QuitarCarpetaParaTest() => QuitarCarpetaInterno();
    internal void PulsarAbrirCarpetaParaTest() => PulsarAbrirCarpetaInterno();
    internal void SeleccionarFilaParaTest(int indice)
    {
        if (indice >= 0 && indice < dgvBackups.Rows.Count)
        {
            dgvBackups.ClearSelection();
            dgvBackups.Rows[indice].Selected = true;
            ActualizarEstadoBotonesSeleccion();
        }
    }
    internal bool ProcesarTeclaParaTest(Keys k)
    {
        var msg = default(Message);
        return ProcessCmdKey(ref msg, k);
    }
    internal string ErrorDe(Control c) => _errorProvider.GetError(c);
}

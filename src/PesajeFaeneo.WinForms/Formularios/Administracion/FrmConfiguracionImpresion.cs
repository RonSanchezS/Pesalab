using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.WinForms.Formularios.Administracion;

public class FrmConfiguracionImpresion : FormBase
{
    private readonly IServicioConfiguracionImpresion _config;
    private readonly IServicioBoletas _boletas;
    private readonly IImpresorBoletas _impresor;

    private readonly ErrorProvider _errorProvider;
    private readonly Dictionary<string, Control> _controles;

    internal Label lblEquipo;
    internal Label lblEstadoConfig;
    internal ComboBox cmbImpresora;
    internal ComboBox cmbFormato;
    internal ComboBox cmbPapel;
    internal NumericUpDown numCopias;
    internal CheckBox chkImprimirAlGrabar;
    internal NumericUpDown numDesplX;
    internal NumericUpDown numDesplY;
    internal Label lblAdvertencia;

    internal Button btnVistaPrevia;
    internal Button btnImprimirMuestra;
    internal Button btnCalibracion;
    internal Button btnGuardar;
    internal Button btnCancelar;

    internal Func<Form, DialogResult>? MostrarDialogoParaTest { get; set; }

    internal sealed record ItemTexto(string Texto, string? Valor)
    {
        public override string ToString() => Texto;
    }

    internal sealed record ItemFormato(string Texto, FormatoBoleta Valor)
    {
        public override string ToString() => Texto;
    }

    public FrmConfiguracionImpresion(
        IServicioConfiguracionImpresion config,
        IServicioBoletas boletas,
        IImpresorBoletas impresor)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _boletas = boletas ?? throw new ArgumentNullException(nameof(boletas));
        _impresor = impresor ?? throw new ArgumentNullException(nameof(impresor));

        Text = "Configuración de impresión";
        Size = new Size(620, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _errorProvider = new ErrorProvider { ContainerControl = this };

        // 1. Encabezado de equipo y estado
        lblEquipo = new Label
        {
            Text = "Equipo: -",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };

        lblEstadoConfig = new Label
        {
            Text = "-",
            Location = new Point(20, 45),
            AutoSize = true,
            ForeColor = Color.DarkSlateGray
        };

        // 2. Impresora y formato
        var lblEtqImpresora = new Label { Text = "Impresora:", Location = new Point(20, 80), AutoSize = true };
        cmbImpresora = new ComboBox
        {
            Location = new Point(140, 77),
            Width = 430,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbImpresora.SelectedIndexChanged += (_, _) =>
        {
            RecargarPapeles(conservarSeleccion: true);
            ActualizarAdvertencias();
        };

        var lblEtqFormato = new Label { Text = "Formato:", Location = new Point(20, 115), AutoSize = true };
        cmbFormato = new ComboBox
        {
            Location = new Point(140, 112),
            Width = 430,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbFormato.Items.Add(new ItemFormato("Media carta (216 × 140 mm)", FormatoBoleta.MediaCarta));
        cmbFormato.Items.Add(new ItemFormato("Ticket 80 mm", FormatoBoleta.Ticket80mm));
        cmbFormato.SelectedIndex = 0;

        var lblEtqPapel = new Label { Text = "Papel:", Location = new Point(20, 150), AutoSize = true };
        cmbPapel = new ComboBox
        {
            Location = new Point(140, 147),
            Width = 430,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var lblEtqCopias = new Label { Text = "Copias:", Location = new Point(20, 185), AutoSize = true };
        numCopias = new NumericUpDown
        {
            Location = new Point(140, 182),
            Width = 80,
            Minimum = 1,
            Maximum = 3,
            Value = 1
        };

        chkImprimirAlGrabar = new CheckBox
        {
            Text = "Imprimir la boleta automáticamente al grabar",
            Location = new Point(140, 215),
            AutoSize = true
        };
        chkImprimirAlGrabar.CheckedChanged += (_, _) => ActualizarAdvertencias();

        // 3. Calibración y desplazamientos
        var grpCalibracion = new GroupBox
        {
            Text = "Calibración de márgenes (desplazamiento)",
            Location = new Point(20, 250),
            Size = new Size(550, 100)
        };

        var lblEtqDesplX = new Label
        {
            Text = "Desplazamiento horizontal (mm, + = derecha):",
            Location = new Point(15, 28),
            AutoSize = true
        };
        numDesplX = new NumericUpDown
        {
            Location = new Point(340, 25),
            Width = 80,
            Minimum = -20m,
            Maximum = 20m,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = 0m
        };

        var lblEtqDesplY = new Label
        {
            Text = "Desplazamiento vertical (mm, + = abajo):",
            Location = new Point(15, 62),
            AutoSize = true
        };
        numDesplY = new NumericUpDown
        {
            Location = new Point(340, 59),
            Width = 80,
            Minimum = -20m,
            Maximum = 20m,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = 0m
        };

        btnCalibracion = new Button
        {
            Text = "Imprimir página de calibración",
            Location = new Point(430, 22),
            Size = new Size(110, 62)
        };
        btnCalibracion.Click += (_, _) => ImprimirCalibracion();

        grpCalibracion.Controls.AddRange(new Control[]
        {
            lblEtqDesplX, numDesplX,
            lblEtqDesplY, numDesplY,
            btnCalibracion
        });

        // 4. Advertencias
        lblAdvertencia = new Label
        {
            Location = new Point(20, 360),
            Size = new Size(550, 35),
            ForeColor = Color.DarkRed
        };

        // 5. Botones de acción
        btnVistaPrevia = new Button
        {
            Text = "Vista previa (muestra)",
            Location = new Point(20, 410),
            Size = new Size(150, 34)
        };
        btnVistaPrevia.Click += async (_, _) => await VistaPreviaMuestraAsync();

        btnImprimirMuestra = new Button
        {
            Text = "Imprimir muestra",
            Location = new Point(180, 410),
            Size = new Size(130, 34)
        };
        btnImprimirMuestra.Click += async (_, _) => await ImprimirMuestraAsync();

        btnGuardar = new Button
        {
            Text = "&Guardar",
            Location = new Point(330, 410),
            Size = new Size(110, 34)
        };
        btnGuardar.Click += async (_, _) => await GuardarAsync();

        btnCancelar = new Button
        {
            Text = "Cancelar",
            Location = new Point(450, 410),
            Size = new Size(110, 34)
        };
        btnCancelar.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            lblEquipo, lblEstadoConfig,
            lblEtqImpresora, cmbImpresora,
            lblEtqFormato, cmbFormato,
            lblEtqPapel, cmbPapel,
            lblEtqCopias, numCopias,
            chkImprimirAlGrabar,
            grpCalibracion,
            lblAdvertencia,
            btnVistaPrevia, btnImprimirMuestra, btnGuardar, btnCancelar
        });

        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;

        _controles = new Dictionary<string, Control>
        {
            [CamposImpresion.Impresora] = cmbImpresora,
            [CamposImpresion.Formato] = cmbFormato,
            [CamposImpresion.Papel] = cmbPapel,
            [CamposImpresion.Copias] = numCopias,
            [CamposImpresion.DesplazamientoX] = numDesplX,
            [CamposImpresion.DesplazamientoY] = numDesplY
        };

        Shown += async (_, _) => await EjecutarAsync(CargarInternoAsync);
    }

    private async Task CargarInternoAsync()
    {
        var equipo = await _config.ObtenerAsync();
        lblEquipo.Text = $"Equipo: {equipo.NombreEquipo}";
        lblEstadoConfig.Text = equipo.Configurada
            ? "Configuración guardada"
            : "Sin configurar: se usan valores por defecto";

        // Poblar impresoras
        cmbImpresora.Items.Clear();
        var pred = _impresor.ObtenerImpresoraPredeterminada();
        cmbImpresora.Items.Add(new ItemTexto($"(Predeterminada de Windows: {pred ?? "ninguna"})", null));

        var instaladas = _impresor.ListarImpresoras();
        foreach (var imp in instaladas)
        {
            cmbImpresora.Items.Add(new ItemTexto(imp, imp));
        }

        var configActual = equipo.Configuracion;
        var nombreImp = configActual.NombreImpresora;

        if (nombreImp is not null)
        {
            var encontrado = false;
            for (int i = 1; i < cmbImpresora.Items.Count; i++)
            {
                if (cmbImpresora.Items[i] is ItemTexto it && string.Equals(it.Valor, nombreImp, StringComparison.OrdinalIgnoreCase))
                {
                    cmbImpresora.SelectedIndex = i;
                    encontrado = true;
                    break;
                }
            }

            if (!encontrado)
            {
                var itemNoInstalada = new ItemTexto($"{nombreImp} (NO INSTALADA)", nombreImp);
                cmbImpresora.Items.Add(itemNoInstalada);
                cmbImpresora.SelectedItem = itemNoInstalada;
            }
        }
        else
        {
            cmbImpresora.SelectedIndex = 0;
        }

        // Formato
        for (int i = 0; i < cmbFormato.Items.Count; i++)
        {
            if (cmbFormato.Items[i] is ItemFormato f && f.Valor == configActual.Formato)
            {
                cmbFormato.SelectedIndex = i;
                break;
            }
        }

        // Papel
        RecargarPapeles(conservarSeleccion: false);
        if (configActual.NombrePapel is not null)
        {
            var papelEncontrado = false;
            for (int i = 1; i < cmbPapel.Items.Count; i++)
            {
                if (cmbPapel.Items[i] is ItemTexto it && string.Equals(it.Valor, configActual.NombrePapel, StringComparison.OrdinalIgnoreCase))
                {
                    cmbPapel.SelectedIndex = i;
                    papelEncontrado = true;
                    break;
                }
            }

            if (!papelEncontrado)
            {
                var itemNoDisp = new ItemTexto($"{configActual.NombrePapel} (no disponible)", configActual.NombrePapel);
                cmbPapel.Items.Add(itemNoDisp);
                cmbPapel.SelectedItem = itemNoDisp;
            }
        }
        else
        {
            cmbPapel.SelectedIndex = 0;
        }

        numCopias.Value = Math.Clamp(configActual.Copias, 1, 3);
        chkImprimirAlGrabar.Checked = configActual.ImprimirAlGrabar;
        numDesplX.Value = Math.Clamp(configActual.DesplazamientoXMm, -20m, 20m);
        numDesplY.Value = Math.Clamp(configActual.DesplazamientoYMm, -20m, 20m);

        ActualizarAdvertencias();
    }

    private void RecargarPapeles(bool conservarSeleccion)
    {
        var papelAnterior = (cmbPapel.SelectedItem as ItemTexto)?.Valor;
        cmbPapel.Items.Clear();
        cmbPapel.Items.Add(new ItemTexto("(Automático)", null));

        var impSeleccionada = (cmbImpresora.SelectedItem as ItemTexto)?.Valor;
        var papeles = _impresor.ListarPapeles(impSeleccionada);
        foreach (var p in papeles)
        {
            cmbPapel.Items.Add(new ItemTexto(p, p));
        }

        if (conservarSeleccion && papelAnterior is not null)
        {
            var encontrado = false;
            for (int i = 1; i < cmbPapel.Items.Count; i++)
            {
                if (cmbPapel.Items[i] is ItemTexto it && string.Equals(it.Valor, papelAnterior, StringComparison.OrdinalIgnoreCase))
                {
                    cmbPapel.SelectedIndex = i;
                    encontrado = true;
                    break;
                }
            }
            if (!encontrado) cmbPapel.SelectedIndex = 0;
        }
        else
        {
            cmbPapel.SelectedIndex = 0;
        }
    }

    internal static bool EsImpresoraVirtual(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;
        return nombre.Contains("PDF", StringComparison.OrdinalIgnoreCase) ||
               nombre.Contains("XPS", StringComparison.OrdinalIgnoreCase) ||
               nombre.Contains("OneNote", StringComparison.OrdinalIgnoreCase) ||
               nombre.Contains("Fax", StringComparison.OrdinalIgnoreCase);
    }

    private void ActualizarAdvertencias()
    {
        var itemImp = cmbImpresora.SelectedItem as ItemTexto;
        if (itemImp?.Texto.EndsWith("(NO INSTALADA)", StringComparison.Ordinal) == true)
        {
            lblAdvertencia.Text = "La impresora configurada no está instalada en este equipo.";
            return;
        }

        var efectiva = itemImp?.Valor ?? _impresor.ObtenerImpresoraPredeterminada();
        if (chkImprimirAlGrabar.Checked && EsImpresoraVirtual(efectiva))
        {
            lblAdvertencia.Text = "Impresora virtual: pedirá un nombre de archivo en cada impresión. No se recomienda con la impresión automática.";
            return;
        }

        lblAdvertencia.Text = string.Empty;
    }

    private ConfiguracionImpresion LeerConfiguracion()
    {
        var imp = (cmbImpresora.SelectedItem as ItemTexto)?.Valor;
        var formato = (cmbFormato.SelectedItem as ItemFormato)?.Valor ?? FormatoBoleta.MediaCarta;
        var papel = (cmbPapel.SelectedItem as ItemTexto)?.Valor;
        var copias = (int)numCopias.Value;
        var imprimirAlGrabar = chkImprimirAlGrabar.Checked;
        var desplX = numDesplX.Value;
        var desplY = numDesplY.Value;

        return new ConfiguracionImpresion(imp, formato, papel, copias, imprimirAlGrabar, desplX, desplY);
    }

    private async Task GuardarAsync()
    {
        bool guardado = false;
        await EjecutarAsync(async () =>
        {
            try
            {
                await _config.GuardarAsync(LeerConfiguracion());
                guardado = true;
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controles))
            {
            }
        }, btnGuardar);

        if (guardado)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task VistaPreviaMuestraAsync()
    {
        PrintDocument? doc = null;
        await EjecutarAsync(async () =>
        {
            var datos = await _boletas.CrearMuestraAsync();
            doc = _impresor.CrearVistaPrevia(datos, LeerConfiguracion());
        }, btnVistaPrevia);

        if (doc is null) return;

        Ejecutar(() =>
        {
            using var frm = new FrmVistaPrevia(doc, "Vista previa — boleta de muestra");
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

    private async Task ImprimirMuestraAsync()
    {
        await EjecutarAsync(async () =>
        {
            var datos = await _boletas.CrearMuestraAsync();
            _impresor.Imprimir(datos, LeerConfiguracion());
        }, btnImprimirMuestra);
    }

    private void ImprimirCalibracion()
    {
        Ejecutar(() => _impresor.ImprimirCalibracion(LeerConfiguracion()), btnCalibracion);
    }

    internal Task CargarParaTestAsync() => CargarInternoAsync();
    internal Task GuardarParaTestAsync() => GuardarAsync();
    internal Task VistaPreviaParaTestAsync() => VistaPreviaMuestraAsync();
    internal Task ImprimirMuestraParaTestAsync() => ImprimirMuestraAsync();
    internal void CalibrarParaTest() => ImprimirCalibracion();
    internal ConfiguracionImpresion LeerConfiguracionParaTest() => LeerConfiguracion();
}

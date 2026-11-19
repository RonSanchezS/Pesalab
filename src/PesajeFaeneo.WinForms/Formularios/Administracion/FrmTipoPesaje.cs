using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.WinForms.Formularios.Administracion;

/// <summary>
/// F11 (D3-D5): tipo de pesaje de la empresa (res completa, media res o ambos). Se abre desde el asistente de puesta en
/// marcha y desde Administración › Tipo de pesaje. Sin modo guardado no hay opción marcada: la elección es explícita.
/// </summary>
public sealed class FrmTipoPesaje : FormBase
{
    private readonly IServicioParametros _servicio;

    private ConfiguracionDesposte? _configuracion;

    internal RadioButton rbAmbos = null!;
    internal RadioButton rbSoloResCompleta = null!;
    internal RadioButton rbSoloMediaRes = null!;
    internal Label lblAviso = null!;
    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;

    /// <summary>Reemplaza el cuadro de confirmación al cambiar un modo ya definido; devuelve true si se acepta.</summary>
    internal Func<string, bool>? ConfirmarParaTest { get; set; }

    internal Task CargarParaTestAsync() => CargarAsync();
    internal Task GuardarParaTestAsync() => GuardarAsync();

    public FrmTipoPesaje(IServicioParametros servicio)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));

        InicializarComponentes();

        Shown += async (_, _) => await EjecutarAsync(CargarAsync);
    }

    private void InicializarComponentes()
    {
        SuspendLayout();

        Text = "Tipo de pesaje";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var raiz = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(20, 16, 20, 16)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblIntro = new Label
        {
            Text = "Elija qué pesa su empresa. Define qué se muestra al pesar, en las boletas y en los reportes.",
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        raiz.Controls.Add(lblIntro);

        rbAmbos = AgregarOpcion(raiz, "Res completa y media res", "Se elige en cada pesaje.");
        rbSoloResCompleta = AgregarOpcion(raiz, "Solo res completa",
            "Siempre se pesan los dos lados. No se muestra «media res» en el pesaje, las boletas ni los reportes.");
        rbSoloMediaRes = AgregarOpcion(raiz, "Solo media res", "Se pesa un solo lado por boleta.");

        var lblNota = new Label
        {
            Text = "Cambiar esta opción no modifica las boletas ya grabadas.",
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 4)
        };
        raiz.Controls.Add(lblNota);

        lblAviso = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = Color.DarkOrange,
            Margin = new Padding(0, 0, 0, 8)
        };
        raiz.Controls.Add(lblAviso);

        var flpBotones = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };

        btnCancelar = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(100, 32),
            Margin = new Padding(0)
        };
        btnGuardar = new Button
        {
            Text = "Guardar",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(100, 32),
            Enabled = false,
            Margin = new Padding(0, 0, 10, 0)
        };
        btnGuardar.Click += async (_, _) => await GuardarAsync();

        flpBotones.Controls.Add(btnCancelar);
        flpBotones.Controls.Add(btnGuardar);
        raiz.Controls.Add(flpBotones);

        Controls.Add(raiz);
        CancelButton = btnCancelar;

        ResumeLayout(false);
        PerformLayout();
    }

    private RadioButton AgregarOpcion(TableLayoutPanel raiz, string titulo, string detalle)
    {
        var opcion = new RadioButton
        {
            Text = titulo,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 0)
        };
        opcion.CheckedChanged += (_, _) => Pintar();

        var lblDetalle = new Label
        {
            Text = detalle,
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Margin = new Padding(20, 0, 0, 6)
        };

        raiz.Controls.Add(opcion);
        raiz.Controls.Add(lblDetalle);
        return opcion;
    }

    private async Task CargarAsync()
    {
        _configuracion = await _servicio.ObtenerConfiguracionDesposteAsync();

        rbAmbos.Checked = _configuracion.Modo == ModoDesposte.Ambos;
        rbSoloResCompleta.Checked = _configuracion.Modo == ModoDesposte.SoloResCompleta;
        rbSoloMediaRes.Checked = _configuracion.Modo == ModoDesposte.SoloMediaRes;
        Pintar();
    }

    internal ModoDesposte? ModoSeleccionado =>
        rbAmbos.Checked ? ModoDesposte.Ambos :
        rbSoloResCompleta.Checked ? ModoDesposte.SoloResCompleta :
        rbSoloMediaRes.Checked ? ModoDesposte.SoloMediaRes :
        null;

    private void Pintar()
    {
        var modo = ModoSeleccionado;
        btnGuardar.Enabled = modo is not null && _configuracion is not null;
        lblAviso.Text = modo is { } m && _configuracion is not null ? TextoAviso(m, _configuracion) : string.Empty;
    }

    /// <summary>D6: las boletas de un tipo que el modo oculta siguen mostrando su desposte donde aparezcan.</summary>
    internal static string TextoAviso(ModoDesposte modo, ConfiguracionDesposte configuracion)
    {
        var (cantidad, tipo) = ReglasDesposte.TipoUnico(modo) switch
        {
            TipoDesposte.ResCompleta => (configuracion.BoletasMediaRes, "media res"),
            TipoDesposte.MediaRes => (configuracion.BoletasResCompleta, "res completa"),
            _ => (0, string.Empty)
        };

        if (cantidad == 0)
        {
            return string.Empty;
        }

        var boletas = cantidad == 1 ? "Hay 1 boleta" : $"Hay {cantidad} boletas";
        return $"{boletas} de {tipo}; seguirán mostrando su tipo de desposte en las pantallas y reportes que las incluyan.";
    }

    private async Task GuardarAsync()
    {
        if (ModoSeleccionado is not { } modo || _configuracion is null || Ocupado)
        {
            return;
        }

        if (_configuracion.Modo is { } anterior && anterior != modo)
        {
            var texto = $"Cambiará el tipo de pesaje de «{ReglasDesposte.Texto(anterior)}» a «{ReglasDesposte.Texto(modo)}». ¿Continuar?";
            if (!Confirmar(texto))
            {
                return;
            }
        }

        var guardado = false;
        await EjecutarAsync(async () =>
        {
            await _servicio.CambiarModoDesposteAsync(modo);
            guardado = true;
        }, btnGuardar);

        if (guardado)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private bool Confirmar(string texto)
    {
        if (ConfirmarParaTest is not null)
        {
            return ConfirmarParaTest(texto);
        }

        return MessageBox.Show(this, texto, "Tipo de pesaje", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }
}

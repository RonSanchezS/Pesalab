using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.WinForms.Formularios.Licencias;

/// <summary>Contexto en el que se abre la pantalla de activación (F13, D5).</summary>
public enum ModoActivacion
{
    /// <summary>Al arrancar, sin prueba iniciada: Activar, Probar 15 días y Salir.</summary>
    NoIniciada,

    /// <summary>Al arrancar, con la prueba vencida: Activar, Continuar en modo consulta y Salir.</summary>
    Vencida,

    /// <summary>Desde Ayuda › Activar licencia: Activar y Cerrar.</summary>
    DesdeMenu
}

/// <summary>Qué eligió el usuario al cerrar <see cref="FrmActivacion"/>.</summary>
public enum ResultadoActivacion
{
    /// <summary>Cerró sin elegir (solo desde el menú).</summary>
    Ninguno,
    Activada,
    PruebaIniciada,
    ModoConsulta,
    Salir
}

/// <summary>
/// F13 (D5, D9): clave de activación, prueba de 15 días y modo consulta. Se abre antes del inicio de sesión (con la prueba sin
/// iniciar o vencida) y desde Ayuda › Activar licencia.
/// </summary>
public sealed class FrmActivacion : FormBase
{
    private const string TextoAyudaConsulta =
        " Ingrese una clave de activación o continúe en modo consulta (podrá ver el historial, los reportes y las copias de seguridad, pero no pesar).";

    private readonly IServicioLicencia _licencia;

    private bool _cerrando;

    internal Label lblIntro = null!;
    internal Label lblClave = null!;
    internal TextBox txtClave = null!;
    internal Label lblCodigoEquipo = null!;
    internal Label lblError = null!;
    internal Button btnActivar = null!;
    internal Button btnProbar = null!;
    internal Button btnConsulta = null!;
    internal Button btnSalir = null!;

    public ModoActivacion Modo { get; private set; } = ModoActivacion.DesdeMenu;

    public MotivoVencimiento? Motivo { get; private set; }

    /// <summary>Lo que el usuario eligió; <see cref="ResultadoActivacion.Salir"/> también si cerró la ventana al arrancar.</summary>
    public ResultadoActivacion Resultado { get; private set; }

    internal Task ActivarParaTestAsync() => ActivarAsync();
    internal Task IniciarPruebaParaTestAsync() => IniciarPruebaAsync();

    public FrmActivacion(IServicioLicencia licencia)
    {
        _licencia = licencia ?? throw new ArgumentNullException(nameof(licencia));

        InicializarComponentes();
        Configurar(ModoActivacion.DesdeMenu);
    }

    /// <summary>Ajusta textos y botones al contexto. Se llama antes de mostrar el formulario.</summary>
    public void Configurar(ModoActivacion modo, MotivoVencimiento? motivo = null)
    {
        Modo = modo;
        Motivo = motivo;
        Resultado = ResultadoActivacion.Ninguno;

        lblIntro.Text = TextoIntro(modo, motivo);
        lblError.Text = string.Empty;

        btnProbar.Visible = modo == ModoActivacion.NoIniciada;
        btnConsulta.Visible = modo == ModoActivacion.Vencida;
        btnSalir.Text = modo == ModoActivacion.DesdeMenu ? "Cerrar" : "Salir";

        var alArrancar = modo != ModoActivacion.DesdeMenu;
        StartPosition = alArrancar ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        ShowInTaskbar = alArrancar;
    }

    internal static string TextoIntro(ModoActivacion modo, MotivoVencimiento? motivo) => modo switch
    {
        ModoActivacion.NoIniciada =>
            "Ingrese su clave de activación. Si todavía no tiene una, puede probar el sistema durante 15 días.",
        ModoActivacion.Vencida => motivo switch
        {
            MotivoVencimiento.Reloj => "Se detectó un cambio en el reloj del sistema." + TextoAyudaConsulta,
            MotivoVencimiento.Alterada => "Se detectó una alteración en los datos de la prueba." + TextoAyudaConsulta,
            _ => "La prueba de 15 días terminó." + TextoAyudaConsulta
        },
        _ => "Ingrese su clave de activación para desbloquear la versión completa."
    };

    private void InicializarComponentes()
    {
        SuspendLayout();

        Text = "Activar Capataz";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
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

        lblIntro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 0, 0, 14)
        };
        raiz.Controls.Add(lblIntro);

        lblClave = new Label
        {
            Text = "Clave de activación",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        raiz.Controls.Add(lblClave);

        txtClave = new TextBox
        {
            Width = 420,
            MaxLength = 40,
            CharacterCasing = CharacterCasing.Upper,
            PlaceholderText = "XXXX-XXXX-XXXX-XXXX-XXXX",
            Margin = new Padding(0, 0, 0, 6)
        };
        txtClave.TextChanged += (_, _) => lblError.Text = string.Empty;
        raiz.Controls.Add(txtClave);

        lblError = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = Color.DarkRed,
            Margin = new Padding(0, 0, 0, 6)
        };
        raiz.Controls.Add(lblError);

        lblCodigoEquipo = new Label
        {
            Text = $"Código de equipo: {_licencia.CodigoEquipo}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 8)
        };
        raiz.Controls.Add(lblCodigoEquipo);

        var botones = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };

        btnSalir = CrearBoton("Salir", new Padding(0));
        btnSalir.Click += (_, _) => Cerrar(Modo == ModoActivacion.DesdeMenu ? ResultadoActivacion.Ninguno : ResultadoActivacion.Salir);

        btnConsulta = CrearBoton("Continuar en modo consulta", new Padding(0, 0, 10, 0));
        btnConsulta.Click += (_, _) => Cerrar(ResultadoActivacion.ModoConsulta);

        btnProbar = CrearBoton("Probar 15 días", new Padding(0, 0, 10, 0));
        btnProbar.Click += async (_, _) => await IniciarPruebaAsync();

        btnActivar = CrearBoton("Activar", new Padding(0, 0, 10, 0));
        btnActivar.Click += async (_, _) => await ActivarAsync();

        botones.Controls.Add(btnSalir);
        botones.Controls.Add(btnConsulta);
        botones.Controls.Add(btnProbar);
        botones.Controls.Add(btnActivar);
        raiz.Controls.Add(botones);

        Controls.Add(raiz);
        AcceptButton = btnActivar;
        CancelButton = btnSalir;

        ResumeLayout(false);
        PerformLayout();
    }

    private static Button CrearBoton(string texto, Padding margen) => new()
    {
        Text = texto,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowOnly,
        MinimumSize = new Size(100, 32),
        Padding = new Padding(8, 0, 8, 0),
        Margin = margen
    };

    private async Task ActivarAsync()
    {
        if (Ocupado)
        {
            return;
        }

        lblError.Text = string.Empty;
        var clave = txtClave.Text;
        var activada = false;

        await EjecutarAsync(async () =>
        {
            try
            {
                await _licencia.ActivarAsync(clave);
                activada = true;
            }
            catch (ValidacionException ex)
            {
                MostrarError(ex.Errores.Count > 0 ? ex.Errores[0].Mensaje : ex.Message);
            }
            catch (DomainException ex)
            {
                // Clave inválida, usada o revocada, sin conexión o error del servidor: el mensaje ya viene redactado.
                MostrarError(ex.Message);
            }
        }, btnActivar);

        if (activada)
        {
            Cerrar(ResultadoActivacion.Activada);
        }
    }

    private async Task IniciarPruebaAsync()
    {
        if (Ocupado)
        {
            return;
        }

        lblError.Text = string.Empty;
        EstadoLicencia? estado = null;

        await EjecutarAsync(async () =>
        {
            try
            {
                estado = await _licencia.IniciarPruebaAsync();
            }
            catch (DomainException ex)
            {
                MostrarError(ex.Message);
            }
        }, btnProbar);

        if (estado is null)
        {
            return;
        }

        if (estado.PermiteOperar)
        {
            Cerrar(ResultadoActivacion.PruebaIniciada);
            return;
        }

        // El servidor ya conocía este equipo con una fecha antigua: la prueba nace vencida. Se pasa al modo vencido.
        Configurar(ModoActivacion.Vencida, estado.Motivo ?? MotivoVencimiento.PruebaTerminada);
    }

    private void MostrarError(string mensaje)
    {
        lblError.Text = mensaje;
        txtClave.Focus();
        txtClave.SelectAll();
    }

    private void Cerrar(ResultadoActivacion resultado)
    {
        Resultado = resultado;
        _cerrando = true;
        DialogResult = resultado is ResultadoActivacion.Ninguno or ResultadoActivacion.Salir
            ? DialogResult.Cancel
            : DialogResult.OK;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // No se cierra con una activación en curso (el resultado se perdería y la clave quedaría consumida).
        if (Ocupado)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Cerrar la ventana al arrancar (X, Alt+F4) equivale a Salir: nunca se llega al inicio de sesión sin licencia.
        if (!_cerrando && Modo != ModoActivacion.DesdeMenu && Resultado == ResultadoActivacion.Ninguno)
        {
            Resultado = ResultadoActivacion.Salir;
        }

        base.OnFormClosed(e);
    }
}

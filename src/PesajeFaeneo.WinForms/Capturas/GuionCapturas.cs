#if CAPTURAS_MANUAL
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Infrastructure.Balanza.Demo;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;
using Serilog;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>
/// Guion de capturas para los manuales (solo build CAPTURAS_MANUAL): abre las pantallas reales con el IFormFactory real,
/// las deja en el estado de cada paso y las captura con PrintWindow. Las pantallas se muestran sin modalidad (Show) para
/// poder seguir el guion; los cuadros de mensaje (modales) se detectan por su ventana y se cierran tras capturarlos.
/// </summary>
internal sealed partial class GuionCapturas
{
    private const int TiempoMaximoEscenaMs = 90_000;
    private static readonly Size TamanoShell = new(1200, 680);
    private static readonly Size TamanoPesaje = new(1024, 700);

    private readonly IServiceProvider _servicios;
    private readonly IFormFactory _fabrica;
    private readonly ISesionActual _sesion;
    private readonly IServicioBalanza _balanza;
    private readonly Capturador _cap;
    private readonly ConfigDemo _cfg;
    private readonly ArgumentosCapturas _args;
    private readonly SynchronizationContext _ui;
    private readonly List<Form> _abiertos = new();
    private readonly List<string> _fallos = new();
    private readonly List<string> _omitidas = new();
    private int _hechas;
    private bool _esperandoCuadro;

    public GuionCapturas(IServiceProvider servicios, ArgumentosCapturas args, ConfigDemo cfg, Capturador cap)
    {
        _servicios = servicios;
        _fabrica = servicios.GetRequiredService<IFormFactory>();
        _sesion = servicios.GetRequiredService<ISesionActual>();
        _balanza = servicios.GetRequiredService<IServicioBalanza>();
        _args = args;
        _cfg = cfg;
        _cap = cap;
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("El guion debe iniciarse en el hilo de UI.");
    }

    public IReadOnlyList<string> Fallos => _fallos;
    public IReadOnlyList<string> Omitidas => _omitidas;
    public int Hechas => _hechas;

    public async Task EjecutarAsync()
    {
        // Vigilante: un cuadro de mensaje inesperado nunca debe detener el guion.
        using var vigilante = new System.Windows.Forms.Timer { Interval = 400 };
        vigilante.Tick += (_, _) =>
        {
            if (_esperandoCuadro)
            {
                return;
            }

            var h = BuscarCuadro();
            if (h != IntPtr.Zero)
            {
                Log.Warning("Capturas: cuadro de mensaje inesperado '{Titulo}' cerrado por el vigilante.", Win32Capturas.Titulo(h));
                CerrarCuadro(h, Boton.No);
            }
        };
        vigilante.Start();

        BotonesEnEspanol.Instalar(); // los cuadros de mensaje salen con los botones en español
        try
        {
            BalanzaDemoReiniciar();
            if (_args.SinPesajes)
            {
                // Corrida aparte, con datos sin ningún pesaje: solo las pantallas previas a la primera boleta.
                await EscenasDatosVaciosAsync();
                return;
            }

            await EscenasComunAntesDeSesionAsync();
            await EscenasOperadorAsync();
            await EscenasOperadorExtraAsync();
            await EscenasSupervisorAsync();
            await EscenasSupervisorExtraAsync();
            await EscenasAdministradorAsync();
            await EscenasAdministradorExtraAsync();
            await EscenasComunConSesionAsync();
        }
        finally
        {
            BotonesEnEspanol.Quitar();
        }
    }

    // ───────────────────────── Núcleo de escenas ─────────────────────────

    private bool Seleccionada(string id) =>
        _args.Solo.Count == 0 || _args.Solo.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Ejecuta una escena (grupo de capturas) con tiempo máximo; nunca propaga errores.</summary>
    private async Task Escena(string id, Func<Task> cuerpo)
    {
        if (!Seleccionada(id))
        {
            return;
        }

        Console.WriteLine($"[capturas] {id} ...");
        try
        {
            var tarea = cuerpo();
            var primero = await Task.WhenAny(tarea, Task.Delay(TiempoMaximoEscenaMs));
            if (primero != tarea)
            {
                throw new TimeoutException($"La escena '{id}' superó {TiempoMaximoEscenaMs / 1000} s.");
            }

            await tarea;
            _hechas++;
        }
        catch (Exception ex)
        {
            _fallos.Add($"{id}: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[capturas] FALLÓ {id}: {ex}");
            Log.Error(ex, "Capturas: falló la escena {Id}", id);
        }
        finally
        {
            await LimpiarAsync();
        }
    }

    private void Omitir(string id, string motivo)
    {
        _omitidas.Add($"{id}: {motivo}");
        Console.WriteLine($"[capturas] OMITIDA {id}: {motivo}");
    }

    // ───────────────────────── Formularios ─────────────────────────

    /// <summary>Crea el formulario con la fábrica real, lo coloca y lo muestra sin modalidad.</summary>
    private async Task<T> AbrirAsync<T>(Action<T>? configurar = null, Size? tamano = null, bool esperar = true) where T : Form
    {
        var f = _fabrica.Crear<T>();
        configurar?.Invoke(f);
        var objetivo = Preparar(f, tamano);
        _abiertos.Add(f);
        f.Show();
        if (f.Size != objetivo)
        {
            // Algunos formularios (p. ej. los que nacen maximizados) vuelven a su tamaño mínimo al mostrarse.
            f.Size = objetivo;
            Ubicar(f);
        }
        Capturador.QuitarEsquinasRedondeadas(f);
        f.Activate();
        Console.WriteLine($"[capturas]   {typeof(T).Name}: tamaño {f.Size} estado {f.WindowState} dpi {f.DeviceDpi}");
        if (esperar)
        {
            await EstabilizarAsync(f);
        }

        return f;
    }

    private static Size Preparar(Form f, Size? tamano)
    {
        f.StartPosition = FormStartPosition.Manual;
        f.ShowInTaskbar = true;
        if (f.WindowState == FormWindowState.Maximized)
        {
            f.WindowState = FormWindowState.Normal;
            f.Size = tamano ?? TamanoShell;
        }
        else if (tamano is { } t)
        {
            f.Size = t;
        }

        Ubicar(f);
        return f.Size;
    }

    /// <summary>Ventana siempre completa dentro de la pantalla (el proceso no tiene escalado: coordenadas de 96 DPI).</summary>
    private static void Ubicar(Form f)
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        f.Location = new Point(Math.Max(0, Math.Min(30, area.Width - f.Width)), Math.Max(0, Math.Min(10, area.Height - f.Height)));
    }

    /// <summary>Espera a que el formulario termine sus cargas asíncronas (EjecutarAsync) y se pinte.</summary>
    private async Task EstabilizarAsync(Form f, int minimoMs = 450)
    {
        var calmas = 0;
        var tope = Environment.TickCount64 + 20_000;
        var arranque = Environment.TickCount64;
        while (Environment.TickCount64 < tope)
        {
            await Task.Delay(150);
            if (f.IsDisposed)
            {
                return;
            }

            calmas = f.UseWaitCursor ? 0 : calmas + 1;
            if (calmas >= 3 && Environment.TickCount64 - arranque >= minimoMs)
            {
                break;
            }
        }

        f.Refresh();
        await Task.Delay(200);
    }

    private async Task LimpiarAsync()
    {
        _esperandoCuadro = false;
        foreach (var f in _abiertos.ToArray())
        {
            if (f.IsDisposed)
            {
                continue;
            }

            try
            {
                if (f is Formularios.Pesaje.FrmPesaje p)
                {
                    p.ConfirmarCierre = false;
                }

                f.Close();
                if (!f.IsDisposed)
                {
                    f.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Capturas: no se pudo cerrar {Tipo}", f.GetType().Name);
            }
        }

        _abiertos.Clear();
        var h = BuscarCuadro();
        if (h != IntPtr.Zero)
        {
            CerrarCuadro(h, Boton.No);
        }

        await Task.Delay(150);
    }

    private static async Task EsperarAsync(int ms = 300) => await Task.Delay(ms);

    private static async Task EsperarHastaAsync(Func<bool> condicion, int tiempoMs = 15_000, string? que = null)
    {
        var tope = Environment.TickCount64 + tiempoMs;
        while (!condicion())
        {
            if (Environment.TickCount64 > tope)
            {
                throw new TimeoutException($"Tiempo de espera agotado: {que ?? "condición"}.");
            }

            await Task.Delay(100);
        }
    }

    // ───────────────────────── Captura ─────────────────────────

    private void Foto(string id, string titulo, string descripcion, Form f, params object?[] destacados) =>
        FotoInterna(id, titulo, descripcion, f, soloDestacados: false, destacados);

    /// <summary>Como Foto, pero el JSON solo lista los controles indicados.</summary>
    private void FotoSolo(string id, string titulo, string descripcion, Form f, params object?[] destacados) =>
        FotoInterna(id, titulo, descripcion, f, soloDestacados: true, destacados);

    /// <summary>Como Foto, pero la imagen se recorta a un control (por ejemplo, la barra de estado).</summary>
    private void FotoRegion(string id, string titulo, string descripcion, Form f, Control region, params object?[] destacados) =>
        FotoInterna(id, titulo, descripcion, f, soloDestacados: true, destacados, region);

    /// <summary>Como FotoRegion, pero la región es un rectángulo de pantalla (por ejemplo, la barra de menús más su desplegable).</summary>
    private void FotoRecorte(string id, string titulo, string descripcion, Form f, Rectangle regionPantalla, int margen, params object?[] destacados) =>
        FotoInterna(id, titulo, descripcion, f, soloDestacados: true, destacados, null, regionPantalla, margen);

    private void FotoInterna(string id, string titulo, string descripcion, Form f, bool soloDestacados, object?[] destacados, Control? region = null, Rectangle? regionPantalla = null, int margen = 6)
    {
        f.Activate();
        Win32Capturas.SetForegroundWindow(f.Handle); // ventana activa: la barra de título se ve normal, no atenuada
        f.Refresh();
        Win32Capturas.HideCaret(Win32Capturas.GetFocus()); // el cursor de texto (a veces muy grueso) no sale en las capturas
        AvisarRecortes(f);
        _cap.Capturar(id, titulo, descripcion, f, destacados.Where(d => d is not null)!, soloDestacados, region, margen, regionPantalla);
        Console.WriteLine($"[capturas]   -> {id}.png");
    }

    private static bool TieneBarraHorizontal(DataGridView g) =>
        typeof(DataGridView).GetProperty("HorizontalScrollBar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(g) is ScrollBar { Visible: true };

    /// <summary>Avisa en el log si algo de la pantalla queda recortado (grilla con barra horizontal o control fuera de su contenedor).</summary>
    private static void AvisarRecortes(Form f)
    {
        foreach (var c in Capturador.Descendientes(f))
        {
            if (!c.Visible)
            {
                continue;
            }

            if (c is DataGridView g && TieneBarraHorizontal(g))
            {
                Console.WriteLine($"[capturas]   AVISO RECORTE: la grilla {c.Name}{c.GetType().Name} tiene barra horizontal (columnas cortadas) en {f.GetType().Name}.");
            }
            else if (c.Parent is not null and not ScrollableControl { AutoScroll: true } && c.Parent is not TabControl && c.Right > c.Parent.ClientSize.Width + 2 && c is not Form)
            {
                Console.WriteLine($"[capturas]   AVISO RECORTE: {c.GetType().Name} '{(c is ButtonBase or Label ? c.Text : c.Name)}' sale del contenedor en {f.GetType().Name}.");
            }
        }
    }

    // ───────────────────────── Cuadros de mensaje (modales) ─────────────────────────

    internal enum Boton { Aceptar = 1, Cancelar = 2, Si = 6, No = 7 }

    private static IntPtr BuscarCuadro()
    {
        foreach (var h in Win32Capturas.VentanasDelProceso())
        {
            if (Win32Capturas.Clase(h) == "#32770")
            {
                return h;
            }
        }

        return IntPtr.Zero;
    }

    private static void CerrarCuadro(IntPtr h, Boton preferido)
    {
        Win32Capturas.PostMessage(h, Win32Capturas.WM_COMMAND, (IntPtr)(int)preferido, IntPtr.Zero);
        Win32Capturas.PostMessage(h, Win32Capturas.WM_COMMAND, (IntPtr)(int)Boton.Cancelar, IntPtr.Zero);
        Win32Capturas.PostMessage(h, Win32Capturas.WM_COMMAND, (IntPtr)(int)Boton.Aceptar, IntPtr.Zero);
    }

    /// <summary>
    /// Dispara una acción que abre un cuadro de mensaje modal (sin esperarla), lo captura y lo cierra con el botón indicado.
    /// Devuelve true si apareció el cuadro.
    /// </summary>
    private async Task<bool> CapturarCuadroAsync(string id, string titulo, string descripcion, Action disparador, Boton cerrarCon, int esperaMs = 8000)
    {
        _esperandoCuadro = true;
        try
        {
            _ui.Post(_ => disparador(), null);
            var h = IntPtr.Zero;
            var tope = Environment.TickCount64 + esperaMs;
            while (Environment.TickCount64 < tope && (h = BuscarCuadro()) == IntPtr.Zero)
            {
                await Task.Delay(100);
            }

            if (h == IntPtr.Zero)
            {
                throw new TimeoutException($"No apareció el cuadro de mensaje de '{id}'.");
            }

            await Task.Delay(500);
            Win32Capturas.SetForegroundWindow(h);
            await Task.Delay(150);
            _cap.CapturarNativa(id, titulo, descripcion, h);
            Console.WriteLine($"[capturas]   -> {id}.png (cuadro: {Win32Capturas.Titulo(h)})");
            CerrarCuadro(h, cerrarCon);
            tope = Environment.TickCount64 + 5000;
            while (Environment.TickCount64 < tope && BuscarCuadro() != IntPtr.Zero)
            {
                await Task.Delay(100);
            }

            await Task.Delay(300);
            return true;
        }
        finally
        {
            _esperandoCuadro = false;
        }
    }

    // ───────────────────────── Reflexión sobre los formularios ─────────────────────────

    private static T Campo<T>(object objetivo, string nombre) where T : class
    {
        var tipo = objetivo.GetType();
        while (tipo is not null)
        {
            var campo = tipo.GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (campo is not null)
            {
                return (campo.GetValue(objetivo) as T) ?? throw new InvalidOperationException($"El campo {nombre} de {objetivo.GetType().Name} es nulo.");
            }

            var prop = tipo.GetProperty(nombre, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (prop is not null)
            {
                return (prop.GetValue(objetivo) as T) ?? throw new InvalidOperationException($"La propiedad {nombre} de {objetivo.GetType().Name} es nula.");
            }

            tipo = tipo.BaseType;
        }

        throw new MissingFieldException(objetivo.GetType().Name, nombre);
    }

    private static object? Invocar(object objetivo, string metodo, params object?[] args)
    {
        var tipo = objetivo.GetType();
        while (tipo is not null)
        {
            var m = tipo.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .FirstOrDefault(x => x.Name == metodo && x.GetParameters().Length == args.Length);
            if (m is not null)
            {
                return m.Invoke(objetivo, args);
            }

            tipo = tipo.BaseType;
        }

        throw new MissingMethodException(objetivo.GetType().Name, metodo);
    }

    private static async Task InvocarAsync(object objetivo, string metodo, params object?[] args)
    {
        if (Invocar(objetivo, metodo, args) is Task t)
        {
            await t;
        }
    }

    // ───────────────────────── Sesión ─────────────────────────

    private async Task IniciarSesionAsync(string usuario, string password)
    {
        await CerrarSesionSiHayAsync();
        using var scope = _servicios.CreateScope();
        var r = await scope.ServiceProvider.GetRequiredService<IServicioAutenticacion>().IniciarSesionAsync(usuario, password);
        if (r.Tipo != ResultadoLoginTipo.Exitoso)
        {
            throw new InvalidOperationException($"No se pudo iniciar sesión como {usuario}: {r.Mensaje}");
        }
    }

    private async Task CerrarSesionSiHayAsync()
    {
        if (_sesion.EstaAutenticada)
        {
            using var scope = _servicios.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();
        }
    }

    // ───────────────────────── Balanza de demostración ─────────────────────────

    private static void BalanzaDemoReiniciar() => ControladorBalanzaDemo.ReiniciarGuion();

    /// <summary>Fuerza el peso de la balanza de demostración y espera a que quede estable (o en 0 sin estabilidad exigida).</summary>
    private async Task ForzarPesoAsync(decimal peso, bool esperarEstable = true)
    {
        ControladorBalanzaDemo.PesoForzado = peso;
        await EsperarHastaAsync(() =>
        {
            var i = _balanza.Instantanea;
            if (i.Estado != EstadoBalanza.Conectada || i.PesoKg is null)
            {
                return false;
            }

            if (Math.Abs(i.PesoKg.Value - peso) > 0.05m)
            {
                return false;
            }

            return !esperarEstable || i.Estable;
        }, 30_000, $"peso estable {peso}");
        await Task.Delay(400);
    }

    private async Task EsperarBalanzaConectadaAsync()
    {
        await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.Conectada, 30_000, "balanza conectada");
    }
}
#endif

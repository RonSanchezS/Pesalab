#if CAPTURAS_MANUAL
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WinFormsApp = System.Windows.Forms.Application;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>Contexto de aplicación del build de capturas: en lugar del login real, corre el guion y sale.</summary>
internal sealed class ContextoCapturas : ApplicationContext
{
    private readonly IServiceProvider _servicios;
    private readonly ArgumentosCapturas _args;
    private readonly ConfigDemo _cfg;
    private bool _iniciado;

    public int CodigoSalida { get; private set; } = 1;

    public ContextoCapturas(IServiceProvider servicios, ArgumentosCapturas args, ConfigDemo cfg)
    {
        _servicios = servicios;
        _args = args;
        _cfg = cfg;
        WinFormsApp.Idle += AlReposo;
    }

    private void AlReposo(object? sender, EventArgs e)
    {
        if (_iniciado)
        {
            return;
        }

        _iniciado = true;
        WinFormsApp.Idle -= AlReposo;
        _ = CorrerAsync();
    }

    private async Task CorrerAsync()
    {
        try
        {
            var capturador = new Capturador(_args.Salida!);
            var guion = new GuionCapturas(_servicios, _args, _cfg, capturador);
            await guion.EjecutarAsync();
            capturador.GuardarIndice();

            Console.WriteLine();
            Console.WriteLine($"[capturas] Escenas correctas: {guion.Hechas}. Capturas: {capturador.Entradas.Count}. Fallos: {guion.Fallos.Count}. Omitidas: {guion.Omitidas.Count}.");
            foreach (var f in guion.Fallos)
            {
                Console.WriteLine($"[capturas]   FALLO  {f}");
            }

            foreach (var o in guion.Omitidas)
            {
                Console.WriteLine($"[capturas]   OMITIDA {o}");
            }

            if (capturador.DpiDistinto != 0)
            {
                Console.WriteLine($"[capturas] ADVERTENCIA: hubo capturas con DPI {capturador.DpiDistinto} (distinto de 96). Revise la escala de Windows.");
            }

            CodigoSalida = guion.Fallos.Count == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "El guion de capturas terminó con error.");
            Console.WriteLine($"[capturas] ERROR FATAL: {ex}");
            CodigoSalida = 2;
        }
        finally
        {
            ExitThread();
        }
    }
}
#endif

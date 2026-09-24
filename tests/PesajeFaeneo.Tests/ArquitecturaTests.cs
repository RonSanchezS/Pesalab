using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ArquitecturaTests
{
    [Fact]
    public void Domain_NoReferenciaOtrosEnsamblados()
    {
        VerificarSinReferenciasProhibidas(
            typeof(Usuario).Assembly,
            "Domain",
            "Microsoft.EntityFrameworkCore",
            "System.Windows.Forms",
            "PesajeFaeneo.Application",
            "PesajeFaeneo.Infrastructure",
            "PesajeFaeneo.WinForms",
            "Serilog");
    }

    [Fact]
    public void Application_NoReferenciaEfCoreWinFormsInfrastructureNiSerilog()
    {
        VerificarSinReferenciasProhibidas(
            typeof(IDatabaseInitializer).Assembly,
            "Application",
            "Microsoft.EntityFrameworkCore",
            "System.Windows.Forms",
            "PesajeFaeneo.Infrastructure",
            "PesajeFaeneo.WinForms",
            "Serilog",
            "Microsoft.Extensions.Hosting");
    }

    [Fact]
    public void Infrastructure_NoReferenciaWinFormsNiSystemWindowsForms()
    {
        VerificarSinReferenciasProhibidas(
            typeof(PesajeDbContext).Assembly,
            "Infrastructure",
            "System.Windows.Forms",
            "PesajeFaeneo.WinForms");
    }

    private static void VerificarSinReferenciasProhibidas(
        System.Reflection.Assembly ensamblado,
        string nombreCapa,
        params string[] ensambladosProhibidos)
    {
        var referenciados = ensamblado.GetReferencedAssemblies();

        foreach (var refAssembly in referenciados)
        {
            foreach (var prohibido in ensambladosProhibidos)
            {
                Assert.False(
                    refAssembly.Name?.StartsWith(prohibido, StringComparison.OrdinalIgnoreCase) ?? false,
                    $"{nombreCapa} no debe referenciar el ensamblado: {refAssembly.Name}");
            }
        }
    }
}

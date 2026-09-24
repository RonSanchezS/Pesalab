using PesajeFaeneo.Domain.Entities;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ArquitecturaTests
{
    [Fact]
    public void Domain_NoReferenciaOtrosEnsamblados()
    {
        var domainAssembly = typeof(Usuario).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        var ensambladosProhibidos = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "System.Windows.Forms",
            "PesajeFaeneo.Application",
            "PesajeFaeneo.Infrastructure",
            "PesajeFaeneo.WinForms",
            "Serilog"
        };

        foreach (var refAssembly in referencedAssemblies)
        {
            foreach (var prohibido in ensambladosProhibidos)
            {
                Assert.False(
                    refAssembly.Name?.StartsWith(prohibido, StringComparison.OrdinalIgnoreCase) ?? false,
                    $"Domain no debe referenciar el ensamblado: {refAssembly.Name}");
            }
        }
    }
}

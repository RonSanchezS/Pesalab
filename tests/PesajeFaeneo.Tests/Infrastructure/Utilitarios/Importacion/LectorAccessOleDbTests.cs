using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Utilitarios.Importacion;
using Xunit;

namespace PesajeFaeneo.Tests.Infrastructure.Utilitarios.Importacion;

public class LectorAccessOleDbTests
{
    [Fact]
    public void ListarTablas_ProveedorInexistente_LanzaReglaNegocioExceptionSinMotor()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var rutaTemporal = Path.GetTempFileName();
        try
        {
            var lector = new LectorAccessOleDb(new[] { "Proveedor.Inexistente.1" });
            var ex = Assert.Throws<ReglaNegocioException>(() => lector.ListarTablas(rutaTemporal));
            Assert.Equal("IMP_ACCESS_SIN_MOTOR", ex.Codigo);
        }
        finally
        {
            File.Delete(rutaTemporal);
        }
    }

    [Fact]
    public void Leer_ProveedorInexistente_LanzaReglaNegocioExceptionSinMotor()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var rutaTemporal = Path.GetTempFileName();
        try
        {
            var lector = new LectorAccessOleDb(new[] { "Proveedor.Inexistente.1" });
            var ex = Assert.Throws<ReglaNegocioException>(() => lector.Leer(rutaTemporal, "Tabla1", "HUELLA", 100));
            Assert.Equal("IMP_ACCESS_SIN_MOTOR", ex.Codigo);
        }
        finally
        {
            File.Delete(rutaTemporal);
        }
    }
}

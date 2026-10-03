using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Ayuda;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmAcercaDeTests
{
    [Fact]
    public void FrmAcercaDe_TextoSoporte_ContieneVersionYCarpetaDatos()
    {
        HiloSta.Ejecutar(() =>
        {
            var rutaTemp = Path.Combine(Path.GetTempPath(), "PesajeFaeneo-Test-" + Guid.NewGuid().ToString("N"));
            var appPaths = new AppPaths(rutaTemp);
            using var form = new FrmAcercaDe(appPaths);

            Assert.Contains("Versión: " + InfoVersion.Version, form.TextoSoporte);
            Assert.Contains("Carpeta de datos: " + appPaths.BaseDir, form.TextoSoporte);
        });
    }

    [Fact]
    public void FrmAcercaDe_BtnCopiar_InvocaGanchoConTextoSoporte()
    {
        HiloSta.Ejecutar(() =>
        {
            var rutaTemp = Path.Combine(Path.GetTempPath(), "PesajeFaeneo-Test-" + Guid.NewGuid().ToString("N"));
            var appPaths = new AppPaths(rutaTemp);
            using var form = new FrmAcercaDe(appPaths);
            form.Show();

            string? textoCopiado = null;
            var llamadas = 0;
            form.CopiarAlPortapapelesParaTest = t =>
            {
                llamadas++;
                textoCopiado = t;
            };

            form.BtnCopiar.PerformClick();

            Assert.Equal(1, llamadas);
            Assert.Equal(form.TextoSoporte, textoCopiado);
        });
    }
}

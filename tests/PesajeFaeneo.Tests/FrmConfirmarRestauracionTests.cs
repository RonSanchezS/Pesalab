using System.Drawing;
using System.Windows.Forms;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmConfirmarRestauracionTests
{
    private static ValidacionRestauracion CrearValidacion(IReadOnlyList<string>? advertencias = null)
    {
        var backup = new InfoBackup(
            @"C:\FakeBackups\manuales\fake.zip",
            "fake.zip",
            TipoBackup.Manual,
            new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
            1572864,
            "1.0.0",
            "EQUIPO-TEST",
            "migracion_fake",
            150,
            150L,
            Valido: true,
            Problema: null);

        return new ValidacionRestauracion(
            "token-val",
            backup,
            Ganaderos: 12,
            Clientes: 25,
            UsuariosActivos: 3,
            AdministradoresActivos: 1,
            RequiereActualizarFormato: false,
            Advertencias: advertencias ?? new[] { "Advertencia 1 de prueba", "Advertencia 2 de prueba" });
    }

    [Fact]
    public void ConfirmarRestauracion_ValidacionBotonYCampos()
    {
        HiloSta.Ejecutar(() =>
        {
            var zona = TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");
            var val = CrearValidacion();
            using var form = new FrmConfirmarRestauracion(val, zona);

            // Botón deshabilitado al abrir
            Assert.False(form.btnRestaurar.Enabled);

            // "restaurar" en minúsculas NO habilita
            form.txtConfirmacion.Text = "restaurar";
            Assert.False(form.btnRestaurar.Enabled);

            // "  RESTAURAR  " con espacios SÍ habilita
            form.txtConfirmacion.Text = "  RESTAURAR  ";
            Assert.True(form.btnRestaurar.Enabled);
            Assert.Equal("  RESTAURAR  ", form.Confirmacion);

            // Advertencias como etiquetas
            Assert.Equal(2, form.pnlAdvertencias.Controls.Count);
            Assert.Contains("Advertencia 1 de prueba", form.pnlAdvertencias.Controls[0].Text);

            // Conteos en tblDetalles
            var valores = form.tblDetalles.Controls.OfType<Label>().Select(l => l.Text).ToList();
            Assert.Contains("150", valores); // boletas
            Assert.Contains("12", valores);  // ganaderos
            Assert.Contains("25", valores);  // clientes
            Assert.Contains("3", valores);   // usuarios
            Assert.Contains("1", valores);   // administradores
        });
    }
}

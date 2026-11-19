using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using PesajeFaeneo.WinForms.Formularios.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F10 (O8): pantalla de reportes con el tipo "Pesajes por raza".</summary>
public class FrmReportesPorRazaTests
{
    private static FakeTimeProvider CrearTimeProvider()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 2, 0, 0, TimeSpan.Zero));
        tp.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));
        return tp;
    }

    private static FrmReportes CrearForm(ServicioReportesFalso servicio) =>
        new(servicio, new GeneradorPdfReportesFalso(), new GeneradorExcelReportesFalso(), CrearTimeProvider());

    private static OpcionesReportes OpcionesConRazas(params string[] razas) =>
        new(Array.Empty<ElementoBusqueda>(), Array.Empty<ElementoBusqueda>(), razas);

    [Fact]
    public void ElComboDeRazas_SoloEstaHabilitadoEnPorRaza_YTipoPorRazaEstaEnElCombo()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearForm(new ServicioReportesFalso());

            Assert.Contains(form.cmbTipo.Items.Cast<object>(), o => o.ToString()!.Contains("Pesajes por raza") || o.GetType().GetProperty("Texto")?.GetValue(o) as string == "Pesajes por raza");

            foreach (var tipo in new[] { TipoReporte.PesajesDelDia, TipoReporte.PesajesEntreFechas, TipoReporte.PorGanadero, TipoReporte.PorCliente, TipoReporte.Anuladas })
            {
                form.Configurar(tipo);
                Assert.False(form.cmbRaza.Enabled, $"cmbRaza no debe estar habilitado en {tipo}");
            }

            form.Configurar(TipoReporte.PorRaza);
            Assert.True(form.cmbRaza.Enabled);
            Assert.True(form.dtpHasta.Enabled);
            Assert.False(form.ctlGanadero.Enabled);
            Assert.False(form.ctlCliente.Enabled);
        });
    }

    [Fact]
    public void AlCargar_LlenaElComboConLasRazasDelServicio_SinSeleccionarNinguna()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso { Opciones = OpcionesConRazas("ANGUS NEGRO", "NELORE") };
            using var form = CrearForm(servicio);

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new[] { "ANGUS NEGRO", "NELORE" }, form.cmbRaza.Items.Cast<string>());
            Assert.Equal(-1, form.cmbRaza.SelectedIndex);
        });
    }

    [Fact]
    public void Generar_PorRaza_EnviaLaRazaElegida_YLosDemasTiposNo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso { Opciones = OpcionesConRazas("ANGUS NEGRO", "NELORE") };
            using var form = CrearForm(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.cmbRaza.SelectedItem = "NELORE";

            form.Configurar(TipoReporte.PorRaza);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            var filtroRaza = servicio.FiltrosRecibidos.Last();
            Assert.Equal(TipoReporte.PorRaza, filtroRaza.Tipo);
            Assert.Equal("NELORE", filtroRaza.ColorRaza);

            form.Configurar(TipoReporte.PesajesEntreFechas);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            Assert.Null(servicio.FiltrosRecibidos.Last().ColorRaza);
        });
    }

    [Fact]
    public void Generar_PorRazaSinElegirRaza_MuestraElErrorEnElCombo()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso
            {
                Excepcion = new ValidacionException(CamposReportes.Raza, "Seleccione la raza.")
            };
            using var form = CrearForm(servicio);
            form.Configurar(TipoReporte.PorRaza);

            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Seleccione la raza.", form.ErrorDe(form.cmbRaza));
            Assert.Null(form.ReporteActual);
        });
    }

    [Fact]
    public void CambiarLaRaza_InvalidaElReporteEnPantalla()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso { Opciones = OpcionesConRazas("ANGUS NEGRO", "NELORE") };
            using var form = CrearForm(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.cmbRaza.SelectedItem = "NELORE";
            form.Configurar(TipoReporte.PorRaza);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();
            Assert.NotNull(form.ReporteActual);

            form.cmbRaza.SelectedItem = "ANGUS NEGRO"; // D15: cualquier cambio de filtro invalida

            Assert.Null(form.ReporteActual);
            Assert.Empty(form.dgvVista.Rows);
            Assert.Contains("Los filtros cambiaron", form.lblAviso.Text);
        });
    }

    [Fact]
    public void RecargarLasOpciones_ConservaLaRazaElegidaSiSigueExistiendo_SinInvalidar()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso { Opciones = OpcionesConRazas("ANGUS NEGRO", "NELORE") };
            using var form = CrearForm(servicio);
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.cmbRaza.SelectedItem = "NELORE";
            form.Configurar(TipoReporte.PorRaza);
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("NELORE", form.cmbRaza.SelectedItem);
            Assert.NotNull(form.ReporteActual); // recargar la lista no es un cambio de filtro
        });
    }

    [Fact]
    public void Layout_ConElComboDeRazas_SinRecortesA96DpiYConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new ServicioReportesFalso { Opciones = OpcionesConRazas("ANGUS NEGRO", "NELORE") };
            using var form = CrearForm(servicio);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.Configurar(TipoReporte.PorRaza);
            System.Windows.Forms.Application.DoEvents();

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }
}

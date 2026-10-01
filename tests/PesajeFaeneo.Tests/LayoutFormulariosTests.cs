using System.Windows.Forms;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria.Dtos;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.Tests.Utilidades.Gestion;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>
/// Defectos de la prueba manual de F6 (docs/plans/F6-remediacion-prueba-manual.md): textos cortados y
/// controles recortados en los formularios de gestión de boletas, a 96 DPI y con escalado 1,25.
/// </summary>
public class LayoutFormulariosTests
{
    private static FakeTimeProvider Reloj()
    {
        var tiempo = new FakeTimeProvider(new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("BO_Layout", TimeSpan.FromHours(-4), "BO", "BO"));
        return tiempo;
    }

    private static FrmBuscarBoletas CrearBuscador()
    {
        var falso = new ServicioGestionPesajesFalso();
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "sup", "Supervisor Test", Rol.Supervisor, false));
        return new FrmBuscarBoletas(falso, new FormFactoryFalsa(), sesion, Reloj());
    }

    private static FrmVisorAuditoria CrearVisor() => new(new ServicioAuditoriaFalso(), Reloj());

    private static FrmReimprimirBoleta CrearReimpresion(out ServicioBoletasFalso boletas)
    {
        boletas = new ServicioBoletasFalso();
        var parametros = new ServicioParametrosFalso();
        parametros.Parametros = parametros.Parametros with { UltimoNumeroBoletaEmitido = 120 };
        boletas.DatosRetorno = DatosBoletaDePrueba.Crear(
            numeroBoleta: 120,
            pesajeId: 15,
            nombreGanadero: new string('G', 100),
            nombreCliente: new string('C', 100),
            origen: OrigenPeso.Simulador,
            estado: EstadoPesaje.Anulado,
            motivoAnulacion: "ERROR DE PRUEBA EN GANADERO, SE REGISTRÓ CON LA MARCA EQUIVOCADA Y DEBE VOLVER A PESARSE HOY");
        return new FrmReimprimirBoleta(boletas, new ServicioImpresionBoletasFalso(), parametros);
    }

    [Fact]
    public void BuscarBoletas_FiltrosSinTextosCortados_NiConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearBuscador();
            form.Show();
            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    [Fact]
    public void VisorAuditoria_FiltrosSinTextosCortados_NiConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearVisor();
            form.Show();
            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    [Fact]
    public void ReimprimirBoleta_ConTextosLargos_NoCortaNiSaleDelGrupo()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearReimpresion(out _);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.StartsWith("ANULADA:", form.lblEstado.Text);
            Assert.Equal("Aún no se imprimió", form.lblImpresiones.Text);
            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    [Fact]
    public void Filtros_FechasEnFormatoDiaMesAnio_IndependienteDeLaRegion()
    {
        HiloSta.Ejecutar(() =>
        {
            using var buscador = CrearBuscador();
            using var visor = CrearVisor();

            foreach (var dtp in new[] { buscador.dtpDesde, buscador.dtpHasta, visor.dtpDesde, visor.dtpHasta })
            {
                Assert.Equal(DateTimePickerFormat.Custom, dtp.Format);
                Assert.Equal(DisenoFiltros.FormatoFecha, dtp.CustomFormat);
            }

            Assert.Equal("dd/MM/yyyy", DisenoFiltros.FormatoFecha);
        });
    }

    private static PesajeFaeneo.WinForms.Formularios.Reportes.FrmReportes CrearReportes(PesajeFaeneo.Tests.Utilidades.Reportes.ServicioReportesFalso? servicio = null)
    {
        var s = servicio ?? new PesajeFaeneo.Tests.Utilidades.Reportes.ServicioReportesFalso();
        var p = new PesajeFaeneo.Tests.Utilidades.Reportes.GeneradorPdfReportesFalso();
        var e = new PesajeFaeneo.Tests.Utilidades.Reportes.GeneradorExcelReportesFalso();
        return new PesajeFaeneo.WinForms.Formularios.Reportes.FrmReportes(s, p, e, Reloj());
    }

    [Fact]
    public void Reportes_AntesDeGenerar_SinTextosCortados_NiConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearReportes();
            form.Show();
            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    [Fact]
    public void Reportes_DespuesDeGenerar_SinTextosCortados_NiConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new PesajeFaeneo.Tests.Utilidades.Reportes.ServicioReportesFalso
            {
                Resultado = PesajeFaeneo.Tests.Utilidades.Reportes.DatosReporteDePrueba.Crear(PesajeFaeneo.Application.Reportes.Dtos.TipoReporte.PesajesEntreFechas, 5)
            };
            using var form = CrearReportes(servicio);
            form.Show();
            form.GenerarParaTestAsync().GetAwaiter().GetResult();

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }
}

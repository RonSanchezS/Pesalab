using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FormFactoryTests
{
    [Fact]
    public void Crear_DosLlamadas_DevuelveInstanciasDistintas()
    {
        HiloSta.Ejecutar(() =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<IFormFactory, FormFactory>();
            using var proveedor = services.BuildServiceProvider();
            var fabrica = proveedor.GetRequiredService<IFormFactory>();

            using var form1 = fabrica.Crear<FormularioSimple>();
            using var form2 = fabrica.Crear<FormularioSimple>();

            Assert.NotSame(form1, form2);
        });
    }

    [Fact]
    public void Crear_AlDesecharElFormulario_DesechaLosServiciosScopedDeSuPropioScope()
    {
        HiloSta.Ejecutar(() =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<IFormFactory, FormFactory>();
            services.AddScoped<ServicioDescartablePrueba>();
            using var proveedor = services.BuildServiceProvider();
            var fabrica = proveedor.GetRequiredService<IFormFactory>();

            var formulario = fabrica.Crear<FormularioPrueba>();
            var servicioInyectado = formulario.Servicio;

            Assert.False(servicioInyectado.FueDescartado);

            formulario.Dispose();

            Assert.True(servicioInyectado.FueDescartado);
        });
    }

    private class FormularioSimple : Form { }

    private class ServicioDescartablePrueba : IDisposable
    {
        public bool FueDescartado { get; private set; }

        public void Dispose() => FueDescartado = true;
    }

    private class FormularioPrueba : Form
    {
        public ServicioDescartablePrueba Servicio { get; }

        public FormularioPrueba(ServicioDescartablePrueba servicio)
        {
            Servicio = servicio;
        }
    }
}

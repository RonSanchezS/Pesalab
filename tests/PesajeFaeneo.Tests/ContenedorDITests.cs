using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesajeFaeneo.Application;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Infrastructure;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.WinForms;
using PesajeFaeneo.WinForms.Formularios;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ContenedorDITests
{
    [Fact]
    public void ContenedorDI_SeConstruyeSinErrores_YResuelveServicios()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddWinForms();

        var host = builder.Build();

        Assert.NotNull(host.Services.GetService<IDbContextFactory<PesajeDbContext>>());
        Assert.NotNull(host.Services.GetService<IDatabaseInitializer>());
        Assert.NotNull(host.Services.GetService<IDataSeeder>());
        Assert.NotNull(host.Services.GetService<IPasswordHasher>());
        Assert.NotNull(host.Services.GetService<IFormFactory>());
        Assert.NotNull(host.Services.GetService<TimeProvider>());

        var formFactory = host.Services.GetRequiredService<IFormFactory>();
        var frmPrincipal = formFactory.Crear<FrmPrincipal>();
        Assert.NotNull(frmPrincipal);
    }
}

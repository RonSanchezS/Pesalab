#pragma warning disable EF1001 // APIs internas de EF Core usadas deliberadamente para comparar modelo vs. snapshot.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>
/// Si alguien cambia el modelo (una entidad, una Configuration, ConfigureConventions) y olvida
/// regenerar la migración, este test debe fallar: compara el modelo en tiempo de ejecución contra el
/// snapshot que quedó grabado en <c>PesajeDbContextModelSnapshot</c>, igual que hace
/// <c>dotnet ef migrations has-pending-model-changes</c> internamente.
/// </summary>
public class ModeloVsMigracionTests
{
    [Fact]
    public void Modelo_NoTieneCambiosPendientesRespectoALaMigracionInicial()
    {
        using var context = new PesajeDbContext(new DbContextOptionsBuilder<PesajeDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var differ = context.GetService<IMigrationsModelDiffer>();
        var runtimeInitializer = context.GetService<IModelRuntimeInitializer>();

        var modeloSnapshot = migrationsAssembly.ModelSnapshot?.Model;
        Assert.NotNull(modeloSnapshot);

        var modeloSnapshotFinalizado = runtimeInitializer.Initialize(modeloSnapshot!, designTime: true);
        var modeloActual = context.GetService<IDesignTimeModel>().Model;

        var tieneDiferencias = differ.HasDifferences(
            modeloSnapshotFinalizado.GetRelationalModel(),
            modeloActual.GetRelationalModel());

        Assert.False(tieneDiferencias);
    }
}

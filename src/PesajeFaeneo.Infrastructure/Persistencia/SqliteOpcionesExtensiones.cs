using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PesajeFaeneo.Infrastructure.Persistencia;

/// <summary>
/// Punto único de configuración de <see cref="DbContextOptionsBuilder"/> para SQLite, reutilizado
/// por el registro en DI, la fábrica de diseño de EF Core y los fixtures de prueba, de modo que
/// producción y pruebas compartan exactamente el mismo comportamiento (pragmas, convenciones UTC).
/// </summary>
public static class SqliteOpcionesExtensiones
{
    private static readonly SqlitePragmasInterceptor PragmasInterceptor = new();

    // Definido sobre el DbContextOptionsBuilder no genérico para que sirva tanto a AddDbContextFactory
    // (que expone un Action<DbContextOptionsBuilder> no genérico) como a los usos con
    // DbContextOptionsBuilder<PesajeDbContext> (DesignTimeDbContextFactory, fixtures de prueba).
    public static DbContextOptionsBuilder ConfigurarSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        optionsBuilder.UseSqlite(connectionString);
        optionsBuilder.AddInterceptors(PragmasInterceptor);
        return optionsBuilder;
    }

    /// <summary>
    /// Variante para conexiones ya abiertas (por ejemplo la conexión SQLite en memoria usada por
    /// <c>SqliteDbFixture</c> en las pruebas), que también recibe el interceptor de pragmas.
    /// </summary>
    public static DbContextOptionsBuilder ConfigurarSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection)
    {
        optionsBuilder.UseSqlite(connection);
        optionsBuilder.AddInterceptors(PragmasInterceptor);
        return optionsBuilder;
    }
}

using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public class DataSeeder : IDataSeeder
{
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public DataSeeder(
        IDbContextFactory<PesajeDbContext> contextFactory,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task SembrarAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var hayUsuarios = await context.Usuarios.AnyAsync(cancellationToken);
        if (!hayUsuarios)
        {
            var (hash, salt) = _passwordHasher.Hashear("admin");
            var admin = new Usuario
            {
                NombreUsuario = "admin",
                NombreCompleto = "Administrador del Sistema",
                Hash = hash,
                Salt = salt,
                Rol = Rol.Administrador,
                Activo = true,
                DebeCambiarPassword = true,
                CreadoEn = _timeProvider.GetUtcNow().UtcDateTime
            };

            await context.Usuarios.AddAsync(admin, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

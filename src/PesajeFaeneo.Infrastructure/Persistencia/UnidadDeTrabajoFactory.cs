using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public sealed class UnidadDeTrabajoFactory : IUnidadDeTrabajoFactory
{
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;

    public UnidadDeTrabajoFactory(IDbContextFactory<PesajeDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IUnidadDeTrabajo> CrearAsync(CancellationToken cancellationToken = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return new UnidadDeTrabajo(context);
    }
}

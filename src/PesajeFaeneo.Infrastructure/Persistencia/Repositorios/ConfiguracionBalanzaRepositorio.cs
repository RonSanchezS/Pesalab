using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Repositorios;

public class ConfiguracionBalanzaRepositorio : IConfiguracionBalanzaRepositorio
{
    private readonly PesajeDbContext _context;

    public ConfiguracionBalanzaRepositorio(PesajeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ConfiguracionBalanza> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var config = await _context.ConfiguracionesBalanza.FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("No existe la fila de configuración de balanza (Id = 1).");
        }

        return config;
    }
}

using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Gestion;

public class ServicioGestionPesajesFalso : IServicioGestionPesajes
{
    public List<FiltroBoletas> FiltrosRecibidos { get; } = new();
    public List<EditarPesaje> EdicionesRecibidas { get; } = new();
    public List<AnularPesaje> AnulacionesRecibidas { get; } = new();
    public List<int> ObtenerEdicionRecibidos { get; } = new();

    public ResultadoBusquedaBoletas ResultadoBusqueda { get; set; } = new(Array.Empty<BoletaEncontrada>(), false);
    public OpcionesBusquedaBoletas Opciones { get; set; } = new(Array.Empty<ElementoBusqueda>(), Array.Empty<ElementoBusqueda>());
    public BoletaParaEdicion? BoletaParaEdicion { get; set; }
    public ResultadoEdicion ResultadoEditar { get; set; } = new(false, Array.Empty<string>());

    public Exception? ExcepcionBuscar { get; set; }
    public Exception? ExcepcionObtenerEdicion { get; set; }
    public Exception? ExcepcionEditar { get; set; }
    public Exception? ExcepcionAnular { get; set; }

    public Func<Task>? PausaBuscarAsync { get; set; }
    public Func<Task>? PausaEditarAsync { get; set; }
    public Func<Task>? PausaAnularAsync { get; set; }

    public async Task<ResultadoBusquedaBoletas> BuscarAsync(FiltroBoletas filtro, CancellationToken cancellationToken = default)
    {
        FiltrosRecibidos.Add(filtro);
        if (PausaBuscarAsync != null) await PausaBuscarAsync();
        if (ExcepcionBuscar != null) throw ExcepcionBuscar;
        return ResultadoBusqueda;
    }

    public Task<OpcionesBusquedaBoletas> ObtenerOpcionesBusquedaAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Opciones);
    }

    public async Task<BoletaParaEdicion> ObtenerParaEdicionAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        ObtenerEdicionRecibidos.Add(pesajeId);
        if (ExcepcionObtenerEdicion != null) throw ExcepcionObtenerEdicion;
        return BoletaParaEdicion ?? throw new InvalidOperationException("BoletaParaEdicion no configurada en el falso.");
    }

    public async Task<ResultadoEdicion> EditarAsync(EditarPesaje dto, CancellationToken cancellationToken = default)
    {
        EdicionesRecibidas.Add(dto);
        if (PausaEditarAsync != null) await PausaEditarAsync();
        if (ExcepcionEditar != null) throw ExcepcionEditar;
        return ResultadoEditar;
    }

    public async Task AnularAsync(AnularPesaje dto, CancellationToken cancellationToken = default)
    {
        AnulacionesRecibidas.Add(dto);
        if (PausaAnularAsync != null) await PausaAnularAsync();
        if (ExcepcionAnular != null) throw ExcepcionAnular;
    }
}

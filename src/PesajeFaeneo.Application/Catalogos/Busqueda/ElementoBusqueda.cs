namespace PesajeFaeneo.Application.Catalogos.Busqueda;

public sealed record ElementoBusqueda
{
    public int Id { get; }
    public string Codigo { get; }
    public string Descripcion { get; }
    public string? Detalle { get; }
    public bool Activo { get; }
    internal string CodigoPlegado { get; }
    internal string DescripcionPlegada { get; }

    public ElementoBusqueda(int id, string codigo, string descripcion, string? detalle = null, bool activo = true)
    {
        Id = id;
        Codigo = codigo ?? string.Empty;
        Descripcion = descripcion ?? string.Empty;
        Detalle = detalle;
        Activo = activo;
        CodigoPlegado = TextoBusqueda.Plegar(Codigo);
        DescripcionPlegada = TextoBusqueda.Plegar(Descripcion);
    }

    public string TextoMostrado => $"{Codigo} — {Descripcion}";
}

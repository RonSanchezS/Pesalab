namespace PesajeFaeneo.Application.Catalogos.Busqueda;

public static class MotorBusqueda
{
    public const int MaximoPorDefecto = 20;

    public static IReadOnlyList<ElementoBusqueda> Buscar(
        IReadOnlyList<ElementoBusqueda> elementos,
        string? texto,
        int maximo = MaximoPorDefecto)
    {
        var t = TextoBusqueda.Plegar(texto);
        if (string.IsNullOrEmpty(t) || elementos == null || elementos.Count == 0)
        {
            return Array.Empty<ElementoBusqueda>();
        }

        var palabrasT = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var coincidencias = new List<(ElementoBusqueda Elemento, int Rango)>();

        foreach (var e in elementos)
        {
            var rango = ObtenerRango(e, t, palabrasT);
            if (rango.HasValue)
            {
                coincidencias.Add((e, rango.Value));
            }
        }

        return coincidencias
            .OrderBy(c => c.Rango)
            .ThenBy(c => c.Elemento.DescripcionPlegada, StringComparer.Ordinal)
            .ThenBy(c => c.Elemento.CodigoPlegado, StringComparer.Ordinal)
            .Select(c => c.Elemento)
            .Take(maximo)
            .ToList()
            .AsReadOnly();
    }

    public static ElementoBusqueda? ResolverExacto(IReadOnlyList<ElementoBusqueda> elementos, string? texto)
    {
        var t = TextoBusqueda.Plegar(texto);
        if (string.IsNullOrEmpty(t) || elementos == null || elementos.Count == 0)
        {
            return null;
        }

        var exactos = elementos.Where(e => e.CodigoPlegado == t).Take(2).ToList();
        if (exactos.Count == 1)
        {
            return exactos[0];
        }

        if (exactos.Count > 1)
        {
            return null;
        }

        var resultados = Buscar(elementos, texto, maximo: 2);
        return resultados.Count == 1 ? resultados[0] : null;
    }

    private static int? ObtenerRango(ElementoBusqueda e, string t, string[] palabrasT)
    {
        // 0: CodigoPlegado == t
        if (string.Equals(e.CodigoPlegado, t, StringComparison.Ordinal))
        {
            return 0;
        }

        // 1: CodigoPlegado.StartsWith(t, Ordinal)
        if (e.CodigoPlegado.StartsWith(t, StringComparison.Ordinal))
        {
            return 1;
        }

        // 2: DescripcionPlegada.StartsWith(t, Ordinal)
        if (e.DescripcionPlegada.StartsWith(t, StringComparison.Ordinal))
        {
            return 2;
        }

        // 3: todas las palabras de t (separadas por espacio) son prefijo de alguna palabra de DescripcionPlegada
        if (palabrasT.Length > 0)
        {
            var palabrasDesc = e.DescripcionPlegada.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var todasPrefijo = palabrasT.All(pt => palabrasDesc.Any(pd => pd.StartsWith(pt, StringComparison.Ordinal)));
            if (todasPrefijo)
            {
                return 3;
            }
        }

        // 4: DescripcionPlegada.Contains(t, Ordinal)
        if (e.DescripcionPlegada.Contains(t, StringComparison.Ordinal))
        {
            return 4;
        }

        return null;
    }
}

namespace PesajeFaeneo.Domain.Excepciones;

public class DomainException : Exception
{
    public DomainException(string mensaje) : base(mensaje) { }
    public DomainException(string mensaje, Exception inner) : base(mensaje, inner) { }
}

public record ErrorValidacion(string Campo, string Mensaje);

public class ValidacionException : DomainException
{
    public IReadOnlyList<ErrorValidacion> Errores { get; }

    public ValidacionException(IEnumerable<ErrorValidacion> errores)
        : base(FormatearMensaje(errores))
    {
        Errores = errores.ToList().AsReadOnly();
    }

    public ValidacionException(string campo, string mensaje)
        : this([new ErrorValidacion(campo, mensaje)])
    {
    }

    private static string FormatearMensaje(IEnumerable<ErrorValidacion> errores)
    {
        var lista = errores.Select(e => $"{e.Campo}: {e.Mensaje}");
        return $"Errores de validación: {string.Join("; ", lista)}";
    }
}

public class ReglaNegocioException : DomainException
{
    public string Codigo { get; }

    public ReglaNegocioException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

public class EntidadNoEncontradaException : DomainException
{
    public string Entidad { get; }
    public object Id { get; }

    public EntidadNoEncontradaException(string entidad, object id)
        : base($"No se encontró la entidad '{entidad}' con identificador '{id}'.")
    {
        Entidad = entidad;
        Id = id;
    }
}

public class ConflictoException : DomainException
{
    public ConflictoException(string mensaje) : base(mensaje) { }
}

public class ConcurrenciaException : DomainException
{
    public ConcurrenciaException(string mensaje) : base(mensaje) { }
}

public class NoAutorizadoException : DomainException
{
    public NoAutorizadoException(string mensaje = "No tiene permisos para esta acción.") : base(mensaje) { }
}

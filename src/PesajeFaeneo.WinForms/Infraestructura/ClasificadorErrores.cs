using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.WinForms.Infraestructura;

public enum TipoErrorClasificado
{
    Validacion,
    Negocio,
    NoAutorizado,
    Inesperado
}

public record ResultadoClasificacionError(
    TipoErrorClasificado Tipo,
    string Titulo,
    string Mensaje,
    string? CodigoIncidente,
    bool DebeRegistrarLog);

public static class ClasificadorErrores
{
    public static ResultadoClasificacionError Clasificar(Exception ex)
    {
        // Si viene envuelto en TargetInvocationException o AggregateException, extraer la causa raíz
        while (ex is System.Reflection.TargetInvocationException or AggregateException && ex.InnerException != null)
        {
            ex = ex.InnerException;
        }

        return ex switch
        {
            ValidacionException valEx => new ResultadoClasificacionError(
                TipoErrorClasificado.Validacion,
                "Validación de datos",
                valEx.Message,
                null,
                DebeRegistrarLog: false),

            ConflictoException or EntidadNoEncontradaException or ReglaNegocioException => new ResultadoClasificacionError(
                TipoErrorClasificado.Negocio,
                "Aviso del sistema",
                ex.Message,
                null,
                DebeRegistrarLog: false),

            NoAutorizadoException noAuthEx => new ResultadoClasificacionError(
                TipoErrorClasificado.NoAutorizado,
                "Permisos insuficientes",
                string.IsNullOrWhiteSpace(noAuthEx.Message) ? "No tiene permisos para esta acción." : noAuthEx.Message,
                null,
                DebeRegistrarLog: false),

            _ => CrearErrorInesperado()
        };
    }

    private static ResultadoClasificacionError CrearErrorInesperado()
    {
        var codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return new ResultadoClasificacionError(
            TipoErrorClasificado.Inesperado,
            "Error inesperado",
            $"Ocurrió un error inesperado. Código: {codigo}",
            codigo,
            DebeRegistrarLog: true);
    }
}

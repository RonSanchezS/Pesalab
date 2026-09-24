using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.WinForms.Infraestructura;

public enum TipoErrorClasificado
{
    Validacion,
    Negocio,
    NoAutorizado,
    Cancelado,
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
        // Si viene envuelto en TargetInvocationException, extraer la causa raíz.
        while (ex is System.Reflection.TargetInvocationException && ex.InnerException != null)
        {
            ex = ex.InnerException;
        }

        // Un AggregateException solo se desenvuelve cuando trae una única excepción interna: con más
        // de una no hay una causa "raíz" inequívoca, y clasificar solo la primera ocultaría las demás.
        if (ex is AggregateException agregada)
        {
            var planas = agregada.Flatten();
            if (planas.InnerExceptions.Count == 1)
            {
                ex = planas.InnerExceptions[0];
            }
        }

        return ex switch
        {
            ValidacionException valEx => new ResultadoClasificacionError(
                TipoErrorClasificado.Validacion,
                "Validación de datos",
                valEx.Message,
                null,
                DebeRegistrarLog: false),

            ConcurrenciaException => new ResultadoClasificacionError(
                TipoErrorClasificado.Negocio,
                "Aviso del sistema",
                "El registro fue modificado por otro usuario. Vuelva a cargarlo.",
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

            OperationCanceledException => new ResultadoClasificacionError(
                TipoErrorClasificado.Cancelado,
                string.Empty,
                string.Empty,
                null,
                DebeRegistrarLog: false),

            // Cualquier otra DomainException no cubierta arriba: es un error de negocio conocido, no
            // uno inesperado del sistema (antes caía en "Error inesperado" con log y código de incidente).
            DomainException dominioEx => new ResultadoClasificacionError(
                TipoErrorClasificado.Negocio,
                "Aviso del sistema",
                dominioEx.Message,
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

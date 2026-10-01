using Microsoft.Extensions.Logging;

namespace PesajeFaeneo.Tests.Utilidades.Reportes;

internal sealed class LoggerEspia<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensaje, Exception? Excepcion)> Mensajes { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Mensajes.Add((logLevel, formatter(state, exception), exception));
    }
}

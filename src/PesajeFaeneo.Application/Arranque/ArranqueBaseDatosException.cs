namespace PesajeFaeneo.Application.Arranque;

using PesajeFaeneo.Application.Arranque.Dtos;

/// <summary>La base de datos no se puede abrir con esta versión: Program muestra Message y termina con código 2.</summary>
public sealed class ArranqueBaseDatosException : Exception
{
    public MotivoFalloArranque Motivo { get; }

    public ArranqueBaseDatosException(MotivoFalloArranque motivo, string mensaje, Exception? interna = null)
        : base(mensaje, interna)
    {
        Motivo = motivo;
    }
}

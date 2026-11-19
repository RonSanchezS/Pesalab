using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Parametros.Dtos;

public sealed record ParametrosDto(
    string NombreEmpresa,
    string Direccion,
    string Telefono,
    string Ciudad,
    string RegistroSanitario,
    string TelefonosReclamos,
    string ObservacionPorDefecto,
    string? LogoArchivo,
    string? RutaLogoCompleta,
    long SiguienteNumeroBoleta,
    long? UltimoNumeroBoletaEmitido,
    DateTime ModificadoEn)
{
    /// <summary>
    /// D1 (F10): el número de boleta solo puede fijarse a mano mientras no exista ningún pesaje (incluidos los anulados);
    /// después lo asigna el sistema (grabado e importación).
    /// </summary>
    public bool NumeracionEditable => UltimoNumeroBoletaEmitido is null;
}

public enum AccionLogo
{
    SinCambio,
    Reemplazar,
    Quitar
}

public sealed record ActualizarParametros(
    string NombreEmpresa,
    string? Direccion,
    string? Telefono,
    string? Ciudad,
    string? RegistroSanitario,
    string? TelefonosReclamos,
    string? ObservacionPorDefecto,
    long SiguienteNumeroBoleta,
    AccionLogo AccionLogo = AccionLogo.SinCambio,
    string? RutaLogoOrigen = null);

/// <summary>Nombre y logo de la empresa (datos no sensibles) para mostrarlos antes de iniciar sesión (F10, O7).</summary>
public sealed record IdentidadEmpresa(string NombreEmpresa, string? RutaLogoCompleta);

/// <summary>
/// F11: tipo de pesaje guardado (null = sin definir) y boletas vigentes de cada tipo, para avisar qué boletas existentes
/// seguirán mostrando el desposte con el modo elegido.
/// </summary>
public sealed record ConfiguracionDesposte(ModoDesposte? Modo, int BoletasResCompleta, int BoletasMediaRes);

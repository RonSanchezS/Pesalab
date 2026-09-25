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
    DateTime ModificadoEn);

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

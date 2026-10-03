using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.PuestaEnMarcha;

public static class CamposPuestaEnMarcha
{
    public const string Empresa = "Empresa";
    public const string Balanza = "Balanza";
}

public static class ReglasPuestaEnMarcha
{
    /// <summary>Valor sembrado en ParametrosSistema (ParametrosSistemaConfiguration.HasData). Un test los mantiene iguales.</summary>
    public const string NombreEmpresaPorDefecto = "NOMBRE DE EMPRESA";

    public static bool EmpresaConfigurada(string? nombreEmpresa) =>
        !string.IsNullOrWhiteSpace(nombreEmpresa) &&
        !string.Equals(nombreEmpresa.Trim(), NombreEmpresaPorDefecto, StringComparison.OrdinalIgnoreCase);

    public static bool PuedeFinalizar(EstadoPuestaEnMarcha estado, bool confirmaSimulador)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return estado.EmpresaConfigurada &&
               (estado.TipoConexionBalanza != TipoConexion.Simulador || confirmaSimulador);
    }
}

using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.PuestaEnMarcha.Dtos;

public sealed record EstadoPuestaEnMarcha(
    bool Completa,
    bool EmpresaConfigurada,
    string NombreEmpresa,
    TipoConexion TipoConexionBalanza,
    bool ImpresionConfigurada,
    int FleterosActivos,
    int UsuariosNoAdministradoresActivos,
    ModoDesposte? ModoDesposte);           // F11 (D3): null = tipo de pesaje sin definir (paso obligatorio pendiente)

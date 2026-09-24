using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Seguridad.Dtos;

public sealed record UsuarioListado(
    int Id,
    string NombreUsuario,
    string NombreCompleto,
    Rol Rol,
    bool Activo,
    bool Bloqueado,
    DateTime? UltimoAcceso);

public sealed record CrearUsuario(
    string NombreUsuario,
    string NombreCompleto,
    Rol Rol,
    string Password,
    string Confirmacion);

public sealed record EditarUsuario(
    int Id,
    string NombreCompleto,
    Rol Rol);

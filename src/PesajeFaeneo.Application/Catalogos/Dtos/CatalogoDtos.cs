namespace PesajeFaeneo.Application.Catalogos.Dtos;

// F10 (O6): en las altas y ediciones no viaja el código: lo asigna el sistema al crear y no se puede cambiar.
public sealed record GanaderoListado(int Id, string Codigo, string Nombre, bool Activo);
public sealed record CrearGanadero(string Nombre);
public sealed record EditarGanadero(int Id, string Nombre);

public sealed record ClienteListado(int Id, int Codigo, string Nombre, string Direccion, bool Activo);
public sealed record CrearCliente(string Nombre, string? Direccion);
public sealed record EditarCliente(int Id, string Nombre, string? Direccion);

public sealed record FleteroListado(int Id, int Codigo, string Nombre, string Celular, bool Activo);
public sealed record CrearFletero(string Nombre, string Celular);
public sealed record EditarFletero(int Id, string Nombre, string Celular);

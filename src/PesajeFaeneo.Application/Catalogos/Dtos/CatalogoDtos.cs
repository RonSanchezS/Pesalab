namespace PesajeFaeneo.Application.Catalogos.Dtos;

public sealed record GanaderoListado(int Id, string Marca, string Nombre, bool Activo);
public sealed record CrearGanadero(string Marca, string Nombre);
public sealed record EditarGanadero(int Id, string Marca, string Nombre);

public sealed record ClienteListado(int Id, int Codigo, string Nombre, string Direccion, bool Activo);
public sealed record CrearCliente(int Codigo, string Nombre, string? Direccion);
public sealed record EditarCliente(int Id, int Codigo, string Nombre, string? Direccion);

public sealed record FleteroListado(int Id, int Orden, string Nombre, string Celular, bool Activo);
public sealed record CrearFletero(int Orden, string Nombre, string Celular);
public sealed record EditarFletero(int Id, int Orden, string Nombre, string Celular);

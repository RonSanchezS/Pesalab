using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Builders;

public class UsuarioBuilder
{
    private string _nombreUsuario = "operador1";
    private string _nombreCompleto = "Operador de Prueba";
    private string _hash = "fake_hash";
    private string _salt = "fake_salt";
    private Rol _rol = Rol.Operador;
    private bool _activo = true;
    private bool _debeCambiarPassword = false;

    public UsuarioBuilder ConNombreUsuario(string nombreUsuario) { _nombreUsuario = nombreUsuario; return this; }
    public UsuarioBuilder ConRol(Rol rol) { _rol = rol; return this; }
    public UsuarioBuilder Inactivo() { _activo = false; return this; }
    public UsuarioBuilder ConDebeCambiarPassword(bool debe) { _debeCambiarPassword = debe; return this; }

    public Usuario Build() => new()
    {
        NombreUsuario = _nombreUsuario,
        NombreCompleto = _nombreCompleto,
        Hash = _hash,
        Salt = _salt,
        Rol = _rol,
        Activo = _activo,
        DebeCambiarPassword = _debeCambiarPassword,
        CreadoEn = DateTime.UtcNow
    };
}

public class GanaderoBuilder
{
    private string _marca = "AH";
    private string _nombre = "GANADERO EJEMPLO";
    private bool _activo = true;

    public GanaderoBuilder ConMarca(string marca) { _marca = marca; return this; }
    public GanaderoBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public GanaderoBuilder Inactivo() { _activo = false; return this; }

    public Ganadero Build() => new()
    {
        Marca = _marca,
        Nombre = _nombre,
        Activo = _activo,
        CreadoEn = DateTime.UtcNow
    };
}

public class ClienteBuilder
{
    private int _codigo = 110;
    private string _nombre = "CLIENTE EJEMPLO";
    private string _direccion = "FRIAL EJEMPLO PUESTO 1";
    private bool _activo = true;

    public ClienteBuilder ConCodigo(int codigo) { _codigo = codigo; return this; }
    public ClienteBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public ClienteBuilder ConDireccion(string direccion) { _direccion = direccion; return this; }
    public ClienteBuilder Inactivo() { _activo = false; return this; }

    public Cliente Build() => new()
    {
        Codigo = _codigo,
        Nombre = _nombre,
        Direccion = _direccion,
        Activo = _activo,
        CreadoEn = DateTime.UtcNow
    };
}

public class PesajeBuilder
{
    private long _numeroBoleta = 32779;
    private DateTime _fechaHora = DateTime.UtcNow;
    private int _ganaderoId;
    private int _clienteId;
    private string _direccion = "FRIAL EJEMPLO";
    private int _secuencia = 1;
    private string _colorRaza = "BLANCO";
    private char _sexo = 'M';
    private TipoDesposte _tipoDesposte = TipoDesposte.MediaRes;
    private decimal _pesoIzquierdo = 106.0m;
    private decimal _pesoDerecho = 0.0m;
    private decimal _pesoTotal = 106.0m;
    private string _observacion = "SIN NOVEDAD";
    private EstadoPesaje _estado = EstadoPesaje.Vigente;
    private int _usuarioId;

    public PesajeBuilder ConNumeroBoleta(long numero) { _numeroBoleta = numero; return this; }
    public PesajeBuilder ConGanaderoId(int id) { _ganaderoId = id; return this; }
    public PesajeBuilder ConClienteId(int id) { _clienteId = id; return this; }
    public PesajeBuilder ConUsuarioId(int id) { _usuarioId = id; return this; }
    public PesajeBuilder ConPesos(decimal izq, decimal der)
    {
        _pesoIzquierdo = izq;
        _pesoDerecho = der;
        _pesoTotal = izq + der;
        return this;
    }
    public PesajeBuilder ConDesposte(TipoDesposte desposte) { _tipoDesposte = desposte; return this; }
    public PesajeBuilder Anulado(string motivo, int anuladoPorId)
    {
        _estado = EstadoPesaje.Anulado;
        return this;
    }

    public Pesaje Build() => new()
    {
        NumeroBoleta = _numeroBoleta,
        FechaHora = _fechaHora,
        GanaderoId = _ganaderoId,
        ClienteId = _clienteId,
        Direccion = _direccion,
        Secuencia = _secuencia,
        ColorRaza = _colorRaza,
        Sexo = _sexo,
        TipoDesposte = _tipoDesposte,
        PesoIzquierdo = _pesoIzquierdo,
        PesoDerecho = _pesoDerecho,
        PesoTotal = _pesoTotal,
        Observacion = _observacion,
        Estado = _estado,
        UsuarioId = _usuarioId,
        CreadoEn = DateTime.UtcNow
    };
}

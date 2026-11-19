using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Builders;

/// <summary>
/// Fecha/hora fija usada por defecto en todos los builders de prueba en lugar de <c>DateTime.UtcNow</c>,
/// para que las pruebas sean deterministas (y coherentes con la convención de persistir todo en UTC).
/// Cada builder expone <c>ConFechaHora(DateTime)</c> para casos que necesiten un instante distinto.
/// </summary>
internal static class FechaPruebaPorDefecto
{
    public static readonly DateTime Valor = new(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);
}

public class UsuarioBuilder
{
    private string _nombreUsuario = "operador1";
    private string _nombreCompleto = "Operador de Prueba";
    private string _hash = "fake_hash";
    private string _salt = "fake_salt";
    private Rol _rol = Rol.Operador;
    private bool _activo = true;
    private bool _debeCambiarPassword = false;
    private int _intentosFallidos = 0;
    private DateTime? _bloqueadoHasta = null;
    private DateTime _fechaHora = FechaPruebaPorDefecto.Valor;

    public UsuarioBuilder ConNombreUsuario(string nombreUsuario) { _nombreUsuario = nombreUsuario; return this; }
    public UsuarioBuilder ConNombreCompleto(string nombreCompleto) { _nombreCompleto = nombreCompleto; return this; }
    public UsuarioBuilder ConRol(Rol rol) { _rol = rol; return this; }
    public UsuarioBuilder Inactivo() { _activo = false; return this; }
    public UsuarioBuilder ConDebeCambiarPassword(bool debe) { _debeCambiarPassword = debe; return this; }
    public UsuarioBuilder ConFechaHora(DateTime fechaHora) { _fechaHora = fechaHora; return this; }
    public UsuarioBuilder ConBloqueo(int intentos, DateTime? hasta) { _intentosFallidos = intentos; _bloqueadoHasta = hasta; return this; }
    public UsuarioBuilder ConPassword(string password, IPasswordHasher hasher)
    {
        var (hash, salt) = hasher.Hashear(password);
        _hash = hash;
        _salt = salt;
        return this;
    }

    public Usuario Build() => new()
    {
        NombreUsuario = _nombreUsuario,
        NombreCompleto = _nombreCompleto,
        Hash = _hash,
        Salt = _salt,
        Rol = _rol,
        Activo = _activo,
        DebeCambiarPassword = _debeCambiarPassword,
        IntentosFallidos = _intentosFallidos,
        BloqueadoHasta = _bloqueadoHasta,
        CreadoEn = _fechaHora
    };
}

public class GanaderoBuilder
{
    private string _codigo = "AH";
    private string _nombre = "GANADERO EJEMPLO";
    private bool _activo = true;
    private DateTime _fechaHora = FechaPruebaPorDefecto.Valor;

    public GanaderoBuilder ConCodigo(string codigo) { _codigo = codigo; return this; }
    public GanaderoBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public GanaderoBuilder Inactivo() { _activo = false; return this; }
    public GanaderoBuilder ConFechaHora(DateTime fechaHora) { _fechaHora = fechaHora; return this; }

    public Ganadero Build() => new()
    {
        Codigo = _codigo,
        Nombre = _nombre,
        Activo = _activo,
        CreadoEn = _fechaHora
    };
}

public class ClienteBuilder
{
    private int _codigo = 110;
    private string _nombre = "CLIENTE EJEMPLO";
    private string _direccion = "FRIAL EJEMPLO PUESTO 1";
    private bool _activo = true;
    private DateTime _fechaHora = FechaPruebaPorDefecto.Valor;

    public ClienteBuilder ConCodigo(int codigo) { _codigo = codigo; return this; }
    public ClienteBuilder ConNombre(string nombre) { _nombre = nombre; return this; }
    public ClienteBuilder ConDireccion(string direccion) { _direccion = direccion; return this; }
    public ClienteBuilder Inactivo() { _activo = false; return this; }
    public ClienteBuilder ConFechaHora(DateTime fechaHora) { _fechaHora = fechaHora; return this; }

    public Cliente Build() => new()
    {
        Codigo = _codigo,
        Nombre = _nombre,
        Direccion = _direccion,
        Activo = _activo,
        CreadoEn = _fechaHora
    };
}

public class PesajeBuilder
{
    private long _numeroBoleta = 32779;
    private DateTime _fechaHora = FechaPruebaPorDefecto.Valor;
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
    private OrigenPeso _origenPeso = OrigenPeso.Balanza;
    private int _usuarioId;
    private string? _motivoAnulacion;
    private int? _anuladoPorId;
    private DateTime? _anuladoEn;

    public PesajeBuilder ConNumeroBoleta(long numero) { _numeroBoleta = numero; return this; }
    public PesajeBuilder ConGanaderoId(int id) { _ganaderoId = id; return this; }
    public PesajeBuilder ConClienteId(int id) { _clienteId = id; return this; }
    public PesajeBuilder ConUsuarioId(int id) { _usuarioId = id; return this; }
    public PesajeBuilder ConFechaHora(DateTime fechaHora) { _fechaHora = fechaHora; return this; }
    public PesajeBuilder ConSecuencia(int secuencia) { _secuencia = secuencia; return this; }
    public PesajeBuilder ConOrigen(OrigenPeso origen) { _origenPeso = origen; return this; }
    public PesajeBuilder ConEstadoVigente()
    {
        _estado = EstadoPesaje.Vigente;
        _motivoAnulacion = null;
        _anuladoPorId = null;
        _anuladoEn = null;
        return this;
    }
    public PesajeBuilder ConPesos(decimal izq, decimal der)
    {
        _pesoIzquierdo = izq;
        _pesoDerecho = der;
        _pesoTotal = izq + der;
        return this;
    }
    public PesajeBuilder ConDesposte(TipoDesposte desposte) { _tipoDesposte = desposte; return this; }

    /// <summary>
    /// Antes ignoraba <paramref name="motivo"/> y <paramref name="anuladoPorId"/>: solo cambiaba el
    /// Estado, dejando MotivoAnulacion/AnuladoPorId/AnuladoEn en null. Eso viola la CK_Pesajes_Anulacion
    /// (T10), que exige los tres no-nulos cuando Estado = Anulado.
    /// </summary>
    public PesajeBuilder Anulado(string motivo, int anuladoPorId)
    {
        _estado = EstadoPesaje.Anulado;
        _motivoAnulacion = motivo;
        _anuladoPorId = anuladoPorId;
        _anuladoEn = _fechaHora;
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
        OrigenPeso = _origenPeso,
        Observacion = _observacion,
        Estado = _estado,
        MotivoAnulacion = _motivoAnulacion,
        AnuladoPorId = _anuladoPorId,
        AnuladoEn = _anuladoEn,
        UsuarioId = _usuarioId,
        CreadoEn = _fechaHora
    };
}

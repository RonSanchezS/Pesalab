using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public Rol Rol { get; set; } = Rol.Operador;
    public bool Activo { get; set; } = true;
    public bool DebeCambiarPassword { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime? UltimoAcceso { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class ParametrosSistema
{
    public int Id { get; set; } = 1;
    public string NombreEmpresa { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string RegistroSanitario { get; set; } = string.Empty;
    public string TelefonosReclamos { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public long SiguienteNumeroBoleta { get; set; } = 1;
    public string ObservacionPorDefecto { get; set; } = string.Empty;
    public bool ConfiguracionInicialCompleta { get; set; } = false;
    public DateTime ModificadoEn { get; set; }
}

public class ConfiguracionBalanza
{
    public int Id { get; set; } = 1;
    public TipoConexion TipoConexion { get; set; } = TipoConexion.Simulador;
    public string PuertoCom { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public ParidadSerial Paridad { get; set; } = ParidadSerial.None;
    public BitsParada BitsParada { get; set; } = BitsParada.One;
    public string HostTcp { get; set; } = "192.168.1.100";
    public int PuertoTcp { get; set; } = 4001;
    public string PatronRegex { get; set; } = @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";
    public int LecturasParaEstable { get; set; } = 5;
    public decimal ToleranciaKg { get; set; } = 0.5m;
    public int TimeoutMs { get; set; } = 3000;
    public DelimitadorTrama DelimitadorTrama { get; set; } = DelimitadorTrama.FinDeLinea;
    public UnidadPesoTrama UnidadPeso { get; set; } = UnidadPesoTrama.Kilogramos;
    public DateTime ModificadoEn { get; set; }
}

public class Ganadero
{
    public int Id { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }

    public ICollection<Pesaje> Pesajes { get; set; } = new List<Pesaje>();
}

public class Cliente
{
    public int Id { get; set; }
    public int Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }

    public ICollection<Pesaje> Pesajes { get; set; } = new List<Pesaje>();
}

public class Fletero
{
    public int Id { get; set; }
    public int Orden { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
}

public class Pesaje
{
    public int Id { get; set; }
    public long NumeroBoleta { get; set; }
    public DateTime FechaHora { get; set; }

    public int GanaderoId { get; set; }
    public Ganadero Ganadero { get; set; } = null!;

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public string Direccion { get; set; } = string.Empty;
    public int Secuencia { get; set; }
    public string ColorRaza { get; set; } = string.Empty;
    public char Sexo { get; set; } = 'M';
    public TipoDesposte TipoDesposte { get; set; } = TipoDesposte.MediaRes;

    public decimal PesoIzquierdo { get; set; }
    public decimal PesoDerecho { get; set; }
    public decimal PesoTotal { get; set; }
    public OrigenPeso OrigenPeso { get; set; } = OrigenPeso.Balanza;

    public string Observacion { get; set; } = string.Empty;
    public EstadoPesaje Estado { get; set; } = EstadoPesaje.Vigente;

    public string? MotivoAnulacion { get; set; }
    public int? AnuladoPorId { get; set; }
    public Usuario? AnuladoPor { get; set; }
    public DateTime? AnuladoEn { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime CreadoEn { get; set; }
    public DateTime? ModificadoEn { get; set; }
}

public class Auditoria
{
    public long Id { get; set; }
    public DateTime FechaHora { get; set; }
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public AccionAuditoria Accion { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public string? DatosAntesJson { get; set; }
    public string? DatosDespuesJson { get; set; }
    public string? Motivo { get; set; }
}

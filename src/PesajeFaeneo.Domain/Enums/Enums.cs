namespace PesajeFaeneo.Domain.Enums;

public enum Rol
{
    Administrador = 1,
    Supervisor = 2,
    Operador = 3
}

public enum TipoConexion
{
    Serial = 1,
    Tcp = 2,
    Simulador = 3
}

public enum TipoDesposte
{
    MediaRes = 1,
    ResCompleta = 2
}

public enum EstadoPesaje
{
    Vigente = 1,
    Anulado = 2
}

public enum OrigenPeso
{
    Balanza = 1,
    Simulador = 2,
    Importado = 3
}

public enum ParidadSerial
{
    None = 0,
    Odd = 1,
    Even = 2,
    Mark = 3,
    Space = 4
}

public enum BitsParada
{
    One = 1,
    Two = 2,
    OnePointFive = 3
}

public enum DelimitadorTrama
{
    FinDeLinea = 1,
    Espacio = 2,
    SignoIgual = 3
}

public enum UnidadPesoTrama
{
    Kilogramos = 1,
    Gramos = 2
}

public enum AccionAuditoria
{
    Crear = 1,
    Modificar = 2,
    Anular = 3,
    Login = 4,
    LoginFallido = 5,
    Logout = 6,
    CambioPassword = 7,
    RestablecerPassword = 8,
    CambioParametros = 9,
    CambioConfigBalanza = 10,
    CambioConfigImpresion = 11,
    Imprimir = 12,
    Reimprimir = 13,
    Backup = 14,
    Restauracion = 15,
    Importacion = 16,
    ActualizacionSistema = 17
}

public enum Permiso
{
    Pesar = 1,
    ImprimirBoleta = 2,
    ReimprimirBoleta = 3,
    AltaRapidaCatalogos = 4,
    VerReportes = 5,
    EditarBoleta = 6,
    AnularBoleta = 7,
    GestionarCatalogos = 8,
    GestionarUsuarios = 9,
    GestionarParametros = 10,
    ConfigurarBalanza = 11,
    BackupRestore = 12,
    ImportarLegado = 13,
    VerAuditoria = 14
}

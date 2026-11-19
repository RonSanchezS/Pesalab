using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Boletas.Dtos;

public sealed record EmpresaBoleta(
    string Nombre,
    string Direccion,
    string Ciudad,
    string Telefono,
    string RegistroSanitario,
    string TelefonosReclamos,
    string? RutaLogo);                 // ruta completa o null si no hay logo o no existe

public sealed record FleteroBoleta(int Codigo, string Nombre, string Celular);

public sealed record DatosBoleta(
    int PesajeId,                      // 0 en la muestra
    long NumeroBoleta,                 // 0 en la muestra
    DateTime FechaHoraLocal,           // hora local del pesaje (Kind = Unspecified)
    EmpresaBoleta Empresa,
    string CodigoGanadero,
    string NombreGanadero,
    int Secuencia,
    int CodigoCliente,
    string NombreCliente,
    string Direccion,
    string ColorRaza,
    char Sexo,
    TipoDesposte TipoDesposte,
    decimal PesoIzquierdo,
    decimal PesoDerecho,
    decimal PesoTotal,
    string Observacion,
    OrigenPeso Origen,
    EstadoPesaje Estado,
    string? MotivoAnulacion,
    string NombreOperador,
    FleteroBoleta? Fletero,            // null si el pesaje no tiene fletero (F10, O5)
    int ImpresionesPrevias,            // auditorías Imprimir/Reimprimir existentes (D10)
    DateTime GeneradoEnLocal,          // hora local en que se arma la boleta (pie y leyenda de reimpresión)
    bool EsMuestra = false,
    bool MostrarDesposte = true)       // F11 (D6): false si la boleta es del único tipo que pesa la empresa
{
    public bool EsReimpresion => ImpresionesPrevias > 0;
}

public sealed record RegistroImpresion(
    int PesajeId,
    long NumeroBoleta,
    bool EsReimpresion,
    int Copias,
    string Impresora,                  // nombre real o "(predeterminada)"
    FormatoBoleta Formato,
    string NombreEquipo);

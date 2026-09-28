using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Balanza;

public static class CamposBalanza
{
    public const string TipoConexion = nameof(TipoConexion);
    public const string PuertoCom = nameof(PuertoCom);
    public const string BaudRate = nameof(BaudRate);
    public const string DataBits = nameof(DataBits);
    public const string Paridad = nameof(Paridad);
    public const string BitsParada = nameof(BitsParada);
    public const string HostTcp = nameof(HostTcp);
    public const string PuertoTcp = nameof(PuertoTcp);
    public const string PatronRegex = nameof(PatronRegex);
    public const string LecturasParaEstable = nameof(LecturasParaEstable);
    public const string ToleranciaKg = nameof(ToleranciaKg);
    public const string TimeoutMs = nameof(TimeoutMs);
    public const string DelimitadorTrama = nameof(DelimitadorTrama);
    public const string UnidadPeso = nameof(UnidadPeso);
}

public static class ReglasBalanza
{
    public static readonly IReadOnlyList<int> BaudiosPermitidos = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
    public static readonly IReadOnlyList<int> BitsDatosPermitidos = [7, 8];
    public static readonly IReadOnlyList<BitsParada> BitsParadaPermitidos = [BitsParada.One, BitsParada.Two]; // ver R24
    public const string PatronPuertoCom = "^COM[1-9][0-9]{0,2}$";   // tras Trim + ToUpperInvariant
    public const int LongitudMaximaHost = 100;
    public const int PuertoTcpMinimo = 1;
    public const int PuertoTcpMaximo = 65535;
    public const int LongitudMaximaPatron = 250;
    public const string GrupoPeso = "peso";
    public const string GrupoEstable = "estable";
    public const int LecturasMinimas = 1;
    public const int LecturasMaximas = 50;
    public const decimal ToleranciaMinimaKg = 0m;
    public const decimal ToleranciaMaximaKg = 10m;
    public const int TimeoutMinimoMs = 500;
    public const int TimeoutMaximoMs = 60000;
    public const int TimeoutPorDefectoMs = 3000;

    public const decimal PesoMaximoAbsolutoKg = 99999.9m;
    public const int LongitudMaximaTrama = 256;
    public const int TiempoLimiteRegexMs = 100;
    public const int TimeoutsAntesDeReconectar = 3;
    public const int TramasInvalidasConsecutivasParaAlerta = 3;
    public static readonly IReadOnlyList<int> EsperasReconexionSegundos = [1, 2, 4, 8, 10]; // el último se repite
    public const int EsperaTrasCerrarPuertoMs = 500;
    public const int CapacidadCanalTramas = 64;
    public const int TramasRecientesMaximo = 50;
    public const int DuracionPruebaSegundos = 10;
    public const int IntervaloSimuladorMs = 200;
    public const decimal PesoMaximoSimuladorKg = 2000m;
    public const int IntervaloMonitorUiMs = 100;
}

using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public static class CamposImportacion
{
    public const string Marca = "Marca";
    public const string Nombre = "Nombre";
    public const string Activo = "Activo";
    public const string Codigo = "Codigo";
    public const string Direccion = "Direccion";
    public const string NumeroBoleta = "NumeroBoleta";
    public const string Fecha = "Fecha";
    public const string Hora = "Hora";
    public const string MarcaGanadero = "MarcaGanadero";
    public const string CodigoCliente = "CodigoCliente";
    public const string Secuencia = "Secuencia";
    public const string ColorRaza = "ColorRaza";
    public const string Sexo = "Sexo";
    public const string TipoDesposte = "TipoDesposte";
    public const string PesoIzquierdo = "PesoIzquierdo";
    public const string PesoDerecho = "PesoDerecho";
    public const string PesoTotal = "PesoTotal";
    public const string Observacion = "Observacion";
    public const string Estado = "Estado";
    public const string MotivoAnulacion = "MotivoAnulacion";
    public const string Mapeo = "Mapeo";

    private static readonly IReadOnlyList<CampoImportacion> CamposGanaderos = new CampoImportacion[]
    {
        new(Marca, "Marca", true, new[] { "MARCA", "CODMARCA", "CODIGOMARCA", "MARCAGANADERO" }),
        new(Nombre, "Nombre", true, new[] { "NOMBRE", "GANADERO", "NOMBREGANADERO", "NOMGAN", "PROPIETARIO" }),
        new(Activo, "Activo", false, new[] { "ACTIVO", "VIGENTE" })
    };

    private static readonly IReadOnlyList<CampoImportacion> CamposClientes = new CampoImportacion[]
    {
        new(Codigo, "Código", true, new[] { "CODIGO", "COD", "CODCLI", "CODCLIENTE", "CODIGOCLIENTE", "NROCLIENTE" }),
        new(Nombre, "Nombre", true, new[] { "NOMBRE", "CLIENTE", "NOMBRECLIENTE", "NOMCLI", "RAZONSOCIAL" }),
        new(Direccion, "Dirección", false, new[] { "DIRECCION", "DIR", "DOMICILIO" }),
        new(Activo, "Activo", false, new[] { "ACTIVO", "VIGENTE" })
    };

    private static readonly IReadOnlyList<CampoImportacion> CamposPesajes = new CampoImportacion[]
    {
        new(NumeroBoleta, "N° de boleta", true, new[] { "NUMEROBOLETA", "NROBOLETA", "NUMBOLETA", "BOLETA", "NROBOL", "NUMBOL", "NBOLETA", "NBOL", "NUMERO", "NRO" }),
        new(Fecha, "Fecha", true, new[] { "FECHA", "FECHAPESAJE", "FECHAHORA", "FEC" }),
        new(Hora, "Hora", false, new[] { "HORA", "HORAPESAJE" }),
        new(MarcaGanadero, "Marca del ganadero", true, new[] { "MARCA", "MARCAGANADERO", "CODMARCA", "GANADERO" }),
        new(CodigoCliente, "Código del cliente", true, new[] { "CODIGOCLIENTE", "CODCLIENTE", "CODCLI", "CLIENTE", "CODIGOCLI" }),
        new(Secuencia, "N° de res (secuencia)", false, new[] { "SECUENCIA", "NRORES", "NUMRES", "NRES", "RES", "SEC" }),
        new(Direccion, "Dirección", false, new[] { "DIRECCION", "DIR", "DESTINO" }),
        new(ColorRaza, "Color / raza", false, new[] { "COLORRAZA", "COLOR", "RAZA" }),
        new(Sexo, "Sexo", false, new[] { "SEXO", "SEX" }),
        new(TipoDesposte, "Tipo de desposte", false, new[] { "TIPODESPOSTE", "DESPOSTE", "TIPOCORTE", "CORTE", "TIPO" }),
        new(PesoIzquierdo, "Peso izquierdo", true, new[] { "PESOIZQUIERDO", "PESOIZQ", "IZQUIERDO", "IZQ", "PIZQ", "PESOKGIZQ" }),
        new(PesoDerecho, "Peso derecho", true, new[] { "PESODERECHO", "PESODER", "DERECHO", "DER", "PDER" }),
        new(PesoTotal, "Peso total", false, new[] { "PESOTOTAL", "TOTAL", "TOTALNETO", "PESONETO", "NETO", "PESO" }),
        new(Observacion, "Observación", false, new[] { "OBSERVACION", "OBS", "OBSERVACIONES", "NOTA" }),
        new(Estado, "Estado / anulada", false, new[] { "ESTADO", "ANULADA", "ANULADO" }),
        new(MotivoAnulacion, "Motivo de anulación", false, new[] { "MOTIVOANULACION", "MOTIVO" })
    };

    public static IReadOnlyList<CampoImportacion> De(TipoImportacion tipo)
    {
        return tipo switch
        {
            TipoImportacion.Ganaderos => CamposGanaderos,
            TipoImportacion.Clientes => CamposClientes,
            TipoImportacion.Pesajes => CamposPesajes,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de importación no soportado.")
        };
    }
}

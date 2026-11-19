using PesajeFaeneo.Application.Licencias;

namespace PesajeFaeneo.Tests.Utilidades.Licencias;

public sealed class IdentificadorEquipoFalso : IIdentificadorEquipo
{
    public const string MachineGuid = "11111111-2222-3333-4444-555555555555";

    public string Hwid { get; set; } = FirmaLicencia.Hwid(MachineGuid);
}

/// <summary>Almacén en memoria que cuenta las escrituras (para comprobar el refresco de <c>l</c>).</summary>
public sealed class AlmacenLicenciaFalso : IAlmacenLicenciaLocal
{
    public DatosLicenciaLocal Datos { get; set; } = DatosLicenciaLocal.Vacio;

    public int EscriturasPrueba { get; private set; }

    public int EscriturasActivacion { get; private set; }

    public Exception? ErrorAlEscribir { get; set; }

    public DatosLicenciaLocal Leer() => Datos;

    public void EscribirPrueba(long inicio, long ultimo, string firma)
    {
        if (ErrorAlEscribir is not null)
        {
            throw ErrorAlEscribir;
        }

        EscriturasPrueba++;
        Datos = Datos with
        {
            Inicio = inicio.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Ultimo = ultimo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Firma = firma
        };
    }

    public void EscribirActivacion(string token)
    {
        if (ErrorAlEscribir is not null)
        {
            throw ErrorAlEscribir;
        }

        EscriturasActivacion++;
        Datos = Datos with { Activacion = token };
    }

    public void LimpiarActivacion() => EscribirActivacion(string.Empty);

    /// <summary>Deja una prueba válida (firmada) con esos valores.</summary>
    public void SembrarPrueba(string hwid, long inicio, long ultimo)
    {
        Datos = Datos with
        {
            Inicio = inicio.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Ultimo = ultimo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Firma = FirmaLicencia.Prueba(inicio, ultimo, hwid)
        };
    }

    public void SembrarActivacion(string hwid) => Datos = Datos with { Activacion = FirmaLicencia.TokenActivacion(hwid) };
}

public sealed class ServidorLicenciasFalso : IServidorLicencias
{
    public ResultadoActivacionServidor ResultadoActivacion { get; set; } = ResultadoActivacionServidor.Ok;

    public long? InicioPruebaRegistrada { get; set; }

    public EstadoRemotoLicencia Remoto { get; set; } = new(EstadoClaveRemota.Ninguna, null);

    /// <summary>Si no es null, toda llamada lanza esto (simula red caída o error del servidor).</summary>
    public ServidorLicenciasException? Error { get; set; }

    public List<string> Claves { get; } = new();

    public int ActivacionesRecibidas { get; private set; }

    public int PruebasRegistradas { get; private set; }

    public int Consultas { get; private set; }

    public Task<ResultadoActivacionServidor> ActivarAsync(string clave, string hwid, CancellationToken cancellationToken = default)
    {
        ActivacionesRecibidas++;
        Claves.Add(clave);
        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(ResultadoActivacion);
    }

    public Task<long?> RegistrarPruebaAsync(string hwid, CancellationToken cancellationToken = default)
    {
        PruebasRegistradas++;
        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(InicioPruebaRegistrada);
    }

    public Task<EstadoRemotoLicencia> ConsultarAsync(string hwid, CancellationToken cancellationToken = default)
    {
        Consultas++;
        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(Remoto);
    }
}

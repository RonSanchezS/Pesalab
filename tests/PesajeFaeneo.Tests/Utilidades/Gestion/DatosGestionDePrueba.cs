using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Utilidades.Gestion;

public static class DatosGestionDePrueba
{
    public static BoletaParaEdicion CrearBoletaParaEdicion(
        int id = 1,
        long numeroBoleta = 3001,
        int version = 0,
        DateTime? fechaHoraLocal = null,
        int ganaderoId = 10,
        int clienteId = 20,
        string direccion = "CALLE PRUEBA 123",
        int secuencia = 1,
        string colorRaza = "BLANCO",
        char sexo = 'M',
        string observacion = "OBSERVACION PRUEBA",
        TipoDesposte tipoDesposte = TipoDesposte.MediaRes,
        decimal pesoIzquierdo = 120.0m,
        decimal pesoDerecho = 0.0m,
        decimal pesoTotal = 120.0m,
        OrigenPeso origen = OrigenPeso.Balanza,
        string nombreOperador = "OPERADOR TEST",
        IReadOnlyList<ElementoBusqueda>? ganaderos = null,
        IReadOnlyList<ElementoBusqueda>? clientes = null)
    {
        var listaGan = ganaderos ?? new[]
        {
            new ElementoBusqueda(10, "AH", "GANADERO AH"),
            new ElementoBusqueda(11, "BB", "GANADERO BB")
        };

        var listaCli = clientes ?? new[]
        {
            new ElementoBusqueda(20, "100", "CLIENTE 100"),
            new ElementoBusqueda(21, "200", "CLIENTE 200")
        };

        return new BoletaParaEdicion(
            Id: id,
            NumeroBoleta: numeroBoleta,
            Version: version,
            FechaHoraLocal: fechaHoraLocal ?? new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Unspecified),
            GanaderoId: ganaderoId,
            ClienteId: clienteId,
            Direccion: direccion,
            Secuencia: secuencia,
            ColorRaza: colorRaza,
            Sexo: sexo,
            Observacion: observacion,
            TipoDesposte: tipoDesposte,
            PesoIzquierdo: pesoIzquierdo,
            PesoDerecho: pesoDerecho,
            PesoTotal: pesoTotal,
            Origen: origen,
            NombreOperador: nombreOperador,
            Ganaderos: listaGan,
            Clientes: listaCli);
    }

    public static BoletaEncontrada CrearBoletaEncontrada(
        int id = 1,
        long numeroBoleta = 3001,
        int version = 0,
        DateTime? fechaHoraLocal = null,
        string marca = "AH",
        string nombreGanadero = "GANADERO AH",
        int secuencia = 1,
        string nombreCliente = "CLIENTE 100",
        TipoDesposte tipoDesposte = TipoDesposte.MediaRes,
        decimal pesoIzquierdo = 120.0m,
        decimal pesoDerecho = 0.0m,
        decimal pesoTotal = 120.0m,
        OrigenPeso origen = OrigenPeso.Balanza,
        EstadoPesaje estado = EstadoPesaje.Vigente,
        string? motivoAnulacion = null)
    {
        return new BoletaEncontrada(
            Id: id,
            NumeroBoleta: numeroBoleta,
            Version: version,
            FechaHoraLocal: fechaHoraLocal ?? new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Unspecified),
            Marca: marca,
            NombreGanadero: nombreGanadero,
            Secuencia: secuencia,
            NombreCliente: nombreCliente,
            TipoDesposte: tipoDesposte,
            PesoIzquierdo: pesoIzquierdo,
            PesoDerecho: pesoDerecho,
            PesoTotal: pesoTotal,
            Origen: origen,
            Estado: estado,
            MotivoAnulacion: motivoAnulacion);
    }
}

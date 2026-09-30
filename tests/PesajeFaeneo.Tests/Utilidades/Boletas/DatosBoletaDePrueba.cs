using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public static class DatosBoletaDePrueba
{
    public static DatosBoleta Crear(
        int pesajeId = 1,
        long numeroBoleta = 32779,
        DateTime? fechaHoraLocal = null,
        EmpresaBoleta? empresa = null,
        string marcaGanadero = "AH",
        string nombreGanadero = "GANADERO EJEMPLO",
        int secuencia = 1,
        int codigoCliente = 110,
        string nombreCliente = "CLIENTE EJEMPLO",
        string direccion = "CALLE EJEMPLO 123",
        string colorRaza = "BLANCO",
        char sexo = 'M',
        TipoDesposte tipoDesposte = TipoDesposte.ResCompleta,
        decimal pesoIzquierdo = 120.0m,
        decimal pesoDerecho = 118.5m,
        decimal pesoTotal = 238.5m,
        string observacion = "SIN NOVEDAD",
        OrigenPeso origen = OrigenPeso.Balanza,
        EstadoPesaje estado = EstadoPesaje.Vigente,
        string? motivoAnulacion = null,
        string nombreOperador = "JUAN OPERADOR",
        IReadOnlyList<FleteroBoleta>? fleteros = null,
        int impresionesPrevias = 0,
        DateTime? generadoEnLocal = null,
        bool esMuestra = false)
    {
        var fecha = fechaHoraLocal ?? new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified);
        var gen = generadoEnLocal ?? fecha;
        var emp = empresa ?? new EmpresaBoleta("FRIGORÍFICO CENTRAL", "CALLE 1", "SANTA CRUZ", "70000000", "REG-1", "80000000", null);

        return new DatosBoleta(
            PesajeId: pesajeId,
            NumeroBoleta: numeroBoleta,
            FechaHoraLocal: fecha,
            Empresa: emp,
            MarcaGanadero: marcaGanadero,
            NombreGanadero: nombreGanadero,
            Secuencia: secuencia,
            CodigoCliente: codigoCliente,
            NombreCliente: nombreCliente,
            Direccion: direccion,
            ColorRaza: colorRaza,
            Sexo: sexo,
            TipoDesposte: tipoDesposte,
            PesoIzquierdo: pesoIzquierdo,
            PesoDerecho: pesoDerecho,
            PesoTotal: pesoTotal,
            Observacion: observacion,
            Origen: origen,
            Estado: estado,
            MotivoAnulacion: motivoAnulacion,
            NombreOperador: nombreOperador,
            Fleteros: fleteros ?? Array.Empty<FleteroBoleta>(),
            ImpresionesPrevias: impresionesPrevias,
            GeneradoEnLocal: gen,
            EsMuestra: esMuestra);
    }
}

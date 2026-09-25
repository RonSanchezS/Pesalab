using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FabricaAuditoriaTests
{
    [Fact]
    public void Crear_GanaderoConPesajesNavegacion_SerializaSoloSnapshotSinPropiedadPesajes()
    {
        var ganadero = new Ganadero
        {
            Id = 15,
            Marca = "AH",
            Nombre = "GANADERO EJEMPLO",
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        var pesaje = new Pesaje
        {
            Id = 100,
            NumeroBoleta = 12345,
            GanaderoId = 15,
            Ganadero = ganadero
        };
        ganadero.Pesajes.Add(pesaje);

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 1,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Ganadero,
            ganadero.Id.ToString(),
            despues: ganadero,
            motivo: "Alta ganadero con navegacion");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.DoesNotContain("Pesajes", auditoria.DatosDespuesJson);
        Assert.Contains("\"Id\":15", auditoria.DatosDespuesJson);
        Assert.Contains("\"Marca\":\"AH\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"Nombre\":\"GANADERO EJEMPLO\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"Activo\":true", auditoria.DatosDespuesJson);
    }
}

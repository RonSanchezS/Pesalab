using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class MatrizPermisosTests
{
    // Tabla D1 escrita explícitamente a mano en el test (sin derivar de MatrizPermisos)
    private static readonly Dictionary<(Rol Rol, Permiso Permiso), bool> MatrizEsperada = new()
    {
        // Operador
        { (Rol.Operador, Permiso.Pesar), true },
        { (Rol.Operador, Permiso.ImprimirBoleta), true },
        { (Rol.Operador, Permiso.ReimprimirBoleta), true },
        { (Rol.Operador, Permiso.AltaRapidaCatalogos), true },
        { (Rol.Operador, Permiso.VerReportes), false },
        { (Rol.Operador, Permiso.EditarBoleta), false },
        { (Rol.Operador, Permiso.AnularBoleta), false },
        { (Rol.Operador, Permiso.GestionarCatalogos), false },
        { (Rol.Operador, Permiso.VerAuditoria), false },
        { (Rol.Operador, Permiso.GestionarUsuarios), false },
        { (Rol.Operador, Permiso.GestionarParametros), false },
        { (Rol.Operador, Permiso.ConfigurarBalanza), false },
        { (Rol.Operador, Permiso.BackupRestore), false },
        { (Rol.Operador, Permiso.ImportarLegado), false },

        // Supervisor
        { (Rol.Supervisor, Permiso.Pesar), true },
        { (Rol.Supervisor, Permiso.ImprimirBoleta), true },
        { (Rol.Supervisor, Permiso.ReimprimirBoleta), true },
        { (Rol.Supervisor, Permiso.AltaRapidaCatalogos), true },
        { (Rol.Supervisor, Permiso.VerReportes), true },
        { (Rol.Supervisor, Permiso.EditarBoleta), true },
        { (Rol.Supervisor, Permiso.AnularBoleta), true },
        { (Rol.Supervisor, Permiso.GestionarCatalogos), true },
        { (Rol.Supervisor, Permiso.VerAuditoria), true },
        { (Rol.Supervisor, Permiso.GestionarUsuarios), false },
        { (Rol.Supervisor, Permiso.GestionarParametros), false },
        { (Rol.Supervisor, Permiso.ConfigurarBalanza), false },
        { (Rol.Supervisor, Permiso.BackupRestore), false },
        { (Rol.Supervisor, Permiso.ImportarLegado), false },

        // Administrador
        { (Rol.Administrador, Permiso.Pesar), true },
        { (Rol.Administrador, Permiso.ImprimirBoleta), true },
        { (Rol.Administrador, Permiso.ReimprimirBoleta), true },
        { (Rol.Administrador, Permiso.AltaRapidaCatalogos), true },
        { (Rol.Administrador, Permiso.VerReportes), true },
        { (Rol.Administrador, Permiso.EditarBoleta), true },
        { (Rol.Administrador, Permiso.AnularBoleta), true },
        { (Rol.Administrador, Permiso.GestionarCatalogos), true },
        { (Rol.Administrador, Permiso.VerAuditoria), true },
        { (Rol.Administrador, Permiso.GestionarUsuarios), true },
        { (Rol.Administrador, Permiso.GestionarParametros), true },
        { (Rol.Administrador, Permiso.ConfigurarBalanza), true },
        { (Rol.Administrador, Permiso.BackupRestore), true },
        { (Rol.Administrador, Permiso.ImportarLegado), true },
    };

    public static TheoryData<Rol, Permiso, bool> ObtenerCasos()
    {
        var data = new TheoryData<Rol, Permiso, bool>();
        foreach (var kvp in MatrizEsperada)
        {
            data.Add(kvp.Key.Rol, kvp.Key.Permiso, kvp.Value);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ObtenerCasos))]
    public void Tiene_VerificaContraTablaD1(Rol rol, Permiso permiso, bool esperado)
    {
        var resultado = MatrizPermisos.Tiene(rol, permiso);
        Assert.Equal(esperado, resultado);

        var conjunto = MatrizPermisos.PermisosDe(rol);
        Assert.Equal(esperado, conjunto.Contains(permiso));
    }

    [Fact]
    public void Administrador_TieneTodosLosPermisosDefinidos()
    {
        var todosLosPermisos = Enum.GetValues<Permiso>();
        var permisosAdmin = MatrizPermisos.PermisosDe(Rol.Administrador);

        foreach (var permiso in todosLosPermisos)
        {
            Assert.Contains(permiso, permisosAdmin);
        }
    }
}

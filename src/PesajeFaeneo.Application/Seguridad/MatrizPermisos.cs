using System.Collections.Frozen;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Seguridad;

public static class MatrizPermisos
{
    private static readonly IReadOnlySet<Permiso> PermisosOperador = new HashSet<Permiso>
    {
        Permiso.Pesar,
        Permiso.ImprimirBoleta,
        Permiso.ReimprimirBoleta,
        Permiso.AltaRapidaCatalogos
    }.ToFrozenSet();

    private static readonly IReadOnlySet<Permiso> PermisosSupervisor = new HashSet<Permiso>
    {
        Permiso.Pesar,
        Permiso.ImprimirBoleta,
        Permiso.ReimprimirBoleta,
        Permiso.AltaRapidaCatalogos,
        Permiso.VerReportes,
        Permiso.EditarBoleta,
        Permiso.AnularBoleta,
        Permiso.GestionarCatalogos,
        Permiso.VerAuditoria
    }.ToFrozenSet();

    private static readonly IReadOnlySet<Permiso> PermisosAdministrador = new HashSet<Permiso>
    {
        Permiso.Pesar,
        Permiso.ImprimirBoleta,
        Permiso.ReimprimirBoleta,
        Permiso.AltaRapidaCatalogos,
        Permiso.VerReportes,
        Permiso.EditarBoleta,
        Permiso.AnularBoleta,
        Permiso.GestionarCatalogos,
        Permiso.VerAuditoria,
        Permiso.GestionarUsuarios,
        Permiso.GestionarParametros,
        Permiso.ConfigurarBalanza,
        Permiso.BackupRestore,
        Permiso.ImportarLegado
    }.ToFrozenSet();

    private static readonly IReadOnlySet<Permiso> ConjuntoVacio = new HashSet<Permiso>().ToFrozenSet();

    public static IReadOnlySet<Permiso> PermisosDe(Rol rol) => rol switch
    {
        Rol.Operador => PermisosOperador,
        Rol.Supervisor => PermisosSupervisor,
        Rol.Administrador => PermisosAdministrador,
        _ => ConjuntoVacio
    };

    public static bool Tiene(Rol rol, Permiso permiso) => PermisosDe(rol).Contains(permiso);
}

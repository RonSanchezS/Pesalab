using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.WinForms.Infraestructura;

public sealed record OpcionMenu(Permiso? Permiso, string? FaseFutura = null);

public static class AutorizadorMenu
{
    public static void Aplicar(ToolStripItemCollection items, Func<Permiso, bool> tienePermiso)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(tienePermiso);

        foreach (ToolStripItem rawItem in items)
        {
            if (rawItem is not ToolStripMenuItem item)
            {
                continue;
            }

            if (item.Tag is OpcionMenu opcion)
            {
                var autorizado = opcion.Permiso is null || tienePermiso(opcion.Permiso.Value);
                item.Available = autorizado;

                if (opcion.FaseFutura is not null)
                {
                    item.Enabled = false;
                    item.ToolTipText = $"Disponible en la fase {opcion.FaseFutura}";
                }
            }

            if (item.HasDropDownItems)
            {
                Aplicar(item.DropDownItems, tienePermiso);

                // Solo cuentan los ítems ejecutables/contenedores: un separador no debe
                // mantener visible a un padre cuyos ítems reales están todos ocultos.
                var hayHijosDisponibles = item.DropDownItems
                    .OfType<ToolStripMenuItem>()
                    .Any(h => h.Available);

                if (!hayHijosDisponibles)
                {
                    item.Available = false;
                }
            }
        }
    }
}

namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Piezas comunes de las barras de filtros (Buscar boletas, Visor de auditoría): etiquetas y botones que se
/// dimensionan por su texto en lugar de anchos fijos en píxeles, y un panel que las acomoda en filas.
/// </summary>
internal static class DisenoFiltros
{
    /// <summary>Formato fijo de fecha de los filtros, igual al de la boleta (es-BO), sin depender de la región del equipo.</summary>
    public const string FormatoFecha = "dd/MM/yyyy";

    public static FlowLayoutPanel CrearPanel() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true
    };

    /// <summary>
    /// Grupo con título que envuelve el panel de filtros. Su alto sigue al del panel: el AutoSize del GroupBox
    /// no se recalcula cuando el panel crece después del escalado por DPI y la segunda fila quedaba tapada.
    /// </summary>
    public static GroupBox CrearGrupo(string titulo, FlowLayoutPanel filtros)
    {
        var grupo = new GroupBox
        {
            Text = titulo,
            Dock = DockStyle.Top,
            Padding = new Padding(6, 4, 6, 6)
        };
        grupo.Controls.Add(filtros);

        void Ajustar() => grupo.Height = filtros.Bottom + grupo.Padding.Bottom;
        filtros.SizeChanged += (_, _) => Ajustar();
        filtros.LocationChanged += (_, _) => Ajustar();
        Ajustar();
        return grupo;
    }

    // En un FlowLayoutPanel, un control anclado solo a la izquierda queda centrado verticalmente en su fila.
    public static Label Etiqueta(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(10, 3, 2, 3)
    };

    public static DateTimePicker Fecha(DateTime valor) => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = FormatoFecha,
        ShowCheckBox = true,
        Checked = true,
        Value = valor,
        Width = 140,
        Anchor = AnchorStyles.Left
    };

    public static Button Boton(string texto, int anchoMinimo) => new()
    {
        Text = texto,
        AutoSize = true,
        // GrowOnly: el tamaño dado es el mínimo (y se escala por DPI); crece si el texto no entra.
        AutoSizeMode = AutoSizeMode.GrowOnly,
        Size = new Size(anchoMinimo, 30),
        Anchor = AnchorStyles.Left,
        Margin = new Padding(10, 3, 3, 3)
    };
}

using System.Drawing;
using System.Windows.Forms;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.WinForms.Formularios;

namespace PesajeFaeneo.WinForms.Formularios.Utilitarios;

public class FrmConfirmarRestauracion : FormBase
{
    internal readonly TableLayoutPanel tblDetalles;
    internal readonly FlowLayoutPanel pnlAdvertencias;
    internal readonly Label lblAviso;
    internal readonly Label lblPrompt;
    internal readonly TextBox txtConfirmacion;
    internal readonly Button btnRestaurar;
    internal readonly Button btnCancelar;

    public string Confirmacion => txtConfirmacion.Text;

    public FrmConfirmarRestauracion(ValidacionRestauracion validacion, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(validacion);
        ArgumentNullException.ThrowIfNull(zona);

        Text = "Confirmar restauración";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(550, 450);
        Padding = new Padding(12);

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(validacion.Backup.CreadoEnUtc, zona);
        var fechaLocalStr = fechaLocal.ToString("dd/MM/yyyy HH:mm", TextosBoleta.Cultura);

        var pnlPrincipal = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        tblDetalles = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(4),
            Margin = new Padding(0, 0, 0, 10)
        };
        tblDetalles.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tblDetalles.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void AgregarFila(int fila, string etiqueta, string valor)
        {
            var lblEtiqueta = new Label
            {
                Text = etiqueta + ":",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(4, 3, 12, 3)
            };
            var lblValor = new Label
            {
                Text = valor,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 3, 4, 3)
            };
            tblDetalles.Controls.Add(lblEtiqueta, 0, fila);
            tblDetalles.Controls.Add(lblValor, 1, fila);
        }

        AgregarFila(0, "Fecha de la copia", fechaLocalStr);
        AgregarFila(1, "Tipo", ReglasBackup.TextoTipo(validacion.Backup.Tipo));
        AgregarFila(2, "Equipo", validacion.Backup.Equipo);
        AgregarFila(3, "Versión del sistema", validacion.Backup.VersionAplicacion);
        AgregarFila(4, "Boletas", validacion.Backup.Pesajes.ToString(TextosBoleta.Cultura));
        AgregarFila(5, "Última boleta", validacion.Backup.UltimoNumeroBoleta?.ToString(TextosBoleta.Cultura) ?? "—");
        AgregarFila(6, "Ganaderos", validacion.Ganaderos.ToString(TextosBoleta.Cultura));
        AgregarFila(7, "Clientes", validacion.Clientes.ToString(TextosBoleta.Cultura));
        AgregarFila(8, "Usuarios activos", validacion.UsuariosActivos.ToString(TextosBoleta.Cultura));
        AgregarFila(9, "Administradores activos", validacion.AdministradoresActivos.ToString(TextosBoleta.Cultura));

        pnlPrincipal.Controls.Add(tblDetalles);

        pnlAdvertencias = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10)
        };

        foreach (var adv in validacion.Advertencias)
        {
            var lblAdv = new Label
            {
                Text = "⚠ " + adv,
                AutoSize = true,
                MaximumSize = new Size(500, 0),
                ForeColor = Color.DarkOrange,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(4, 2, 4, 2)
            };
            pnlAdvertencias.Controls.Add(lblAdv);
        }

        pnlPrincipal.Controls.Add(pnlAdvertencias);

        lblAviso = new Label
        {
            Text = $"Se reemplazarán TODOS los datos actuales por los de esta copia. Lo registrado después del {fechaLocalStr} se perderá (queda guardado en una copia de resguardo).",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
            ForeColor = Color.DarkRed,
            Margin = new Padding(4, 5, 4, 10)
        };
        pnlPrincipal.Controls.Add(lblAviso);

        lblPrompt = new Label
        {
            Text = "Escriba RESTAURAR para confirmar:",
            AutoSize = true,
            Margin = new Padding(4, 5, 4, 3)
        };
        pnlPrincipal.Controls.Add(lblPrompt);

        txtConfirmacion = new TextBox
        {
            Width = 200,
            Margin = new Padding(4, 2, 4, 15)
        };
        pnlPrincipal.Controls.Add(txtConfirmacion);

        var pnlBotones = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        btnRestaurar = new Button
        {
            Text = "Restaurar",
            DialogResult = DialogResult.OK,
            Enabled = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(100, 32),
            Margin = new Padding(4, 3, 8, 3)
        };

        btnCancelar = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(100, 32),
            Margin = new Padding(4, 3, 4, 3)
        };

        pnlBotones.Controls.Add(btnRestaurar);
        pnlBotones.Controls.Add(btnCancelar);
        pnlPrincipal.Controls.Add(pnlBotones);

        Controls.Add(pnlPrincipal);
        CancelButton = btnCancelar;

        txtConfirmacion.TextChanged += (_, _) =>
        {
            btnRestaurar.Enabled = string.Equals(txtConfirmacion.Text.Trim(), "RESTAURAR", StringComparison.Ordinal);
        };
    }
}

namespace PesajeFaeneo.WinForms.Formularios.Ayuda;

using System.Runtime.InteropServices;
using PesajeFaeneo.Infrastructure.Common;

public sealed class FrmAcercaDe : FormBase
{
    private readonly AppPaths _appPaths;
    internal Action<string>? CopiarAlPortapapelesParaTest { get; set; }
    internal string TextoSoporte { get; }
    internal Button BtnCopiar { get; private set; } = null!;
    internal Button BtnCerrar { get; private set; } = null!;

    public FrmAcercaDe(AppPaths appPaths)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));

        Text = "Acerca de";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ShowInTaskbar = false;

        var datos = new (string Etiqueta, string Valor)[]
        {
            ("Versión", InfoVersion.Version),
            ("Compilación", InfoVersion.Informativa),
            (".NET", RuntimeInformation.FrameworkDescription),
            ("Proceso", Environment.Is64BitProcess ? "64 bits" : "32 bits"),
            ("Equipo", Environment.MachineName),
            ("Carpeta de datos", _appPaths.BaseDir),
            ("Carpeta del programa", AppContext.BaseDirectory.TrimEnd('\\'))
        };

        var lineas = new List<string> { InfoVersion.Producto };
        foreach (var (etiqueta, valor) in datos)
        {
            lineas.Add($"{etiqueta}: {valor}");
        }
        TextoSoporte = string.Join(Environment.NewLine, lineas);

        InicializarComponentes(datos);
    }

    private void InicializarComponentes((string Etiqueta, string Valor)[] datos)
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTitulo = new Label
        {
            Text = InfoVersion.Producto,
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 15)
        };
        mainLayout.Controls.Add(lblTitulo, 0, 0);

        var tlpTabla = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = datos.Length,
            Margin = new Padding(0, 0, 0, 15)
        };
        tlpTabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlpTabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (var i = 0; i < datos.Length; i++)
        {
            tlpTabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblEtiqueta = new Label
            {
                Text = datos[i].Etiqueta,
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 4, 15, 4)
            };

            var lblValor = new Label
            {
                Text = datos[i].Valor,
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Margin = new Padding(0, 4, 0, 4)
            };

            tlpTabla.Controls.Add(lblEtiqueta, 0, i);
            tlpTabla.Controls.Add(lblValor, 1, i);
        }
        mainLayout.Controls.Add(tlpTabla, 0, 1);

        var flpBotones = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0)
        };

        BtnCerrar = new Button
        {
            Text = "Cerrar",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(90, 32),
            Margin = new Padding(0, 0, 0, 0)
        };

        BtnCopiar = new Button
        {
            Text = "Copiar para soporte",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Size = new Size(150, 32),
            Margin = new Padding(0, 0, 10, 0)
        };
        BtnCopiar.Click += (_, _) => CopiarAlPortapapeles(TextoSoporte);

        flpBotones.Controls.Add(BtnCerrar);
        flpBotones.Controls.Add(BtnCopiar);
        mainLayout.Controls.Add(flpBotones, 0, 2);

        Controls.Add(mainLayout);
        AcceptButton = BtnCerrar;
        CancelButton = BtnCerrar;
    }

    private void CopiarAlPortapapeles(string texto)
    {
        if (CopiarAlPortapapelesParaTest != null)
        {
            CopiarAlPortapapelesParaTest(texto);
            return;
        }

        Clipboard.SetText(texto);
    }
}

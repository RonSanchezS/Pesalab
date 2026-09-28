using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmGanaderoEdicion : FormBase
{
    private readonly IServicioGanaderos _servicioGanaderos;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private bool _esEdicion;
    private bool _esAltaRapida;
    private int _id;

    internal Label lblTitulo = null!;
    internal Label lblMarca = null!;
    internal TextBox txtMarca = null!;
    internal Label lblNombre = null!;
    internal TextBox txtNombre = null!;
    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;
    internal ErrorProvider _errorProvider = null!;

    public int? IdGuardado { get; private set; }

    public FrmGanaderoEdicion(IServicioGanaderos servicioGanaderos)
    {
        _servicioGanaderos = servicioGanaderos ?? throw new ArgumentNullException(nameof(servicioGanaderos));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposCatalogo.Marca, txtMarca },
            { CamposCatalogo.Nombre, txtNombre }
        };
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 220);

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(20, 15, 25, 15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lblTitulo = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainLayout.SetColumnSpan(lblTitulo, 2);
        mainLayout.Controls.Add(lblTitulo, 0, 0);

        lblMarca = new Label
        {
            Text = "Marca:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtMarca = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasCatalogos.LongitudMaximaMarca,
            CharacterCasing = CharacterCasing.Upper
        };
        mainLayout.Controls.Add(lblMarca, 0, 1);
        mainLayout.Controls.Add(txtMarca, 1, 1);

        lblNombre = new Label
        {
            Text = "Nombre:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtNombre = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasCatalogos.LongitudMaximaNombre,
            CharacterCasing = CharacterCasing.Upper
        };
        mainLayout.Controls.Add(lblNombre, 0, 2);
        mainLayout.Controls.Add(txtNombre, 1, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        btnCancelar = new Button
        {
            Text = "Cancelar",
            Size = new Size(95, 32),
            DialogResult = DialogResult.Cancel
        };

        btnGuardar = new Button
        {
            Text = "Guardar",
            Size = new Size(95, 32)
        };
        btnGuardar.Click += async (_, _) => await GuardarAsync();

        buttonPanel.Controls.Add(btnCancelar);
        buttonPanel.Controls.Add(btnGuardar);
        mainLayout.SetColumnSpan(buttonPanel, 2);
        mainLayout.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(mainLayout);

        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;

        ResumeLayout(false);
    }

    public void ConfigurarAlta()
    {
        _esEdicion = false;
        _esAltaRapida = false;
        _id = 0;
        Text = "Nuevo Ganadero";
        lblTitulo.Text = "Nuevo Ganadero";
        txtMarca.Text = string.Empty;
        txtNombre.Text = string.Empty;
    }

    public void ConfigurarEdicion(GanaderoListado dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _esEdicion = true;
        _esAltaRapida = false;
        _id = dto.Id;
        Text = "Editar Ganadero";
        lblTitulo.Text = "Editar Ganadero";
        txtMarca.Text = dto.Marca;
        txtNombre.Text = dto.Nombre;
    }

    public void ConfigurarAltaRapida(string textoBuscado)
    {
        ConfigurarAlta();
        _esAltaRapida = true;
        Text = "Alta rápida de ganadero";
        lblTitulo.Text = "Alta rápida de ganadero";
        var t = ValidadorCatalogos.NormalizarMarca(textoBuscado ?? string.Empty);
        if (System.Text.RegularExpressions.Regex.IsMatch(t, ReglasCatalogos.PatronMarca))
        {
            txtMarca.Text = t;
            ActiveControl = txtNombre;
        }
        else
        {
            txtNombre.Text = ValidadorCatalogos.NormalizarMayusculas(textoBuscado ?? string.Empty);
            ActiveControl = txtMarca;
        }
    }

    private Task GuardarAsync()
    {
        return EjecutarAsync(async () =>
        {
            try
            {
                if (_esEdicion)
                {
                    await _servicioGanaderos.EditarAsync(new EditarGanadero(_id, txtMarca.Text, txtNombre.Text));
                    IdGuardado = _id;
                }
                else if (_esAltaRapida)
                {
                    IdGuardado = await _servicioGanaderos.CrearRapidoAsync(new CrearGanadero(txtMarca.Text, txtNombre.Text));
                }
                else
                {
                    IdGuardado = await _servicioGanaderos.CrearAsync(new CrearGanadero(txtMarca.Text, txtNombre.Text));
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
            }
        }, btnGuardar);
    }

    internal TextBox TxtMarca => txtMarca;
    internal TextBox TxtNombre => txtNombre;
    internal Task GuardarParaTestAsync() => GuardarAsync();
}

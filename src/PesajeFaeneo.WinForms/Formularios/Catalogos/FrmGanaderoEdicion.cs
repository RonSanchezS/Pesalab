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
    internal Label lblCodigo = null!;
    internal TextBox txtCodigo = null!;
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
            { CamposCatalogo.Codigo, txtCodigo },
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

        // F10 (O6): el código lo asigna el sistema; se muestra pero no se puede escribir.
        lblCodigo = new Label
        {
            Text = "Código:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtCodigo = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false
        };
        mainLayout.Controls.Add(lblCodigo, 0, 1);
        mainLayout.Controls.Add(txtCodigo, 1, 1);

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

    internal const string TextoCodigoAlta = "(se asigna al guardar)";

    public void ConfigurarAlta()
    {
        _esEdicion = false;
        _esAltaRapida = false;
        _id = 0;
        Text = "Nuevo Ganadero";
        lblTitulo.Text = "Nuevo Ganadero";
        txtCodigo.Text = TextoCodigoAlta;
        txtNombre.Text = string.Empty;
        ActiveControl = txtNombre;
    }

    public void ConfigurarEdicion(GanaderoListado dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _esEdicion = true;
        _esAltaRapida = false;
        _id = dto.Id;
        Text = "Editar Ganadero";
        lblTitulo.Text = "Editar Ganadero";
        txtCodigo.Text = dto.Codigo;
        txtNombre.Text = dto.Nombre;
        ActiveControl = txtNombre;
    }

    public void ConfigurarAltaRapida(string textoBuscado)
    {
        ConfigurarAlta();
        _esAltaRapida = true;
        Text = "Alta rápida de ganadero";
        lblTitulo.Text = "Alta rápida de ganadero";
        // El texto que se estaba buscando pasa a ser el nombre; el código lo asigna el sistema.
        txtNombre.Text = ValidadorCatalogos.NormalizarMayusculas(textoBuscado ?? string.Empty);
        ActiveControl = txtNombre;
    }

    private Task GuardarAsync()
    {
        return EjecutarAsync(async () =>
        {
            try
            {
                if (_esEdicion)
                {
                    await _servicioGanaderos.EditarAsync(new EditarGanadero(_id, txtNombre.Text));
                    IdGuardado = _id;
                }
                else if (_esAltaRapida)
                {
                    IdGuardado = await _servicioGanaderos.CrearRapidoAsync(new CrearGanadero(txtNombre.Text));
                }
                else
                {
                    IdGuardado = await _servicioGanaderos.CrearAsync(new CrearGanadero(txtNombre.Text));
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
            }
        }, btnGuardar);
    }

    internal TextBox TxtCodigo => txtCodigo;
    internal TextBox TxtNombre => txtNombre;
    internal Task GuardarParaTestAsync() => GuardarAsync();
}

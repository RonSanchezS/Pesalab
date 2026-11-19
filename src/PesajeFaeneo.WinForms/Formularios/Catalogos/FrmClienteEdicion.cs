using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmClienteEdicion : FormBase
{
    private readonly IServicioClientes _servicioClientes;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private bool _esEdicion;
    private bool _esAltaRapida;
    private int _id;

    internal Label lblTitulo = null!;
    internal Label lblCodigo = null!;
    internal TextBox txtCodigo = null!;
    internal Label lblNombre = null!;
    internal TextBox txtNombre = null!;
    internal Label lblDireccion = null!;
    internal TextBox txtDireccion = null!;
    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;
    internal ErrorProvider _errorProvider = null!;

    public int? IdGuardado { get; private set; }

    public FrmClienteEdicion(IServicioClientes servicioClientes)
    {
        _servicioClientes = servicioClientes ?? throw new ArgumentNullException(nameof(servicioClientes));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposCatalogo.Codigo, txtCodigo },
            { CamposCatalogo.Nombre, txtNombre },
            { CamposCatalogo.Direccion, txtDireccion }
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
        ClientSize = new Size(480, 260);

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(20, 15, 25, 15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
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

        lblDireccion = new Label
        {
            Text = "Dirección:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtDireccion = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasCatalogos.LongitudMaximaDireccion,
            CharacterCasing = CharacterCasing.Upper
        };
        mainLayout.Controls.Add(lblDireccion, 0, 3);
        mainLayout.Controls.Add(txtDireccion, 1, 3);

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
        mainLayout.Controls.Add(buttonPanel, 0, 4);

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
        Text = "Nuevo Cliente";
        lblTitulo.Text = "Nuevo Cliente";
        txtCodigo.Text = TextoCodigoAlta;
        txtNombre.Text = string.Empty;
        txtDireccion.Text = string.Empty;
        ActiveControl = txtNombre;
    }

    public void ConfigurarEdicion(ClienteListado dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _esEdicion = true;
        _esAltaRapida = false;
        _id = dto.Id;
        Text = "Editar Cliente";
        lblTitulo.Text = "Editar Cliente";
        txtCodigo.Text = dto.Codigo.ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtNombre.Text = dto.Nombre;
        txtDireccion.Text = dto.Direccion;
        ActiveControl = txtNombre;
    }

    public void ConfigurarAltaRapida(string textoBuscado)
    {
        ConfigurarAlta();
        _esAltaRapida = true;
        Text = "Alta rápida de cliente";
        lblTitulo.Text = "Alta rápida de cliente";
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
                    await _servicioClientes.EditarAsync(new EditarCliente(_id, txtNombre.Text, txtDireccion.Text));
                    IdGuardado = _id;
                }
                else if (_esAltaRapida)
                {
                    IdGuardado = await _servicioClientes.CrearRapidoAsync(new CrearCliente(txtNombre.Text, txtDireccion.Text));
                }
                else
                {
                    IdGuardado = await _servicioClientes.CrearAsync(new CrearCliente(txtNombre.Text, txtDireccion.Text));
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
    internal TextBox TxtDireccion => txtDireccion;
    internal Task GuardarParaTestAsync() => GuardarAsync();
}

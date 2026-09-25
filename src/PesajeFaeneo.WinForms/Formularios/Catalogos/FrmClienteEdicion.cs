using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmClienteEdicion : FormBase
{
    private readonly IServicioClientes _servicioClientes;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private bool _esEdicion;
    private int _id;

    internal Label lblTitulo = null!;
    internal Label lblCodigo = null!;
    internal NumericUpDown numCodigo = null!;
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
            { CamposCatalogo.Codigo, numCodigo },
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
        numCodigo = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = ReglasCatalogos.CodigoClienteMinimo,
            Maximum = ReglasCatalogos.CodigoClienteMaximo
        };
        mainLayout.Controls.Add(lblCodigo, 0, 1);
        mainLayout.Controls.Add(numCodigo, 1, 1);

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

    public void ConfigurarAlta(int codigoSugerido)
    {
        _esEdicion = false;
        _id = 0;
        Text = "Nuevo Cliente";
        lblTitulo.Text = "Nuevo Cliente";
        numCodigo.Value = Math.Clamp(codigoSugerido, ReglasCatalogos.CodigoClienteMinimo, ReglasCatalogos.CodigoClienteMaximo);
        txtNombre.Text = string.Empty;
        txtDireccion.Text = string.Empty;
    }

    public void ConfigurarEdicion(ClienteListado dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _esEdicion = true;
        _id = dto.Id;
        Text = "Editar Cliente";
        lblTitulo.Text = "Editar Cliente";
        numCodigo.Value = Math.Clamp(dto.Codigo, ReglasCatalogos.CodigoClienteMinimo, ReglasCatalogos.CodigoClienteMaximo);
        txtNombre.Text = dto.Nombre;
        txtDireccion.Text = dto.Direccion;
    }

    private Task GuardarAsync()
    {
        return EjecutarAsync(async () =>
        {
            try
            {
                if (_esEdicion)
                {
                    await _servicioClientes.EditarAsync(new EditarCliente(_id, (int)numCodigo.Value, txtNombre.Text, txtDireccion.Text));
                    IdGuardado = _id;
                }
                else
                {
                    IdGuardado = await _servicioClientes.CrearAsync(new CrearCliente((int)numCodigo.Value, txtNombre.Text, txtDireccion.Text));
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
            }
        }, btnGuardar);
    }

    internal NumericUpDown NumCodigo => numCodigo;
    internal TextBox TxtNombre => txtNombre;
    internal TextBox TxtDireccion => txtDireccion;
    internal Task GuardarParaTestAsync() => GuardarAsync();
}

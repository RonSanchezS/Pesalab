using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmFleteroEdicion : FormBase
{
    private readonly IServicioFleteros _servicioFleteros;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private bool _esEdicion;
    private int _id;

    internal Label lblTitulo = null!;
    internal Label lblOrden = null!;
    internal ComboBox cmbOrden = null!;
    internal Label lblNombre = null!;
    internal TextBox txtNombre = null!;
    internal Label lblCelular = null!;
    internal TextBox txtCelular = null!;
    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;
    internal ErrorProvider _errorProvider = null!;

    public int? IdGuardado { get; private set; }

    public sealed record ItemOrdenFletero(int Orden, string Texto)
    {
        public override string ToString() => Texto;
    }

    public FrmFleteroEdicion(IServicioFleteros servicioFleteros)
    {
        _servicioFleteros = servicioFleteros ?? throw new ArgumentNullException(nameof(servicioFleteros));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposCatalogo.Orden, cmbOrden },
            { CamposCatalogo.Nombre, txtNombre },
            { CamposCatalogo.Celular, txtCelular }
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

        lblOrden = new Label
        {
            Text = "Orden:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        cmbOrden = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var opcionesOrden = new List<ItemOrdenFletero>
        {
            new(1, "F1"),
            new(2, "F2"),
            new(3, "F3"),
            new(4, "F4")
        };
        cmbOrden.DisplayMember = nameof(ItemOrdenFletero.Texto);
        cmbOrden.ValueMember = nameof(ItemOrdenFletero.Orden);
        cmbOrden.DataSource = opcionesOrden;

        mainLayout.Controls.Add(lblOrden, 0, 1);
        mainLayout.Controls.Add(cmbOrden, 1, 1);

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

        lblCelular = new Label
        {
            Text = "Celular:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtCelular = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = ReglasCatalogos.LongitudMaximaCelular
        };
        mainLayout.Controls.Add(lblCelular, 0, 3);
        mainLayout.Controls.Add(txtCelular, 1, 3);

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

    public void ConfigurarAlta(int ordenSugerido)
    {
        _esEdicion = false;
        _id = 0;
        Text = "Nuevo Fletero";
        lblTitulo.Text = "Nuevo Fletero";
        var ordenValido = Math.Clamp(ordenSugerido, ReglasCatalogos.OrdenFleteroMinimo, ReglasCatalogos.OrdenFleteroMaximo);
        cmbOrden.SelectedValue = ordenValido;
        txtNombre.Text = string.Empty;
        txtCelular.Text = string.Empty;
    }

    public void ConfigurarEdicion(FleteroListado dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _esEdicion = true;
        _id = dto.Id;
        Text = "Editar Fletero";
        lblTitulo.Text = "Editar Fletero";
        var ordenValido = Math.Clamp(dto.Orden, ReglasCatalogos.OrdenFleteroMinimo, ReglasCatalogos.OrdenFleteroMaximo);
        cmbOrden.SelectedValue = ordenValido;
        txtNombre.Text = dto.Nombre;
        txtCelular.Text = dto.Celular ?? string.Empty;
    }

    private Task GuardarAsync()
    {
        return EjecutarAsync(async () =>
        {
            try
            {
                var orden = cmbOrden.SelectedValue is int o ? o : 1;
                var celular = txtCelular.Text?.Trim() ?? string.Empty;

                if (_esEdicion)
                {
                    await _servicioFleteros.EditarAsync(new EditarFletero(_id, orden, txtNombre.Text, celular));
                    IdGuardado = _id;
                }
                else
                {
                    IdGuardado = await _servicioFleteros.CrearAsync(new CrearFletero(orden, txtNombre.Text, celular));
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
            }
        }, btnGuardar);
    }

    internal ComboBox CmbOrden => cmbOrden;
    internal TextBox TxtNombre => txtNombre;
    internal TextBox TxtCelular => txtCelular;
    internal Task GuardarParaTestAsync() => GuardarAsync();
}

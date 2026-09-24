using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.WinForms.Formularios.Seguridad;

public class FrmUsuarioEdicion : FormBase
{
    private readonly IServicioUsuarios _servicioUsuarios;
    private readonly ISesionActual _sesionActual;
    private readonly Dictionary<string, Control> _controlesPorCampo;

    private bool _esEdicion;
    private int _usuarioId;

    internal Label lblTitulo = null!;
    internal Label lblNombreUsuario = null!;
    internal TextBox txtNombreUsuario = null!;
    internal Label lblNombreCompleto = null!;
    internal TextBox txtNombreCompleto = null!;
    internal Label lblRol = null!;
    internal ComboBox cmbRol = null!;
    internal Label lblPassword = null!;
    internal TextBox txtPassword = null!;
    internal Label lblConfirmacion = null!;
    internal TextBox txtConfirmacion = null!;
    internal Button btnGuardar = null!;
    internal Button btnCancelar = null!;
    internal ErrorProvider _errorProvider = null!;

    private record ItemRol(Rol Rol, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    public FrmUsuarioEdicion(IServicioUsuarios servicioUsuarios, ISesionActual sesionActual)
    {
        _servicioUsuarios = servicioUsuarios ?? throw new ArgumentNullException(nameof(servicioUsuarios));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));

        InitializeComponent();

        _controlesPorCampo = new Dictionary<string, Control>
        {
            { CamposUsuario.NombreUsuario, txtNombreUsuario },
            { CamposUsuario.NombreCompleto, txtNombreCompleto },
            { CamposUsuario.Rol, cmbRol },
            { CamposUsuario.Password, txtPassword },
            { CamposUsuario.Confirmacion, txtConfirmacion }
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
        ClientSize = new Size(480, 340);

        _errorProvider = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 15, 25, 15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
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

        lblNombreUsuario = new Label
        {
            Text = "Nombre de usuario:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtNombreUsuario = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = 30
        };
        txtNombreUsuario.Leave += (_, _) =>
        {
            if (!_esEdicion)
            {
                txtNombreUsuario.Text = txtNombreUsuario.Text.Trim().ToLowerInvariant();
            }
        };
        mainLayout.Controls.Add(lblNombreUsuario, 0, 1);
        mainLayout.Controls.Add(txtNombreUsuario, 1, 1);

        lblNombreCompleto = new Label
        {
            Text = "Nombre completo:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtNombreCompleto = new TextBox
        {
            Dock = DockStyle.Fill,
            MaxLength = 100
        };
        mainLayout.Controls.Add(lblNombreCompleto, 0, 2);
        mainLayout.Controls.Add(txtNombreCompleto, 1, 2);

        lblRol = new Label
        {
            Text = "Rol:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        cmbRol = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbRol.Items.Add(new ItemRol(Rol.Administrador, "Administrador"));
        cmbRol.Items.Add(new ItemRol(Rol.Supervisor, "Supervisor"));
        cmbRol.Items.Add(new ItemRol(Rol.Operador, "Operador"));
        cmbRol.SelectedIndex = 2; // Operador por defecto
        mainLayout.Controls.Add(lblRol, 0, 3);
        mainLayout.Controls.Add(cmbRol, 1, 3);

        lblPassword = new Label
        {
            Text = "Contraseña inicial:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtPassword = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            MaxLength = 100
        };
        mainLayout.Controls.Add(lblPassword, 0, 4);
        mainLayout.Controls.Add(txtPassword, 1, 4);

        lblConfirmacion = new Label
        {
            Text = "Confirmación:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        txtConfirmacion = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            MaxLength = 100
        };
        mainLayout.Controls.Add(lblConfirmacion, 0, 5);
        mainLayout.Controls.Add(txtConfirmacion, 1, 5);

        var pnlBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        btnCancelar = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 35)
        };
        btnCancelar.Click += (_, _) => Close();

        btnGuardar = new Button
        {
            Text = "Guardar",
            Size = new Size(100, 35)
        };
        btnGuardar.Click += btnGuardar_Click;

        pnlBotones.Controls.Add(btnCancelar);
        pnlBotones.Controls.Add(btnGuardar);

        mainLayout.SetColumnSpan(pnlBotones, 2);
        mainLayout.Controls.Add(pnlBotones, 0, 6);

        Controls.Add(mainLayout);

        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;

        ResumeLayout(false);
    }

    public void ConfigurarAlta()
    {
        _esEdicion = false;
        _usuarioId = 0;
        Text = "Nuevo usuario";
        lblTitulo.Text = "Registrar nuevo usuario";

        txtNombreUsuario.Text = string.Empty;
        txtNombreUsuario.ReadOnly = false;

        txtNombreCompleto.Text = string.Empty;

        lblPassword.Visible = true;
        txtPassword.Visible = true;
        txtPassword.Text = string.Empty;

        lblConfirmacion.Visible = true;
        txtConfirmacion.Visible = true;
        txtConfirmacion.Text = string.Empty;

        cmbRol.Enabled = true;
        cmbRol.SelectedIndex = 2; // Operador
    }

    public void ConfigurarEdicion(UsuarioListado usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        _esEdicion = true;
        _usuarioId = usuario.Id;
        Text = "Editar usuario";
        lblTitulo.Text = $"Editar usuario: {usuario.NombreUsuario}";

        txtNombreUsuario.Text = usuario.NombreUsuario;
        txtNombreUsuario.ReadOnly = true;

        txtNombreCompleto.Text = usuario.NombreCompleto;

        // Seleccionar rol
        for (int i = 0; i < cmbRol.Items.Count; i++)
        {
            if (cmbRol.Items[i] is ItemRol item && item.Rol == usuario.Rol)
            {
                cmbRol.SelectedIndex = i;
                break;
            }
        }

        // Si edita su propio registro, cmbRol queda deshabilitado
        if (_sesionActual.Usuario?.Id == usuario.Id)
        {
            cmbRol.Enabled = false;
        }
        else
        {
            cmbRol.Enabled = true;
        }

        // Contraseñas ocultas en edición
        lblPassword.Visible = false;
        txtPassword.Visible = false;
        lblConfirmacion.Visible = false;
        txtConfirmacion.Visible = false;
    }

    private async void btnGuardar_Click(object? sender, EventArgs e)
    {
        await EjecutarAsync(async () =>
        {
            var itemRol = cmbRol.SelectedItem as ItemRol;
            var rol = itemRol?.Rol ?? Rol.Operador;

            try
            {
                if (_esEdicion)
                {
                    await _servicioUsuarios.EditarAsync(new EditarUsuario(
                        _usuarioId,
                        txtNombreCompleto.Text,
                        rol));
                }
                else
                {
                    await _servicioUsuarios.CrearAsync(new CrearUsuario(
                        txtNombreUsuario.Text,
                        txtNombreCompleto.Text,
                        rol,
                        txtPassword.Text,
                        txtConfirmacion.Text));
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) when (MostrarErroresValidacion(ex, _errorProvider, _controlesPorCampo))
            {
                // Mostrado en ErrorProvider
            }
        }, btnGuardar);
    }
}

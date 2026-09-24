using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Seguridad;

public class FrmUsuarios : FormBase
{
    private readonly IServicioUsuarios _servicioUsuarios;
    private readonly IFormFactory _formFactory;
    private readonly TimeProvider _timeProvider;

    private DataGridView dgvUsuarios = null!;
    private CheckBox chkMostrarInactivos = null!;
    private Button btnNuevo = null!;
    private Button btnEditar = null!;
    private Button btnActivarDesactivar = null!;
    private Button btnRestablecer = null!;
    private Button btnCerrar = null!;

    private sealed record UsuarioFila(
        int Id,
        string NombreUsuario,
        string NombreCompleto,
        Rol Rol,
        bool Activo,
        bool Bloqueado,
        string UltimoAccesoTexto)
    {
        public string RolTexto => Rol.ToString();
        public string ActivoTexto => Activo ? "Sí" : "No";
        public string BloqueadoTexto => Bloqueado ? "Sí" : "No";
    }

    public FrmUsuarios(
        IServicioUsuarios servicioUsuarios,
        IFormFactory formFactory,
        TimeProvider timeProvider)
    {
        _servicioUsuarios = servicioUsuarios ?? throw new ArgumentNullException(nameof(servicioUsuarios));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        InitializeComponent();

        Shown += async (_, _) => await CargarUsuariosAsync();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Gestión de Usuarios";
        ClientSize = new Size(880, 520);
        MinimumSize = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(15)
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        chkMostrarInactivos = new CheckBox
        {
            Text = "Mostrar usuarios inactivos",
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        chkMostrarInactivos.CheckedChanged += async (_, _) => await CargarUsuariosAsync();
        mainLayout.Controls.Add(chkMostrarInactivos, 0, 0);

        dgvUsuarios = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            RowHeadersVisible = false
        };

        dgvUsuarios.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColUsuario",
            HeaderText = "Usuario",
            DataPropertyName = nameof(UsuarioFila.NombreUsuario),
            Width = 110
        });

        dgvUsuarios.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNombre",
            HeaderText = "Nombre completo",
            DataPropertyName = nameof(UsuarioFila.NombreCompleto),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        dgvUsuarios.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColRol",
            HeaderText = "Rol",
            DataPropertyName = nameof(UsuarioFila.RolTexto),
            Width = 110
        });

        dgvUsuarios.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColActivo",
            HeaderText = "Activo",
            DataPropertyName = nameof(UsuarioFila.ActivoTexto),
            Width = 70
        });

        dgvUsuarios.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColBloqueado",
            HeaderText = "Bloqueado",
            DataPropertyName = nameof(UsuarioFila.BloqueadoTexto),
            Width = 85
        });

        dgvUsuarios.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColUltimoAcceso",
            HeaderText = "Último acceso",
            DataPropertyName = nameof(UsuarioFila.UltimoAccesoTexto),
            Width = 140
        });

        dgvUsuarios.SelectionChanged += (_, _) => ActualizarEstadoBotones();
        dgvUsuarios.CellDoubleClick += async (_, _) => await EditarSeleccionadoAsync();
        dgvUsuarios.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await EditarSeleccionadoAsync();
            }
        };

        mainLayout.Controls.Add(dgvUsuarios, 0, 1);

        var pnlBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10, 0, 0, 0)
        };

        btnNuevo = new Button { Text = "Nuevo", Size = new Size(130, 35) };
        btnNuevo.Click += async (_, _) => await NuevoUsuarioAsync();

        btnEditar = new Button { Text = "Editar", Size = new Size(130, 35), Enabled = false };
        btnEditar.Click += async (_, _) => await EditarSeleccionadoAsync();

        btnActivarDesactivar = new Button { Text = "Desactivar", Size = new Size(130, 35), Enabled = false };
        btnActivarDesactivar.Click += async (_, _) => await CambiarEstadoSeleccionadoAsync();

        btnRestablecer = new Button { Text = "Restablecer clave", Size = new Size(130, 35), Enabled = false };
        btnRestablecer.Click += async (_, _) => await RestablecerPasswordSeleccionadoAsync();

        btnCerrar = new Button { Text = "Cerrar", Size = new Size(130, 35) };
        btnCerrar.Click += (_, _) => Close();

        pnlBotones.Controls.Add(btnNuevo);
        pnlBotones.Controls.Add(btnEditar);
        pnlBotones.Controls.Add(btnActivarDesactivar);
        pnlBotones.Controls.Add(btnRestablecer);
        pnlBotones.Controls.Add(btnCerrar);

        mainLayout.Controls.Add(pnlBotones, 1, 1);

        Controls.Add(mainLayout);

        CancelButton = btnCerrar;

        ResumeLayout(false);
    }

    private UsuarioFila? FilaSeleccionada
    {
        get
        {
            if (dgvUsuarios.SelectedRows.Count == 0) return null;
            return dgvUsuarios.SelectedRows[0].DataBoundItem as UsuarioFila;
        }
    }

    private void ActualizarEstadoBotones()
    {
        var seleccionado = FilaSeleccionada;
        if (seleccionado == null)
        {
            btnEditar.Enabled = false;
            btnActivarDesactivar.Enabled = false;
            btnRestablecer.Enabled = false;
            btnActivarDesactivar.Text = "Desactivar";
            return;
        }

        btnEditar.Enabled = true;
        btnRestablecer.Enabled = true;
        btnActivarDesactivar.Enabled = true;
        btnActivarDesactivar.Text = seleccionado.Activo ? "Desactivar" : "Activar";
    }

    private async Task CargarUsuariosAsync(int? idASeleccionar = null)
    {
        await EjecutarAsync(async () =>
        {
            var incluirInactivos = chkMostrarInactivos.Checked;
            var usuarios = await _servicioUsuarios.ListarAsync(incluirInactivos);

            var idActual = idASeleccionar ?? FilaSeleccionada?.Id;

            var listaFilas = usuarios.Select(u =>
            {
                string ultimoAccesoTexto = "—";
                if (u.UltimoAcceso.HasValue)
                {
                    var local = TimeZoneInfo.ConvertTimeFromUtc(u.UltimoAcceso.Value, _timeProvider.LocalTimeZone);
                    ultimoAccesoTexto = local.ToString("dd/MM/yyyy HH:mm");
                }

                return new UsuarioFila(
                    u.Id,
                    u.NombreUsuario,
                    u.NombreCompleto,
                    u.Rol,
                    u.Activo,
                    u.Bloqueado,
                    ultimoAccesoTexto);
            }).ToList();

            dgvUsuarios.DataSource = listaFilas;

            if (idActual.HasValue)
            {
                foreach (DataGridViewRow row in dgvUsuarios.Rows)
                {
                    if (row.DataBoundItem is UsuarioFila fila && fila.Id == idActual.Value)
                    {
                        row.Selected = true;
                        dgvUsuarios.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            ActualizarEstadoBotones();
        });
    }

    private async Task NuevoUsuarioAsync()
    {
        using var frm = _formFactory.Crear<FrmUsuarioEdicion>();
        frm.ConfigurarAlta();
        if (frm.ShowDialog(this) == DialogResult.OK)
        {
            await CargarUsuariosAsync();
        }
    }

    private async Task EditarSeleccionadoAsync()
    {
        var fila = FilaSeleccionada;
        if (fila == null) return;

        await EjecutarAsync(async () =>
        {
            var usuario = await _servicioUsuarios.ObtenerAsync(fila.Id);
            using var frm = _formFactory.Crear<FrmUsuarioEdicion>();
            frm.ConfigurarEdicion(usuario);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                await CargarUsuariosAsync(fila.Id);
            }
        });
    }

    private async Task CambiarEstadoSeleccionadoAsync()
    {
        var fila = FilaSeleccionada;
        if (fila == null) return;

        var accion = fila.Activo ? "desactivar" : "activar";
        var confirmacion = MessageBox.Show(this,
            $"¿Está seguro de que desea {accion} al usuario '{fila.NombreUsuario}'?",
            $"Confirmar {accion}",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacion != DialogResult.Yes) return;

        await EjecutarAsync(async () =>
        {
            await _servicioUsuarios.CambiarEstadoAsync(fila.Id, !fila.Activo);
            await CargarUsuariosAsync(fila.Id);
        });
    }

    private async Task RestablecerPasswordSeleccionadoAsync()
    {
        var fila = FilaSeleccionada;
        if (fila == null) return;

        var confirmacion = MessageBox.Show(this,
            $"¿Desea restablecer la contraseña del usuario '{fila.NombreUsuario}'?",
            "Confirmar restablecimiento",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacion != DialogResult.Yes) return;

        using var frm = _formFactory.Crear<FrmRestablecerPassword>();
        frm.UsuarioId = fila.Id;
        frm.NombreUsuario = fila.NombreUsuario;
        if (frm.ShowDialog(this) == DialogResult.OK)
        {
            await CargarUsuariosAsync(fila.Id);
        }
    }
}

using System.Globalization;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmClientes : FormBase
{
    private readonly IServicioClientes _servicioClientes;
    private readonly IFormFactory _formFactory;

    private DataGridView dgvClientes = null!;
    private ControlBusquedaCatalogo busqueda = null!;
    private CheckBox chkMostrarInactivos = null!;
    private Button btnNuevo = null!;
    private Button btnEditar = null!;
    private Button btnActivarDesactivar = null!;
    private Button btnCerrar = null!;

    private sealed record ClienteFila(
        int Id,
        int Codigo,
        string Nombre,
        string Direccion,
        bool Activo)
    {
        public string ActivoTexto => Activo ? "Sí" : "No";
    }

    public FrmClientes(IServicioClientes servicioClientes, IFormFactory formFactory)
    {
        _servicioClientes = servicioClientes ?? throw new ArgumentNullException(nameof(servicioClientes));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));

        InitializeComponent();

        Shown += async (_, _) => await EjecutarAsync(() => CargarAsync());
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Clientes";
        ClientSize = new Size(900, 520);
        MinimumSize = new Size(750, 400);
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var lblBuscar = new Label
        {
            Text = "Buscar:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        topPanel.Controls.Add(lblBuscar, 0, 0);

        busqueda = new ControlBusquedaCatalogo
        {
            Dock = DockStyle.Fill,
            Placeholder = "Buscar por código, nombre o dirección..."
        };
        busqueda.ElementoSeleccionado += (_, e) =>
        {
            SeleccionarFila(e.Id);
        };
        topPanel.Controls.Add(busqueda, 1, 0);

        chkMostrarInactivos = new CheckBox
        {
            Text = "Mostrar inactivos",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 0, 0, 0)
        };
        chkMostrarInactivos.CheckedChanged += async (_, _) => await EjecutarAsync(() => CargarAsync(), chkMostrarInactivos);
        topPanel.Controls.Add(chkMostrarInactivos, 2, 0);

        mainLayout.Controls.Add(topPanel, 0, 0);

        dgvClientes = new DataGridView
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

        dgvClientes.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCodigo",
            HeaderText = "Código",
            DataPropertyName = nameof(ClienteFila.Codigo),
            Width = 80
        });

        dgvClientes.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNombre",
            HeaderText = "Nombre",
            DataPropertyName = nameof(ClienteFila.Nombre),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        dgvClientes.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDireccion",
            HeaderText = "Dirección",
            DataPropertyName = nameof(ClienteFila.Direccion),
            Width = 250
        });

        dgvClientes.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColActivo",
            HeaderText = "Activo",
            DataPropertyName = nameof(ClienteFila.ActivoTexto),
            Width = 70
        });

        dgvClientes.SelectionChanged += (_, _) => ActualizarEstadoBotones();
        dgvClientes.DoubleClick += async (_, _) => await EditarSeleccionadoAsync();
        dgvClientes.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                await EditarSeleccionadoAsync();
            }
        };

        mainLayout.Controls.Add(dgvClientes, 0, 1);

        var btnPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10, 0, 0, 0)
        };

        btnPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        btnPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        btnPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        btnPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        btnPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        btnNuevo = new Button
        {
            Text = "Nuevo",
            Dock = DockStyle.Fill
        };
        btnNuevo.Click += async (_, _) => await NuevoClienteAsync();
        btnPanel.Controls.Add(btnNuevo, 0, 0);

        btnEditar = new Button
        {
            Text = "Editar",
            Dock = DockStyle.Fill,
            Enabled = false
        };
        btnEditar.Click += async (_, _) => await EditarSeleccionadoAsync();
        btnPanel.Controls.Add(btnEditar, 0, 1);

        btnActivarDesactivar = new Button
        {
            Text = "Desactivar",
            Dock = DockStyle.Fill,
            Enabled = false
        };
        btnActivarDesactivar.Click += async (_, _) => await ActivarDesactivarSeleccionadoAsync();
        btnPanel.Controls.Add(btnActivarDesactivar, 0, 2);

        btnCerrar = new Button
        {
            Text = "Cerrar",
            Dock = DockStyle.Fill,
            DialogResult = DialogResult.Cancel
        };
        btnCerrar.Click += (_, _) => Close();
        btnPanel.Controls.Add(btnCerrar, 0, 4);

        mainLayout.Controls.Add(btnPanel, 1, 1);

        Controls.Add(mainLayout);
        CancelButton = btnCerrar;

        ResumeLayout(false);
    }

    private ClienteFila? FilaSeleccionada
    {
        get
        {
            if (dgvClientes.CurrentRow?.DataBoundItem is ClienteFila fila)
            {
                return fila;
            }
            return null;
        }
    }

    private void ActualizarEstadoBotones()
    {
        var seleccionado = FilaSeleccionada;
        var haySeleccion = seleccionado != null;

        btnEditar.Enabled = haySeleccion;
        btnActivarDesactivar.Enabled = haySeleccion;

        if (!haySeleccion)
        {
            btnActivarDesactivar.Text = "Desactivar";
            return;
        }

        btnActivarDesactivar.Text = seleccionado!.Activo ? "Desactivar" : "Activar";
    }

    private async Task CargarAsync(int? idASeleccionar = null)
    {
        var incluirInactivos = chkMostrarInactivos.Checked;
        var clientes = await _servicioClientes.ListarAsync(incluirInactivos);

        var idActual = idASeleccionar ?? FilaSeleccionada?.Id;

        var listaFilas = clientes.Select(c => new ClienteFila(
            c.Id,
            c.Codigo,
            c.Nombre,
            c.Direccion,
            c.Activo)).ToList();

        dgvClientes.DataSource = listaFilas;

        var elementosBusqueda = clientes
            .Where(c => c.Activo)
            .Select(c => new ElementoBusqueda(c.Id, c.Codigo.ToString(CultureInfo.InvariantCulture), c.Nombre, c.Direccion, c.Activo))
            .ToList();
        busqueda.EstablecerElementos(elementosBusqueda);

        if (idActual.HasValue)
        {
            SeleccionarFila(idActual.Value);
        }

        ActualizarEstadoBotones();
    }

    private void SeleccionarFila(int id)
    {
        foreach (DataGridViewRow row in dgvClientes.Rows)
        {
            if (row.DataBoundItem is ClienteFila fila && fila.Id == id)
            {
                row.Selected = true;
                dgvClientes.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async Task NuevoClienteAsync()
    {
        await EjecutarAsync(async () =>
        {
            using var frm = _formFactory.Crear<FrmClienteEdicion>();
            frm.ConfigurarAlta();
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                await CargarAsync(frm.IdGuardado);
            }
        }, btnNuevo);
    }

    private async Task EditarSeleccionadoAsync()
    {
        var fila = FilaSeleccionada;
        if (fila == null) return;

        await EjecutarAsync(async () =>
        {
            var dto = await _servicioClientes.ObtenerAsync(fila.Id);
            using var frm = _formFactory.Crear<FrmClienteEdicion>();
            frm.ConfigurarEdicion(dto);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                await CargarAsync(fila.Id);
            }
        }, btnEditar);
    }

    private async Task ActivarDesactivarSeleccionadoAsync()
    {
        var fila = FilaSeleccionada;
        if (fila == null) return;

        var accion = fila.Activo ? "desactivar" : "activar";
        var confirmacion = MessageBox.Show(
            this,
            $"¿Está seguro de que desea {accion} al cliente '{fila.Codigo} — {fila.Nombre}'?",
            "Confirmación",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacion != DialogResult.Yes) return;

        await CambiarEstadoAsync(fila);
    }

    private Task CambiarEstadoAsync(ClienteFila fila)
    {
        return EjecutarAsync(async () =>
        {
            await _servicioClientes.CambiarEstadoAsync(fila.Id, !fila.Activo);
            await CargarAsync(fila.Id);
        }, btnActivarDesactivar);
    }

    internal DataGridView GrillaParaTest => dgvClientes;
    internal ControlBusquedaCatalogo BusquedaParaTest => busqueda;
    internal Task CargarParaTestAsync() => CargarAsync();
    internal bool MostrarInactivosParaTest
    {
        set => chkMostrarInactivos.Checked = value;
    }
    internal void SeleccionarFilaParaTest(int id) => SeleccionarFila(id);
    internal Task CambiarEstadoSeleccionadoParaTestAsync()
    {
        var fila = FilaSeleccionada;
        return fila == null ? Task.CompletedTask : CambiarEstadoAsync(fila);
    }
}

using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmGanaderos : FormBase
{
    private readonly IServicioGanaderos _servicioGanaderos;
    private readonly IFormFactory _formFactory;

    private DataGridView dgvGanaderos = null!;
    private ControlBusquedaCatalogo busqueda = null!;
    private CheckBox chkMostrarInactivos = null!;
    private Button btnNuevo = null!;
    private Button btnEditar = null!;
    private Button btnActivarDesactivar = null!;
    private Button btnCerrar = null!;

    private sealed record GanaderoFila(
        int Id,
        string Codigo,
        string Nombre,
        bool Activo)
    {
        public string ActivoTexto => Activo ? "Sí" : "No";
    }

    public FrmGanaderos(IServicioGanaderos servicioGanaderos, IFormFactory formFactory)
    {
        _servicioGanaderos = servicioGanaderos ?? throw new ArgumentNullException(nameof(servicioGanaderos));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));

        InitializeComponent();

        Shown += async (_, _) => await EjecutarAsync(() => CargarAsync());
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Ganaderos";
        ClientSize = new Size(840, 500);
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
            Placeholder = "Buscar por código o nombre..."
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

        dgvGanaderos = new DataGridView
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

        dgvGanaderos.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCodigo",
            HeaderText = "Código",
            DataPropertyName = nameof(GanaderoFila.Codigo),
            Width = 80
        });

        dgvGanaderos.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNombre",
            HeaderText = "Nombre",
            DataPropertyName = nameof(GanaderoFila.Nombre),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        dgvGanaderos.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColActivo",
            HeaderText = "Activo",
            DataPropertyName = nameof(GanaderoFila.ActivoTexto),
            Width = 70
        });

        dgvGanaderos.SelectionChanged += (_, _) => ActualizarEstadoBotones();
        dgvGanaderos.DoubleClick += async (_, _) => await EditarSeleccionadoAsync();
        dgvGanaderos.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                await EditarSeleccionadoAsync();
            }
        };

        mainLayout.Controls.Add(dgvGanaderos, 0, 1);

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
        btnNuevo.Click += async (_, _) => await NuevoGanaderoAsync();
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

    private GanaderoFila? FilaSeleccionada
    {
        get
        {
            if (dgvGanaderos.CurrentRow?.DataBoundItem is GanaderoFila fila)
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
        var ganaderos = await _servicioGanaderos.ListarAsync(incluirInactivos);

        var idActual = idASeleccionar ?? FilaSeleccionada?.Id;

        var listaFilas = ganaderos.Select(g => new GanaderoFila(
            g.Id,
            g.Codigo,
            g.Nombre,
            g.Activo)).ToList();

        dgvGanaderos.DataSource = listaFilas;

        var elementosBusqueda = ganaderos
            .Where(g => g.Activo)
            .Select(g => new ElementoBusqueda(g.Id, g.Codigo, g.Nombre, null, g.Activo))
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
        foreach (DataGridViewRow row in dgvGanaderos.Rows)
        {
            if (row.DataBoundItem is GanaderoFila fila && fila.Id == id)
            {
                row.Selected = true;
                dgvGanaderos.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async Task NuevoGanaderoAsync()
    {
        await EjecutarAsync(async () =>
        {
            using var frm = _formFactory.Crear<FrmGanaderoEdicion>();
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
            var dto = await _servicioGanaderos.ObtenerAsync(fila.Id);
            using var frm = _formFactory.Crear<FrmGanaderoEdicion>();
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
            $"¿Está seguro de que desea {accion} al ganadero '{fila.Codigo} — {fila.Nombre}'?",
            "Confirmación",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacion != DialogResult.Yes) return;

        await CambiarEstadoAsync(fila);
    }

    private Task CambiarEstadoAsync(GanaderoFila fila)
    {
        return EjecutarAsync(async () =>
        {
            await _servicioGanaderos.CambiarEstadoAsync(fila.Id, !fila.Activo);
            await CargarAsync(fila.Id);
        }, btnActivarDesactivar);
    }

    internal DataGridView GrillaParaTest => dgvGanaderos;
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

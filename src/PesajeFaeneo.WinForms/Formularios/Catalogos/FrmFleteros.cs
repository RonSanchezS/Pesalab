using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms.Formularios.Catalogos;

public class FrmFleteros : FormBase
{
    private readonly IServicioFleteros _servicioFleteros;
    private readonly IFormFactory _formFactory;

    private DataGridView dgvFleteros = null!;
    private CheckBox chkMostrarInactivos = null!;
    private Button btnNuevo = null!;
    private Button btnEditar = null!;
    private Button btnActivarDesactivar = null!;
    private Button btnCerrar = null!;

    private sealed record FleteroFila(
        int Id,
        int Orden,
        string Nombre,
        string? Celular,
        bool Activo)
    {
        public string OrdenTexto => Activo ? $"F{Orden}" : "—";
        public string CelularTexto => Celular ?? string.Empty;
        public string ActivoTexto => Activo ? "Sí" : "No";
    }

    public FrmFleteros(IServicioFleteros servicioFleteros, IFormFactory formFactory)
    {
        _servicioFleteros = servicioFleteros ?? throw new ArgumentNullException(nameof(servicioFleteros));
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));

        InitializeComponent();

        Shown += async (_, _) => await EjecutarAsync(() => CargarAsync());
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Fleteros";
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
            ColumnCount = 2,
            RowCount = 1
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        chkMostrarInactivos = new CheckBox
        {
            Text = "Mostrar inactivos",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(5, 0, 0, 0)
        };
        chkMostrarInactivos.CheckedChanged += async (_, _) => await EjecutarAsync(() => CargarAsync(), chkMostrarInactivos);
        topPanel.Controls.Add(chkMostrarInactivos, 1, 0);

        mainLayout.Controls.Add(topPanel, 0, 0);

        dgvFleteros = new DataGridView
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

        dgvFleteros.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColOrden",
            HeaderText = "Orden",
            DataPropertyName = nameof(FleteroFila.OrdenTexto),
            Width = 80
        });

        dgvFleteros.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNombre",
            HeaderText = "Nombre",
            DataPropertyName = nameof(FleteroFila.Nombre),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        dgvFleteros.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCelular",
            HeaderText = "Celular",
            DataPropertyName = nameof(FleteroFila.CelularTexto),
            Width = 140
        });

        dgvFleteros.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColActivo",
            HeaderText = "Activo",
            DataPropertyName = nameof(FleteroFila.ActivoTexto),
            Width = 70
        });

        dgvFleteros.SelectionChanged += (_, _) => ActualizarEstadoBotones();
        dgvFleteros.DoubleClick += async (_, _) => await EditarSeleccionadoAsync();
        dgvFleteros.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                await EditarSeleccionadoAsync();
            }
        };

        mainLayout.Controls.Add(dgvFleteros, 0, 1);

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
        btnNuevo.Click += async (_, _) => await NuevoFleteroAsync();
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

    private FleteroFila? FilaSeleccionada
    {
        get
        {
            if (dgvFleteros.CurrentRow?.DataBoundItem is FleteroFila fila)
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
        var fleteros = await _servicioFleteros.ListarAsync(incluirInactivos);

        var idActual = idASeleccionar ?? FilaSeleccionada?.Id;

        var listaFilas = fleteros.Select(f => new FleteroFila(
            f.Id,
            f.Orden,
            f.Nombre,
            f.Celular,
            f.Activo)).ToList();

        dgvFleteros.DataSource = listaFilas;

        if (idActual.HasValue)
        {
            SeleccionarFila(idActual.Value);
        }

        ActualizarEstadoBotones();
    }

    private void SeleccionarFila(int id)
    {
        foreach (DataGridViewRow row in dgvFleteros.Rows)
        {
            if (row.DataBoundItem is FleteroFila fila && fila.Id == id)
            {
                row.Selected = true;
                dgvFleteros.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async Task NuevoFleteroAsync()
    {
        await EjecutarAsync(async () =>
        {
            var fleterosActivos = (await _servicioFleteros.ListarAsync(incluirInactivos: false)).ToList();
            var ordenesUsadas = fleterosActivos.Select(f => f.Orden).ToHashSet();
            var primerOrdenLibre = Enumerable.Range(1, 4).FirstOrDefault(o => !ordenesUsadas.Contains(o));

            if (primerOrdenLibre == 0 || fleterosActivos.Count >= 4)
            {
                MessageBox.Show(
                    this,
                    "Ya hay 4 fleteros activos. Desactive uno antes de agregar otro.",
                    "Límite alcanzado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using var frm = _formFactory.Crear<FrmFleteroEdicion>();
            frm.ConfigurarAlta(primerOrdenLibre);
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
            var dto = await _servicioFleteros.ObtenerAsync(fila.Id);
            using var frm = _formFactory.Crear<FrmFleteroEdicion>();
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
        var identificador = fila.Activo ? $"F{fila.Orden}" : "—";
        var confirmacion = MessageBox.Show(
            this,
            $"¿Está seguro de que desea {accion} al fletero '{identificador} — {fila.Nombre}'?",
            "Confirmación",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacion != DialogResult.Yes) return;

        await CambiarEstadoAsync(fila);
    }

    private Task CambiarEstadoAsync(FleteroFila fila)
    {
        return EjecutarAsync(async () =>
        {
            await _servicioFleteros.CambiarEstadoAsync(fila.Id, !fila.Activo);
            await CargarAsync(fila.Id);
        }, btnActivarDesactivar);
    }

    internal DataGridView GrillaParaTest => dgvFleteros;
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

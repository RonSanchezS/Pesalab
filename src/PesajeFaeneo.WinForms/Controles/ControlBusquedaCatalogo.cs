using System.ComponentModel;
using PesajeFaeneo.Application.Catalogos.Busqueda;

namespace PesajeFaeneo.WinForms.Controles;

public partial class ControlBusquedaCatalogo : UserControl
{
    private readonly TextBox _txtTexto;
    private readonly ListBox _lstResultados;
    private IReadOnlyList<ElementoBusqueda> _elementos = Array.Empty<ElementoBusqueda>();
    private bool _cambiandoTextoPorCodigo;

    public event EventHandler<ElementoBusqueda>? ElementoSeleccionado;
    public event EventHandler? SeleccionBorrada;
    public event EventHandler<string>? AltaRapidaSolicitada;

    public ElementoBusqueda? Seleccion { get; private set; }
    public bool PermitirAltaRapida { get; set; }

    [DefaultValue("")]
    public string Placeholder
    {
        get => _txtTexto.PlaceholderText;
        set => _txtTexto.PlaceholderText = value ?? string.Empty;
    }

    [DefaultValue(MotorBusqueda.MaximoPorDefecto)]
    public int MaximoResultados { get; set; } = MotorBusqueda.MaximoPorDefecto;

    public ControlBusquedaCatalogo()
    {
        _txtTexto = new TextBox
        {
            Dock = DockStyle.Fill,
            CharacterCasing = CharacterCasing.Normal
        };

        _lstResultados = new ListBox
        {
            TabStop = false,
            IntegralHeight = true,
            Visible = false,
            DisplayMember = nameof(ElementoBusqueda.TextoMostrado)
        };

        Controls.Add(_txtTexto);
        Height = _txtTexto.PreferredHeight;

        _txtTexto.TextChanged += TxtTexto_TextChanged;
        _txtTexto.PreviewKeyDown += TxtTexto_PreviewKeyDown;
        _txtTexto.KeyDown += TxtTexto_KeyDown;
        _txtTexto.Leave += TxtTexto_Leave;
        _lstResultados.MouseClick += LstResultados_MouseClick;
    }

    // El alto lo manda el cuadro de texto: el escalado por DPI del formulario (o un Dock) no debe dejar una
    // franja vacía debajo, porque la fuente del TextBox ya viene escalada.
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        base.SetBoundsCore(x, y, width, _txtTexto?.PreferredHeight ?? height, specified);
    }

    public void EstablecerElementos(IReadOnlyList<ElementoBusqueda> elementos)
    {
        _elementos = elementos ?? Array.Empty<ElementoBusqueda>();
        if (Seleccion != null)
        {
            var actual = _elementos.FirstOrDefault(e => e.Id == Seleccion.Id);
            if (actual != null)
            {
                Seleccion = actual;
                _cambiandoTextoPorCodigo = true;
                try
                {
                    _txtTexto.Text = actual.TextoMostrado;
                }
                finally
                {
                    _cambiandoTextoPorCodigo = false;
                }
            }
            else
            {
                Limpiar();
            }
        }
    }

    public void Seleccionar(int id)
    {
        var encontrado = _elementos.FirstOrDefault(e => e.Id == id);
        if (encontrado != null)
        {
            SeleccionarElemento(encontrado);
        }
    }

    public void Limpiar()
    {
        Seleccion = null;
        _cambiandoTextoPorCodigo = true;
        try
        {
            _txtTexto.Text = string.Empty;
        }
        finally
        {
            _cambiandoTextoPorCodigo = false;
        }
        OcultarLista();
    }

    private void TxtTexto_TextChanged(object? sender, EventArgs e)
    {
        if (_cambiandoTextoPorCodigo)
        {
            return;
        }

        if (Seleccion != null)
        {
            Seleccion = null;
            SeleccionBorrada?.Invoke(this, EventArgs.Empty);
        }

        if (string.IsNullOrWhiteSpace(_txtTexto.Text))
        {
            OcultarLista();
            return;
        }

        var resultados = MotorBusqueda.Buscar(_elementos, _txtTexto.Text, MaximoResultados);
        if (resultados.Count == 0)
        {
            OcultarLista();
            return;
        }

        _lstResultados.BeginUpdate();
        _lstResultados.Items.Clear();
        foreach (var item in resultados)
        {
            _lstResultados.Items.Add(item);
        }
        _lstResultados.EndUpdate();

        PosicionarLista();
        _lstResultados.Visible = true;
        _lstResultados.BringToFront();
    }

    private void TxtTexto_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (_lstResultados.Visible || (Seleccion == null && !string.IsNullOrWhiteSpace(_txtTexto.Text)))
            {
                e.IsInputKey = true;
            }
        }
        else if (e.KeyCode == Keys.Escape)
        {
            if (_lstResultados.Visible)
            {
                e.IsInputKey = true;
            }
        }
    }

    private void TxtTexto_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ProcesarTecla(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void TxtTexto_Leave(object? sender, EventArgs e)
    {
        var form = FindForm();
        if (form != null && _lstResultados.Visible)
        {
            var pt = form.PointToClient(Cursor.Position);
            if (_lstResultados.Bounds.Contains(pt))
            {
                return;
            }
        }

        EjecutarSalida();
    }

    private void LstResultados_MouseClick(object? sender, MouseEventArgs e)
    {
        var index = _lstResultados.IndexFromPoint(e.Location);
        if (index >= 0 && index < _lstResultados.Items.Count)
        {
            var item = (ElementoBusqueda)_lstResultados.Items[index];
            SeleccionarElemento(item);
            _txtTexto.Focus();
        }
    }

    private void SeleccionarElemento(ElementoBusqueda elemento)
    {
        Seleccion = elemento;
        _cambiandoTextoPorCodigo = true;
        try
        {
            _txtTexto.Text = elemento.TextoMostrado;
        }
        finally
        {
            _cambiandoTextoPorCodigo = false;
        }
        OcultarLista();
        _txtTexto.SelectAll();
        ElementoSeleccionado?.Invoke(this, elemento);
    }

    private void OcultarLista()
    {
        _lstResultados.Visible = false;
    }

    private void AsegurarListaEnFormulario()
    {
        var form = FindForm();
        if (form != null && !form.Controls.Contains(_lstResultados))
        {
            form.Controls.Add(_lstResultados);
            form.Deactivate += (_, _) => OcultarLista();
        }
    }

    private void PosicionarLista()
    {
        var form = FindForm();
        if (form == null) return;

        AsegurarListaEnFormulario();

        var screenPoint = _txtTexto.PointToScreen(new Point(0, _txtTexto.Height));
        var clientPoint = form.PointToClient(screenPoint);
        var ancho = Math.Max(_txtTexto.Width, 350);
        var visibleCount = Math.Min(_lstResultados.Items.Count, 8);
        var alto = _lstResultados.ItemHeight * Math.Max(visibleCount, 1) + 4;

        if (clientPoint.Y + alto > form.ClientSize.Height)
        {
            var topScreen = _txtTexto.PointToScreen(Point.Empty);
            var topClient = form.PointToClient(topScreen);
            clientPoint.Y = Math.Max(0, topClient.Y - alto);
        }

        _lstResultados.Bounds = new Rectangle(clientPoint.X, clientPoint.Y, ancho, alto);
    }

    private void EjecutarSalida()
    {
        if (Seleccion == null && !string.IsNullOrWhiteSpace(_txtTexto.Text))
        {
            var exacto = MotorBusqueda.ResolverExacto(_elementos, _txtTexto.Text);
            if (exacto != null)
            {
                SeleccionarElemento(exacto);
            }
        }
        OcultarLista();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_lstResultados.Parent != null)
            {
                _lstResultados.Parent.Controls.Remove(_lstResultados);
            }
            _lstResultados.Dispose();
        }
        base.Dispose(disposing);
    }

    internal TextBox CajaTexto => _txtTexto;
    internal ListBox ListaResultados => _lstResultados;
    internal bool ListaVisible => _lstResultados.Visible;

    internal void SimularSalida()
    {
        EjecutarSalida();
    }

    internal bool ProcesarTecla(Keys tecla)
    {
        switch (tecla)
        {
            case Keys.Down:
                if (!_lstResultados.Visible && !string.IsNullOrWhiteSpace(_txtTexto.Text))
                {
                    TxtTexto_TextChanged(_txtTexto, EventArgs.Empty);
                }
                if (_lstResultados.Visible && _lstResultados.Items.Count > 0)
                {
                    _lstResultados.SelectedIndex = Math.Min(_lstResultados.SelectedIndex + 1, _lstResultados.Items.Count - 1);
                    return true;
                }
                return false;

            case Keys.Up:
                if (_lstResultados.Visible && _lstResultados.Items.Count > 0)
                {
                    _lstResultados.SelectedIndex = Math.Max(_lstResultados.SelectedIndex - 1, 0);
                    return true;
                }
                return false;

            case Keys.PageDown:
                if (_lstResultados.Visible && _lstResultados.Items.Count > 0)
                {
                    _lstResultados.SelectedIndex = Math.Min(_lstResultados.SelectedIndex + 8, _lstResultados.Items.Count - 1);
                    return true;
                }
                return false;

            case Keys.PageUp:
                if (_lstResultados.Visible && _lstResultados.Items.Count > 0)
                {
                    _lstResultados.SelectedIndex = Math.Max(_lstResultados.SelectedIndex - 8, 0);
                    return true;
                }
                return false;

            case Keys.Enter:
                if (_lstResultados.Visible && _lstResultados.SelectedIndex >= 0)
                {
                    var item = (ElementoBusqueda)_lstResultados.SelectedItem!;
                    SeleccionarElemento(item);
                    return true;
                }

                var exacto = MotorBusqueda.ResolverExacto(_elementos, _txtTexto.Text);
                if (exacto != null)
                {
                    SeleccionarElemento(exacto);
                    return true;
                }

                var resultados = MotorBusqueda.Buscar(_elementos, _txtTexto.Text, MaximoResultados);
                if (resultados.Count == 0)
                {
                    if (PermitirAltaRapida)
                    {
                        AltaRapidaSolicitada?.Invoke(this, _txtTexto.Text.Trim());
                        return true;
                    }
                    return false;
                }

                if (resultados.Count == 1)
                {
                    SeleccionarElemento(resultados[0]);
                    return true;
                }

                if (resultados.Count > 1)
                {
                    _lstResultados.BeginUpdate();
                    _lstResultados.Items.Clear();
                    foreach (var res in resultados)
                    {
                        _lstResultados.Items.Add(res);
                    }
                    _lstResultados.EndUpdate();
                    PosicionarLista();
                    _lstResultados.Visible = true;
                    _lstResultados.SelectedIndex = 0;
                    _lstResultados.BringToFront();
                    return true;
                }

                return false;

            case Keys.Escape:
                if (_lstResultados.Visible)
                {
                    OcultarLista();
                    return true;
                }
                return false;

            default:
                return false;
        }
    }
}

using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Controles;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ControlBusquedaCatalogoTests
{
    private static readonly IReadOnlyList<ElementoBusqueda> ElementosPrueba = new List<ElementoBusqueda>
    {
        new(1, "110", "CLIENTE CIENTO DIEZ"),
        new(2, "11", "CLIENTE ONCE"),
        new(3, "111", "CLIENTE CIENTO ONCE"),
        new(4, "200", "OTRO CLIENTE")
    };

    [Fact]
    public void EscribirTexto_MuestraListaConElementosEnOrden()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.CajaTexto.Text = "11";

            Assert.True(control.ListaVisible);
            Assert.Equal(3, control.ListaResultados.Items.Count);
            // El primero debe ser "11" por coincidencia exacta
            var primerItem = (ElementoBusqueda)control.ListaResultados.Items[0];
            Assert.Equal("11", primerItem.Codigo);
        });
    }

    [Fact]
    public void DownMasEnter_SeleccionaElementoYDisparaEventoUnaVez()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.CajaTexto.Text = "11";

            int eventosDisparados = 0;
            ElementoBusqueda? seleccionado = null;
            control.ElementoSeleccionado += (_, e) =>
            {
                eventosDisparados++;
                seleccionado = e;
            };

            control.ProcesarTecla(Keys.Down); // Marca el primer elemento
            control.ProcesarTecla(Keys.Enter); // Selecciona

            Assert.Equal(1, eventosDisparados);
            Assert.NotNull(seleccionado);
            Assert.Equal("11", seleccionado.Codigo);
            Assert.False(control.ListaVisible);
            Assert.Equal(seleccionado, control.Seleccion);
        });
    }

    [Fact]
    public void CodigoExactoMasEnter_SeleccionaSinNavegar()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.CajaTexto.Text = "110";

            int eventosDisparados = 0;
            control.ElementoSeleccionado += (_, _) => eventosDisparados++;

            control.ProcesarTecla(Keys.Enter);

            Assert.Equal(1, eventosDisparados);
            Assert.NotNull(control.Seleccion);
            Assert.Equal("110", control.Seleccion.Codigo);
            Assert.False(control.ListaVisible);
        });
    }

    [Fact]
    public void Escape_OcultaListaSinSeleccionar()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.CajaTexto.Text = "11";
            Assert.True(control.ListaVisible);

            control.ProcesarTecla(Keys.Escape);

            Assert.False(control.ListaVisible);
            Assert.Null(control.Seleccion);
        });
    }

    [Fact]
    public void ModificarTextoTrasSeleccionar_BorraSeleccionYDisparaSeleccionBorrada()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.Seleccionar(2);
            Assert.NotNull(control.Seleccion);

            int borradosDisparados = 0;
            control.SeleccionBorrada += (_, _) => borradosDisparados++;

            control.CajaTexto.Text = "11 modificado";

            Assert.Equal(1, borradosDisparados);
            Assert.Null(control.Seleccion);
        });
    }

    [Fact]
    public void SeleccionarProgramatico_Y_EstablecerElementosSinEseId_BorraSeleccion()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.Seleccionar(1);
            Assert.NotNull(control.Seleccion);
            Assert.Equal("110 — CLIENTE CIENTO DIEZ", control.CajaTexto.Text);

            // Reemplazar elementos por una lista que no incluye id 1
            control.EstablecerElementos(new List<ElementoBusqueda>
            {
                new(2, "11", "CLIENTE ONCE")
            });

            Assert.Null(control.Seleccion);
            Assert.Equal(string.Empty, control.CajaTexto.Text);
        });
    }

    [Fact]
    public void SimularSalida_ConTextoAmbiguoNoSelecciona_ConCodigoExactoSelecciona()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);

            // Texto ambiguo "CLIENTE"
            control.CajaTexto.Text = "CLIENTE";
            control.SimularSalida();
            Assert.Null(control.Seleccion);

            // Código exacto "200"
            control.CajaTexto.Text = "200";
            control.SimularSalida();
            Assert.NotNull(control.Seleccion);
            Assert.Equal("200", control.Seleccion.Codigo);
        });
    }

    [Fact]
    public void EnterSinResultados_PermitirAltaRapida_DisparaAltaRapidaSolicitada()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            using var control = new ControlBusquedaCatalogo
            {
                PermitirAltaRapida = true
            };
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.CajaTexto.Text = "INEXISTENTE";

            string? textoSolicitado = null;
            control.AltaRapidaSolicitada += (_, texto) => textoSolicitado = texto;

            control.ProcesarTecla(Keys.Enter);

            Assert.Equal("INEXISTENTE", textoSolicitado);
        });
    }

    [Fact]
    public void Dispose_EliminaListaResultadosDeFormControls()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new Form();
            var control = new ControlBusquedaCatalogo();
            form.Controls.Add(control);
            form.Show();

            control.EstablecerElementos(ElementosPrueba);
            control.CajaTexto.Text = "11"; // Asegura que la lista se cree y agregue a form.Controls

            Assert.True(form.Controls.Contains(control.ListaResultados));

            control.Dispose();
            form.Controls.Remove(control);

            Assert.False(form.Controls.Contains(control.ListaResultados));
        });
    }
}

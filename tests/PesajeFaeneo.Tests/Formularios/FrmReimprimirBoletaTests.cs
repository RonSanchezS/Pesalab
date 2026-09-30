using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Formularios;

public class FrmReimprimirBoletaTests
{
    private readonly ServicioBoletasFalso _boletas = new();
    private readonly ServicioImpresionBoletasFalso _impresion = new();
    private readonly ServicioParametrosFalso _parametros = new();

    private FrmReimprimirBoleta CrearFormulario()
    {
        return new FrmReimprimirBoleta(_boletas, _impresion, _parametros);
    }

    [Fact]
    public void Cargar_ConUltimaBoletaEmitida_CargaNumeroYResumen()
    {
        HiloSta.Ejecutar(() =>
        {
            _parametros.Parametros = _parametros.Parametros with { UltimoNumeroBoletaEmitido = 120 };
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(numeroBoleta: 120, pesajeId: 15, pesoTotal: 238.5m);

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(120m, form.numBoleta.Value);
            Assert.Contains("238,5 kg", form.lblTotal.Text);
            Assert.True(form.btnImprimir.Enabled);
            Assert.True(form.btnVistaPrevia.Enabled);
            Assert.Equal(new long[] { 120 }, _boletas.NumerosObtenidos);
        });
    }

    [Fact]
    public void Cargar_SinBoletasEmitidas_MuestraMensajeYBotonesDeshabilitados()
    {
        HiloSta.Ejecutar(() =>
        {
            _parametros.Parametros = _parametros.Parametros with { UltimoNumeroBoletaEmitido = null };

            using var form = CrearFormulario();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Aún no hay boletas emitidas.", form.lblMensaje.Text);
            Assert.False(form.btnImprimir.Enabled);
            Assert.False(form.btnVistaPrevia.Enabled);
            Assert.Empty(_boletas.NumerosObtenidos);
        });
    }

    [Fact]
    public void Buscar_NumeroInexistente_EstableceErrorEnProviderYDeshabilitaBotones()
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.ExcepcionObtenerPorNumero = new ValidacionException(CamposBoleta.Numero, "No existe la boleta N° 999.");

            using var form = CrearFormulario();
            form.numBoleta.Value = 999;
            form.BuscarParaTestAsync().GetAwaiter().GetResult();

            var errorProvider = (ErrorProvider)typeof(FrmReimprimirBoleta)
                .GetField("_errorProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(form)!;

            var error = errorProvider.GetError(form.numBoleta);
            Assert.NotEmpty(error);
            Assert.Contains("No existe la boleta", error);
            Assert.False(form.btnImprimir.Enabled);
            Assert.False(form.btnVistaPrevia.Enabled);
        });
    }

    [Fact]
    public void Imprimir_Exito_InvocaServicioYRefrescaResumen()
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 10, numeroBoleta: 50, impresionesPrevias: 0);
            _impresion.ResultadoPorDefecto = new ResultadoImpresion(EstadoImpresion.Impresa, "Boleta N° 50 impresa con éxito.", 50, false);

            using var form = CrearFormulario();
            form.numBoleta.Value = 50;
            form.BuscarParaTestAsync().GetAwaiter().GetResult();

            // Al imprimir, primero obtiene boleta (1ra búsqueda), luego imprime, luego refresca (2da búsqueda)
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 10, numeroBoleta: 50, impresionesPrevias: 1);

            form.ImprimirParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(_impresion.Solicitudes);
            Assert.Equal((10, SolicitudImpresion.Manual), _impresion.Solicitudes[0]);
            Assert.Equal("Boleta N° 50 impresa con éxito.", form.lblMensaje.Text);
            Assert.Equal(2, _boletas.NumerosObtenidos.Count);
        });
    }

    [Fact]
    public void Imprimir_Fallida_InvocaGanchoDeAdvertencia()
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 10, numeroBoleta: 50);
            _impresion.ResultadoPorDefecto = new ResultadoImpresion(EstadoImpresion.Fallida, "Sin papel en la impresora.", 50, false);

            string? advertenciaRecibida = null;
            using var form = CrearFormulario();
            form.MostrarAdvertenciaParaTest = msg => advertenciaRecibida = msg;

            form.numBoleta.Value = 50;
            form.BuscarParaTestAsync().GetAwaiter().GetResult();

            form.ImprimirParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Sin papel en la impresora.", advertenciaRecibida);
            Assert.Equal("Sin papel en la impresora.", form.lblMensaje.Text);
        });
    }

    [Fact]
    public void Teclas_F9SinDatos_NoImprime_Y_Enter_NoLlamaImprimir()
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = CrearFormulario();

            // Sin buscar datos, F9 no debe llamar a imprimir
            var consumidaF9 = form.ProcesarTeclaParaTest(Keys.F9);
            Assert.True(consumidaF9);
            Assert.Empty(_impresion.Solicitudes);

            // Enter no debe invocar imprimir
            form.ProcesarTeclaParaTest(Keys.Enter);
            Assert.Empty(_impresion.Solicitudes);
        });
    }

    [Fact]
    public void VistaPrevia_AbreDialogoYDocumentoQuedaDesechado()
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 20, numeroBoleta: 77);

            bool documentoDesechado = false;
            var doc = new PrintDocument();
            doc.Disposed += (_, _) => documentoDesechado = true;
            _impresion.DocumentoVistaPrevia = doc;

            FrmVistaPrevia? formVistaPreviaRecibido = null;
            using var form = CrearFormulario();
            form.MostrarDialogoParaTest = frm =>
            {
                formVistaPreviaRecibido = Assert.IsType<FrmVistaPrevia>(frm);
            };

            form.numBoleta.Value = 77;
            form.BuscarParaTestAsync().GetAwaiter().GetResult();
            form.VistaPreviaParaTestAsync().GetAwaiter().GetResult();

            Assert.NotNull(formVistaPreviaRecibido);
            Assert.Same(doc, formVistaPreviaRecibido.ppcVista.Document);

            // Al desechar el formulario de vista previa, el documento debe desecharse
            formVistaPreviaRecibido.Dispose();
            Assert.True(documentoDesechado);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CambiarNumeroTrasBuscar_InvalidaResumenYF9NoImprimeLaBoletaAnterior(bool tecleando)
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(pesajeId: 10, numeroBoleta: 50, pesoTotal: 238.5m);

            using var form = CrearFormulario();
            form.numBoleta.Value = 50;
            form.BuscarParaTestAsync().GetAwaiter().GetResult();
            Assert.True(form.btnImprimir.Enabled);

            // El operador cambia el número sin volver a buscar (Enter).
            if (tecleando)
            {
                form.numBoleta.Text = "51";
            }
            else
            {
                form.numBoleta.Value = 51;
            }

            Assert.False(form.btnImprimir.Enabled);
            Assert.False(form.btnVistaPrevia.Enabled);
            Assert.Equal("-", form.lblTotal.Text);

            form.ProcesarTeclaParaTest(Keys.F9);
            form.ProcesarTeclaParaTest(Keys.F4);
            form.ImprimirParaTestAsync().GetAwaiter().GetResult();
            form.VistaPreviaParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(_impresion.Solicitudes);
            Assert.Empty(_impresion.VistasPrevias);
        });
    }

    [Fact]
    public void Buscar_RespuestaLlegaTrasCambiarNumero_SeDescarta()
    {
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource<DatosBoleta>();
            _boletas.TcsObtenerPorNumero = tcs;

            using var form = CrearFormulario();
            form.numBoleta.Value = 50;
            var busqueda = form.BuscarParaTestAsync();

            form.numBoleta.Value = 51;
            tcs.SetResult(DatosBoletaDePrueba.Crear(pesajeId: 10, numeroBoleta: 50));

            var reloj = System.Diagnostics.Stopwatch.StartNew();
            while (!busqueda.IsCompleted && reloj.Elapsed < TimeSpan.FromSeconds(5))
            {
                System.Windows.Forms.Application.DoEvents();
            }

            Assert.True(busqueda.IsCompleted);
            busqueda.GetAwaiter().GetResult();

            Assert.False(form.btnImprimir.Enabled);
            Assert.Equal("-", form.lblTotal.Text);
        });
    }

    [Fact]
    public void EstadoAnulado_MuestraLeyendaANULADA()
    {
        HiloSta.Ejecutar(() =>
        {
            _boletas.DatosRetorno = DatosBoletaDePrueba.Crear(
                estado: EstadoPesaje.Anulado,
                motivoAnulacion: "ERROR DE DIGITACION");

            using var form = CrearFormulario();
            form.numBoleta.Value = 1;
            form.BuscarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("ANULADA", form.lblEstado.Text);
            Assert.Contains("ERROR DE DIGITACION", form.lblEstado.Text);
        });
    }
}

using System.Windows.Forms;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Gestion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmAnularBoletaTests
{
    [Fact]
    public void Configurar_MuestraNumeroResumenYAdvertencia_BotonHabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmAnularBoleta(falso);

            var b = DatosGestionDePrueba.CrearBoletaEncontrada(numeroBoleta: 9001);
            form.Configurar(b);

            Assert.Equal("Boleta N° 9001", form.lblTitulo.Text);
            Assert.Contains("Ganadero: AH - GANADERO AH", form.lblResumen.Text);
            Assert.Contains("Esta acción NO se puede deshacer", form.lblAdvertencia.Text);
            Assert.True(form.btnAnular.Enabled);
            Assert.Empty(form.txtMotivo.Text);
        });
    }

    [Fact]
    public void Anular_SinConfirmacion_NoLlamaServicioYNoCierra()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmAnularBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());

            form.ConfirmarParaTest = _ => false;
            form.txtMotivo.Text = "MOTIVO DE PRUEBA";

            form.AnularParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(falso.AnulacionesRecibidas);
            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.NotEqual(DialogResult.Retry, form.DialogResult);
        });
    }

    [Fact]
    public void Anular_ConfirmadoConMotivoValido_LlamaServicioYCierraConOk()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmAnularBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada(id: 77, version: 2));

            form.ConfirmarParaTest = _ => true;
            form.txtMotivo.Text = "ERROR EN DIGITACION DE PESO";

            form.AnularParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.OK, form.DialogResult);
            var recibido = Assert.Single(falso.AnulacionesRecibidas);
            Assert.Equal(77, recibido.PesajeId);
            Assert.Equal(2, recibido.VersionEsperada);
            Assert.Equal("ERROR EN DIGITACION DE PESO", recibido.Motivo);
        });
    }

    [Fact]
    public void Anular_ValidacionException_MuestraErrorEnTxtMotivoYNoCierra()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ExcepcionAnular = new ValidacionException(CamposGestionPesajes.Motivo, "El motivo es demasiado corto.");

            using var form = new FrmAnularBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());

            form.ConfirmarParaTest = _ => true;
            form.txtMotivo.Text = "MOTIVO VALIDO"; // pasa la validación previa del formulario; el error lo da el servicio

            form.AnularParaTestAsync().GetAwaiter().GetResult();

            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.Equal("El motivo es demasiado corto.", form._errorProvider.GetError(form.txtMotivo));
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("    ")]
    public void Anular_MotivoCorto_MuestraErrorSinConfirmarNiLlamarServicio(string motivo)
    {
        // F12 (E5): el motivo se valida antes de la confirmación.
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmAnularBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());

            var confirmaciones = 0;
            form.ConfirmarParaTest = _ => { confirmaciones++; return true; };
            form.txtMotivo.Text = motivo;

            form.AnularParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(0, confirmaciones);
            Assert.Empty(falso.AnulacionesRecibidas);
            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.Equal("Ingrese el motivo de la anulación (mínimo 5 caracteres).", form._errorProvider.GetError(form.txtMotivo));
        });
    }

    [Fact]
    public void Anular_ConcurrenciaExceptionYReglaNegocioPesYaAnulada_CierraConRetryYMuestraAdvertencia()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ExcepcionAnular = new ConcurrenciaException("La boleta ya fue modificada.");

            using var form = new FrmAnularBoleta(falso);
            string? adv = null;
            form.MostrarAdvertenciaParaTest = msg => adv = msg;
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());
            form.ConfirmarParaTest = _ => true;
            form.txtMotivo.Text = "MOTIVO VALIDO";

            form.AnularParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Retry, form.DialogResult);
            Assert.Equal("La boleta ya fue modificada.", adv);

            // ReglaNegocioException("PES_YA_ANULADA")
            falso.ExcepcionAnular = new ReglaNegocioException("PES_YA_ANULADA", "La boleta ya se encuentra anulada.");
            using var form2 = new FrmAnularBoleta(falso);
            form2.MostrarAdvertenciaParaTest = msg => adv = msg;
            form2.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());
            form2.ConfirmarParaTest = _ => true;
            form2.txtMotivo.Text = "MOTIVO VALIDO";

            form2.AnularParaTestAsync().GetAwaiter().GetResult();
            Assert.Equal(DialogResult.Retry, form2.DialogResult);
            Assert.Equal("La boleta ya se encuentra anulada.", adv);
        });
    }

    [Fact]
    public void Anular_ConfirmacionSePideAntesDeLlamarServicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var orden = new List<string>();
            var falso = new ServicioGestionPesajesFalso();
            falso.PausaAnularAsync = () =>
            {
                orden.Add("servicio");
                return Task.CompletedTask;
            };

            using var form = new FrmAnularBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());

            form.ConfirmarParaTest = _ =>
            {
                orden.Add("confirmacion");
                return true;
            };
            form.txtMotivo.Text = "MOTIVO VALIDO";

            form.AnularParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new[] { "confirmacion", "servicio" }, orden);
        });
    }

    [Fact]
    public void Controles_TxtMotivoMaxLength_Es100()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmAnularBoleta(falso);

            Assert.Equal(100, form.txtMotivo.MaxLength);
        });
    }

    [Fact]
    public void Controles_AcceptButtonYCancelButton_Configurados()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmAnularBoleta(falso);

            Assert.Same(form.btnAnular, form.AcceptButton);
            Assert.Same(form.btnCancelar, form.CancelButton);
            Assert.Equal(DialogResult.Cancel, form.btnCancelar.DialogResult);
        });
    }

    [Fact]
    public void Anular_Reentrada_DobleInvocacionRapida_LlamaServicioSoloUnaVez()
    {
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource();
            var falso = new ServicioGestionPesajesFalso();
            falso.PausaAnularAsync = () => tcs.Task;

            using var form = new FrmAnularBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaEncontrada());
            form.ConfirmarParaTest = _ => true;
            form.txtMotivo.Text = "MOTIVO VALIDO";

            var t1 = form.AnularParaTestAsync();
            var t2 = form.AnularParaTestAsync();

            tcs.SetResult();
            Task.WaitAll(t1, t2);

            Assert.Single(falso.AnulacionesRecibidas);
        });
    }
}

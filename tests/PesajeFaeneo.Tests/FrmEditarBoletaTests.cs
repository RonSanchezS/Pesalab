using System.Windows.Forms;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Gestion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmEditarBoletaTests
{
    [Fact]
    public void Configurar_LlenaSoloLecturaYEditables_PreseleccionaValores_PesosNoEditables()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmEditarBoleta(falso);

            var dto = DatosGestionDePrueba.CrearBoletaParaEdicion(
                numeroBoleta: 5001,
                pesoIzquierdo: 110.5m,
                pesoDerecho: 115.0m,
                pesoTotal: 225.5m,
                origen: OrigenPeso.Simulador,
                tipoDesposte: TipoDesposte.ResCompleta);

            form.Configurar(dto);

            Assert.Equal("Boleta N° 5001", form.lblTitulo.Text);
            Assert.Contains("RES COMPLETA", form.lblDesposte.Text);
            Assert.True(form.txtPesoIzquierdo.ReadOnly);
            Assert.True(form.txtPesoDerecho.ReadOnly);
            Assert.True(form.txtPesoTotal.ReadOnly);
            Assert.False(form.txtPesoTotal.TabStop);
            Assert.Contains("225,5 kg", form.txtPesoTotal.Text);
            Assert.Equal("SIMULADO", form.lblOrigen.Text);
            Assert.Contains("no se pueden modificar", form.lblAviso.Text);

            // Editables
            Assert.Equal(10, form.ctlGanadero.Seleccion?.Id);
            Assert.Equal(20, form.ctlCliente.Seleccion?.Id);
            Assert.Equal(1m, form.numSecuencia.Value);
            Assert.Equal("CALLE PRUEBA 123", form.txtDireccion.Text);
            Assert.Equal("BLANCO", form.txtColorRaza.Text);
            Assert.Equal("MACHO", form.cmbSexo.Text);
            Assert.Equal("OBSERVACION PRUEBA", form.txtObservacion.Text);
        });
    }

    [Fact]
    public void Guardar_SinCambiosDeValores_NoCierraYMuestraMensaje()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ResultadoEditar = new ResultadoEdicion(false, Array.Empty<string>());

            using var form = new FrmEditarBoleta(falso);
            var dto = DatosGestionDePrueba.CrearBoletaParaEdicion();
            form.Configurar(dto);

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.Equal("No se modificó ningún dato.", form.lblMensaje.Text);
            Assert.Empty(form.CamposModificados);
        });
    }

    [Fact]
    public void Guardar_ConCambios_LlamaServicioYSiExitosoCierraConOkYCamposModificados()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ResultadoEditar = new ResultadoEdicion(true, new[] { "Dirección" });

            using var form = new FrmEditarBoleta(falso);
            var dto = DatosGestionDePrueba.CrearBoletaParaEdicion(id: 42, version: 3);
            form.Configurar(dto);

            form.txtDireccion.Text = "NUEVA DIRECCION 456";
            form.txtMotivo.Text = "CAMBIO DE DOMICILIO";

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.Equal(new[] { "Dirección" }, form.CamposModificados);

            var recibido = Assert.Single(falso.EdicionesRecibidas);
            Assert.Equal(42, recibido.PesajeId);
            Assert.Equal(3, recibido.VersionEsperada);
            Assert.Equal("NUEVA DIRECCION 456", recibido.Direccion);
            Assert.Equal("CAMBIO DE DOMICILIO", recibido.Motivo);
        });
    }

    [Fact]
    public void Guardar_FleteroEscritoSinElegir_MarcaErrorYNoLlamaServicio()
    {
        // F12 (E1): antes se guardaba sin fletero y sin aviso.
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ResultadoEditar = new ResultadoEdicion(true, new[] { "Dirección" });

            using var form = new FrmEditarBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            form.txtDireccion.Text = "NUEVA DIRECCION 456";
            form.ctlFletero.CajaTexto.Text = "FLETERO QUE NO EXISTE";

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(falso.EdicionesRecibidas);
            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.Equal("Elija el fletero de la lista o borre el texto.", form._errorProvider.GetError(form.ctlFletero));

            form.ctlFletero.Limpiar();
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            var recibido = Assert.Single(falso.EdicionesRecibidas);
            Assert.Null(recibido.FleteroId);
        });
    }

    [Fact]
    public void Guardar_ValidacionException_MuestraErrorProviderYNoCierra()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ExcepcionEditar = new ValidacionException(new[]
            {
                new ErrorValidacion(CamposPesaje.Secuencia, "Secuencia inválida"),
                new ErrorValidacion(CamposPesaje.Direccion, "Dirección requerida")
            });

            using var form = new FrmEditarBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.Equal("Secuencia inválida", form._errorProvider.GetError(form.numSecuencia));
            Assert.Equal("Dirección requerida", form._errorProvider.GetError(form.txtDireccion));
        });
    }

    [Fact]
    public void Guardar_ConcurrenciaException_CierraConRetryYMuestraAdvertencia()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ExcepcionEditar = new ConcurrenciaException("La boleta fue modificada por otro usuario.");

            using var form = new FrmEditarBoleta(falso);
            string? adv = null;
            form.MostrarAdvertenciaParaTest = msg => adv = msg;

            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Retry, form.DialogResult);
            Assert.Equal("La boleta fue modificada por otro usuario.", adv);
        });
    }

    [Fact]
    public void Guardar_ReglaNegocioExceptionPesAnuladaYEntidadNoEncontrada_CierraConRetry()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ExcepcionEditar = new ReglaNegocioException("PES_ANULADA", "La boleta está anulada.");

            using var form = new FrmEditarBoleta(falso);
            string? adv = null;
            form.MostrarAdvertenciaParaTest = msg => adv = msg;
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Retry, form.DialogResult);
            Assert.Equal("La boleta está anulada.", adv);

            // EntidadNoEncontradaException
            falso.ExcepcionEditar = new EntidadNoEncontradaException("Pesaje", 1);
            using var form2 = new FrmEditarBoleta(falso);
            form2.MostrarAdvertenciaParaTest = msg => adv = msg;
            form2.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            form2.GuardarParaTestAsync().GetAwaiter().GetResult();
            Assert.Equal(DialogResult.Retry, form2.DialogResult);
        });
    }

    [Fact]
    public void Teclado_F12_DisparaGuardadoSiBotonHabilitado_IgnoraSiDeshabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmEditarBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            form.btnGuardar.Enabled = true;
            bool procesado = form.ProcesarTeclaParaTest(Keys.F12);
            Assert.True(procesado);

            // Ahora deshabilitado
            falso.EdicionesRecibidas.Clear();
            form.btnGuardar.Enabled = false;
            procesado = form.ProcesarTeclaParaTest(Keys.F12);
            Assert.True(procesado); // F12 se consume pero no dispara
            Assert.Empty(falso.EdicionesRecibidas);
        });
    }

    [Fact]
    public void SeleccionCliente_CambiarCliente_NoModificaTxtDireccion()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmEditarBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion(direccion: "DIRECCION FIJA"));

            Assert.Equal("DIRECCION FIJA", form.txtDireccion.Text);

            // Cambiar cliente
            form.ctlCliente.Seleccionar(21);
            Assert.Equal(21, form.ctlCliente.Seleccion?.Id);

            // D15: Dirección no cambia
            Assert.Equal("DIRECCION FIJA", form.txtDireccion.Text);
        });
    }

    [Fact]
    public void Guardar_Reentrada_DobleInvocacionRapida_LlamaServicioSoloUnaVez()
    {
        HiloSta.Ejecutar(() =>
        {
            var tcs = new TaskCompletionSource();
            var falso = new ServicioGestionPesajesFalso();
            falso.PausaEditarAsync = () => tcs.Task;

            using var form = new FrmEditarBoleta(falso);
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion());

            var t1 = form.GuardarParaTestAsync();
            var t2 = form.GuardarParaTestAsync();

            tcs.SetResult();
            Task.WaitAll(t1, t2);

            Assert.Single(falso.EdicionesRecibidas);
        });
    }

    [Fact]
    public void Configurar_SexoHembra_SeleccionaHembraYEnviaH()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            falso.ResultadoEditar = new ResultadoEdicion(true, new[] { "Sexo" });

            using var form = new FrmEditarBoleta(falso);
            var dto = DatosGestionDePrueba.CrearBoletaParaEdicion(sexo: 'H');
            form.Configurar(dto);

            Assert.Equal("HEMBRA", form.cmbSexo.Text);

            form.GuardarParaTestAsync().GetAwaiter().GetResult();
            var recibido = Assert.Single(falso.EdicionesRecibidas);
            Assert.Equal('H', recibido.Sexo);
        });
    }

    [Fact]
    public void Teclado_Enter_NoGuarda_AcceptButtonEsNull()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmEditarBoleta(falso);

            Assert.Null(form.AcceptButton);
        });
    }

    [Fact]
    public void Teclado_Escape_CancelaDialogo_CancelButtonEsBtnCancelar()
    {
        HiloSta.Ejecutar(() =>
        {
            var falso = new ServicioGestionPesajesFalso();
            using var form = new FrmEditarBoleta(falso);

            Assert.Same(form.btnCancelar, form.CancelButton);
            Assert.Equal(DialogResult.Cancel, form.btnCancelar.DialogResult);
        });
    }
}

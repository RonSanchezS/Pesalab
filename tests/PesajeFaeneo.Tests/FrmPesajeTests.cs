using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
using PesajeFaeneo.WinForms.Impresion;
using PesajeFaeneo.WinForms.Presentacion.Pesaje;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmPesajeTests
{
    private sealed class ContextoPruebaFrmPesaje
    {
        public ServicioPesajesFalso ServicioPesajes { get; } = new();
        public ServicioGanaderosFalso ServicioGanaderos { get; } = new();
        public ServicioClientesFalso ServicioClientes { get; } = new();
        public ServicioFleterosFalso ServicioFleteros { get; } = new();
        public ServicioParametrosFalso ServicioParametros { get; } = new();
        public ServicioBalanzaFalso ServicioBalanza { get; } = new();
        public SimuladorBalanzaFalso SimuladorBalanza { get; } = new();
        public SesionActual Sesion { get; } = new();
        public FormFactoryFalsa FormFactory { get; } = new();
        public FakeTimeProvider Reloj { get; } = new();
        public ServicioImpresionBoletasFalso ServicioImpresion { get; } = new();

        public ContextoPruebaFrmPesaje(Rol rol = Rol.Administrador)
        {
            Reloj.SetUtcNow(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(-4)));
            Reloj.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("BO", TimeSpan.FromHours(-4), "BO", "BO"));

            Sesion.Iniciar(new UsuarioSesion(1, "usuario_test", "Usuario Test", rol, false));

            FormFactory.Registrar(() => new FrmGanaderoEdicion(ServicioGanaderos));
            FormFactory.Registrar(() => new FrmClienteEdicion(ServicioClientes));

            ServicioGanaderos.ElementosBusqueda.Add(new ElementoBusqueda(1, "AH", "JUAN PEREZ", null));
            ServicioGanaderos.ElementosBusqueda.Add(new ElementoBusqueda(2, "BB", "MARIO GOMEZ", null));

            ServicioClientes.ElementosBusqueda.Add(new ElementoBusqueda(10, "100", "CARLOS LOPEZ", "Av. 123"));
            ServicioClientes.ElementosBusqueda.Add(new ElementoBusqueda(20, "200", "ANA SUAREZ", "Calle Central 45"));
        }

        public FrmPesaje CrearFormulario()
        {
            return new FrmPesaje(
                ServicioPesajes,
                ServicioGanaderos,
                ServicioClientes,
                ServicioFleteros,
                ServicioParametros,
                ServicioBalanza,
                SimuladorBalanza,
                Sesion,
                FormFactory,
                Reloj,
                ServicioImpresion);
        }
    }

    [Fact]
    public void Layout_PesoEnVivoYBotonesDeCapturaVisibles_SinRecortes_NiConEscalado()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            // Defecto de la prueba manual: el display dejaba ~30 px para una fuente de 48 pt y el Label no
            // dibujaba nada; y el GroupBox de pesos ocultaba los botones de captura.
            var alturaLinea = System.Windows.Forms.TextRenderer.MeasureText("8.888,8 kg", form.lblPesoVivo.Font).Height;
            Assert.True(form.lblPesoVivo.ClientSize.Height >= alturaLinea,
                $"El peso en vivo necesita {alturaLinea} px de alto y tiene {form.lblPesoVivo.ClientSize.Height}");

            foreach (var boton in new[] { form.btnCapturarIzquierdo, form.btnCapturarDerecho })
            {
                var grupo = boton.Parent!.Parent!;
                var enGrupo = grupo.RectangleToClient(boton.RectangleToScreen(boton.ClientRectangle));
                Assert.True(grupo.ClientRectangle.Contains(enGrupo), $"'{boton.Text}' {enGrupo} queda fuera de 'Pesos Capturados' {grupo.ClientRectangle}");
            }

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    [Fact]
    public void CargarAsync_LlenaGanaderosClientesObservacionYProximaBoleta()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Parametros = new ParametrosDto(
                "EMPRESA", "DIR", "TEL", "CIU", "RS", "800", "OBS INICIAL", null, null, 42, 41, ctx.Reloj.GetUtcNow().UtcDateTime);
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("OBS INICIAL", form.txtObservacion.Text);
            Assert.Contains("42", form.lblProximaBoleta.Text);
        });
    }

    [Fact]
    public void SeleccionarGanaderoYCliente_ActualizaSecuenciaInfoYDireccion()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioPesajes.ResultadoSugerencia = new SugerenciaSecuencia(Siguiente: 15, PesajesVigentesHoy: 14);

            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal(15, form.numSecuencia.Value);
            Assert.Contains("14", form.lblInfoSecuencia.Text);

            form.ctlCliente.Seleccionar(10);
            Assert.Equal("Av. 123", form.txtDireccion.Text);
        });
    }

    [Fact]
    public void SugerenciaSecuencia_QueLlegaTarde_SeDescartaSiGanaderoCambio()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            var tcs1 = new TaskCompletionSource<SugerenciaSecuencia>();
            var tcs2 = new TaskCompletionSource<SugerenciaSecuencia>();

            ctx.ServicioPesajes.AlSugerirSecuenciaAsync = id => id == 1 ? tcs1.Task : tcs2.Task;

            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlGanadero.Seleccionar(2);

            tcs2.SetResult(new SugerenciaSecuencia(Siguiente: 20, PesajesVigentesHoy: 19));
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(20, form.numSecuencia.Value);

            // Ahora completa la primera (que correspondía a Ganadero 1)
            tcs1.SetResult(new SugerenciaSecuencia(Siguiente: 5, PesajesVigentesHoy: 4));
            System.Windows.Forms.Application.DoEvents();

            // Debe mantenerse en 20, no retroceder a 5
            Assert.Equal(20, form.numSecuencia.Value);
        });
    }

    [Fact]
    public void CapturarLado_InstantaneaNoCapturableNoAsigna_CapturableAsignaTextoFormateado()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // Instantánea no estable (no capturable)
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, false, ctx.Reloj.GetUtcNow(), null, "Balanza", false);

            form.CapturarParaTest(LadoPesaje.Izquierdo);
            Assert.Equal(string.Empty, form.txtPesoIzquierdo.Text);

            // Instantánea estable y reciente (capturable)
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);

            form.CapturarParaTest(LadoPesaje.Izquierdo);
            Assert.Equal(120.0m.ToString("0.0", CultureInfo.CurrentCulture), form.txtPesoIzquierdo.Text);
        });
    }

    [Fact]
    public void CapturarLado_RequiereRetornoACeroEntreLados()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // Primer lado: 150 kg estable
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 150m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);
            Assert.NotEmpty(form.txtPesoIzquierdo.Text);

            // Segundo lado sin retornar a cero: 160 kg estable
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 160m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);
            Assert.Equal(string.Empty, form.txtPesoDerecho.Text);

            // Retorno a cero
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            // Ahora el segundo lado sí se acepta
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                4, EstadoBalanza.Conectada, 160m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);
            Assert.Equal(160.0m.ToString("0.0", CultureInfo.CurrentCulture), form.txtPesoDerecho.Text);
        });
    }

    [Fact]
    public void ModoSimulador_OperadorDeshabilitaAccionesYOcultaSimulador_AdminPermiteCapturaYMarcaOrigenSimulado()
    {
        HiloSta.Ejecutar(() =>
        {
            // Caso 1: Operador con balanza simulada
            var ctxOperador = new ContextoPruebaFrmPesaje(Rol.Operador);
            ctxOperador.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 100m, true, ctxOperador.Reloj.GetUtcNow(), null, "Simulador", true);

            using var formOp = ctxOperador.CrearFormulario();
            formOp.ConfirmarCierre = false;
            formOp.Show();
            formOp.CargarParaTestAsync().GetAwaiter().GetResult();
            formOp.SondearBalanzaParaTest();

            Assert.False(formOp.grpSimulador.Visible);
            Assert.False(formOp.btnCapturarIzquierdo.Enabled);
            Assert.False(formOp.btnGrabar.Enabled);
            Assert.Contains("MODO SIMULADOR: solo un administrador", formOp.lblGuia.Text);

            // Caso 2: Administrador con balanza simulada
            var ctxAdmin = new ContextoPruebaFrmPesaje(Rol.Administrador);
            ctxAdmin.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 100m, true, ctxAdmin.Reloj.GetUtcNow(), null, "Simulador", true);

            using var formAdmin = ctxAdmin.CrearFormulario();
            formAdmin.ConfirmarCierre = false;
            formAdmin.Show();
            formAdmin.CargarParaTestAsync().GetAwaiter().GetResult();
            formAdmin.SondearBalanzaParaTest();

            Assert.True(formAdmin.grpSimulador.Visible);
            Assert.True(formAdmin.btnCapturarIzquierdo.Enabled);

            // Grabar con simulador
            formAdmin.ctlGanadero.Seleccionar(1);
            formAdmin.ctlCliente.Seleccionar(10);
            formAdmin.CapturarParaTest(LadoPesaje.Izquierdo);

            // Cero
            ctxAdmin.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctxAdmin.Reloj.GetUtcNow(), null, "Simulador", true);
            formAdmin.SondearBalanzaParaTest();

            // Derecho
            ctxAdmin.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 110m, true, ctxAdmin.Reloj.GetUtcNow(), null, "Simulador", true);
            formAdmin.CapturarParaTest(LadoPesaje.Derecho);

            formAdmin.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(ctxAdmin.ServicioPesajes.DtosRegistrados);
            Assert.True(ctxAdmin.ServicioPesajes.DtosRegistrados[0].CapturadoConSimulador);
        });
    }

    [Fact]
    public void GrabarAsync_EnviaDtoCompletoYRestableceEstadoPostGrabado()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            form.txtColorRaza.Text = "NEGRO COLORADO";
            form.cmbSexo.SelectedIndex = 0; // MACHO
            form.cmbDesposte.SelectedIndex = 0; // RES COMPLETA
            form.txtObservacion.Text = "OBSERVACION DE PRUEBA";

            // Capturar 120 y 130 kg
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(
                Id: 1,
                NumeroBoleta: 99,
                FechaHoraUtc: ctx.Reloj.GetUtcNow().UtcDateTime,
                PesoTotal: 250m,
                Origen: OrigenPeso.Balanza);

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(ctx.ServicioPesajes.DtosRegistrados);
            var dto = ctx.ServicioPesajes.DtosRegistrados[0];
            Assert.Equal(1, dto.GanaderoId);
            Assert.Equal(10, dto.ClienteId);
            Assert.Equal(120m, dto.PesoIzquierdo);
            Assert.Equal(130m, dto.PesoDerecho);
            Assert.Equal("NEGRO COLORADO", dto.ColorRaza);
            Assert.Equal('M', dto.Sexo);
            Assert.Equal(TipoDesposte.ResCompleta, dto.TipoDesposte);
            Assert.Equal("OBSERVACION DE PRUEBA", dto.Observacion);

            // Post-grabado
            Assert.Equal(string.Empty, form.txtPesoIzquierdo.Text);
            Assert.Equal(string.Empty, form.txtPesoDerecho.Text);
            Assert.Equal(string.Empty, form.txtPesoTotal.Text);
            Assert.Equal(string.Empty, form.txtColorRaza.Text);
            Assert.Equal("FAENEO NORMAL", form.txtObservacion.Text); // Restablecido a valor por defecto
            Assert.Equal(1, form.ctlGanadero.Seleccion?.Id);
            Assert.Equal(10, form.ctlCliente.Seleccion?.Id);
            Assert.Contains("Boleta N° 99 grabada", form.lblGuia.Text);
            Assert.True(ctx.ServicioPesajes.VecesSugerirLlamado >= 2);
            Assert.Equal(0, ctx.ServicioPesajes.VecesListarDelDiaLlamado); // el historial ya no vive en esta pantalla
        });
    }

    [Fact]
    public void GrabarAsync_ConValidacionException_MuestraErrorEnControlYConservaPesos()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            ctx.ServicioPesajes.ExcepcionAlRegistrar = new ValidacionException(CamposPesaje.Secuencia, "Secuencia duplicada.");

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.NotEmpty(form._errorProvider.GetError(form.numSecuencia));
            Assert.NotEmpty(form.txtPesoIzquierdo.Text);
            Assert.NotEmpty(form.txtPesoDerecho.Text);
        });
    }

    [Fact]
    public void ProcesarTecla_F12_RespetaEstadoHabilitadoYProtegeContraDoblePulsacion()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // 1. Grabar deshabilitado: F12 no invoca servicio
            form.ProcesarTeclaParaTest(Keys.F12);
            Assert.Empty(ctx.ServicioPesajes.DtosRegistrados);

            // Preparar formulario para grabar
            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            Assert.True(form.btnGrabar.Enabled);

            // F12 con modificador Ctrl no debe disparar
            form.ProcesarTeclaParaTest(Keys.F12 | Keys.Control);
            Assert.Empty(ctx.ServicioPesajes.DtosRegistrados);

            // F12 con TCS para verificar que una segunda pulsación inmediata no duplica
            var tcs = new TaskCompletionSource<PesajeRegistrado>();
            ctx.ServicioPesajes.AlRegistrarAsync = _ => tcs.Task;

            form.ProcesarTeclaParaTest(Keys.F12);
            Assert.Single(ctx.ServicioPesajes.DtosRegistrados);

            // Segunda pulsación mientras está pendiente
            form.ProcesarTeclaParaTest(Keys.F12);
            Assert.Single(ctx.ServicioPesajes.DtosRegistrados);

            // Completar TCS
            tcs.SetResult(new PesajeRegistrado(10, 1, ctx.Reloj.GetUtcNow().UtcDateTime, 250m, OrigenPeso.Balanza));
            System.Windows.Forms.Application.DoEvents();
        });
    }

    [Fact]
    public void ProcesarTecla_Enter_AvanzaCamposCorrectamenteSegunOrdenTabulacion()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.Show();
            System.Windows.Forms.Application.DoEvents();

            form.txtColorRaza.Focus();
            System.Windows.Forms.Application.DoEvents();
            Assert.True(form.txtColorRaza.Focused);

            form.ProcesarTeclaParaTest(Keys.Enter);
            System.Windows.Forms.Application.DoEvents();
            Assert.True(form.cmbSexo.Focused);

            // Con cliente seleccionado, Enter desde el TextBox interno del buscador avanza a txtDireccion
            form.ctlCliente.Seleccionar(10);
            System.Windows.Forms.Application.DoEvents();

            var txtInterno = form.ctlCliente.Controls.OfType<TextBox>().First();
            txtInterno.Focus();
            System.Windows.Forms.Application.DoEvents();
            Assert.True(txtInterno.Focused);

            form.ProcesarTeclaParaTest(Keys.Enter);
            System.Windows.Forms.Application.DoEvents();
            Assert.True(form.txtDireccion.Focused);

            form.Close();
        });
    }

    [Fact]
    public void FormClosing_ConPesosYConfirmarCierreFalso_Cierra_DuranteGrabadoPendiente_CancelaCierre()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            // Bloquear con TCS durante grabado
            var tcs = new TaskCompletionSource<PesajeRegistrado>();
            ctx.ServicioPesajes.AlRegistrarAsync = _ => tcs.Task;

            var tareaGrabar = form.GrabarParaTestAsync();

            // Intentar cerrar mientras se graba -> debe cancelarse
            form.Close();
            Assert.False(form.IsDisposed);

            // Completar grabado
            tcs.SetResult(new PesajeRegistrado(1, 1, ctx.Reloj.GetUtcNow().UtcDateTime, 120m, OrigenPeso.Balanza));
            tareaGrabar.GetAwaiter().GetResult();

            // Ahora con pesos y ConfirmarCierre = false, Close() sí debe cerrar
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            form.Close();
            Assert.True(form.IsDisposed);
        });
    }

    [Fact]
    public void AltaRapidaGanadero_RecargaListaYSeleccionaNuevoGanadero()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje(Rol.Operador);
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            ctx.ServicioGanaderos.AlCrearRapidoAsync = dto =>
            {
                ctx.ServicioGanaderos.ElementosBusqueda.Add(new ElementoBusqueda(77, "1", dto.Nombre, null));
                return Task.FromResult(77);
            };

            form.MostrarDialogoParaTest = f =>
            {
                if (f is FrmGanaderoEdicion ge)
                {
                    ge.TxtNombre.Text = "NUEVO GANADERO";
                    ge.GuardarParaTestAsync().GetAwaiter().GetResult();
                    return DialogResult.OK;
                }
                return DialogResult.Cancel;
            };

            // Simular búsqueda sin resultados que dispara AltaRapidaSolicitada
            form.ctlGanadero.Controls.OfType<TextBox>().First().Text = "ZZ";
            form.ctlGanadero.ProcesarTecla(Keys.Enter);
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal(77, form.ctlGanadero.Seleccion?.Id);
            Assert.Equal("NUEVO GANADERO", form.ctlGanadero.Seleccion?.Descripcion);
        });
    }

    [Fact]
    public void AltaRapidaCliente_RecargaListaYSeleccionaNuevoCliente_SinPedirCodigo()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje(Rol.Operador);
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            ctx.ServicioClientes.AlCrearRapidoAsync = dto =>
            {
                ctx.ServicioClientes.ElementosBusqueda.Add(new ElementoBusqueda(88, "1", dto.Nombre, dto.Direccion));
                return Task.FromResult(88);
            };

            form.MostrarDialogoParaTest = f =>
            {
                if (f is FrmClienteEdicion ce)
                {
                    Assert.Equal(FrmClienteEdicion.TextoCodigoAlta, ce.TxtCodigo.Text); // el código lo asigna el sistema
                    Assert.Equal("CLIENTE NUEVO", ce.TxtNombre.Text);
                    ce.TxtNombre.Text = "CLIENTE ALTA RAPIDA";
                    ce.TxtDireccion.Text = "CALLE NUEVA 123";
                    ce.GuardarParaTestAsync().GetAwaiter().GetResult();
                    return DialogResult.OK;
                }
                return DialogResult.Cancel;
            };

            form.ctlCliente.Controls.OfType<TextBox>().First().Text = "CLIENTE NUEVO";
            form.ctlCliente.ProcesarTecla(Keys.Enter);
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal(88, form.ctlCliente.Seleccion?.Id);
            Assert.Equal("CALLE NUEVA 123", form.txtDireccion.Text);
        });
    }

    [Fact]
    public void PermisoAltaRapida_ConfiguraPermitirAltaRapidaEnControlesSegunSesion()
    {
        HiloSta.Ejecutar(() =>
        {
            // Operador tiene AltaRapidaCatalogos -> PermitirAltaRapida == true
            var ctxOperador = new ContextoPruebaFrmPesaje(Rol.Operador);
            using var formOp = ctxOperador.CrearFormulario();
            formOp.ConfirmarCierre = false;

            Assert.True(formOp.ctlGanadero.PermitirAltaRapida);
            Assert.True(formOp.ctlCliente.PermitirAltaRapida);

            // Sesion sin autenticar -> PermitirAltaRapida == false
            var ctxSinSesion = new ContextoPruebaFrmPesaje(Rol.Operador);
            ctxSinSesion.Sesion.Cerrar();
            using var formSin = ctxSinSesion.CrearFormulario();
            formSin.ConfirmarCierre = false;

            Assert.False(formSin.ctlGanadero.PermitirAltaRapida);
            Assert.False(formSin.ctlCliente.PermitirAltaRapida);
        });
    }

    [Fact]
    public void GrabarAsync_ConResultadoImpresa_EnviaAutomaticaAlGrabarYGuiaIndicaGrabadaEImpresa()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(
                Id: 10,
                NumeroBoleta: 55,
                FechaHoraUtc: ctx.Reloj.GetUtcNow().UtcDateTime,
                PesoTotal: 250m,
                Origen: OrigenPeso.Balanza);

            ctx.ServicioImpresion.ResultadoPorDefecto = new ResultadoImpresion(
                EstadoImpresion.Impresa, "Boleta N° 55 impresa", 1, true);

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(ctx.ServicioImpresion.Solicitudes);
            Assert.Equal((10, SolicitudImpresion.AutomaticaAlGrabar), ctx.ServicioImpresion.Solicitudes[0]);
            Assert.Contains("grabada", form.lblGuia.Text);
            Assert.Contains("impresa", form.lblGuia.Text);
        });
    }

    [Fact]
    public void GrabarAsync_ConResultadoOmitida_GuiaIndicaPulseF9ParaImprimir()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(
                Id: 10,
                NumeroBoleta: 55,
                FechaHoraUtc: ctx.Reloj.GetUtcNow().UtcDateTime,
                PesoTotal: 250m,
                Origen: OrigenPeso.Balanza);

            ctx.ServicioImpresion.ResultadoPorDefecto = new ResultadoImpresion(
                EstadoImpresion.Omitida, "Impresión automática desactivada", 0, false);

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("pulse F9 para imprimir", form.lblGuia.Text);
        });
    }

    [Fact]
    public void GrabarAsync_ConResultadoFallida_GuiaIndicaNoSeImprimioYConservaGrabado()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(
                Id: 10,
                NumeroBoleta: 55,
                FechaHoraUtc: ctx.Reloj.GetUtcNow().UtcDateTime,
                PesoTotal: 250m,
                Origen: OrigenPeso.Balanza);

            ctx.ServicioImpresion.ResultadoPorDefecto = new ResultadoImpresion(
                EstadoImpresion.Fallida, "Sin papel", 0, false);

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Contains("NO se imprimió: Sin papel", form.lblGuia.Text);
            Assert.Equal(string.Empty, form.txtPesoIzquierdo.Text);
            Assert.Single(ctx.ServicioPesajes.DtosRegistrados);
        });
    }

    [Fact]
    public void GrabarAsync_ConValidacionException_NoLlamaAServicioImpresion()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            ctx.ServicioPesajes.AlRegistrarAsync = _ =>
                throw new ValidacionException(new[] { new ErrorValidacion("Ganadero", "Requerido") });

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(ctx.ServicioImpresion.Solicitudes);
        });
    }

    // Graba un pesaje de prueba (Id y número dados) con el flujo completo de la pantalla; deja el formulario con ese
    // pesaje como "último grabado" (el que imprime F9).
    private static void GrabarPesajeDePrueba(ContextoPruebaFrmPesaje ctx, FrmPesaje form, int id, long numeroBoleta)
    {
        form.ctlGanadero.Seleccionar(1);
        form.ctlCliente.Seleccionar(10);

        ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
            1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
        form.CapturarParaTest(LadoPesaje.Izquierdo);

        ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
            2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
        form.SondearBalanzaParaTest();

        ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
            3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
        form.CapturarParaTest(LadoPesaje.Derecho);

        ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(
            Id: id,
            NumeroBoleta: numeroBoleta,
            FechaHoraUtc: ctx.Reloj.GetUtcNow().UtcDateTime,
            PesoTotal: 250m,
            Origen: OrigenPeso.Balanza);

        form.GrabarParaTestAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Layout_SinGrillaDelDia_ElHistorialYaNoVivePeroLaPantallaNoSeRecorta()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            // O3: la grilla del día es ahora FrmHistorialPesajes; el registro no consulta el día.
            Assert.Empty(form.Controls.Find("dgvHoy", true));
            Assert.DoesNotContain(AplanarControles(form), c => c is DataGridView);
            Assert.Equal(0, ctx.ServicioPesajes.VecesListarDelDiaLlamado);

            // Los pesos y las acciones pasaron a la columna derecha, al lado de los datos.
            Assert.True(form.btnGrabar.Parent!.PointToScreen(Point.Empty).X > form.ctlGanadero.PointToScreen(Point.Empty).X);

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }

    private static IEnumerable<Control> AplanarControles(Control raiz)
    {
        foreach (Control c in raiz.Controls)
        {
            yield return c;
            foreach (var h in AplanarControles(c))
            {
                yield return h;
            }
        }
    }

    [Fact]
    public void ImpresionManual_ImprimeElUltimoPesajeGrabado_SinGrabarNingunoDeshabilitaBoton()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            // Antes de grabar nada: F9 no hace nada.
            Assert.False(form.btnImprimir.TabStop);
            Assert.Null(form.IdBoletaParaImprimirParaTest);
            Assert.False(form.btnImprimir.Enabled);
            form.ProcesarTeclaParaTest(Keys.F9);
            System.Windows.Forms.Application.DoEvents();
            Assert.Empty(ctx.ServicioImpresion.Solicitudes);

            GrabarPesajeDePrueba(ctx, form, id: 5, numeroBoleta: 102);
            ctx.ServicioImpresion.Solicitudes.Clear(); // descarta la impresión automática al grabar

            Assert.Equal(5, form.IdBoletaParaImprimirParaTest);
            Assert.True(form.btnImprimir.Enabled);

            form.ProcesarTeclaParaTest(Keys.F9);
            System.Windows.Forms.Application.DoEvents();

            Assert.Single(ctx.ServicioImpresion.Solicitudes);
            Assert.Equal((5, SolicitudImpresion.Manual), ctx.ServicioImpresion.Solicitudes[0]);

            // Un segundo pesaje pasa a ser "el último".
            GrabarPesajeDePrueba(ctx, form, id: 6, numeroBoleta: 103);
            Assert.Equal(6, form.IdBoletaParaImprimirParaTest);
        });
    }

    [Fact]
    public void ImpresionManual_Concurrente_UnaSolaLlamada_YBloqueaCierreHastaCompletar()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            GrabarPesajeDePrueba(ctx, form, id: 1, numeroBoleta: 10);
            ctx.ServicioImpresion.Solicitudes.Clear();

            var tcs = new TaskCompletionSource<ResultadoImpresion>();
            ctx.ServicioImpresion.TcsImpresion = tcs;

            // Dos F9 seguidos
            form.ProcesarTeclaParaTest(Keys.F9);
            form.ProcesarTeclaParaTest(Keys.F9);

            Assert.Single(ctx.ServicioImpresion.Solicitudes);

            // Intento de cierre mientras imprime
            var e = new FormClosingEventArgs(CloseReason.UserClosing, false);
            form.SimularFormClosingParaTest(e);
            Assert.True(e.Cancel);

            // Completar impresión
            tcs.SetResult(new ResultadoImpresion(EstadoImpresion.Impresa, "Impresa", 1, true));
            System.Windows.Forms.Application.DoEvents();

            // Cierre posterior
            var e2 = new FormClosingEventArgs(CloseReason.UserClosing, false);
            form.SimularFormClosingParaTest(e2);
            Assert.False(e2.Cancel);
        });
    }

    [Fact]
    public void ProcessCmdKey_F9ConControl_NoDisparaImpresion()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            GrabarPesajeDePrueba(ctx, form, id: 1, numeroBoleta: 10);
            ctx.ServicioImpresion.Solicitudes.Clear();

            form.ProcesarTeclaParaTest(Keys.F9 | Keys.Control);
            System.Windows.Forms.Application.DoEvents();

            Assert.Empty(ctx.ServicioImpresion.Solicitudes);
        });
    }

    // ---- F10 (O5): fletero opcional elegido al pesar ----

    private static void CapturarDosLados(ContextoPruebaFrmPesaje ctx, FrmPesaje form)
    {
        ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
            1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
        form.CapturarParaTest(LadoPesaje.Izquierdo);

        ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
            2, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
        form.SondearBalanzaParaTest();

        ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
            3, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
        form.CapturarParaTest(LadoPesaje.Derecho);
    }

    [Fact]
    public void Fletero_AlCargarSeLlenaLaListaConLosActivos_YLaGrabacionSinFleteroEnviaNulo()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioFleteros.ElementosBusqueda.Add(new ElementoBusqueda(5, "1", "FLETERO UNO", "70000001"));
            ctx.ServicioFleteros.ElementosBusqueda.Add(new ElementoBusqueda(6, "12", "FLETERO DOCE", "70000012"));
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(ctx.ServicioFleteros.VecesListarParaBusqueda >= 1);
            Assert.Null(form.ctlFletero.Seleccion); // opcional: arranca sin fletero
            Assert.False(form.ctlFletero.PermitirAltaRapida);

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            CapturarDosLados(ctx, form);
            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Null(Assert.Single(ctx.ServicioPesajes.DtosRegistrados).FleteroId);
        });
    }

    [Fact]
    public void Fletero_ElegidoSeEnviaEnElDto_YSeLimpiaTrasGrabarMientrasGanaderoYClienteQuedan()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioFleteros.ElementosBusqueda.Add(new ElementoBusqueda(5, "1", "FLETERO UNO", "70000001"));
            ctx.ServicioFleteros.ElementosBusqueda.Add(new ElementoBusqueda(6, "12", "FLETERO DOCE", "70000012"));
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            form.ctlFletero.Seleccionar(6);
            CapturarDosLados(ctx, form);
            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(6, Assert.Single(ctx.ServicioPesajes.DtosRegistrados).FleteroId);
            Assert.Null(form.ctlFletero.Seleccion);
            Assert.Equal(1, form.ctlGanadero.Seleccion?.Id);
            Assert.Equal(10, form.ctlCliente.Seleccion?.Id);
        });
    }

    [Fact]
    public void Fletero_ErrorDeValidacion_SeMuestraEnElControlDelFletero()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioFleteros.ElementosBusqueda.Add(new ElementoBusqueda(5, "1", "FLETERO UNO", "70000001"));
            ctx.ServicioPesajes.ExcepcionAlRegistrar = new ValidacionException(CamposPesaje.Fletero, "El fletero seleccionado no existe o está desactivado.");
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            form.ctlFletero.Seleccionar(5);
            CapturarDosLados(ctx, form);

            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("El fletero seleccionado no existe o está desactivado.", form._errorProvider.GetError(form.ctlFletero));
            Assert.Equal(5, form.ctlFletero.Seleccion?.Id); // se conserva para corregir
        });
    }

    [Fact]
    public void Fletero_OrdenDeTabulacion_VaTrasDireccionYAntesDelColorRaza_YElSimuladorSigueEnLaPantalla()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje(Rol.Administrador);
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.txtDireccion.TabIndex < form.ctlFletero.TabIndex);
            Assert.True(form.ctlFletero.TabIndex < form.txtColorRaza.TabIndex);
            Assert.True(form.ctlCliente.TabIndex < form.txtDireccion.TabIndex);

            // El Enter desde la dirección baja al fletero (flujo por teclado).
            form.txtDireccion.Focus();
            form.ProcesarTeclaParaTest(Keys.Enter);
            Assert.True(form.ctlFletero.ContainsFocus, "Enter en Dirección debe pasar al selector de fletero");

            // El panel del simulador sigue disponible en esta pantalla (O3 solo quitó la grilla del día).
            Assert.NotNull(form.grpSimulador);
        });
    }

    // ---- F11: tipo de pesaje configurable ----

    [Theory]
    [InlineData(ModoDesposte.Ambos, true)]
    [InlineData(ModoDesposte.SoloResCompleta, false)]
    [InlineData(ModoDesposte.SoloMediaRes, false)]
    public void ModoDesposte_ConUnSoloTipo_OcultaLaFilaDesposte(ModoDesposte modo, bool visible)
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Modo = modo;
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(modo, form.ModoDesposteActual);
            Assert.Equal(visible, form.cmbDesposte.Visible);
            Assert.Equal(visible, form.lblDesposte.Visible);
        });
    }

    [Fact]
    public void ModoSoloResCompleta_ExigeLosDosLadosConRetornoACeroYGrabaResCompleta()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Modo = ModoDesposte.SoloResCompleta;
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 120m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Izquierdo);

            // Sin volver a cero no se puede capturar el otro lado (regla de res completa intacta).
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                2, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();
            Assert.False(form.btnCapturarDerecho.Enabled);
            Assert.False(form.btnGrabar.Enabled);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                3, EstadoBalanza.Conectada, 0m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                4, EstadoBalanza.Conectada, 130m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.CapturarParaTest(LadoPesaje.Derecho);

            ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(1, 99, ctx.Reloj.GetUtcNow().UtcDateTime, 250m, OrigenPeso.Balanza);
            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            var dto = Assert.Single(ctx.ServicioPesajes.DtosRegistrados);
            Assert.Equal(TipoDesposte.ResCompleta, dto.TipoDesposte);
            Assert.False(form.cmbDesposte.Visible);
        });
    }

    [Fact]
    public void ModoSoloMediaRes_UnLadoBasta_MensajeSinPrefijoYGrabaMediaRes()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Modo = ModoDesposte.SoloMediaRes;
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);

            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 115m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();
            Assert.Equal("Capture un lado (F5 o F6).", form.lblGuia.Text);

            form.CapturarParaTest(LadoPesaje.Derecho);
            Assert.True(form.btnGrabar.Enabled);

            ctx.ServicioPesajes.ResultadoRegistrar = new PesajeRegistrado(1, 99, ctx.Reloj.GetUtcNow().UtcDateTime, 115m, OrigenPeso.Balanza);
            form.GrabarParaTestAsync().GetAwaiter().GetResult();

            var dto = Assert.Single(ctx.ServicioPesajes.DtosRegistrados);
            Assert.Equal(TipoDesposte.MediaRes, dto.TipoDesposte);
            Assert.Equal(0m, dto.PesoIzquierdo);
            Assert.Equal(115m, dto.PesoDerecho);
        });
    }

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta)]
    [InlineData(ModoDesposte.SoloMediaRes)]
    public void Layout_SinLaFilaDesposte_SinRecortesNiConEscalado(ModoDesposte modo)
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Modo = modo;
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            // La fila oculta colapsa: Observación queda inmediatamente debajo de Sexo.
            Assert.True(form.txtObservacion.Top - form.cmbSexo.Bottom < form.cmbSexo.Height);

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }
    [Fact]
    public void Grabar_FleteroSinElegirLoBloqueaYElBotonSeVeDeshabilitado()
    {
        // F12 (E1, E2): un fletero escrito y no elegido bloquea el grabado; Grabar deshabilitado ya no se ve verde.
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Modo = ModoDesposte.SoloMediaRes;
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.btnGrabar.Enabled);
            Assert.Equal(SystemColors.Control, form.btnGrabar.BackColor);
            Assert.Equal(SystemColors.GrayText, form.btnGrabar.ForeColor);

            form.ctlGanadero.Seleccionar(1);
            form.ctlCliente.Seleccionar(10);
            ctx.ServicioBalanza.Instantanea = new InstantaneaBalanza(
                1, EstadoBalanza.Conectada, 115m, true, ctx.Reloj.GetUtcNow(), null, "Balanza", false);
            form.SondearBalanzaParaTest();
            form.CapturarParaTest(LadoPesaje.Derecho);

            Assert.True(form.btnGrabar.Enabled);
            Assert.Equal(Color.FromArgb(46, 139, 87), form.btnGrabar.BackColor);
            Assert.Equal(Color.White, form.btnGrabar.ForeColor);

            form.ctlFletero.CajaTexto.Text = "FLETERO QUE NO EXISTE";
            Assert.False(form.btnGrabar.Enabled);
            Assert.Equal(SystemColors.Control, form.btnGrabar.BackColor);
            Assert.Equal("Elija el fletero de la lista o borre el texto.", form.lblGuia.Text);

            form.ctlFletero.Limpiar();
            form.SondearBalanzaParaTest();
            Assert.True(form.btnGrabar.Enabled);
            Assert.Equal("Listo para grabar (F12).", form.lblGuia.Text);
        });
    }
}

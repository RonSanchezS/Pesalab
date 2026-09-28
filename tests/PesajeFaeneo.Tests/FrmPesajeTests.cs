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
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
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
        public ServicioParametrosFalso ServicioParametros { get; } = new();
        public ServicioBalanzaFalso ServicioBalanza { get; } = new();
        public SimuladorBalanzaFalso SimuladorBalanza { get; } = new();
        public SesionActual Sesion { get; } = new();
        public FormFactoryFalsa FormFactory { get; } = new();
        public FakeTimeProvider Reloj { get; } = new();

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
                ServicioParametros,
                ServicioBalanza,
                SimuladorBalanza,
                Sesion,
                FormFactory,
                Reloj);
        }
    }

    [Fact]
    public void CargarAsync_LlenaGanaderosClientesObservacionYProximaBoleta()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje();
            ctx.ServicioParametros.Parametros = new ParametrosDto(
                "EMPRESA", "DIR", "TEL", "CIU", "RS", "800", "OBS INICIAL", null, null, 42, 41, ctx.Reloj.GetUtcNow().UtcDateTime);
            ctx.ServicioPesajes.PesajesDelDia.Add(new PesajeResumen(
                Id: 1,
                NumeroBoleta: 41,
                FechaHoraUtc: ctx.Reloj.GetUtcNow().UtcDateTime,
                Marca: "AH",
                NombreGanadero: "JUAN PEREZ",
                Secuencia: 1,
                NombreCliente: "CARLOS LOPEZ",
                TipoDesposte: TipoDesposte.ResCompleta,
                PesoIzquierdo: 120m,
                PesoDerecho: 130m,
                PesoTotal: 250m,
                Estado: EstadoPesaje.Vigente,
                Origen: OrigenPeso.Balanza));

            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("OBS INICIAL", form.txtObservacion.Text);
            Assert.Contains("42", form.lblProximaBoleta.Text);
            Assert.Single(form.dgvHoy.Rows);
            Assert.Equal(41L, (long)form.dgvHoy.Rows[0].Cells[0].Value);
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
            Assert.True(ctx.ServicioPesajes.VecesListarDelDiaLlamado >= 2);
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
                ctx.ServicioGanaderos.ElementosBusqueda.Add(new ElementoBusqueda(77, dto.Marca, dto.Nombre, null));
                return Task.FromResult(77);
            };

            form.MostrarDialogoParaTest = f =>
            {
                if (f is FrmGanaderoEdicion ge)
                {
                    ge.TxtMarca.Text = "ZZ";
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
            Assert.Contains("ZZ", form.ctlGanadero.Seleccion?.Codigo);
        });
    }

    [Fact]
    public void AltaRapidaCliente_SugiereCodigoRecargaListaYSeleccionaNuevoCliente()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new ContextoPruebaFrmPesaje(Rol.Operador);
            using var form = ctx.CrearFormulario();
            form.ConfirmarCierre = false;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            ctx.ServicioClientes.SiguienteCodigoSugerido = 888;
            ctx.ServicioClientes.AlCrearRapidoAsync = dto =>
            {
                ctx.ServicioClientes.ElementosBusqueda.Add(new ElementoBusqueda(88, dto.Codigo.ToString(), dto.Nombre, dto.Direccion));
                return Task.FromResult(88);
            };

            form.MostrarDialogoParaTest = f =>
            {
                if (f is FrmClienteEdicion ce)
                {
                    Assert.Equal(888, ce.NumCodigo.Value);
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
}

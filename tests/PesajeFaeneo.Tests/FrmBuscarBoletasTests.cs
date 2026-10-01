using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Gestion;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmBuscarBoletasTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Buscar_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    private static (FrmBuscarBoletas Form, ServicioGestionPesajesFalso Falso, FormFactoryFalsa Factory, SesionActual Sesion, FakeTimeProvider Tiempo)
        CrearContexto(DateTime? ahoraUtc = null)
    {
        // Por defecto: 2026-09-28 02:00:00 UTC, que en UTC-4 es 2026-09-27 22:00:00 (cruza medianoche UTC)
        var tiempo = new FakeTimeProvider(ahoraUtc ?? new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc));
        tiempo.SetLocalTimeZone(ZonaBolivia);

        var falso = new ServicioGestionPesajesFalso();
        var factory = new FormFactoryFalsa();
        factory.Registrar(() => new FrmEditarBoleta(falso));
        factory.Registrar(() => new FrmAnularBoleta(falso));

        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "sup", "Supervisor Test", Rol.Supervisor, false));

        var form = new FrmBuscarBoletas(falso, factory, sesion, tiempo);
        return (form, falso, factory, sesion, tiempo);
    }

    [Fact]
    public void Cargar_LlenaCombosYBuscaConHoyLocal()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                falso.Opciones = new OpcionesBusquedaBoletas(
                    new[] { new ElementoBusqueda(10, "AH", "GANADERO AH") },
                    new[] { new ElementoBusqueda(20, "100", "CLIENTE 100") }
                );

                form.CargarParaTestAsync().GetAwaiter().GetResult();

                // Busca con fecha local 2026-09-27 (cruza medianoche UTC de 2026-09-28 02:00)
                var filtro = Assert.Single(falso.FiltrosRecibidos);
                Assert.Equal(new DateOnly(2026, 9, 27), filtro.Desde);
                Assert.Equal(new DateOnly(2026, 9, 27), filtro.Hasta);
                Assert.Null(filtro.NumeroBoleta);
            }
        });
    }

    [Fact]
    public void Buscar_ConTxtNumero_EnviaNumeroYFechasNulas()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                form.txtNumero.Text = "123";
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                var filtro = Assert.Single(falso.FiltrosRecibidos);
                Assert.Equal(123, filtro.NumeroBoleta);
                Assert.Null(filtro.Desde);
                Assert.Null(filtro.Hasta);
            }
        });
    }

    [Fact]
    public void Buscar_ConNumeroInvalido_MuestraErrorEnErrorProviderYNoLlamaServicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                form.txtNumero.Text = "0";
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Empty(falso.FiltrosRecibidos);
                Assert.Equal("Ingrese un número de boleta válido.", form._errorProvider.GetError(form.txtNumero));

                form.txtNumero.Text = "abc";
                form.BuscarParaTestAsync().GetAwaiter().GetResult();
                Assert.Empty(falso.FiltrosRecibidos);
                Assert.Equal("Ingrese un número de boleta válido.", form._errorProvider.GetError(form.txtNumero));
            }
        });
    }

    [Fact]
    public void Buscar_FechasDesmarcadas_EnviaDesdeYHastaNulos()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                form.txtNumero.Text = string.Empty;
                form.dtpDesde.Checked = false;
                form.dtpHasta.Checked = false;

                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                var filtro = Assert.Single(falso.FiltrosRecibidos);
                Assert.Null(filtro.Desde);
                Assert.Null(filtro.Hasta);
                Assert.Null(filtro.NumeroBoleta);
            }
        });
    }

    [Fact]
    public void Resultados_BoletaVigenteYAnulada_AnuladaGris_BotonesSegunEstado()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var bVigente = DatosGestionDePrueba.CrearBoletaEncontrada(id: 1, numeroBoleta: 101, estado: EstadoPesaje.Vigente);
                var bAnulada = DatosGestionDePrueba.CrearBoletaEncontrada(id: 2, numeroBoleta: 102, estado: EstadoPesaje.Anulado);

                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { bVigente, bAnulada }, false);

                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal(2, form.FilasParaTest.Count);

                // Fila 0: Vigente seleccionada por defecto
                Assert.True(form.btnEditar.Enabled);
                Assert.True(form.btnAnular.Enabled);

                // Fila 1: Anulada
                var rowAnulada = form.dgvResultados.Rows[1];
                Assert.Equal(Color.Gray, rowAnulada.DefaultCellStyle.ForeColor);

                // Seleccionar anulada
                form.dgvResultados.ClearSelection();
                rowAnulada.Selected = true;
                form.dgvResultados.CurrentCell = rowAnulada.Cells[0];

                Assert.False(form.btnEditar.Enabled);
                Assert.False(form.btnAnular.Enabled);
            }
        });
    }

    [Fact]
    public void Permisos_UsuarioSinAnularBoleta_BtnAnularDeshabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var sesionFalsa = new SesionFalsa();
            sesionFalsa.Usuario = new UsuarioSesion(1, "sup", "Sup", Rol.Supervisor, false);
            sesionFalsa.Permisos.Add(Permiso.EditarBoleta); // Solo EditarBoleta, sin AnularBoleta

            var falso = new ServicioGestionPesajesFalso();
            var factory = new FormFactoryFalsa();
            var tiempo = new FakeTimeProvider();

            using var form = new FrmBuscarBoletas(falso, factory, sesionFalsa, tiempo);

            var bVigente = DatosGestionDePrueba.CrearBoletaEncontrada(id: 1, estado: EstadoPesaje.Vigente);
            falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { bVigente }, false);

            form.BuscarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.btnEditar.Enabled);
            Assert.False(form.btnAnular.Enabled);
        });
    }

    [Fact]
    public void Resumen_TruncadoTrue_MuestraMensajePrimeras200_YListaVaciaMuestraNoSeEncontraron()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { DatosGestionDePrueba.CrearBoletaEncontrada() }, true);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Contains("primeras 200", form.lblResumen.Text);

                // Vacía
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(Array.Empty<BoletaEncontrada>(), false);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal("No se encontraron boletas.", form.lblResumen.Text);
            }
        });
    }

    [Fact]
    public void Teclado_EnterConFocoEnGrilla_EditaYNoRealizaNuevaBusqueda()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var bVigente = DatosGestionDePrueba.CrearBoletaEncontrada(id: 10, estado: EstadoPesaje.Vigente);
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { bVigente }, false);
                falso.BoletaParaEdicion = DatosGestionDePrueba.CrearBoletaParaEdicion(id: 10);

                form.BuscarParaTestAsync().GetAwaiter().GetResult();
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                int busquedasAntes = falso.FiltrosRecibidos.Count;
                form.dgvResultados.Focus();
                System.Windows.Forms.Application.DoEvents();
                form.MostrarDialogoParaTest = _ => DialogResult.Cancel;

                bool procesado = form.ProcesarTeclaParaTest(Keys.Enter);
                System.Windows.Forms.Application.DoEvents();
                Assert.True(procesado);

                Assert.Contains(10, falso.ObtenerEdicionRecibidos);
                // Como se canceló el diálogo, no debe haber nueva búsqueda
                Assert.Equal(busquedasAntes, falso.FiltrosRecibidos.Count);
                form.Close();
            }
        });
    }

    [Fact]
    public void Teclado_F4yF8_DisparanEditarYAnularSiHabilitado_IgnoranSiDeshabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var b = DatosGestionDePrueba.CrearBoletaEncontrada(id: 5, estado: EstadoPesaje.Vigente);
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { b }, false);
                falso.BoletaParaEdicion = DatosGestionDePrueba.CrearBoletaParaEdicion(id: 5);

                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                bool dialogoAbierto = false;
                form.MostrarDialogoParaTest = _ =>
                {
                    dialogoAbierto = true;
                    return DialogResult.Cancel;
                };

                // F4 con botón habilitado
                form.btnEditar.Enabled = true;
                bool p1 = form.ProcesarTeclaParaTest(Keys.F4);
                Assert.True(p1);
                Assert.True(dialogoAbierto);

                // F4 con botón deshabilitado
                dialogoAbierto = false;
                form.btnEditar.Enabled = false;
                bool p2 = form.ProcesarTeclaParaTest(Keys.F4);
                Assert.True(p2);
                Assert.False(dialogoAbierto);

                // F8 con botón habilitado
                dialogoAbierto = false;
                form.btnAnular.Enabled = true;
                bool p3 = form.ProcesarTeclaParaTest(Keys.F8);
                Assert.True(p3);
                Assert.True(dialogoAbierto);

                // F8 con botón deshabilitado
                dialogoAbierto = false;
                form.btnAnular.Enabled = false;
                bool p4 = form.ProcesarTeclaParaTest(Keys.F8);
                Assert.True(p4);
                Assert.False(dialogoAbierto);
            }
        });
    }

    [Fact]
    public void Editar_DialogoOkORetry_VuelveABuscarConMismoFiltroYReseleccionaId_CancelNoVuelveABuscar()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var b = DatosGestionDePrueba.CrearBoletaEncontrada(id: 99, estado: EstadoPesaje.Vigente);
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { b }, false);
                falso.BoletaParaEdicion = DatosGestionDePrueba.CrearBoletaParaEdicion(id: 99);

                form.BuscarParaTestAsync().GetAwaiter().GetResult();
                Assert.Single(falso.FiltrosRecibidos);

                // DialogResult.OK -> Vuelve a buscar
                form.MostrarDialogoParaTest = _ => DialogResult.OK;
                form.EditarParaTestAsync().GetAwaiter().GetResult();
                Assert.Equal(2, falso.FiltrosRecibidos.Count);

                // DialogResult.Retry -> Vuelve a buscar
                form.MostrarDialogoParaTest = _ => DialogResult.Retry;
                form.EditarParaTestAsync().GetAwaiter().GetResult();
                Assert.Equal(3, falso.FiltrosRecibidos.Count);

                // DialogResult.Cancel -> No vuelve a buscar
                form.MostrarDialogoParaTest = _ => DialogResult.Cancel;
                form.EditarParaTestAsync().GetAwaiter().GetResult();
                Assert.Equal(3, falso.FiltrosRecibidos.Count);
            }
        });
    }

    [Fact]
    public void Editar_ObtenerParaEdicionAsyncLanzaPesAnulada_NoAbreDialogo_MuestraAdvertenciaYRecargaGrilla()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var b = DatosGestionDePrueba.CrearBoletaEncontrada(id: 15, estado: EstadoPesaje.Vigente);
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { b }, false);
                falso.ExcepcionObtenerEdicion = new ReglaNegocioException("PES_ANULADA", "La boleta ya fue anulada por otro usuario.");

                form.BuscarParaTestAsync().GetAwaiter().GetResult();
                Assert.Single(falso.FiltrosRecibidos);

                bool dialogoAbierto = false;
                form.MostrarDialogoParaTest = _ =>
                {
                    dialogoAbierto = true;
                    return DialogResult.OK;
                };

                string? advertencia = null;
                form.MostrarAdvertenciaParaTest = msg => advertencia = msg;

                form.EditarParaTestAsync().GetAwaiter().GetResult();

                Assert.False(dialogoAbierto);
                Assert.Equal("La boleta ya fue anulada por otro usuario.", advertencia);
                // Se recargó la grilla
                Assert.Equal(2, falso.FiltrosRecibidos.Count);
            }
        });
    }

    [Fact]
    public void Filtros_UsuarioCambiaFiltroSinBuscar_RecargaTrasEditarUsaUltimoFiltroEjecutado()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var b = DatosGestionDePrueba.CrearBoletaEncontrada(id: 33, estado: EstadoPesaje.Vigente);
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { b }, false);
                falso.BoletaParaEdicion = DatosGestionDePrueba.CrearBoletaParaEdicion(id: 33);

                form.BuscarParaTestAsync().GetAwaiter().GetResult();
                var primerFiltro = falso.FiltrosRecibidos[0];
                Assert.Null(primerFiltro.NumeroBoleta);

                // Usuario tipea un número pero NO hace clic en Buscar
                form.txtNumero.Text = "999";

                form.MostrarDialogoParaTest = _ => DialogResult.OK;
                form.EditarParaTestAsync().GetAwaiter().GetResult();

                // La recarga usó el filtro anterior, sin número de boleta (R43)
                Assert.Equal(2, falso.FiltrosRecibidos.Count);
                var segundoFiltro = falso.FiltrosRecibidos[1];
                Assert.Null(segundoFiltro.NumeroBoleta);
                Assert.Equal(primerFiltro.Desde, segundoFiltro.Desde);
            }
        });
    }

    [Fact]
    public void Busqueda_RespuestaTardiaDescartada_GanaLaSegunda()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _, _, _) = CrearContexto();
            using (form)
            {
                var b1 = DatosGestionDePrueba.CrearBoletaEncontrada(id: 1, numeroBoleta: 111);
                var b2 = DatosGestionDePrueba.CrearBoletaEncontrada(id: 2, numeroBoleta: 222);

                var tcs = new TaskCompletionSource();

                // Primera búsqueda pausada
                falso.PausaBuscarAsync = () => tcs.Task;
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { b1 }, false);
                var t1 = form.BuscarParaTestAsync();

                // Segunda búsqueda sin pausa
                falso.PausaBuscarAsync = null;
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(new[] { b2 }, false);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                // Liberar primera búsqueda tardía
                tcs.SetResult();
                t1.GetAwaiter().GetResult();

                // La grilla debe conservar el resultado de la segunda búsqueda (b2)
                var fila = Assert.Single(form.FilasParaTest);
                Assert.Equal(222, fila.NumeroBoleta);
            }
        });
    }

    [Fact]
    public void Controles_AcceptButtonYCancelButton()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, _, _, _, _) = CrearContexto();
            using (form)
            {
                Assert.Same(form.btnBuscar, form.AcceptButton);
                Assert.Same(form.btnCerrar, form.CancelButton);
            }
        });
    }
}

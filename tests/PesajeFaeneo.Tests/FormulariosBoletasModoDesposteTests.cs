using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.Tests.Utilidades.Gestion;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11 (D6): historial, buscador, edición, anulación y reimpresión ocultan el desposte solo si sobra.</summary>
public class FormulariosBoletasModoDesposteTests
{
    // ---- Historial ----

    private static PesajeResumen Resumen(int id, TipoDesposte tipo) => new(
        Id: id,
        NumeroBoleta: 100 + id,
        FechaHoraUtc: new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc),
        Codigo: "AH",
        NombreGanadero: "JUAN PEREZ",
        Secuencia: id,
        NombreCliente: "CARLOS LOPEZ",
        TipoDesposte: tipo,
        PesoIzquierdo: 120m,
        PesoDerecho: tipo == TipoDesposte.ResCompleta ? 130m : 0m,
        PesoTotal: tipo == TipoDesposte.ResCompleta ? 250m : 120m,
        Estado: EstadoPesaje.Vigente,
        Origen: OrigenPeso.Balanza);

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta, false, false)]
    [InlineData(ModoDesposte.SoloResCompleta, true, true)]
    [InlineData(ModoDesposte.Ambos, false, true)]
    public void Historial_ColumnaDesposteSegunModoYDatos(ModoDesposte modo, bool conMediaRes, bool visible)
    {
        HiloSta.Ejecutar(() =>
        {
            var pesajes = new ServicioPesajesFalso();
            pesajes.PesajesDelDia.Add(Resumen(1, TipoDesposte.ResCompleta));
            if (conMediaRes)
            {
                pesajes.PesajesDelDia.Add(Resumen(2, TipoDesposte.MediaRes));
            }

            var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.FromHours(-4)));
            reloj.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("BO", TimeSpan.FromHours(-4), "BO", "BO"));
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador", Rol.Operador, false));
            var parametros = new ServicioParametrosFalso { Modo = modo };

            using var form = new FrmHistorialPesajes(pesajes, new ServicioImpresionBoletasFalso(), parametros, sesion, reloj);
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(visible, form.dgvHistorial.Columns["colDesposte"]!.Visible);
            Assert.Equal(pesajes.PesajesDelDia.Count, form.dgvHistorial.Rows.Count);
        });
    }

    // ---- Buscador y anulación ----

    private static (FrmBuscarBoletas Form, ServicioGestionPesajesFalso Falso) CrearBuscador()
    {
        var tiempo = new FakeTimeProvider(new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc));
        var falso = new ServicioGestionPesajesFalso();
        var factory = new FormFactoryFalsa();
        factory.Registrar(() => new FrmAnularBoleta(falso));
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "sup", "Supervisor", Rol.Supervisor, false));
        return (new FrmBuscarBoletas(falso, factory, sesion, tiempo), falso);
    }

    [Theory]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes, false)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.ResCompleta, true)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.MediaRes, true)]
    public void Buscador_ColumnaDesposteYResumenDeAnulacionSegunModo(ModoDesposte modo, TipoDesposte tipo, bool visible)
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso) = CrearBuscador();
            using (form)
            {
                falso.ResultadoBusqueda = new ResultadoBusquedaBoletas(
                    new[] { DatosGestionDePrueba.CrearBoletaEncontrada(tipoDesposte: tipo) }, false, modo);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal(visible, form.dgvResultados.Columns["colDesposte"]!.Visible);

                string? resumen = null;
                form.MostrarDialogoParaTest = f =>
                {
                    resumen = ((FrmAnularBoleta)f).lblResumen.Text;
                    return DialogResult.Cancel;
                };
                form.AnularParaTestAsync().GetAwaiter().GetResult();

                Assert.NotNull(resumen);
                Assert.Equal(visible, resumen.Contains("Desposte", StringComparison.Ordinal));
                Assert.Contains("Total: 120,0 kg", resumen);
            }
        });
    }

    // ---- Edición ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Editar_EtiquetaDeDesposteSegunElDto(bool mostrar)
    {
        HiloSta.Ejecutar(() =>
        {
            using var form = new FrmEditarBoleta(new ServicioGestionPesajesFalso());
            form.Configurar(DatosGestionDePrueba.CrearBoletaParaEdicion() with { MostrarDesposte = mostrar });
            form.Show();

            Assert.Equal(mostrar, form.lblDesposte.Visible);
        });
    }

    // ---- Reimpresión ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reimprimir_FilaDeDesposteSegunLaBoleta(bool mostrar)
    {
        HiloSta.Ejecutar(() =>
        {
            var boletas = new ServicioBoletasFalso
            {
                DatosRetorno = DatosBoletaDePrueba.Crear(numeroBoleta: 120, pesajeId: 15) with { MostrarDesposte = mostrar }
            };
            var parametros = new ServicioParametrosFalso();
            parametros.Parametros = parametros.Parametros with { UltimoNumeroBoletaEmitido = 120 };

            using var form = new FrmReimprimirBoleta(boletas, new ServicioImpresionBoletasFalso(), parametros);
            form.Show();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(mostrar, form.lblDesposte.Visible);
            Assert.Equal(mostrar, form.lblDesposteTitulo.Visible);
            Assert.Equal("Desposte:", form.lblDesposteTitulo.Text);
            Assert.True(form.lblTotal.Visible);
        });
    }
}

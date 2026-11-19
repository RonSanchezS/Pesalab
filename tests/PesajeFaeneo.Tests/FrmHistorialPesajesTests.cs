using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmHistorialPesajesTests
{
    private sealed class Contexto
    {
        public ServicioPesajesFalso ServicioPesajes { get; } = new();
        public ServicioImpresionBoletasFalso ServicioImpresion { get; } = new();
        public ServicioParametrosFalso ServicioParametros { get; } = new();
        public SesionActual Sesion { get; } = new();
        public FakeTimeProvider Reloj { get; } = new();

        public Contexto(Rol? rol = Rol.Operador)
        {
            // 27/09/2026 10:00 hora de Bolivia (UTC-4).
            Reloj.SetUtcNow(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(-4)));
            Reloj.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("BO", TimeSpan.FromHours(-4), "BO", "BO"));

            if (rol is not null)
            {
                Sesion.Iniciar(new UsuarioSesion(1, "usuario", "Usuario", rol.Value, false));
            }
        }

        public FrmHistorialPesajes Crear() => new(ServicioPesajes, ServicioImpresion, ServicioParametros, Sesion, Reloj);

        public PesajeResumen Resumen(int id, long numero, EstadoPesaje estado = EstadoPesaje.Vigente, string cliente = "CARLOS LOPEZ") =>
            new(
                Id: id,
                NumeroBoleta: numero,
                FechaHoraUtc: Reloj.GetUtcNow().UtcDateTime,
                Codigo: "AH",
                NombreGanadero: "JUAN PEREZ",
                Secuencia: 1,
                NombreCliente: cliente,
                TipoDesposte: TipoDesposte.ResCompleta,
                PesoIzquierdo: 120m,
                PesoDerecho: 130m,
                PesoTotal: 250m,
                Estado: estado,
                Origen: OrigenPeso.Balanza);
    }

    [Fact]
    public void Cargar_ConsultaElDiaDeHoyLocalYMuestraLasFilas()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(2, 102));
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(1, 101, EstadoPesaje.Anulado));

            using var form = ctx.Crear();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new DateOnly(2026, 9, 27), Assert.Single(ctx.ServicioPesajes.DiasConsultados));
            Assert.Equal(2, form.dgvHistorial.Rows.Count);
            Assert.Equal("102", form.dgvHistorial.Rows[0].Cells["colBoleta"].Value);
            Assert.Equal("10:00", form.dgvHistorial.Rows[0].Cells["colHora"].Value);
            Assert.Equal("AH", form.dgvHistorial.Rows[0].Cells["colCodigo"].Value);
            Assert.Equal("ANULADA", form.dgvHistorial.Rows[1].Cells["colEstado"].Value);
            Assert.Equal(Color.Gray, form.dgvHistorial.Rows[1].DefaultCellStyle.ForeColor);
            Assert.Contains("1 vigente", form.lblResumen.Text);
            Assert.Contains("1 anulada", form.lblResumen.Text);
        });
    }

    [Fact]
    public void Cargar_SinPesajes_AvisaQueNoHay()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            using var form = ctx.Crear();
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(form.dgvHistorial.Rows);
            Assert.False(form.btnReimprimir.Enabled);
            Assert.Contains("No hay pesajes", form.lblResumen.Text);
        });
    }

    [Fact]
    public void CambiarElDia_RecargaConElDiaElegido()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            ctx.ServicioPesajes.AlListarPorDiaAsync = dia => Task.FromResult<IReadOnlyList<PesajeResumen>>(
                dia.Day == 25 ? new[] { ctx.Resumen(7, 70, cliente: "CLIENTE DEL 25") } : Array.Empty<PesajeResumen>());

            using var form = ctx.Crear();
            form.dtpDia.Value = new DateTime(2026, 9, 25);

            Assert.Equal(new DateOnly(2026, 9, 25), ctx.ServicioPesajes.DiasConsultados.Last());
            Assert.Equal("CLIENTE DEL 25", Assert.Single(form.dgvHistorial.Rows.Cast<DataGridViewRow>()).Cells["colCliente"].Value);
        });
    }

    [Fact]
    public void RespuestaTardiaDeOtroDia_SeDescarta()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            var tardia = new TaskCompletionSource<IReadOnlyList<PesajeResumen>>();
            ctx.ServicioPesajes.AlListarPorDiaAsync = dia => dia.Day == 20
                ? tardia.Task
                : Task.FromResult<IReadOnlyList<PesajeResumen>>(new[] { ctx.Resumen(9, 90, cliente: "DEL DIA 21") });

            using var form = ctx.Crear();
            form.dtpDia.Value = new DateTime(2026, 9, 20); // carga en curso (pendiente)
            form.dtpDia.Value = new DateTime(2026, 9, 21); // se encadena al terminar la primera
            form.CargarParaTestAsync().GetAwaiter().GetResult(); // el día 21 responde primero
            Assert.Equal("DEL DIA 21", Assert.Single(form.dgvHistorial.Rows.Cast<DataGridViewRow>()).Cells["colCliente"].Value);

            // Llega la respuesta vieja (día 20): no debe pisar lo que se ve.
            tardia.SetResult(new[] { ctx.Resumen(8, 80, cliente: "DEL DIA 20") });
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal("DEL DIA 21", Assert.Single(form.dgvHistorial.Rows.Cast<DataGridViewRow>()).Cells["colCliente"].Value);
        });
    }

    [Fact]
    public void Reimprimir_UsaElFlujoDeImpresionManualConLaFilaSeleccionada()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(2, 102));
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(1, 101));
            ctx.ServicioImpresion.ResultadoPorDefecto = new ResultadoImpresion(EstadoImpresion.Impresa, "Boleta impresa.", 1, true);

            using var form = ctx.Crear();
            form.Show();
            System.Windows.Forms.Application.DoEvents(); // deja terminar la carga disparada por Shown
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.True(form.btnReimprimir.Visible);
            Assert.True(form.btnReimprimir.Enabled);

            form.dgvHistorial.ClearSelection();
            form.dgvHistorial.CurrentCell = form.dgvHistorial.Rows[1].Cells[0];
            form.dgvHistorial.Rows[1].Selected = true;

            form.ProcesarTeclaParaTest(Keys.F9);
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal((1, SolicitudImpresion.Manual), Assert.Single(ctx.ServicioImpresion.Solicitudes));
            Assert.Equal("Boleta impresa.", form.lblResumen.Text);
        });
    }

    [Fact]
    public void Reimprimir_Fallida_AdvierteConElMensaje()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(1, 101));
            ctx.ServicioImpresion.ResultadoPorDefecto = new ResultadoImpresion(EstadoImpresion.Fallida, "No se pudo imprimir.", 0, false);
            string? advertencia = null;

            using var form = ctx.Crear();
            form.MostrarAdvertenciaParaTest = t => advertencia = t;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            form.ReimprimirParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("No se pudo imprimir.", advertencia);
        });
    }

    [Fact]
    public void SinPermisoDeReimprimir_ElBotonQuedaOcultoYF9NoHaceNada()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto(rol: null); // sin sesión: ningún permiso
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(1, 101));

            using var form = ctx.Crear();
            form.Show();
            System.Windows.Forms.Application.DoEvents(); // deja terminar la carga disparada por Shown
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.False(form.btnReimprimir.Visible);
            Assert.False(form.btnReimprimir.Enabled);

            form.ProcesarTeclaParaTest(Keys.F9);
            form.ReimprimirParaTestAsync().GetAwaiter().GetResult();

            Assert.Empty(ctx.ServicioImpresion.Solicitudes);
        });
    }

    [Fact]
    public void F5_RecargaElDia()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            using var form = ctx.Crear();
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            var antes = ctx.ServicioPesajes.DiasConsultados.Count;

            Assert.True(form.ProcesarTeclaParaTest(Keys.F5));

            Assert.Equal(antes + 1, ctx.ServicioPesajes.DiasConsultados.Count);
        });
    }

    [Fact]
    public void Grilla_ColumnasNoOrdenablesYConCodigoEnLugarDeMarca()
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto();
            using var form = ctx.Crear();

            Assert.All(form.dgvHistorial.Columns.Cast<DataGridViewColumn>(),
                c => Assert.Equal(DataGridViewColumnSortMode.NotSortable, c.SortMode));
            var encabezados = form.dgvHistorial.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText).ToList();
            Assert.Contains("Código", encabezados);
            Assert.DoesNotContain("Marca", encabezados);
        });
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public void Layout_SinRecortesA96DpiYConEscalado(Rol rol)
    {
        HiloSta.Ejecutar(() =>
        {
            var ctx = new Contexto(rol);
            ctx.ServicioPesajes.PesajesDelDia.Add(ctx.Resumen(1, 101));
            using var form = ctx.Crear();
            form.Show();
            System.Windows.Forms.Application.DoEvents(); // deja terminar la carga disparada por Shown
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();

            VerificadorLayout.Verificar(form);
            VerificadorLayout.VerificarEscalado(form);
        });
    }
}

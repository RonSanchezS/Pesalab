using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Auditoria.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Gestion;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmVisorAuditoriaTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Auditoria_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    private static (FrmVisorAuditoria Form, ServicioAuditoriaFalso Falso, FakeTimeProvider Tiempo)
        CrearContexto(DateTime? ahoraUtc = null)
    {
        // 2026-09-28 02:00:00 UTC = 2026-09-27 22:00:00 en UTC-4
        var tiempo = new FakeTimeProvider(ahoraUtc ?? new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc));
        tiempo.SetLocalTimeZone(ZonaBolivia);

        var falso = new ServicioAuditoriaFalso();
        var form = new FrmVisorAuditoria(falso, tiempo);

        return (form, falso, tiempo);
    }

    [Fact]
    public void Cargar_LlenaUsuariosEnComboYBuscaConHoyLocal()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                falso.Usuarios.AddRange(new[]
                {
                    new UsuarioFiltro(1, "admin", "Administrador"),
                    new UsuarioFiltro(2, "operador", "Operador 1")
                });

                form.CargarParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal(3, form.cmbUsuario.Items.Count); // (Todos) + 2 usuarios

                var filtro = Assert.Single(falso.FiltrosRecibidos);
                Assert.Equal(new DateOnly(2026, 9, 27), filtro.Desde);
                Assert.Equal(new DateOnly(2026, 9, 27), filtro.Hasta);
                Assert.Null(filtro.Entidad);
                Assert.Null(filtro.Accion);
                Assert.Null(filtro.UsuarioId);
                Assert.Null(filtro.NumeroBoleta);
            }
        });
    }

    [Fact]
    public void Filtros_ValoresSeleccionadosSeEnvianAlServicio_YTodasEnvianNull()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                falso.Usuarios.Add(new UsuarioFiltro(15, "supervisor", "Sup"));
                form.CargarParaTestAsync().GetAwaiter().GetResult();

                // Configurar filtros específicos
                form.dtpDesde.Value = new DateTime(2026, 9, 1);
                form.dtpHasta.Value = new DateTime(2026, 9, 10);
                form.cmbEntidad.SelectedItem = EntidadesAuditoria.Pesaje;
                form.cmbAccion.SelectedItem = AccionAuditoria.Modificar.ToString();
                form.cmbUsuario.SelectedIndex = 1; // supervisor (id: 15)
                form.txtBoleta.Text = "555";

                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal(2, falso.FiltrosRecibidos.Count);
                var f = falso.FiltrosRecibidos[1];
                Assert.Equal(new DateOnly(2026, 9, 1), f.Desde);
                Assert.Equal(new DateOnly(2026, 9, 10), f.Hasta);
                Assert.Equal(EntidadesAuditoria.Pesaje, f.Entidad);
                Assert.Equal(AccionAuditoria.Modificar, f.Accion);
                Assert.Equal(15, f.UsuarioId);
                Assert.Equal(555, f.NumeroBoleta);

                // Volver a (Todas) / (Todos) y sin boleta
                form.cmbEntidad.SelectedIndex = 0;
                form.cmbAccion.SelectedIndex = 0;
                form.cmbUsuario.SelectedIndex = 0;
                form.txtBoleta.Text = "";
                form.dtpDesde.Checked = false;
                form.dtpHasta.Checked = false;

                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                var f2 = falso.FiltrosRecibidos[2];
                Assert.Null(f2.Desde);
                Assert.Null(f2.Hasta);
                Assert.Null(f2.Entidad);
                Assert.Null(f2.Accion);
                Assert.Null(f2.UsuarioId);
                Assert.Null(f2.NumeroBoleta);
            }
        });
    }

    [Fact]
    public void Validacion_BoletaInvalida_MuestraErrorSinLlamarAlServicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                form.txtBoleta.Text = "no-es-numero";
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Empty(falso.FiltrosRecibidos);
            }
        });
    }

    [Fact]
    public void Detalle_RegistroModificarPesaje_CalculaDiferenciasYResalta()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, _, _) = CrearContexto();
            using (form)
            {
                var antes = new
                {
                    NumeroBoleta = 100,
                    Direccion = "Av. Principal 123",
                    ColorRaza = "Negro",
                    Observacion = (string?)null
                };

                var despues = new
                {
                    NumeroBoleta = 100,
                    Direccion = "Av. Secundaria 456",
                    ColorRaza = "Negro",
                    Observacion = "Nota editada"
                };

                string antesJson = JsonSerializer.Serialize(antes);
                string despuesJson = JsonSerializer.Serialize(despues);

                var reg = new RegistroAuditoria(
                    Id: 1,
                    FechaHoraLocal: new DateTime(2026, 9, 27, 10, 0, 0),
                    Usuario: "admin",
                    Accion: AccionAuditoria.Modificar,
                    Entidad: EntidadesAuditoria.Pesaje,
                    EntidadId: "100",
                    Motivo: "Corrección de dirección",
                    DatosAntesJson: antesJson,
                    DatosDespuesJson: despuesJson);

                form.MostrarDetalleParaTest(reg);

                Assert.Equal(antesJson, form.txtAntesJson.Text);
                Assert.Equal(despuesJson, form.txtDespuesJson.Text);

                // dgvCambios debe contener las filas con nombres legibles
                var filas = form.dgvCambios.Rows.Cast<DataGridViewRow>().ToList();
                Assert.Equal(4, filas.Count);

                // Fila Dirección: cambió (resaltada en amarillo)
                var filaDir = filas.First(r => (string)r.Cells["colCampo"].Value == "Dirección");
                Assert.Equal("Av. Principal 123", filaDir.Cells["colAntes"].Value);
                Assert.Equal("Av. Secundaria 456", filaDir.Cells["colDespues"].Value);
                Assert.Equal(Color.FromArgb(255, 255, 204), filaDir.DefaultCellStyle.BackColor);

                // Fila N° de boleta: no cambió (color blanco)
                var filaNum = filas.First(r => (string)r.Cells["colCampo"].Value == "N° de boleta");
                Assert.Equal("100", filaNum.Cells["colAntes"].Value);
                Assert.Equal("100", filaNum.Cells["colDespues"].Value);
                Assert.Equal(Color.White, filaNum.DefaultCellStyle.BackColor);
            }
        });
    }

    [Fact]
    public void Detalle_RegistroCrear_AntesEsNuloYMuestraGuiones()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, _, _) = CrearContexto();
            using (form)
            {
                var despues = new { Nombre = "Juan Pérez", Activo = true };
                string despuesJson = JsonSerializer.Serialize(despues);

                var reg = new RegistroAuditoria(
                    Id: 2,
                    FechaHoraLocal: new DateTime(2026, 9, 27, 11, 0, 0),
                    Usuario: "admin",
                    Accion: AccionAuditoria.Crear,
                    Entidad: EntidadesAuditoria.Cliente,
                    EntidadId: "1",
                    Motivo: null,
                    DatosAntesJson: null,
                    DatosDespuesJson: despuesJson);

                form.MostrarDetalleParaTest(reg);

                Assert.Empty(form.txtAntesJson.Text);
                Assert.Equal(despuesJson, form.txtDespuesJson.Text);

                var filas = form.dgvCambios.Rows.Cast<DataGridViewRow>().ToList();
                Assert.NotEmpty(filas);
                foreach (var f in filas)
                {
                    Assert.Equal("—", f.Cells["colAntes"].Value);
                }
            }
        });
    }

    [Fact]
    public void Detalle_JsonInvalido_MuestraFilaDatosSinLanzar()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, _, _) = CrearContexto();
            using (form)
            {
                var reg = new RegistroAuditoria(
                    Id: 3,
                    FechaHoraLocal: new DateTime(2026, 9, 27, 12, 0, 0),
                    Usuario: "admin",
                    Accion: AccionAuditoria.Modificar,
                    Entidad: "Otra",
                    EntidadId: "1",
                    Motivo: null,
                    DatosAntesJson: "{ invalido",
                    DatosDespuesJson: "texto");

                form.MostrarDetalleParaTest(reg);

                var fila = Assert.Single(form.dgvCambios.Rows.Cast<DataGridViewRow>());
                Assert.Equal("(datos)", fila.Cells["colCampo"].Value);
            }
        });
    }

    [Fact]
    public void Resumen_TruncadoTrue_MensajePrimeros500_ListaVacia_NoHayRegistrosYDetalleVacio()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                var reg = new RegistroAuditoria(1, DateTime.Now, "admin", AccionAuditoria.Crear, "Pesaje", "1", null, null, "{}");
                falso.ResultadoConsultar = new ResultadoAuditoria(new[] { reg }, true);

                form.BuscarParaTestAsync().GetAwaiter().GetResult();
                Assert.Contains("primeros 500", form.lblResumen.Text);

                // Caso vacío
                falso.ResultadoConsultar = new ResultadoAuditoria(Array.Empty<RegistroAuditoria>(), false);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal("No hay registros para esos filtros.", form.lblResumen.Text);
                Assert.Empty(form.dgvCambios.Rows.Cast<DataGridViewRow>());
                Assert.Empty(form.txtAntesJson.Text);
                Assert.Empty(form.txtDespuesJson.Text);
            }
        });
    }

    [Fact]
    public void Fila_LoginFallido_EntidadIdEnmascaradoSeMuestraTalCual()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                var reg = new RegistroAuditoria(
                    Id: 10,
                    FechaHoraLocal: new DateTime(2026, 9, 27, 15, 30, 0),
                    Usuario: "(sistema)",
                    Accion: AccionAuditoria.LoginFallido,
                    Entidad: EntidadesAuditoria.Usuario,
                    EntidadId: "***",
                    Motivo: "Intento fallido",
                    DatosAntesJson: null,
                    DatosDespuesJson: null);

                falso.ResultadoConsultar = new ResultadoAuditoria(new[] { reg }, false);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                var fila = Assert.Single(form.FilasParaTest);
                Assert.Equal("***", fila.EntidadId);

                var dgvRow = form.dgvRegistros.Rows[0];
                Assert.Equal("***", dgvRow.Cells["colEntidadId"].Value);
                Assert.Equal("LoginFallido", dgvRow.Cells["colAccion"].Value);
            }
        });
    }

    [Fact]
    public void Busqueda_RespuestaTardiaDescartada_GanaLaSegunda()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                var reg1 = new RegistroAuditoria(1, DateTime.Now, "u1", AccionAuditoria.Crear, "Pesaje", "1", null, null, null);
                var reg2 = new RegistroAuditoria(2, DateTime.Now, "u2", AccionAuditoria.Modificar, "Pesaje", "2", null, null, null);

                var tcs = new TaskCompletionSource();
                falso.PausaConsultarAsync = () => tcs.Task;
                falso.ResultadoConsultar = new ResultadoAuditoria(new[] { reg1 }, false);
                var t1 = form.BuscarParaTestAsync();

                falso.PausaConsultarAsync = null;
                falso.ResultadoConsultar = new ResultadoAuditoria(new[] { reg2 }, false);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                tcs.SetResult();
                t1.GetAwaiter().GetResult();

                var fila = Assert.Single(form.FilasParaTest);
                Assert.Equal(2, fila.Id);
            }
        });
    }

    [Fact]
    public void SeguridadUI_SoloLectura_SoloExistenBotonesBuscarLimpiarCerrar()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, _, _) = CrearContexto();
            using (form)
            {
                var botones = ObtenerBotonesRecursivo(form);

                Assert.Equal(3, botones.Count);
                Assert.Contains(form.btnBuscar, botones);
                Assert.Contains(form.btnLimpiar, botones);
                Assert.Contains(form.btnCerrar, botones);

                // Ningún botón permite editar, borrar o exportar
                foreach (var b in botones)
                {
                    string texto = b.Text.ToLowerInvariant();
                    Assert.DoesNotContain("editar", texto);
                    Assert.DoesNotContain("modificar", texto);
                    Assert.DoesNotContain("borrar", texto);
                    Assert.DoesNotContain("eliminar", texto);
                    Assert.DoesNotContain("exportar", texto);
                }
            }
        });
    }

    [Fact]
    public void Teclado_AcceptButtonYCancelButton_EnterConGrillaEnfocadaNoBusca()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, falso, _) = CrearContexto();
            using (form)
            {
                Assert.Same(form.btnBuscar, form.AcceptButton);
                Assert.Same(form.btnCerrar, form.CancelButton);

                var reg = new RegistroAuditoria(1, DateTime.Now, "u1", AccionAuditoria.Crear, "Pesaje", "1", null, null, null);
                falso.ResultadoConsultar = new ResultadoAuditoria(new[] { reg }, false);
                form.BuscarParaTestAsync().GetAwaiter().GetResult();

                form.Show();
                System.Windows.Forms.Application.DoEvents();
                int llamadasAntes = falso.FiltrosRecibidos.Count;

                form.dgvRegistros.Focus();
                System.Windows.Forms.Application.DoEvents();

                bool consumido = form.ProcesarTeclaParaTest(Keys.Enter);
                System.Windows.Forms.Application.DoEvents();

                Assert.True(consumido);
                Assert.Equal(llamadasAntes, falso.FiltrosRecibidos.Count);

                form.Close();
            }
        });
    }

    [Fact]
    public void Detalle_FechasConvertidasAHoraLocal()
    {
        HiloSta.Ejecutar(() =>
        {
            var (form, _, _) = CrearContexto();
            using (form)
            {
                // 2026-09-28T02:30:00Z en UTC-4 es 2026-09-27T22:30:00
                var despues = new { FechaHora = "2026-09-28T02:30:00Z" };
                string despuesJson = JsonSerializer.Serialize(despues);

                var reg = new RegistroAuditoria(
                    Id: 5,
                    FechaHoraLocal: new DateTime(2026, 9, 27, 22, 30, 0),
                    Usuario: "admin",
                    Accion: AccionAuditoria.Crear,
                    Entidad: EntidadesAuditoria.Pesaje,
                    EntidadId: "1",
                    Motivo: null,
                    DatosAntesJson: null,
                    DatosDespuesJson: despuesJson);

                form.MostrarDetalleParaTest(reg);

                var filaFecha = Assert.Single(form.dgvCambios.Rows.Cast<DataGridViewRow>());
                Assert.Equal("Fecha y hora", filaFecha.Cells["colCampo"].Value);
                Assert.Equal("27/09/2026 22:30:00", filaFecha.Cells["colDespues"].Value);
            }
        });
    }

    private static List<Button> ObtenerBotonesRecursivo(Control control)
    {
        var lista = new List<Button>();
        foreach (Control c in control.Controls)
        {
            if (c is Button b)
            {
                lista.Add(b);
            }
            if (c.HasChildren)
            {
                lista.AddRange(ObtenerBotonesRecursivo(c));
            }
        }
        return lista;
    }
}

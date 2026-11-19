#if CAPTURAS_MANUAL
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Auditoria.Dtos;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Reportes;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.WinForms.Capturas;

internal sealed partial class GuionCapturas
{
    private static readonly Size TamanoGrande = new(1340, 660);
    private static readonly Size TamanoBuscar = new(1370, 660);

    private static DateTime Hoy => DateTime.Today;

    private async Task EscenasSupervisorAsync()
    {
        await IniciarSesionAsync(_cfg.Supervisor.NombreUsuario, _cfg.Supervisor.Password);
        await ForzarPesoAsync(0);

        // ───── Buscar / editar / anular boletas ─────
        await Escena("su-01", async () =>
        {
            var f = await AbrirAsync<FrmBuscarBoletas>(tamano: TamanoBuscar);
            f.dtpDesde.Checked = true;
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Checked = true;
            f.dtpHasta.Value = Hoy;
            f.cmbEstado.SelectedIndex = 0;
            Clic(f.btnBuscar);
            await EstabilizarAsync(f);
            f.dgvResultados.ClearSelection();
            f.dgvResultados.Rows[0].Selected = true;
            f.dgvResultados.CurrentCell = f.dgvResultados.Rows[0].Cells[0];
            await Task.Delay(300);
            FotoSolo("su-01-buscar-boletas", "Buscar boletas",
                "Pantalla «Buscar / editar boleta» con los filtros (fechas, número, estado, ganadero y cliente) y los resultados; las boletas anuladas se ven en gris.",
                f, f.dtpDesde, f.dtpHasta, f.txtNumero, f.cmbEstado, f.ctlGanadero, f.ctlCliente, f.btnBuscar, f.btnLimpiar, f.dgvResultados, f.lblResumen, f.btnEditar, f.btnAnular, f.btnCerrar);

            // Una fila anulada seleccionada: Editar y Anular se desactivan.
            var iAnulada = f.dgvResultados.Rows.Cast<DataGridViewRow>().ToList()
                .FindIndex(r => r.DefaultCellStyle.ForeColor == Color.Gray || r.DefaultCellStyle.ForeColor == Color.DimGray || r.DefaultCellStyle.ForeColor == SystemColors.GrayText);
            if (iAnulada < 0)
            {
                iAnulada = f.FilasParaTest.ToList().FindIndex(b => b.Estado == EstadoPesaje.Anulado);
            }

            if (iAnulada >= 0)
            {
                f.dgvResultados.ClearSelection();
                f.dgvResultados.Rows[iAnulada].Selected = true;
                f.dgvResultados.CurrentCell = f.dgvResultados.Rows[iAnulada].Cells[0];
                await Task.Delay(300);
                FotoSolo("su-02-buscar-anulada-seleccionada", "Boleta anulada seleccionada",
                    "Una boleta anulada se ve en gris y no se puede editar ni anular otra vez: los botones «Editar» y «Anular» quedan desactivados.",
                    f, f.dgvResultados, f.btnEditar, f.btnAnular);
            }

            f.cmbEstado.SelectedIndex = 2;
            Clic(f.btnBuscar);
            await EstabilizarAsync(f);
            FotoSolo("su-03-buscar-solo-anuladas", "Buscar solo boletas anuladas",
                "Con el estado «Anulado» solo se listan las boletas anuladas.", f, f.cmbEstado, f.btnBuscar, f.dgvResultados, f.lblResumen);

            f.cmbEstado.SelectedIndex = 0;
            await ElegirAsync(f.ctlGanadero, "pampa");
            Clic(f.btnBuscar);
            await EstabilizarAsync(f);
            FotoSolo("su-04-buscar-por-ganadero", "Buscar boletas de un ganadero",
                "Los filtros se combinan: aquí, las boletas de un ganadero en los últimos días.", f, f.ctlGanadero, f.btnBuscar, f.dgvResultados, f.lblResumen);
        });

        await Escena("su-05", async () =>
        {
            using var scope = _servicios.CreateScope();
            var gestion = scope.ServiceProvider.GetRequiredService<IServicioGestionPesajes>();
            var res = await gestion.BuscarAsync(new FiltroBoletas(DateOnly.FromDateTime(Hoy.AddDays(-1)), DateOnly.FromDateTime(Hoy), null, null, null, EstadoPesaje.Vigente));
            var boleta = res.Boletas.First();
            var dto = await gestion.ObtenerParaEdicionAsync(boleta.Id);
            var f = await AbrirAsync<FrmEditarBoleta>(c => c.Configurar(dto), new Size(760, 720));
            await Task.Delay(300);
            FotoSolo("su-05-editar-boleta", "Editar boleta",
                "Ventana «Editar boleta»: se pueden cambiar el ganadero, el cliente, el fletero, la dirección, el color o raza, el sexo y la observación; los pesos no se pueden modificar.",
                f, f.lblTitulo, f.txtPesoIzquierdo, f.txtPesoDerecho, f.txtPesoTotal, f.ctlGanadero, f.numSecuencia, f.ctlCliente, f.ctlFletero, f.txtDireccion, f.txtColorRaza,
                f.cmbSexo, f.txtObservacion, f.txtMotivo, f.btnGuardar, f.btnCancelar);
            f.txtColorRaza.Text = "NELORE CRUZADO";
            f.txtMotivo.Text = "Corrección de raza";
            await Task.Delay(200);
            FotoSolo("su-06-editar-boleta-motivo", "Editar boleta con motivo",
                "Tras cambiar un dato se escribe el motivo del cambio y se pulsa «Guardar»; la modificación queda en la auditoría.",
                f, f.txtColorRaza, f.txtMotivo, f.btnGuardar);
        });

        await Escena("su-07", async () =>
        {
            using var scope = _servicios.CreateScope();
            var gestion = scope.ServiceProvider.GetRequiredService<IServicioGestionPesajes>();
            var res = await gestion.BuscarAsync(new FiltroBoletas(DateOnly.FromDateTime(Hoy.AddDays(-1)), DateOnly.FromDateTime(Hoy), null, null, null, EstadoPesaje.Vigente));
            var boleta = res.Boletas.First();
            var f = await AbrirAsync<FrmAnularBoleta>(c => c.Configurar(boleta), new Size(620, 420));
            f.txtMotivo.Text = "Pesaje duplicado por error de captura";
            await Task.Delay(300);
            FotoSolo("su-07-anular-boleta", "Anular boleta",
                "Para anular una boleta se escribe el motivo (obligatorio) y se pulsa «Anular boleta»; la anulación no se puede deshacer.",
                f, f.lblTitulo, f.lblResumen, f.lblAdvertencia, f.txtMotivo, f.btnAnular, f.btnCancelar);
        });

        // ───── Reportes ─────
        await Escena("su-10", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.PesajesDelDia), TamanoGrande);
            f.dtpDesde.Value = Hoy;
            await Task.Delay(300);
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoSolo("su-10-reporte-pesajes-del-dia", "Reporte: pesajes del día",
                "Reporte «Pesajes del día»: se elige el tipo, la fecha y se pulsa «Generar»; abajo aparecen los totales de reses y kilos.",
                f, f.cmbTipo, f.dtpDesde, f.dtpHasta, f.btnGenerar, f.dgvVista, f.lblTotales, f.btnVistaPrevia, f.btnImprimir, f.btnPdf, f.btnExcel, f.btnCerrar);

            f.cmbTipo.SelectedIndex = IndiceTipo(f, TipoReporte.PesajesEntreFechas);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            await Task.Delay(300);
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoSolo("su-11-reporte-entre-fechas", "Reporte: pesajes entre fechas",
                "Reporte «Pesajes entre fechas»: se indican la fecha inicial y la final.", f, f.cmbTipo, f.dtpDesde, f.dtpHasta, f.btnGenerar, f.dgvVista, f.lblTotales);

            // Vista previa del reporte (sin imprimir).
            var reporte = f.ReporteActual!;
            using var scope = _servicios.CreateScope();
            var paginas = await _servicios.GetRequiredService<IGeneradorPdfReportes>().GenerarPaginasAsync(reporte, ReglasReportes.ResolucionImpresionDpi);
            var vista = new FrmVistaPrevia(new DocumentoPaginasReporte(paginas, paraVistaPrevia: true, reporte.Titulo), "Vista previa — " + reporte.Titulo);
            Preparar(vista, new Size(900, 760));
            _abiertos.Add(vista);
            vista.Show();
            Capturador.QuitarEsquinasRedondeadas(vista);
            vista.Activate();
            await Task.Delay(2500);
            FotoSolo("su-12-reporte-vista-previa", "Vista previa de un reporte",
                "La vista previa muestra cómo saldrá el reporte impreso (misma presentación que el PDF).", vista, vista.ppcVista, vista.btnCerrar);
            vista.Close();
        });

        await Escena("su-13", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.PorGanadero), TamanoGrande);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            await ElegirAsync(f.ctlGanadero, "pampa");
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoSolo("su-13-reporte-por-ganadero", "Reporte: pesajes por ganadero",
                "Reporte «Pesajes por ganadero»: además de las fechas se elige el ganadero.", f, f.cmbTipo, f.dtpDesde, f.dtpHasta, f.ctlGanadero, f.btnGenerar, f.dgvVista, f.lblTotales);
        });

        await Escena("su-14", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.PorCliente), TamanoGrande);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            await ElegirAsync(f.ctlCliente, "carn");
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoSolo("su-14-reporte-por-cliente", "Reporte: pesajes por cliente",
                "Reporte «Pesajes por cliente»: se elige el cliente y las fechas.", f, f.cmbTipo, f.dtpDesde, f.dtpHasta, f.ctlCliente, f.btnGenerar, f.dgvVista, f.lblTotales);
        });

        await Escena("su-15", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.PorRaza), TamanoGrande);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            await Task.Delay(300);
            f.cmbRaza.Focus();
            f.cmbRaza.DroppedDown = true;
            await Task.Delay(500);
            FotoSolo("su-15-reporte-por-raza-lista", "Reporte por raza: elegir la raza",
                "En «Pesajes por raza» se elige la raza de la lista (aparecen las razas ya registradas).", f, f.cmbTipo, f.cmbRaza);
            f.cmbRaza.DroppedDown = false;
            var iNelore = f.cmbRaza.Items.Cast<object>().ToList().FindIndex(o => o.ToString()!.Contains("NELORE", StringComparison.OrdinalIgnoreCase));
            f.cmbRaza.SelectedIndex = iNelore >= 0 ? iNelore : 0;
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoSolo("su-16-reporte-por-raza", "Reporte: pesajes por raza",
                "Resultado del reporte por raza con los totales de la raza elegida.", f, f.cmbTipo, f.cmbRaza, f.btnGenerar, f.dgvVista, f.lblTotales);
        });

        await Escena("su-17", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.Anuladas), TamanoGrande);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoSolo("su-17-reporte-anuladas", "Reporte de boletas anuladas",
                "Reporte «Boletas anuladas»: incluye el motivo y quién anuló cada boleta.", f, f.cmbTipo, f.btnGenerar, f.dgvVista, f.lblTotales);
        });

        // ───── Catálogos ─────
        await Escena("su-20", async () =>
        {
            var f = await AbrirAsync<FrmGanaderos>(tamano: new Size(960, 640));
            await Task.Delay(300);
            FotoSolo("su-20-catalogo-ganaderos", "Catálogo de ganaderos",
                "Lista de ganaderos con su código (automático), nombre y si están activos; arriba, los botones para crear, editar y activar o desactivar.", f);

            var nuevo = await AbrirAsync<FrmGanaderoEdicion>(c => c.ConfigurarAlta(), new Size(520, 300));
            FotoSolo("su-21-ganadero-nuevo", "Nuevo ganadero",
                "En un ganadero nuevo el código aparece como «(se asigna al guardar)»: se escribe solo el nombre.",
                nuevo, nuevo.txtCodigo, nuevo.txtNombre, nuevo.btnGuardar, nuevo.btnCancelar);
            nuevo.Close();

            using var scope = _servicios.CreateScope();
            var lista = await scope.ServiceProvider.GetRequiredService<IServicioGanaderos>().ListarAsync(false);
            var editar = await AbrirAsync<FrmGanaderoEdicion>(c => c.ConfigurarEdicion(lista[0]), new Size(520, 300));
            FotoSolo("su-22-ganadero-editar", "Editar ganadero",
                "Al editar, el código se ve pero no se puede cambiar; solo se modifica el nombre.", editar, editar.txtCodigo, editar.txtNombre, editar.btnGuardar, editar.btnCancelar);
        });

        await Escena("su-23", async () =>
        {
            var f = await AbrirAsync<FrmClientes>(tamano: new Size(1024, 640));
            await Task.Delay(300);
            FotoSolo("su-23-catalogo-clientes", "Catálogo de clientes",
                "Lista de clientes con su código (automático), nombre y dirección.", f);

            var nuevo = await AbrirAsync<FrmClienteEdicion>(c => c.ConfigurarAlta(), new Size(560, 340));
            FotoSolo("su-24-cliente-nuevo", "Nuevo cliente",
                "Cliente nuevo: el código se asigna al guardar; se escribe el nombre y la dirección.",
                nuevo, nuevo.txtCodigo, nuevo.txtNombre, nuevo.txtDireccion, nuevo.btnGuardar, nuevo.btnCancelar);
            nuevo.Close();

            using var scope = _servicios.CreateScope();
            var lista = await scope.ServiceProvider.GetRequiredService<IServicioClientes>().ListarAsync(false);
            var editar = await AbrirAsync<FrmClienteEdicion>(c => c.ConfigurarEdicion(lista[0]), new Size(560, 340));
            FotoSolo("su-25-cliente-editar", "Editar cliente",
                "Al editar un cliente se puede cambiar el nombre y la dirección, no el código.", editar, editar.txtCodigo, editar.txtNombre, editar.txtDireccion, editar.btnGuardar, editar.btnCancelar);
        });

        await Escena("su-26", async () =>
        {
            var f = await AbrirAsync<FrmFleteros>(tamano: new Size(960, 600));
            await Task.Delay(300);
            FotoSolo("su-26-catalogo-fleteros", "Catálogo de fleteros",
                "Lista de fleteros con su código (automático), nombre y celular. No hay límite de fleteros.", f);

            var nuevo = await AbrirAsync<FrmFleteroEdicion>(c => c.ConfigurarAlta(), new Size(520, 340));
            FotoSolo("su-27-fletero-nuevo", "Nuevo fletero",
                "Fletero nuevo: el código se asigna al guardar; se escribe el nombre y el celular.",
                nuevo, nuevo.txtCodigo, nuevo.txtNombre, nuevo.txtCelular, nuevo.btnGuardar, nuevo.btnCancelar);
            nuevo.Close();

            using var scope = _servicios.CreateScope();
            var lista = await scope.ServiceProvider.GetRequiredService<IServicioFleteros>().ListarAsync(false);
            var editar = await AbrirAsync<FrmFleteroEdicion>(c => c.ConfigurarEdicion(lista[0]), new Size(520, 340));
            FotoSolo("su-28-fletero-editar", "Editar fletero",
                "Al editar un fletero se cambia el nombre o el celular; el código queda fijo.", editar, editar.txtCodigo, editar.txtNombre, editar.txtCelular, editar.btnGuardar, editar.btnCancelar);
        });

        // ───── Visor de auditoría ─────
        await Escena("su-30", async () =>
        {
            var f = await AbrirAsync<FrmVisorAuditoria>(tamano: new Size(1024, 700));
            f.dtpDesde.Checked = true;
            f.dtpDesde.Value = Hoy.AddDays(-6);
            f.dtpHasta.Checked = true;
            f.dtpHasta.Value = Hoy;
            Clic(f.btnBuscar);
            await EstabilizarAsync(f);
            FotoSolo("su-30-auditoria", "Visor de auditoría",
                "El visor de auditoría lista quién hizo qué y cuándo (altas, cambios, anulaciones, inicios de sesión…), con filtros por fecha, tipo, acción, usuario y boleta.",
                f, f.dtpDesde, f.dtpHasta, f.cmbEntidad, f.cmbAccion, f.cmbUsuario, f.txtBoleta, f.btnBuscar, f.dgvRegistros, f.lblResumen);

            var i = f.dgvRegistros.Rows.Cast<DataGridViewRow>().ToList().FindIndex(r => r.Tag is RegistroAuditoria a && a.Accion == AccionAuditoria.Modificar && a.Entidad == "Pesaje");
            if (i < 0)
            {
                throw new InvalidOperationException("No hay un registro de modificación de pesaje para mostrar.");
            }

            f.dgvRegistros.ClearSelection();
            f.dgvRegistros.Rows[i].Selected = true;
            f.dgvRegistros.CurrentCell = f.dgvRegistros.Rows[i].Cells[0];
            // Más alto para el detalle (los cambios) y la fila elegida arriba.
            var division = Capturador.Descendientes(f).OfType<SplitContainer>().First();
            division.SplitterDistance = Math.Max(division.Panel1MinSize, 150);
            f.dgvRegistros.FirstDisplayedScrollingRowIndex = i;
            f.MostrarDetalleParaTest((RegistroAuditoria)f.dgvRegistros.Rows[i].Tag!);
            f.tabDetalle.SelectedIndex = 0;
            var cambiada = f.dgvCambios.Rows.Cast<DataGridViewRow>().ToList().FindIndex(r => r.DefaultCellStyle.BackColor == Color.FromArgb(255, 255, 204));
            if (cambiada > 0)
            {
                f.dgvCambios.FirstDisplayedScrollingRowIndex = Math.Max(0, cambiada - 1);
            }

            await Task.Delay(400);
            FotoSolo("su-31-auditoria-cambios", "Auditoría: pestaña Cambios",
                "Al elegir un registro, la pestaña «Cambios» muestra qué dato cambió, con el valor anterior y el nuevo (en amarillo lo modificado).",
                f, f.dgvRegistros, f.tabDetalle, f.dgvCambios);

            f.tabDetalle.SelectedIndex = 1;
            f.txtAntesJson.Select(0, 0);
            f.txtDespuesJson.Select(0, 0);
            f.dgvRegistros.Focus();
            await Task.Delay(400);
            FotoSolo("su-32-auditoria-json", "Auditoría: pestaña JSON",
                "La pestaña «JSON» muestra el detalle técnico completo del registro, antes y después del cambio.", f, f.tabDetalle, f.txtAntesJson, f.txtDespuesJson);
        });
    }

    private static int IndiceTipo(FrmReportes f, TipoReporte tipo)
    {
        for (var i = 0; i < f.cmbTipo.Items.Count; i++)
        {
            var o = f.cmbTipo.Items[i]!;
            var propiedad = o.GetType().GetProperty("Tipo");
            if (propiedad?.GetValue(o) is TipoReporte t && t == tipo)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"El reporte {tipo} no está en la lista.");
    }
}
#endif

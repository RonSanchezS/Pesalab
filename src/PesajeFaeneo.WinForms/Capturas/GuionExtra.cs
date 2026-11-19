#if CAPTURAS_MANUAL
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza.Demo;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.PuestaEnMarcha;
using PesajeFaeneo.WinForms.Formularios.Reportes;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>Segunda ronda de capturas: recortes de detalle (menús, barra de estado, filtros), variantes por rol y estados nuevos.</summary>
internal sealed partial class GuionCapturas
{
    // ───────────────────────── Recortes ─────────────────────────

    /// <summary>Barra de menús (hasta el último menú) más el desplegable abierto de <paramref name="item"/>, en coordenadas de pantalla.</summary>
    private static Rectangle RegionMenu(FrmPrincipal shell, ToolStripMenuItem item)
    {
        var barra = shell.MenuPrincipal;
        var rb = barra.RectangleToScreen(barra.ClientRectangle);
        var ultimo = barra.Items.Cast<ToolStripItem>().Where(i => i.Available).Max(i => i.Bounds.Right);
        var derecha = Math.Max(item.DropDown.Bounds.Right, barra.RectangleToScreen(new Rectangle(ultimo, 0, 0, 0)).X);
        return Rectangle.FromLTRB(rb.Left, rb.Top, derecha, item.DropDown.Bounds.Bottom);
    }

    private static Rectangle RegionItem(ToolStrip tira, ToolStripItem item) => tira.RectangleToScreen(item.Bounds);

    /// <summary>Abre el menú y captura solo la barra de menús con su desplegable (id con sufijo -detalle).</summary>
    private async Task MenuDetalleAsync(FrmPrincipal shell, string id, string menu, string descripcion)
    {
        await AbrirMenuAsync(shell, menu);
        var item = shell.MenuPrincipal.Items.OfType<ToolStripMenuItem>().First(i => i.Text!.Replace("&", string.Empty) == menu);
        await EsperarHastaAsync(() => item.DropDown.Visible && item.DropDown.Width > 0, 5000, "desplegable del menu");
        await Task.Delay(500);
        var destacados = new List<object?> { item };
        destacados.AddRange(item.DropDownItems.Cast<ToolStripItem>().Where(i => i is not ToolStripSeparator));
        FotoRecorte(id, $"Menú {menu} (detalle)", descripcion, shell, RegionMenu(shell, item), 6, destacados.ToArray());
        CerrarMenus(shell);
    }

    /// <summary>Abre y cierra cada menu una vez: el primer desplegable que se crea tarda en pintarse y salia vacio en la captura.</summary>
    private async Task CalentarMenusAsync(FrmPrincipal shell)
    {
        foreach (var m in shell.MenuPrincipal.Items.OfType<ToolStripMenuItem>().Where(m => m.Available))
        {
            shell.Activate();
            m.ShowDropDown();
            await Task.Delay(200);
            m.HideDropDown();
        }

        await Task.Delay(300);
    }

    /// <summary>Captura el menu abierto en la ventana completa y, aparte, el recorte de detalle.</summary>
    private async Task MenuCompletoYDetalleAsync(FrmPrincipal shell, string id, string menu, string descripcion)
    {
        await AbrirMenuAsync(shell, menu);
        var item = shell.MenuPrincipal.Items.OfType<ToolStripMenuItem>().First(i => i.Text!.Replace("&", string.Empty) == menu);
        await EsperarHastaAsync(() => item.DropDown.Visible && item.DropDown.Width > 0, 5000, "desplegable del menu");
        await Task.Delay(500);
        FotoSolo(id, $"Menú {menu}", descripcion, shell);
        CerrarMenus(shell);
        await MenuDetalleAsync(shell, id + "-detalle", menu, descripcion);
    }

    /// <summary>Barra de estado completa y, aparte, solo el segmento de la balanza.</summary>
    private void FotosBarraEstado(FrmPrincipal shell, string id, string titulo, string descripcion)
    {
        var barra = Campo<StatusStrip>(shell, "statusStrip");
        FotoRegion(id, titulo, descripcion, shell, barra, shell.LblStatusBalanza, shell.LblStatusEmpresa);
        FotoRecorte(id + "-detalle", titulo + " (detalle)", descripcion, shell, RegionItem(barra, shell.LblStatusBalanza), 3, shell.LblStatusBalanza);
    }

    private async Task EsperarEstadoAsync(EstadoBalanza estado, int tiempoMs = 40_000) =>
        await EsperarHastaAsync(() => _balanza.Instantanea.Estado == estado, tiempoMs, $"estado {estado}");

    /// <summary>Responde a un cuadro de mensaje (sin capturarlo) con el botón indicado.</summary>
    private async Task ResponderCuadroAsync(Action disparador, Boton boton)
    {
        _esperandoCuadro = true;
        try
        {
            _ui.Post(_ => disparador(), null);
            var h = IntPtr.Zero;
            var tope = Environment.TickCount64 + 8000;
            while (Environment.TickCount64 < tope && (h = BuscarCuadro()) == IntPtr.Zero)
            {
                await Task.Delay(100);
            }

            if (h == IntPtr.Zero)
            {
                throw new TimeoutException("No apareció el cuadro de mensaje.");
            }

            await Task.Delay(300);
            CerrarCuadro(h, boton);
            await Task.Delay(500);
        }
        finally
        {
            _esperandoCuadro = false;
        }
    }

    private static GroupBox PrimerGrupo(Form f) => Capturador.Descendientes(f).OfType<GroupBox>().First();

    private static Rectangle RegionControl(Control c) => c.RectangleToScreen(c.ClientRectangle);

    // ───────────────────────── Operador (extras) ─────────────────────────

    private async Task EscenasOperadorExtraAsync()
    {
        await Escena("op-06", async () =>
        {
            await ForzarPesoAsync(0);
            var shell = await AbrirAsync<FrmPrincipal>(tamano: TamanoShell);
            await CalentarMenusAsync(shell);
            await Task.Delay(800);

            await MenuDetalleAsync(shell, "op-02-menu-pesaje-detalle", "Pesaje", "Menú «Pesaje» del operador: «Registrar pesaje» (F2) e «Historial de pesajes» (F4).");
            await MenuDetalleAsync(shell, "op-03-menu-boletas-detalle", "Boletas", "Menú «Boletas» del operador: solo «Reimprimir boleta» (F3).");
            await MenuDetalleAsync(shell, "op-04-menu-sesion-detalle", "Sesión", "Menú «Sesión»: «Cambiar contraseña…», «Cerrar sesión» y «Salir».");

            FotosBarraEstado(shell, "op-06-barra-estado-verde", "Barra de estado: balanza estable (operador)",
                "Con el usuario operador: «Balanza: conectada (COM3 9600 8N1) 0.0 kg» en verde cuando el peso es estable.");

            ControladorBalanzaDemo.PesoForzado = 60m;
            ControladorBalanzaDemo.Ruido = 3m;
            await EsperarHastaAsync(() => _balanza.Instantanea is { Estado: EstadoBalanza.Conectada, Estable: false, PesoKg: not null }, 30_000, "balanza inestable");
            await Task.Delay(300);
            FotosBarraEstado(shell, "op-07-barra-estado-inestable", "Barra de estado: balanza inestable (operador)",
                "Mientras el peso se mueve, el indicador se ve en negro: todavía no se puede capturar.");
            ControladorBalanzaDemo.Ruido = 0m;
            await ForzarPesoAsync(0);

            ControladorBalanzaDemo.Silencio = true;
            await EsperarEstadoAsync(EstadoBalanza.SinDatos);
            await Task.Delay(300);
            FotosBarraEstado(shell, "op-08-barra-estado-sin-datos", "Barra de estado: balanza sin datos (operador)",
                "Si la balanza deja de enviar datos, el indicador pasa a rojo: «Balanza: sin datos».");
            ControladorBalanzaDemo.Silencio = false;
            await EsperarEstadoAsync(EstadoBalanza.Conectada);

            ControladorBalanzaDemo.Desconectada = true;
            await EsperarEstadoAsync(EstadoBalanza.Error);
            await Task.Delay(300);
            FotosBarraEstado(shell, "op-09-barra-estado-error", "Barra de estado: error de la balanza (operador)",
                "Si se pierde la conexión, el indicador pasa a rojo con el motivo del error; el sistema reintenta solo.");
            ControladorBalanzaDemo.Desconectada = false;
            await EsperarEstadoAsync(EstadoBalanza.Conectada, 60_000);
            await ForzarPesoAsync(0);
        });

        await Escena("op-13b", async () =>
        {
            var f = await AbrirPesajeAsync();
            await ElegirAsync(f.ctlGanadero, "triun");
            await ElegirAsync(f.ctlCliente, "carn");
            f.ctlFletero.CajaTexto.Focus();
            f.ctlFletero.CajaTexto.Text = "ra"; // dos coincidencias (RAMIRO..., ...PARADA): al salir no se elige ninguno
            await Task.Delay(300);
            f.txtColorRaza.Focus();
            await Task.Delay(400);
            FotoSolo("op-13b-pesaje-fletero-sin-elegir", "Fletero escrito pero no elegido",
                "Si se escribe parte del nombre del fletero y no se elige de la lista, la guía pide «Elija el fletero de la lista o borre el texto.» y «Grabar» queda deshabilitado hasta resolverlo.",
                f, f.ctlFletero, f.txtColorRaza, f.lblGuia, f.btnGrabar);
        });
    }

    // ───────────────────────── Supervisor (extras) ─────────────────────────

    private async Task EscenasSupervisorExtraAsync()
    {
        await Escena("su-00", async () =>
        {
            await ForzarPesoAsync(0);
            var shell = await AbrirAsync<FrmPrincipal>(tamano: TamanoShell);
            await CalentarMenusAsync(shell);
            await Task.Delay(800);
            FotoSolo("su-00-inicio-supervisor", "Pantalla de inicio (supervisor)",
                "Pantalla de inicio del supervisor: las mismas tarjetas que el operador y, en el menú superior, además Catálogos y Reportes.",
                shell, shell.TarjetaPesaje, shell.TarjetaTicket, shell.TarjetaHistorial, shell.LblStatusBalanza);

            await MenuCompletoYDetalleAsync(shell, "su-00-menu-pesaje", "Pesaje", "Menú «Pesaje» del supervisor.");
            await MenuCompletoYDetalleAsync(shell, "su-00-menu-boletas", "Boletas", "Menú «Boletas» del supervisor: Reimprimir boleta, Buscar / editar boleta y Anular boleta.");
            await MenuCompletoYDetalleAsync(shell, "su-00-menu-catalogos", "Catálogos", "Menú «Catálogos»: Ganaderos, Clientes y Fleteros.");
            await MenuCompletoYDetalleAsync(shell, "su-00-menu-reportes", "Reportes", "Menú «Reportes»: Reportes de pesaje, Pesajes por raza y Boletas anuladas.");
            await MenuCompletoYDetalleAsync(shell, "su-00-menu-sesion", "Sesión", "Menú «Sesión» del supervisor.");
            await MenuCompletoYDetalleAsync(shell, "su-00-menu-ayuda", "Ayuda", "Menú «Ayuda» del supervisor: solo «Acerca de…».");
        });

        await Escena("su-04b", async () =>
        {
            var f = await AbrirAsync<FrmBuscarBoletas>(tamano: TamanoBuscar);
            f.dtpDesde.Checked = true;
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Checked = true;
            f.dtpHasta.Value = Hoy;
            Clic(f.btnBuscar);
            await EstabilizarAsync(f);
            FotoRecorte("su-01-filtros-detalle", "Buscar boletas: filtros (detalle)",
                "Zona de filtros del buscador: fechas, número de boleta, estado, ganadero y cliente, con los botones «Buscar» y «Limpiar».",
                f, RegionControl(PrimerGrupo(f)), 2, f.dtpDesde, f.dtpHasta, f.txtNumero, f.cmbEstado, f.ctlGanadero, f.ctlCliente, f.btnBuscar, f.btnLimpiar);
            await BuscarAsync(f.ctlGanadero, "ha");
            FotoSolo("su-04b-buscar-ganadero-lista", "Buscar boletas: elegir ganadero",
                "En el campo «Ganadero» se escribe parte del nombre o del código y se elige de la lista desplegable.",
                f, f.ctlGanadero, f.ctlGanadero.ListaResultados);
        });

        // Editar boleta: sin cambios, con error, y con la boleta cambiada por otra persona (concurrencia).
        await Escena("su-06b", async () =>
        {
            using var scope = _servicios.CreateScope();
            var gestion = scope.ServiceProvider.GetRequiredService<IServicioGestionPesajes>();
            var res = await gestion.BuscarAsync(new FiltroBoletas(DateOnly.FromDateTime(Hoy.AddDays(-1)), DateOnly.FromDateTime(Hoy), null, null, null, EstadoPesaje.Vigente));
            var dto = await gestion.ObtenerParaEdicionAsync(res.Boletas.First().Id);

            var f = await AbrirAsync<FrmEditarBoleta>(c => c.Configurar(dto), new Size(760, 720));
            await f.GuardarParaTestAsync();
            await Task.Delay(400);
            FotoSolo("su-06b-editar-sin-cambios", "Editar boleta sin cambios",
                "Si se pulsa «Guardar» sin haber cambiado ningún dato, aparece «No se modificó ningún dato.» y la ventana sigue abierta.",
                f, f.lblMensaje, f.btnGuardar, f.btnCancelar);

            // F12: un estado que el usuario sí puede producir (antes se forzaba un color/raza más largo que el campo).
            f.ctlGanadero.Limpiar();
            await f.GuardarParaTestAsync();
            await Task.Delay(400);
            FotoSolo("su-06c-editar-error-validacion", "Editar boleta con un dato no válido",
                "Si falta un dato obligatorio (por ejemplo, el ganadero), aparece un icono rojo junto al campo y no se guarda.",
                f, f.ctlGanadero, f.btnGuardar);

            f.ctlGanadero.Seleccionar(dto.GanaderoId);
            f.ctlFletero.CajaTexto.Text = "ra";
            f.ctlFletero.SimularSalida();
            await f.GuardarParaTestAsync();
            await Task.Delay(400);
            FotoSolo("su-06e-editar-fletero-sin-elegir", "Editar boleta con el fletero sin elegir",
                "Si el fletero quedó escrito sin elegirlo de la lista, aparece un icono rojo junto al campo («Elija el fletero de la lista o borre el texto.») y no se guarda.",
                f, f.ctlFletero, f.btnGuardar);
            f.Close();

            // Concurrencia: otra persona modifica la misma boleta mientras esta ventana está abierta.
            var viejo = await gestion.ObtenerParaEdicionAsync(dto.Id);
            var f2 = await AbrirAsync<FrmEditarBoleta>(c => c.Configurar(viejo), new Size(760, 720));
            await gestion.EditarAsync(new EditarPesaje(
                viejo.Id, viejo.Version, viejo.GanaderoId, viejo.ClienteId, viejo.Direccion, viejo.Secuencia,
                "CRIOLLO CRUZADO", viejo.Sexo, viejo.Observacion, "Cambio hecho por otra persona", viejo.FleteroId));
            f2.txtColorRaza.Text = "NELORE CRUZADO";
            await CapturarCuadroAsync("su-06d-editar-boleta-cambiada", "La boleta cambió mientras se editaba",
                "Si otra persona modificó o anuló la boleta mientras se editaba, el sistema avisa y recarga los datos actuales.",
                () => { _ = f2.GuardarParaTestAsync(); }, Boton.Aceptar);
        });

        await Escena("su-07b", async () =>
        {
            using var scope = _servicios.CreateScope();
            var gestion = scope.ServiceProvider.GetRequiredService<IServicioGestionPesajes>();
            var res = await gestion.BuscarAsync(new FiltroBoletas(DateOnly.FromDateTime(Hoy.AddDays(-1)), DateOnly.FromDateTime(Hoy), null, null, null, EstadoPesaje.Vigente));
            var boleta = res.Boletas.First();
            var f = await AbrirAsync<FrmAnularBoleta>(c => c.Configurar(boleta), new Size(620, 420));
            f.txtMotivo.Text = "Pesaje duplicado por error de captura";
            await Task.Delay(200);
            await CapturarCuadroAsync("su-07b-anular-confirmacion", "Confirmar la anulación",
                "Antes de anular, el sistema pregunta «Se anulará la boleta N° …. ¿Desea continuar?»; la anulación no se puede deshacer.",
                () => { _ = (Task)Invocar(f, "AnularAsync")!; }, Boton.No);

            // F12 (E5): con un motivo corto ya no aparece la confirmación: el error se muestra antes.
            f.txtMotivo.Text = "ab";
            await (Task)Invocar(f, "AnularAsync")!;
            await Task.Delay(3500); // el icono del ErrorProvider parpadea unos segundos antes de quedar fijo
            FotoSolo("su-07c-anular-motivo-corto", "Anular con un motivo demasiado corto",
                "El motivo debe tener entre 5 y 100 caracteres: si es más corto, aparece el icono rojo junto al campo, sin preguntar nada, y la boleta no se anula.",
                f, f.txtMotivo, f.btnAnular);
        });


        // Prueba empírica: ¿la vista previa cambia de página con AvPág/RePág o con la rueda del mouse? (solo registra el resultado en el log)
        await Escena("su-12c", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.PesajesEntreFechas), TamanoGrande);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            await Task.Delay(300);
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            var reporte = f.ReporteActual!;
            var paginas = await _servicios.GetRequiredService<IGeneradorPdfReportes>().GenerarPaginasAsync(reporte, ReglasReportes.ResolucionImpresionDpi);
            var vista = new FrmVistaPrevia(new DocumentoPaginasReporte(paginas, paraVistaPrevia: true, reporte.Titulo), "Vista previa — " + reporte.Titulo);
            Preparar(vista, new Size(900, 760));
            _abiertos.Add(vista);
            vista.Show();
            vista.Activate();
            Win32Capturas.SetForegroundWindow(vista.Handle);
            await Task.Delay(2500);
            var ppc = vista.ppcVista;
            string Estado() => $"StartPage={ppc.StartPage}, Posicion={typeof(PrintPreviewControl).GetField("position", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(ppc)}, AutoZoom={ppc.AutoZoom}, Zoom={ppc.Zoom:0.00}, Rows={ppc.Rows}, Columns={ppc.Columns}";
            Console.WriteLine($"[capturas] PAGINADO páginas del reporte: {paginas.Count}. Inicial: {Estado()}");
            ppc.Focus();
            SendKeys.SendWait("{PGDN}");
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras AvPág: {Estado()}");
            SendKeys.SendWait("{PGUP}");
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras RePág: {Estado()}");
            SendKeys.SendWait("{DOWN}{DOWN}{DOWN}");
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras 3 flechas abajo: {Estado()}");
            SendKeys.SendWait("{END}");
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras Fin: {Estado()}");
            SendKeys.SendWait("{HOME}");
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras Inicio: {Estado()}");
            var centro = ppc.PointToScreen(new Point(ppc.Width / 2, ppc.Height / 2));
            var lParam = (IntPtr)((centro.Y << 16) | (centro.X & 0xFFFF));
            for (var i = 0; i < 6; i++)
            {
                Win32Capturas.SendMessage(ppc.Handle, 0x020A, (IntPtr)(-120 << 16), lParam);
            }

            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras 6 clics de rueda abajo: {Estado()}");
            // Rueda real del mouse (evento de entrada del sistema, con el puntero sobre la vista previa).
            Win32Capturas.SetCursorPos(centro.X, centro.Y);
            await Task.Delay(200);
            for (var i = 0; i < 6; i++)
            {
                Win32Capturas.mouse_event(0x0800, 0, 0, -120, UIntPtr.Zero);
                await Task.Delay(120);
            }

            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras 6 clics de rueda reales: {Estado()}");
            ppc.StartPage = 0;
            Win32Capturas.SendMessage(ppc.Handle, 0x0115, (IntPtr)3, IntPtr.Zero); // WM_VSCROLL, SB_PAGEDOWN
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras WM_VSCROLL (barra de desplazamiento, una página): {Estado()}");
            Win32Capturas.SendMessage(ppc.Handle, 0x0115, (IntPtr)1, IntPtr.Zero); // SB_LINEDOWN
            await Task.Delay(500);
            Console.WriteLine($"[capturas] PAGINADO tras WM_VSCROLL (una línea): {Estado()}");
            ppc.StartPage = 1;
            await Task.Delay(800);
            FotoSolo("su-12b-reporte-vista-previa-pagina2", "Vista previa de un reporte: página 2",
                "Segunda página de la vista previa del reporte (se cambia con la barra de desplazamiento).", vista, vista.ppcVista, vista.btnCerrar);
        });
        // Reportes: recortes, aviso de filtros, exportación y fila con peso simulado.
        await Escena("su-10b", async () =>
        {
            var f = await AbrirAsync<FrmReportes>(c => c.Configurar(TipoReporte.PesajesEntreFechas), TamanoGrande);
            f.dtpDesde.Value = Hoy.AddDays(-5);
            f.dtpHasta.Value = Hoy;
            await Task.Delay(300);
            Clic(f.btnGenerar);
            await EstabilizarAsync(f);
            FotoRecorte("su-10-filtros-detalle", "Reportes: filtros (detalle)",
                "Zona de filtros de los reportes: tipo de reporte, fechas, ganadero, cliente y raza (según el tipo), y el botón «Generar».",
                f, RegionControl(PrimerGrupo(f)), 2, f.cmbTipo, f.dtpDesde, f.dtpHasta, f.ctlGanadero, f.ctlCliente, f.cmbRaza, f.btnGenerar);
            FotoRecorte("su-10-totales-detalle", "Reportes: totales (detalle)",
                "Línea de totales del reporte: peso neto total, cantidad de reses (res completa = 1, media res = 0,5) y boletas.",
                f, RegionControl(f.lblTotales.Parent!), 2, f.lblTotales, f.lblAviso);

            // Fila con peso simulado: marcada con asterisco y con una nota naranja al pie.
            var filas = f.dgvVista.Rows.Cast<DataGridViewRow>().ToList();
            var iSim = filas.FindIndex(r => r.Cells.Cast<DataGridViewCell>().Any(c => c.Value?.ToString()?.Contains('*') == true));
            if (iSim >= 0)
            {
                f.dgvVista.ClearSelection();
                f.dgvVista.Rows[iSim].Selected = true;
                f.dgvVista.CurrentCell = f.dgvVista.Rows[iSim].Cells[0];
                f.dgvVista.FirstDisplayedScrollingRowIndex = Math.Max(0, iSim - 3);
                await Task.Delay(300);
                FotoSolo("su-11b-reporte-peso-simulado", "Reporte con un peso simulado",
                    "Una boleta grabada con la balanza en modo simulador se marca con un asterisco (*) y el reporte agrega una nota en naranja: esos pesos no tienen valor comercial.",
                    f, f.dgvVista, f.lblAviso, f.lblTotales);
            }
            else
            {
                Omitir("su-11b-reporte-peso-simulado", "no se encontró la fila con asterisco en la grilla");
            }

            // Exportación: se elige una ruta fija (sin el cuadro de guardar de Windows) y se captura la pregunta de abrir el archivo.
            var rutaPdf = string.Empty;
            f.ElegirRutaGuardarParaTest = (nombre, _) => rutaPdf = Path.Combine(@"C:\Users\Public\Documents", nombre);
            f.AbrirArchivoParaTest = _ => { };
            await CapturarCuadroAsync("su-18-reporte-abrir-archivo", "Reporte exportado: ¿abrirlo ahora?",
                "Al exportar a PDF o Excel, el sistema indica dónde guardó el archivo y pregunta «¿Desea abrirlo ahora?».",
                () => { _ = f.ExportarPdfParaTestAsync(); }, Boton.No, esperaMs: 30_000);
            await Task.Delay(300);
            if (File.Exists(rutaPdf))
            {
                File.Delete(rutaPdf);
            }

            f.dtpDesde.Value = Hoy.AddDays(-4);
            await Task.Delay(400);
            FotoSolo("su-10c-reporte-filtros-cambiaron", "Reporte: los filtros cambiaron",
                "Si se cambia un filtro después de generar el reporte, el reporte se borra y aparece «Los filtros cambiaron: presione Generar para actualizar el reporte.».",
                f, f.dtpDesde, f.btnGenerar, f.lblAviso, f.dgvVista);
        });

        // Catálogos: inactivos, desactivar y errores de validación.
        await Escena("su-29", async () =>
        {
            var f = await AbrirAsync<FrmGanaderos>(tamano: new Size(960, 640));
            f.MostrarInactivosParaTest = true;
            await EstabilizarAsync(f);
            var dgv = f.GrillaParaTest;
            var iInactivo = dgv.Rows.Cast<DataGridViewRow>().ToList().FindIndex(r => r.DataBoundItem!.GetType().GetProperty("Activo")!.GetValue(r.DataBoundItem) is false);
            if (iInactivo >= 0)
            {
                dgv.ClearSelection();
                dgv.Rows[iInactivo].Selected = true;
                dgv.CurrentCell = dgv.Rows[iInactivo].Cells[0];
                Invocar(f, "ActualizarEstadoBotones");
                await Task.Delay(300);
            }

            FotoSolo("su-29-ganaderos-inactivos", "Catálogo de ganaderos con inactivos",
                "Con «Mostrar inactivos» marcado se ven también los ganaderos desactivados; al elegir uno, el botón pasa a decir «Activar».",
                f, Campo<CheckBox>(f, "chkMostrarInactivos"), dgv, Campo<Button>(f, "btnActivarDesactivar"));

            f.MostrarInactivosParaTest = false;
            await EstabilizarAsync(f);
            dgv.ClearSelection();
            dgv.Rows[0].Selected = true;
            dgv.CurrentCell = dgv.Rows[0].Cells[0];
            await Task.Delay(300);
            await CapturarCuadroAsync("su-29b-desactivar-confirmacion", "Confirmar la desactivación",
                "Antes de desactivar un ganadero, el sistema pide confirmación; un ganadero desactivado deja de aparecer al pesar pero se conserva su historial.",
                () => { _ = (Task)Invocar(f, "ActivarDesactivarSeleccionadoAsync")!; }, Boton.No);

            var clientes = await AbrirAsync<FrmClientes>(tamano: new Size(1024, 640));
            clientes.MostrarInactivosParaTest = true;
            await EstabilizarAsync(clientes);
            var dgvC = clientes.GrillaParaTest;
            var iC = dgvC.Rows.Cast<DataGridViewRow>().ToList().FindIndex(r => r.DataBoundItem!.GetType().GetProperty("Activo")!.GetValue(r.DataBoundItem) is false);
            if (iC >= 0)
            {
                dgvC.ClearSelection();
                dgvC.Rows[iC].Selected = true;
                dgvC.CurrentCell = dgvC.Rows[iC].Cells[0];
                Invocar(clientes, "ActualizarEstadoBotones");
                await Task.Delay(300);
            }

            FotoSolo("su-29c-clientes-inactivos", "Catálogo de clientes con inactivos",
                "Los clientes desactivados se ven al marcar «Mostrar inactivos» y se pueden volver a activar.",
                clientes, Campo<CheckBox>(clientes, "chkMostrarInactivos"), dgvC, Campo<Button>(clientes, "btnActivarDesactivar"));
        });

        await Escena("su-21b", async () =>
        {
            var g = await AbrirAsync<FrmGanaderoEdicion>(c => c.ConfigurarAlta(), new Size(520, 300));
            g.txtNombre.Text = "ab";
            await g.GuardarParaTestAsync();
            await Task.Delay(400);
            FotoSolo("su-21b-ganadero-error-nombre", "Nuevo ganadero con el nombre demasiado corto",
                "El nombre debe tener entre 3 y 100 caracteres: si es más corto, aparece el icono rojo junto al campo y no se guarda.",
                g, g.txtNombre, g.btnGuardar);
            g.Close();

            var fl = await AbrirAsync<FrmFleteroEdicion>(c => c.ConfigurarAlta(), new Size(520, 340));
            fl.txtNombre.Text = "PEDRO";
            fl.txtCelular.Text = "12";
            await fl.GuardarParaTestAsync();
            await Task.Delay(400);
            FotoSolo("su-27b-fletero-error-celular", "Nuevo fletero con el celular no válido",
                "El celular debe tener al menos 6 dígitos: si no, aparece el icono rojo junto al campo y no se guarda.",
                fl, fl.txtNombre, fl.txtCelular, fl.btnGuardar);
        });
    }

    // ───────────────────────── Administrador (extras) ─────────────────────────

    private async Task<ConfiguracionBalanzaDto> ConfigBalanzaAsync()
    {
        using var scope = _servicios.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IServicioConfiguracionBalanza>().ObtenerAsync()).Configuracion;
    }

    private async Task GuardarConfigBalanzaAsync(ConfiguracionBalanzaDto dto)
    {
        using var scope = _servicios.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IServicioConfiguracionBalanza>().ActualizarAsync(dto);
    }

    private async Task EscenasAdministradorExtraAsync()
    {
        // Usuarios: bloqueado, inactivos y edición.
        await Escena("ad-14", async () =>
        {
            using (var scope = _servicios.CreateScope())
            {
                var auth = scope.ServiceProvider.GetRequiredService<IServicioAutenticacion>();
                for (var i = 0; i < 5; i++)
                {
                    await auth.IniciarSesionAsync(_cfg.Nuevo.NombreUsuario, "clave-equivocada");
                }
            }

            var f = await AbrirAsync<FrmUsuarios>();
            var dgv = Campo<DataGridView>(f, "dgvUsuarios");
            var i2 = dgv.Rows.Cast<DataGridViewRow>().ToList().FindIndex(r => (string)r.DataBoundItem!.GetType().GetProperty("NombreUsuario")!.GetValue(r.DataBoundItem)! == _cfg.Nuevo.NombreUsuario);
            dgv.ClearSelection();
            dgv.Rows[i2].Selected = true;
            dgv.CurrentCell = dgv.Rows[i2].Cells[0];
            await Task.Delay(300);
            FotoSolo("ad-14-usuarios-bloqueado", "Usuarios: un usuario bloqueado",
                "Tras 5 intentos fallidos de ingreso, el usuario queda «Bloqueado: Sí» durante 5 minutos.",
                f, dgv, Campo<Button>(f, "btnEditar"), Campo<Button>(f, "btnRestablecer"));

            Campo<CheckBox>(f, "chkMostrarInactivos").Checked = true;
            await EstabilizarAsync(f);
            FotoSolo("ad-15-usuarios-inactivos", "Usuarios con «Mostrar usuarios inactivos»",
                "Al marcar «Mostrar usuarios inactivos» aparecen también los usuarios desactivados; el botón de estado pasa a decir «Activar».",
                f, Campo<CheckBox>(f, "chkMostrarInactivos"), dgv, Campo<Button>(f, "btnActivarDesactivar"));

            using var scope2 = _servicios.CreateScope();
            var usuarios = await scope2.ServiceProvider.GetRequiredService<IServicioUsuarios>().ListarAsync(true);
            var op = usuarios.First(u => u.NombreUsuario == "operador");
            var ed = await AbrirAsync<FrmUsuarioEdicion>(c => c.ConfigurarEdicion(op));
            FotoSolo("ad-16-usuario-editar", "Editar usuario",
                "Al editar un usuario se puede cambiar el nombre completo y el rol; el nombre de usuario no se puede cambiar.",
                ed, ed.txtNombreUsuario, ed.txtNombreCompleto, ed.cmbRol, ed.btnGuardar, ed.btnCancelar);
        });

        // Configuración de impresión con impresora virtual e impresión automática.
        await Escena("ad-32", async () =>
        {
            var f = await AbrirAsync<FrmConfiguracionImpresion>();
            if (f.cmbImpresora.Items.Count > 1)
            {
                f.cmbImpresora.SelectedIndex = 1;
            }

            f.chkImprimirAlGrabar.Checked = true;
            Anonimizar(f);
            await Task.Delay(300);
            FotoSolo("ad-32-impresion-impresora-virtual", "Impresión automática con una impresora virtual",
                "Si se marca «Imprimir la boleta automáticamente al grabar» con una impresora virtual (PDF), aparece una advertencia en rojo: pediría un nombre de archivo en cada boleta.",
                f, f.cmbImpresora, f.chkImprimirAlGrabar, f.lblAdvertencia, f.btnGuardar);
        });

        // Balanza RX8: Serie, 9600 8N1, inicio con '=' y gramos.
        await Escena("ad-43", async () =>
        {
            var original = await ConfigBalanzaAsync();
            try
            {
                ControladorBalanzaDemo.FormatoRx8 = true;
                await GuardarConfigBalanzaAsync(original with
                {
                    TipoConexion = TipoConexion.Serial, PuertoCom = "COM3", BaudRate = 9600, DataBits = 8,
                    Paridad = ParidadSerial.None, BitsParada = BitsParada.One,
                    DelimitadorTrama = DelimitadorTrama.SignoIgual, UnidadPeso = UnidadPesoTrama.Gramos
                });
                await ForzarPesoAsync(121.4m);
                var f = await AbrirAsync<FrmConfiguracionBalanza>(tamano: new Size(1024, 860));
                await Task.Delay(600);
                FotoSolo("ad-43-balanza-rx8", "Configuración de balanza para el indicador RX8",
                    "Valores del indicador RX8 Accurex: conexión Serie, COM3, 9600 baudios, 8 bits, sin paridad, 1 bit de parada, separación «Inicio con '='» y unidad Gramos.",
                    f, f.cmbTipoConexion, f.cmbPuerto, f.cmbBaudios, f.cmbBitsDatos, f.cmbParidad, f.cmbBitsParada, f.cmbDelimitador, f.cmbUnidad, f.btnProbarConexion, f.btnGuardar);
                var prueba = f.ProbarConexionParaTestAsync();
                await prueba;
                await ForzarPesoAsync(121.4m);
                FotoSolo("ad-43b-balanza-rx8-prueba", "Prueba de conexión con el indicador RX8",
                    "La prueba muestra las tramas recibidas del indicador (=121400, el peso en gramos) y confirma «Conexión correcta … Estabilidad: alcanzada».",
                    f, f.lblEstadoPrueba, f.lblPesoPrueba, f.lstTramas);
                f.Close();
            }
            finally
            {
                ControladorBalanzaDemo.FormatoRx8 = false;
                await GuardarConfigBalanzaAsync(original);
                await ForzarPesoAsync(0);
            }
        });

        // Modo simulador: balanza, asistente, pesaje y barra de estado naranja.
        await Escena("ad-44", async () =>
        {
            var original = await ConfigBalanzaAsync();
            var simulador = _servicios.GetRequiredService<ISimuladorBalanza>();
            try
            {
                await GuardarConfigBalanzaAsync(original with { TipoConexion = TipoConexion.Simulador });
                simulador.EstablecerPesoObjetivo(121.4m);
                await EsperarHastaAsync(() => _balanza.Instantanea is { EsSimulador: true, Estado: EstadoBalanza.Conectada, Estable: true }, 40_000, "simulador estable");

                var shell = await AbrirAsync<FrmPrincipal>(tamano: TamanoShell);
                await Task.Delay(800);
                var barra = Campo<StatusStrip>(shell, "statusStrip");
                FotoRegion("ad-44-barra-estado-simulador", "Barra de estado con el simulador activo",
                    "Con la balanza en modo Simulador, el indicador de la barra de estado se ve con fondo naranja y la marca [SIMULADOR].", shell, barra, shell.LblStatusBalanza);
                FotoRecorte("ad-44-barra-estado-simulador-detalle", "Barra de estado con el simulador activo (detalle)",
                    "Segmento de la balanza con la marca [SIMULADOR] sobre fondo naranja.", shell, RegionItem(barra, shell.LblStatusBalanza), 3, shell.LblStatusBalanza);
                shell.Close();

                var f = await AbrirAsync<FrmConfiguracionBalanza>(tamano: new Size(1024, 860));
                await Task.Delay(600);
                FotoSolo("ad-45-balanza-simulador", "Configuración de balanza en modo Simulador",
                    "Con el tipo «Simulador» se habilita el control del simulador (peso objetivo, «Aplicar», «Simular carga», «Vaciar»); solo para pruebas.",
                    f, f.cmbTipoConexion, f.grpSimulador, f.numPesoObjetivo, f.btnAplicarPeso, f.btnCargaAleatoria, f.btnVaciar, f.chkDesconexion, f.btnGuardar);
                f.Close();

                var asistente = await AbrirAsync<FrmAsistentePuestaEnMarcha>(tamano: new Size(900, 800));
                FotoSolo("ad-46-asistente-simulador", "Asistente con la balanza en modo Simulador",
                    "Si la balanza está en modo Simulador, el asistente exige marcar «Continuar con la balanza en modo SIMULADOR (solo pruebas…)» para poder finalizar.",
                    asistente, asistente.ChkSimulador, asistente.BtnFinalizar, asistente.LblResumen);
                asistente.Close();

                var p = await AbrirAsync<Formularios.Pesaje.FrmPesaje>(tamano: TamanoPesaje);
                await Task.Delay(800);
                FotoSolo("ad-47-pesaje-modo-simulador", "Registro de pesaje en modo Simulador",
                    "En modo Simulador la pantalla de pesaje muestra la franja «MODO SIMULADOR — los pesos no son reales» y, para el administrador, el panel para simular cargas.",
                    p, p.lblSimulador, p.grpSimulador, p.lblPesoVivo, p.lblEstadoBalanza);
            }
            finally
            {
                await GuardarConfigBalanzaAsync(original);
                await EsperarHastaAsync(() => _balanza.Instantanea is { EsSimulador: false, Estado: EstadoBalanza.Conectada }, 60_000, "balanza de demostración reconectada");
                await ForzarPesoAsync(0);
            }
        });

        // Restauración programada (sin permitir el reinicio).
        await Escena("ad-52", async () =>
        {
            var f = await AbrirAsync<FrmBackupRestaurar>(tamano: new Size(1024, 700));
            f.ConfirmarRestauracionParaTest = _ => "RESTAURAR";
            f.SeleccionarFilaParaTest(0);
            await Task.Delay(300);
            try
            {
                await CapturarCuadroAsync("ad-52-restauracion-programada", "La restauración quedó programada",
                    "Tras confirmar, el sistema avisa que la restauración quedó programada, que se guardó una copia de la base actual y que se cerrará y volverá a abrirse.",
                    () => { _ = f.RestaurarSeleccionadaParaTestAsync(); }, Boton.Aceptar, esperaMs: 30_000);
            }
            finally
            {
                // La restauración no debe aplicarse nunca: se borran los marcadores pendientes.
                var restore = _servicios.GetRequiredService<PesajeFaeneo.Infrastructure.Common.AppPaths>().RestoreDir;
                foreach (var d in Directory.GetDirectories(restore, "pendiente-*"))
                {
                    Directory.Delete(d, true);
                }

                var marcador = Path.Combine(restore, "pendiente.json");
                if (File.Exists(marcador))
                {
                    File.Delete(marcador);
                }
            }
        });
    }

    // ───────────────────────── Datos sin pesajes (primera boleta) ─────────────────────────

    private async Task EscenasDatosVaciosAsync()
    {
        await IniciarSesionAsync("admin", _cfg.PasswordAdmin);
        await Escena("ad-21", async () =>
        {
            var f = await AbrirAsync<FrmParametros>();
            await Task.Delay(500);
            FotoSolo("ad-21-parametros-antes-primera-boleta", "Parámetros antes de emitir la primera boleta",
                "Mientras no exista ninguna boleta, el «Siguiente» número se puede fijar a mano (por ejemplo, para continuar el talonario anterior); después queda automático.",
                f, f.grpBoleta, f.numSiguienteBoleta, f.lblNotaNumeracion, f.btnGuardar);

            f.numSiguienteBoleta.Value = 5000;
            await CapturarCuadroAsync("ad-22-parametros-confirmar-numeracion", "Confirmar el cambio de numeración",
                "Al cambiar el número de boleta, el sistema pide confirmar: «Cambiará el siguiente número de boleta de … a …. Esta acción afecta la numeración oficial».",
                () => { _ = (Task)Invocar(f, "GuardarAsync")!; }, Boton.No);
        });
    }
}
#endif

#if CAPTURAS_MANUAL
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Infrastructure.Balanza.Demo;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Formularios.Pesaje;
using PesajeFaeneo.WinForms.Impresion;
using PesajeFaeneo.WinForms.Presentacion.Pesaje;

namespace PesajeFaeneo.WinForms.Capturas;

internal sealed partial class GuionCapturas
{
    // ───────────────────────── Utilidades de pantalla de pesaje ─────────────────────────

    /// <summary>Escribe el texto en el buscador, deja abierta la lista y devuelve el primer resultado.</summary>
    private static async Task<ElementoBusqueda> BuscarAsync(ControlBusquedaCatalogo ctl, string texto)
    {
        ctl.CajaTexto.Focus();
        // La primera vez que se abre la lista del control, esta se dibuja más baja de lo debido (ItemHeight aún sin ajustar a la fuente):
        // se abre y se cierra una vez antes de la captura para mostrar el aspecto normal.
        ctl.CajaTexto.Text = "e";
        await Task.Delay(250);
        ctl.CajaTexto.Text = string.Empty;
        await Task.Delay(150);
        ctl.CajaTexto.Text = texto;
        await Task.Delay(350);
        if (ctl.ListaResultados.Items.Count == 0)
        {
            throw new InvalidOperationException($"La búsqueda '{texto}' no dio resultados.");
        }

        return (ElementoBusqueda)ctl.ListaResultados.Items[0]!;
    }

    private static async Task ElegirAsync(ControlBusquedaCatalogo ctl, string texto)
    {
        var e = await BuscarAsync(ctl, texto);
        ctl.Seleccionar(e.Id);
        await Task.Delay(500);
    }

    private async Task<FrmPesaje> AbrirPesajeAsync()
    {
        await ForzarPesoAsync(0);
        var f = await AbrirAsync<FrmPesaje>(tamano: TamanoPesaje);
        f.ConfirmarCierre = false;
        await Task.Delay(500);
        return f;
    }

    private static void Clic(Button b)
    {
        if (!b.Enabled)
        {
            throw new InvalidOperationException($"El botón '{b.Text}' está deshabilitado.");
        }

        b.PerformClick();
    }

    private async Task EsperarPesajeGrabadoAsync(FrmPesaje f)
    {
        var tarea = (Task)Invocar(f, "GrabarParaTestAsync")!;
        await tarea;
        await Task.Delay(600);
    }

    private async Task AbrirMenuAsync(FrmPrincipal shell, string texto)
    {
        var item = shell.MenuPrincipal.Items.OfType<ToolStripMenuItem>()
            .First(i => i.Text!.Replace("&", string.Empty) == texto);
        shell.Activate();
        item.ShowDropDown();
        await Task.Delay(500);
    }

    private static void CerrarMenus(FrmPrincipal shell)
    {
        foreach (var i in shell.MenuPrincipal.Items.OfType<ToolStripMenuItem>())
        {
            i.HideDropDown();
        }
    }

    // ───────────────────────── Operador ─────────────────────────

    private async Task EscenasOperadorAsync()
    {
        await IniciarSesionAsync(_cfg.Operador.NombreUsuario, _cfg.Operador.Password);
        await EsperarBalanzaConectadaAsync();

        // Pantalla de inicio, menús y barra de estado.
        await Escena("op-01", async () =>
        {
            await ForzarPesoAsync(0);
            var shell = await AbrirAsync<FrmPrincipal>(tamano: TamanoShell);
            await Task.Delay(800);
            FotoSolo("op-01-inicio", "Pantalla de inicio (operador)",
                "Pantalla de inicio con las tarjetas «Control de peso», «Imprimir ticket» y «Historial de pesajes», el menú superior y la barra de estado con la balanza conectada.",
                shell, shell.TarjetaPesaje, shell.TarjetaTicket, shell.TarjetaHistorial, shell.LblStatusBalanza, shell.LblStatusEmpresa);

            await AbrirMenuAsync(shell, "Pesaje");
            FotoSolo("op-02-menu-pesaje", "Menú Pesaje (operador)", "Menú «Pesaje» abierto: «Registrar pesaje (F2)» y «Historial de pesajes (F4)».", shell);
            CerrarMenus(shell);
            await AbrirMenuAsync(shell, "Boletas");
            FotoSolo("op-03-menu-boletas", "Menú Boletas (operador)", "Menú «Boletas» abierto: el operador solo ve «Reimprimir boleta (F3)».", shell);
            CerrarMenus(shell);
            await AbrirMenuAsync(shell, "Sesión");
            FotoSolo("op-04-menu-sesion", "Menú Sesión", "Menú «Sesión»: «Cambiar contraseña…», «Cerrar sesión» y «Salir».", shell);
            CerrarMenus(shell);

            await CapturarCuadroAsync("op-05-cerrar-sesion-confirmacion", "Confirmar cierre de sesión",
                "Pregunta «¿Está seguro de que desea cerrar la sesión?» con los botones Sí y No.",
                () => Invocar(shell, "CerrarSesionClick"), Boton.No);
        });

        // Registro de pesaje, res completa, paso a paso.
        await Escena("op-10", async () =>
        {
            var f = await AbrirPesajeAsync();
            FotoSolo("op-10-pesaje-vacio", "Registro de pesaje, pantalla vacía",
                "Pantalla de registro de pesaje recién abierta: peso en vivo estable en 0.0 kg y el mensaje guía «Seleccione el ganadero (código o nombre).».",
                f, f.lblProximaBoleta, f.lblPesoVivo, f.lblEstable, f.lblEstadoBalanza, f.lblGuia, f.ctlGanadero, f.ctlCliente, f.ctlFletero, f.numSecuencia,
                f.txtColorRaza, f.cmbSexo, f.cmbDesposte, f.txtObservacion, f.btnCapturarIzquierdo, f.btnCapturarDerecho, f.btnBorrarPesaje, f.btnGrabar, f.btnImprimir, f.btnCerrar);

            await BuscarAsync(f.ctlGanadero, "san");
            FotoSolo("op-11-pesaje-buscar-ganadero", "Buscar ganadero por nombre o código",
                "Al escribir «san» en el campo del ganadero se abre la lista con las coincidencias; se elige una con las flechas y Enter, o con el mouse.",
                f, f.ctlGanadero, f.ctlGanadero.ListaResultados);
            f.ctlGanadero.ListaResultados.SelectedIndex = 0;
            await Task.Delay(200);
            FotoSolo("op-11b-pesaje-ganadero-resaltado", "Ganadero resaltado en la lista",
                "La primera coincidencia queda resaltada; Enter la elige.", f, f.ctlGanadero, f.ctlGanadero.ListaResultados);
            f.ctlGanadero.Seleccionar(((ElementoBusqueda)f.ctlGanadero.ListaResultados.Items[0]!).Id);
            await Task.Delay(500);

            await ElegirAsync(f.ctlCliente, "carn");
            await Task.Delay(300);
            FotoSolo("op-12-pesaje-ganadero-cliente", "Ganadero y cliente elegidos",
                "Con el ganadero y el cliente elegidos, el sistema sugiere el número de secuencia del día y copia la dirección del cliente.",
                f, f.ctlGanadero, f.numSecuencia, f.lblInfoSecuencia, f.ctlCliente, f.txtDireccion);

            await BuscarAsync(f.ctlFletero, "pe");
            FotoSolo("op-13-pesaje-fletero-lista", "Elegir el fletero",
                "El fletero se elige igual que el ganadero: se escribe parte del nombre o del código y se elige de la lista.",
                f, f.ctlFletero, f.ctlFletero.ListaResultados);
            f.ctlFletero.Seleccionar(((ElementoBusqueda)f.ctlFletero.ListaResultados.Items[0]!).Id);
            f.txtColorRaza.Text = "NELORE";
            await Task.Delay(300);

            await ForzarPesoAsync(121.4m);
            FotoSolo("op-14-pesaje-peso-estable", "Peso estable en la balanza",
                "La res está en la balanza: el peso en vivo (121.4 kg) aparece en verde con la palabra ESTABLE y se habilita «Capturar izquierdo (F5)».",
                f, f.lblPesoVivo, f.lblEstable, f.btnCapturarIzquierdo, f.btnCapturarDerecho, f.lblGuia);

            Clic(f.btnCapturarIzquierdo);
            await Task.Delay(500);
            FotoSolo("op-15-pesaje-izquierdo-capturado", "Lado izquierdo capturado",
                "Tras capturar el lado izquierdo, el sistema pide retirar el peso de la balanza (retorno a cero) antes de capturar el derecho.",
                f, f.txtPesoIzquierdo, f.txtPesoDerecho, f.txtPesoTotal, f.lblGuia, f.btnBorrarPesaje);

            await ForzarPesoAsync(0);
            await Task.Delay(300);
            FotoSolo("op-16-pesaje-balanza-en-cero", "Balanza en cero",
                "La balanza volvió a cero: ya puede colocar el otro lado de la res.", f, f.lblPesoVivo, f.lblGuia, f.txtPesoIzquierdo);

            await ForzarPesoAsync(118.6m);
            Clic(f.btnCapturarDerecho);
            await Task.Delay(500);
            FotoSolo("op-17-pesaje-listo-grabar", "Pesos capturados, listo para grabar",
                "Con los dos lados capturados aparece el total y el mensaje «Listo para grabar (F12).».",
                f, f.txtPesoIzquierdo, f.txtPesoDerecho, f.txtPesoTotal, f.lblGuia, f.btnGrabar, f.btnBorrarPesaje);
            await ForzarPesoAsync(0);
        });

        // Mensaje guía cuando falta un dato (sin ganadero) y error de validación.
        await Escena("op-18", async () =>
        {
            var f = await AbrirPesajeAsync();
            await ElegirAsync(f.ctlCliente, "carn");
            await ForzarPesoAsync(121.4m);
            Clic(f.btnCapturarIzquierdo);
            await ForzarPesoAsync(0);
            await ForzarPesoAsync(118.6m);
            Clic(f.btnCapturarDerecho);
            await Task.Delay(500);
            FotoSolo("op-18-pesaje-falta-ganadero", "Falta elegir el ganadero",
                "Con los pesos capturados pero sin ganadero, «Grabar pesaje» sigue desactivado y el mensaje guía dice «Seleccione el ganadero (código o nombre).».",
                f, f.ctlGanadero, f.lblGuia, f.btnGrabar);

            await ElegirAsync(f.ctlGanadero, "triun");
            f.txtColorRaza.MaxLength = 100;
            f.txtColorRaza.Text = new string('A', 45);
            await EsperarPesajeGrabadoAsync(f); // el servicio rechaza el texto largo y el formulario marca el campo
            await Task.Delay(300);
            FotoSolo("op-19-pesaje-error-validacion", "Error de validación al grabar",
                "Si un dato no es válido, aparece un icono rojo junto al campo (pase el mouse para leer el motivo) y la boleta no se graba.",
                f, f.txtColorRaza, f.lblGuia, f.btnGrabar);
            f.txtColorRaza.Text = "NELORE";
            f._errorProvider.Clear();

            Clic(f.btnBorrarPesaje);
            await Task.Delay(400);
            FotoSolo("op-20-pesaje-borrado", "Pesaje borrado",
                "«Borrar pesaje (F8)» deja los pesos capturados en blanco para volver a pesar la res.",
                f, f.txtPesoIzquierdo, f.txtPesoDerecho, f.txtPesoTotal, f.lblGuia, f.btnBorrarPesaje);
        });

        // Grabar una res completa.
        await Escena("op-21", async () =>
        {
            var f = await AbrirPesajeAsync();
            await ElegirAsync(f.ctlGanadero, "triun");
            await ElegirAsync(f.ctlCliente, "carn");
            await ElegirAsync(f.ctlFletero, "pedro");
            f.txtColorRaza.Text = "NELORE";
            await ForzarPesoAsync(121.4m);
            Clic(f.btnCapturarIzquierdo);
            await ForzarPesoAsync(0);
            await ForzarPesoAsync(118.6m);
            Clic(f.btnCapturarDerecho);
            await Task.Delay(400);
            await EsperarPesajeGrabadoAsync(f);
            await ForzarPesoAsync(0);
            await Task.Delay(300);
            FotoSolo("op-21-pesaje-grabado", "Boleta grabada",
                "Mensaje verde «Boleta N° … grabada — 240.0 kg»: la boleta quedó registrada; con la impresión automática desactivada se imprime con F9.",
                f, f.lblGuia, f.lblProximaBoleta, f.btnImprimir, f.ctlGanadero, f.ctlCliente);
        });

        // Media res.
        await Escena("op-22", async () =>
        {
            var f = await AbrirPesajeAsync();
            f.cmbDesposte.SelectedIndex = 1;
            await Task.Delay(300);
            await ElegirAsync(f.ctlGanadero, "pampa");
            await ElegirAsync(f.ctlCliente, "super");
            f.txtColorRaza.Text = "CRIOLLO";
            f.cmbSexo.SelectedIndex = 1;
            await ForzarPesoAsync(104.7m);
            await Task.Delay(300);
            FotoSolo("op-22-pesaje-media-res", "Media res: tipo de desposte",
                "En «Media res» se captura un solo lado (izquierdo con F5 o derecho con F6).",
                f, f.cmbDesposte, f.cmbSexo, f.btnCapturarIzquierdo, f.btnCapturarDerecho, f.lblGuia);
            Clic(f.btnCapturarDerecho);
            await Task.Delay(500);
            FotoSolo("op-23-pesaje-media-res-lista", "Media res lista para grabar",
                "Con un solo lado capturado, el total es ese peso y el sistema indica «Listo para grabar (F12).».",
                f, f.txtPesoIzquierdo, f.txtPesoDerecho, f.txtPesoTotal, f.lblGuia, f.btnGrabar);
            await EsperarPesajeGrabadoAsync(f);
            await ForzarPesoAsync(0);
        });

        // Altas rápidas.
        await Escena("op-24", async () =>
        {
            var g = await AbrirAsync<FrmGanaderoEdicion>(c => c.ConfigurarAltaRapida("Hacienda Nueva"));
            FotoSolo("op-24-alta-rapida-ganadero", "Alta rápida de ganadero",
                "Si el ganadero no existe, se puede crear desde el mismo pesaje: se escribe el nombre y el código lo asigna el sistema.",
                g, g.lblCodigo, g.txtCodigo, g.lblNombre, g.txtNombre, g.btnGuardar, g.btnCancelar);
            g.Close();
            var c = await AbrirAsync<FrmClienteEdicion>(x => x.ConfigurarAltaRapida("Carnes del Sur"));
            FotoSolo("op-25-alta-rapida-cliente", "Alta rápida de cliente",
                "El cliente nuevo se crea con su nombre y, si se desea, su dirección; el código también es automático.",
                c, c.lblCodigo, c.txtCodigo, c.lblNombre, c.txtNombre, c.lblDireccion, c.txtDireccion, c.btnGuardar, c.btnCancelar);
        });

        // Historial de pesajes.
        await Escena("op-30", async () =>
        {
            var f = await AbrirAsync<FrmHistorialPesajes>(tamano: new Size(1390, 640));
            await Task.Delay(500);
            f.dgvHistorial.ClearSelection();
            if (f.dgvHistorial.Rows.Count > 0)
            {
                f.dgvHistorial.Rows[0].Selected = true;
                f.dgvHistorial.CurrentCell = f.dgvHistorial.Rows[0].Cells[0];
            }

            await Task.Delay(300);
            FotoSolo("op-30-historial-hoy", "Historial de pesajes del día",
                "El historial muestra los pesajes de un día (hoy al abrir), el más reciente primero, con su número de boleta, ganadero, cliente, fletero y pesos.",
                f, f.dtpDia, f.btnActualizar, f.btnHoy, f.dgvHistorial, f.lblResumen, f.btnReimprimir, f.btnCerrar);

            f.dtpDia.Value = DateTime.Today.AddDays(-2);
            await Task.Delay(500);
            await EstabilizarAsync(f);
            FotoSolo("op-31-historial-otro-dia", "Historial de otro día",
                "Para ver otro día se cambia la fecha; con «Hoy» se vuelve al día actual.", f, f.dtpDia, f.btnHoy, f.dgvHistorial, f.lblResumen);

            f.dtpDia.Value = DateTime.Today.AddDays(-1);
            await Task.Delay(500);
            await EstabilizarAsync(f);
            FotoSolo("op-32-historial-anuladas", "Historial con boletas anuladas",
                "Las boletas anuladas se muestran en el historial con el estado «Anulado».", f, f.dgvHistorial, f.lblResumen);
        });

        // Reimprimir boleta (F3), vista previa en media carta y ticket.
        await Escena("op-40", async () =>
        {
            var f = await AbrirAsync<FrmReimprimirBoleta>(tamano: new Size(560, 480));
            await Task.Delay(400);
            await RegistrarImpresionPreviaAsync(f); // una impresión anterior (solo auditoría, no se imprime nada): la boleta pasa a ser reimpresión
            FotoSolo("op-40-reimprimir-boleta", "Reimprimir boleta",
                "Se escribe el número de boleta y se pulsa «Buscar»; el resumen muestra fecha, ganadero, cliente, desposte, total, estado y cuántas veces se imprimió.",
                f, f.numBoleta, f.btnBuscar, f.lblFecha, f.lblGanadero, f.lblCliente, f.lblDesposte, f.lblTotal, f.lblEstado, f.lblImpresiones, f.btnVistaPrevia, f.btnImprimir, f.btnCerrar);

            await GuardarFormatoImpresionAsync(FormatoBoleta.MediaCarta);
            await AbrirVistaPreviaBoletaAsync(f, "op-41-vista-previa-media-carta", "Vista previa de la boleta en media carta",
                "Vista previa de la boleta en media carta: datos de la empresa, del ganadero, cliente y fletero, pesos y firmas; la leyenda de reimpresión aparece porque la boleta ya se imprimió antes.",
                new Size(900, 700));
            await GuardarFormatoImpresionAsync(FormatoBoleta.Ticket80mm, "A4"); // en la impresora virtual no hay papel de 80 mm: se usa A4 para ver el diseño completo
            await AbrirVistaPreviaBoletaAsync(f, "op-42-vista-previa-ticket", "Vista previa de la boleta en ticket",
                "Vista previa de la misma boleta en formato ticket de 80 mm.",
                new Size(900, 900));
            BorrarConfiguracionImpresion(); // vuelve a «sin configurar»: así el asistente y la pantalla de impresión muestran el estado inicial
        });
    }

    /// <summary>Registra en la auditoría una impresión anterior de la boleta que muestra el formulario (no imprime nada) y vuelve a buscarla.</summary>
    private async Task RegistrarImpresionPreviaAsync(FrmReimprimirBoleta f)
    {
        var datos = Campo<DatosBoleta>(f, "_datos");
        using (var scope = _servicios.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IServicioBoletas>().RegistrarImpresionAsync(
                new RegistroImpresion(datos.PesajeId, datos.NumeroBoleta, false, 1, "Microsoft Print to PDF", FormatoBoleta.MediaCarta, EquipoDemo));
        }

        await f.BuscarParaTestAsync();
        await Task.Delay(300);
    }

    /// <summary>Quita el archivo de configuración de impresión de la carpeta de demostración (equipo «sin configurar»).</summary>
    private void BorrarConfiguracionImpresion()
    {
        var ruta = Path.Combine(_servicios.GetRequiredService<PesajeFaeneo.Infrastructure.Common.AppPaths>().ConfigDir, "impresion.json");
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }

    private async Task GuardarFormatoImpresionAsync(FormatoBoleta formato, string? papel = null)
    {
        var almacen = _servicios.GetRequiredService<IAlmacenConfiguracionImpresion>();
        await almacen.GuardarAsync(new ConfiguracionImpresion(null, formato, papel, 1, false, 0m, 0m));
    }

    /// <summary>Abre la vista previa de la boleta que muestra el formulario de reimpresión (sin imprimir nada) y la captura.</summary>
    private async Task AbrirVistaPreviaBoletaAsync(FrmReimprimirBoleta reimprimir, string id, string titulo, string descripcion, Size tamano)
    {
        var datos = Campo<DatosBoleta>(reimprimir, "_datos");
        using var scope = _servicios.CreateScope();
        var doc = await scope.ServiceProvider.GetRequiredService<IServicioImpresionBoletas>().CrearVistaPreviaAsync(datos.PesajeId);
        var vista = new FrmVistaPrevia(doc, $"Vista previa — boleta N° {datos.NumeroBoleta}");
        Preparar(vista, tamano);
        _abiertos.Add(vista);
        vista.Show();
        Capturador.QuitarEsquinasRedondeadas(vista);
        vista.Activate();
        await Task.Delay(1500);
        FotoSolo(id, titulo, descripcion, vista, vista.ppcVista, vista.btnCerrar);
        vista.Close();
        await Task.Delay(300);
    }
}
#endif

#if CAPTURAS_MANUAL
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Infrastructure.Balanza.Demo;
using PesajeFaeneo.WinForms.Controles;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Administracion;
using PesajeFaeneo.WinForms.Formularios.Boletas;
using PesajeFaeneo.WinForms.Formularios.PuestaEnMarcha;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Formularios.Utilitarios;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.WinForms.Capturas;

internal sealed partial class GuionCapturas
{
    private const string EquipoDemo = "PC-PESAJE-01";

    /// <summary>Cambia en pantalla el texto de las etiquetas que mostrarían datos reales del equipo (nombre, carpetas).</summary>
    private static void Anonimizar(Control raiz)
    {
        foreach (var c in Capturador.Descendientes(raiz).Prepend(raiz))
        {
            if (c is Label l)
            {
                if (l.Text.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                {
                    l.Text = l.Text.Replace(Environment.MachineName, EquipoDemo, StringComparison.OrdinalIgnoreCase);
                }

                if (l.Text.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase) && l.Text.Contains("capturas-build", StringComparison.OrdinalIgnoreCase))
                {
                    l.Text = @"C:\Program Files\PesajeFaeneo";
                }
                else if (l.Text.Contains(@"\Users\", StringComparison.OrdinalIgnoreCase) || l.Text.Contains("capturas-build", StringComparison.OrdinalIgnoreCase))
                {
                    l.Text = @"C:\ProgramData\PesajeFaeneo";
                }
            }
        }
    }

    private async Task EscenasAdministradorAsync()
    {
        await IniciarSesionAsync("admin", _cfg.PasswordAdmin);
        await ForzarPesoAsync(0);

        await Escena("ad-01", async () =>
        {
            var f = await AbrirAsync<FrmAsistentePuestaEnMarcha>(tamano: new Size(900, 800));
            await Task.Delay(400);
            FotoSolo("ad-01-asistente-puesta-en-marcha", "Asistente de puesta en marcha",
                "El asistente lista los pasos para dejar el sistema listo (copias de seguridad, empresa, tipo de pesaje, balanza, impresión, fleteros, usuarios e importación) y marca cuáles faltan.",
                f, f.ChkSimulador, f.BtnFinalizar, f.BtnOmitir, f.LblResumen);
        });

        // ───── Usuarios ─────
        await Escena("ad-10", async () =>
        {
            var f = await AbrirAsync<FrmUsuarios>();
            var dgv = Campo<DataGridView>(f, "dgvUsuarios");
            var i = dgv.Rows.Cast<DataGridViewRow>().ToList().FindIndex(r => (string)r.DataBoundItem!.GetType().GetProperty("NombreUsuario")!.GetValue(r.DataBoundItem)! == "operador");
            dgv.ClearSelection();
            dgv.Rows[i].Selected = true;
            dgv.CurrentCell = dgv.Rows[i].Cells[0];
            await Task.Delay(300);
            FotoSolo("ad-10-usuarios", "Gestión de usuarios",
                "Lista de usuarios con su rol, si están activos o bloqueados y su último acceso; abajo, los botones para crear, editar, desactivar o restablecer la clave.",
                f, dgv, Campo<CheckBox>(f, "chkMostrarInactivos"), Campo<Button>(f, "btnNuevo"), Campo<Button>(f, "btnEditar"), Campo<Button>(f, "btnActivarDesactivar"),
                Campo<Button>(f, "btnRestablecer"), Campo<Button>(f, "btnCerrar"));

            var nuevo = await AbrirAsync<FrmUsuarioEdicion>(c => c.ConfigurarAlta());
            nuevo.txtNombreUsuario.Text = "ana.vaca";
            nuevo.txtNombreCompleto.Text = "ANA VACA SUÁREZ";
            nuevo.txtPassword.Text = "ClaveDemo123";
            nuevo.txtConfirmacion.Text = "ClaveDemo123";
            await Task.Delay(300);
            FotoSolo("ad-11-usuario-nuevo", "Nuevo usuario",
                "Al crear un usuario se escribe su nombre de usuario, su nombre completo, su rol y una contraseña inicial; al ingresar por primera vez tendrá que cambiarla.",
                nuevo, nuevo.txtNombreUsuario, nuevo.txtNombreCompleto, nuevo.cmbRol, nuevo.txtPassword, nuevo.txtConfirmacion, nuevo.btnGuardar, nuevo.btnCancelar);
            nuevo.Close();

            using var scope = _servicios.CreateScope();
            var usuarios = await scope.ServiceProvider.GetRequiredService<Application.Seguridad.IServicioUsuarios>().ListarAsync(true);
            var op = usuarios.First(u => u.NombreUsuario == "operador");
            var rest = await AbrirAsync<FrmRestablecerPassword>(c => { c.UsuarioId = op.Id; c.NombreUsuario = op.NombreUsuario; });
            await Task.Delay(300);
            FotoSolo("ad-12-restablecer-password", "Restablecer la contraseña de un usuario",
                "Para restablecer la clave de un usuario se escribe una contraseña temporal; el usuario deberá cambiarla al ingresar.", rest);
            rest.Close();

            dgv.Focus();
            await CapturarCuadroAsync("ad-13-restablecer-confirmacion", "Confirmar restablecimiento",
                "Pregunta de confirmación antes de restablecer la contraseña del usuario seleccionado.",
                () => { _ = (Task)Invocar(f, "RestablecerPasswordSeleccionadoAsync")!; }, Boton.No);
        });

        // ───── Parámetros ─────
        await Escena("ad-20", async () =>
        {
            var f = await AbrirAsync<FrmParametros>();
            await Task.Delay(500);
            FotoSolo("ad-20-parametros", "Parámetros del sistema",
                "Datos de la empresa que salen en las boletas y los reportes, el logo y el siguiente número de boleta (que queda bloqueado cuando ya se emitió la primera boleta).",
                f, f.txtNombreEmpresa, f.txtDireccion, f.txtCiudad, f.txtTelefono, f.txtRegistroSanitario, f.txtTelefonosReclamos, f.txtObservacionPorDefecto,
                f.grpBoleta, f.numSiguienteBoleta, f.lblNotaNumeracion, f.grpLogo, f.picLogo, f.btnCambiarLogo, f.btnQuitarLogo, f.btnGuardar, f.btnCancelar);
        });

        // ───── Tipo de pesaje (F11) ─────
        await Escena("ad-25", async () =>
        {
            var f = await AbrirAsync<FrmTipoPesaje>();
            await Task.Delay(400);
            FotoSolo("ad-25-tipo-de-pesaje", "Tipo de pesaje",
                "Se elige si la empresa pesa res completa y media res, solo res completa o solo media res; define qué se muestra al pesar, en las boletas y en los reportes.",
                f, f.rbAmbos, f.rbSoloResCompleta, f.rbSoloMediaRes, f.btnGuardar, f.btnCancelar);

            f.rbSoloResCompleta.Checked = true;
            await Task.Delay(300);
            FotoSolo("ad-26-tipo-de-pesaje-aviso", "Cambiar el tipo de pesaje con boletas existentes",
                "Si ya hay boletas del tipo que se deja de pesar, el sistema avisa que seguirán mostrando su tipo de desposte en las pantallas y reportes que las incluyan.",
                f, f.rbSoloResCompleta, f.lblAviso, f.btnGuardar);
            f.Close();

            // Pantalla de pesaje con «Solo res completa»: sin la fila Desposte. Se vuelve a Ambos al terminar.
            using (var scope = _servicios.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<Application.Parametros.IServicioParametros>()
                    .CambiarModoDesposteAsync(Domain.Enums.ModoDesposte.SoloResCompleta);
            }

            try
            {
                var p = await AbrirPesajeAsync();
                await Task.Delay(400);
                FotoSolo("ad-27-pesaje-solo-res-completa", "Pesaje con «Solo res completa»",
                    "Con el tipo de pesaje «Solo res completa» la pantalla de pesaje no muestra la opción de desposte: siempre se capturan los dos lados.",
                    p, p.cmbSexo, p.txtObservacion, p.btnCapturarIzquierdo, p.btnCapturarDerecho, p.lblGuia);
                p.Close();
            }
            finally
            {
                using var scope = _servicios.CreateScope();
                await scope.ServiceProvider.GetRequiredService<Application.Parametros.IServicioParametros>()
                    .CambiarModoDesposteAsync(Domain.Enums.ModoDesposte.Ambos);
            }
        });

        // ───── Configuración de impresión ─────
        await Escena("ad-30", async () =>
        {
            var f = await AbrirAsync<FrmConfiguracionImpresion>();
            Anonimizar(f);
            await Task.Delay(300);
            FotoSolo("ad-30-configuracion-impresion", "Configuración de impresión",
                "Aquí se elige la impresora, el formato de la boleta (media carta o ticket), el papel, las copias, si se imprime al grabar y el ajuste fino de la posición; abajo, la vista previa de una boleta de muestra y la página de calibración.",
                f, f.cmbImpresora, f.cmbFormato, f.cmbPapel, f.numCopias, f.chkImprimirAlGrabar, f.numDesplX, f.numDesplY, f.btnVistaPrevia, f.btnImprimirMuestra, f.btnCalibracion, f.btnGuardar, f.btnCancelar);

            using var scope = _servicios.CreateScope();
            var datos = await scope.ServiceProvider.GetRequiredService<IServicioBoletas>().CrearMuestraAsync();
            var impresor = _servicios.GetRequiredService<IImpresorBoletas>();
            var doc = impresor.CrearVistaPrevia(datos, f.LeerConfiguracionParaTest());
            var vista = new FrmVistaPrevia(doc, "Vista previa — boleta de muestra");
            Preparar(vista, new Size(900, 700));
            _abiertos.Add(vista);
            vista.Show();
            Capturador.QuitarEsquinasRedondeadas(vista);
            vista.Activate();
            await Task.Delay(1500);
            FotoSolo("ad-31-impresion-muestra", "Vista previa de la boleta de muestra",
                "La boleta de muestra permite revisar el formato antes de imprimir; la leyenda «MUESTRA» indica que no es una boleta real.", vista, vista.ppcVista, vista.btnCerrar);
        });

        // ───── Configuración de balanza ─────
        await Escena("ad-40", async () =>
        {
            await ForzarPesoAsync(121.4m);
            var f = await AbrirAsync<FrmConfiguracionBalanza>(tamano: new Size(1024, 860));
            await Task.Delay(800);
            FotoSolo("ad-40-configuracion-balanza", "Configuración de balanza",
                "Pantalla de configuración de la balanza: tipo de conexión, puerto, velocidad y formato de lectura; a la derecha, el estado de la balanza en uso y la prueba de conexión.",
                f, f.cmbTipoConexion, f.cmbPuerto, f.btnRefrescarPuertos, f.cmbBaudios, f.cmbBitsDatos, f.cmbParidad, f.cmbBitsParada, f.txtPatron, f.numLecturas, f.numTolerancia,
                f.numTimeout, f.cmbDelimitador, f.cmbUnidad, f.btnProbarConexion, f.lblEstadoActual, f.lblPesoActual, f.btnReconectar, f.btnGuardar, f.btnCerrar);

            var prueba = f.ProbarConexionParaTestAsync();
            await Task.Delay(4500);
            FotoSolo("ad-41-balanza-prueba-en-curso", "Probando la conexión con la balanza",
                "Durante la prueba se ven las tramas que llegan de la balanza y el peso leído.", f, f.btnProbarConexion, f.btnCancelarPrueba, f.lblEstadoPrueba, f.lblPesoPrueba, f.lstTramas);
            await prueba;
            await ForzarPesoAsync(121.4m); // la balanza en uso se reanuda tras la prueba y vuelve a estabilizarse
            FotoSolo("ad-42-balanza-prueba-resultado", "Resultado de la prueba de conexión",
                "Al terminar, la prueba indica si hubo conexión, cuántas tramas se recibieron y si el peso llegó a estabilizarse.", f, f.lblEstadoPrueba, f.lblPesoPrueba, f.lstTramas);
            await ForzarPesoAsync(0);
        });

        // ───── Copias de seguridad ─────
        await Escena("ad-50", async () =>
        {
            var f = await AbrirAsync<FrmBackupRestaurar>(tamano: new Size(1024, 700));
            Anonimizar(f);
            await Task.Delay(400);
            FotoSolo("ad-50-backup-restaurar", "Copias de seguridad",
                "Lista de copias de seguridad con sus fechas y tamaños, los botones para crear una copia, exportarla o restaurarla, y la configuración de la copia automática diaria.",
                f, f.dgvBackups, f.btnCrear, f.btnExportar, f.btnRestaurar, f.btnRestaurarArchivo, f.btnAbrirCarpeta, f.btnActualizar, f.chkAutomatico, f.chkAlCerrar, f.nudDias,
                f.txtCarpetaSecundaria, f.btnExaminarCarpeta, f.btnGuardarConfiguracion, f.btnCerrar);

            using var scope = _servicios.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IServicioBackup>();
            var lista = await servicio.ListarAsync();
            var elegido = lista.First(b => b.Valido);
            var validacion = await servicio.ValidarAsync(elegido.RutaArchivo);
            var zona = _servicios.GetRequiredService<TimeProvider>().LocalTimeZone;
            var conf = new FrmConfirmarRestauracion(validacion, zona);
            Preparar(conf, null);
            _abiertos.Add(conf);
            conf.Show();
            Capturador.QuitarEsquinasRedondeadas(conf);
            conf.Activate();
            await Task.Delay(500);
            Anonimizar(conf);
            FotoSolo("ad-51-confirmar-restauracion", "Confirmar la restauración",
                "Antes de restaurar, el sistema muestra los datos de la copia y pide escribir la palabra de confirmación; restaurar reemplaza todos los datos actuales.",
                conf, conf.tblDetalles, conf.pnlAdvertencias, conf.lblPrompt, conf.txtConfirmacion, conf.btnRestaurar, conf.btnCancelar);
            conf.Close();
            await servicio.DescartarValidacionAsync(validacion.Token);
        });

        // ───── Importar datos legados ─────
        await Escena("ad-60", async () =>
        {
            var carpeta = Path.Combine(Path.GetTempPath(), "pesaje-demo-importar");
            Directory.CreateDirectory(carpeta);
            var csv = Path.Combine(carpeta, "ganaderos-sistema-anterior.csv");
            await File.WriteAllTextAsync(csv,
                "CODIGO;NOMBRE\r\n9001;ESTANCIA EL ROBLE\r\n9002;GANADERA LA ESPERANZA S.R.L.\r\n9003;FELIPE ARANCIBIA\r\n3;JUAN CARLOS SUAREZ ROCA\r\n9005;\r\n9006;HACIENDA LAS PALMAS\r\n",
                new System.Text.UTF8Encoding(false));

            var f = await AbrirAsync<FrmImportarLegado>(tamano: new Size(1024, 760), configurar: c => c.ElegirArchivoParaTest = () => csv);
            const string rutaMostrada = @"C:\Importar\ganaderos-sistema-anterior.csv";
            await Task.Delay(300);
            FotoSolo("ad-60-importar-paso1", "Importar datos del sistema anterior: paso 1",
                "Primer paso: se elige qué se va a importar (ganaderos, clientes o pesajes) y se busca el archivo del sistema anterior (CSV, TXT, DBF o Access).",
                f, f.cmbTipo, f.txtArchivo, f.btnExaminar, f.cmbCodificacion, f.btnLeer, f.lblInfoArchivo);

            f.cmbTipo.SelectedIndex = 0;
            await f.ExaminarParaTestAsync();
            await Task.Delay(300);
            await f.LeerParaTestAsync();
            await Task.Delay(500);
            var rutaReal = f.txtArchivo.Text;
            f.txtArchivo.Text = rutaMostrada;
            await Task.Delay(200);
            FotoSolo("ad-61-importar-paso2", "Importar datos del sistema anterior: paso 2",
                "Segundo paso: el sistema muestra una muestra del archivo y propone a qué dato corresponde cada columna; se puede corregir con las listas.",
                f, f.txtArchivo, f.dgvMapeo, f.dgvMuestra, f.lblInfoArchivo, f.btnSimular);
            f.txtArchivo.Text = rutaReal;

            await f.SimularParaTestAsync();
            await Task.Delay(500);
            f.txtArchivo.Text = rutaMostrada;
            await Task.Delay(200);
            FotoSolo("ad-62-importar-paso3-simulacro", "Importar datos del sistema anterior: paso 3 (simulacro)",
                "Tercer paso: el simulacro muestra cuántas filas se importarían, cuántas ya existen y cuáles tienen errores, sin cambiar nada; si todo está bien se pulsa «Importar».",
                f, f.btnSimular, f.btnImportar, f.btnInforme, f.lblResumen, f.dgvIncidencias);
            f.txtArchivo.Text = rutaReal;
        });

        // ───── Visor de logs ─────
        await Escena("ad-70", async () =>
        {
            var f = await AbrirAsync<FrmVisorLogs>(tamano: new Size(1024, 700));
            await Task.Delay(600);
            if (f.dgvEntradas.Rows.Count > 0)
            {
                f.dgvEntradas.ClearSelection();
                f.dgvEntradas.Rows[0].Selected = true;
                f.dgvEntradas.CurrentCell = f.dgvEntradas.Rows[0].Cells[0];
            }

            await Task.Delay(300);
            FotoSolo("ad-70-visor-logs", "Visor de logs",
                "El visor de logs muestra el registro técnico del sistema por día, con filtros por nivel y por texto, y el detalle de cada entrada; sirve para el soporte técnico.",
                f, f.cmbArchivo, f.cmbNivel, f.txtBuscar, f.btnFiltrar, f.btnActualizar, f.btnAbrirCarpeta, f.dgvEntradas, f.txtDetalle, f.lblResumen);
        });
    }

    // ───────────────────────── Comunes con sesión (menús completos, barra de estado, Acerca de, cambio de clave) ─────────────────────────

    private async Task EscenasComunConSesionAsync()
    {
        await IniciarSesionAsync("admin", _cfg.PasswordAdmin);
        await ForzarPesoAsync(0);

        await Escena("comun-1", async () =>
        {
            var shell = await AbrirAsync<FrmPrincipal>(tamano: TamanoShell);
            await Task.Delay(800);
            FotoSolo("comun-10-inicio-administrador", "Pantalla de inicio (administrador)",
                "Pantalla de inicio del administrador: ve el menú completo (Pesaje, Boletas, Catálogos, Reportes, Administración, Utilitarios, Sesión y Ayuda).",
                shell, shell.TarjetaPesaje, shell.TarjetaTicket, shell.TarjetaHistorial);

            foreach (var (id, menu, desc) in new[]
            {
                ("comun-11-menu-pesaje", "Pesaje", "Menú «Pesaje»: Registrar pesaje (F2) e Historial de pesajes (F4)."),
                ("comun-12-menu-boletas", "Boletas", "Menú «Boletas»: Reimprimir boleta (F3), Buscar / editar boleta (F6) y Anular boleta."),
                ("comun-13-menu-catalogos", "Catálogos", "Menú «Catálogos»: Ganaderos, Clientes y Fleteros."),
                ("comun-14-menu-reportes", "Reportes", "Menú «Reportes»: Reportes de pesaje, Pesajes por raza y Boletas anuladas."),
                ("comun-15-menu-administracion", "Administración", "Menú «Administración»: Usuarios, Parámetros del sistema, Configuración de balanza, Configuración de impresión y Visor de auditoría."),
                ("comun-16-menu-utilitarios", "Utilitarios", "Menú «Utilitarios»: Backup / Restaurar, Importar datos legados y Visor de logs."),
                ("comun-17-menu-sesion", "Sesión", "Menú «Sesión»: Cambiar contraseña…, Cerrar sesión y Salir."),
                ("comun-18-menu-ayuda", "Ayuda", "Menú «Ayuda»: Asistente de puesta en marcha… y Acerca de…")
            })
            {
                await AbrirMenuAsync(shell, menu);
                FotoSolo(id, $"Menú {menu}", desc, shell);
                CerrarMenus(shell);
                await MenuDetalleAsync(shell, id + "-detalle", menu, desc);
            }

            // Barra de estado: balanza estable (verde), inestable (negro), sin datos y error (rojo), en diagnóstico (gris).
            await ForzarPesoAsync(121.4m);
            await Task.Delay(300);
            FotosBarraEstado(shell, "comun-20-barra-estado-verde", "Barra de estado con la balanza conectada",
                "Barra inferior: empresa, usuario, rol y estado de la balanza. En verde, «Balanza: conectada (COM3 9600 8N1)» con el peso que llega.");

            ControladorBalanzaDemo.PesoForzado = 60m;
            ControladorBalanzaDemo.Ruido = 3m;
            await EsperarHastaAsync(() => _balanza.Instantanea is { Estado: EstadoBalanza.Conectada, Estable: false, PesoKg: not null }, 30_000, "balanza inestable");
            await Task.Delay(300);
            FotosBarraEstado(shell, "comun-23-barra-estado-inestable", "Barra de estado con la balanza inestable",
                "Mientras el peso se mueve, el indicador se ve en negro: todavía no se puede capturar el peso.");
            ControladorBalanzaDemo.Ruido = 0m;
            await ForzarPesoAsync(121.4m);

            ControladorBalanzaDemo.Silencio = true;
            await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.SinDatos, 40_000, "estado sin datos");
            await Task.Delay(400);
            FotosBarraEstado(shell, "comun-21-barra-estado-sin-datos", "Barra de estado con la balanza sin datos",
                "Si la balanza deja de enviar datos, el indicador se pone en rojo («Balanza: sin datos»): hay que revisar el cable y el indicador.");
            ControladorBalanzaDemo.Silencio = false;
            await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.Conectada, 40_000, "balanza reconectada");

            ControladorBalanzaDemo.Desconectada = true;
            await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.Error, 40_000, "estado de error");
            await Task.Delay(400);
            FotosBarraEstado(shell, "comun-22-barra-estado-error", "Barra de estado con error en la balanza",
                "Si se pierde la conexión con la balanza, el indicador se pone en rojo con el motivo del error y el sistema reintenta conectarse solo.");
            ControladorBalanzaDemo.Desconectada = false;
            await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.Conectada, 60_000, "balanza reconectada tras el error");

            // En gris: la balanza en uso se pausa mientras se prueba una configuración (diagnóstico).
            using (var scope = _servicios.CreateScope())
            {
                var config = (await scope.ServiceProvider.GetRequiredService<Application.Balanza.IServicioConfiguracionBalanza>().ObtenerAsync()).Configuracion;
                var prueba = _balanza.ProbarAsync(config, TimeSpan.FromSeconds(6), null);
                await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.EnDiagnostico, 15_000, "estado de diagnóstico");
                await Task.Delay(400);
                FotosBarraEstado(shell, "comun-24-barra-estado-diagnostico", "Barra de estado con la balanza en diagnóstico",
                    "Mientras se prueba la conexión desde «Configuración de balanza», el indicador se ve en gris: «Balanza: en diagnóstico».");
                await prueba;
            }

            await EsperarHastaAsync(() => _balanza.Instantanea.Estado == EstadoBalanza.Conectada, 60_000, "balanza reconectada tras el diagnóstico");
            await ForzarPesoAsync(0);
        });

        await Escena("comun-30", async () =>
        {
            var f = await AbrirAsync<FrmCambioPassword>(c => c.Obligatorio = false);
            Invocar(f, "SetCampos", "claveActual1", "claveNueva123", "claveNueva123");
            await Task.Delay(200);
            FotoSolo("comun-30-cambiar-password", "Cambiar contraseña",
                "Ventana «Cambiar contraseña» (menú Sesión): se escribe la contraseña actual y la nueva dos veces; debe tener entre 6 y 100 caracteres.",
                f, Campo<TextBox>(f, "txtActual"), Campo<TextBox>(f, "txtNueva"), Campo<TextBox>(f, "txtConfirmacion"), Campo<Button>(f, "btnAceptar"), Campo<Button>(f, "btnCancelar"));
        });

        await Escena("comun-31", async () =>
        {
            var f = await AbrirAsync<Formularios.Ayuda.FrmAcercaDe>();
            Anonimizar(f);
            await Task.Delay(300);
            FotoSolo("comun-31-acerca-de", "Acerca de",
                "Ventana «Acerca de»: versión del sistema, equipo y carpetas de datos; el botón «Copiar» deja estos datos en el portapapeles para enviarlos a soporte.", f, f.BtnCopiar, f.BtnCerrar);
        });
    }
}
#endif

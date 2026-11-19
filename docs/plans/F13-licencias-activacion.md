# F13 — Licencias: clave de activación y prueba de 15 días (1.3.0)

Medida antipiratería, igual que en ScaleLogger: el sistema se activa con una clave de un solo equipo o se usa en
prueba durante 15 días. Usa **el mismo servidor y la misma base** que ScaleLogger: proyecto Supabase
`uxzehuncusnaofmfjydk`, tablas `license_keys` y `trials`.

Rama: la actual (`feat/f9-empaquetado`). No hay migraciones de la base local. La versión pasa a **1.3.0** (MINOR).

---

## 0. Referencia: ScaleLogger 0.1.0

Código en `Desktop\ScaleLogger\src-tauri\src\license.rs` y `supabase\migrations\20261008000000_licensing.sql`.

- Funciones RPC públicas (rol `anon`) por PostgREST: `activate_license(p_key, p_hwid)`, `register_trial(p_hwid)`
  y `check_license(p_hwid)`. Las tablas tienen RLS sin políticas.
- Clave: `XXXX-XXXX-XXXX-XXXX-XXXX` (20 caracteres hex en mayúsculas). En la base solo queda su SHA-256, y cada
  clave sirve para un solo equipo. La activación es idempotente en el mismo equipo.
- Equipo (hwid) = `hex(SHA256("hw|" + MachineGuid.lower + "|" + SAL))`. La sal es de cada aplicación.
- El estado local se guarda en `HKCU\Software\Classes\CLSID\{…}\Cfg` con los valores:
  - `d`: inicio de la prueba
  - `l`: última vez visto
  - `s`: firma de `d|l|hwid`
  - `a`: token de activación

  Todos se firman con SHA-256 y la sal.
- Prueba de 15 días:
  - Se registra en el servidor y, al reconciliar, gana la fecha más antigua.
  - Si el reloj retrocede más de 1 día, la prueba vence.
  - Si la firma no coincide, se considera alterada.
- Al arrancar se sincroniza una vez, en segundo plano. Una revocación borra `a`.

**Problema:** la base no distingue producto. Usada tal cual, una clave de ScaleLogger activaría Pesalab y al revés.

---

## 1. Decisiones

| # | Decisión |
|---|---|
| D1 | **Columna `product` en el servidor** (decisión del usuario). La migración es `supabase/migrations/20261013000000_licencias_por_producto.sql`: agrega `product` a `license_keys` y `trials` (por defecto `'scalelogger'`), la clave primaria de `trials` pasa a `(product, hardware_id)` y suma las funciones `activate_product_license(p_key, p_hwid, p_product)`, `register_product_trial(p_hwid, p_product)` y `check_product_license(p_hwid, p_product)`. Las tres funciones de ScaleLogger conservan firma y respuesta como envoltorios de `'scalelogger'`, así que ScaleLogger 0.1.0 no se recompila. Para administrar están `generate_product_license_keys(p_product, n, nota)`, `revoke_license_key(clave)` y `release_license_key(clave)`. Producto de Pesalab: **`pesajefaeneo`**. La aplica el usuario desde el SQL Editor. |
| D2 | **Mismo esquema local que ScaleLogger, con identidad propia.** La sal es `pf-86372349-fb7c-4def-8add-f9b6b87ad2c2` y el estado vive en `HKCU\Software\Classes\CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}\Cfg`, con los valores `d`, `l`, `s` y `a` en epoch y hex en minúsculas. Fórmulas: `hwid = SHA256("hw|{guid}|{sal}")`, `s = SHA256("trial|{d}|{l}|{hwid}|{sal}")` y `a = SHA256("act|{hwid}|{sal}")`. Si no se puede leer `MachineGuid`, se usa `Environment.MachineName` en minúsculas y nunca la cadena vacía. |
| D3 | **Estados:** `Activada`, `Prueba(díasRestantes)`, `NoIniciada` y `Vencida(motivo)`. Los motivos son `PruebaTerminada`, `Reloj` y `Alterada`. El cálculo es puro y está en `ReglasLicencia.Calcular`; hace lo mismo que `compute_status`: tolera 1 día de atraso del reloj, usa `max(ahora, l)` y reescribe `l` cuando avanzó ≥ 1 h. Los días restantes son `15 − ⌊(efectivo − d) / 86400⌋`, y con ≤ 0 la prueba queda vencida. |
| D4 | **Vencida = modo consulta** (decisión del usuario). Se puede iniciar sesión, consultar el historial y los reportes, reimprimir, editar y anular boletas, y hacer copias de seguridad. **No se puede pesar** (grabar boletas) **ni importar datos legados**. Se bloquea en el servicio con `ExigirOperacion()` dentro de `ServicioPesajes.RegistrarAsync` y `ServicioImportacion` (importar y simular), con `ReglaNegocioException("LIC_MODO_CONSULTA")`, antes de abrir la transacción. En la interfaz, "Control de peso" (menú, tarjeta y F2) e "Importar datos legados" se deshabilitan con aviso. Una prueba que vence durante la sesión se detecta al grabar. |
| D5 | **Pantalla de activación al arrancar** (`FrmActivacion`, en `Program.Main` después de preparar la base y antes del login). Solo aparece con `NoIniciada` o `Vencida`. Con `NoIniciada`: **Activar**, **Probar 15 días** y **Salir**. Con `Vencida`: **Activar**, **Continuar en modo consulta** y **Salir**. Desde el menú: **Activar** y **Cerrar**. Muestra el "Código de equipo" (los primeros 8 caracteres del hwid, en `XXXX-XXXX`) para que soporte ubique la fila. |
| D6 | **Sin internet.** La primera activación necesita internet. La prueba puede iniciarse sin conexión (se registra en el servidor al sincronizar). Una vez activado, el sistema funciona sin conexión indefinidamente. |
| D7 | **Sincronización** en segundo plano una vez por arranque, después de abrir el shell, con `Task.Run` y sin bloquear la interfaz. Hace lo mismo que `sync_blocking`: con revocación borra `a`, reactiva desde el servidor, reconcilia hacia la fecha de prueba más antigua y registra la prueba si el servidor no la tiene. Los errores de red solo se registran en el log. La barra de estado se actualiza al terminar. |
| D8 | **Cliente HTTP** en Infrastructure. Un `HttpClient` propio con 8 s de espera hace `POST {url}/rest/v1/rpc/{función}` con las cabeceras `apikey` y `Authorization: Bearer`. La URL y la clave anon son **constantes de código**, no `appsettings.json`, para que no se pueda redirigir a otro servidor editando un archivo. La clave anon es pública por diseño; jamás se usa la `service_role`. |
| D9 | **Validación local de la clave**: se quitan los caracteres no alfanuméricos y deben quedar 20 hexadecimales. Si no, el mensaje es "La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX." y no se llama al servidor. Los mensajes del servidor son "Esta clave ya fue utilizada en otro equipo.", "Esta clave fue revocada." y "Clave de activación inválida.". Sin red: "No se pudo contactar al servidor de licencias. Revise la conexión a internet.". La clave nunca va al log ni a la auditoría. |
| D10 | **Shell.** La barra de estado muestra "Prueba: N días" (naranja) o "Modo consulta" (rojo); el texto es un enlace que abre la activación y no se muestra si el sistema está activado. **Ayuda › Activar licencia…** aparece mientras no esté activado. "Acerca de" muestra el estado. Si la activación se hace desde el shell, se rehabilita Control de peso sin reiniciar. |
| D11 | **Fuera del control de licencia:** `--autoprueba`, `--restablecer-admin` (sale antes) y el build `CAPTURAS_MANUAL`. Este último usa un almacén en memoria ya activado y un servidor de demostración, y nunca toca la red ni el registro real. |
| D12 | **Riesgos aceptados** (iguales o mayores que en ScaleLogger): .NET se descompila con facilidad, así que la sal y las fórmulas se pueden extraer y el token `a` se puede falsificar (mitigarlo exigiría una respuesta firmada por el servidor con Ed25519 y una función Edge, fuera de alcance). Si se borra el registro sin conexión, la prueba arranca de nuevo, pero se corrige en la siguiente sincronización. El estado es por usuario de Windows (HKCU): otro usuario del mismo equipo lo recupera del servidor al sincronizar. Un equipo revocado vuelve a su estado de prueba. |

---

## 2. Diseño por capas

**Application, `Application/Licencias`:**
- `EstadoLicencia` (record) con `TipoEstadoLicencia`, `MotivoVencimiento`, `DiasRestantes`, `PermiteOperar` y `Texto`.
- `ReglasLicencia`: constantes (15 días, 86 400 s, tolerancia del reloj y refresco de 1 h), `Calcular`, `NormalizarClave` y `ClaveConFormatoValido`.
- `IServicioLicencia`, implementado por `ServicioLicencia` (singleton, con un candado interno). Operaciones: `ObtenerEstado`, `ExigirOperacion`, `CodigoEquipo`, `ActivarAsync`, `IniciarPruebaAsync` y `SincronizarAsync`, más el evento `EstadoCambiado`.
- Abstracciones:
  - `IAlmacenLicenciaLocal`: `Leer() → DatosLicenciaLocal(d?, l?, s?, a?)` y escrituras.
  - `IServidorLicencias`: `ActivarAsync`, `RegistrarPruebaAsync` y `ConsultarAsync`. Lanza `ServidorLicenciasException`.
  - `IIdentificadorEquipo`: `Hwid`.
  - `FirmaLicencia`: las fórmulas SHA-256 de D2. Es pura y testeable.

**Infrastructure, `Infrastructure/Licencias`:**
- `ServidorLicenciasSupabase`, con `HttpMessageHandler` inyectable para los tests.
- `AlmacenLicenciaRegistro`, con `[SupportedOSPlatform("windows")]` y la subclave inyectable para los tests.
- `IdentificadorEquipoWindows`.
- `ConstantesLicencia`: URL, anon, producto, sal y CLSID.
- En `#if CAPTURAS_MANUAL`: `AlmacenLicenciaMemoria` y `ServidorLicenciasDemo`.

**WinForms:**
- `Formularios/Licencias/FrmActivacion`.
- `Program.Main`: control previo al login y sincronización en segundo plano.
- `FrmPrincipal`: barra de estado, menú Ayuda y bloqueo de Control de peso e Importar.
- `FrmAcercaDe`.

---

## 3. Tareas

| T | Contenido |
|---|---|
| T1 | Migración del servidor (hecha, sin aplicar): el archivo SQL con verificaciones y vuelta atrás. Copiarla también a `Desktop\ScaleLogger\supabase\migrations`. |
| T2 | Application: `Licencias/*` y la integración `ExigirOperacion` en `ServicioPesajes` y `ServicioImportacion`. Registro en DI. |
| T3 | Infrastructure: servidor, almacén, identificador, constantes y la variante de capturas. Registro en DI. |
| T4 | WinForms: `FrmActivacion`, `Program.Main`, `FrmPrincipal` y `FrmAcercaDe`. |
| T5 | Pruebas: `ReglasLicencia` (todas las ramas de D3), `FirmaLicencia` (vectores conocidos), `ServicioLicencia` con dobles (activar ×4 resultados, formato, prueba con y sin conexión, sincronización ×5 casos, refresco de `l`), `ServidorLicenciasSupabase` con un handler falso (URL, cabeceras, cuerpo con `p_product = "pesajefaeneo"`, errores, timeout), `AlmacenLicenciaRegistro` en una subclave temporal de HKCU, `ServicioPesajes`/`ServicioImportacion` en modo consulta, `FrmActivacion` (botones por modo, validación, resultados), `FrmPrincipal` en modo consulta y `ContenedorDITests`. |
| T6 | Documentación: versión 1.3.0, `CHANGELOG.md`, `README.md` (sección Licencias), guía de instalación y soporte (activación, cambio de PC, revocación, consultas SQL para generar claves), manuales (Administrador: activación y modo consulta; Operador y Supervisor: aviso breve), casos UAT-F13-01 a 08 y bitácora en `AGENTS.md`. |

---

## 4. Criterios de aceptación

1. Instalación nueva → antes del login aparece "Activar Pesaje Faeneo" con Activar, Probar 15 días y Salir.
2. "Probar 15 días" → se entra normal; la barra muestra "Prueba: 15 días"; el servidor tiene la fila `trials(pesajefaeneo, hwid)`.
3. Una clave válida de `pesajefaeneo` activa el equipo; la barra no muestra nada de licencia; funciona sin internet.
4. Una clave de ScaleLogger → "Clave de activación inválida.". La misma clave de Pesalab en otro equipo → "Esta clave ya fue utilizada en otro equipo.".
5. Prueba vencida (reloj adelantado 16 días) → pantalla con "Continuar en modo consulta": se ve el historial y los reportes; Control de peso queda deshabilitado; grabar por servicio da `LIC_MODO_CONSULTA`.
6. Reloj atrasado más de 1 día durante la prueba → vencida por reloj. Si se edita `d` en el registro, queda alterada.
7. Si se revoca la clave en el servidor, en el siguiente arranque con internet el sistema pasa a modo consulta (o a prueba, si le quedaban días).
8. ScaleLogger 0.1.0 sigue activando, registrando pruebas y consultando como antes.

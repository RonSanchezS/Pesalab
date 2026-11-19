# F13-T6 — Plan de ejecución paso a paso (documentación, versión 1.3.0 e instalador)

Este plan está escrito para que lo ejecute **un agente de código sin contexto previo** (opencode u otro modelo más
chico). Siga los pasos **en orden** y **al pie de la letra**. No hay que tomar decisiones de diseño: cada texto a
escribir está en este archivo.

Contexto: la funcionalidad de licencias (fase F13, plan `docs/plans/F13-licencias-activacion.md`) **ya está
programada y probada**: compila sin advertencias y pasan 1952 pruebas. Falta la tarea **T6**: la versión, el
CHANGELOG, el README, las guías, los manuales, el checklist UAT, la bitácora y el instalador.

---

## 0. Reglas obligatorias (leer antes de empezar)

1. **No modifique nada dentro de `src/` ni de `tests/`.** El código está terminado. Si un paso falla por el código,
   **deténgase y avise**; no lo "arregle".
2. **No haga commit, push, merge ni cambie de rama.** Deje todo en el árbol de trabajo.
3. **No ejecute SQL, no se conecte a Supabase, no abra el sistema y no toque el registro de Windows.**
4. **Codificación:** todo archivo que edite debe quedar en **UTF-8 sin BOM** y con finales de línea **CRLF**
   (`.editorconfig`). Use la herramienta de edición de su entorno. No use `Set-Content` ni `Out-File` de
   Windows PowerShell 5.1, porque agregan BOM o cambian la codificación.
5. **Ubique por texto, no por número de línea.** Cuando un paso dice "inserte antes de la línea `X`", busque ese
   texto exacto. Si no lo encuentra, o aparece más de una vez, **deténgase y avise**.
6. **Copie los textos tal cual**, con sus tildes, comillas «», el signo `›` (no `>`), las negritas `**` y los
   acentos graves `` ` ``. No resuma ni reescriba. La primera y la última línea de cada bloque de
   ejemplo (las de tres o cuatro acentos graves, con o sin la palabra `markdown`) **no** se copian. Los bloques SQL que
   están **dentro** de un bloque de cuatro acentos graves (pasos 4 y 5i) **sí** se copian, con sus líneas de tres
   acentos graves.
7. Donde diga `<FECHA>`, ponga la fecha del día en que ejecuta, en formato `AAAA-MM-DD`.
8. Todos los comandos se ejecutan desde la raíz del repositorio:
   `C:\Users\Gary Ronald\Downloads\Pesalab-new`.
9. Cada paso termina con una **Verificación**. Si no da lo esperado, **no siga**: informe el paso, el comando y la
   salida.
10. Los pasos 12 y 14 tienen una **COMPUERTA**: hay que preguntarle al usuario y esperar su respuesta.

---

## Paso 1 — Verificar el punto de partida

Ejecute:

```powershell
git status --short src/PesajeFaeneo.Application/Licencias src/PesajeFaeneo.Infrastructure/Licencias src/PesajeFaeneo.WinForms/Formularios/Licencias supabase docs/plans
dotnet build src\PesajeFaeneo.sln -c Release -v q
dotnet test tests\PesajeFaeneo.Tests -c Release --no-build
```

**Verificación:**
- `git status` muestra (con `??`) las carpetas `Licencias`, `supabase/` y los dos planes F13.
- El build termina con `0 Warning(s)` y `0 Error(s)`.
- La prueba termina con `Failed: 0, Passed: 1952`. Si el número de aprobadas es otro, o hay fallos, deténgase.

---

## Paso 2 — Versión 1.3.0

Archivo: `Directory.Build.props` (raíz).

Reemplace exactamente:

```
    <Version>1.2.1</Version>
```

por:

```
    <Version>1.3.0</Version>
```

**Verificación:** `Select-String -Path Directory.Build.props -Pattern '<Version>1.3.0</Version>'` encuentra 1 línea.

---

## Paso 3 — CHANGELOG

Archivo: `CHANGELOG.md`.

Inserte el bloque siguiente **inmediatamente antes** de la línea
`## [1.2.1] - 2026-10-13 - Correcciones de la revisión de manuales (F12)`, dejando una línea en blanco entre el
bloque y esa línea:

```markdown
## [1.3.0] - <FECHA> - Licencias: clave de activación y prueba de 15 días (F13)

Medida antipiratería, igual que en ScaleLogger. El sistema se activa con una clave de un solo equipo o se usa en prueba durante 15 días. Usa el mismo servidor de licencias y la misma base que ScaleLogger, con el producto `pesajefaeneo` (plan `docs/plans/F13-licencias-activacion.md`). No hay migraciones de la base local.

### Agregado
- **Pantalla «Activar Pesaje Faeneo»** antes del inicio de sesión cuando el equipo no está activado:
  - Con el sistema sin activar ofrece **Activar**, **Probar 15 días** y **Salir**.
  - Con la prueba vencida ofrece **Activar**, **Continuar en modo consulta** y **Salir**.
  - Muestra el **Código de equipo**, que soporte usa para ubicar el equipo en el servidor.
- **Clave de activación** con formato `XXXX-XXXX-XXXX-XXXX-XXXX`, válida para un solo equipo. La primera activación necesita internet; después el sistema funciona sin conexión. Reinstalar en el mismo equipo no consume otra clave.
- **Prueba de 15 días:**
  - Se registra en el servidor; si se borran los datos locales, se recupera la fecha original.
  - Vence si se atrasa el reloj de Windows más de un día o si se alteran los datos de la prueba.
- **Modo consulta** (prueba vencida o clave revocada):
  - Se puede entrar, consultar el historial y los reportes, reimprimir, editar y anular boletas, y hacer copias de seguridad.
  - No se puede **pesar** ni **importar datos legados**. El servicio también lo rechaza (`LIC_MODO_CONSULTA`).
- **Barra de estado:** muestra «Prueba: N días» (naranja) o «Modo consulta» (rojo); con un clic se abre la activación. Si el sistema está activado no muestra nada.
- **Ayuda › Activar licencia…**, visible mientras el sistema no esté activado. **Ayuda › Acerca de…** muestra la licencia y el código de equipo.
- **Sincronización con el servidor** una vez por arranque, en segundo plano: detecta revocaciones y restaura la activación o la fecha de la prueba.

### Servidor de licencias (Supabase, compartido con ScaleLogger)
- Migración `supabase/migrations/20261013000000_licencias_por_producto.sql`. Agrega la columna `product` a `license_keys` y `trials` y las funciones `activate_product_license`, `register_product_trial` y `check_product_license`.
- Para administración agrega `generate_product_license_keys`, `revoke_license_key` y `release_license_key`.
- Las funciones de ScaleLogger 0.1.0 conservan firma y respuesta, así que ScaleLogger no se recompila.

### Documentación
- README (sección «Licencias y activación»), guía de instalación y soporte (activación, actualización a 1.3.0 y administración de claves en la sección 16), manual técnico, manuales de cliente 1.3.0 (nuevo capítulo «Licencia del sistema» en el del Administrador y aviso de modo consulta en los del Operador y el Supervisor) y checklist UAT (UAT-F13-01 a 08).
```

**Verificación:** `Select-String -Path CHANGELOG.md -Pattern '^## \[1.3.0\]'` encuentra 1 línea, y aparece **antes**
de la de `[1.2.1]`.

---

## Paso 4 — README

Archivo: `README.md`.

Inserte el bloque siguiente **inmediatamente antes** de la línea `## Publicación e instalador (F9)`, con una línea en
blanco antes y otra después:

````markdown
## Licencias y activación (F13)

El sistema se activa con una **clave de un solo equipo** (`XXXX-XXXX-XXXX-XXXX-XXXX`) o se usa en **prueba durante 15 días**. Plan: `docs/plans/F13-licencias-activacion.md`.

- **Servidor:** el mismo proyecto Supabase que ScaleLogger (`uxzehuncusnaofmfjydk`), con el producto `pesajefaeneo`. La aplicación solo llama a las funciones RPC `activate_product_license`, `register_product_trial` y `check_product_license` con la clave pública `anon`. La URL y la clave están en el código (`Infrastructure/Licencias/ConstantesLicencia.cs`), no en `appsettings.json`.
- **Migración del servidor:** `supabase/migrations/20261013000000_licencias_por_producto.sql`. Se aplica a mano en el SQL Editor de Supabase; las verificaciones y la vuelta atrás están al final del archivo.
- **Estado local:** `HKCU\Software\Classes\CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}\Cfg`, con valores firmados con SHA-256 y el identificador del equipo. El identificador sale del `MachineGuid` de Windows.
- **Estados:** Activada; Prueba (N días); Sin activar; Vencida, que equivale al modo consulta: no se puede pesar ni importar.
- **Fuera del control de licencia:** `--autoprueba`, `--restablecer-admin` y el build de capturas (`CAPTURAS_MANUAL`, que usa una licencia de demostración en memoria).
- **Generar claves** (SQL Editor de Supabase):

  ```sql
  select * from public.generate_product_license_keys('pesajefaeneo', 5, 'Cliente o lote');
  ```

  Las claves se muestran **una sola vez**: en la base solo queda su hash. La revocación, la liberación por cambio de PC y las consultas de soporte están en la guía de instalación y soporte, sección 16.
````

**Verificación:** `Select-String -Path README.md -Pattern '^## Licencias y activación \(F13\)'` encuentra 1 línea.

---

## Paso 5 — Guía de instalación y soporte

Archivo: `docs/manual/guia-instalacion-soporte.md`. Son **nueve** ediciones (5a a 5i).

**5a.** En la primera línea de texto, reemplace `Corresponde a la versión **1.2.1** del Sistema` por
`Corresponde a la versión **1.3.0** del Sistema`.

**5b.** En la sección 2, reemplace `(por ejemplo `PesajeFaeneo-Setup-1.2.1-x64.exe`)` por
`(por ejemplo `PesajeFaeneo-Setup-1.3.0-x64.exe`)`.

**5c.** En la tabla de la sección 1, inserte esta fila **inmediatamente después** de la fila que empieza con
`| Otros programas |`:

```markdown
| Internet | Solo para **activar la licencia** la primera vez (y, de forma opcional, para sincronizarla al abrir el sistema). Una vez activado, el sistema funciona sin conexión. |
```

**5d.** En la sección 4, al final del paso que empieza con `1. Abra **Sistema de Pesaje para Faeneo**. En la
primera ejecución`, agregue al final del mismo renglón (separado por un espacio):

```
Antes del inicio de sesión aparece la pantalla **Activar Pesaje Faeneo** (ver «Activación de la licencia» al final de esta sección).
```

**5e.** Inserte el bloque siguiente **inmediatamente antes** de la línea `## 5. Actualizar a una versión nueva`,
con una línea en blanco antes y otra después:

```markdown
### Activación de la licencia

En un equipo sin activar, antes del inicio de sesión aparece **Activar Pesaje Faeneo** con:

- **Activar:** escriba la clave entregada (`XXXX-XXXX-XXXX-XXXX-XXXX`; no importan las mayúsculas ni los guiones) y pulse **Activar**. Necesita internet. Cada clave sirve para **un solo equipo**. Si se reinstala Windows o el sistema en el mismo equipo, la misma clave vuelve a funcionar.
- **Probar 15 días:** el sistema funciona completo durante 15 días. Puede iniciarse sin internet; la fecha se registra en el servidor la próxima vez que el sistema abra con conexión.
- **Salir:** cierra el sistema.

La ventana muestra también el **Código de equipo** (por ejemplo `A1B2-C3D4`). Anótelo si va a pedir soporte por la licencia: lo usa la sección 16.

Durante la prueba, la barra de estado muestra **Prueba: N días**. Para activar más tarde, use **Ayuda › Activar licencia…** o haga clic en ese texto.

Cuando la prueba vence, la pantalla ofrece **Continuar en modo consulta**. En ese modo se puede entrar y consultar el historial y los reportes, reimprimir, editar y anular boletas y hacer copias de seguridad, pero **no se puede pesar ni importar datos legados**. La barra de estado muestra **Modo consulta**. La prueba también vence si se atrasa el reloj de Windows más de un día («Se detectó un cambio en el reloj del sistema.») o si se modifican los datos de la prueba («Se detectó una alteración en los datos de la prueba.»).

El estado de la licencia se ve en **Ayuda › Acerca de…** (líneas **Licencia** y **Código de equipo**).
```

**5f.** Inserte el bloque siguiente **inmediatamente antes** de la línea `### Actualizar de 1.2.0 a 1.2.1`, con una
línea en blanco antes y otra después:

```markdown
### Actualizar a 1.3.0 (licencias)

La versión 1.3.0 agrega la **activación por clave** y la **prueba de 15 días**. **No hay migraciones de base de datos** respecto de la 1.2.0 y la 1.2.1, así que desde esas versiones no se crea la copia `premigracion`.

**Importante:** al abrir la 1.3.0 por primera vez, el equipo **no está activado** y aparece la pantalla **Activar Pesaje Faeneo**. Tenga la clave del cliente a mano antes de actualizar (ver sección 16) y un equipo con internet para activarla. Si todavía no la tiene, puede elegir **Probar 15 días** y activar después con **Ayuda › Activar licencia…**. Los datos, los usuarios y la configuración no cambian.
```

**5g.** Reemplace la línea `### Actualizar de 1.0.0 o 1.1.0 a 1.2.1` por `### Actualizar de 1.0.0 o 1.1.0 a 1.3.0`.

Dentro de esa misma subsección, reemplace `(la 1.2.1 no agrega migraciones respecto de la 1.2.0)` por
`(la 1.2.1 y la 1.3.0 no agregan migraciones respecto de la 1.2.0)`.

Al final del párrafo que empieza con `Estas dos versiones traen cambios en la base de datos`, después de la tabla de
migraciones y antes de `Qué cambia para el Administrador después de actualizar:`, agregue este párrafo (con una línea
en blanco antes y otra después):

```markdown
Además, como en toda actualización a 1.3.0, al abrir el sistema aparece **Activar Pesaje Faeneo** (ver «Actualizar a 1.3.0 (licencias)»).
```

**5h.** Reemplace
`(para volver de 1.2.1, el instalador de la versión que estaba instalada: 1.0.0, 1.1.0 o 1.2.0)` por
`(para volver de 1.3.0, el instalador de la versión que estaba instalada: 1.0.0, 1.1.0, 1.2.0 o 1.2.1)`.

**5i.** Al **final del archivo** (después de la última viñeta de la sección 15, la que empieza con `- **Antivirus:**`),
agregue primero estas viñetas a la lista de la sección 15, sin línea en blanco entre ellas y la anterior:

```markdown
- **Licencia por equipo y por usuario de Windows:** la activación queda guardada en el perfil del usuario de Windows (registro `HKCU`). Otro usuario de Windows del mismo equipo la recupera del servidor al abrir el sistema **con internet**; sin conexión verá la pantalla de activación.
- **Protección de la licencia:** la clave se valida contra el servidor, pero los datos locales se protegen con una firma que un atacante con conocimientos podría falsificar. Es una medida disuasoria, no una protección absoluta.
```

y después, con una línea en blanco antes, esta sección nueva:

````markdown
## 16. Licencias: claves de activación (soporte)

Las claves se administran en el proyecto Supabase compartido con ScaleLogger (`uxzehuncusnaofmfjydk`), desde **SQL Editor**. Todas las consultas usan el producto `pesajefaeneo`. **Nunca** copie la clave `service_role` en un archivo ni en el código.

**Requisito previo (una sola vez):** haber aplicado `supabase/migrations/20261013000000_licencias_por_producto.sql` y sus verificaciones (ver el final de ese archivo).

### Generar claves

```sql
select * from public.generate_product_license_keys('pesajefaeneo', 1, 'Nombre del cliente');
```

Cambie `1` por la cantidad de claves. Copie las claves en ese momento y entréguelas por un medio seguro: **no se pueden volver a ver** (en la base solo queda su hash). La nota (tercer parámetro) sirve para saber de quién es cada clave.

### Ver el estado de las claves de un cliente

```sql
select status, left(hardware_id, 8) as codigo_equipo, activated_at, note, created_at
  from public.license_keys
 where product = 'pesajefaeneo' and note ilike '%cliente%'
 order by created_at;
```

`status`: `available` (sin usar), `used` (activada en un equipo) o `revoked` (revocada). `codigo_equipo` coincide con el **Código de equipo** que muestra el sistema, en minúsculas y sin guion.

### Buscar un equipo por su código

Si el cliente le dicta el **Código de equipo** `A1B2-C3D4`, escríbalo en minúsculas y sin guion:

```sql
select status, activated_at, note from public.license_keys
 where product = 'pesajefaeneo' and hardware_id like 'a1b2c3d4%';
select started_at from public.trials
 where product = 'pesajefaeneo' and hardware_id like 'a1b2c3d4%';
```

### Cambio de computadora

Una clave usada queda ligada a su equipo. Para activarla en otra PC:

```sql
select public.release_license_key('XXXX-XXXX-XXXX-XXXX-XXXX');
```

Responde `liberada`. Luego active la clave en la PC nueva. **El equipo anterior sigue activado** mientras no se le revoque una clave: si el cliente se queda con las dos PC, entregue una clave nueva en lugar de liberar la anterior.

### Revocar una clave

```sql
select public.revoke_license_key('XXXX-XXXX-XXXX-XXXX-XXXX');
```

Responde `revocada`. La próxima vez que ese equipo abra el sistema **con internet**, pierde la activación: vuelve a la prueba si le quedaban días o pasa a **modo consulta**.

### Mensajes de la pantalla de activación

| Mensaje | Causa | Qué hacer |
| :--- | :--- | :--- |
| «La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.» | Faltan o sobran caracteres, o hay letras que no van de la A a la F. | Revise la clave carácter por carácter (son números del 0 al 9 y letras de la A a la F). |
| «Clave de activación inválida.» | La clave no existe, o es de otro producto (por ejemplo, de ScaleLogger). | Compruebe que es una clave de `pesajefaeneo` (consulta «Ver el estado»). |
| «Esta clave ya fue utilizada en otro equipo.» | La clave está activada en otra PC. | Si es un cambio de PC, libérela (ver «Cambio de computadora»). |
| «Esta clave fue revocada.» | Se revocó en el servidor. | Entregue una clave nueva. |
| «No se pudo contactar al servidor de licencias. Revise la conexión a internet.» | Sin internet, o un firewall bloquea `https://uxzehuncusnaofmfjydk.supabase.co`. | Conecte el equipo a internet o pida que se permita ese dominio. Mientras tanto puede usar **Probar 15 días**. |
| «No se pudo guardar la información de la licencia en este equipo…» | El usuario de Windows no puede escribir en su propio registro (política del equipo). | Pruebe con otro usuario de Windows o revise las políticas del equipo. |

### Reiniciar la licencia de un equipo de pruebas (solo desarrollo)

Para volver un equipo de pruebas a «Sin activar»:

1. Borre la clave del registro `HKCU\Software\Classes\CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}`.
2. Borre en el servidor sus filas: `delete from public.trials where product = 'pesajefaeneo' and hardware_id like '<código>%';` y libere su clave.

Si no se hacen las dos cosas, la sincronización vuelve a restaurar la activación o la prueba anterior.
````

**Verificación del paso 5:**

```powershell
Select-String -Path docs\manual\guia-instalacion-soporte.md -Pattern '1\.2\.1'
```

Las coincidencias que **deben** quedar son solo estas cinco:
- la línea `### Actualizar de 1.2.0 a 1.2.1`;
- el párrafo siguiente, que empieza con `La versión 1.2.1 solo corrige la interfaz`;
- `respecto de la 1.2.0 y la 1.2.1` (paso 5f);
- `(la 1.2.1 y la 1.3.0 no agregan migraciones` (paso 5g);
- `1.0.0, 1.1.0, 1.2.0 o 1.2.1` (paso 5h).

Si aparece otra (por ejemplo, en la primera línea o en el ejemplo del instalador de la sección 2), falta una
edición. Además, `Select-String -Path docs\manual\guia-instalacion-soporte.md -Pattern '^## 16\. Licencias'`
encuentra 1 línea, y lo mismo con `-Pattern '^### Activación de la licencia'`.

---

## Paso 6 — Manual técnico (`docs/manual/manual-usuario.md`)

**6a.** En la sección `### Barra de estado (parte inferior)`, reemplace la línea
`6. La versión del sistema (por ejemplo `v1.2.1`).` por estas dos líneas:

```markdown
6. La versión del sistema (por ejemplo `v1.3.0`).
7. Solo si el sistema no está activado: **Prueba: N días** (naranja) o **Modo consulta** (rojo). Un clic abre la activación (ver la sección 12).
```

**6b.** Inserte el bloque siguiente **inmediatamente antes** de la línea `## Anexo A. Teclas rápidas`, con una línea
en blanco antes y otra después:

```markdown
## 12. Licencia: activación, prueba y modo consulta

El sistema se activa con una **clave de activación** (`XXXX-XXXX-XXXX-XXXX-XXXX`) que sirve para un solo equipo, o se usa en **prueba durante 15 días**.

### Pantalla «Activar Pesaje Faeneo»

Aparece **antes del inicio de sesión** cuando el equipo no está activado:

| Situación | Texto de la pantalla | Botones |
| :--- | :--- | :--- |
| Sin activar | «Ingrese su clave de activación. Si todavía no tiene una, puede probar el sistema durante 15 días.» | **Activar**, **Probar 15 días**, **Salir** |
| Prueba vencida | «La prueba de 15 días terminó. Ingrese una clave de activación o continúe en modo consulta (podrá ver el historial, los reportes y las copias de seguridad, pero no pesar).» | **Activar**, **Continuar en modo consulta**, **Salir** |
| Reloj atrasado | «Se detectó un cambio en el reloj del sistema. …» (mismo final) | Igual que la prueba vencida |
| Datos alterados | «Se detectó una alteración en los datos de la prueba. …» (mismo final) | Igual que la prueba vencida |
| Desde **Ayuda › Activar licencia…** | «Ingrese su clave de activación para desbloquear la versión completa.» | **Activar**, **Cerrar** |

Debajo de la clave se ve el **Código de equipo** (por ejemplo `A1B2-C3D4`), que soporte usa para ubicar el equipo.

La primera activación necesita internet. Después, el sistema funciona sin conexión. Cada vez que se abre, consulta el servidor en segundo plano (si hay internet) para detectar si la clave fue revocada.

### Modo consulta

Con la prueba vencida (o la clave revocada) se puede entrar en **modo consulta**:

- **Se puede:** iniciar sesión, ver el historial y los reportes, reimprimir, buscar, editar y anular boletas, administrar catálogos y usuarios, y hacer o restaurar copias de seguridad.
- **No se puede:** **Registrar pesaje** (menú, tarjeta de inicio y `F2` quedan deshabilitados) ni **Importar datos legados**. Al pasar el mouse se ve «Modo consulta: active el sistema en Ayuda › Activar licencia.». Si la prueba vence con la pantalla de pesaje abierta, al grabar aparece «El sistema está en modo consulta: la prueba terminó o la licencia no está activa. Actívelo en Ayuda › Activar licencia.».

Para salir del modo consulta, active el sistema con **Ayuda › Activar licencia…** (o con un clic en **Modo consulta** de la barra de estado). Después de activar no hace falta reiniciar.

### Mensajes al activar

| Mensaje | Qué significa |
| :--- | :--- |
| «Ingrese la clave de activación.» | El campo está vacío. |
| «La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.» | La clave no tiene 20 caracteres válidos (0–9 y A–F). |
| «Clave de activación inválida.» | La clave no existe o es de otro producto. |
| «Esta clave ya fue utilizada en otro equipo.» | La clave está activada en otra PC. |
| «Esta clave fue revocada.» | La clave se anuló en el servidor. |
| «No se pudo contactar al servidor de licencias. Revise la conexión a internet.» | Sin internet o servidor inaccesible. |

La administración de claves (generar, liberar por cambio de PC, revocar) está en la guía de instalación y soporte, sección 16.
```

**Verificación:** `Select-String -Path docs\manual\manual-usuario.md -Pattern '^## 12\. Licencia'` encuentra 1
línea y está **antes** de `## Anexo A. Teclas rápidas`.

---

## Paso 7 — Manuales para clientes (fuentes Markdown)

Carpeta: `docs/manual/clientes/fuente/`. Antes de editar, lea `FORMATO.md` (sintaxis de recuadros y títulos).
**No agregue imágenes** en este paso.

**7a. Cabeceras.** En `manual-operador.md`, `manual-supervisor.md` y `manual-administrador.md`, dentro de la
cabecera entre `---` del principio:
- reemplace `version: 1.2.1` por `version: 1.3.0`;
- reemplace la línea `fecha: …` por `fecha: <FECHA>`.

**7b. Operador y Supervisor.** En `manual-operador.md` **y también** en `manual-supervisor.md`, inserte el bloque
siguiente **inmediatamente antes** de la línea `# Glosario {glosario}`, con una línea en blanco antes y otra
después:

```markdown
## El sistema dice «Modo consulta» o pide una clave de activación

El sistema necesita una **licencia** para funcionar completo. Si en la barra de estado, abajo, ve **Prueba: N días**, el sistema está en período de prueba y funciona normalmente. Si ve **Modo consulta**, la prueba terminó: puede ver el historial y reimprimir, pero **no puede pesar** (**Registrar pesaje** aparece en gris).

Si al abrir el sistema aparece la ventana **Activar Pesaje Faeneo**, o si ve **Modo consulta**, avise a su administrador del sistema. **No pulse Probar 15 días ni escriba claves** si no se lo indican.

> [!IMPORTANTE]
> Las boletas ya grabadas no se pierden en modo consulta. Cuando el administrador active el sistema, podrá volver a pesar sin cerrar el programa.
```

**7c. Administrador — tabla de menús.** En `manual-administrador.md`, reemplace la fila:

```
| **Ayuda** | **Asistente de puesta en marcha…**, **Acerca de…** | Capítulo 3 y este capítulo |
```

por:

```
| **Ayuda** | **Asistente de puesta en marcha…**, **Activar licencia…** (solo si el sistema no está activado), **Acerca de…** | Capítulo 3, capítulo «Licencia del sistema» y este capítulo |
```

**7d. Administrador — Acerca de.** En `manual-administrador.md`, en el párrafo que empieza con
`Para ver la versión y los datos del sistema, elija **Ayuda › Acerca de…**.`, reemplace
`la carpeta de datos y la carpeta del programa.` por
`la carpeta de datos, la carpeta del programa, el estado de la **Licencia** y el **Código de equipo**.`

**7e. Administrador — primer ingreso.** En `manual-administrador.md`, inserte el bloque siguiente
**inmediatamente después** de la línea `## Primer ingreso: usuario admin` (deje una línea en blanco antes y otra
después del bloque):

```markdown
> [!IMPORTANTE]
> Si el sistema todavía no está activado, **antes** de la ventana de inicio de sesión aparece **Activar Pesaje Faeneo**. Escriba la clave que le entregaron y pulse **Activar**, o pulse **Probar 15 días**. Vea el capítulo «Licencia del sistema».
```

**7f. Administrador — capítulo nuevo.** En `manual-administrador.md`, inserte el bloque siguiente
**inmediatamente antes** de la línea `# Recuperar el acceso del administrador`, con una línea en blanco antes y otra
después:

```markdown
# Licencia del sistema

## Qué es la licencia

El sistema necesita una **clave de activación** para funcionar sin límite de tiempo. La clave tiene esta forma: XXXX-XXXX-XXXX-XXXX-XXXX (cinco grupos de cuatro números o letras). **Cada clave sirve para una sola computadora.**

Si todavía no tiene la clave, puede **probar el sistema durante 15 días**.

## Activar el sistema

La ventana **Activar Pesaje Faeneo** aparece sola al abrir el sistema mientras no esté activado. También se abre desde **Ayuda › Activar licencia…**.

1. Asegúrese de que la computadora tenga **internet**. Solo se necesita esta vez.
2. En **Clave de activación**, escriba la clave tal como se la entregaron. No importa si usa mayúsculas o minúsculas, ni si escribe los guiones.
3. Pulse **Activar**.
4. Si la clave es correcta, la ventana se cierra y el sistema queda activado para siempre en esta computadora.

> [!SI-ALGO-SALE-MAL]
> - **La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.**: revise la clave letra por letra. Solo lleva números del 0 al 9 y letras de la A a la F.
> - **Clave de activación inválida.**: la clave no existe o es de otro programa. Pida la clave correcta a su proveedor.
> - **Esta clave ya fue utilizada en otro equipo.**: la clave ya está en otra computadora. Si cambió de computadora, pida a su proveedor que la libere.
> - **Esta clave fue revocada.**: pida una clave nueva a su proveedor.
> - **No se pudo contactar al servidor de licencias. Revise la conexión a internet.**: conecte la computadora a internet y pulse **Activar** otra vez. Mientras tanto puede usar **Probar 15 días**.

## Probar 15 días

En la ventana **Activar Pesaje Faeneo**, pulse **Probar 15 días**. El sistema funciona completo durante 15 días, y abajo, en la barra de estado, se ve cuántos días quedan (por ejemplo **Prueba: 12 días**). Para activar antes de que termine, haga clic en ese texto o use **Ayuda › Activar licencia…**.

> [!IMPORTANTE]
> No cambie la fecha ni la hora de Windows hacia atrás. Si el sistema detecta que el reloj se atrasó, termina la prueba.

## Modo consulta

Cuando la prueba termina, la ventana **Activar Pesaje Faeneo** ofrece **Continuar en modo consulta**. En modo consulta:

- **Puede** entrar, ver el historial y los reportes, reimprimir, editar y anular boletas, y hacer copias de seguridad. Sus datos no se pierden.
- **No puede** registrar pesajes ni importar datos del sistema anterior: esas opciones aparecen en gris.

Abajo, en la barra de estado, se ve **Modo consulta** en rojo. Para volver a pesar, active el sistema con **Ayuda › Activar licencia…**. No hace falta cerrar el programa.

## Código de equipo

La ventana de activación y **Ayuda › Acerca de…** muestran un **Código de equipo** (por ejemplo A1B2-C3D4). Si llama a su proveedor por la licencia, dígale ese código: así ubica su computadora.

> [!CONSEJO]
> Si cambia de computadora, avise a su proveedor **antes**: liberará la clave para que pueda activarla en la nueva.
```

**Verificación del paso 7:**

```powershell
Select-String -Path docs\manual\clientes\fuente\manual-*.md -Pattern '^version: 1\.3\.0' | Measure-Object | % Count
Select-String -Path docs\manual\clientes\fuente\manual-administrador.md -Pattern '^# Licencia del sistema'
Select-String -Path docs\manual\clientes\fuente\manual-operador.md,docs\manual\clientes\fuente\manual-supervisor.md -Pattern 'Modo consulta» o pide'
```

Resultados esperados: el primer comando cuenta `3` (operador, supervisor y administrador; `manual-ejemplo.md` no se
toca), el segundo encuentra 1 línea y el tercero 2 líneas.

---

## Paso 8 — Armar los manuales (Word y PDF)

**No regenere las capturas** (no ejecute `scripts\capturas-manual.ps1`): esa tarea queda pendiente y se anota en la
bitácora (paso 10).

Ejecute:

```powershell
python docs\manual\clientes\construir-manuales.py
python docs\manual\clientes\herramientas\revisar-paginas.py
```

**Verificación:**
- El primer comando termina sin errores y actualiza `docs/manual/clientes/Manual-Operador.pdf`,
  `Manual-Supervisor.pdf` y `Manual-Administrador.pdf` (y sus `.docx`). Si dice que falta una captura o que hay un
  error de sintaxis, **deténgase** e informe la salida: casi seguro algún bloque del paso 7 quedó mal sangrado o le
  falta una línea en blanco. Corrija **solo** su propio texto y vuelva a ejecutar.
- Si Edge falla al generar el PDF, vuelva a ejecutar el primer comando (el armado ya reintenta hasta 5 veces).
- `revisar-paginas.py` no informa páginas vacías **fuera** de un fin de capítulo.
- Anote el número de páginas de cada PDF (lo muestra la salida o se ve al abrirlo) para la bitácora.

---

## Paso 9 — Checklist UAT y acta

**9a.** Archivo `docs/puesta-en-marcha/checklist-uat.md`. Inserte el bloque siguiente **inmediatamente antes** de la
línea `## Actualización (UAT-ACT)`, con una línea en blanco antes y otra después:

```markdown
## Licencias — F13 (UAT-F13)

Casos de la activación por clave y la prueba de 15 días. Necesitan internet y claves de `pesajefaeneo` generadas para la prueba (guía de instalación y soporte, sección 16). Use un equipo de pruebas: activar consume la clave.

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-F13-01 | F13 | 1. Instale el sistema en un equipo que nunca lo tuvo y ábralo. | Antes del inicio de sesión aparece **Activar Pesaje Faeneo** con **Activar**, **Probar 15 días** y **Salir**, y un **Código de equipo**. **Salir** cierra el sistema. | | |
| UAT-F13-02 | F13 | 1. Abra el sistema y pulse **Probar 15 días**. <br> 2. Ingrese y mire la barra de estado y **Ayuda › Acerca de…**. | Se abre el inicio de sesión. La barra muestra **Prueba: 15 días** en naranja y Acerca de dice "Prueba (15 días restantes)". **Registrar pesaje** funciona. | | |
| UAT-F13-03 | F13 | 1. Abra **Ayuda › Activar licencia…**. <br> 2. Escriba `ABC` y pulse **Activar**. <br> 3. Escriba una clave de **ScaleLogger** y pulse **Activar**. | Paso 2: "La clave debe tener el formato XXXX-XXXX-XXXX-XXXX-XXXX.". Paso 3: "Clave de activación inválida.". El sistema sigue en prueba. | | |
| UAT-F13-04 | F13 | 1. En **Ayuda › Activar licencia…** escriba una clave válida de `pesajefaeneo` y pulse **Activar**. <br> 2. Desconecte internet, cierre y vuelva a abrir el sistema. | La ventana se cierra; desaparece "Prueba" de la barra; Acerca de dice "Activada". Sin internet, el sistema abre directo en el inicio de sesión. | | |
| UAT-F13-05 | F13 | 1. En **otro** equipo, intente activar con la misma clave de UAT-F13-04. | "Esta clave ya fue utilizada en otro equipo.". | | |
| UAT-F13-06 | F13 | 1. En un equipo en prueba, cierre el sistema y adelante la fecha de Windows 16 días. <br> 2. Abra el sistema. <br> 3. Pulse **Continuar en modo consulta** e ingrese. <br> 4. Vuelva la fecha a la correcta. | Paso 2: "La prueba de 15 días terminó…" con **Activar**, **Continuar en modo consulta** y **Salir**. Paso 3: la barra dice **Modo consulta** en rojo; **Registrar pesaje** (menú, tarjeta y `F2`) e **Importar datos legados** están en gris; historial, reportes y backup funcionan. | | |
| UAT-F13-07 | F13 | 1. En un equipo en prueba (con pocos días usados), cierre el sistema y atrase la fecha de Windows 3 días. <br> 2. Abra el sistema. <br> 3. Vuelva la fecha a la correcta. | Paso 2: "Se detectó un cambio en el reloj del sistema…" y solo se puede activar o entrar en modo consulta. | | |
| UAT-F13-08 | F13 | 1. Revoque la clave del equipo de UAT-F13-04 (`revoke_license_key`, guía §16). <br> 2. Con internet, abra el sistema, ingrese y espere unos segundos. | La barra pasa a **Modo consulta** (o a **Prueba: N días** si le quedaban días de prueba) y **Registrar pesaje** queda en gris. | | |
```

**9b.** Archivo `docs/puesta-en-marcha/acta-aceptacion.md`. En la tabla "Resumen de resultados":
- inserte, **inmediatamente después** de la fila `| Correcciones 1.2.1 F12 (UAT-F12) | 6 | | | |`, la fila:
  `| Licencias 1.3.0 F13 (UAT-F13) | 8 | | | |`
- reemplace `| **Total** | **66** | | | |` por `| **Total** | **74** | | | |`.

Si en `acta-aceptacion.md` aparece la versión `1.2.1` como versión a aceptar (por ejemplo en un campo "Versión"),
reemplácela por `1.3.0`. No cambie otras apariciones.

**Verificación:**
`Select-String -Path docs\puesta-en-marcha\checklist-uat.md -Pattern '^\| UAT-F13-0' | Measure-Object | % Count`
da `8`.

---

## Paso 10 — Bitácora y estado en `AGENTS.md`

Archivo: `AGENTS.md` (raíz). Son cinco ediciones (10a a 10e).

**10a. Diagrama.** En el bloque `mermaid` de la sección 1, inserte **inmediatamente después** de la línea
`    F11 --> F12["F12: Correcciones de la revisión de manuales"]` la línea:

```
    F12 --> F13["F13: Licencias y activación"]
```

**10b. Tabla de estado.** Inserte **inmediatamente después** de la fila que empieza con
`| **F12** | **Correcciones de la revisión de manuales (1.2.1)** |` la fila:

```
| **F13** | **Licencias: clave de activación y prueba de 15 días (1.3.0)** | ✅ Completado | 6 tareas (T1–T6) | 6 | <FECHA> |
```

**10c. Registro por fase.** Inserte el bloque siguiente **inmediatamente antes** de la línea `---` que sigue a la
lista de F12 (la lista termina con `- [x] **E6:** prueba de balanza con 0 tramas avisa y orienta`):

```markdown

### [x] Fase F13 — Licencias y activación (COMPLETADA; plan: `docs/plans/F13-licencias-activacion.md`)
- [x] **T1:** migración del servidor `supabase/migrations/20261013000000_licencias_por_producto.sql` (columna `product`, funciones por producto, compatibilidad con ScaleLogger 0.1.0)
- [x] **T2:** `Application/Licencias` (reglas, firma, servicio) y `ExigirOperacion` en pesaje e importación
- [x] **T3:** `Infrastructure/Licencias` (cliente Supabase, almacén en el registro, identificador de equipo, demo de capturas)
- [x] **T4:** `FrmActivacion`, control previo al login, sincronización en segundo plano, barra de estado, menú Ayuda y Acerca de
- [x] **T5:** pruebas (1768 → 1952)
- [x] **T6:** versión 1.3.0, CHANGELOG, README, guías, manuales, checklist UAT y bitácora
```

**10d. Convención.** En la sección 4, inserte el párrafo siguiente **inmediatamente después** del párrafo que
empieza con `- **Tipo de pesaje (F11):**` (deje una línea en blanco antes y otra después):

```markdown
- **Licencias (F13):** servidor Supabase compartido con ScaleLogger (`uxzehuncusnaofmfjydk`), producto `pesajefaeneo`, funciones RPC `activate_product_license`/`register_product_trial`/`check_product_license` (rol `anon`; la URL y la clave anon son constantes de código en `Infrastructure/Licencias/ConstantesLicencia.cs`, nunca en `appsettings.json`). Estado local en `HKCU\Software\Classes\CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}\Cfg` (`d`, `l`, `s`, `a`), firmado con SHA-256 + sal (`FirmaLicencia`) y el hwid (`SHA256("hw|MachineGuid|sal")`). Reglas puras en `ReglasLicencia.Calcular` (15 días, tolerancia de reloj de 1 día, refresco de `l` cada hora). `IServicioLicencia` es singleton y no exige sesión; `ExigirOperacion()` (`LIC_MODO_CONSULTA`) se llama antes de la transacción en `ServicioPesajes.RegistrarAsync` y en `ServicioImportacion` (simular e importar). `Program.Main` muestra `FrmActivacion` tras preparar la base y antes del login (no con `--autoprueba`, `--restablecer-admin` ni el build de capturas) y lanza una sincronización en segundo plano por arranque. Vencida = modo consulta.
```

**10e. Bitácora.** Al **final del archivo**, agregue (con una línea en blanco antes):

```markdown
### Fase F13 — Licencias y activación (1.3.0)
- **Fecha:** <FECHA>
- **Estado:** ✅ Completado (T1–T6). Plan: `docs/plans/F13-licencias-activacion.md`; ejecución de T6: `docs/plans/F13-T6-ejecucion.md`. Rama `feat/f9-empaquetado` (sin commits).
- **Funcionalidad:** activación por clave de un solo equipo y prueba de 15 días, en el mismo servidor y la misma base que ScaleLogger (producto `pesajefaeneo`). La prueba vencida pasa a modo consulta: no se puede pesar ni importar. Decisiones D1–D12 del plan.
- **Servidor:** migración `20261013000000_licencias_por_producto.sql` (columna `product`, PK de `trials` = `(product, hardware_id)`, funciones `*_product_*`, envoltorios compatibles para ScaleLogger 0.1.0, `generate_product_license_keys`/`revoke_license_key`/`release_license_key`). La aplica el usuario desde el SQL Editor.
- **Compilación:** Release 0 warnings / 0 errores; también con `-p:CapturasManual=true`.
- **Pruebas:** 1952 en verde (1768 → 1952, +184).
- **Documentación:** CHANGELOG 1.3.0, README, guía de instalación y soporte (activación en §4, actualización a 1.3.0 en §5, §16 de administración de claves), manual técnico §12, manuales de cliente 1.3.0 (Operador <N> p., Supervisor <N> p., Administrador <N> p.), checklist UAT-F13-01 a 08 (total 74).
- **Pendiente:**
  - Aplicar la migración en Supabase, ejecutar sus verificaciones y probar ScaleLogger 0.1.0.
  - UAT-F13-01 a 08 en una PC limpia.
  - Capturas de la pantalla de activación y del modo consulta para los manuales: el build de capturas usa una licencia de demostración ya activada, así que hay que agregar escenas al guion. `comun-31-acerca-de` no muestra todavía las líneas Licencia y Código de equipo.
  - Abrir los `.docx` en Word.
  - Entregar claves a los clientes que actualicen desde 1.2.x.
- **Riesgos aceptados (D12):** .NET se descompila con facilidad (sal y fórmulas extraíbles); se puede reiniciar la prueba borrando el registro sin conexión hasta la siguiente sincronización; el estado es por usuario de Windows (HKCU).
```

Reemplace cada `<N>` con el número de páginas que anotó en el paso 8.

**Verificación:** `Select-String -Path AGENTS.md -Pattern 'F13'` encuentra al menos 6 líneas (diagrama, tabla,
registro, convención y bitácora).

---

## Paso 11 — Verificación final de archivos

**11a.** Compile y pruebe otra vez (debe dar lo mismo que en el paso 1, ahora con versión 1.3.0):

```powershell
dotnet build src\PesajeFaeneo.sln -c Release -v q
dotnet test tests\PesajeFaeneo.Tests -c Release --no-build
```

Esperado: `0 Warning(s)`, `0 Error(s)` y `Failed: 0, Passed: 1952`.

**11b.** Compruebe la codificación de los archivos editados (sin BOM y con CRLF). Ejecute en PowerShell:

```powershell
$archivos = 'Directory.Build.props','CHANGELOG.md','README.md','AGENTS.md',
  'docs\manual\guia-instalacion-soporte.md','docs\manual\manual-usuario.md',
  'docs\manual\clientes\fuente\manual-operador.md','docs\manual\clientes\fuente\manual-supervisor.md',
  'docs\manual\clientes\fuente\manual-administrador.md',
  'docs\puesta-en-marcha\checklist-uat.md','docs\puesta-en-marcha\acta-aceptacion.md'
foreach ($a in $archivos) {
  $b = [IO.File]::ReadAllBytes((Resolve-Path $a))
  $bom = $b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF
  $t = [Text.Encoding]::UTF8.GetString($b)
  $lfSolos = ([regex]::Matches($t, '(?<!\r)\n')).Count
  "{0,-60} BOM={1} LF_sin_CR={2}" -f $a, $bom, $lfSolos
}
```

Esperado: `BOM=False` y `LF_sin_CR=0` en **todas** las filas. `AGENTS.md` en este repositorio se llama
`Agents.md` en disco (Windows no distingue mayúsculas): si `Resolve-Path` falla, use `Agents.md`.

Si algún archivo tiene BOM o `LF_sin_CR` mayor que 0, corríjalo con este script, **solo para ese archivo**, y vuelva
a verificar:

```powershell
$a = 'RUTA\DEL\ARCHIVO'
$t = [IO.File]::ReadAllText((Resolve-Path $a))
$t = $t -replace "`r`n", "`n" -replace "`n", "`r`n"
[IO.File]::WriteAllText((Resolve-Path $a), $t, (New-Object Text.UTF8Encoding $false))
```

**11c.** Ejecute `git status --short` y `git diff --stat`. Ningún archivo de `src/` ni de `tests/` debe aparecer
como modificado **por usted** en esta ejecución. Compárelo con la salida del paso 1: los archivos de licencias ya
estaban; no debe haber nuevos cambios en `src/` ni en `tests/`.

---

## Paso 12 — Copiar la migración al repositorio de ScaleLogger (COMPUERTA)

**Pregunte al usuario:** «¿Copio la migración `20261013000000_licencias_por_producto.sql` a
`C:\Users\Gary Ronald\Desktop\ScaleLogger\supabase\migrations` para que ese repositorio tenga el historial de la base
compartida? (solo se copia el archivo; no se hace commit)».

- Si responde **sí**:

  ```powershell
  Copy-Item supabase\migrations\20261013000000_licencias_por_producto.sql 'C:\Users\Gary Ronald\Desktop\ScaleLogger\supabase\migrations\'
  ```

  **Verificación:** `Test-Path 'C:\Users\Gary Ronald\Desktop\ScaleLogger\supabase\migrations\20261013000000_licencias_por_producto.sql'`
  responde `True`.
- Si responde **no**, o no responde: no haga nada y siga.

---

## Paso 13 — Informe intermedio

Antes de seguir, informe al usuario:
- los pasos completados y sus verificaciones;
- el número de páginas de cada manual;
- la salida de `git diff --stat`.

---

## Paso 14 — Instalador 1.3.0 (COMPUERTA)

**Pregunte al usuario:** «¿Ya aplicaste la migración en Supabase, corriste sus verificaciones y probaste que la
activación funciona? Solo entonces conviene construir y entregar el instalador 1.3.0. ¿Lo construyo?».

- Si la respuesta **no es un "sí" claro**, **termine aquí**. Informe que el instalador queda pendiente.
- Si responde **sí**:

**14a.** Construya el instalador (tarda unos minutos y necesita internet para descargar los paquetes de ejecución):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\construir-instalador.ps1
```

**Verificación:** termina con código 0, la autoprueba informa los 5 pasos `OK` y existen
`artifacts\instalador\PesajeFaeneo-Setup-1.3.0-x64.exe` y su `.sha256`. Anote el tamaño en MB y el SHA-256 (está en el
archivo `.sha256`). Si falla, **deténgase** e informe la salida. No modifique `scripts\` ni `instalador\`.

**14b.** Prepare la carpeta de entrega:

```powershell
$destino = 'C:\Users\Gary Ronald\Desktop\Instaladores\PesajeFaeneo-1.3.0'
New-Item -ItemType Directory -Force "$destino\Manuales" | Out-Null
Copy-Item artifacts\instalador\PesajeFaeneo-Setup-1.3.0-x64.exe, artifacts\instalador\PesajeFaeneo-Setup-1.3.0-x64.exe.sha256 $destino
Copy-Item docs\manual\clientes\Manual-*.pdf, docs\manual\clientes\Manual-*.docx "$destino\Manuales"
Copy-Item docs\manual\guia-balanza.md "$destino\Manuales\Guia-de-balanza.md"
Copy-Item docs\manual\guia-instalacion-soporte.md "$destino\Manuales\Guia-de-instalacion-y-soporte.md"
Copy-Item 'C:\Users\Gary Ronald\Desktop\Instaladores\PesajeFaeneo-1.2.1\README.md' "$destino\README.md"
```

**14c.** Edite `$destino\README.md` (la copia nueva, **no** la de 1.2.1). **No** haga un "reemplazar todo" de
`1.2.1`: la lista de Novedades debe conservar la viñeta de 1.2.1. Haga solo estas ediciones:
1. Título: `# Sistema de Pesaje para Faeneo — Instalador 1.2.1` → `# Sistema de Pesaje para Faeneo — Instalador 1.3.0`.
2. En la tabla de contenido, en las dos filas de archivos, `PesajeFaeneo-Setup-1.2.1-x64.exe` →
   `PesajeFaeneo-Setup-1.3.0-x64.exe` (la segunda fila termina en `.sha256`). En la primera fila, `56,4 MB` → el
   tamaño nuevo, con coma decimal.
3. En el comando `Get-FileHash` de "Cómo instalar": `.\PesajeFaeneo-Setup-1.2.1-x64.exe` →
   `.\PesajeFaeneo-Setup-1.3.0-x64.exe`.
4. El SHA-256 viejo `1d6ba04a9a82ca7bd9c1a21cc876d0b05b2bcec359e69c43f8c3b99d9c979c13` (aparece **dos** veces: en la
   línea `SHA-256:` y en el comando `Get-FileHash`) → el SHA-256 nuevo, en las dos.
5. El título `## Actualizar desde 1.0.0, 1.1.0 o 1.2.0` → `## Actualizar desde 1.0.0, 1.1.0, 1.2.0 o 1.2.1`.
6. La viñeta `- Desde 1.2.0 no hay cambios en la base de datos: no se crea copia premigracion.` →
   `- Desde 1.2.0 o 1.2.1 no hay cambios en la base de datos: no se crea copia premigracion.`
7. En "Estado de esta versión": `los 1768 tests automáticos` → `los 1952 tests automáticos`.
8. En "Novedades", inserte **inmediatamente antes** de la viñeta que empieza con `- **1.2.1:**` esta viñeta:

   ```markdown
   - **1.3.0:** activación por clave de un solo equipo y prueba de 15 días. Con la prueba vencida, el sistema queda en modo consulta (se ve todo, pero no se puede pesar).
   ```
9. Inserte esta sección **inmediatamente antes** de la línea `## Primer ingreso` (con una línea en blanco antes y
   otra después):

```markdown
## Activación (nuevo en 1.3.0)

- Al abrir el sistema por primera vez aparece **Activar Pesaje Faeneo**: escriba la clave (`XXXX-XXXX-XXXX-XXXX-XXXX`) y pulse **Activar** (necesita internet solo esa vez), o pulse **Probar 15 días**.
- Cada clave sirve para **una sola computadora**. Al actualizar desde 1.2.x también aparece esta pantalla: tenga la clave a mano.
- Con la prueba vencida el sistema entra en **modo consulta**: se ven el historial, los reportes y los backups, pero no se puede pesar.
- Administración de claves (generar, cambio de PC, revocar): `Manuales\Guia-de-instalacion-y-soporte.md`, sección 16.
```

**Verificación:** `Select-String -Path "$destino\README.md" -Pattern '1\.2\.1'` encuentra solo tres líneas: las
de los puntos 5 y 6 (las que dicen "1.2.0 o 1.2.1") y la viñeta `- **1.2.1:**` de Novedades. Además,
`Select-String -Path "$destino\README.md" -Pattern '1d6ba04a'` no encuentra nada.

**14d.** En `AGENTS.md`, dentro de la bitácora de F13 (paso 10e), inserte antes de `- **Pendiente:**` la viñeta:

```markdown
- **Instalador:** `PesajeFaeneo-Setup-1.3.0-x64.exe` (<TAMAÑO> MB, SHA-256 `<SHA256>`), autoprueba 5/5; copiado con manuales y README a `Desktop\Instaladores\PesajeFaeneo-1.3.0`. **No se ejecutó el instalador.**
```

Reemplace `<TAMAÑO>` y `<SHA256>` con los valores anotados en 14a. Vuelva a ejecutar la verificación de codificación
del paso 11b para `AGENTS.md`.

---

## Paso 15 — Informe final

Informe al usuario, en español y en pocas líneas:
1. qué pasos se completaron y cuáles no (y por qué);
2. el resultado de las pruebas (`Passed: 1952`);
3. las páginas de los manuales;
4. el instalador (nombre, tamaño y SHA-256) o que quedó pendiente;
5. los pendientes de la bitácora (migración en Supabase, UAT-F13, capturas de la pantalla de activación, `.docx` en
   Word, claves para clientes que actualizan).

No haga commit. Si el usuario lo pide después, el mensaje sugerido es
`docs(F13): version 1.3.0, documentacion de licencias y checklist UAT`.

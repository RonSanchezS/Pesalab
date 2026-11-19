# F14 — Guía de marca blanca: entregar el sistema a otra empresa

Documento para **agentes de IA** que preparan una entrega del Sistema de Pesaje para Faeneo (v1.3.0) a una empresa nueva, con su nombre, su logo y sus claves de licencia. Léalo completo antes de tocar nada.

---

## 0. Reglas para el agente

1. **No renombre namespaces, proyectos, carpetas `src/PesajeFaeneo.*` ni archivos `.csproj`/`.sln`.** Hay ~2 600 apariciones de `PesajeFaeneo` (incluidas migraciones EF y sus `Designer.cs`); renombrarlas no aporta nada al cliente y rompe migraciones, tests y `InternalsVisibleTo`. El cliente solo ve: títulos de ventana, textos impresos, nombre del instalador, accesos directos, nombre del `.exe` si usted decide cambiarlo (ver 3.4) y la carpeta de datos.
2. **Trabaje siempre en una rama por cliente** (`cliente/<slug>`, p. ej. `cliente/frigorifico-andino`) creada desde la etiqueta de la última versión estable. Nunca en `main`. Las mejoras de producto van a `main` y se fusionan a las ramas de cliente (`git merge main`), por eso los cambios de marca deben ser **pocos y concentrados** (sección 2).
3. **No cambie reglas de negocio, antifraude ni migraciones.** Marca blanca = cadenas, imágenes, identificadores y documentación.
4. **No publique ni envíe nada** (instalador, claves, SQL a Supabase) sin que el usuario lo confirme. Las claves de licencia y la `service_role` de Supabase jamás se escriben en el repo ni en chat.
5. Antes de terminar, el árbol debe cumplir la compuerta de la sección 6.
6. Los `.ps1`, `.iss` y `.txt` de `scripts/` e `instalador/` son **ASCII puro** (sin tildes ni eñes, sin BOM); los `.cs` van en UTF-8 sin BOM y CRLF (`.editorconfig`).

---

## 1. Qué se personaliza sin recompilar y qué sí

| Dato | Dónde vive | ¿Recompila? |
| :--- | :--- | :---: |
| Nombre de la empresa, RUC/NIT, dirección, teléfonos, logo | Base de datos (`ParametrosSistema`) + carpeta `logo/` | **No**: Administración › Parámetros o asistente de puesta en marcha |
| Tipo de pesaje (res completa / media res / ambos) | `ParametrosSistema.ModoDesposte` | No: Administración › Tipo de pesaje |
| Balanza, impresora, backups | BD / `config\*.json` por equipo | No |
| Ganaderos, clientes, fleteros, usuarios | BD | No |
| Nombre del producto en ventanas, "Acerca de", pie de boleta, PDF/Excel | Constantes de código | **Sí** |
| Editor / copyright / metadatos del `.exe` | `Directory.Build.props` | **Sí** |
| Instalador (nombre, `AppId`, carpeta, accesos, icono) | `instalador/PesajeFaeneo.iss` | **Sí** |
| Carpeta de datos, mutex, clave de registro de licencia | Constantes de código | **Sí** |
| Licencias (producto en Supabase, sal) | `ConstantesLicencia.cs` + servidor | **Sí** |
| Manuales y guías | `docs/manual/**` | Se regeneran |

Si el cliente solo necesita **su nombre y logo en las pantallas y boletas, basta configurar los Parámetros** (login, inicio, boleta y reportes ya leen nombre y logo de la base). Haga la marca blanca de compilación (secciones 2–5) solo cuando se le venda como producto propio del cliente o del revendedor.

---

## 2. Paso previo obligatorio: centralizar la marca (una sola vez, en `main`)

Hoy las cadenas están repartidas. Antes del primer cliente, cree **un único punto** para que cada entrega cambie un solo archivo.

1. Crear `src/PesajeFaeneo.Domain/Marca.cs` (Domain no tiene dependencias, la pueden usar todas las capas):

   ```csharp
   namespace PesajeFaeneo.Domain;

   /// <summary>Identidad de marca de esta compilación. Es lo único que cambia entre clientes.</summary>
   public static class Marca
   {
       public const string Producto = "Sistema de Pesaje para Faeneo";
       public const string Editor = "Pesalab";
       public const string CarpetaDatos = "PesajeFaeneo";          // %ProgramData%\<CarpetaDatos>
       public const string MutexId = "PesajeFaeneo";               // Global\<MutexId>
       public const string CreadorDocumentos = "PesajeFaeneo";     // metadato de PDF
   }
   ```
2. Reemplazar las literales por `Marca.*` en estos puntos (lista completa verificada en la versión 1.3.0):

   | Archivo | Qué reemplazar |
   | :--- | :--- |
   | `Infrastructure/Common/InfoVersion.cs:11` | `Producto = Marca.Producto` |
   | `Infrastructure/Common/AppPaths.cs:18` | `"PesajeFaeneo"` → `Marca.CarpetaDatos` |
   | `WinForms/Program.cs:27` y `:91` | `Global\PesajeFaeneo` → `$@"Global\{Marca.MutexId}"` (y `-Capturas`) |
   | `WinForms/Formularios/FrmPrincipal.cs:55,110,114` | título por defecto → `Marca.Producto` |
   | `WinForms/Formularios/Seguridad/FrmLogin.cs:15,46` | idem |
   | `Application/Boletas/Diseno/DisenoBoleta.cs:229` | pie "Sistema de Pesaje para Faeneo" → `Marca.Producto` |
   | `Infrastructure/Reportes/DisenoPdfReporte.cs:193` | `Creator = Marca.CreadorDocumentos` |
   | `Infrastructure/Instalacion/AutopruebaInstalacion.cs` | revisar cualquier ruta/nombre fijo |
   | `Infrastructure/Licencias/ConstantesLicencia.cs` | ver sección 5 |

   Busque lo que falte con:

   ```bash
   grep -rnE "Sistema de Pesaje|Pesalab|\"PesajeFaeneo\"|Global\\\\PesajeFaeneo" src tests scripts instalador build.ps1 --include=*.cs --include=*.ps1 --include=*.iss --include=*.csproj --include=*.json | grep -v "/obj/\|/bin/\|Migraciones"
   ```
   (Ignore `namespace`/`using PesajeFaeneo.*`: eso no se toca.)
3. Revisar los tests que fijan esas cadenas (`grep -rn "Sistema de Pesaje para Faeneo" tests`) y hacerlos leer `Marca.*`. Debe seguir en verde la suite completa (1 952 tests).
4. `Directory.Build.props`: dejar `Product`, `Company`, `Copyright` en una sola sección comentada `<!-- MARCA -->`.
5. `instalador/PesajeFaeneo.iss`: ya concentra todo en las líneas `#define NombreApp / Editor / Exe / CarpetaDatos`; mantenga ese bloque arriba y comentado.
6. Commit en `main`: `refactor(F14): centralizar la marca en Marca.cs`. A partir de aquí, cada cliente edita **solo** `Marca.cs`, `Directory.Build.props`, el bloque de `#define` del `.iss`, `ConstantesLicencia.cs` y los recursos de la sección 3.

---

## 3. Entrega por cliente — checklist paso a paso

### 3.1 Datos que debe pedir al usuario (no invente ninguno)

| Dato | Ejemplo | Uso |
| :--- | :--- | :--- |
| Nombre comercial del **producto** | "PesaCarne Andino" (o se mantiene el genérico) | `Marca.Producto`, instalador, ventanas |
| Nombre del **editor** / revendedor | "Frigorífico Andino S.A." | `Marca.Editor`, copyright, instalador |
| Slug (minúsculas, sin espacios) | `andino` | rama, producto de licencia, nombres de archivo |
| Logo (PNG/JPG/BMP) | `logo-andino.png` ≤ 1 MB | datos del cliente y, opcional, icono |
| Nombre legal, NIT/RUC, dirección, teléfonos | | Parámetros del sistema (cliente lo carga) |
| Formato de boleta (media carta / ticket 80 mm) y tipo de pesaje | | configuración inicial |
| ¿Licencia propia o gestionada por usted? ¿Prueba de 15 días? | | sección 5 |

### 3.2 Rama y versión

```bash
git checkout -b cliente/<slug> v1.3.0     # o la última etiqueta estable
```
Edite `Directory.Build.props`: `Version` (p. ej. `1.3.0` → mantenga la numeración del producto; el cliente no necesita una distinta), `Product`, `Company`, `Copyright`.

### 3.3 Logo

* **Logo del cliente en las pantallas, boleta y reportes**: no se compila. Se carga en *Administración › Parámetros del sistema › Logo* (PNG/JPEG/BMP con firma válida, ≤ 1 MB; se guarda en `%ProgramData%\<CarpetaDatos>\logo`). Deje el logo listo en la carpeta de entrega y explique el paso en la guía de puesta en marcha del cliente. Un PNG recortado/corrupto se rechaza o se ignora con aviso: valídelo abriéndolo antes.
* **Icono del programa / instalador (opcional)**: hoy el `.exe` y el instalador usan el icono por defecto. Para ponerlo: agregar `<ApplicationIcon>Recursos\app.ico</ApplicationIcon>` en `PesajeFaeneo.WinForms.csproj`, y en el `.iss` `SetupIconFile=..\src\PesajeFaeneo.WinForms\Recursos\app.ico` y `UninstallDisplayIcon={app}\{#Exe}`. El `.ico` debe traer 16/32/48/256 px. Ruta del `.ico` en el `.iss` en ASCII.
* **Datos de demostración**: `docs/manual/clientes/capturas/logo-ejemplo.png` es solo para los manuales; no lo mande al cliente.

### 3.4 Nombre del ejecutable (decisión)

El `.exe` se llama `PesajeFaeneo.WinForms.exe` y el `.iss`/`publicar.ps1` lo referencian. **Recomendación: no lo cambie** (el cliente ve el nombre del acceso directo, no el del `.exe`). Si el usuario lo exige, cambie `<AssemblyName>` en `PesajeFaeneo.WinForms.csproj` y actualice a la vez: `#define Exe` del `.iss`, `scripts/publicar.ps1`, `scripts/construir-instalador.ps1`, `scripts/capturas-manual.ps1` (rutas al `.exe`) y las pruebas que lo busquen; luego verifique la autoprueba.

### 3.5 Identificadores únicos por cliente (obligatorio generarlos nuevos)

Genere GUID nuevos (`[guid]::NewGuid()`) y **no reutilice los de Pesalab**; si dos productos comparten uno se pisan al instalar o comparten licencia:

| Identificador | Dónde | Valor actual (NO reutilizar) |
| :--- | :--- | :--- |
| `AppId` del instalador | `instalador/*.iss` línea `AppId={{...}` | `{0157C544-528F-4307-A487-5208BF10696F}` |
| Clave de registro de licencia | `ConstantesLicencia.SubclaveRegistro` | `...CLSID\{D1F44BC1-BA04-4C5E-8B08-3BDA0DE12FBB}\Cfg` |
| `AppMutex` / mutex en código | `.iss` y `Marca.MutexId` | `Global\PesajeFaeneo` |
| Carpeta de datos | `Marca.CarpetaDatos` y `#define CarpetaDatos` del `.iss` | `PesajeFaeneo` |

El `AppId` queda **fijo para siempre** una vez entregado (si cambia, las actualizaciones se instalan al lado). Anótelo en la bitácora del cliente.
Los tres valores de `.iss` y `Marca.cs` deben coincidir (`AppMutex=Global\<MutexId>`; la carpeta de datos del `.iss` = `{commonappdata}\<CarpetaDatos>`). Si desalinea el mutex, el instalador no detecta el programa abierto.

### 3.6 Instalador

En `instalador/PesajeFaeneo.iss` cambie (ASCII puro, sin tildes):

```
#define NombreApp "<Producto sin tildes>"
#define Editor "<Editor sin tildes>"
#define CarpetaDatos "{commonappdata}\<CarpetaDatos>"
AppId={{<NUEVO-GUID>}
DefaultDirName={autopf}\<CarpetaDatos>
OutputBaseFilename=<Slug>-Setup-{#VersionApp}-x64
```
Revise además textos de `[Messages]`/`[CustomMessages]`, accesos directos (`[Icons]`), `LicenseFile`/`InfoBeforeFile` y `instalador/LICENCIAS-TERCEROS.txt` (conserve las licencias de terceros; **QuestPDF Community**: confirme que el cliente/revendedor cumple las condiciones de ingresos de esa licencia, pendiente P10 abierto en Agents.md).

### 3.7 Licencias (ver sección 5)

### 3.8 Documentación del cliente

Los manuales traen "Pesalab"/"Sistema de Pesaje para Faeneo" y capturas con la empresa ficticia «FRIGORÍFICO EJEMPLO S.R.L.». Para la entrega:

1. Buscar y reemplazar el nombre de producto/editor en `docs/manual/clientes/fuente/*.md`, `docs/manual/*.md`, `README.md` y `docs/puesta-en-marcha/*` (`grep -rn "Pesalab\|Sistema de Pesaje para Faeneo" docs README.md CHANGELOG.md`).
2. Regenerar: `python docs\manual\clientes\construir-manuales.py` (ver `docs/manual/clientes/README.md`). Para que las capturas muestren **el nombre del producto del cliente**, recompile el build de capturas **después** de aplicar `Marca.cs`: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\capturas-manual.ps1` (usa datos de demostración, nunca `ProgramData`; no toque mouse ni teclado mientras corre). La empresa ficticia de las capturas puede dejarse: el cliente ve su propia empresa tras configurar Parámetros.
3. No incluya `docs/plans/**`, `Agents.md`, `supabase/**` ni el historial de fases en lo que se entrega al cliente.
4. Conserve los `.docx`/`.pdf` en `entrega/<slug>/`. Los `.docx` no se han abierto nunca en Word: ábralos una vez (el usuario) y acepte "actualizar campos".

---

## 4. Compilar y empaquetar

Orden exacto (desde la rama del cliente):

```powershell
dotnet build src\PesajeFaeneo.sln -c Release          # 0 warnings, 0 errores
dotnet test  src\PesajeFaeneo.sln -c Release           # 100 % verde
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publicar.ps1            # self-contained win-x64 + autoprueba 5/5
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\construir-instalador.ps1 -SinPublicar   # requiere Inno Setup 6.3+
```
Resultado esperado: `<Slug>-Setup-<version>-x64.exe`. Registre su **SHA-256** (`Get-FileHash`). El instalador **no está firmado** (SmartScreen advertirá): informe al usuario. Entregue en una carpeta `entrega/<slug>/` con el instalador, manuales PDF/DOCX, la guía de instalación y las instrucciones de logo/primer arranque. No ejecute el instalador en la PC de desarrollo sin que el usuario lo pida: antes de dar la entrega por buena debe probarse en una **VM o PC limpia** (UAT-INS-*, UAT-PRI-*, UAT-F13-*).

---

## 5. Licencias por cliente (decisión que debe tomar el usuario)

Hoy: servidor Supabase compartido con ScaleLogger (`uxzehuncusnaofmfjydk`), producto `pesajefaeneo`, URL y clave `anon` fijas en `ConstantesLicencia.cs`, y estado local firmado con una sal. Opciones:

| Opción | Cambios | Cuándo |
| :--- | :--- | :--- |
| **A. Mismo producto `pesajefaeneo` para todos** | Ninguno. Genera claves con `generate_product_license_keys` y las entrega por cliente. | Revendedor único que lleva el control. **Recomendada** salvo que se exija aislamiento. |
| **B. Producto por cliente** (`pesajefaeneo-<slug>`) | `ConstantesLicencia.Producto`; agregar el producto al servidor (revisar la migración `20261013000000_licencias_por_producto.sql`: las funciones aceptan `product` como texto). Cambiar la `Sal`/`SubclaveRegistro` también. | Clientes que no deben compartir pool de claves ni poder activarse con claves de otro. |
| **C. Servidor propio del cliente/revendedor** | Nuevo proyecto Supabase, aplicar la migración, cambiar `UrlServidor` y `ClaveAnon` | El cliente exige independencia. |
| **D. Sin licencia** | Quitar `FrmActivacion` del arranque (`Program.Main`) y el `ExigirOperacion()` de `ServicioPesajes`/`ServicioImportacion`; ajustar tests | Solo si el usuario lo pide expresamente: elimina la protección comercial. |

Reglas:
* **Nunca** ponga la `service_role` ni claves reales de clientes en el repo. La `anon` es pública por diseño; igualmente no la comparta fuera del producto.
* Con B o C cambie también `FirmaLicencia.Sal` y la subclave de registro: así una instalación de otro cliente no se valida contra la suya.
* Aplicar migraciones o generar claves en Supabase **lo hace el usuario** desde el SQL Editor; usted solo le prepara el SQL y le indica cómo verificar.
* Al actualizar un cliente existente desde una versión sin licencias, entréguele su clave junto con el instalador.

---

## 6. Compuerta final (el agente debe comprobar y reportar cada punto)

- [ ] `grep` del paso 2 no encuentra el nombre/editor viejo en código, scripts, instalador, README, CHANGELOG ni docs de entrega (salvo `namespace`/`using`, migraciones y créditos de terceros).
- [ ] `AppId`, subclave de registro, mutex y carpeta de datos son nuevos y coherentes entre `Marca.cs` y `.iss`.
- [ ] Build Release 0 warnings / 0 errores; tests en verde; `has-pending-model-changes` limpio (no debe haber migraciones nuevas por marca).
- [ ] `publicar.ps1`: autoprueba 5/5 OK (BaseDeDatos, Pdf, Excel, Backup, Escritura).
- [ ] Instalador generado, SHA-256 registrado, nombre `<Slug>-Setup-<ver>-x64.exe`.
- [ ] Ventana de login, principal, "Ayuda › Acerca de", boleta de muestra (media carta y ticket) y un PDF/Excel de reportes muestran el producto/editor correctos (render a PNG de la boleta de muestra o capturas; no entregue sin mirarlo).
- [ ] Logo del cliente probado: carga en Parámetros y aparece en login, boleta y PDF.
- [ ] Manuales regenerados sin el nombre anterior; `revisar-paginas.py` sin páginas vacías fuera de fin de capítulo.
- [ ] Sin secretos ni datos de otros clientes en `entrega/<slug>/`.
- [ ] Entrada nueva en la bitácora (`Agents.md` de la rama del cliente) con: fecha, slug, versión base, decisiones (opción de licencia, exe renombrado o no), SHA-256, y pendientes (prueba en PC limpia, Word, claves).

---

## 7. Mantenimiento: actualizar a un cliente ya entregado

1. `git checkout cliente/<slug> && git merge main`. Los conflictos esperables son solo `Marca.cs`, `Directory.Build.props`, el bloque `#define` del `.iss` y `ConstantesLicencia.cs`: **conserve siempre la versión del cliente**.
2. Suba `Version` (la misma numeración del producto), actualice CHANGELOG del cliente, repita secciones 4 y 6.
3. **Nunca** cambie `AppId`, `CarpetaDatos`, `MutexId`, `SubclaveRegistro` ni la `Sal` de un cliente ya instalado: se perdería la base de datos visible, la licencia o la actualización in situ.
4. Las migraciones de BD ya están automatizadas con resguardo previo (`PreMigracion`); no las toque por cliente.

---

## 8. Riesgos y pendientes conocidos

* El producto base se descompila con facilidad (.NET): las cadenas de marca y la sal de licencia son extraíbles. No es un impedimento de reventa, solo una limitación de la protección.
* QuestPDF Community: condiciones de uso por confirmar con cada cliente/revendedor.
* Instalador sin firma de código; sugiera un certificado si el cliente final lo exige.
* La boleta impresa con el pie "Sistema de Pesaje para Faeneo" cambia con `Marca.Producto`; si el cliente quiere **quitar** el pie, es un cambio de `DisenoBoleta` y de sus tests de diseño (`DisenoBoletaTests`): propóngalo, no lo haga sin pedirlo.
* Un cliente con idioma/país distinto (cultura `es-BO`, moneda, formatos de fecha, etiquetas "NIT") requiere otro trabajo (localización) que esta guía **no** cubre.

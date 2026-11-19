# Manual de usuario — Capataz

> **Referencia técnica para soporte.** Los manuales que se entregan a los clientes (uno por rol, en Word y PDF, con capturas) están en `docs/manual/clientes/`. Este documento se conserva como referencia completa para el equipo de soporte.

## 1. Introducción

Este manual corresponde a la versión **1.2.1** del Capataz. La versión instalada se ve en la barra inferior de la ventana principal y en **Ayuda › Acerca de…**.

El sistema sirve para:

- Pesar reses (res completa o media res, según el tipo de pesaje que configure la empresa) con la balanza conectada a la computadora.
- Grabar cada pesaje como una **boleta** con número correlativo, sin huecos ni repeticiones.
- Imprimir y reimprimir boletas.
- Buscar, corregir datos descriptivos y anular boletas.
- Emitir reportes en pantalla, PDF y Excel.
- Mantener los catálogos de ganaderos, clientes y fleteros.
- Guardar copias de seguridad de la información.

### Los tres roles

Cada persona entra con su propio usuario. Lo que ve y lo que puede hacer depende de su rol:

| Rol | Para qué sirve |
| :--- | :--- |
| **Operador** | Pesa, imprime y reimprime boletas, y da de alta ganaderos y clientes nuevos desde la pantalla de pesaje. |
| **Supervisor** | Todo lo del Operador, más: reportes, buscar/editar/anular boletas, catálogos y visor de auditoría. |
| **Administrador** | Todo lo del Supervisor, más: usuarios, parámetros de la empresa, tipo de pesaje, balanza, impresión, copias de seguridad, importación de datos, visor de logs y asistente de puesta en marcha. |

El detalle completo está en el **Anexo B**. Las opciones de menú a las que su rol no tiene acceso no aparecen en pantalla.

> Nota: las guías para el técnico (instalación, balanza) están en otros documentos: `guia-instalacion-soporte.md` y `guia-balanza.md`.

## 2. Inicio de sesión y contraseña

### Entrar al sistema

1. Abra **Capataz** desde el menú Inicio o desde el escritorio.
2. Escriba su **Usuario** y su **Contraseña**.
3. Pulse **Ingresar**.

> [Captura: pantalla "Iniciar sesión" con el logo y el nombre de la empresa arriba y los campos Usuario y Contraseña]

La ventana se titula "Capataz — Iniciar sesión" y muestra arriba el **logo y el nombre de la empresa** (los que se configuran en Parámetros del sistema). Si todavía no se configuró el nombre (sigue en "NOMBRE DE EMPRESA") o falla la lectura, muestra "Capataz"; en ningún caso impide iniciar sesión. También muestra la versión instalada (por ejemplo `v1.2.1`). **Salir** cierra el sistema sin entrar.

Si el aviso **"Bloq Mayús está activado"** aparece, las letras se escriben en mayúsculas. Las contraseñas distinguen mayúsculas de minúsculas.

Si el usuario o la contraseña no coinciden, el sistema responde **"Usuario o contraseña incorrectos."** (siempre el mismo mensaje, a propósito). Si la cuenta fue desactivada, responde "El usuario está desactivado. Contacte al administrador."

### Cuenta bloqueada

Después de **5 intentos fallidos** seguidos, la cuenta se bloquea durante **5 minutos**. El sistema muestra: "Usuario bloqueado temporalmente por intentos fallidos. Intente de nuevo en N minuto(s)." Espere ese tiempo o pida al Administrador que restablezca su contraseña (eso también desbloquea la cuenta).

### Primer ingreso y cambio obligatorio de contraseña

- En una instalación nueva, el usuario inicial es `admin` y la contraseña inicial es `admin`.
- Las cuentas nuevas y las cuentas cuya contraseña fue restablecida **deben cambiar la contraseña al ingresar**. El sistema muestra "Debe cambiar su contraseña para continuar".

Pasos:

1. Escriba la **Contraseña actual**.
2. Escriba la **Nueva contraseña** y repítala en **Confirmación**.
3. Pulse **Aceptar**.

Reglas de la contraseña nueva:

- Entre 6 y 100 caracteres.
- No puede ser igual a la contraseña anterior.
- No puede ser igual al nombre de usuario.

### Cambiar la contraseña cuando quiera

Menú **Sesión › Cambiar contraseña…** y siga los mismos pasos.

### Cerrar sesión o salir

- **Sesión › Cerrar sesión** (atajo `Ctrl+Shift+L`): pregunta "¿Está seguro de que desea cerrar la sesión?" y vuelve a la pantalla de inicio de sesión para que entre otra persona.
- **Sesión › Salir**: cierra el sistema.

Cierre el sistema con **Salir** antes de apagar la computadora. Así, si el Administrador activó la copia de seguridad al cerrar, esta se guarda.

## 3. Pantalla principal

La pantalla principal tiene una barra de menús arriba, la **pantalla de inicio** en el centro y una barra de estado abajo.

> [Captura: pantalla principal completa con la barra de menús, las tres tarjetas y la barra de estado]

### Pantalla de inicio

- **Encabezado:** el logo, el nombre de la empresa y, debajo, la dirección y la ciudad (los de Parámetros del sistema). Mientras la empresa no se configure muestra el valor inicial "NOMBRE DE EMPRESA" (a diferencia de la pantalla de inicio de sesión, que en ese caso muestra "Capataz").
- **"¿Qué desea hacer?"** y tres **tarjetas** con las tareas del día a día:

| Tarjeta | Qué abre | Tecla |
| :--- | :--- | :--- |
| **Control de peso** | Registrar pesaje (sección 4) | `F2` |
| **Imprimir ticket** | Reimprimir boleta (sección 5) | `F3` |
| **Historial de pesajes** | Historial de pesajes (sección 5) | `F4` |

- Al abrir el sistema, la tarjeta **Control de peso** ya tiene el foco: `Enter` abre el pesaje.
- Si el rol no tiene permiso para una tarjeta, esta no desaparece: queda gris con el texto "Su rol no tiene permiso para esta acción." (hoy los tres roles tienen permiso para las tres).
- Debajo, una línea de atajos que muestra solo las teclas que el rol puede usar: el Operador ve "F2 Control de peso · F3 Imprimir ticket · F4 Historial"; el Supervisor y el Administrador ven además "F6 Buscar boletas". Si un rol no pudiera usar ninguna, la línea no se muestra.

### Menús

| Menú | Opciones | Quién las ve |
| :--- | :--- | :--- |
| **Pesaje** | Registrar pesaje (`F2`) | Todos |
| | Historial de pesajes (`F4`) | Todos |
| **Boletas** | Reimprimir boleta (`F3`) | Todos |
| | Buscar / editar boleta (`F6`) | Supervisor, Administrador |
| | Anular boleta | Supervisor, Administrador |
| **Catálogos** | Ganaderos, Clientes, Fleteros | Supervisor, Administrador |
| **Reportes** | Reportes de pesaje, Pesajes por raza, Boletas anuladas | Supervisor, Administrador |
| **Administración** | Usuarios, Parámetros del sistema, Tipo de pesaje, Configuración de balanza, Configuración de impresión | Administrador |
| | Visor de auditoría | Supervisor, Administrador |
| **Utilitarios** | Backup / Restaurar, Importar datos legados, Visor de logs | Administrador |
| **Sesión** | Cambiar contraseña…, Cerrar sesión, Salir | Todos |
| **Ayuda** | Asistente de puesta en marcha… | Administrador |
| | Acerca de… | Todos |

Para abrir un menú con el teclado, mantenga `Alt` y pulse la letra subrayada del menú.

### Barra de estado (parte inferior)

De izquierda a derecha muestra:

1. El nombre de la empresa.
2. **Usuario:** su usuario y su nombre completo.
3. **Rol:** su rol.
4. **Balanza:** el estado de la balanza y el peso actual (por ejemplo `Balanza: conectada (COM3 9600 8N1)  125,4 kg`; si no hay lectura, `---- kg`). Aquí y en la pantalla de pesaje el separador decimal es el de la configuración regional de Windows (`125,4` o `125.4`); boletas y reportes usan siempre coma.
5. La fecha y la hora del equipo (`dd/MM/yyyy HH:mm:ss`).
6. La versión del sistema (por ejemplo `v1.3.0`).
7. Solo si el sistema no está activado: **Prueba: N días** (naranja) o **Modo consulta** (rojo). Un clic abre la activación (ver la sección 12).

### Indicador de balanza: estados y colores

| Texto | Color | Qué significa |
| :--- | :--- | :--- |
| Balanza: conectada (…) con peso **estable** | Verde | Todo bien; el peso se puede capturar. |
| Balanza: conectada (…) con peso **inestable** | Negro | Hay conexión, pero el peso todavía se mueve. Espere. |
| Balanza: sin datos | Rojo | La balanza no envía nada. Revise cable, adaptador y que el indicador esté encendido. |
| Balanza: tramas no reconocidas | Rojo | Llegan datos, pero el sistema no los entiende. Avise al técnico (configuración). |
| Balanza: error — … | Rojo | Falla de conexión; el mensaje dice el motivo. |
| Balanza: detenida / conectando… / en diagnóstico | Gris | Estados transitorios. Si "conectando…" no termina, avise al técnico. |

**Modo simulador.** Si la balanza está configurada como simulador, el texto empieza con **`[SIMULADOR]`** y el fondo de ese cuadro se pone **naranja**. Los pesos del simulador **no son reales** (ver sección 4).

## 4. Registrar un pesaje

Abra **Pesaje › Registrar pesaje** o pulse `F2` en la pantalla principal.

> [Captura: pantalla "Registro de pesaje" con el peso en vivo, los datos del animal y, a la derecha, los pesos capturados y los botones]

### Qué muestra la pantalla

- **Arriba:** la **próxima boleta** que se va a emitir ("Próxima boleta: N"), el estado de la balanza y el **peso en vivo** en letra grande, con la palabra **ESTABLE** o **INESTABLE**. Con la balanza en simulador aparece además la franja naranja "MODO SIMULADOR — los pesos no son reales".
- **Línea de guía:** una frase que le dice qué hacer a continuación. El fondo cambia de color: amarillo suave (indicación o aviso), verde (listo para grabar o boleta grabada) o rojo (problema).
- **Izquierda:** los datos del animal (ganadero, secuencia, cliente, dirección, fletero, color/raza, sexo, desposte y observación). Junto a la secuencia, una nota dice cuántos pesajes vigentes tiene hoy ese ganadero ("Hoy: N pesaje(s) de este ganadero").
- **Derecha:** los pesos capturados (izquierdo, derecho y total), los botones de captura y los de grabar e imprimir. Las boletas ya grabadas ya no se listan aquí: están en **Pesaje › Historial de pesajes** (ver más abajo).

### Datos del animal

Se llenan en este orden. La tecla `Enter` pasa al siguiente campo.

| Campo | Qué escribir |
| :--- | :--- |
| **Ganadero** | El código o el nombre. Aparece una lista de coincidencias. |
| **Secuencia** | El número del animal del ganadero en el día (de 1 a 9999). El sistema sugiere el siguiente (empieza en 1 cada día). Puede cambiarlo. |
| **Cliente** | El código o el nombre. |
| **Dirección** | Se completa con la dirección del cliente; puede corregirla. |
| **Fletero** | Opcional. El código o el nombre del fletero que lleva la carne; si no hay, déjelo vacío ("Sin fletero (opcional)"). Hay que **elegirlo de la lista** (o con `Enter` si hay una sola coincidencia). Si queda texto escrito sin elegir un fletero, la guía dice "Elija el fletero de la lista o borre el texto." y **Grabar (F12) queda deshabilitado** hasta que se elija uno o se borre el texto; con el campo vacío se graba sin fletero. La boleta imprime solo el fletero elegido. Se borra después de cada grabado: se elige en cada pesaje. No hay alta rápida de fleteros. |
| **Color/Raza** | Texto libre. |
| **Sexo** | `M - MACHO` o `H - HEMBRA`. |
| **Desposte** | `RES COMPLETA` o `MEDIA RES`. Solo aparece si la empresa pesa ambos tipos (ver "Tipo de pesaje" en Administración); si pesa uno solo, el tipo es fijo y la fila no se muestra. |
| **Observación** | Se completa con la observación por defecto de la empresa; puede cambiarla. |

Todo se guarda en mayúsculas.

### Si el ganadero o el cliente no existe: alta rápida

1. Escriba el nombre en el buscador.
2. Pulse `Enter`. Si no hay coincidencias, se abre el cuadro **Alta rápida de ganadero** (o **de cliente**) con lo escrito ya cargado.
3. Complete lo que falte (ganadero: nombre; cliente: nombre y dirección opcional) y pulse **Guardar**. El **código** no se escribe: dice "(se asigna al guardar)" y lo pone el sistema.
4. El registro nuevo queda seleccionado en la pantalla de pesaje.

El Operador puede hacer altas rápidas. Corregir o desactivar ganaderos y clientes requiere el menú **Catálogos** (Supervisor o Administrador).

### Pesar una res completa

La res completa se pesa en **dos lados**.

1. Elija **Desposte: RES COMPLETA** (si la empresa pesa solo res completa, la opción no aparece: ya está fija).
2. Cuelgue el **lado izquierdo** en la balanza y espere a que el peso diga **ESTABLE**.
3. Pulse `F5` (o el botón **Capturar izq. (F5)**). El peso aparece en **Izquierdo (kg)**.
4. **Retire el lado de la balanza.** El peso debe bajar a cero (2,0 kg o menos). Mientras no baje, la guía dice "Retire el peso de la balanza (esperando retorno a cero)." y no se puede capturar el otro lado. Esto evita capturar dos veces el mismo lado por accidente.
5. Cuelgue el **lado derecho**, espere **ESTABLE** y pulse `F6` (**Capturar der. (F6)**).
6. El sistema calcula el **Total (kg)**. Cuando todo está correcto, la guía dice **"Listo para grabar (F12)."**
7. Pulse `F12` (**Grabar**).

### Pesar una media res

1. Elija **Desposte: MEDIA RES** (si la empresa pesa solo media res, la opción no aparece: ya está fija y la guía dice "Capture un lado (F5 o F6).").
2. Cuelgue la media res, espere **ESTABLE** y capture **un solo lado** con `F5` (izquierdo) o `F6` (derecho).
3. Pulse `F12` para grabar.

### Rangos de peso válidos

| | Mínimo | Máximo |
| :--- | :--- | :--- |
| Cada lado | 10,0 kg | 600,0 kg |
| Total de res completa | 10,0 kg | 1.200,0 kg |
| Total de media res | 10,0 kg | 600,0 kg |

Si el peso estable está fuera de rango, la guía avisa "Peso fuera de rango (10,0 a 600,0 kg)." y no se captura. Con la balanza en reposo (peso estable de hasta ±2,0 kg, por ejemplo vacía), la guía **no** dice "fuera de rango": muestra el mensaje de captura ("Capture el lado izquierdo (F5) y el derecho (F6)." o "Capture un lado (F5 o F6)."). Un peso estable de 2,1 a 9,9 kg sí muestra "Peso fuera de rango". Los pesos se manejan con un decimal (0,1 kg).

Si al grabar aparece "El tipo de pesaje configurado es «…». Cierre y vuelva a abrir la pantalla de pesaje.", el tipo de pesaje de la empresa cambió mientras la pantalla estaba abierta y el desposte elegido ya no está permitido. No se grabó nada ni se gastó número: cierre la pantalla con `Esc`, vuelva a abrirla y pese de nuevo.

### Si se equivoca: borrar el pesaje

Pulse `F8` (**Borrar pesaje (F8)**) para descartar los pesos capturados del pesaje en curso y empezar de nuevo con ese animal. No borra ninguna boleta ya grabada. Los pesos de una boleta **ya grabada** no se pueden modificar; ver sección 6.

### Mensajes de la línea de guía

| Mensaje | Qué hacer |
| :--- | :--- |
| Seleccione el ganadero (código o nombre). | Elija el ganadero. |
| Seleccione el cliente (código o nombre). | Elija el cliente. |
| Elija el fletero de la lista o borre el texto. | Hay un fletero escrito sin elegir: elíjalo de la lista o borre el texto. Mientras tanto, Grabar (F12) está deshabilitado. |
| Esperando peso estable… | Espere a que el peso deje de moverse. |
| Capture el lado izquierdo (F5) y el derecho (F6). | Capture los dos lados. |
| Capture el lado derecho (F6). / Capture el lado izquierdo (F5). | Falta el otro lado. |
| Media res: capture un lado (F5 o F6). / Capture un lado (F5 o F6). | Capture un solo lado (la segunda forma aparece cuando la empresa pesa solo media res). |
| Retire el peso de la balanza (esperando retorno a cero). | Retire el lado anterior para poder capturar el siguiente. |
| Peso fuera de rango (10,0 a 600,0 kg). | Revise el animal o la balanza. |
| Listo para grabar (F12). | Pulse `F12`. |
| Balanza en MODO SIMULADOR: solo un administrador puede capturar y grabar. | Avise al Administrador; ver "Modo simulador" más abajo. |
| Balanza: … No se puede capturar. | La balanza no está conectada; ver sección 11. |
| (motivo) Use F8 para borrar el pesaje. | Los pesos capturados no son coherentes (por ejemplo, dos lados en media res). Pulse `F8` y vuelva a capturar. |

Si hay varias cosas pendientes, la guía muestra primero la más importante. Orden de prioridad: simulador, balanza, ganadero, cliente, fletero sin elegir, "Listo para grabar", pesos incoherentes con los dos lados capturados (aviso de `F8`), retorno a cero, peso inestable ("Esperando peso estable…"), peso fuera de rango y, por último, los mensajes de captura. Por eso, en media res con los dos lados capturados el aviso de `F8` aparece aunque aún se esté esperando el retorno a cero, la estabilidad o el rango.

Los botones **Capturar**, **Borrar pesaje**, **Imprimir último** y **Grabar** se activan solos cuando corresponde. Si un botón está gris, la línea de guía explica por qué. **Grabar (F12)** es verde con letras blancas cuando está habilitado y gris con letras grises cuando no lo está.

### Después de grabar

- La guía muestra en verde **"Boleta N° … grabada — … kg"** (con "(SIMULADO)" si se pesó con el simulador) y se oye un sonido breve.
- Si la impresión automática está activada para este equipo, la boleta se imprime enseguida y la guía agrega "— impresa."; si no está activada, agrega "— pulse F9 para imprimir.". Si la impresión falla, la boleta **ya quedó grabada** y la guía dice en rojo "Boleta N° … grabada, pero NO se imprimió… Pulse F9 para reintentar."
- Los pesos y el **Color/Raza** se borran, la **Observación** vuelve a la de la empresa y el cursor queda en **Color/Raza** para el siguiente animal. El ganadero, el cliente, la dirección y el sexo se conservan; la secuencia sugerida avanza.
- La boleta nueva queda disponible en el **Historial de pesajes** y para imprimirla con `F9`. El fletero se borra (se elige en cada pesaje); el código y el número de boleta los asigna el sistema.

### Salir de la pantalla de pesaje

Pulse `Esc` o el botón **Cerrar (Esc)**. Si hay pesos capturados sin grabar, el sistema avisa antes de cerrar.

### Modo simulador

El simulador es una balanza falsa que sirve solo para **pruebas y capacitación**.

- Solo el **Administrador** puede capturar y grabar pesos con la balanza en modo simulador. Para Operador y Supervisor, los botones de captura y grabar quedan bloqueados y la guía lo explica.
- Al Administrador se le muestra el recuadro **Panel de Simulación de Balanza (Admin)**: escriba un valor en **Peso kg:** y pulse **Cargar** para simular una res colgada, o **Vaciar** para volver a cero.
- Toda boleta grabada con el simulador queda marcada como **SIMULADO** en el historial, en el buscador de boletas, en los reportes y en la auditoría, y su impresión lleva la leyenda **"PESO SIMULADO — SIN VALOR COMERCIAL"**.
- Si en la jornada normal ve `[SIMULADOR]` en la barra de estado, avise al Administrador: la balanza real no está configurada.

## 5. Boletas: impresión y reimpresión

### Imprimir desde la pantalla de pesaje

- **Automática:** si el Administrador activó "Imprimir la boleta automáticamente al grabar" en este equipo, se imprime al pulsar `F12`.
- **Manual:** pulse `F9` (o el botón **Imprimir último (F9)**) para imprimir **el último pesaje grabado en esta pantalla**. Sirve también para reintentar si falló la impresión. Si la boleta ya se había impreso, esta impresión cuenta como reimpresión (lleva la leyenda REIMPRESIÓN). Para imprimir otra boleta use el **Historial de pesajes** o **Reimprimir boleta**.

La configuración (impresora, papel, copias) la hace el Administrador; ver sección 9.

### Qué trae la boleta

Datos de la empresa y logo, número, fecha y hora, ganadero (código y nombre), secuencia, cliente, dirección, color/raza, sexo, desposte, pesos, observación, operador que pesó, teléfono de reclamos y fecha y hora de impresión. Si se eligió un fletero, sale en la zona de datos (nombre y celular). Abajo hay tres firmas: **Jefe de Despacho**, **Fletero** (con su nombre si se eligió uno) y **Médico Veterinario**. Si la empresa pesa un solo tipo (ver "Tipo de pesaje" en la sección 9), la boleta no muestra el desposte, salvo que la boleta sea de otro tipo (anterior al cambio o importada): el sistema oculta el dato cuando sobra, pero nunca muestra un tipo distinto del grabado. En media carta, el lugar del desposte queda en blanco.

### Reimprimir una boleta

Menú **Boletas › Reimprimir boleta** o `F3` desde la pantalla principal.

> [Captura: ventana "Reimprimir boleta" con el resumen de una boleta]

1. El campo **Número de boleta** aparece con la última boleta emitida y su resumen ya cargado (si todavía no hay boletas, dice "Aún no hay boletas emitidas."). Para otra, escriba el número.
2. Pulse `Enter` (o **Buscar (Enter)**). Se muestra el resumen: fecha, ganadero, cliente, desposte (salvo que se oculte por el tipo de pesaje), peso total, estado (VIGENTE o ANULADA con su motivo, y "(SIMULADO)" si corresponde) e impresiones ("Aún no se imprimió" o "Impresiones previas: N").
3. Si quiere ver la boleta antes de imprimir, pulse `F4` (**Vista previa (F4)**).
4. Pulse `F9` (**Imprimir (F9)**).

Importante:

- Si cambia el número, el resumen se borra y `F4`/`F9` se desactivan hasta buscar de nuevo con `Enter`. Así nunca se imprime una boleta distinta de la que ve en pantalla.
- Cada impresión queda registrada en la auditoría.
- Toda reimpresión sale con la leyenda **"REIMPRESIÓN n — fecha y hora"**.
- La reimpresión de una boleta anulada sale con la leyenda **ANULADA** y el motivo.

### Leyendas que puede ver en una boleta

| Leyenda | Cuándo aparece |
| :--- | :--- |
| ORIGINAL / COPIA 1 / COPIA 2 | Cuando se configuran 2 o 3 copias; cada copia sale como página independiente. |
| REIMPRESIÓN n — fecha y hora | En toda impresión posterior a la primera. |
| ANULADA: motivo | En boletas anuladas. El motivo se recorta a unos 30 caracteres. |
| PESO SIMULADO — SIN VALOR COMERCIAL | En boletas pesadas con el simulador. |
| IMPORTADA DEL SISTEMA ANTERIOR | En boletas traídas del sistema anterior. |
| MUESTRA — SIN VALOR | En las impresiones de prueba de la configuración de impresión. |

Las leyendas no se excluyen: una boleta anulada y pesada con el simulador lleva las dos. Además, una marca destacada dice MUESTRA, ANULADA o SIMULADO (solo una, en ese orden de prioridad): en media carta es una marca de agua gris; en el ticket, una línea `*** … ***` debajo del total.

### Historial de pesajes

Menú **Pesaje › Historial de pesajes** o `F4` desde la pantalla principal (todos los roles). Antes era una grilla dentro del registro de pesaje; ahora tiene su propio apartado.

> [Captura: ventana "Historial de pesajes" con el día elegido y la lista]

1. Elija el **Día** (por defecto, hoy) y pulse **Actualizar (F5)**; también se actualiza al cambiar el día. **Hoy** vuelve al día actual.
2. La lista muestra los pesajes del día, el más reciente primero: boleta, hora, código y nombre del ganadero, secuencia, cliente, **fletero**, desposte, pesos, origen (SIMULADO o IMPORTADA; vacío si vino de la balanza) y estado (VIGENTE o ANULADA). Las anuladas se ven en gris. La columna **Desposte** se oculta si la empresa pesa un solo tipo y todos los pesajes del día son de ese tipo. Se muestran hasta 500 pesajes por día ("Se muestran los últimos 500 pesajes del día.").
3. Para reimprimir una boleta, selecciónela y pulse `F9` (**Reimprimir (F9)**). Usa el mismo flujo que **Reimprimir boleta**: queda registrada en la auditoría y sale con la leyenda de reimpresión.

`Esc` cierra la ventana.

## 6. Buscar, editar y anular boletas

Esta sección es para **Supervisor** y **Administrador**.

Abra **Boletas › Buscar / editar boleta** (`F6`). Para ir directo a anular, use **Boletas › Anular boleta**: abre el mismo buscador.

> [Captura: ventana "Buscar / editar boletas" con filtros y resultados]

### Buscar

Los filtros se combinan (deben cumplirse todos):

| Filtro | Nota |
| :--- | :--- |
| **Desde / Hasta** | Rango de fechas. |
| **N° boleta** | Si lo escribe, **se ignoran las fechas**. |
| **Estado** | Todos, Vigente o Anulado. |
| **Ganadero / Cliente** | Elija del buscador (vacío = todos). |

Pulse `Enter` o **Buscar (Enter)**. **Limpiar** borra los filtros.

- Se muestran como máximo **200 boletas**, la más reciente primero. Si hay más, el aviso naranja dice "Se muestran las primeras 200 boletas; refine la búsqueda."
- Las boletas anuladas se ven en gris. La columna de origen dice SIMULADO o IMPORTADA cuando corresponde.
- La columna del desposte se oculta si la empresa pesa un solo tipo y todas las boletas encontradas son de ese tipo.
- **Editar (F4)** y **Anular (F8)** se activan solo con una boleta **vigente** seleccionada.

### Editar una boleta

1. Seleccione la boleta y pulse `F4` (o **Editar (F4)**). Con el cursor en la grilla, `Enter` también abre la edición.
2. Cambie lo necesario. Con `Tab` pasa de un campo al otro (`Enter` **no** guarda).
3. Opcionalmente escriba el **Motivo del cambio** (hasta 100 caracteres; queda en la auditoría).
4. Pulse `F12` (**Guardar (F12)**). `Esc` cancela.

Se pueden cambiar solo datos descriptivos: **ganadero, secuencia, cliente, dirección, fletero, color/raza, sexo y observación**.

**No se pueden cambiar** los pesos, el tipo de desposte, el número ni la fecha. Si un peso está mal, hay que **anular la boleta y registrar un pesaje nuevo**.

Otras reglas:

- Si pulsa Guardar sin haber cambiado nada, el sistema dice "No se modificó ningún dato." y no escribe nada.
- Cambiar el cliente **no** cambia la dirección de la boleta: si corresponde, corríjala a mano.
- Si en **Fletero** queda texto escrito sin elegir de la lista, al pulsar Guardar (`F12`) el campo se marca con "Elija el fletero de la lista o borre el texto." y no se guarda nada. Con el campo vacío la boleta queda sin fletero.
- Si la boleta cambió en otra ventana mientras la editaba, el sistema avisa "La boleta fue modificada por otro usuario o en otra ventana. Ciérrela y vuelva a abrirla.", cierra la edición sin guardar y recarga la búsqueda; ábrala de nuevo para ver los datos actuales.
- Una boleta **anulada** no se puede editar.

### Anular una boleta

1. Seleccione la boleta y pulse `F8` (o **Anular (F8)**).
2. Escriba el **motivo** (obligatorio, de 5 a 100 caracteres). Escriba primero lo importante: en el papel solo salen unos 30 caracteres.
3. Pulse **Anular boleta**. El motivo se revisa **antes** de preguntar: si es demasiado corto o está vacío, aparece el aviso junto al campo y no se pide confirmación. Si es válido, confirme ("Se anulará la boleta N° …. ¿Desea continuar?"; la respuesta por defecto es No).

> Atención: la anulación **no se puede deshacer**. La boleta conserva su número, no vuelve a estar vigente y su número **no se reutiliza**. Reimprimirla muestra la leyenda ANULADA y el motivo.

Las boletas anuladas no cuentan en los reportes de pesaje; tienen su propio reporte (sección 7).

## 7. Reportes

Esta sección es para **Supervisor** y **Administrador**.

Menú **Reportes › Reportes de pesaje** (abre en "Pesajes del día"), **Reportes › Pesajes por raza** o **Reportes › Boletas anuladas**.

> [Captura: ventana "Reportes" con filtros, totales y la grilla]

### Tipos de reporte

| Tipo | Contenido |
| :--- | :--- |
| Pesajes del día | Boletas vigentes de un día. |
| Pesajes entre fechas | Boletas vigentes de un rango de fechas. |
| Pesajes por ganadero | Boletas vigentes de un ganadero en un rango de fechas. |
| Pesajes por cliente | Boletas vigentes de un cliente en un rango de fechas. |
| Pesajes por raza | Boletas vigentes de una raza (Color/Raza) en un rango de fechas. La lista de razas sale de los pesajes ya registrados; se elige una. La comparación no distingue mayúsculas pero sí acentos (por ejemplo, "CEBÚ" y "CEBU" son razas distintas). |
| Boletas anuladas | Boletas anuladas, con fecha de anulación ("Anulada el"), usuario ("Anulada por") y motivo. El rango de fechas se aplica a la **fecha de emisión** de la boleta, no a la de anulación. No lleva columna de fletero. |

### Cómo generar un reporte

1. Elija el **Tipo de reporte**.
2. Elija **Desde** y **Hasta** (en "Pesajes del día" solo cuenta la fecha Desde). Si es por ganadero, cliente o raza, elija cuál.
3. Pulse **Generar (Enter)**.
4. Si cambia cualquier filtro después de generar, el reporte se limpia y aparece "Los filtros cambiaron: presione Generar para actualizar el reporte." Genere de nuevo.

### Cómo se cuentan las reses

- Una **res completa** cuenta como **1**.
- Una **media res** cuenta como **0,5**.
- La línea de totales dice "Total Peso Neto: … Kg.", "Cantidad Reses: …" y "Boletas: N (res completa: X · media res: Y)". En el reporte de anuladas: "Boletas anuladas: N" y "Peso neto anulado: … Kg.".
- Si la empresa pesa un solo tipo (ver "Tipo de pesaje") y todas las boletas del reporte son de ese tipo, el reporte no muestra la columna "Tipo Corte" ni el desglose "res completa / media res"; la línea de totales dice "Boletas: N". Si hay alguna boleta de otro tipo (anterior al cambio o importada), se muestran.
- Los pesos se muestran con un decimal y coma (por ejemplo `1.234,5`).
- Las boletas con peso **simulado** aparecen marcadas con `*` y con una nota al pie: "(*) n boleta(s) con peso SIMULADO — sin valor comercial."

### Salidas

| Botón | Qué hace |
| :--- | :--- |
| **Vista previa** | Muestra las páginas en pantalla. |
| **Imprimir…** | Elige impresora e imprime. |
| **Exportar PDF…** | Guarda un PDF; luego pregunta "¿Desea abrirlo ahora?". |
| **Exportar Excel…** | Guarda un archivo Excel con números y fechas reales; también pregunta si desea abrirlo. |

El archivo siempre corresponde a lo que se ve en pantalla.

### Límites

- Un reporte puede tener como máximo **20.000 boletas**. Si se supera, el sistema dice "El reporte supera el máximo de 20.000 boletas. Reduzca el rango de fechas."
- La grilla muestra las primeras **1.000** boletas; los totales incluyen todas.
- **Imprimir** y **Vista previa** funcionan hasta **2.000** boletas. Para más, exporte a PDF.
- Si el equipo no tiene **ninguna impresora instalada**, la vista previa puede fallar. Use **Exportar PDF…**.
- Si no hay logo configurado, el reporte sale sin logo y la pantalla lo avisa.
- Si un archivo no se puede guardar (por ejemplo, porque está abierto en Excel), el sistema avisa "No se pudo guardar el archivo «…». Verifique que no esté abierto en otro programa y que tenga permiso para escribir en esa carpeta."

## 8. Catálogos: ganaderos, clientes y fleteros

Esta sección es para **Supervisor** y **Administrador**. Abra **Catálogos** y elija el catálogo.

En las tres pantallas:

- **Nuevo** crea un registro, **Editar** lo corrige y **Desactivar** (o **Activar**) lo retira o lo recupera.
- Nada se borra: un registro desactivado deja de ofrecerse al pesar, pero las boletas viejas lo conservan.
- **Mostrar inactivos** muestra también los desactivados.
- Todo se guarda en mayúsculas y sin espacios repetidos.

| Catálogo | Datos | Reglas |
| :--- | :--- | :--- |
| **Ganaderos** | Código (automático), Nombre | El **código lo asigna el sistema** (1, 2, 3…; hasta 99.999) al crear el ganadero y no se puede escribir ni cambiar. Los ganaderos importados del sistema anterior conservan su código original (por ejemplo `AH`); para numerar los nuevos solo cuentan los códigos numéricos. El nombre, de 3 a 100 caracteres y con al menos una letra. |
| **Clientes** | Código (automático), Nombre, Dirección | El **código lo asigna el sistema** (el mayor código existente más 1, hasta 999.999) y no se puede cambiar. El nombre, de 3 a 100 caracteres y con al menos una letra; la dirección es opcional, hasta 150 caracteres. |
| **Fleteros** | Código (automático), Nombre, Celular | **Sin límite de fleteros.** El código lo asigna el sistema. El fletero se **elige al pesar** y la boleta imprime solo ese. El celular es **obligatorio**: de 6 a 20 caracteres (números, espacios, `+`, `-`, paréntesis) y al menos 6 dígitos. |

En los tres catálogos el código nuevo es el **mayor código existente más 1**, contando también los desactivados: un código nunca se reutiliza y los huecos no se rellenan. Al crear, el campo Código dice "(se asigna al guardar)".

La pantalla de **Ganaderos** y la de **Clientes** tienen un campo **Buscar:** que filtra mientras escribe y no distingue acentos. La de **Fleteros** no tiene buscador.

## 9. Administración

Esta sección es para el **Administrador**.

### Usuarios (Administración › Usuarios)

> [Captura: ventana "Gestión de Usuarios"]

La lista muestra usuario, nombre completo, rol, si está activo, si está bloqueado y el último acceso ("—" si nunca entró). Los desactivados se ven solo con **Mostrar usuarios inactivos**.

- **Nuevo:** escriba nombre de usuario, nombre completo, rol (Administrador, Supervisor u Operador), una contraseña inicial y su confirmación. El usuario deberá cambiarla al ingresar.
  - El nombre de usuario usa de 3 a 30 caracteres: letras minúsculas, números, punto, guion y guion bajo. El nombre completo, de 3 a 100 caracteres.
- **Editar:** cambia nombre completo y rol.
- **Desactivar / Activar:** un usuario no se borra; se desactiva.
- **Restablecer clave:** define una contraseña temporal; el usuario deberá cambiarla al ingresar. También desbloquea la cuenta.
- El sistema no permite dejar al sistema sin administrador activo ("Debe existir al menos un administrador activo."), ni que un administrador cambie su propio rol o desactive su propio usuario.
- Cree una cuenta por persona y **no comparta la cuenta de administrador**.

### Parámetros del sistema (Administración › Parámetros del sistema)

Datos de la empresa que salen en la boleta y los reportes:

- Nombre Empresa (de 3 a 100 caracteres), Dirección, Ciudad, Teléfono, Reg. Sanitario, Tel. Reclamos y Observación (la que aparece por defecto al pesar).
- **Numeración de Boleta:** muestra el siguiente número ("Siguiente:") y el último emitido ("Último número emitido: N" o "Aún no hay boletas emitidas"). **El número de boleta es automático**: lo asigna el sistema al grabar cada pesaje y no se puede cambiar a mano ("Se asigna automáticamente al grabar cada pesaje."). Única excepción: mientras **no exista ninguna boleta** (ni siquiera anulada) se puede fijar el número inicial, de 1 a 999.999.999, para continuar el talonario anterior ("Solo puede fijarse antes de emitir la primera boleta."); al guardar, el sistema pide confirmación. Después de la primera boleta el campo queda bloqueado. Para continuar una numeración histórica use la importación de datos legados.
- **Logo de la Empresa:** botones **Cambiar...** y **Quitar**. Acepta imágenes PNG, JPEG o BMP de hasta 1 MB.

### Tipo de pesaje (Administración › Tipo de pesaje)

Define qué pesa la empresa ("Elija qué pesa su empresa. Define qué se muestra al pesar, en las boletas y en los reportes."). Es también el paso 3 del asistente de puesta en marcha. Es un dato de la empresa (vale para todas las computadoras), no de cada equipo.

> [Captura: ventana "Tipo de pesaje" con las tres opciones y el aviso de boletas existentes]

| Opción | Efecto |
| :--- | :--- |
| **Res completa y media res** | Se elige el desposte en cada pesaje (como hasta la versión 1.1). |
| **Solo res completa** | La pantalla de pesaje no muestra "Desposte": siempre se capturan los dos lados. Boletas, historial, buscador, edición, anulación, reimpresión y reportes no muestran el desposte ni "media res". |
| **Solo media res** | La pantalla de pesaje no muestra "Desposte": se captura un solo lado. Igual que arriba, el desposte deja de mostrarse. |

- **Ocultar, nunca falsear.** Con una opción de un solo tipo, el desposte se oculta solo cuando todos los datos que se muestran son de ese tipo. Si en la pantalla, la boleta o el reporte aparece alguna boleta del otro tipo (anterior al cambio o importada), el desposte se vuelve a mostrar con su valor real.
- Cambiar la opción **no modifica** las boletas ya grabadas. Si hay boletas vigentes del tipo que se deja de pesar, la ventana avisa cuántas son ("Hay N boletas de media res; seguirán mostrando su tipo de desposte en las pantallas y reportes que las incluyan.").
- Si cambia una opción ya guardada por otra, el sistema pide confirmación ("Cambiará el tipo de pesaje de «…» a «…». ¿Continuar?"). El cambio queda en la auditoría como cambio de parámetros, campo "Tipo de pesaje". Guardar la misma opción no registra nada.
- En una instalación nueva el tipo empieza "sin definir" (el sistema funciona como "Res completa y media res") hasta que se elige en el asistente o aquí. Las instalaciones que ya estaban en uso al actualizar a la versión 1.2.0 quedaron en "Res completa y media res".
- Las reglas de pesaje no cambian: rangos de peso y retorno a cero son los mismos (sección 4).
- Al importar boletas del sistema anterior de un tipo que no se pesa, se importan igual con una advertencia en el informe.

### Configuración de balanza (Administración › Configuración de balanza)

Pantalla para el técnico: tipo de conexión, puerto, protocolo, "Probar conexión". Los detalles están en la **guía de balanza** (`guia-balanza.md`). No la cambie si la balanza funciona bien.

### Configuración de impresión (Administración › Configuración de impresión)

> [Captura: ventana "Configuración de impresión"]

Esta configuración es **propia de cada computadora**.

| Campo | Para qué sirve |
| :--- | :--- |
| **Impresora** | La impresora de Windows para las boletas. |
| **Formato** | **Media carta (216 × 140 mm)** para matricial, láser o inyección; **Ticket 80 mm** para térmica. |
| **Papel** | Tamaño de papel que ofrece la impresora. |
| **Copias** | De 1 a 3 (cada copia sale como página independiente con ORIGINAL / COPIA). |
| **Imprimir la boleta automáticamente al grabar** | Imprime al pulsar `F12`. |
| **Desplazamiento horizontal / vertical (mm)** | Mueve la impresión de -20 a +20 mm para alinear con formularios preimpresos. |

Botones de ayuda:

- **Imprimir página de calibración:** dibuja un recuadro a **10 mm de cada borde del papel**. Mida con una regla; si no está a 10 mm, ajuste los desplazamientos y repita.
- **Vista previa (muestra)** e **Imprimir muestra:** usan una boleta ficticia con la leyenda "MUESTRA — SIN VALOR". No gastan números de boleta ni tocan datos.

Avisos que puede ver:

- "La impresora configurada no está instalada en este equipo."
- "Impresora virtual: pedirá un nombre de archivo en cada impresión. No se recomienda con la impresión automática."

### Visor de auditoría (Administración › Visor de auditoría)

Disponible también para el Supervisor. Es **solo lectura**: no permite borrar ni cambiar nada.

1. Elija los filtros: **Desde**, **Hasta**, **Entidad**, **Acción**, **Usuario**, **N° boleta**.
2. Pulse **Buscar (Enter)**. Se muestran como máximo 500 registros.
3. Seleccione una fila. La pestaña **Cambios** muestra qué campos cambiaron (resaltados); la pestaña **JSON** muestra el detalle técnico.

Allí queda el rastro de pesajes, ediciones, anulaciones, impresiones, ingresos al sistema, cambios de configuración, copias de seguridad, importaciones y restauraciones.

### Asistente de puesta en marcha

Es una ventana que ayuda a dejar el sistema listo para trabajar. Aparece **sola** cuando el Administrador entra (después del cambio de contraseña) mientras la puesta en marcha no esté terminada. El Supervisor y el Operador nunca la ven. También se abre desde **Ayuda › Asistente de puesta en marcha…**.

> [Captura: asistente con los 8 pasos y sus estados]

Cada paso muestra su estado y un botón **Abrir…** que lleva a la pantalla correspondiente:

| Paso | Qué es | Estado |
| :--- | :--- | :--- |
| 1. Copias de seguridad | Si esta computadora reemplaza a otra, restaure aquí la copia del equipo anterior y configure una carpeta secundaria (pendrive o red). | Opcional |
| 2. Datos de la empresa | Nombre, dirección y teléfonos de la boleta; logo opcional. | **Obligatorio** (se cumple cuando el nombre deja de ser "NOMBRE DE EMPRESA") |
| 3. Tipo de pesaje | Res completa y media res, solo res completa o solo media res (ver "Tipo de pesaje"). | **Obligatorio** (se cumple al guardar una opción) |
| 4. Balanza | Tipo de conexión, puerto y protocolo. Use "Probar conexión" antes de guardar. | **Obligatorio** (se cumple cuando la conexión no es Simulador) |
| 5. Impresión de boletas | Impresora, papel y calibración de este equipo. | Recomendado |
| 6. Fleteros | Fleteros entre los que se elige al pesar; sale el elegido en la boleta. | Opcional |
| 7. Usuarios | Cuentas de operadores y supervisores. | Recomendado |
| 8. Datos del sistema anterior | Importar ganaderos, clientes y boletas desde CSV, DBF o Access. | Opcional |

Los estados posibles son **Listo** (el tipo de pesaje muestra cuál, por ejemplo "Listo (res completa)"; la balanza, "Listo (Serie)" o "Listo (Red)"), **Pendiente**, **Opcional**, **SIMULADOR** (balanza), "N activos" (fleteros) y "N usuarios". Debajo, una línea "Falta: …" dice qué impide finalizar.

Reglas del asistente:

- **Finalizar** solo se activa cuando están hechos los pasos 2, 3 y 4. Si la balanza sigue en modo Simulador, aparece una casilla para confirmar que desea **continuar con el simulador (solo pruebas)**; sin marcarla, no se puede finalizar.
- **Finalizar** deja constancia en la auditoría ("Puesta en marcha completada") y el asistente no vuelve a aparecer.
- **Omitir por ahora** cierra el asistente sin marcarlo como terminado; volverá a aparecer la próxima vez que entre el Administrador.
- La tecla `Enter` no finaliza el asistente (es a propósito).
- Si restaura una copia desde el paso 1, el sistema se reinicia para aplicarla.
- En computadoras donde el sistema ya se usaba, el asistente aparece una vez con los pasos ya marcados como listos.

## 10. Utilitarios: copias de seguridad, importación y logs

Esta sección es para el **Administrador**. Abra el menú **Utilitarios**.

### Backup / Restaurar

> [Captura: ventana "Backup / Restaurar" con la lista de copias]

Una **copia de seguridad** es un archivo `.zip` con toda la información del sistema (base de datos y logo). Sirve para recuperarse de un daño o para pasar los datos a otra computadora.

Dónde se guardan: `C:\ProgramData\PesajeFaeneo\backups`, en tres carpetas:

| Carpeta | Contenido |
| :--- | :--- |
| `manuales` | Las que usted crea con **Crear backup ahora**. |
| `automaticos` | Una por día, que crea el sistema. |
| `seguridad` | Copias que el sistema hace solo antes de restaurar, importar o actualizar de versión (conserva las 10 más recientes de cada clase). |

El nombre es `PesajeFaeneo_<tipo>_<fecha>.zip`.

**Crear una copia ahora:** pulse **Crear backup ahora**. Aparece en la lista.

**Backup automático:** marque **Crear un backup automático cada día** (con el sistema abierto, lo revisa unos 2 minutos después de abrirse y luego cada hora, y crea la copia si todavía no hay una del día). Debajo se ve el resultado del último automático de la sesión ("Último backup automático: …" o "…FALLÓ…"). Marque **Crear también al cerrar el sistema** para otra copia al salir. **Conservar los automáticos (días):** de 7 a 365 (30 por defecto); siempre se guardan al menos las 3 más recientes. Pulse **Guardar configuración**.

**Copiar también a (opcional):** elija con **Examinar…** un pendrive o una carpeta de red; cada copia se duplicará allí. Si el pendrive no está conectado, la copia local se hace igual y el sistema muestra una advertencia: "No se pudo copiar la copia de seguridad a la carpeta elegida. Verifique que el dispositivo esté conectado, tenga espacio y permita escribir." **Quitar** deja de usar esa carpeta.

**Exportar copia…:** guarda una copia elegida en otra ubicación. La copia contiene **todos los datos comerciales y las contraseñas cifradas de los usuarios**: guárdela en un lugar seguro.

**Restaurar** (vuelve a un estado anterior; **se pierde lo hecho después de esa copia**):

1. Elija la copia en la lista y pulse **Restaurar seleccionada…**, o pulse **Restaurar desde archivo…** para una copia que está fuera de la lista.
2. El sistema revisa la copia y muestra la ventana **Confirmar restauración** con un resumen (fecha de la copia, tipo, equipo, versión del sistema, boletas, última boleta, ganaderos, clientes, usuarios y administradores activos, y advertencias).
3. Escriba la palabra **RESTAURAR** (en mayúsculas) y pulse **Restaurar**.
4. El sistema guarda una copia de la base actual, se **cierra y se vuelve a abrir solo** para aplicar la restauración.
5. Entre con los usuarios de la copia restaurada.

`F5` o **Actualizar (F5)** recarga la lista. **Abrir carpeta** abre la carpeta de copias en el explorador.

Las copias **no incluyen** la configuración de impresión ni la de backup, porque son propias de cada computadora; ni los logs.

### Importar datos legados

Para traer ganaderos, clientes y boletas del sistema anterior desde archivos CSV/TXT, DBF o Access (`.mdb`, `.accdb`). Siga estos pasos y **en este orden: 1) Ganaderos, 2) Clientes, 3) Pesajes**.

> [Captura: ventana "Importar datos legados" con los tres pasos]

1. **Archivo:** elija el **Tipo** (Ganaderos, Clientes o Pesajes (boletas)), pulse **Examinar…** para elegir el archivo (si es Access, elija la **Tabla**; si el texto sale con símbolos raros, cambie la **Codificación**) y pulse **Leer archivo**.
2. **Columnas:** asigne a cada campo del sistema la columna de su archivo. El sistema sugiere por nombre; revise la muestra.
3. **Simulacro e importación:** pulse **Simulacro**. Prueba todo sin guardar nada y cuenta filas válidas, omitidas y con errores. Si todo está bien, pulse **Importar**. Si cambia el archivo o las columnas después del simulacro, hay que simular de nuevo.

Reglas útiles:

- Antes de importar, el sistema crea una copia de seguridad automática.
- Los ganaderos y clientes con código repetido se omiten sin error. La importación es el único camino en que el código **no** lo asigna el sistema: se conserva el código del archivo (por ejemplo, la columna `MARCA` del sistema anterior se reconoce como el código del ganadero).
- Una boleta con un número que ya existe se rechaza.
- Si la numeración del archivo es mayor, el siguiente número de boleta del sistema se ajusta solo.
- **Guardar informe…** crea un archivo CSV con el detalle de advertencias, omisiones y errores.
- Para leer archivos Access hace falta un componente adicional en la computadora; si falta, el sistema lo avisa y se puede exportar la tabla a CSV desde Access (ver la guía de instalación y soporte, sección 10).

### Visor de logs

Muestra los archivos de registro técnico del sistema, útiles para soporte.

- Elija el **Archivo** (por día), el **Nivel** (Todos, Información o superior, Advertencias y errores, Solo errores) y, si quiere, un texto en **Buscar:**. Pulse **Filtrar**.
- Seleccione una línea para ver el detalle completo abajo.
- **Actualizar (F5)** recarga; **Abrir carpeta** abre la carpeta de logs.
- Las filas amarillas son advertencias y las rojas, errores.

## 11. Problemas frecuentes

| Síntoma | Causa probable | Qué hacer |
| :--- | :--- | :--- |
| La balanza aparece en **rojo** ("sin datos", "tramas no reconocidas" o "error") | Cable o adaptador USB suelto, indicador apagado, o configuración equivocada | Revise que el indicador esté encendido y que el cable USB esté firme, y espere unos segundos: el sistema reintenta conectarse solo. Si sigue en rojo, avise al técnico (guía de balanza, secciones 5 y 8). |
| El peso dice **INESTABLE** todo el tiempo | La res se mueve (balanceo), hay vibración, o la tolerancia de estabilidad es muy estricta | Espere a que la res deje de balancearse. Si ocurre con la balanza vacía o con una carga quieta, avise al técnico (guía de balanza, sección 6). |
| No se puede capturar y la guía dice "Balanza en MODO SIMULADOR: solo un administrador puede capturar y grabar." | La balanza está configurada como simulador | No pese con el simulador. Avise al Administrador para configurar la balanza real. |
| La boleta se grabó pero **no se imprimió** ("…grabada, pero NO se imprimió… Pulse F9 para reintentar.") | Impresora apagada, sin papel, desconectada o mal configurada | Revise la impresora y pulse `F9` (**Imprimir último**) en la misma pantalla de pesaje; si ya la cerró, use **Historial de pesajes** o **Reimprimir boleta**. La boleta **no se pierde** ni se duplica. Si sigue fallando, avise al Administrador (Configuración de impresión). |
| En un reporte, **Vista previa** falla | La computadora no tiene ninguna impresora instalada | Use **Exportar PDF…** (siempre disponible) o pida instalar una impresora. |
| "Usuario bloqueado temporalmente por intentos fallidos…" | Se escribió mal la contraseña 5 veces | Espere los minutos indicados o pida al Administrador **Restablecer clave**. |
| "Usuario o contraseña incorrectos." | Usuario o contraseña mal escritos; Bloq Mayús activado | Revise el usuario y la contraseña. Si la olvidó, pida al Administrador **Restablecer clave**. |
| "El usuario está desactivado. Contacte al administrador." | La cuenta fue desactivada | El Administrador puede reactivarla en **Administración › Usuarios** (marque **Mostrar usuarios inactivos**). |
| Al grabar: "El tipo de pesaje configurado es «…». Cierre y vuelva a abrir la pantalla de pesaje." | El tipo de pesaje cambió con la pantalla de pesaje abierta | Cierre la pantalla de pesaje, vuelva a abrirla y pese de nuevo. No se grabó la boleta ni se gastó número. |
| **Grabar (F12)** está gris y la guía dice "Elija el fletero de la lista o borre el texto." | Hay un fletero escrito en el campo Fletero que no se eligió de la lista | Elija el fletero de la lista o borre el texto; el botón se habilita solo. Las boletas grabadas con la versión 1.2.0 o anteriores pudieron quedar sin fletero si se escribió y no se eligió: corríjalas (Supervisor: **Buscar / editar boleta**, campo Fletero) y reimprímalas. |
| En **Buscar / editar boleta**, al guardar, el campo Fletero se marca con "Elija el fletero de la lista o borre el texto." | Hay texto escrito en Fletero sin elegir de la lista | Elija el fletero o borre el texto y pulse Guardar (`F12`) otra vez. |
| En la pantalla de pesaje no aparece "Desposte" | La empresa pesa un solo tipo (Administración › Tipo de pesaje) | Es normal. Si deben pesarse ambos tipos, el Administrador cambia el tipo de pesaje. |
| "El sistema ya está en ejecución en este equipo (puede estar abierto en la sesión de otro usuario de Windows)." | Hizo doble clic dos veces, o el sistema quedó abierto en otra sesión de Windows | Espere unos segundos si acaba de abrirlo. Si no, cierre el sistema en la otra sesión de Windows (**Sesión › Salir**) o cierre esa sesión; solo puede haber un sistema abierto por computadora. |
| Al crear una copia aparece la advertencia "No se pudo copiar la copia de seguridad a la carpeta elegida…" | El pendrive o la carpeta de red no está conectada, no tiene espacio o no permite escribir | La copia local **sí se hizo**. Conecte el pendrive (o revise la red) y cree otra copia con **Crear backup ahora**. |
| El número de boleta salta (falta uno) | Se anuló una boleta | Es normal: las boletas anuladas conservan su número y no se reutilizan. Búsquela con **Buscar / editar boleta** y estado **Anulado**. |
| Una opción del menú no aparece | Su rol no tiene permiso | Ver el Anexo B. Pida al Administrador el rol que corresponda. |
| Las horas de las boletas no coinciden con el reloj | Fecha, hora o zona horaria de Windows mal configuradas | Corrija la fecha y la hora de Windows (el reloj de la barra de estado muestra la hora que usa el sistema). |
| Aparece un mensaje de error inesperado | Falla no prevista | Anote qué estaba haciendo y avise a soporte; el Administrador puede abrir **Utilitarios › Visor de logs** y mostrar el error. |

## 12. Licencia: activación, prueba y modo consulta

El sistema se activa con una **clave de activación** (`XXXX-XXXX-XXXX-XXXX-XXXX`) que sirve para un solo equipo, o se usa en **prueba durante 15 días**.

### Pantalla «Activar Capataz»

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

## Anexo A. Teclas rápidas

### Pantalla principal

| Tecla | Acción |
| :--- | :--- |
| `F2` | Registrar pesaje |
| `F3` | Reimprimir boleta |
| `F4` | Historial de pesajes |
| `F6` | Buscar / editar boleta (Supervisor, Administrador) |
| `Enter` | Abrir la tarjeta que tiene el foco (al abrir el sistema, **Control de peso**) |
| `Ctrl+Shift+L` | Cerrar sesión |
| `Alt` + letra subrayada | Abrir un menú |

### Pantalla de pesaje

| Tecla | Acción |
| :--- | :--- |
| `F5` | Capturar lado izquierdo |
| `F6` | Capturar lado derecho |
| `F8` | Borrar pesaje (descarta los pesos capturados) |
| `F9` | Imprimir el último pesaje grabado en la pantalla |
| `F12` | Grabar pesaje |
| `Enter` | Pasar al siguiente campo; en el buscador de ganadero o cliente, elegir la coincidencia única o abrir el alta rápida si no existe |
| `Esc` | Cerrar la pantalla (avisa si hay pesos sin grabar); cierra una lista desplegada |

### Reimprimir boleta

| Tecla | Acción |
| :--- | :--- |
| `Enter` | Buscar el número escrito |
| `F4` | Vista previa |
| `F9` | Imprimir |
| `Esc` | Cerrar |

### Historial de pesajes

| Tecla | Acción |
| :--- | :--- |
| `F5` | Actualizar el día elegido |
| `F9` | Reimprimir la boleta seleccionada |
| `Esc` | Cerrar |

### Buscar / editar boletas

| Tecla | Acción |
| :--- | :--- |
| `Enter` | Buscar; con el cursor en la grilla, editar la boleta seleccionada |
| `F4` | Editar la boleta seleccionada |
| `F8` | Anular la boleta seleccionada |
| `Esc` | Cerrar |

### Editar boleta

| Tecla | Acción |
| :--- | :--- |
| `Tab` | Pasar al siguiente campo (`Enter` no guarda) |
| `F12` | Guardar |
| `Esc` | Cancelar |

### Otras pantallas

| Pantalla | Tecla | Acción |
| :--- | :--- | :--- |
| Reportes | `Enter` / `Esc` | Generar / cerrar |
| Visor de auditoría | `Enter` / `Esc` | Buscar / cerrar |
| Backup / Restaurar | `F5` / `Esc` | Actualizar la lista / cerrar |
| Visor de logs | `F5` / `Esc` | Actualizar / cerrar |
| Vista previa de boleta | `Esc` | Cerrar |
| Anular boleta | `Esc` | Cancelar |

## Anexo B. Permisos por rol

Cada fila es una acción del sistema. "Sí" significa que el rol puede hacerla.

| Permiso | Qué permite | Operador | Supervisor | Administrador |
| :--- | :--- | :---: | :---: | :---: |
| Pesar | Registrar pesajes (Pesaje › Registrar pesaje) y ver el historial de pesajes (Pesaje › Historial de pesajes) | Sí | Sí | Sí |
| Imprimir boleta | Imprimir desde la pantalla de pesaje (`F9`) | Sí | Sí | Sí |
| Reimprimir boleta | Boletas › Reimprimir boleta | Sí | Sí | Sí |
| Alta rápida de catálogos | Crear ganaderos y clientes desde la pantalla de pesaje | Sí | Sí | Sí |
| Ver reportes | Menú Reportes | No | Sí | Sí |
| Editar boleta | Boletas › Buscar / editar boleta | No | Sí | Sí |
| Anular boleta | Boletas › Anular boleta | No | Sí | Sí |
| Gestionar catálogos | Menú Catálogos (ganaderos, clientes, fleteros) | No | Sí | Sí |
| Ver auditoría | Administración › Visor de auditoría | No | Sí | Sí |
| Gestionar usuarios | Administración › Usuarios | No | No | Sí |
| Gestionar parámetros | Parámetros del sistema, Tipo de pesaje, Configuración de impresión, Visor de logs y Asistente de puesta en marcha | No | No | Sí |
| Configurar balanza | Administración › Configuración de balanza; pesar con el simulador | No | No | Sí |
| Backup y restauración | Utilitarios › Backup / Restaurar | No | No | Sí |
| Importar datos legados | Utilitarios › Importar datos legados | No | No | Sí |

Todos los roles pueden además cambiar su propia contraseña, cerrar sesión, salir y ver **Ayuda › Acerca de…**.

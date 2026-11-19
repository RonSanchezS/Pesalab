# Guía de balanza

Esta guía es para el **técnico que instala** el sistema y deja la balanza lista. Corresponde a la versión **1.2.1** del Capataz (la configuración de balanza no cambió desde la 1.0.0).

La configuración se hace en **Administración › Configuración de balanza** y solo está disponible para el rol **Administrador**.

> [Captura: pantalla "Configuración y Diagnóstico de Balanza" completa]

## 1. Tipos de conexión

El sistema lee el peso que el **indicador** de la balanza transmite de forma **continua**. Hay tres tipos de conexión:

| Tipo (en pantalla) | Cuándo se usa | Datos que se configuran |
| :--- | :--- | :--- |
| **Serie (RS-232 / USB)** | Indicador con salida serie, directa o con un adaptador USB-serie. | Puerto (COM1 a COM999), baudios (1200, 2400, 4800, 9600, 19200, 38400, 57600 o 115200), bits de datos (7 u 8), paridad (Ninguna, Impar, Par, Marca o Espacio) y bits de parada (1 o 2). |
| **Red (TCP/IP)** | Indicador con puerto Ethernet o conversor serie-Ethernet. | Host / IP y puerto (1 a 65535). |
| **Simulador** | Solo pruebas y capacitación (ver sección 9). | Ninguno. |

Notas:

- En una instalación nueva la balanza viene configurada como **Simulador** (con 9600 baudios, 8 bits, sin paridad y 1 bit de parada ya cargados como valores de partida). Hay que cambiarla a **Serie** o **Red** para pesar de verdad.
- En modo **Serie**, el sistema respeta la configuración que ya tiene el controlador (driver) del puerto y solo cambia baudios, bits, paridad o parada cuando difieren. Esto es necesario para los adaptadores USB-serie con chip CH340, que rechazan que se reconfigure todo el puerto.
- Si el controlador rechaza el formato pedido, el mensaje indica en qué formato está configurado el puerto: use esos mismos valores o cámbielos en el Administrador de dispositivos de Windows (Propiedades del puerto › Configuración de puerto).
- El sistema **no pregunta** el peso al indicador: espera a que el indicador lo transmita solo. Un indicador que solo responde cuando se le envía una orden no está soportado en esta versión.
- Los parámetros de los grupos que no corresponden al tipo elegido aparecen deshabilitados. Aun así el sistema los valida al guardar: deje en ellos valores válidos (por ejemplo, un puerto `COMn` aunque use Red, o una IP o nombre de equipo aunque use Serie).

## 2. Adaptador USB-serie y número de puerto COM fijo

Si el indicador se conecta a la computadora con un adaptador USB-serie (el del sitio usa chip **CH340**), siga estos pasos.

### Instalar el controlador

1. Conecte el adaptador a un puerto USB de la computadora.
2. Instale el controlador (driver) del adaptador CH340 si Windows no lo instala solo.
3. Abra el **Administrador de dispositivos** de Windows y busque **Puertos (COM y LPT)**. El adaptador aparece con un nombre y un número de puerto, por ejemplo `COM4`.

### Fijar el número de puerto COM

El número de COM puede cambiar si el adaptador se conecta a otro puerto USB o se vuelve a conectar (en el sitio se observó el cambio de **COM3 a COM4**). Para evitarlo:

1. En el Administrador de dispositivos, abra **Puertos (COM y LPT)**.
2. Haga doble clic en el adaptador (o clic derecho › **Propiedades**).
3. Abra la pestaña **Configuración de puerto** y pulse **Opciones avanzadas**.
4. En **Número de puerto COM**, elija el número que quiere usar y acepte.
5. Anote ese número y escríbalo en **Puerto** de la configuración de balanza del sistema.

### Buenas costumbres

- Use **siempre el mismo puerto USB** de la computadora para el adaptador. Marque con una etiqueta ese puerto.
- Si cambia el puerto USB o reemplaza el adaptador, revise el número de COM y corríjalo en el sistema.
- Con el tiempo pueden quedar **entradas fantasma** (puertos COM que ya no existen pero siguen reservados). Para verlas, en el Administrador de dispositivos use **Ver › Mostrar dispositivos ocultos**. Quite las entradas fantasma que ya no se usen para liberar números de COM.
- En la pantalla de configuración, el botón **Refrescar** vuelve a leer los puertos disponibles. El campo **Puerto** también permite escribir el nombre a mano.

## 3. Configuración verificada: indicador RX8 Accurex

Esta configuración se verificó en sitio con un indicador **RX8 Accurex** conectado con un adaptador **CH340**:

| Campo en pantalla | Valor |
| :--- | :--- |
| Tipo | Serie (RS-232 / USB) |
| Puerto | El COM del adaptador CH340 (en el sitio: COM3 o COM4; ver sección 2) |
| Baudios | 9600 |
| Bits de datos | 8 |
| Paridad | Ninguna |
| Bits de parada | 1 |
| Separación de tramas | Inicio con '=' (RX8 / XK3190) |
| Unidad que transmite | Gramos (g) |
| Patrón (regex) | El que viene sembrado: `^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)` |

Cómo transmite este indicador:

- Envía el peso en **gramos** con el formato `=NNNNNN` (un signo igual y el número), separado por espacios y **sin salto de línea**. Ejemplo: `=012345` son 12,345 kg, que el sistema redondea a **12,3 kg**; `=450000` son 450,0 kg.
- En ráfagas encadena tramas sin espacio entre ellas (por ejemplo `=650000-=381000-`). Por eso se usa la separación **Inicio con '='**: lo único constante es el `=` al comienzo de cada trama.

Valores de partida de la estabilidad (vienen cargados): **Lecturas para estabilidad** 5, **Tolerancia** 0,5 kg, **Tiempo sin datos** 3000 ms. Ver sección 6.

> **Pendiente en sitio:** la conversión de gramos a kilogramos debe validarse con un **peso patrón conocido** (ver sección 7).

> [Captura: configuración del RX8 completa y resultado de "Probar conexión"]

## 4. Otros indicadores: tramas, unidad y patrón

Para un indicador distinto hay que averiguar cómo transmite (en el manual del fabricante o mirando las tramas con **Probar conexión**, sección 5) y ajustar tres cosas: la **separación de tramas**, la **unidad** y el **patrón**.

### Separación de tramas

Una "trama" es cada mensaje con un peso. El sistema necesita saber dónde empieza y dónde termina cada una:

| Opción | Cuándo usarla |
| :--- | :--- |
| **Fin de línea (CR/LF)** | El indicador termina cada trama con un salto de línea. Es la opción por defecto. |
| **Espacio** | El indicador separa las tramas con un espacio y sin salto de línea. |
| **Inicio con '=' (RX8 / XK3190)** | Cada trama comienza con `=` y puede venir pegada a la anterior (indicadores RX8 Accurex y XK3190). |

Una trama muy larga (más de 256 caracteres sin separador) se descarta.

### Unidad que transmite

- **Kilogramos (kg):** el número recibido ya está en kg. Es la opción por defecto.
- **Gramos (g):** el sistema divide el número por 1000 y luego redondea a 0,1 kg (el medio punto sube).

Si el indicador transmite en otra unidad, esta versión no la convierte.

Cuidado: si la unidad está mal, el peso sale **mil veces mayor o menor** (ver sección 8).

### Patrón (expresión regular)

El patrón le dice al sistema en qué parte de la trama está el peso. Reglas:

- Debe contener un grupo con nombre `(?<peso>...)`. Sin él, no se puede guardar.
- Puede tener, opcionalmente, un grupo `(?<estable>...)` (ver sección 6).
- Máximo 250 caracteres.
- Acepta punto o coma como separador decimal; la coma se convierte en punto.

El patrón por defecto es:

```text
^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)
```

Se lee así: desde el inicio de la trama (`^`), salta cualquier texto que no sea signo ni dígito (`[^-+\d]*`, por ejemplo el `=`); luego toma el peso (`peso`): un signo opcional, espacios opcionales, dígitos y, si los hay, decimales con punto o coma.

**Por qué conviene un patrón específico del indicador.** El patrón por defecto acepta casi cualquier trama que tenga un número. Si el indicador manda caracteres basura (por ejemplo al encenderse, o por un baudaje equivocado), esa basura puede contener dígitos y el sistema podría tomarla por un peso. Un patrón que describe **exactamente** la forma de la trama del indicador rechaza lo que no encaja.

Ejemplo para un indicador que transmite `=` seguido del peso en gramos (como el RX8), con la separación **Inicio con '='** y la unidad **Gramos**:

```text
^=(?<peso>-?\d+)
```

Solo acepta tramas que empiezan con `=` y luego un número. Una trama como `xx12` se rechaza. Antes de guardar, pruebe siempre el patrón con **Probar patrón** (más abajo).

### Probar un patrón sin conectar nada

En el grupo **Probar patrón con una trama de ejemplo (sin conexión)**:

1. Escriba una trama real en **Trama de ejemplo** (por ejemplo `=012345`).
2. Pulse **Probar**.
3. Verde: muestra el peso leído y si es "estable: sí / no / no informado". Rojo: "Error: …" (patrón inválido) o "Trama no reconocida: …".

La prueba usa la unidad elegida en pantalla, de modo que `=012345` con **Gramos** muestra 12,3 kg.

## 5. Probar conexión

La prueba comprueba la configuración **que está en pantalla**, aunque todavía no esté guardada.

1. Ponga el indicador a transmitir (idealmente con una carga estable en la balanza).
2. En **Administración › Configuración de balanza**, ajuste los datos de la conexión y de la lectura.
3. Pulse **Probar conexión** (grupo **Diagnóstico en vivo de la configuración**). La prueba dura **10 segundos**; mientras corre, **Cancelar** la detiene.
4. Observe:
   - La lista muestra las **tramas tal como llegan** (las últimas 50), ya separadas según la **Separación de tramas**. Los caracteres de control se ven como `<0x..>` (por ejemplo `<0x02>`).
   - El peso grande muestra la lectura reconocida (verde cuando está estable).
5. Lea el resultado:
   - **"Conexión correcta: X de Y tramas reconocidas. Estabilidad: alcanzada."** Todo bien.
   - **"…Estabilidad: no alcanzada."** Hay conexión y lecturas, pero no se asentaron; ver sección 6.
   - **"…Revise el patrón de lectura."** Llegaron tramas, pero ninguna se reconoció: patrón, separación o unidad (ver sección 8).
   - **"Se conectó, pero no llegó ninguna trama. Revise que el indicador esté encendido y transmitiendo, y la «Separación de tramas» (el RX8 usa «Inicio con '='»)."** (en rojo oscuro). El puerto se abrió, pero no se separó ninguna trama: el indicador no transmite, la velocidad es otra, o la **Separación de tramas** no corresponde (por ejemplo **Fin de línea** con un indicador que no manda salto de línea, como el RX8). No es una conexión correcta: no pulse **Guardar** hasta que lleguen tramas. Ver sección 8.
   - **"No se pudo conectar: …"** El mensaje dice el motivo (puerto, cable, red).
   - **"Prueba cancelada."**
6. Si todo está bien, pulse **Guardar**. El sistema avisa: "Configuración guardada. La balanza se reinició con la nueva configuración."

Detalles:

- Mientras dura la prueba, la lectura normal se pausa (la barra de estado muestra "en diagnóstico") y se reanuda sola al terminar.
- Los controles de conexión y lectura se bloquean durante la prueba.
- En el grupo **Balanza en uso (servicio compartido)** se ve el estado real y el peso actual; **Reconectar** reinicia la conexión con la configuración guardada.
- Si cierra la ventana con cambios sin guardar, el sistema pregunta antes.

## 6. Estabilidad

El sistema solo permite capturar un peso cuando es **estable**. Hay tres valores en el grupo **Lectura y detección de estabilidad**:

| Campo | Rango | Valor de partida | Qué hace |
| :--- | :--- | :--- | :--- |
| **Lecturas para estabilidad** | 1 a 50 | 5 | Cuántas lecturas seguidas se miran. |
| **Tolerancia (kg)** | 0,0 a 10,0 (un decimal) | 0,5 | La diferencia máxima permitida entre la mayor y la menor de esas lecturas. |
| **Tiempo sin datos (ms)** | 500 a 60000 | 3000 | Cuánto espera una trama antes de contar un vencimiento. También es el tiempo máximo para conectar. |

El peso es **estable** cuando las últimas N lecturas (N = Lecturas para estabilidad) están todas dentro de la tolerancia. Para capturar, además, la lectura debe ser reciente (dentro del tiempo sin datos).

Vigilancia de la conexión (no configurable):

- Tras **3 vencimientos seguidos** del tiempo sin datos (con el valor de partida, unos 9 segundos sin recibir nada), la barra de estado pasa a **"Balanza: sin datos"** y el sistema cierra la conexión y la vuelve a abrir solo, con esperas crecientes de 1, 2, 4, 8 y luego 10 segundos ("Reintentando conexión (intento N)…").
- Tras **3 tramas seguidas** que el patrón no reconoce, la barra pasa a **"Balanza: tramas no reconocidas"**. Vuelve a la normalidad con la primera trama reconocida.

Ajustes típicos:

- Si el peso dice INESTABLE aunque la carga está quieta: suba la **Tolerancia** o baje las **Lecturas para estabilidad**.
- Si se capturan pesos antes de que la res se asiente: baje la **Tolerancia** o suba las **Lecturas para estabilidad**.

### El grupo opcional `(?<estable>...)`: lo que informa el propio indicador

Algunos indicadores incluyen en la trama una marca de estabilidad. Si el patrón tiene un grupo llamado `estable`, el sistema lo interpreta así (así funciona `ParserTramas`):

- Si el grupo **captura algo** en esa trama, la trama se considera "estable informado".
- Si el grupo **no captura nada** (porque la marca no aparece), la trama se considera **inestable informada**: el sistema descarta las lecturas acumuladas y empieza la cuenta de nuevo.
- Aun con marca de estable, el sistema **sigue exigiendo** las N lecturas dentro de la tolerancia.
- Si el patrón **no** tiene el grupo `estable`, la estabilidad se deduce solo con las N lecturas y la tolerancia. El patrón por defecto no tiene este grupo.

Ejemplo (es el que cubren las pruebas del sistema), con el patrón:

```text
^(?<estable>ST)?[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)
```

| Trama | Peso | Estable informado |
| :--- | :--- | :--- |
| `ST,GS,+  98.6kg` | 98,6 kg | Sí (empieza con `ST`) |
| `US,GS,+  98.6kg` | 98,6 kg | No (no empieza con `ST`) |
| `ST,GS,-   0.4kg` | -0,4 kg | Sí |

**Cuidado:** si usa el grupo `estable` pero el indicador no manda esa marca en las tramas estables, el peso quedará **INESTABLE para siempre**. Pruebe con **Probar patrón** y con **Probar conexión** antes de guardar.

Además, entre el primer y el segundo lado de una res completa, el sistema exige que el peso baje a **2,0 kg o menos** (retorno a cero). Este valor no es configurable.

## 7. Validación con peso patrón

Antes de usar la balanza en producción hay que comprobar que el peso que muestra el sistema coincide con un peso conocido. Este procedimiento corresponde al caso **UAT-BAL-02** del checklist de aceptación en sitio.

Material: al menos una pesa **patrón certificada** (idealmente de 20 kg o más) y, si existe, una carga de 100 kg o más.

Procedimiento:

1. Con la balanza **vacía**, compruebe que el peso es 0,0 kg.
2. Coloque la primera pesa patrón. Espere a que diga **ESTABLE**.
3. Anote la lectura (en la pantalla de configuración, en **Balanza en uso**, o en la barra de estado).
4. Retire la pesa y vuelva a colocarla. Repita hasta tener **3 lecturas** con cada pesa.
5. Repita con las demás pesas patrón.
6. Compare cada lectura con el valor certificado. La diferencia **no debe superar la resolución del indicador** (el sistema trabaja con una resolución de 0,1 kg).
7. Complete la planilla y guárdela con el acta de aceptación.

Planilla de registro:

| Pesa patrón (kg certificados) | Lectura 1 (kg) | Lectura 2 (kg) | Lectura 3 (kg) | Mayor diferencia (kg) | ¿Dentro de la resolución? | Resultado (OK / FALLA) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| | | | | | | |
| | | | | | | |
| | | | | | | |

Si falla:

- Lecturas **mil veces mayores o menores**: revise la **Unidad que transmite** (sección 8).
- Diferencias pequeñas pero mayores que la resolución: revise la calibración del indicador con el proveedor de la balanza; el sistema solo muestra lo que el indicador transmite.

## 8. Problemas y soluciones

En la barra de estado y en **Balanza en uso**, los errores de conexión se muestran como **"Balanza: error — "** seguido del mensaje de la tabla.

| Mensaje o síntoma | Causa | Solución |
| :--- | :--- | :--- |
| "No se pudo abrir el puerto COMx. Verifique que el cable o adaptador esté conectado." | El COM no existe o está mal escrito (el adaptador cambió de número), cable o adaptador desconectado, o falta el controlador. | Abra el Administrador de dispositivos y confirme el COM del adaptador (sección 2). Corrija el **Puerto** y pulse **Refrescar** y **Probar conexión**. |
| "El puerto COMx está en uso por otra aplicación o no hay permiso para abrirlo." | Otro programa (otro software de balanza, un monitor serie) tiene el puerto abierto. | Cierre ese programa y pulse **Reconectar**. |
| "El driver del puerto COMx rechazó la configuración …" | El controlador del adaptador no permite el formato pedido (típico del CH340). | Use los valores que el mensaje dice que tiene el puerto, o cámbielos en Propiedades del puerto (Administrador de dispositivos). |
| Barra de estado: **"Balanza: sin datos"** (rojo), o en la prueba "Se conectó, pero no llegó ninguna trama…" | El indicador no transmite: apagado, cable suelto, velocidad equivocada, o el indicador transmite solo bajo pedido. También ocurre si la **Separación de tramas** no corresponde: con **Fin de línea** y un indicador que no manda salto de línea, el sistema nunca corta una trama y descarta todo. | Revise energía y cable; confirme baudios; pruebe con **Probar conexión** y mire si llegan tramas en la lista. Si la lista queda vacía con el indicador encendido, pruebe la separación **Espacio** o **Inicio con '='** (sección 4). |
| **"Balanza: tramas no reconocidas"** (rojo) o "Revise el patrón de lectura." | Llegan datos, pero el patrón no los entiende: baudios o paridad equivocados (llegan caracteres raros), separación de tramas incorrecta o patrón equivocado. | Mire las tramas en la lista de la prueba. Si son caracteres raros: baudios, bits y paridad. Si son legibles: separación de tramas y patrón (use **Probar patrón**). |
| El peso dice **INESTABLE** siempre | La carga se mueve; o **Lecturas para estabilidad** y **Tolerancia** no son adecuadas; o el patrón tiene el grupo `estable` y el indicador no manda la marca. | Ver sección 6: ajuste tolerancia y lecturas; revise el grupo `estable`. |
| El peso sale **mil veces mayor o menor** (por ejemplo 12345,0 en vez de 12,3) | La **Unidad que transmite** no coincide con la del indicador. | Si el indicador transmite gramos, elija **Gramos (g)**; si transmite kilos, **Kilogramos (kg)**. Si el número queda fuera del rango admitido, la trama se rechaza. |
| Después de reconectar el adaptador, no hay datos o el puerto no se abre | El número de **COM cambió** (por ejemplo de COM3 a COM4) | Vea el nuevo número en el Administrador de dispositivos, corrija **Puerto**, y fíjelo (sección 2). |
| "No se pudo conectar a host:puerto (…)" o "Tiempo de espera agotado al conectar con host:puerto." (Red) | IP o puerto equivocados, equipo apagado o sin red. | Compruebe la IP y el puerto del indicador y que la computadora lo alcance en la red. |
| "Se perdió la conexión con la balanza.", "Se perdió la comunicación con el puerto COMx." (Serie), "La balanza cerró la conexión." o "Se perdió la conexión con host:puerto." (Red) | Cable, adaptador o red interrumpidos | Revise la conexión; el sistema reintenta solo y se recupera. |
| Barra de estado: **"[SIMULADOR]"** | La balanza está configurada como Simulador | Cambie el **Tipo** a Serie o Red y guarde. |

Dónde buscar más información: **Utilitarios › Visor de logs** (nivel "Advertencias y errores"). Los problemas de balanza se registran allí una vez por cambio, no en cada lectura.

## 9. Modo simulador

El simulador es una balanza falsa interna del sistema:

- Genera un peso que se acerca a un **peso objetivo** con una oscilación que se va amortiguando, para imitar una res que se asienta. También puede simular una desconexión física.
- Sirve **solo para pruebas y capacitación**. Los pesos **no son reales**.
- En la barra de estado se ve **`[SIMULADOR]`** con fondo naranja, y en la pantalla de pesaje el aviso "MODO SIMULADOR — los pesos no son reales".
- Las boletas pesadas con el simulador quedan marcadas **SIMULADO** (buscador de boletas, historial de pesajes, reportes y auditoría; en los reportes, además, con un asterisco y una nota al pie) y se imprimen con la leyenda **"PESO SIMULADO — SIN VALOR COMERCIAL"** y una marca de agua `SIMULADO` en media carta.
- **Solo el Administrador** puede capturar y grabar pesajes con el simulador. Para Operador y Supervisor, la captura y el grabado quedan bloqueados.
- El Administrador controla el simulador en la pantalla de pesaje (**Panel de Simulación de Balanza (Admin)**: **Peso kg**, **Cargar**, **Vaciar**) y en **Configuración de balanza** (grupo **Control del modo simulador**: **Peso objetivo (kg)** hasta 2000, **Aplicar**, **Simular carga** —un peso al azar entre 80 y 160 kg—, **Vaciar** y la casilla **Simular desconexión física de la balanza**). Ese grupo solo se habilita cuando la configuración **guardada** es Simulador.
- Al terminar la capacitación, **vuelva a configurar la balanza real** antes de la jornada de trabajo.

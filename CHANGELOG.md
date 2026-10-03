# Registro de cambios

Todos los cambios relevantes de este proyecto se documentan en este archivo.
El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es [SemVer](https://semver.org/lang/es/) (`MAYOR.MENOR.PARCHE`): PARCHE = correcciones sin migración; MENOR = funciones nuevas o migraciones; MAYOR = cambios incompatibles con backups anteriores.

## [1.0.0] - sin publicar

### Agregado
- **F0 — Fundaciones del proyecto:** estructura de solución en capas, entidades de dominio, host genérico con DI y Serilog, EF Core con SQLite (WAL), datos iniciales, manejo global de excepciones e infraestructura de pruebas.
- **F1 — Seguridad, usuarios y shell principal:** autenticación con bloqueo por intentos, roles y permisos, pantalla de inicio de sesión, menú principal con autorización, gestión de usuarios y cambio de contraseña.
- **F2 — Parámetros y catálogos:** datos de la empresa, ganaderos, clientes, fleteros (F1..F4) y control reutilizable de búsqueda con autocompletado.
- **F3 — Integración con balanza:** lectores serie (RS-232/USB, kernel32 directo) y TCP, parser de tramas por regex, detector de estabilidad, simulador, servicio compartido y diagnóstico.
- **F4 — Registro de pesaje:** reglas de dominio, numeración atómica de boletas, secuencia por ganadero, pantalla de pesaje con display en vivo, captura, flujo por teclado y alta rápida de catálogos.
- **F5 — Boleta de pesaje:** diseño de boleta (media carta y ticket 80 mm), impresión y vista previa, impresión automática, reimpresión y calibración por equipo.
- **F6 — Edición, anulación y auditoría:** buscador de boletas, edición sin modificar pesos, anulación con motivo, auditoría con diferencias y visor de auditoría.
- **F7 — Reportes:** servicio común de reportes, índices y rendimiento, exportación a PDF (QuestPDF) y Excel (ClosedXML), impresión y reporte de boletas anuladas.
- **F8 — Utilitarios:** backup manual y automático con retención, restauración validada, importador de datos legados (CSV, DBF, Access) y visor de logs.
- **F9 — Empaquetado, instalador y puesta en marcha:**
  - Versión única (`Directory.Build.props`) y "Ayuda › Acerca de".
  - Publicación self-contained por script y autoprueba de la instalación (`--autoprueba`).
  - Migraciones automáticas al iniciar con resguardo previo (`PreMigracion`), detección de base de una versión más nueva y auditoría `ActualizacionSistema`.
  - Instalador Inno Setup por equipo con permisos de datos para usuarios estándar y datos conservados al desinstalar.
  - Instancia única por equipo también entre sesiones de Windows.
  - Asistente de puesta en marcha para el Administrador y recuperación local de la cuenta `admin` (`--restablecer-admin`).
  - Checklist de aceptación en sitio (UAT), acta de aceptación, manual de usuario, guía de balanza y guía de instalación y soporte.

### Notas
- Instalador sin firma de código: Windows SmartScreen o el antivirus pueden advertir al ejecutarlo.
- QuestPDF se usa bajo licencia Community: el cliente debe confirmar que cumple sus condiciones.
- Un PDF de decenas de miles de boletas puede consumir varios GB de memoria: se recomiendan 8 GB de RAM.
- No hay reversión automática de migraciones: ante un fallo, la guía de soporte describe la vuelta atrás desde el resguardo `PesajeFaeneo_premigracion_*.zip`.
- Una sola instancia del sistema por equipo; los datos de `C:\ProgramData\PesajeFaeneo` son legibles por los usuarios locales de Windows.
- Pendiente de validar en sitio: instalador en una PC limpia, conversión de peso con una pesa patrón y formato real de los datos del sistema anterior.

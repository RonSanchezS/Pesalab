# Guía del Agente y Control de Ejecución (Agents.md)

Este documento registra la arquitectura, convenciones, estado de ejecución fase por fase y bitácora técnica para la reconstrucción del **Sistema de Pesaje para Faeneo**.

---

## 1. Orden y Mapa de Fases

El plan maestro se ejecuta en el siguiente orden estricto de dependencias:

```mermaid
flowchart TD
    F0["F0: Fundaciones del proyecto"] --> F1["F1: Seguridad, usuarios y shell"]
    F0 --> F3["F3: Integración con balanza"]
    F1 --> F2["F2: Parámetros y catálogos"]
    F2 --> F4["F4: Registro de pesaje"]
    F3 --> F4
    F4 --> F5["F5: Boleta de pesaje"]
    F5 --> F6["F6: Edición, anulación y auditoría"]
    F6 --> F7["F7: Reportes"]
    F7 --> F8["F8: Utilitarios"]
    F8 --> F9["F9: Empaquetado e instalador"]
```

---

## 2. Estado de Ejecución por Fase

| Fase | Título | Estado | HUs Completadas | Total HUs | Fecha de Finalización |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **F0** | **Fundaciones del proyecto** | ✅ Completado | 7 | 7 | 2026-09-24 |
| **F1** | **Seguridad, usuarios y shell principal** | ⏳ Siguiente en turno | 0 | 6 | — |
| **F2** | **Parámetros y catálogos** | ⏳ Pendiente | 0 | 6 | — |
| **F3** | **Integración con balanza** | ⏳ Pendiente | 0 | 7 | — |
| **F4** | **Registro de pesaje** | ⏳ Pendiente | 0 | 7 | — |
| **F5** | **Boleta de pesaje** | ⏳ Pendiente | 0 | 6 | — |
| **F6** | **Edición, anulación y auditoría** | ⏳ Pendiente | 0 | 5 | — |
| **F7** | **Reportes** | ⏳ Pendiente | 0 | 6 | — |
| **F8** | **Utilitarios** | ⏳ Pendiente | 0 | 6 | — |
| **F9** | **Empaquetado, instalador y puesta en marcha** | ⏳ Pendiente | 0 | 6 | — |

---

## 3. Registro Detallado de HUs por Fase

### [x] Fase F0 — Fundaciones del proyecto (COMPLETADA)
- [x] **HU-F0-01:** Estructura de solución y proyectos (`src/PesajeFaeneo.sln`, props, capas limpias, arquitectura tests).
- [x] **HU-F0-02:** Entidades de dominio y enumeraciones (`Usuario`, `ParametrosSistema`, `ConfiguracionBalanza`, `Ganadero`, `Cliente`, `Fletero`, `Pesaje`, `Auditoria`, Enums y Excepciones).
- [x] **HU-F0-03:** Host genérico, DI, configuración y logging (Serilog con retención de 30 días, Host, Mutex `Global\PesajeFaeneo`, `AppPaths`).
- [x] **HU-F0-04:** DbContext, mapeo EF Core y migración inicial (`PesajeDbContext`, SQLite, pesos REAL, índices únicos y compuestos, migración `Inicial`, pragmas WAL).
- [x] **HU-F0-05:** Datos iniciales y hasher de contraseñas (`Pbkdf2PasswordHasher` con SHA256/100k, `DataSeeder` idempotente con admin/admin).
- [x] **HU-F0-06:** Manejo global de excepciones (`ClasificadorErrores`, `ManejadorErrores`, `FormBase` con `EjecutarAsync`).
- [x] **HU-F0-07:** Infraestructura de pruebas (`SqliteDbFixture` en memoria, `SqliteArchivoFixture`, Builders de prueba, `build.ps1` con cobertura).

### [ ] Fase F1 — Seguridad, usuarios y shell principal
- [ ] **HU-F1-01:** Servicio de autenticación y auditoría base
- [ ] **HU-F1-02:** Sesión, permisos y autorización por rol
- [ ] **HU-F1-03:** Pantalla de inicio de sesión
- [ ] **HU-F1-04:** Shell y menú principal con autorización
- [ ] **HU-F1-05:** Gestión de usuarios (CRUD)
- [ ] **HU-F1-06:** Cambio de contraseña

### [ ] Fase F2 — Parámetros y catálogos
- [ ] **HU-F2-01:** Parámetros del Sistema (datos de la empresa)
- [ ] **HU-F2-02:** Reglas de validación y normalización de catálogos
- [ ] **HU-F2-03:** Gestión de Ganaderos
- [ ] **HU-F2-04:** Gestión de Clientes
- [ ] **HU-F2-05:** Gestión de Fleteros (F1..F4)
- [ ] **HU-F2-06:** Control reutilizable de búsqueda y autocompletado

### [ ] Fase F3 — Integración con balanza
- [ ] **HU-F3-01:** Abstracción IScaleReader y ciclo de conexión
- [ ] **HU-F3-02:** Parser de tramas configurable por regex
- [ ] **HU-F3-03:** Detector de estabilidad
- [ ] **HU-F3-04:** Lectores Serial (RS-232/USB) y TCP
- [ ] **HU-F3-05:** Simulador de balanza
- [ ] **HU-F3-06:** Servicio de balanza compartido y entrega segura a la UI
- [ ] **HU-F3-07:** Configuración y diagnóstico de balanza

### [ ] Fase F4 — Registro de pesaje
- [ ] **HU-F4-01:** Reglas de dominio del pesaje
- [ ] **HU-F4-02:** Grabar pesaje con numeración atómica y auditoría
- [ ] **HU-F4-03:** Sugerencia de secuencia por ganadero del día
- [ ] **HU-F4-04:** Pantalla de pesaje y display en vivo
- [ ] **HU-F4-05:** Captura de pesos desde la balanza
- [ ] **HU-F4-06:** Flujo por teclado, estados de botones y mensajes guía
- [ ] **HU-F4-07:** Alta rápida de ganadero y cliente desde el pesaje

### [ ] Fase F5 — Boleta de pesaje
- [ ] **HU-F5-01:** Datos de boleta y configuración de impresión por equipo
- [ ] **HU-F5-02:** Renderizador del layout de boleta
- [ ] **HU-F5-03:** Impresión y vista previa con PrintDocument
- [ ] **HU-F5-04:** Imprimir desde el pesaje e impresión automática al grabar
- [ ] **HU-F5-05:** Reimpresión por número y acceso rápido
- [ ] **HU-F5-06:** Selección de impresora, papel y calibración

### [ ] Fase F6 — Edición, anulación y auditoría
- [ ] **HU-F6-01:** Buscador de boletas
- [ ] **HU-F6-02:** Editar boleta sin modificar pesos
- [ ] **HU-F6-03:** Anular boleta con motivo obligatorio
- [ ] **HU-F6-04:** Auditoría con diff antes/después en JSON
- [ ] **HU-F6-05:** Visor de auditoría

### [ ] Fase F7 — Reportes
- [ ] **HU-F7-01:** Servicio común de reportes y regla de totales (Media res = 0.5)
- [ ] **HU-F7-02:** Índices y rendimiento de consultas
- [ ] **HU-F7-03:** Pantalla de filtros común y vista previa
- [ ] **HU-F7-04:** Exportación a PDF con formato original (QuestPDF)
- [ ] **HU-F7-05:** Exportación a Excel e impresión
- [ ] **HU-F7-06:** Reporte de boletas anuladas

### [ ] Fase F8 — Utilitarios
- [ ] **HU-F8-01:** Backup manual consistente (SqliteBackupService)
- [ ] **HU-F8-02:** Backup automático diario con retención
- [ ] **HU-F8-03:** Restauración con validación y reinicio
- [ ] **HU-F8-04:** Importador de datos legado desde CSV (con simulacro)
- [ ] **HU-F8-05:** Lectores opcionales DBF y MDB
- [ ] **HU-F8-06:** Visor de logs

### [ ] Fase F9 — Empaquetado, instalador y puesta en marcha
- [ ] **HU-F9-01:** Publicación self-contained y versionado
- [ ] **HU-F9-02:** Migraciones automáticas al iniciar con backup previo
- [ ] **HU-F9-03:** Instalador Inno Setup
- [ ] **HU-F9-04:** Asistente de primer arranque
- [ ] **HU-F9-05:** Checklist de aceptación en sitio (UAT)
- [ ] **HU-F9-06:** Manual de usuario y guía de balanza

---

## 4. Convenciones Arquitectónicas y de Ingeniería
- **Arquitectura en Capas:**
  - `PesajeFaeneo.Domain`: Sin dependencias externas ni NuGet. Entidades, Enums, Excepciones, Reglas puras.
  - `PesajeFaeneo.Application`: Servicios, Interfaces, DTOs, Seguridad, Validadores. Depende solo de Domain.
  - `PesajeFaeneo.Infrastructure`: EF Core, Balanza, Impresión, Exportación, Archivos, Log. Depende de Application y Domain.
  - `PesajeFaeneo.WinForms`: Formularios, Controles, Presentación. Depende de las capas anteriores vía DI.
  - `PesajeFaeneo.Tests`: Tests unitarios y de integración con xUnit (solo `Assert`).
- **Persistencia:** SQLite vía EF Core 8. Modos WAL y synchronous=NORMAL. Decimales convertidos a REAL para SQLite. Totales calculados en memoria.
- **Seguridad:** Autorización basada en `Permiso` verificado en UI y servicios. Auditoría atómica de cambios.
- **Tiempo:** Siempre `TimeProvider` inyectado (o `FakeTimeProvider` en tests).

---

## 5. Bitácora de Ejecución

### Fase F0 — Fundaciones del proyecto
- **Fecha:** 2026-09-24
- **Estado:** ✅ Completado con éxito (7/7 HUs).
- **Compilación:** `dotnet build` en `Release` con 0 warnings y 0 errores (`TreatWarningsAsErrors=true`).
- **Pruebas:** 17 tests unitarios y de integración ejecutados y en verde (0 fallos, 0 omitidos).
- **Cobertura:** Archivo `coverage.cobertura.xml` generado con éxito vía `build.ps1`.
- **Base de datos:** Migración inicial `Inicial` creada con SQLite, pragmas WAL, índices únicos, checks y conversores REAL para pesos decimales.
- **Siguiente paso:** Fase F1 — Seguridad, usuarios y shell principal.

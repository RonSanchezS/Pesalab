# Sistema de Pesaje para Faeneo

Sistema de control, registro de pesaje y emisión de guías de remisión de carne bovina para faeneo. Desarrollado en .NET 8 (Windows Forms) con SQLite y Entity Framework Core.

## Estructura de la Solución (`src/PesajeFaeneo.sln`)

- **PesajeFaeneo.Domain**: Núcleo de la aplicación. Entidades de negocio, enums, excepciones y reglas puras. Sin dependencias externas ni paquetes NuGet.
- **PesajeFaeneo.Application**: Casos de uso, interfaces, DTOs y validaciones de aplicación.
- **PesajeFaeneo.Infrastructure**: Acceso a datos con EF Core (SQLite), comunicación con balanzas (Serial/TCP/Simulador), generación e impresión de boletas y exportación de reportes.
- **PesajeFaeneo.WinForms**: Capa de presentación WinForms, adaptadores de interfaz de usuario y manejo de navegación.
- **PesajeFaeneo.Tests**: Batería de pruebas unitarias y de integración sobre SQLite en memoria y archivos temporales.

## Compilación y Ejecución

### Prerrequisitos
- .NET 8 SDK (o .NET 10 SDK con soporte para net8.0)
- Herramienta `dotnet-ef` instalada globalmente:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### Restaurar y Compilar
```powershell
dotnet restore src/PesajeFaeneo.sln
dotnet build src/PesajeFaeneo.sln -c Release
```

### Ejecutar Pruebas
```powershell
dotnet test src/PesajeFaeneo.sln --logger console;verbosity=normal
```

O mediante el script automatizado:
```powershell
.\build.ps1
```

### Migraciones con EF Core
Para agregar una nueva migración:
```powershell
dotnet ef migrations add <NombreMigracion> -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms -o Persistencia/Migraciones
```

Para actualizar la base de datos de desarrollo manualmente:
```powershell
dotnet ef database update -p src/PesajeFaeneo.Infrastructure -s src/PesajeFaeneo.WinForms
```

Este comando (igual que la aplicación en producción) crea la base de datos SQLite en
`%ProgramData%\PesajeFaeneo\pesaje.db` (junto con `logs`, `logo`, `backups` y `restore` en la misma
carpeta), no en el directorio del repositorio. `DesignTimeDbContextFactory` crea esa carpeta si no
existe. Para partir de una base de datos limpia en desarrollo, basta con borrar
`%ProgramData%\PesajeFaeneo\pesaje.db*`.

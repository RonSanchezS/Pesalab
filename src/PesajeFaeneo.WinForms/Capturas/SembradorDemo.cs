#if CAPTURAS_MANUAL
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;

namespace PesajeFaeneo.WinForms.Capturas;

/// <summary>
/// Datos de demostración para los manuales (empresa FRIGORÍFICO EJEMPLO S.R.L., todo ficticio). Usa los servicios reales
/// (autenticación, usuarios, catálogos, parámetros, balanza, pesajes y gestión de pesajes) con la sesión de cada rol, de modo
/// que auditoría, historial y numeración quedan coherentes. Solo en el build de capturas.
/// </summary>
internal sealed class SembradorDemo
{
    private const string Marca = "demo-sembrado.txt";

    private readonly IServiceProvider _servicios;
    private readonly AppPaths _paths;
    private readonly ConfigDemo _config;
    private readonly RelojDemo _reloj = RelojDemo.Instancia;

    public SembradorDemo(IServiceProvider servicios, AppPaths paths, ConfigDemo config)
    {
        _servicios = servicios;
        _paths = paths;
        _config = config;
    }

    public static bool YaSembrado(AppPaths paths) => File.Exists(Path.Combine(paths.BaseDir, Marca));

    private sealed record Ganad(string Nombre);
    private sealed record Cli(string Nombre, string Direccion);
    private sealed record Flet(string Nombre, string Celular);

    private static readonly Ganad[] Ganaderos =
    [
        new("HACIENDA SAN MIGUEL"), new("GANADERA EL TRIUNFO S.R.L."), new("JUAN CARLOS SUAREZ ROCA"), new("ESTANCIA LA PAMPA"),
        new("AGROPECUARIA SANTA ROSA"), new("MARIO VACA DIEZ"), new("GANADERIA LOS TAJIBOS"), new("ROBERTO ZABALA PEÑA")
    ];

    private static readonly Cli[] Clientes =
    [
        new("CARNICERIA EL BUEN SABOR", "AV. BANZER 4TO ANILLO NRO. 120"),
        new("SUPERMERCADO LA CENTRAL", "CALLE LIBERTAD NRO. 45"),
        new("DISTRIBUIDORA CARNES DEL ORIENTE", "AV. CRISTO REDENTOR NRO. 310"),
        new("FRIGORIFICO LOS PINOS", "CARRETERA AL NORTE KM 12"),
        new("MERCADO LOS POZOS - PUESTO 12", "CALLE SUAREZ ARANA NRO. 88"),
        new("RESTAURANTE LA PARRILLA CRUCEÑA", "AV. MONSEÑOR RIVERO NRO. 255"),
        new("CARNES SELECTAS HERMANOS ROCA", "CALLE BOLIVAR NRO. 17"),
        new("HOTEL PLAZA SANTA CRUZ", "PLAZA 24 DE SEPTIEMBRE NRO. 2")
    ];

    private static readonly Flet[] Fleteros =
    [
        new("PEDRO MAMANI QUISPE", "70123456"), new("LUIS CHAVEZ MENDOZA", "71234567"), new("RAMIRO GUTIERREZ SALAS", "72345678"),
        new("JOSE ANTONIO RIVERO", "73456789"), new("FELIX CUELLAR PARADA", "74567890")
    ];

    // dias atrás, hora, ganadero, cliente, fletero (-1 = ninguno), tipo, raza, sexo, izquierdo, derecho
    private sealed record Pes(int Dia, int Hora, int Min, int Gan, int Cli, int Flet, TipoDesposte Tipo, string Raza, char Sexo, decimal Izq, decimal Der);

    private static readonly Pes[] Pesajes =
    [
        // hace 5 días
        new(5, 6, 10, 0, 0, 0, TipoDesposte.ResCompleta, "NELORE", 'M', 124.6m, 126.2m),
        new(5, 6, 35, 0, 0, 0, TipoDesposte.ResCompleta, "NELORE", 'M', 119.8m, 121.0m),
        new(5, 7, 5, 1, 1, 1, TipoDesposte.ResCompleta, "CRIOLLO", 'H', 98.4m, 99.6m),
        new(5, 7, 40, 2, 2, 2, TipoDesposte.MediaRes, "CEBU", 'M', 131.2m, 0m),
        new(5, 8, 15, 3, 3, -1, TipoDesposte.ResCompleta, "BRAHMAN", 'M', 140.3m, 138.9m),
        new(5, 8, 50, 3, 3, -1, TipoDesposte.ResCompleta, "BRAHMAN", 'H', 112.7m, 113.5m),
        // hace 4 días
        new(4, 6, 20, 4, 4, 3, TipoDesposte.ResCompleta, "OVERO", 'M', 127.5m, 126.1m),
        new(4, 6, 55, 4, 4, 3, TipoDesposte.ResCompleta, "OVERO", 'H', 105.0m, 104.2m),
        new(4, 7, 30, 5, 5, 4, TipoDesposte.MediaRes, "NELORE", 'M', 0m, 129.8m),
        new(4, 8, 5, 6, 6, 0, TipoDesposte.ResCompleta, "CRIOLLO", 'H', 101.9m, 103.3m),
        new(4, 8, 40, 7, 7, -1, TipoDesposte.ResCompleta, "CEBU", 'M', 133.4m, 135.0m),
        // hace 3 días
        new(3, 6, 15, 1, 1, 1, TipoDesposte.ResCompleta, "NELORE", 'M', 122.1m, 123.7m),
        new(3, 6, 50, 1, 1, 1, TipoDesposte.ResCompleta, "NELORE", 'M', 125.9m, 124.4m),
        new(3, 7, 25, 2, 2, 2, TipoDesposte.ResCompleta, "BRAHMAN", 'H', 109.3m, 110.8m),
        new(3, 8, 0, 5, 5, 4, TipoDesposte.MediaRes, "OVERO", 'H', 115.6m, 0m),
        new(3, 8, 35, 6, 6, 0, TipoDesposte.ResCompleta, "CRIOLLO", 'M', 118.2m, 117.5m),
        new(3, 9, 10, 0, 0, 0, TipoDesposte.ResCompleta, "NELORE", 'M', 129.0m, 128.4m),
        // hace 2 días
        new(2, 6, 25, 3, 3, 3, TipoDesposte.ResCompleta, "BRAHMAN", 'M', 136.8m, 137.7m),
        new(2, 7, 0, 3, 3, 3, TipoDesposte.ResCompleta, "BRAHMAN", 'M', 141.1m, 139.9m),
        new(2, 7, 45, 4, 4, -1, TipoDesposte.MediaRes, "CEBU", 'H', 0m, 108.4m),
        new(2, 8, 20, 7, 7, 2, TipoDesposte.ResCompleta, "NELORE", 'H', 107.5m, 106.9m),
        new(2, 8, 55, 2, 2, 2, TipoDesposte.ResCompleta, "CRIOLLO", 'M', 120.6m, 119.4m),
        // ayer
        new(1, 6, 10, 0, 0, 1, TipoDesposte.ResCompleta, "NELORE", 'M', 123.3m, 124.0m),
        new(1, 6, 45, 0, 0, 1, TipoDesposte.ResCompleta, "NELORE", 'M', 121.7m, 122.5m),
        new(1, 7, 20, 1, 1, 4, TipoDesposte.ResCompleta, "OVERO", 'H', 99.9m, 100.7m),
        new(1, 7, 55, 5, 5, -1, TipoDesposte.MediaRes, "BRAHMAN", 'M', 138.2m, 0m),
        new(1, 8, 30, 6, 6, 0, TipoDesposte.ResCompleta, "CEBU", 'M', 130.5m, 131.8m),
        new(1, 9, 5, 7, 7, 3, TipoDesposte.ResCompleta, "CRIOLLO", 'H', 102.4m, 101.6m),
        new(1, 9, 40, 2, 2, 3, TipoDesposte.ResCompleta, "NELORE", 'M', 126.0m, 127.2m),
        // hoy
        new(0, 6, 5, 3, 3, 3, TipoDesposte.ResCompleta, "BRAHMAN", 'M', 137.5m, 136.3m),
        new(0, 6, 32, 3, 3, 3, TipoDesposte.ResCompleta, "BRAHMAN", 'H', 114.8m, 115.9m),
        new(0, 7, 10, 4, 4, -1, TipoDesposte.ResCompleta, "OVERO", 'M', 128.9m, 129.6m),
        new(0, 7, 48, 1, 1, 1, TipoDesposte.MediaRes, "CRIOLLO", 'H', 0m, 104.7m),
        new(0, 8, 15, 6, 6, 0, TipoDesposte.ResCompleta, "CEBU", 'M', 132.0m, 133.1m),
    ];

    /// <summary>Anulada (hace 1 día, boleta 8) y editada (hace 2 días, boleta 5): índices en la lista cronológica de pesajes.</summary>
    private const int IndiceAnulado = 23;
    private const int IndiceAnuladoDos = 12;
    private const int IndiceEditado = 20;

    public async Task SembrarAsync()
    {
        _reloj.Desplazamiento = TimeSpan.Zero;
        var sesion = _servicios.GetRequiredService<ISesionActual>();

        // 1. Administrador (el que siembra el sistema: admin/admin con cambio obligatorio).
        await IniciarAsync("admin", "admin");
        await Con<IServicioAutenticacion>(a => a.CambiarPasswordPropiaAsync("admin", _config.PasswordAdmin, _config.PasswordAdmin));
        await Con<IServicioUsuarios>(async u =>
        {
            var admin = (await u.ListarAsync(true)).First(x => x.NombreUsuario == "admin");
            await u.EditarAsync(new EditarUsuario(admin.Id, _config.NombreCompletoAdmin, Rol.Administrador));
        });

        // 2. Empresa y logo.
        var logo = Path.Combine(_paths.BaseDir, "demo-logo.png");
        GenerarLogo(logo);
        await Con<IServicioParametros>(p => p.ActualizarAsync(new ActualizarParametros(
            "FRIGORÍFICO EJEMPLO S.R.L.",
            "AV. EJEMPLO NRO. 100",
            "(3) 555-0100",
            "SANTA CRUZ",
            "SENASAG-0000",
            "(3) 555-0101 / 700-00000",
            "SIN OBSERVACIONES",
            2401,
            AccionLogo.Reemplazar,
            logo)));

        // 2b. Tipo de pesaje (F11): la demostración mezcla res completa y media res.
        await Con<IServicioParametros>(p => p.CambiarModoDesposteAsync(ModoDesposte.Ambos));

        // 3. Balanza: serie COM3 9600 8N1 (el lector de demostración atiende esa conexión).
        await Con<IServicioConfiguracionBalanza>(async b =>
        {
            var actual = (await b.ObtenerAsync()).Configuracion;
            await b.ActualizarAsync(actual with
            {
                TipoConexion = TipoConexion.Serial,
                PuertoCom = "COM3",
                BaudRate = 9600,
                DataBits = 8,
                Paridad = ParidadSerial.None,
                BitsParada = BitsParada.One
            });
        });

        // 4. Usuarios.
        await Con<IServicioUsuarios>(async u =>
        {
            foreach (var d in _config.Usuarios)
            {
                await u.CrearAsync(new CrearUsuario(d.NombreUsuario, d.NombreCompleto, d.Rol, d.Password, d.Password));
            }

            // Un usuario inactivo (se ve al marcar "Mostrar usuarios inactivos").
            var inactivo = await u.CrearAsync(new CrearUsuario("pedro.salas", "PEDRO SALAS", Rol.Operador, "Inactivo-Demo-5512", "Inactivo-Demo-5512"));
            await u.CambiarEstadoAsync(inactivo, false);
        });

        // 5. Catálogos (los códigos los asigna el sistema).
        var idsGanaderos = new List<int>();
        var idsClientes = new List<int>();
        var idsFleteros = new List<int>();
        await Con<IServicioGanaderos>(async s =>
        {
            foreach (var g in Ganaderos) { idsGanaderos.Add(await s.CrearAsync(new CrearGanadero(g.Nombre))); }
            // Un ganadero, un cliente y un fletero inactivos (para mostrar "Mostrar inactivos" y el boton Activar).
            await s.CambiarEstadoAsync(await s.CrearAsync(new CrearGanadero("GANADERIA EL OLVIDO")), false);
        });
        await Con<IServicioClientes>(async s =>
        {
            foreach (var c in Clientes) { idsClientes.Add(await s.CrearAsync(new CrearCliente(c.Nombre, c.Direccion))); }
            await s.CambiarEstadoAsync(await s.CrearAsync(new CrearCliente("CARNICERIA CERRADA", "CALLE SIN SALIDA NRO. 1")), false);
        });
        await Con<IServicioFleteros>(async s =>
        {
            foreach (var f in Fleteros) { idsFleteros.Add(await s.CrearAsync(new CrearFletero(f.Nombre, f.Celular))); }
            await s.CambiarEstadoAsync(await s.CrearAsync(new CrearFletero("MARIO ORTIZ", "75678901")), false);
        });
        await _servicios.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();

        // 6. Los usuarios de prueba cambian su contraseña de inicio (salvo el que debe quedar con el cambio pendiente).
        foreach (var d in _config.Usuarios.Where(x => !x.DebeCambiar))
        {
            await IniciarAsync(d.NombreUsuario, d.Password);
            await _servicios.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();
        }

        if (_config.SinPesajes)
        {
            // Datos sin ningún pesaje (la primera boleta aún no se emitió): se cierra la sesión y se termina.
            await _servicios.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();
            File.WriteAllText(Path.Combine(_paths.BaseDir, Marca), "Datos de demostracion sin pesajes.");
            return;
        }

        // 7. Pesajes con la sesión del operador (la fecha la fija el reloj de demostración).
        var operador = _config.Operador;
        await IniciarAsync(operador.NombreUsuario, operador.Password);
        var registrados = new List<(int Id, Pes Datos)>();
        var secuencias = new Dictionary<(int Dia, int Gan), int>();
        await Con<IServicioPesajes>(async s =>
        {
            foreach (var p in Pesajes)
            {
                _reloj.Desplazamiento = Desplazamiento(p.Dia, p.Hora, p.Min);
                secuencias[(p.Dia, p.Gan)] = secuencias.GetValueOrDefault((p.Dia, p.Gan)) + 1;
                var r = await s.RegistrarAsync(new RegistrarPesaje(
                    idsGanaderos[p.Gan],
                    idsClientes[p.Cli],
                    Clientes[p.Cli].Direccion,
                    secuencias[(p.Dia, p.Gan)],
                    p.Raza,
                    p.Sexo,
                    p.Tipo,
                    p.Izq,
                    p.Der,
                    "SIN OBSERVACIONES",
                    CapturadoConSimulador: false,
                    FleteroId: p.Flet >= 0 ? idsFleteros[p.Flet] : null));
                registrados.Add((r.Id, p));
            }
        });
        _reloj.Desplazamiento = TimeSpan.Zero;
        await _servicios.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();

        // 8. Una boleta editada y dos anuladas (con la sesión del supervisor, que puede hacerlo).
        var supervisor = _config.Supervisor;
        await IniciarAsync(supervisor.NombreUsuario, supervisor.Password);
        await Con<IServicioGestionPesajes>(async g =>
        {
            var editado = registrados[IndiceEditado];
            _reloj.Desplazamiento = Desplazamiento(editado.Datos.Dia, 10, 15);
            var paraEditar = await g.ObtenerParaEdicionAsync(editado.Id);
            await g.EditarAsync(new EditarPesaje(
                paraEditar.Id, paraEditar.Version, paraEditar.GanaderoId, paraEditar.ClienteId, paraEditar.Direccion,
                paraEditar.Secuencia, "NELORE CRUZADO", paraEditar.Sexo, paraEditar.Observacion, "Corrección de raza", paraEditar.FleteroId));

            foreach (var (indice, motivo) in new[] { (IndiceAnulado, "Pesaje duplicado por error de captura"), (IndiceAnuladoDos, "Cliente equivocado en la boleta") })
            {
                var an = registrados[indice];
                _reloj.Desplazamiento = Desplazamiento(an.Datos.Dia, 10, 30);
                var paraAnular = await g.ObtenerParaEdicionAsync(an.Id);
                await g.AnularAsync(new AnularPesaje(paraAnular.Id, paraAnular.Version, motivo));
            }
        });
        _reloj.Desplazamiento = TimeSpan.Zero;
        await _servicios.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();

        // 9. Un pesaje con peso simulado (solo el administrador puede grabarlo) y dos copias de seguridad manuales de días anteriores.
        await IniciarAsync("admin", _config.PasswordAdmin);
        await Con<IServicioPesajes>(async s =>
        {
            _reloj.Desplazamiento = Desplazamiento(1, 11, 15);
            await s.RegistrarAsync(new RegistrarPesaje(
                idsGanaderos[6], idsClientes[6], Clientes[6].Direccion, 2, "BRAHMAN", 'M', TipoDesposte.ResCompleta,
                118.0m, 119.4m, "PRUEBA CON EL SIMULADOR", CapturadoConSimulador: true, FleteroId: null));
        });
        await Con<IServicioBackup>(async b =>
        {
            _reloj.Desplazamiento = Desplazamiento(3, 18, 5);
            await b.CrearManualAsync();
            _reloj.Desplazamiento = Desplazamiento(1, 18, 20);
            await b.CrearManualAsync();
        });
        _reloj.Desplazamiento = TimeSpan.Zero;
        await _servicios.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync();

        // 10. El usuario con cambio de contraseña pendiente ya existe (se crea con DebeCambiarPassword = true).
        File.WriteAllText(Path.Combine(_paths.BaseDir, Marca), "Datos de demostración para los manuales.");
        _ = sesion;
    }

    private TimeSpan Desplazamiento(int diasAtras, int hora, int minuto)
    {
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.Local).Date;
        var local = hoy.AddDays(-diasAtras).AddHours(hora).AddMinutes(minuto);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        return utc - DateTime.UtcNow;
    }

    private async Task IniciarAsync(string usuario, string password)
    {
        var r = await _servicios.GetRequiredService<IServicioAutenticacion>().IniciarSesionAsync(usuario, password);
        if (r.Tipo != ResultadoLoginTipo.Exitoso)
        {
            throw new InvalidOperationException($"No se pudo iniciar sesión como '{usuario}' para sembrar los datos: {r.Mensaje}");
        }

        // Los usuarios nuevos nacen con cambio obligatorio: se cambia por la misma contraseña no es posible, así que
        // se limpia con un cambio a una contraseña distinta y se vuelve a la de la configuración.
        if (r.DebeCambiarPassword && usuario != "admin")
        {
            var temporal = password + "#2";
            var auth = _servicios.GetRequiredService<IServicioAutenticacion>();
            await auth.CambiarPasswordPropiaAsync(password, temporal, temporal);
            await auth.CambiarPasswordPropiaAsync(temporal, password, password);
        }
    }

    private async Task Con<T>(Func<T, Task> accion) where T : notnull
    {
        using var scope = _servicios.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<T>());
    }

    private static void GenerarLogo(string ruta)
    {
        using var bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        using var camino = new GraphicsPath();
        const int radio = 56;
        var r = new Rectangle(8, 8, 240, 240);
        camino.AddArc(r.X, r.Y, radio, radio, 180, 90);
        camino.AddArc(r.Right - radio, r.Y, radio, radio, 270, 90);
        camino.AddArc(r.Right - radio, r.Bottom - radio, radio, radio, 0, 90);
        camino.AddArc(r.X, r.Bottom - radio, radio, radio, 90, 90);
        camino.CloseFigure();

        using var pincel = new LinearGradientBrush(r, Color.FromArgb(0x1F, 0x6F, 0x8B), Color.FromArgb(0x14, 0x3D, 0x59), 90f);
        g.FillPath(pincel, camino);

        using var fuente = new Font("Segoe UI", 88, FontStyle.Bold, GraphicsUnit.Pixel);
        using var formato = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("FE", fuente, Brushes.White, new RectangleF(0, 4, 256, 256), formato);
        bmp.Save(ruta, ImageFormat.Png);
    }
}
#endif

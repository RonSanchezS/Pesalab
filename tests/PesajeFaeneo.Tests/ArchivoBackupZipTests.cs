namespace PesajeFaeneo.Tests;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using Xunit;

public class ArchivoBackupZipTests : IDisposable
{
    private readonly string _dirTemporal;

    public ArchivoBackupZipTests()
    {
        _dirTemporal = Path.Combine(Path.GetTempPath(), $"zip_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dirTemporal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dirTemporal))
        {
            try { Directory.Delete(_dirTemporal, true); } catch { }
        }
    }

    [Fact]
    public void LeerManifiesto_DevuelveNullParaArchivosInvalidos()
    {
        // 1. Archivo de texto
        var rutaTexto = Path.Combine(_dirTemporal, "texto.zip");
        File.WriteAllText(rutaTexto, "no es zip");
        Assert.Null(ArchivoBackupZip.LeerManifiesto(rutaTexto));

        // 2. Zip sin manifiesto
        var rutaSinManifiesto = Path.Combine(_dirTemporal, "sin_manifest.zip");
        using (var zip = ZipFile.Open(rutaSinManifiesto, ZipArchiveMode.Create))
        {
            var e = zip.CreateEntry("otro.txt");
            using var s = e.Open();
            s.WriteByte(1);
        }
        Assert.Null(ArchivoBackupZip.LeerManifiesto(rutaSinManifiesto));

        // 3. Manifiesto con Formato = 2
        var rutaFormato2 = Path.Combine(_dirTemporal, "formato2.zip");
        var m2 = CrearManifiestoValido(formato: 2);
        EscribirZipConManifiesto(rutaFormato2, JsonSerializer.SerializeToUtf8Bytes(m2, JsonBackup.Opciones));
        Assert.Null(ArchivoBackupZip.LeerManifiesto(rutaFormato2));

        // 4. JSON roto
        var rutaJsonRoto = Path.Combine(_dirTemporal, "json_roto.zip");
        EscribirZipConManifiesto(rutaJsonRoto, "{ roto: "u8.ToArray());
        Assert.Null(ArchivoBackupZip.LeerManifiesto(rutaJsonRoto));
    }

    [Theory]
    [InlineData("../x.txt", "contiene rutas no permitidas")]
    [InlineData(@"logo\x.png", "contiene rutas no permitidas")]
    [InlineData("/abs.txt", "contiene rutas no permitidas")]
    [InlineData("logo/sub/x.png", "contiene un logo no válido")]
    [InlineData("logo/x.exe", "contiene un logo no válido")]
    [InlineData("otro.txt", "contiene archivos que no son de este sistema")]
    public void ExtraerValidado_RechazaEntradasInvalidas(string entradaInvalida, string parteMensaje)
    {
        var rutaZip = Path.Combine(_dirTemporal, $"invalido_{Guid.NewGuid():N}.zip");
        using (var zip = ZipFile.Open(rutaZip, ZipArchiveMode.Create))
        {
            zip.CreateEntry(ReglasBackup.EntradaManifiesto);
            zip.CreateEntry(ReglasBackup.EntradaBaseDatos);
            zip.CreateEntry(entradaInvalida);
        }

        var destino = Path.Combine(_dirTemporal, "destino");
        var ex = Assert.Throws<ValidacionException>(() => ArchivoBackupZip.ExtraerValidado(rutaZip, destino));
        Assert.StartsWith("La copia de seguridad no es válida:", ex.Errores[0].Mensaje);
        Assert.Contains(parteMensaje, ex.Errores[0].Mensaje);
    }

    [Fact]
    public void ExtraerValidado_RechazaSinBaseDatosODemasiadosLogosORepetidos()
    {
        var destino = Path.Combine(_dirTemporal, "destino");

        // Sin pesaje.db
        var rutaSinDb = Path.Combine(_dirTemporal, "sin_db.zip");
        using (var zip = ZipFile.Open(rutaSinDb, ZipArchiveMode.Create))
        {
            zip.CreateEntry(ReglasBackup.EntradaManifiesto);
        }
        var ex1 = Assert.Throws<ValidacionException>(() => ArchivoBackupZip.ExtraerValidado(rutaSinDb, destino));
        Assert.Contains("le falta el manifiesto o la base de datos", ex1.Errores[0].Mensaje);

        // Entrada repetida
        var rutaRepetida = Path.Combine(_dirTemporal, "repetida.zip");
        using (var zip = ZipFile.Open(rutaRepetida, ZipArchiveMode.Create))
        {
            zip.CreateEntry(ReglasBackup.EntradaManifiesto);
            zip.CreateEntry(ReglasBackup.EntradaBaseDatos);
            zip.CreateEntry(ReglasBackup.EntradaBaseDatos);
        }
        var ex2 = Assert.Throws<ValidacionException>(() => ArchivoBackupZip.ExtraerValidado(rutaRepetida, destino));
        Assert.Contains("tiene entradas repetidas", ex2.Errores[0].Mensaje);

        // 21 logos
        var ruta21Logos = Path.Combine(_dirTemporal, "logos21.zip");
        using (var zip = ZipFile.Open(ruta21Logos, ZipArchiveMode.Create))
        {
            zip.CreateEntry(ReglasBackup.EntradaManifiesto);
            zip.CreateEntry(ReglasBackup.EntradaBaseDatos);
            for (int i = 1; i <= 21; i++)
            {
                zip.CreateEntry($"logo/logo{i}.png");
            }
        }
        var ex3 = Assert.Throws<ValidacionException>(() => ArchivoBackupZip.ExtraerValidado(ruta21Logos, destino));
        Assert.Contains("contiene demasiados logos", ex3.Errores[0].Mensaje);
    }

    [Fact]
    public void ExtraerValidado_ConShaAlterado_FallaYConValido_ExtraeTodo()
    {
        var rutaDb = Path.Combine(_dirTemporal, "pesaje.db");
        var dbBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        File.WriteAllBytes(rutaDb, dbBytes);
        var shaReal = Convert.ToHexString(SHA256.HashData(dbBytes));

        var rutaLogo = Path.Combine(_dirTemporal, "test.png");
        var logoBytes = new byte[] { 137, 80, 78, 71 };
        File.WriteAllBytes(rutaLogo, logoBytes);

        // 1. Con SHA alterado
        var mAlterado = CrearManifiestoValido(sha: "0000000000000000000000000000000000000000000000000000000000000000", tamanoDb: dbBytes.Length);
        var rutaZipAlterado = Path.Combine(_dirTemporal, "alterado.zip");
        ArchivoBackupZip.Escribir(rutaZipAlterado, rutaDb, mAlterado, new[] { rutaLogo });

        var destino1 = Path.Combine(_dirTemporal, "destino1");
        var ex = Assert.Throws<ValidacionException>(() => ArchivoBackupZip.ExtraerValidado(rutaZipAlterado, destino1));
        Assert.Contains("no coincide con su manifiesto", ex.Errores[0].Mensaje);

        // 2. Con SHA válido
        var mValido = CrearManifiestoValido(sha: shaReal, tamanoDb: dbBytes.Length, logos: new[] { "test.png" });
        var rutaZipValido = Path.Combine(_dirTemporal, "valido.zip");
        ArchivoBackupZip.Escribir(rutaZipValido, rutaDb, mValido, new[] { rutaLogo });

        var destino2 = Path.Combine(_dirTemporal, "destino2");
        var mExtraido = ArchivoBackupZip.ExtraerValidado(rutaZipValido, destino2);

        Assert.Equal(mValido.Sha256BaseDatos, mExtraido.Sha256BaseDatos);
        Assert.True(File.Exists(Path.Combine(destino2, "pesaje.db")));
        Assert.True(File.Exists(Path.Combine(destino2, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(destino2, "logo", "test.png")));
        Assert.Equal(dbBytes, File.ReadAllBytes(Path.Combine(destino2, "pesaje.db")));
        Assert.Equal(logoBytes, File.ReadAllBytes(Path.Combine(destino2, "logo", "test.png")));
    }

    private static ManifiestoBackup CrearManifiestoValido(
        int formato = 1,
        string sha = "ABCD",
        long tamanoDb = 10,
        IReadOnlyList<string>? logos = null)
    {
        return new ManifiestoBackup(
            Formato: formato,
            Aplicacion: "PesajeFaeneo",
            VersionAplicacion: "1.0.0",
            Equipo: "TEST-PC",
            Tipo: TipoBackup.Manual,
            CreadoEnUtc: DateTime.UtcNow,
            UltimaMigracion: "20260930142130_F6_VersionPesaje",
            Migraciones: new[] { "20260930142130_F6_VersionPesaje" },
            Sha256BaseDatos: sha,
            TamanoBaseDatos: tamanoDb,
            Pesajes: 0,
            Ganaderos: 0,
            Clientes: 0,
            Usuarios: 1,
            UltimoNumeroBoleta: null,
            Logos: logos ?? Array.Empty<string>());
    }

    private static void EscribirZipConManifiesto(string rutaZip, byte[] jsonBytes)
    {
        using var zip = ZipFile.Open(rutaZip, ZipArchiveMode.Create);
        var e = zip.CreateEntry(ReglasBackup.EntradaManifiesto);
        using var s = e.Open();
        s.Write(jsonBytes);
    }
}

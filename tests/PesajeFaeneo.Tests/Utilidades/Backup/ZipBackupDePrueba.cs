namespace PesajeFaeneo.Tests.Utilidades.Backup;

using System.IO.Compression;
using System.Security.Cryptography;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;

internal static class ZipBackupDePrueba
{
    public static string Crear(
        string carpeta,
        string rutaBaseDatos,
        Func<ManifiestoBackup, ManifiestoBackup>? modificar = null,
        IEnumerable<(string Entrada, byte[] Contenido)>? extras = null)
    {
        var rutaZip = Path.Combine(carpeta, $"backup_prueba_{Guid.NewGuid():N}.zip");
        var dbBytes = File.ReadAllBytes(rutaBaseDatos);
        var sha = Convert.ToHexString(SHA256.HashData(dbBytes));

        var manifiesto = new ManifiestoBackup(
            Formato: ReglasBackup.FormatoManifiesto,
            Aplicacion: ReglasBackup.NombreAplicacion,
            VersionAplicacion: "1.0.0",
            Equipo: "EQUIPO-TEST",
            Tipo: TipoBackup.Manual,
            CreadoEnUtc: DateTime.UtcNow,
            UltimaMigracion: "20260930142130_F6_VersionPesaje",
            Migraciones: new[] { "20260930142130_F6_VersionPesaje" },
            Sha256BaseDatos: sha,
            TamanoBaseDatos: dbBytes.Length,
            Pesajes: 0,
            Ganaderos: 0,
            Clientes: 0,
            Usuarios: 1,
            UltimoNumeroBoleta: null,
            Logos: Array.Empty<string>());

        if (modificar is not null)
        {
            manifiesto = modificar(manifiesto);
        }

        ArchivoBackupZip.Escribir(rutaZip, rutaBaseDatos, manifiesto, Array.Empty<string>());

        if (extras is not null)
        {
            using var zip = ZipFile.Open(rutaZip, ZipArchiveMode.Update);
            foreach (var (entrada, contenido) in extras)
            {
                var e = zip.CreateEntry(entrada);
                using var s = e.Open();
                s.Write(contenido);
            }
        }

        return rutaZip;
    }
}

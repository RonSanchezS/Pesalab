namespace PesajeFaeneo.Infrastructure.Utilitarios.Backup;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Domain.Excepciones;

internal static class ArchivoBackupZip
{
    public static void Escribir(string rutaZip, string rutaBaseDatos, ManifiestoBackup manifiesto, IReadOnlyList<string> rutasLogo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaBaseDatos);
        ArgumentNullException.ThrowIfNull(manifiesto);
        ArgumentNullException.ThrowIfNull(rutasLogo);

        using var zipStream = new FileStream(rutaZip, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // 1. manifest.json primero (UTF-8 sin BOM)
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(manifiesto, JsonBackup.Opciones);
        var entryManifiesto = zip.CreateEntry(ReglasBackup.EntradaManifiesto, CompressionLevel.Optimal);
        using (var ms = entryManifiesto.Open())
        {
            ms.Write(jsonBytes);
        }

        // 2. pesaje.db
        zip.CreateEntryFromFile(rutaBaseDatos, ReglasBackup.EntradaBaseDatos, CompressionLevel.Optimal);

        // 3. logos
        foreach (var rutaLogo in rutasLogo)
        {
            var nombre = Path.GetFileName(rutaLogo);
            zip.CreateEntryFromFile(rutaLogo, ReglasBackup.PrefijoLogo + nombre, CompressionLevel.Optimal);
        }
    }

    public static ManifiestoBackup? LeerManifiesto(string rutaZip)
    {
        try
        {
            using var zip = ZipFile.OpenRead(rutaZip);
            var entry = zip.GetEntry(ReglasBackup.EntradaManifiesto);
            if (entry is null || entry.Length > ReglasBackup.TamanoMaximoManifiesto)
            {
                return null;
            }

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var manifiesto = JsonSerializer.Deserialize<ManifiestoBackup>(ms.ToArray(), JsonBackup.Opciones);
            if (manifiesto is null || manifiesto.Formato != ReglasBackup.FormatoManifiesto || manifiesto.Aplicacion != ReglasBackup.NombreAplicacion)
            {
                return null;
            }

            return manifiesto;
        }
        catch
        {
            return null;
        }
    }

    public static ManifiestoBackup ExtraerValidado(string rutaZip, string carpetaDestino)
    {
        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(rutaZip);
        }
        catch (InvalidDataException)
        {
            throw NoValida("no es un archivo .zip");
        }

        using (zip)
        {
            var vistas = new HashSet<string>(StringComparer.Ordinal);
            var logos = new List<ZipArchiveEntry>();

            foreach (var e in zip.Entries)
            {
                var nombre = e.FullName;
                if (nombre.Contains('\\') || nombre.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(nombre) || nombre.StartsWith('/'))
                {
                    throw NoValida("contiene rutas no permitidas");
                }

                if (nombre == ReglasBackup.EntradaManifiesto || nombre == ReglasBackup.EntradaBaseDatos)
                {
                    if (!vistas.Add(nombre))
                    {
                        throw NoValida("tiene entradas repetidas");
                    }
                    continue;
                }

                if (nombre == ReglasBackup.PrefijoLogo)
                {
                    continue; // entrada de carpeta
                }

                if (nombre.StartsWith(ReglasBackup.PrefijoLogo, StringComparison.Ordinal))
                {
                    var archivo = nombre[ReglasBackup.PrefijoLogo.Length..];
                    if (archivo.Length == 0 || archivo.Contains('/') || !ReglasBackup.ExtensionesLogo.Contains(Path.GetExtension(archivo))
                        || e.Length > ReglasBackup.TamanoMaximoLogo || !vistas.Add(nombre))
                    {
                        throw NoValida("contiene un logo no válido");
                    }
                    logos.Add(e);
                    continue;
                }

                throw NoValida("contiene archivos que no son de este sistema");
            }

            if (!vistas.Contains(ReglasBackup.EntradaManifiesto) || !vistas.Contains(ReglasBackup.EntradaBaseDatos))
            {
                throw NoValida("le falta el manifiesto o la base de datos");
            }

            if (logos.Count > ReglasBackup.MaximoArchivosLogo)
            {
                throw NoValida("contiene demasiados logos");
            }

            // 3. Manifiesto
            var entryManifiesto = zip.GetEntry(ReglasBackup.EntradaManifiesto)!;
            if (entryManifiesto.Length > ReglasBackup.TamanoMaximoManifiesto)
            {
                throw NoValida("el manifiesto está dañado o no es de este sistema");
            }

            ManifiestoBackup manifiesto;
            try
            {
                using var s = entryManifiesto.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                var m = JsonSerializer.Deserialize<ManifiestoBackup>(ms.ToArray(), JsonBackup.Opciones);
                if (m is null || m.Formato != ReglasBackup.FormatoManifiesto || m.Aplicacion != ReglasBackup.NombreAplicacion)
                {
                    throw NoValida("el manifiesto está dañado o no es de este sistema");
                }
                manifiesto = m;
            }
            catch (JsonException)
            {
                throw NoValida("el manifiesto está dañado o no es de este sistema");
            }

            // 4. pesaje.db
            var entryDb = zip.GetEntry(ReglasBackup.EntradaBaseDatos)!;
            if (entryDb.Length > ReglasBackup.TamanoMaximoBackup)
            {
                throw NoValida("es demasiado grande");
            }

            Directory.CreateDirectory(carpetaDestino);
            var rutaDbDestino = Path.Combine(carpetaDestino, ReglasBackup.EntradaBaseDatos);
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            try
            {
                CopiarConLimite(entryDb, rutaDbDestino, sha256);
            }
            catch (InvalidDataException)
            {
                throw NoValida("está dañada");
            }

            var hashCalculado = Convert.ToHexString(sha256.GetHashAndReset());
            if (!string.Equals(hashCalculado, manifiesto.Sha256BaseDatos, StringComparison.OrdinalIgnoreCase))
            {
                throw NoValida("la base de datos no coincide con su manifiesto (archivo alterado o dañado)");
            }

            // 5. Logos
            var carpetaLogo = Path.Combine(carpetaDestino, "logo");
            if (logos.Count > 0)
            {
                Directory.CreateDirectory(carpetaLogo);
                foreach (var l in logos)
                {
                    var archivo = l.FullName[ReglasBackup.PrefijoLogo.Length..];
                    var destino = Path.GetFullPath(Path.Combine(carpetaLogo, Path.GetFileName(archivo)));
                    if (!destino.StartsWith(Path.GetFullPath(carpetaLogo) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        throw NoValida("contiene rutas no permitidas");
                    }
                    try
                    {
                        CopiarConLimite(l, destino, null);
                    }
                    catch (InvalidDataException)
                    {
                        throw NoValida("está dañada");
                    }
                }
            }

            // 6. Escribir también carpetaDestino/manifest.json
            var rutaManifiestoDestino = Path.Combine(carpetaDestino, ReglasBackup.EntradaManifiesto);
            File.WriteAllBytes(rutaManifiestoDestino, JsonSerializer.SerializeToUtf8Bytes(manifiesto, JsonBackup.Opciones));

            return manifiesto;
        }
    }

    private static void CopiarConLimite(ZipArchiveEntry entrada, string destino, IncrementalHash? hash)
    {
        using var origen = entrada.Open();
        using var salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int leidos;
        while ((leidos = origen.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += leidos;
            if (total > entrada.Length)
            {
                throw NoValida("está dañada (tamaño inesperado)");
            }
            hash?.AppendData(buffer, 0, leidos);
            salida.Write(buffer, 0, leidos);
        }
        if (total != entrada.Length)
        {
            throw NoValida("está dañada (tamaño inesperado)");
        }
    }

    private static ValidacionException NoValida(string detalle) =>
        new(CamposBackup.Archivo, $"La copia de seguridad no es válida: {detalle}.");
}

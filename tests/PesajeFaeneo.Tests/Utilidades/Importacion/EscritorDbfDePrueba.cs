using System.Buffers.Binary;
using System.Text;

namespace PesajeFaeneo.Tests.Utilidades.Importacion;

internal static class EscritorDbfDePrueba
{
    public static byte[] Crear(
        byte idioma,
        IReadOnlyList<(string Nombre, char Tipo, int Longitud, int Decimales)> campos,
        IEnumerable<(bool Borrado, string Crudo)> registros,
        Encoding codificacion,
        int? cantidadDeclarada = null)
    {
        var regList = registros.ToList();
        int totalRegistros = cantidadDeclarada ?? regList.Count;
        int largoCabecera = 32 + (32 * campos.Count) + 1;
        int largoRegistro = 1 + campos.Sum(c => c.Longitud);

        using var ms = new MemoryStream();

        // Cabecera de 32 bytes
        var cabecera = new byte[32];
        cabecera[0] = 0x03;
        cabecera[1] = 126; // Año 2026 - 1900
        cabecera[2] = 10;  // Mes 10
        cabecera[3] = 1;   // Día 1
        BinaryPrimitives.WriteUInt32LittleEndian(cabecera.AsSpan(4, 4), (uint)totalRegistros);
        BinaryPrimitives.WriteUInt16LittleEndian(cabecera.AsSpan(8, 2), (ushort)largoCabecera);
        BinaryPrimitives.WriteUInt16LittleEndian(cabecera.AsSpan(10, 2), (ushort)largoRegistro);
        cabecera[29] = idioma;

        ms.Write(cabecera, 0, 32);

        // Descriptores de campos (32 bytes cada uno)
        foreach (var campo in campos)
        {
            var desc = new byte[32];
            var nombreBytes = Encoding.ASCII.GetBytes(campo.Nombre);
            int lenNombre = Math.Min(nombreBytes.Length, 11);
            Array.Copy(nombreBytes, desc, lenNombre);

            desc[11] = (byte)char.ToUpperInvariant(campo.Tipo);
            desc[16] = (byte)(campo.Longitud & 0xFF);
            desc[17] = campo.Tipo == 'C' ? (byte)(campo.Longitud >> 8) : (byte)campo.Decimales;

            ms.Write(desc, 0, 32);
        }

        // Fin de descriptores
        ms.WriteByte(0x0D);

        // Registros
        foreach (var (borrado, crudo) in regList)
        {
            ms.WriteByte(borrado ? (byte)'*' : (byte)' ');
            var bytesCrudos = codificacion.GetBytes(crudo);
            ms.Write(bytesCrudos, 0, bytesCrudos.Length);
        }

        // Fin de archivo
        ms.WriteByte(0x1A);

        return ms.ToArray();
    }
}

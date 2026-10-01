using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

internal sealed record ColumnaDbf(string Nombre, char Tipo, int Longitud, int Decimales, int Desplazamiento, bool Soportada);

/// <summary>Lector de solo avance de tablas dBase III/IV, FoxPro y Visual FoxPro (sin memos).</summary>
internal sealed class LectorDbf : IDisposable
{
    private const int TamanoCabecera = 32;
    private const int TamanoDescriptor = 32;
    private const byte FinDescriptores = 0x0D;
    private const byte FinArchivo = 0x1A;
    private const int DiaJulianoEpoch = 2440588; // 1970-01-01

    private static readonly IReadOnlyDictionary<byte, int> PaginasPorDriver = new Dictionary<byte, int>
    {
        [0x01] = 437, [0x02] = 850, [0x03] = 1252, [0x57] = 1252, [0x58] = 1252, [0x59] = 1252,
        [0x64] = 852, [0x65] = 866, [0xC8] = 1250, [0xC9] = 1251
    };

    private static readonly HashSet<byte> VersionesConocidas = new()
    {
        0x02, 0x03, 0x04, 0x05, 0x30, 0x31, 0x32, 0x43, 0x63, 0x83, 0x8B, 0x8E, 0xCB, 0xF5, 0xFB
    };

    private readonly Stream _stream;
    private readonly Encoding _codificacion;
    private readonly byte[] _registro;
    private int _leidos;

    public IReadOnlyList<ColumnaDbf> Columnas { get; }
    public int CantidadDeclarada { get; }
    public int Eliminados { get; private set; }
    public int PaginaDeCodigos { get; }
    public int NumeroRegistroActual => _leidos;

    static LectorDbf() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public LectorDbf(Stream stream, int? paginaForzada, int paginaPorDefecto = 1252)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        var cab = LeerExacto(TamanoCabecera) ?? throw Invalido("el archivo es demasiado corto");
        if (!VersionesConocidas.Contains(cab[0])) throw Invalido("no es una tabla dBase/FoxPro reconocida");
        CantidadDeclarada = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(cab.AsSpan(4, 4)));
        int largoCabecera = BinaryPrimitives.ReadUInt16LittleEndian(cab.AsSpan(8, 2));
        int largoRegistro = BinaryPrimitives.ReadUInt16LittleEndian(cab.AsSpan(10, 2));
        if (largoCabecera < TamanoCabecera + TamanoDescriptor + 1 || largoRegistro < 2)
            throw Invalido("cabecera no válida");

        PaginaDeCodigos = paginaForzada ?? (PaginasPorDriver.TryGetValue(cab[29], out var p) ? p : paginaPorDefecto);
        _codificacion = Encoding.GetEncoding(PaginaDeCodigos);

        var columnas = new List<ColumnaDbf>();
        int desplazamiento = 1; // byte 0 = marca de borrado
        int consumidos = TamanoCabecera;
        while (true)
        {
            var primero = LeerExacto(1) ?? throw Invalido("faltan los descriptores de campo");
            consumidos++;
            if (primero[0] == FinDescriptores) break;
            var resto = LeerExacto(TamanoDescriptor - 1) ?? throw Invalido("descriptor de campo incompleto");
            consumidos += TamanoDescriptor - 1;
            var d = new byte[TamanoDescriptor];
            d[0] = primero[0];
            resto.CopyTo(d, 1);
            int fin = Array.IndexOf(d, (byte)0, 0, 11);
            var nombre = Encoding.ASCII.GetString(d, 0, fin < 0 ? 11 : fin).Trim().ToUpperInvariant();
            char tipo = char.ToUpperInvariant((char)d[11]);
            int longitud = d[16];
            int decimales = d[17];
            if (tipo == 'C')
            {
                // Clipper/FoxPro: un campo C de más de 255 caracteres guarda el byte alto de la longitud en el de decimales.
                longitud = d[16] | (d[17] << 8);
                decimales = 0;
            }
            bool soportada = tipo is 'C' or 'N' or 'F' or 'D' or 'L' or 'I' or 'Y' or 'T' or 'B';
            bool sistema = tipo == '0'; // _NullFlags de VFP
            if (!sistema) columnas.Add(new ColumnaDbf(nombre, tipo, longitud, decimales, desplazamiento, soportada));
            desplazamiento += longitud;
            if (columnas.Count > 255) throw Invalido("demasiados campos");
        }
        if (desplazamiento != largoRegistro) throw Invalido("la longitud de registro no coincide con los campos");
        if (columnas.Count == 0) throw Invalido("la tabla no tiene campos");
        // Saltar hasta el primer registro (VFP agrega 263 bytes de backlink tras el 0x0D).
        int saltar = largoCabecera - consumidos;
        if (saltar < 0) throw Invalido("cabecera no válida");
        if (saltar > 0 && LeerExacto(saltar) is null) throw Invalido("cabecera incompleta");
        _registro = new byte[largoRegistro];
        Columnas = columnas;
    }

    /// <summary>Devuelve el siguiente registro no borrado (null al terminar). Los valores son texto invariante o null.</summary>
    public string?[]? LeerSiguiente()
    {
        while (_leidos < CantidadDeclarada)
        {
            int n = LeerHasta(_registro);
            if (n == 0 || (n >= 1 && _registro[0] == FinArchivo && n < _registro.Length)) return null;
            if (n < _registro.Length) throw Invalido($"el registro {_leidos + 1} está incompleto");
            _leidos++;
            if (_registro[0] == (byte)'*') { Eliminados++; continue; }
            var valores = new string?[Columnas.Count];
            for (int i = 0; i < Columnas.Count; i++) valores[i] = Convertir(Columnas[i], _registro.AsSpan(Columnas[i].Desplazamiento, Columnas[i].Longitud));
            return valores;
        }
        return null;
    }

    private string? Convertir(ColumnaDbf c, ReadOnlySpan<byte> b)
    {
        switch (c.Tipo)
        {
            case 'C':
                { var s = _codificacion.GetString(b).TrimEnd(' ', '\0'); return s.Length == 0 ? null : s; }
            case 'N':
            case 'F':
                {
                    var s = Encoding.ASCII.GetString(b).Trim().Trim('\0');
                    if (s.Length == 0 || s.All(ch => ch == '*')) return null;
                    s = s.Replace(',', '.');
                    return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v.ToString(CultureInfo.InvariantCulture) : s;
                }
            case 'D':
                {
                    var s = Encoding.ASCII.GetString(b).Trim().Trim('\0');
                    if (s.Length == 0 || s == "00000000") return null;
                    return DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : s;
                }
            case 'L':
                return (char)b[0] switch { 'T' or 't' or 'Y' or 'y' => "true", 'F' or 'f' or 'N' or 'n' => "false", _ => null };
            case 'I':
                return BinaryPrimitives.ReadInt32LittleEndian(b).ToString(CultureInfo.InvariantCulture);
            case 'Y':
                return (BinaryPrimitives.ReadInt64LittleEndian(b) / 10000m).ToString(CultureInfo.InvariantCulture);
            case 'B':
                return BinaryPrimitives.ReadDoubleLittleEndian(b).ToString("R", CultureInfo.InvariantCulture);
            case 'T':
                {
                    int dia = BinaryPrimitives.ReadInt32LittleEndian(b[..4]);
                    int ms = BinaryPrimitives.ReadInt32LittleEndian(b.Slice(4, 4));
                    if (dia == 0 && ms == 0) return null;
                    var f = DateTime.UnixEpoch.AddDays(dia - DiaJulianoEpoch).AddMilliseconds(ms);
                    return f.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }
            default:
                return null; // M, G, P y otros: no soportados
        }
    }

    private byte[]? LeerExacto(int n)
    {
        var buf = new byte[n];
        return LeerHasta(buf) == n ? buf : null;
    }

    private int LeerHasta(byte[] buf)
    {
        int total = 0;
        while (total < buf.Length)
        {
            int r = _stream.Read(buf, total, buf.Length - total);
            if (r == 0) break;
            total += r;
        }
        return total;
    }

    private static InvalidDataException Invalido(string detalle) => new($"El archivo DBF no es válido: {detalle}.");

    public void Dispose() => _stream.Dispose();
}

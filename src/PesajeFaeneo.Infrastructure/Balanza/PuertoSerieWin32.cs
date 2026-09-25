using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Balanza;

/// <summary>
/// Puerto serie sobre kernel32 directo, en lugar de System.IO.Ports.SerialPort.
/// SerialPort.Open() siempre reescribe el DCB completo (SetCommState con flags DTR/RTS/handshake
/// propios) y algunos drivers USB-serie, en particular CH340, rechazan cualquier SetCommState con
/// ERROR_GEN_FAILURE (31, "el dispositivo no funciona") aunque no haya cambios. Aquí se lee el DCB
/// actual del driver, solo se modifican baudios/bits/paridad/parada cuando difieren y, si el driver
/// rechaza la escritura, se acepta igual cuando el DCB resultante ya es el deseado. DTR/RTS y el
/// control de flujo quedan como los deja el driver.
/// </summary>
internal sealed class PuertoSerieWin32 : IPuertoSerie
{
    private const int TiempoMaximoLecturaMs = 250;
    private const int PausaSondeoMs = 20;

    private readonly FormatoSerie _formato;
    private SafeFileHandle? _handle;

    public PuertoSerieWin32(ConfiguracionBalanzaDto config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Nombre = config.PuertoCom;
        _formato = new FormatoSerie(
            (uint)config.BaudRate,
            (byte)config.DataBits,
            MapearParidad(config.Paridad),
            MapearBitsParada(config.BitsParada));
    }

    public string Nombre { get; }

    public void Abrir()
    {
        if (_handle is { IsClosed: false })
        {
            throw new InvalidOperationException($"El puerto {Nombre} ya está abierto.");
        }

        var handle = Nativo.CreateFileW(
            @"\\.\" + Nombre,
            Nativo.GenericRead | Nativo.GenericWrite,
            0,
            IntPtr.Zero,
            Nativo.OpenExisting,
            Nativo.FileAttributeNormal,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw TraducirErrorApertura(error);
        }

        try
        {
            AjustarFormato(
                Nombre,
                _formato,
                (out Dcb dcb) =>
                {
                    dcb = new Dcb { DCBlength = (uint)Marshal.SizeOf<Dcb>() };
                    return Nativo.GetCommState(handle, ref dcb);
                },
                (ref Dcb dcb, out int error) =>
                {
                    bool ok = Nativo.SetCommState(handle, ref dcb);
                    error = ok ? 0 : Marshal.GetLastWin32Error();
                    return ok;
                });

            // Lectura no bloqueante: ReadFile devuelve de inmediato lo disponible (o 0 bytes).
            var tiempos = new CommTimeouts { ReadIntervalTimeout = uint.MaxValue };
            if (!Nativo.SetCommTimeouts(handle, ref tiempos))
            {
                throw new IOException(
                    $"No se pudieron configurar los tiempos de lectura del puerto {Nombre}.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        catch
        {
            handle.Dispose();
            throw;
        }

        _handle = handle;
    }

    public int Leer(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count <= 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var handle = _handle ?? throw new InvalidOperationException($"El puerto {Nombre} no está abierto.");
        long limite = Environment.TickCount64 + TiempoMaximoLecturaMs;

        while (true)
        {
            // SafeFileHandle retiene el handle durante la llamada: un Cerrar() concurrente no lo libera
            // a mitad de ReadFile, y la siguiente llamada lanza ObjectDisposedException.
            if (!Nativo.ReadFile(handle, ref buffer[offset], (uint)count, out uint leidos, IntPtr.Zero))
            {
                throw new IOException(
                    $"Error de lectura en el puerto {Nombre}.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if (leidos > 0)
            {
                return (int)leidos;
            }

            if (Environment.TickCount64 >= limite)
            {
                throw new TimeoutException();
            }

            Thread.Sleep(PausaSondeoMs);
        }
    }

    public void Cerrar() => _handle?.Dispose();

    public void Dispose() => _handle?.Dispose();

    internal delegate bool LeerDcb(out Dcb dcb);

    internal delegate bool EscribirDcb(ref Dcb dcb, out int error);

    internal static void AjustarFormato(string nombre, FormatoSerie deseado, LeerDcb leer, EscribirDcb escribir)
    {
        if (!leer(out var actual))
        {
            throw new IOException(
                $"No se pudo leer la configuración del puerto {nombre}.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (deseado.Coincide(actual))
        {
            // Caso habitual con CH340: no se llama SetCommState, que el driver rechazaría.
            return;
        }

        var nuevo = actual;
        nuevo.BaudRate = deseado.Baudios;
        nuevo.ByteSize = deseado.BitsDatos;
        nuevo.Parity = deseado.Paridad;
        nuevo.StopBits = deseado.BitsParada;

        if (escribir(ref nuevo, out int error))
        {
            return;
        }

        if (leer(out var verificado) && deseado.Coincide(verificado))
        {
            return;
        }

        throw new ConexionBalanzaException(
            $"El driver del puerto {nombre} rechazó la configuración {deseado.Describir()} (error {error}). " +
            $"El puerto está configurado en {FormatoSerie.Desde(actual).Describir()}: use esos valores o " +
            "cámbielos en el Administrador de dispositivos (Propiedades del puerto > Configuración de puerto).");
    }

    internal static byte MapearParidad(ParidadSerial paridad) => paridad switch
    {
        ParidadSerial.None => 0,
        ParidadSerial.Odd => 1,
        ParidadSerial.Even => 2,
        ParidadSerial.Mark => 3,
        ParidadSerial.Space => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(paridad), paridad, "Paridad no soportada.")
    };

    internal static byte MapearBitsParada(BitsParada bitsParada) => bitsParada switch
    {
        BitsParada.One => 0,
        BitsParada.OnePointFive => 1,
        BitsParada.Two => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(bitsParada), bitsParada, "Bits de parada no soportados.")
    };

    private Exception TraducirErrorApertura(int error)
    {
        var detalle = new Win32Exception(error);
        return error switch
        {
            Nativo.ErrorAccessDenied => new UnauthorizedAccessException(detalle.Message, detalle),
            Nativo.ErrorInvalidName => new ArgumentException(detalle.Message, nameof(Nombre), detalle),
            _ => new IOException(detalle.Message, detalle)
        };
    }

    internal readonly record struct FormatoSerie(uint Baudios, byte BitsDatos, byte Paridad, byte BitsParada)
    {
        public bool Coincide(Dcb dcb) =>
            dcb.BaudRate == Baudios && dcb.ByteSize == BitsDatos && dcb.Parity == Paridad && dcb.StopBits == BitsParada;

        public static FormatoSerie Desde(Dcb dcb) => new(dcb.BaudRate, dcb.ByteSize, dcb.Parity, dcb.StopBits);

        public string Describir()
        {
            char letraParidad = Paridad switch { 0 => 'N', 1 => 'O', 2 => 'E', 3 => 'M', 4 => 'S', _ => '?' };
            string parada = BitsParada switch { 0 => "1", 1 => "1.5", 2 => "2", _ => "?" };
            return $"{Baudios} baudios {BitsDatos}{letraParidad}{parada}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Dcb
    {
        public uint DCBlength;
        public uint BaudRate;
        public uint Flags;
        public ushort WReserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public sbyte XonChar;
        public sbyte XoffChar;
        public sbyte ErrorChar;
        public sbyte EofChar;
        public sbyte EvtChar;
        public ushort WReserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CommTimeouts
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;
    }

    private static class Nativo
    {
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint OpenExisting = 3;
        public const uint FileAttributeNormal = 0x80;
        public const int ErrorAccessDenied = 5;
        public const int ErrorInvalidName = 123;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFileW(
            string nombre,
            uint acceso,
            uint compartir,
            IntPtr seguridad,
            uint disposicion,
            uint atributos,
            IntPtr plantilla);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCommState(SafeFileHandle handle, ref Dcb dcb);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCommState(SafeFileHandle handle, ref Dcb dcb);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCommTimeouts(SafeFileHandle handle, ref CommTimeouts tiempos);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadFile(SafeFileHandle handle, ref byte buffer, uint cantidad, out uint leidos, IntPtr overlapped);
    }
}

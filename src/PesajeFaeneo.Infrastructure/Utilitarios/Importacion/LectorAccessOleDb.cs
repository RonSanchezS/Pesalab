using System.Data;
using System.Data.OleDb;
using System.Runtime.Versioning;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

[SupportedOSPlatform("windows")]
internal sealed class LectorAccessOleDb
{
    internal static readonly IReadOnlyList<string> ProveedoresPorDefecto = new[] { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" };
    private readonly IReadOnlyList<string> _proveedores;

    internal LectorAccessOleDb(IReadOnlyList<string>? proveedores = null) => _proveedores = proveedores ?? ProveedoresPorDefecto;

    public IReadOnlyList<string> ListarTablas(string ruta)
    {
        using var conexion = Abrir(ruta);
        return ListarTablas(conexion);
    }

    public TablaOrigen Leer(string ruta, string tabla, string huella, int maximoFilas)
    {
        using var conexion = Abrir(ruta);
        var tablas = ListarTablas(conexion);
        if (!tablas.Any(t => t.Equals(tabla, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidacionException(CamposOrigen.Tabla, "La tabla no existe en el archivo.");
        }

        var sql = $"SELECT * FROM [{tabla.Replace("]", "]]")}]";
        using var cmd = new OleDbCommand(sql, conexion);
        using var reader = cmd.ExecuteReader();

        var nombresCabecera = new string[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            nombresCabecera[i] = reader.GetName(i);
        }

        var advertencias = new List<string>();
        var columnas = LectorOrigenDatos.NormalizarColumnas(nombresCabecera, advertencias);

        var filas = new List<FilaOrigen>();
        int numeroFila = 1;

        while (reader.Read())
        {
            var valores = new string?[columnas.Count];
            for (int i = 0; i < columnas.Count; i++)
            {
                valores[i] = ConversorValoresOrigen.ATexto(reader.GetValue(i));
            }

            filas.Add(new FilaOrigen(numeroFila, valores));
            if (filas.Count > maximoFilas)
            {
                throw new ValidacionException(
                    CamposOrigen.Archivo,
                    $"El archivo tiene más de {ReglasImportacion.MaximoFilas.ToString("N0", TextosBoleta.Cultura)} filas; divídalo en partes.");
            }

            numeroFila++;
        }

        return new TablaOrigen(columnas, filas, huella, "Unicode (Access)", advertencias);
    }

    private static IReadOnlyList<string> ListarTablas(OleDbConnection conexion)
    {
        var esquema = conexion.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object?[] { null, null, null, "TABLE" });
        if (esquema == null)
        {
            return Array.Empty<string>();
        }

        var tablas = new List<string>();
        foreach (DataRow fila in esquema.Rows)
        {
            var nombre = fila["TABLE_NAME"]?.ToString();
            if (!string.IsNullOrEmpty(nombre) &&
                !nombre.StartsWith("MSys", StringComparison.OrdinalIgnoreCase) &&
                !nombre.StartsWith("~", StringComparison.OrdinalIgnoreCase))
            {
                tablas.Add(nombre);
            }
        }

        tablas.Sort(StringComparer.OrdinalIgnoreCase);
        return tablas;
    }

    private OleDbConnection Abrir(string ruta)
    {
        foreach (var proveedor in _proveedores)
        {
            var cadena = new OleDbConnectionStringBuilder { Provider = proveedor, DataSource = ruta };
            cadena["Mode"] = "Read";
            var conexion = new OleDbConnection(cadena.ConnectionString);
            try
            {
                conexion.Open();
                return conexion;
            }
            catch (InvalidOperationException)
            {
                conexion.Dispose(); // proveedor no registrado (H14): probar el siguiente
            }
            catch (OleDbException ex)
            {
                conexion.Dispose();
                throw new ValidacionException(CamposOrigen.Archivo, $"No se pudo abrir la base de Access ({ex.Message}). Si tiene contraseña, quítela o exporte la tabla a CSV.");
            }
        }
        throw new ReglaNegocioException("IMP_ACCESS_SIN_MOTOR", LectorOrigenDatos.MensajeSinMotor);
    }
}

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Importacion;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

internal sealed class LectorCsv
{
    public TablaOrigen Leer(byte[] datos, int? paginaForzada, string huella, int maxFilas = ReglasImportacion.MaximoFilas)
    {
        try
        {
            var (cod, salto, nombre) = DetectorCodificacion.Detectar(datos, paginaForzada);
            var texto = cod.GetString(datos, salto, datos.Length - salto);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                DetectDelimiter = true,
                HasHeaderRecord = true,
                BadDataFound = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };

            using var reader = new StringReader(texto);
            using var csv = new CsvReader(reader, config);

            if (!csv.Read())
            {
                throw new ValidacionException(CamposOrigen.Archivo, "El archivo está vacío.");
            }

            csv.ReadHeader();
            var cabecera = csv.HeaderRecord;
            if (cabecera == null || cabecera.Length == 0)
            {
                throw new ValidacionException(CamposOrigen.Archivo, "El archivo está vacío.");
            }

            if (cabecera.Length > ReglasImportacion.MaximoColumnas)
            {
                throw new ValidacionException(CamposOrigen.Archivo, "El archivo tiene más de 255 columnas.");
            }

            var advertencias = new List<string>();
            var columnas = LectorOrigenDatos.NormalizarColumnas(cabecera, advertencias);

            var filas = new List<FilaOrigen>();
            bool advertenciaSobrantesEmitida = false;
            int filasVaciasIgnoradas = 0;

            while (csv.Read())
            {
                var registro = csv.Parser.Record;
                if (registro == null)
                {
                    continue;
                }

                var fila = csv.Parser.Row;

                for (int i = 0; i < registro.Length; i++)
                {
                    var campo = registro[i];
                    if (campo != null && (campo.Contains('\r') || campo.Contains('\n')))
                    {
                        throw new ValidacionException(
                            CamposOrigen.Archivo,
                            $"La fila {fila} tiene comillas sin cerrar o un salto de línea dentro de un campo. Corrija el archivo y vuelva a leerlo.");
                    }
                }

                var valores = new string?[columnas.Count];
                bool todosNull = true;
                for (int i = 0; i < columnas.Count; i++)
                {
                    if (i < registro.Length)
                    {
                        var val = registro[i]?.Trim();
                        if (string.IsNullOrEmpty(val))
                        {
                            valores[i] = null;
                        }
                        else
                        {
                            valores[i] = val;
                            todosNull = false;
                        }
                    }
                    else
                    {
                        valores[i] = null;
                    }
                }

                if (todosNull)
                {
                    filasVaciasIgnoradas++;
                    continue;
                }

                if (registro.Length > columnas.Count && !advertenciaSobrantesEmitida)
                {
                    advertenciaSobrantesEmitida = true;
                    advertencias.Add($"Hay filas con más valores que columnas (la primera es la fila {fila}); los valores sobrantes se ignoran.");
                }

                filas.Add(new FilaOrigen(fila, valores));
                if (filas.Count > maxFilas)
                {
                    throw new ValidacionException(
                        CamposOrigen.Archivo,
                        $"El archivo tiene más de {ReglasImportacion.MaximoFilas.ToString("N0", TextosBoleta.Cultura)} filas; divídalo en partes.");
                }
            }

            if (filasVaciasIgnoradas > 0)
            {
                advertencias.Add($"Se ignoraron {filasVaciasIgnoradas} fila(s) vacía(s).");
            }

            return new TablaOrigen(columnas, filas, huella, nombre, advertencias);
        }
        catch (DomainException)
        {
            throw;
        }
        catch (CsvHelperException)
        {
            throw new ValidacionException(CamposOrigen.Archivo, "El archivo CSV no tiene un formato válido.");
        }
    }
}

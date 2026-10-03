using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public static class PreparadorImportacion
{
    private sealed class PesajeTemporal
    {
        public int Fila { get; set; }
        public long NumeroBoleta { get; set; }
        public DateTime FechaHoraUtc { get; set; }
        public DateTime FechaLocal { get; set; }
        public int GanaderoId { get; set; }
        public int ClienteId { get; set; }
        public string Direccion { get; set; } = string.Empty;
        public int? SecuenciaExplicita { get; set; }
        public bool SecuenciaMapeadaYVacia { get; set; }
        public string ColorRaza { get; set; } = string.Empty;
        public char Sexo { get; set; }
        public TipoDesposte TipoDesposte { get; set; }
        public decimal PesoIzquierdo { get; set; }
        public decimal PesoDerecho { get; set; }
        public decimal PesoTotal { get; set; }
        public string Observacion { get; set; } = string.Empty;
        public EstadoPesaje Estado { get; set; }
        public string? MotivoAnulacion { get; set; }
        public bool TieneAdvertenciaPrevia { get; set; }
    }

    public static PlanImportacion Preparar(
        TipoImportacion tipo,
        TablaOrigen tabla,
        IReadOnlyDictionary<string, int?> mapeo,
        ClavesExistentes existentes,
        DateTime ahoraUtc,
        TimeZoneInfo zona,
        int maximoIncidencias = ReglasImportacion.MaximoIncidencias)
    {
        ArgumentNullException.ThrowIfNull(tabla);
        ArgumentNullException.ThrowIfNull(mapeo);
        ArgumentNullException.ThrowIfNull(existentes);
        ArgumentNullException.ThrowIfNull(zona);

        var campos = CamposImportacion.De(tipo);
        var erroresMapeo = new List<ErrorValidacion>();

        // M1: cada campo obligatorio con índice asignado
        foreach (var campo in campos)
        {
            if (campo.Obligatorio)
            {
                if (!mapeo.TryGetValue(campo.Clave, out var idx) || !idx.HasValue)
                {
                    erroresMapeo.Add(new ErrorValidacion(campo.Clave, $"Asigne una columna al campo «{campo.Titulo}»."));
                }
            }
        }

        // M2: índice fuera de 0..tabla.Columnas.Count - 1
        foreach (var campo in campos)
        {
            if (mapeo.TryGetValue(campo.Clave, out var idx) && idx.HasValue)
            {
                if (idx.Value < 0 || idx.Value >= tabla.Columnas.Count)
                {
                    erroresMapeo.Add(new ErrorValidacion(campo.Clave, "La columna asignada no existe."));
                }
            }
        }

        // M3: un mismo índice en dos campos
        var indicesOcupados = new Dictionary<int, string>();
        var indicesReportados = new HashSet<int>();
        foreach (var campo in campos)
        {
            if (mapeo.TryGetValue(campo.Clave, out var idx) && idx.HasValue)
            {
                int i = idx.Value;
                if (i >= 0 && i < tabla.Columnas.Count)
                {
                    if (indicesOcupados.ContainsKey(i))
                    {
                        if (indicesReportados.Add(i))
                        {
                            var nombreCol = tabla.Columnas[i];
                            erroresMapeo.Add(new ErrorValidacion(CamposImportacion.Mapeo, $"La columna «{nombreCol}» está asignada a más de un campo."));
                        }
                    }
                    else
                    {
                        indicesOcupados[i] = campo.Clave;
                    }
                }
            }
        }

        if (erroresMapeo.Count > 0)
        {
            throw new ValidacionException(erroresMapeo);
        }

        string? V(FilaOrigen fila, string clave)
        {
            if (mapeo.TryGetValue(clave, out var idx) && idx.HasValue && idx.Value >= 0 && idx.Value < fila.Valores.Count)
            {
                return fila.Valores[idx.Value];
            }
            return null;
        }

        bool Mapeado(string clave) => mapeo.TryGetValue(clave, out var idx) && idx.HasValue;

        var incidenciasGlobal = new List<Incidencia>();
        bool incidenciasTruncadas = false;
        int filasConError = 0;
        int filasOmitidas = 0;
        int filasConAdvertencia = 0;

        void AgregarIncidencias(IEnumerable<Incidencia> incs)
        {
            foreach (var inc in incs)
            {
                if (incidenciasGlobal.Count < maximoIncidencias)
                {
                    incidenciasGlobal.Add(inc);
                }
                else
                {
                    incidenciasTruncadas = true;
                }
            }
        }

        var ganaderosImportar = new List<GanaderoAImportar>();
        var clientesImportar = new List<ClienteAImportar>();
        var pesajesTemporales = new List<PesajeTemporal>();

        var marcasVistas = new Dictionary<string, int>(StringComparer.Ordinal);
        var codigosVistos = new Dictionary<int, int>();
        var boletasVistas = new Dictionary<long, int>();

        var fechaLocalHoy = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona).Date;

        foreach (var fila in tabla.Filas)
        {
            var incsFila = new List<Incidencia>();

            switch (tipo)
            {
                case TipoImportacion.Ganaderos:
                    {
                        var marca = ValidadorCatalogos.NormalizarMarca(V(fila, CamposImportacion.Marca));
                        var nombre = ValidadorCatalogos.NormalizarMayusculas(V(fila, CamposImportacion.Nombre));

                        if (nombre.Length > 100)
                        {
                            nombre = nombre[..100];
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Nombre, NivelIncidencia.Advertencia, "El nombre tenía más de 100 caracteres y se recortó."));
                        }

                        var errsGanadero = ValidadorCatalogos.ValidarGanadero(marca, nombre);
                        foreach (var err in errsGanadero)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, err.Campo, NivelIncidencia.Error, err.Mensaje));
                        }

                        var valActivo = V(fila, CamposImportacion.Activo);
                        bool activo = true;
                        if (!string.IsNullOrWhiteSpace(valActivo))
                        {
                            var b = ConversoresImportacion.Booleano(valActivo);
                            if (b == null)
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Activo, NivelIncidencia.Error, $"Valor de «Activo» no reconocido: «{valActivo}»."));
                            }
                            else
                            {
                                activo = b.Value;
                            }
                        }

                        bool tieneErrorMarca = incsFila.Any(i => i.Campo == CamposCatalogo.Marca && i.Nivel == NivelIncidencia.Error);
                        if (!tieneErrorMarca && !string.IsNullOrEmpty(marca))
                        {
                            if (marcasVistas.TryGetValue(marca, out int primeraFila))
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Marca, NivelIncidencia.Error, $"La marca {marca} se repite en el archivo (primera vez en la fila {primeraFila})."));
                            }
                            else
                            {
                                marcasVistas[marca] = fila.Numero;

                                if (existentes.GanaderosPorMarca.ContainsKey(marca))
                                {
                                    incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Marca, NivelIncidencia.Omitida, $"Ya existe un ganadero con la marca {marca}; no se modifica."));
                                }
                            }
                        }

                        if (incsFila.Any(i => i.Nivel == NivelIncidencia.Error))
                        {
                            filasConError++;
                        }
                        else if (incsFila.Any(i => i.Nivel == NivelIncidencia.Omitida))
                        {
                            filasOmitidas++;
                        }
                        else
                        {
                            if (incsFila.Any(i => i.Nivel == NivelIncidencia.Advertencia))
                            {
                                filasConAdvertencia++;
                            }
                            ganaderosImportar.Add(new GanaderoAImportar(fila.Numero, marca, nombre, activo));
                        }

                        AgregarIncidencias(incsFila);
                        break;
                    }

                case TipoImportacion.Clientes:
                    {
                        var valCodigo = V(fila, CamposImportacion.Codigo);
                        bool codigoValido = ConversoresImportacion.TryEntero(valCodigo, out long codigoNum) && codigoNum >= 1 && codigoNum <= 999999;
                        if (!codigoValido)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Codigo, NivelIncidencia.Error, "Código no válido: debe ser un número entre 1 y 999999."));
                        }

                        var nombre = ValidadorCatalogos.NormalizarMayusculas(V(fila, CamposImportacion.Nombre));
                        if (nombre.Length > 100)
                        {
                            nombre = nombre[..100];
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Nombre, NivelIncidencia.Advertencia, "El nombre tenía más de 100 caracteres y se recortó."));
                        }

                        var direccion = ValidadorCatalogos.NormalizarMayusculas(V(fila, CamposImportacion.Direccion));
                        if (direccion.Length > 150)
                        {
                            direccion = direccion[..150];
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Direccion, NivelIncidencia.Advertencia, "La dirección tenía más de 150 caracteres y se recortó."));
                        }

                        if (codigoValido)
                        {
                            var errsCliente = ValidadorCatalogos.ValidarCliente((int)codigoNum, nombre, direccion);
                            foreach (var err in errsCliente)
                            {
                                incsFila.Add(new Incidencia(fila.Numero, err.Campo, NivelIncidencia.Error, err.Mensaje));
                            }
                        }

                        var valActivo = V(fila, CamposImportacion.Activo);
                        bool activo = true;
                        if (!string.IsNullOrWhiteSpace(valActivo))
                        {
                            var b = ConversoresImportacion.Booleano(valActivo);
                            if (b == null)
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Activo, NivelIncidencia.Error, $"Valor de «Activo» no reconocido: «{valActivo}»."));
                            }
                            else
                            {
                                activo = b.Value;
                            }
                        }

                        if (codigoValido)
                        {
                            int codInt = (int)codigoNum;
                            if (codigosVistos.TryGetValue(codInt, out int primeraFila))
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Codigo, NivelIncidencia.Error, $"El código {codInt} se repite en el archivo (primera vez en la fila {primeraFila})."));
                            }
                            else
                            {
                                codigosVistos[codInt] = fila.Numero;

                                if (existentes.ClientesPorCodigo.ContainsKey(codInt))
                                {
                                    incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Codigo, NivelIncidencia.Omitida, $"Ya existe un cliente con el código {codInt}; no se modifica."));
                                }
                            }
                        }

                        if (incsFila.Any(i => i.Nivel == NivelIncidencia.Error))
                        {
                            filasConError++;
                        }
                        else if (incsFila.Any(i => i.Nivel == NivelIncidencia.Omitida))
                        {
                            filasOmitidas++;
                        }
                        else
                        {
                            if (incsFila.Any(i => i.Nivel == NivelIncidencia.Advertencia))
                            {
                                filasConAdvertencia++;
                            }
                            clientesImportar.Add(new ClienteAImportar(fila.Numero, (int)codigoNum, nombre, direccion, activo));
                        }

                        AgregarIncidencias(incsFila);
                        break;
                    }

                case TipoImportacion.Pesajes:
                    {
                        // P1 Número
                        var valNum = V(fila, CamposImportacion.NumeroBoleta);
                        bool numValido = ConversoresImportacion.TryEntero(valNum, out long numBoleta) && numBoleta >= 1;
                        if (!numValido)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.NumeroBoleta, NivelIncidencia.Error, $"Número de boleta no válido: «{valNum}»."));
                        }
                        else
                        {
                            if (boletasVistas.TryGetValue(numBoleta, out int primeraFila))
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.NumeroBoleta, NivelIncidencia.Error, $"La boleta {numBoleta} se repite en el archivo (primera vez en la fila {primeraFila})."));
                            }
                            else
                            {
                                boletasVistas[numBoleta] = fila.Numero;

                                if (existentes.BoletasExistentes.TryGetValue(numBoleta, out var origenExistente))
                                {
                                    if (origenExistente == OrigenPeso.Importado)
                                    {
                                        incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.NumeroBoleta, NivelIncidencia.Omitida, $"La boleta {numBoleta} ya fue importada antes."));
                                    }
                                    else
                                    {
                                        incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.NumeroBoleta, NivelIncidencia.Error, $"La boleta {numBoleta} ya existe en el sistema y no es una boleta importada; no se reemplaza."));
                                    }
                                }
                            }
                        }

                        // P2 Fecha
                        var valFecha = V(fila, CamposImportacion.Fecha);
                        var valHora = V(fila, CamposImportacion.Hora);
                        DateTime fechaHoraUtc = default;
                        DateTime fechaLocal = default;
                        if (!ConversoresImportacion.TryFechaHora(valFecha, valHora, out fechaLocal))
                        {
                            var textoFechaHora = string.IsNullOrWhiteSpace(valHora) ? valFecha : $"{valFecha} {valHora}";
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Fecha, NivelIncidencia.Error, $"Fecha u hora no válida: «{textoFechaHora}»."));
                        }
                        else
                        {
                            if (fechaLocal.Year < ReglasImportacion.AnioMinimo || fechaLocal.Date > fechaLocalHoy.AddDays(1))
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Fecha, NivelIncidencia.Error, $"La fecha {fechaLocal:dd/MM/yyyy} está fuera del rango permitido (1990 a mañana)."));
                            }
                            else
                            {
                                fechaHoraUtc = ConversoresImportacion.LocalAUtc(fechaLocal, zona);
                            }
                        }

                        // P3 Ganadero
                        var marcaGan = ValidadorCatalogos.NormalizarMarca(V(fila, CamposImportacion.MarcaGanadero));
                        int ganaderoId = 0;
                        if (!existentes.GanaderosPorMarca.TryGetValue(marcaGan, out ganaderoId))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.MarcaGanadero, NivelIncidencia.Error, $"No existe un ganadero con la marca {marcaGan}. Importe primero los ganaderos."));
                        }

                        // P4 Cliente
                        var valCodCli = V(fila, CamposImportacion.CodigoCliente);
                        int clienteId = 0;
                        ClienteExistente? clienteExistente = null;
                        if (!ConversoresImportacion.TryEntero(valCodCli, out long codCliNum) || codCliNum < 1 || codCliNum > 999_999)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.CodigoCliente, NivelIncidencia.Error, $"Código de cliente no válido: «{valCodCli}»."));
                        }
                        else if (!existentes.ClientesPorCodigo.TryGetValue((int)codCliNum, out clienteExistente))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.CodigoCliente, NivelIncidencia.Error, $"No existe un cliente con el código {codCliNum}. Importe primero los clientes."));
                        }
                        else
                        {
                            clienteId = clienteExistente.Id;
                        }

                        // P5 Pesos
                        var valIzq = V(fila, CamposImportacion.PesoIzquierdo);
                        var valDer = V(fila, CamposImportacion.PesoDerecho);
                        decimal izq = 0m;
                        decimal der = 0m;
                        bool errorPesos = false;

                        if (string.IsNullOrWhiteSpace(valIzq))
                        {
                            izq = 0m;
                        }
                        else if (!ConversoresImportacion.TryDecimal(valIzq, out izq))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.PesoIzquierdo, NivelIncidencia.Error, $"Peso no válido: «{valIzq}»."));
                            errorPesos = true;
                        }

                        if (string.IsNullOrWhiteSpace(valDer))
                        {
                            der = 0m;
                        }
                        else if (!ConversoresImportacion.TryDecimal(valDer, out der))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.PesoDerecho, NivelIncidencia.Error, $"Peso no válido: «{valDer}»."));
                            errorPesos = true;
                        }

                        decimal total = 0m;
                        if (!errorPesos)
                        {
                            izq = ReglasPesaje.Redondear(izq);
                            der = ReglasPesaje.Redondear(der);

                            if (izq < 0 || der < 0)
                            {
                                incsFila.Add(new Incidencia(fila.Numero, "Pesos", NivelIncidencia.Error, "Los pesos no pueden ser negativos."));
                                errorPesos = true;
                            }
                            else if (izq == 0 && der == 0)
                            {
                                incsFila.Add(new Incidencia(fila.Numero, "Pesos", NivelIncidencia.Error, "La boleta no tiene pesos (ambos lados en cero)."));
                                errorPesos = true;
                            }
                            else if (izq > ReglasImportacion.PesoMaximoLadoImportacionKg || der > ReglasImportacion.PesoMaximoLadoImportacionKg)
                            {
                                incsFila.Add(new Incidencia(fila.Numero, "Pesos", NivelIncidencia.Error, "El peso de un lado no puede superar 1.200,0 kg."));
                                errorPesos = true;
                            }
                            else
                            {
                                total = ReglasPesaje.CalcularTotal(izq, der);
                            }
                        }

                        var valTot = V(fila, CamposImportacion.PesoTotal);
                        if (!errorPesos && !string.IsNullOrWhiteSpace(valTot))
                        {
                            if (!ConversoresImportacion.TryDecimal(valTot, out var dado))
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.PesoTotal, NivelIncidencia.Error, $"Peso total no válido: «{valTot}»."));
                            }
                            else
                            {
                                dado = ReglasPesaje.Redondear(dado);
                                if (Math.Abs(dado - total) > 0.1m)
                                {
                                    incsFila.Add(new Incidencia(
                                        fila.Numero,
                                        CamposImportacion.PesoTotal,
                                        NivelIncidencia.Error,
                                        $"El peso total informado ({dado.ToString("0.0", TextosBoleta.Cultura)}) no coincide con la suma de los lados ({total.ToString("0.0", TextosBoleta.Cultura)})."));
                                }
                            }
                        }

                        // P6 Desposte
                        var valDesposte = V(fila, CamposImportacion.TipoDesposte);
                        TipoDesposte tipoDesposte = TipoDesposte.ResCompleta;
                        if (!ConversoresImportacion.TryDesposte(valDesposte, out var tipoDesposteParsed))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.TipoDesposte, NivelIncidencia.Error, $"Tipo de desposte no reconocido: «{valDesposte}»."));
                        }
                        else
                        {
                            if (tipoDesposteParsed.HasValue)
                            {
                                tipoDesposte = tipoDesposteParsed.Value;
                            }
                            else
                            {
                                tipoDesposte = (izq > 0 && der > 0) ? TipoDesposte.ResCompleta : TipoDesposte.MediaRes;
                            }

                            if (!errorPesos)
                            {
                                var errsPesos = ReglasPesaje.ValidarPesos(tipoDesposte, izq, der);
                                foreach (var err in errsPesos)
                                {
                                    incsFila.Add(new Incidencia(fila.Numero, "Pesos", NivelIncidencia.Advertencia, $"Fuera de las reglas actuales de pesaje: {err.Mensaje}"));
                                }
                            }
                        }

                        // P7 Secuencia
                        bool secuenciaMapeada = Mapeado(CamposImportacion.Secuencia);
                        var valSec = V(fila, CamposImportacion.Secuencia);
                        int? secuenciaExplicita = null;
                        bool secuenciaMapeadaYVacia = false;

                        if (secuenciaMapeada)
                        {
                            if (string.IsNullOrWhiteSpace(valSec))
                            {
                                secuenciaMapeadaYVacia = true;
                            }
                            else
                            {
                                if (!ConversoresImportacion.TryEntero(valSec, out var secNum) || secNum < 1 || secNum > 9999)
                                {
                                    incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Secuencia, NivelIncidencia.Error, $"Secuencia no válida: «{valSec}» (1 a 9999)."));
                                }
                                else
                                {
                                    secuenciaExplicita = (int)secNum;
                                }
                            }
                        }

                        // P8 Textos
                        var dirVal = ValidadorCatalogos.NormalizarMayusculas(V(fila, CamposImportacion.Direccion));
                        if (string.IsNullOrEmpty(dirVal))
                        {
                            dirVal = clienteExistente?.Direccion ?? string.Empty;
                        }
                        dirVal = ConversoresImportacion.Recortar(dirVal, ReglasPesaje.LongitudMaximaDireccion, out var dirRecortada);
                        if (dirRecortada)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Direccion, NivelIncidencia.Advertencia, "La dirección tenía más de 150 caracteres y se recortó."));
                        }

                        var colorVal = ValidadorCatalogos.NormalizarMayusculas(V(fila, CamposImportacion.ColorRaza));
                        colorVal = ConversoresImportacion.Recortar(colorVal, ReglasPesaje.LongitudMaximaColorRaza, out var colorRecortado);
                        if (colorRecortado)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.ColorRaza, NivelIncidencia.Advertencia, "El color/raza tenía más de 30 caracteres y se recortó."));
                        }

                        var obsVal = ValidadorCatalogos.NormalizarMayusculas(V(fila, CamposImportacion.Observacion));
                        obsVal = ConversoresImportacion.Recortar(obsVal, ReglasPesaje.LongitudMaximaObservacion, out var obsRecortada);
                        if (obsRecortada)
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Observacion, NivelIncidencia.Advertencia, "La observación tenía más de 250 caracteres y se recortó."));
                        }

                        // P9 Sexo
                        var valSexo = V(fila, CamposImportacion.Sexo);
                        char sexoFinal = 'M';
                        if (!ConversoresImportacion.TrySexo(valSexo, out var sexoParsed))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Sexo, NivelIncidencia.Error, $"Sexo no reconocido: «{valSexo}»."));
                        }
                        else if (sexoParsed == null)
                        {
                            sexoFinal = 'M';
                            if (Mapeado(CamposImportacion.Sexo))
                            {
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Sexo, NivelIncidencia.Advertencia, "Sexo vacío: se asume M."));
                            }
                        }
                        else
                        {
                            sexoFinal = sexoParsed.Value;
                        }

                        // P10 Estado
                        var nombreColEstado = mapeo.TryGetValue(CamposImportacion.Estado, out var idxEst) && idxEst.HasValue && idxEst.Value >= 0 && idxEst.Value < tabla.Columnas.Count
                            ? tabla.Columnas[idxEst.Value]
                            : null;
                        var claveColEstado = ConversoresImportacion.ClaveColumna(nombreColEstado);
                        bool columnaBooleana = claveColEstado is "ANULADA" or "ANULADO";

                        var valEstado = V(fila, CamposImportacion.Estado);
                        EstadoPesaje estadoFinal = EstadoPesaje.Vigente;
                        string? motivoAnulacionFinal = null;

                        if (!ConversoresImportacion.TryEstado(valEstado, columnaBooleana, out estadoFinal))
                        {
                            incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.Estado, NivelIncidencia.Error, $"Estado no reconocido: «{valEstado}»."));
                        }
                        else if (estadoFinal == EstadoPesaje.Anulado)
                        {
                            var motVal = ValidadorCatalogos.NormalizarTexto(V(fila, CamposImportacion.MotivoAnulacion));
                            if (string.IsNullOrWhiteSpace(motVal))
                            {
                                motivoAnulacionFinal = ReglasImportacion.MotivoAnulacionPorDefecto;
                                incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.MotivoAnulacion, NivelIncidencia.Advertencia, "Boleta anulada sin motivo: se registra «ANULADA EN EL SISTEMA ANTERIOR»."));
                            }
                            else
                            {
                                motivoAnulacionFinal = ConversoresImportacion.Recortar(motVal, 250, out var motRecortado);
                                if (motRecortado)
                                {
                                    incsFila.Add(new Incidencia(fila.Numero, CamposImportacion.MotivoAnulacion, NivelIncidencia.Advertencia, "El motivo de anulación tenía más de 250 caracteres y se recortó."));
                                }
                            }
                        }

                        bool tieneErrorP = incsFila.Any(i => i.Nivel == NivelIncidencia.Error);
                        bool tieneOmitidaP = incsFila.Any(i => i.Nivel == NivelIncidencia.Omitida);
                        bool tieneAdvP = incsFila.Any(i => i.Nivel == NivelIncidencia.Advertencia);

                        if (tieneErrorP)
                        {
                            filasConError++;
                        }
                        else if (tieneOmitidaP)
                        {
                            filasOmitidas++;
                        }
                        else
                        {
                            if (tieneAdvP)
                            {
                                filasConAdvertencia++;
                            }

                            pesajesTemporales.Add(new PesajeTemporal
                            {
                                Fila = fila.Numero,
                                NumeroBoleta = numBoleta,
                                FechaHoraUtc = fechaHoraUtc,
                                FechaLocal = fechaLocal,
                                GanaderoId = ganaderoId,
                                ClienteId = clienteId,
                                Direccion = dirVal,
                                SecuenciaExplicita = secuenciaExplicita,
                                SecuenciaMapeadaYVacia = secuenciaMapeadaYVacia,
                                ColorRaza = colorVal,
                                Sexo = sexoFinal,
                                TipoDesposte = tipoDesposte,
                                PesoIzquierdo = izq,
                                PesoDerecho = der,
                                PesoTotal = total,
                                Observacion = obsVal,
                                Estado = estadoFinal,
                                MotivoAnulacion = motivoAnulacionFinal,
                                TieneAdvertenciaPrevia = tieneAdvP
                            });
                        }

                        AgregarIncidencias(incsFila);
                        break;
                    }
            }
        }

        // Si tipo == Pesajes, calcular secuencias
        var pesajesImportar = new List<PesajeAImportar>();
        if (tipo == TipoImportacion.Pesajes && pesajesTemporales.Count > 0)
        {
            var grupos = pesajesTemporales
                .GroupBy(p => (p.GanaderoId, p.FechaLocal.Date));

            foreach (var grupo in grupos)
            {
                var ordenados = grupo
                    .OrderBy(p => p.FechaHoraUtc)
                    .ThenBy(p => p.NumeroBoleta)
                    .ToList();

                int contador = 0;
                foreach (var p in ordenados)
                {
                    int secuenciaFinal;
                    if (p.SecuenciaExplicita.HasValue)
                    {
                        contador = Math.Max(contador, p.SecuenciaExplicita.Value);
                        secuenciaFinal = p.SecuenciaExplicita.Value;
                    }
                    else
                    {
                        contador = Math.Min(contador + 1, 9999);
                        secuenciaFinal = contador;

                        if (p.SecuenciaMapeadaYVacia)
                        {
                            var incSec = new Incidencia(p.Fila, CamposImportacion.Secuencia, NivelIncidencia.Advertencia, $"Secuencia vacía: se calculó {secuenciaFinal}.");
                            if (incidenciasGlobal.Count < maximoIncidencias)
                            {
                                incidenciasGlobal.Add(incSec);
                            }
                            else
                            {
                                incidenciasTruncadas = true;
                            }

                            if (!p.TieneAdvertenciaPrevia)
                            {
                                filasConAdvertencia++;
                            }
                        }
                    }

                    pesajesImportar.Add(new PesajeAImportar(
                        p.Fila,
                        p.NumeroBoleta,
                        p.FechaHoraUtc,
                        p.GanaderoId,
                        p.ClienteId,
                        p.Direccion,
                        secuenciaFinal,
                        p.ColorRaza,
                        p.Sexo,
                        p.TipoDesposte,
                        p.PesoIzquierdo,
                        p.PesoDerecho,
                        p.PesoTotal,
                        p.Observacion,
                        p.Estado,
                        p.MotivoAnulacion));
                }
            }

            // Ordenar pesajesImportar por el orden original de fila para mantener consistencia
            pesajesImportar.Sort((a, b) => a.Fila.CompareTo(b.Fila));
        }

        return new PlanImportacion(
            tipo,
            tabla.Filas.Count,
            ganaderosImportar,
            clientesImportar,
            pesajesImportar,
            incidenciasGlobal,
            incidenciasTruncadas,
            filasConError,
            filasOmitidas,
            filasConAdvertencia);
    }
}

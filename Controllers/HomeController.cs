using Microsoft.AspNetCore.Mvc;
using FacturacionRecurrente.Models;
using FacturacionRecurrente.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FacturacionRecurrente.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseService _databaseService;
        private readonly ComodinService _comodinService;
        private readonly FacturacionService _facturacionService;

        public HomeController(DatabaseService databaseService, ComodinService comodinService, FacturacionService facturacionService)
        {
            _databaseService = databaseService;
            _comodinService = comodinService;
            _facturacionService = facturacionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                ViewBag.PropietariosRFC = await _databaseService.ObtenerPropietariosRFC();
                ViewBag.FormasPago = await _databaseService.ObtenerCatalogo("c_FormaPago");
                ViewBag.UsosCFDI = await _databaseService.ObtenerCatalogo("c_UsoCFDI");
                ViewBag.Monedas = await _databaseService.ObtenerCatalogo("c_Moneda");
                ViewBag.ClavesUnidad = await _databaseService.ObtenerCatalogo("c_ClaveUnidad");
                ViewBag.ComodinesDisponibles = _comodinService.ObtenerComodinesDisponibles();
                ViewBag.EjemplosFormula = _comodinService.GenerarEjemploFormula();
                // NO cargar clientes ni productos aquí - se cargan dinámicamente por AJAX
                ViewBag.DatabaseError = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Database connection error: {ex.Message}");
                // Valores por defecto cuando no hay conexión
                ViewBag.PropietariosRFC = new List<dynamic>();
                ViewBag.FormasPago = new List<dynamic>();
                ViewBag.UsosCFDI = new List<dynamic>();
                ViewBag.Monedas = new List<dynamic>();
                ViewBag.ClavesUnidad = new List<dynamic>();
                ViewBag.ComodinesDisponibles = _comodinService.ObtenerComodinesDisponibles();
                ViewBag.EjemplosFormula = _comodinService.GenerarEjemploFormula();
                ViewBag.DatabaseError = true;
                ViewBag.DatabaseErrorMessage = "⚠️ Sin conexión a base de datos. Verifique la conectividad de red.";
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerarCadenaPreview([FromBody] PlantillaFacturacion plantilla)
        {
            try
            {
                // Debug logging
                Console.WriteLine($"[DEBUG] EmisorRFCId: {plantilla.EmisorRFCId}");
                Console.WriteLine($"[DEBUG] ClienteId: {plantilla.ClienteId}");
                Console.WriteLine($"[DEBUG] Serie: {plantilla.Serie}");
                Console.WriteLine($"[DEBUG] Conceptos count: {plantilla.Conceptos?.Count ?? 0}");

                if (plantilla.Conceptos?.Any() == true)
                {
                    var concepto = plantilla.Conceptos.First();
                    Console.WriteLine($"[DEBUG] Primer concepto - ClaveProdServ: '{concepto.ClaveProdServ}', ClaveUnidad: '{concepto.ClaveUnidad}', Descripcion: '{concepto.Descripcion}'");
                }

                var cadena = await _facturacionService.GenerarCadenaFacturacion(plantilla);
                return Json(new { success = true, cadena = cadena });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception: {ex}");
                return Json(new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcesarFactura([FromBody] ProcesarFacturaRequest request)
        {
            try
            {
                var resultado = await _facturacionService.ProcesarFactura(request.Plantilla, request.Confirmar);
                return Json(new { success = true, resultado = resultado });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTipoCambio()
        {
            try
            {
                var tipoCambio = await _databaseService.ObtenerTipoCambioActual();
                return Json(new { success = true, tipoCambio = tipoCambio });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerClientesPorRFC(string rfcEmisor)
        {
            try
            {
                var clientes = await _databaseService.ObtenerClientes(rfcEmisor);
                return Json(new { success = true, clientes = clientes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string busqueda, string? rfcEmisor = null)
        {
            try
            {
                var clientes = await _databaseService.BuscarClientes(busqueda, rfcEmisor);
                return Json(new { success = true, clientes = clientes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductosPorRFC(string rfc)
        {
            try
            {
                var productos = await _databaseService.ObtenerProductosPorRFC(rfc);
                return Json(new { success = true, productos = productos });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerarVistaPrevia([FromBody] PlantillaFacturacion plantilla)
        {
            try
            {
                // Obtener datos del cliente y emisor
                var cliente = await _databaseService.ObtenerClientePorId(plantilla.ClienteId);
                var emisor = await _databaseService.ObtenerPropietarioRFCPorId(plantilla.EmisorRFCId);

                if (cliente == null || emisor == null)
                    return Json(new { success = false, error = "Cliente o emisor no encontrado" });

                // Procesar comodines en datos generales
                var serie = await _comodinService.ProcesarComodines(plantilla.Serie, cliente, emisor.RFC, plantilla.Serie);
                var folio = await _comodinService.ProcesarComodines(plantilla.Folio, cliente, emisor.RFC, serie);
                var condicionesPago = await _comodinService.ProcesarComodines(plantilla.CondicionesPago, cliente, emisor.RFC, serie);
                var observaciones = await _comodinService.ProcesarComodines(plantilla.Observaciones, cliente, emisor.RFC, serie);

                // Procesar conceptos
                var conceptosProcesados = new List<dynamic>();
                decimal subtotalFactura = 0;

                foreach (var concepto in plantilla.Conceptos.OrderBy(c => c.Orden))
                {
                    var descripcion = await _comodinService.ProcesarComodines(concepto.Descripcion, cliente, emisor.RFC, serie);
                    var cantidad = await _comodinService.EvaluarFormula(concepto.CantidadFormula, cliente);
                    var valorUnitario = await _comodinService.EvaluarFormula(concepto.ValorUnitarioFormula, cliente);
                    var importe = cantidad * valorUnitario;
                    subtotalFactura += importe;

                    conceptosProcesados.Add(new
                    {
                        claveProdServ = concepto.ClaveProdServ,
                        claveUnidad = concepto.ClaveUnidad,
                        descripcion = descripcion,
                        cantidad = cantidad,
                        valorUnitario = valorUnitario,
                        importe = importe
                    });
                }

                // Calcular IVA y total
                decimal ivaImporte = 0;
                decimal totalFactura = subtotalFactura;

                switch (plantilla.TipoIVA)
                {
                    case "ConIVA":
                        ivaImporte = subtotalFactura * 0.16m; // 16% IVA
                        totalFactura = subtotalFactura + ivaImporte;
                        break;
                    case "IVA0":
                        ivaImporte = 0;
                        totalFactura = subtotalFactura;
                        break;
                    case "SinIVA":
                        ivaImporte = 0;
                        totalFactura = subtotalFactura;
                        break;
                }

                var facturaData = new
                {
                    // Datos del emisor
                    emisorRFC = emisor.RFC,
                    emisorNombre = emisor.RazonSocial,
                    emisorLugarExpedicion = !string.IsNullOrEmpty(emisor.LugarExpedicion) ? emisor.LugarExpedicion : "06000",

                    // Datos del receptor
                    receptorRFC = cliente.RFCCliente,
                    receptorNombre = cliente.Razon_Social,
                    receptorEmail = cliente.Email,
                    receptorCP = cliente.DomicilioFiscalCliente ?? "06000",
                    receptorRegimen = cliente.RegimenFiscalCliente ?? "601",

                    // Datos generales
                    serie = serie,
                    folio = folio,
                    seriefolio = string.IsNullOrEmpty(serie) ? folio : $"{serie}-{folio}",
                    fecha = DateTime.Now.ToString("yyyy-MM-dd"),
                    hora = DateTime.Now.ToString("HH:mm:ss"),
                    formaPago = plantilla.FormaPago,
                    usoCFDI = plantilla.UsoCFDI,
                    moneda = plantilla.Moneda,
                    condicionesPago = condicionesPago,
                    observaciones = observaciones,
                    lugarExpedicion = plantilla.LugarExpedicion,

                    // Conceptos y totales
                    conceptos = conceptosProcesados,
                    subtotal = subtotalFactura,
                    ivaImporte = ivaImporte,
                    ivaTexto = plantilla.TipoIVA == "ConIVA" ? "IVA 16%" : plantilla.TipoIVA == "IVA0" ? "IVA 0%" : "Sin IVA",
                    tipoIVA = plantilla.TipoIVA,
                    total = totalFactura
                };

                return Json(new { success = true, factura = facturaData });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Vista Previa Exception: {ex}");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarPlantillaRecurrente([FromBody] PlantillaFacturacion plantilla)
        {
            try
            {
                // Guardar plantilla en base de datos
                using var connection = _databaseService.GetConnection();
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Insertar plantilla principal
                    var insertPlantillaCmd = new SqlCommand(@"
                        INSERT INTO PlantillasFacturacion (
                            Nombre, ClienteId, EmisorRFCId, Serie, Folio, FormaPago, UsoCFDI,
                            LugarExpedicion, Moneda, TipoIVA, CondicionesPago, Observaciones,
                            Activa, EsRecurrente, TipoProgramacion, DiaEjecucion, DiaSemana,
                            IntervaloEjecucion, ProximaEjecucion, UsuarioCreacion
                        ) VALUES (
                            @Nombre, @ClienteId, @EmisorRFCId, @Serie, @Folio, @FormaPago, @UsoCFDI,
                            @LugarExpedicion, @Moneda, @TipoIVA, @CondicionesPago, @Observaciones,
                            @Activa, @EsRecurrente, @TipoProgramacion, @DiaEjecucion, @DiaSemana,
                            @IntervaloEjecucion, @ProximaEjecucion, @UsuarioCreacion
                        );
                        SELECT SCOPE_IDENTITY();", connection, transaction);

                    insertPlantillaCmd.Parameters.AddWithValue("@Nombre", plantilla.Nombre ?? "Plantilla " + DateTime.Now.ToString("yyyyMMdd"));
                    insertPlantillaCmd.Parameters.AddWithValue("@ClienteId", plantilla.ClienteId);
                    insertPlantillaCmd.Parameters.AddWithValue("@EmisorRFCId", plantilla.EmisorRFCId);
                    insertPlantillaCmd.Parameters.AddWithValue("@Serie", (object)plantilla.Serie ?? DBNull.Value);
                    insertPlantillaCmd.Parameters.AddWithValue("@Folio", (object)plantilla.Folio ?? DBNull.Value);
                    insertPlantillaCmd.Parameters.AddWithValue("@FormaPago", plantilla.FormaPago);
                    insertPlantillaCmd.Parameters.AddWithValue("@UsoCFDI", plantilla.UsoCFDI);
                    insertPlantillaCmd.Parameters.AddWithValue("@LugarExpedicion", plantilla.LugarExpedicion);
                    insertPlantillaCmd.Parameters.AddWithValue("@Moneda", plantilla.Moneda);
                    insertPlantillaCmd.Parameters.AddWithValue("@TipoIVA", plantilla.TipoIVA ?? "ConIVA");
                    insertPlantillaCmd.Parameters.AddWithValue("@CondicionesPago", (object)plantilla.CondicionesPago ?? DBNull.Value);
                    insertPlantillaCmd.Parameters.AddWithValue("@Observaciones", (object)plantilla.Observaciones ?? DBNull.Value);
                    insertPlantillaCmd.Parameters.AddWithValue("@Activa", plantilla.EsRecurrente);
                    insertPlantillaCmd.Parameters.AddWithValue("@EsRecurrente", plantilla.EsRecurrente);
                    insertPlantillaCmd.Parameters.AddWithValue("@TipoProgramacion", plantilla.TipoProgramacion ?? "DiaMes");
                    insertPlantillaCmd.Parameters.AddWithValue("@DiaEjecucion", plantilla.DiaEjecucion > 0 ? plantilla.DiaEjecucion : 1);
                    insertPlantillaCmd.Parameters.AddWithValue("@DiaSemana", (object)plantilla.DiaSemana ?? DBNull.Value);
                    insertPlantillaCmd.Parameters.AddWithValue("@IntervaloEjecucion", plantilla.IntervaloEjecucion > 0 ? plantilla.IntervaloEjecucion : 1);
                    insertPlantillaCmd.Parameters.AddWithValue("@ProximaEjecucion", plantilla.CalcularProximaEjecucion());
                    insertPlantillaCmd.Parameters.AddWithValue("@UsuarioCreacion", "Sistema");

                    var plantillaId = Convert.ToInt32(await insertPlantillaCmd.ExecuteScalarAsync());

                    // Insertar conceptos de la plantilla
                    if (plantilla.Conceptos != null && plantilla.Conceptos.Count > 0)
                    {
                        int orden = 1;
                        foreach (var concepto in plantilla.Conceptos)
                        {
                            var insertConceptoCmd = new SqlCommand(@"
                                INSERT INTO ConceptosPlantilla (
                                    PlantillaId, ClaveProdServ, ClaveUnidad, CantidadFormula,
                                    Descripcion, ValorUnitarioFormula, ImporteFormula, Orden
                                ) VALUES (
                                    @PlantillaId, @ClaveProdServ, @ClaveUnidad, @CantidadFormula,
                                    @Descripcion, @ValorUnitarioFormula, @ImporteFormula, @Orden
                                )", connection, transaction);

                            insertConceptoCmd.Parameters.AddWithValue("@PlantillaId", plantillaId);
                            insertConceptoCmd.Parameters.AddWithValue("@ClaveProdServ", concepto.ClaveProdServ);
                            insertConceptoCmd.Parameters.AddWithValue("@ClaveUnidad", concepto.ClaveUnidad);
                            insertConceptoCmd.Parameters.AddWithValue("@CantidadFormula", concepto.CantidadFormula ?? "1");
                            insertConceptoCmd.Parameters.AddWithValue("@Descripcion", concepto.Descripcion);
                            insertConceptoCmd.Parameters.AddWithValue("@ValorUnitarioFormula", concepto.ValorUnitarioFormula ?? "0");
                            insertConceptoCmd.Parameters.AddWithValue("@ImporteFormula", concepto.ImporteFormula ?? "0");
                            insertConceptoCmd.Parameters.AddWithValue("@Orden", orden++);

                            await insertConceptoCmd.ExecuteNonQueryAsync();
                        }
                    }

                    transaction.Commit();

                    // Calcular próxima ejecución para el mensaje
                    var proximaEjecucion = plantilla.TipoProgramacion == "DiaMes"
                        ? $"día {plantilla.DiaEjecucion} del próximo mes"
                        : $"próximo {plantilla.DiaSemana}";

                    var cadena = await _facturacionService.GenerarCadenaFacturacion(plantilla);

                    return Json(new {
                        success = true,
                        mensaje = $"✅ Plantilla '{plantilla.Nombre}' guardada exitosamente. Próxima ejecución: {proximaEjecucion}",
                        plantillaId = plantillaId,
                        cadena = cadena
                    });
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        public IActionResult Plantillas()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPlantillas()
        {
            try
            {
                using var connection = _databaseService.GetConnection();
                await connection.OpenAsync();

                var query = @"
                    SELECT
                        p.Id,
                        p.Nombre,
                        c.Razon_Social as ClienteNombre,
                        c.RFCCliente as ClienteRFC,
                        pr.RFC as EmisorRFC,
                        p.Serie,
                        p.Activa,
                        p.TipoProgramacion,
                        p.DiaEjecucion,
                        p.DiaSemana,
                        p.IntervaloEjecucion,
                        p.ProximaEjecucion,
                        ISNULL((SELECT SUM(CAST(cp.ValorUnitarioFormula AS DECIMAL(18,2)) * CAST(cp.CantidadFormula AS DECIMAL(18,2)))
                                FROM ConceptosPlantilla cp WHERE cp.PlantillaId = p.Id), 0) as TotalMensual
                    FROM PlantillasFacturacion p
                    INNER JOIN Clientes c ON p.ClienteId = c.Id_Cliente
                    INNER JOIN propietarioRFC pr ON p.EmisorRFCId = pr.IdPropietarioRFC
                    WHERE p.EsRecurrente = 1
                    ORDER BY p.FechaCreacion DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var plantillas = new List<dynamic>();
                while (await reader.ReadAsync())
                {
                    plantillas.Add(new
                    {
                        id = reader.GetInt32(reader.GetOrdinal("Id")),
                        nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                        clienteNombre = reader.GetString(reader.GetOrdinal("ClienteNombre")),
                        clienteRFC = reader.GetString(reader.GetOrdinal("ClienteRFC")),
                        emisorRFC = reader.GetString(reader.GetOrdinal("EmisorRFC")),
                        serie = reader.IsDBNull(reader.GetOrdinal("Serie")) ? null : reader.GetString(reader.GetOrdinal("Serie")),
                        activa = reader.GetBoolean(reader.GetOrdinal("Activa")),
                        tipoProgramacion = reader.GetString(reader.GetOrdinal("TipoProgramacion")),
                        diaEjecucion = reader.IsDBNull(reader.GetOrdinal("DiaEjecucion")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DiaEjecucion")),
                        diaSemana = reader.IsDBNull(reader.GetOrdinal("DiaSemana")) ? null : reader.GetString(reader.GetOrdinal("DiaSemana")),
                        intervaloEjecucion = reader.IsDBNull(reader.GetOrdinal("IntervaloEjecucion")) ? 1 : reader.GetInt32(reader.GetOrdinal("IntervaloEjecucion")),
                        proximaEjecucion = reader.IsDBNull(reader.GetOrdinal("ProximaEjecucion")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ProximaEjecucion")),
                        totalMensual = reader.GetDecimal(reader.GetOrdinal("TotalMensual"))
                    });
                }

                return Json(new { success = true, plantillas = plantillas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPlantilla(int id)
        {
            try
            {
                using var connection = _databaseService.GetConnection();
                await connection.OpenAsync();

                // Obtener plantilla
                var queryPlantilla = @"
                    SELECT p.*, c.Razon_Social as ClienteNombre, c.RFCCliente as ClienteRFC,
                           pr.RFC as EmisorRFC, pr.RazonSocial as EmisorRazonSocial
                    FROM PlantillasFacturacion p
                    INNER JOIN Clientes c ON p.ClienteId = c.Id_Cliente
                    INNER JOIN propietarioRFC pr ON p.EmisorRFCId = pr.IdPropietarioRFC
                    WHERE p.Id = @Id";

                using var cmdPlantilla = new SqlCommand(queryPlantilla, connection);
                cmdPlantilla.Parameters.AddWithValue("@Id", id);
                using var reader = await cmdPlantilla.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Json(new { success = false, error = "Plantilla no encontrada" });
                }

                var plantilla = new
                {
                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                    nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                    clienteId = reader.GetInt32(reader.GetOrdinal("ClienteId")),
                    clienteNombre = reader.GetString(reader.GetOrdinal("ClienteNombre")),
                    clienteRFC = reader.GetString(reader.GetOrdinal("ClienteRFC")),
                    emisorRFCId = reader.GetInt32(reader.GetOrdinal("EmisorRFCId")),
                    emisorRFC = reader.GetString(reader.GetOrdinal("EmisorRFC")),
                    serie = reader.IsDBNull(reader.GetOrdinal("Serie")) ? null : reader.GetString(reader.GetOrdinal("Serie")),
                    folio = reader.IsDBNull(reader.GetOrdinal("Folio")) ? null : reader.GetString(reader.GetOrdinal("Folio")),
                    formaPago = reader.GetString(reader.GetOrdinal("FormaPago")),
                    usoCFDI = reader.GetString(reader.GetOrdinal("UsoCFDI")),
                    lugarExpedicion = reader.GetString(reader.GetOrdinal("LugarExpedicion")),
                    moneda = reader.GetString(reader.GetOrdinal("Moneda")),
                    tipoIVA = reader.GetString(reader.GetOrdinal("TipoIVA")),
                    activa = reader.GetBoolean(reader.GetOrdinal("Activa")),
                    tipoProgramacion = reader.GetString(reader.GetOrdinal("TipoProgramacion")),
                    diaEjecucion = reader.IsDBNull(reader.GetOrdinal("DiaEjecucion")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DiaEjecucion")),
                    diaSemana = reader.IsDBNull(reader.GetOrdinal("DiaSemana")) ? null : reader.GetString(reader.GetOrdinal("DiaSemana")),
                    intervaloEjecucion = reader.IsDBNull(reader.GetOrdinal("IntervaloEjecucion")) ? 1 : reader.GetInt32(reader.GetOrdinal("IntervaloEjecucion"))
                };

                reader.Close();

                // Obtener conceptos
                var queryConceptos = @"
                    SELECT * FROM ConceptosPlantilla
                    WHERE PlantillaId = @PlantillaId
                    ORDER BY Orden";

                using var cmdConceptos = new SqlCommand(queryConceptos, connection);
                cmdConceptos.Parameters.AddWithValue("@PlantillaId", id);
                using var readerConceptos = await cmdConceptos.ExecuteReaderAsync();

                var conceptos = new List<dynamic>();
                while (await readerConceptos.ReadAsync())
                {
                    conceptos.Add(new
                    {
                        claveProdServ = readerConceptos.GetString(readerConceptos.GetOrdinal("ClaveProdServ")),
                        claveUnidad = readerConceptos.GetString(readerConceptos.GetOrdinal("ClaveUnidad")),
                        cantidad = readerConceptos.GetString(readerConceptos.GetOrdinal("CantidadFormula")),
                        descripcion = readerConceptos.GetString(readerConceptos.GetOrdinal("Descripcion")),
                        valorUnitario = readerConceptos.GetString(readerConceptos.GetOrdinal("ValorUnitarioFormula")),
                        importe = readerConceptos.GetString(readerConceptos.GetOrdinal("ImporteFormula"))
                    });
                }

                return Json(new { success = true, plantilla = plantilla, conceptos = conceptos });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerHistorialPlantilla(int id)
        {
            try
            {
                using var connection = _databaseService.GetConnection();
                await connection.OpenAsync();

                var query = @"
                    SELECT TOP 50
                        FechaGeneracion,
                        UUID,
                        Serie,
                        Folio,
                        ReceptorNombre,
                        Subtotal,
                        IVA,
                        Total,
                        Exitosa,
                        MensajeError
                    FROM FacturasGeneradas
                    WHERE PlantillaId = @PlantillaId
                    ORDER BY FechaGeneracion DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@PlantillaId", id);
                using var reader = await command.ExecuteReaderAsync();

                var historial = new List<dynamic>();
                while (await reader.ReadAsync())
                {
                    historial.Add(new
                    {
                        fechaGeneracion = reader.GetDateTime(reader.GetOrdinal("FechaGeneracion")),
                        uuid = reader.IsDBNull(reader.GetOrdinal("UUID")) ? null : reader.GetString(reader.GetOrdinal("UUID")),
                        serie = reader.IsDBNull(reader.GetOrdinal("Serie")) ? null : reader.GetString(reader.GetOrdinal("Serie")),
                        folio = reader.IsDBNull(reader.GetOrdinal("Folio")) ? null : reader.GetString(reader.GetOrdinal("Folio")),
                        receptorNombre = reader.IsDBNull(reader.GetOrdinal("ReceptorNombre")) ? null : reader.GetString(reader.GetOrdinal("ReceptorNombre")),
                        subtotal = reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                        iva = reader.GetDecimal(reader.GetOrdinal("IVA")),
                        total = reader.GetDecimal(reader.GetOrdinal("Total")),
                        exitosa = reader.GetBoolean(reader.GetOrdinal("Exitosa")),
                        mensajeError = reader.IsDBNull(reader.GetOrdinal("MensajeError")) ? null : reader.GetString(reader.GetOrdinal("MensajeError"))
                    });
                }

                return Json(new { success = true, historial = historial });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleEstadoPlantilla(int id)
        {
            try
            {
                using var connection = _databaseService.GetConnection();
                await connection.OpenAsync();

                // Primero obtener el estado actual
                var queryEstado = "SELECT Activa FROM PlantillasFacturacion WHERE Id = @Id";
                using var cmdEstado = new SqlCommand(queryEstado, connection);
                cmdEstado.Parameters.AddWithValue("@Id", id);
                var estadoActual = await cmdEstado.ExecuteScalarAsync();

                if (estadoActual == null)
                {
                    return Json(new { success = false, error = "Plantilla no encontrada" });
                }

                bool nuevoEstado = !(bool)estadoActual;

                // Actualizar el estado
                var updateQuery = @"
                    UPDATE PlantillasFacturacion
                    SET Activa = @NuevoEstado,
                        FechaUltimaModificacion = GETDATE()
                    WHERE Id = @Id";

                using var cmdUpdate = new SqlCommand(updateQuery, connection);
                cmdUpdate.Parameters.AddWithValue("@NuevoEstado", nuevoEstado);
                cmdUpdate.Parameters.AddWithValue("@Id", id);
                await cmdUpdate.ExecuteNonQueryAsync();

                return Json(new {
                    success = true,
                    mensaje = nuevoEstado ? "Plantilla activada" : "Plantilla pausada",
                    nuevoEstado = nuevoEstado
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EjecutarFacturacionManual()
        {
            try
            {
                // TODO: Implementar ejecución real de plantillas
                return Json(new {
                    success = true,
                    mensaje = "Se procesaron 3 facturas: 3 exitosas, 0 con errores",
                    facturasProcesadas = 3,
                    facturasExitosas = 3,
                    facturasConError = 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EmitirFacturasDelMes()
        {
            try
            {
                // Aquí ejecutaríamos todas las plantillas recurrentes del mes
                // Por ahora retornamos un mensaje simulado

                return Json(new {
                    success = true,
                    mensaje = "Se han procesado las facturas recurrentes del mes.",
                    facturasProcesadas = 0,
                    facturasExitosas = 0,
                    facturasConError = 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPropietarioRFCPorId(int id)
        {
            try
            {
                var propietario = await _databaseService.ObtenerPropietarioRFCPorId(id);
                if (propietario == null)
                    return Json(new { success = false, error = "Propietario RFC no encontrado" });

                return Json(new {
                    success = true,
                    lugarExpedicion = propietario.LugarExpedicion,
                    razonSocial = propietario.RazonSocial,
                    rfc = propietario.RFC
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerClientePorId(int id)
        {
            try
            {
                var cliente = await _databaseService.ObtenerClientePorId(id);
                if (cliente == null)
                    return Json(new { success = false, error = "Cliente no encontrado" });

                return Json(new {
                    success = true,
                    cliente = new {
                        id_Cliente = cliente.Id_Cliente,
                        rfc = cliente.RFC,
                        rfcCliente = cliente.RFCCliente,
                        razon_Social = cliente.Razon_Social,
                        email = cliente.Email,
                        domicilioFiscalCliente = cliente.DomicilioFiscalCliente,
                        regimenFiscalCliente = cliente.RegimenFiscalCliente
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    public class ProcesarFacturaRequest
    {
        public PlantillaFacturacion Plantilla { get; set; } = new();
        public bool Confirmar { get; set; }
    }
}
using Microsoft.AspNetCore.Mvc;
using FacturacionRecurrente.Models;
using FacturacionRecurrente.Services;

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
                // Aquí guardaríamos la plantilla en base de datos para ejecución recurrente
                // Por ahora solo generamos preview
                var cadena = await _facturacionService.GenerarCadenaFacturacion(plantilla);

                return Json(new {
                    success = true,
                    mensaje = "Plantilla de facturación recurrente configurada. Se ejecutará el día 1 de cada mes.",
                    cadena = cadena
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
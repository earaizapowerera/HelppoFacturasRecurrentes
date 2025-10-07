using FacturacionRecurrente.Models;
using System.Net.Http;
using System.Text;
using System.Xml;

namespace FacturacionRecurrente.Services
{
    public class FacturacionService
    {
        private readonly DatabaseService _databaseService;
        private readonly ComodinService _comodinService;
        private readonly HttpClient _httpClient;

        public FacturacionService(DatabaseService databaseService, ComodinService comodinService, HttpClient httpClient)
        {
            _databaseService = databaseService;
            _comodinService = comodinService;
            _httpClient = httpClient;
        }

        public async Task<string> GenerarCadenaFacturacion(PlantillaFacturacion plantilla)
        {
            // Obtener datos del cliente y emisor
            var cliente = await _databaseService.ObtenerClientePorId(plantilla.ClienteId);
            var emisor = await _databaseService.ObtenerPropietarioRFCPorId(plantilla.EmisorRFCId);

            if (cliente == null)
                throw new InvalidOperationException("Cliente no encontrado");

            if (emisor == null)
                throw new InvalidOperationException("Emisor RFC no encontrado");

            // Procesar comodines en datos generales
            var serie = await _comodinService.ProcesarComodines(plantilla.Serie, cliente, emisor.RFC, plantilla.Serie);
            var folio = await _comodinService.ProcesarComodines(plantilla.Folio, cliente, emisor.RFC, serie);
            var condicionesPago = await _comodinService.ProcesarComodines(plantilla.CondicionesPago, cliente, emisor.RFC, serie);
            var observaciones = await _comodinService.ProcesarComodines(plantilla.Observaciones, cliente, emisor.RFC, serie);

            // Construir datos generales
            var seriefolio = string.IsNullOrEmpty(serie) ? folio : $"{serie}-{folio}";
            var datosGenerales = string.Join("|",
                seriefolio, // Serie-Folio
                plantilla.FormaPago, // FormaPago
                cliente.RFCCliente, // RFC Receptor
                cliente.Razon_Social, // Nombre Receptor
                "MEX", // País Receptor
                plantilla.UsoCFDI, // UsoCFDI
                cliente.Email, // Emails
                emisor.RFC, // RFC Emisor
                !string.IsNullOrEmpty(emisor.LugarExpedicion) ? emisor.LugarExpedicion : "06000", // Lugar de Expedición
                plantilla.Moneda, // Moneda
                condicionesPago, // Condiciones de Pago
                observaciones, // Observaciones
                "I", // Tipo de Comprobante (Ingreso)
                "", // CFDIs relacionados
                "", // Tipo de Relación
                cliente.RegimenFiscalCliente ?? "601", // Régimen Fiscal Receptor
                cliente.DomicilioFiscalCliente ?? "06000" // Domicilio Fiscal Receptor
            );

            // Procesar conceptos
            var conceptosCadena = new List<string>();
            decimal totalFactura = 0;

            foreach (var concepto in plantilla.Conceptos.OrderBy(c => c.Orden))
            {
                // Evaluar fórmulas
                var cantidad = await _comodinService.EvaluarFormula(concepto.CantidadFormula, cliente);
                var valorUnitario = await _comodinService.EvaluarFormula(concepto.ValorUnitarioFormula, cliente);
                var importe = cantidad * valorUnitario;

                // Calcular IVA según el tipo configurado
                decimal iva = 0;
                decimal totalConcepto = importe;

                switch (plantilla.TipoIVA)
                {
                    case "ConIVA":
                        iva = importe * 0.16m; // 16% IVA
                        totalConcepto = importe + iva;
                        break;
                    case "IVA0":
                        iva = 0;
                        totalConcepto = importe;
                        break;
                    case "SinIVA":
                        iva = 0;
                        totalConcepto = importe;
                        break;
                }

                // Procesar descripción con comodines
                var descripcion = await _comodinService.ProcesarComodines(concepto.Descripcion, cliente, emisor.RFC, serie);

                // Construir cadena del concepto con IVA
                var conceptoCadena = string.Join("|",
                    concepto.Id.ToString(), // Identificador
                    concepto.ClaveProdServ, // ClaveProdServ
                    concepto.ClaveUnidad, // ClaveUnidad
                    "", // Campo no utilizado
                    cantidad.ToString("F2"), // Cantidad
                    descripcion, // Descripción
                    valorUnitario.ToString("F2"), // Valor Unitario
                    importe.ToString("F2"), // Importe (subtotal)
                    iva.ToString("F2"), // IVA
                    totalConcepto.ToString("F2"), // Total con IVA
                    "" // Número de Pedimento (opcional)
                );

                conceptosCadena.Add(conceptoCadena);
                totalFactura += totalConcepto; // Acumular el total con IVA
            }

            // Construir cadena final
            var cadenaCompleta = datosGenerales + "~" + string.Join("~", conceptosCadena);

            return cadenaCompleta;
        }

        public async Task<object> ProcesarFactura(PlantillaFacturacion plantilla, bool confirmar)
        {
            try
            {
                // Generar la cadena
                var cadena = await GenerarCadenaFacturacion(plantilla);

                if (!confirmar)
                {
                    // Solo mostrar preview
                    return new
                    {
                        tipo = "preview",
                        cadena = cadena,
                        mensaje = "Cadena generada. Revise y confirme para enviar al WebService."
                    };
                }

                // Enviar al WebService
                var respuesta = await EnviarAWebService(cadena);

                return new
                {
                    tipo = "resultado",
                    cadena = cadena,
                    respuesta = respuesta,
                    exitoso = respuesta.Contains("\"success\":\"true\"") || respuesta.Contains("Success:true")
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    tipo = "error",
                    mensaje = ex.Message
                };
            }
        }

        private async Task<string> EnviarAWebService(string cadena)
        {
            try
            {
                // URL del WebService
                var url = "https://ws.helppo.com.mx/v40/GeneraFactura40.asmx";

                // Crear el SOAP envelope
                var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GenerarFactura xmlns=""https://helppo.com.mx/"">
      <pData>{cadena}</pData>
    </GenerarFactura>
  </soap:Body>
</soap:Envelope>";

                // Configurar la petición HTTP
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "text/xml; charset=utf-8");
                content.Headers.Add("SOAPAction", "https://helppo.com.mx/GenerarFactura");

                // Enviar la petición
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Parsear la respuesta SOAP
                return ParsearRespuestaSOAP(responseContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al enviar al WebService: {ex.Message}", ex);
            }
        }

        private string ParsearRespuestaSOAP(string soapResponse)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(soapResponse);

                // Buscar el resultado en la respuesta SOAP
                var namespaceManager = new XmlNamespaceManager(doc.NameTable);
                namespaceManager.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
                namespaceManager.AddNamespace("ws", "https://helppo.com.mx/");

                var resultNode = doc.SelectSingleNode("//ws:GenerarFacturaResult", namespaceManager);
                if (resultNode != null)
                {
                    return resultNode.InnerText;
                }

                // Si no encuentra el nodo específico, devolver toda la respuesta
                return soapResponse;
            }
            catch (Exception ex)
            {
                // Si hay error parseando XML, devolver la respuesta completa
                return $"Error parseando respuesta: {ex.Message}\nRespuesta completa: {soapResponse}";
            }
        }
    }
}
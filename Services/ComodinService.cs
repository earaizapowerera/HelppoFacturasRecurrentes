using System.Globalization;
using System.Text.RegularExpressions;
using FacturacionRecurrente.Models;

namespace FacturacionRecurrente.Services
{
    public class ComodinService
    {
        private readonly DatabaseService _databaseService;

        public ComodinService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<string> ProcesarComodines(string texto, Cliente? cliente = null, string? rfcEmisor = null, string? serie = null)
        {
            var fechaActual = DateTime.Now;
            var resultado = texto;

            // Comodines de fecha
            resultado = resultado.Replace("{fechaactual}", fechaActual.ToString("dd/MM/yyyy"));
            resultado = resultado.Replace("{mesactual}", fechaActual.Month.ToString("00"));
            resultado = resultado.Replace("{añoactual}", fechaActual.Year.ToString());
            resultado = resultado.Replace("{anoactual}", fechaActual.Year.ToString()); // Variante sin ñ

            // Días específicos del mes
            var primerDiaMes = new DateTime(fechaActual.Year, fechaActual.Month, 1);
            var ultimoDiaMes = primerDiaMes.AddMonths(1).AddDays(-1);
            resultado = resultado.Replace("{dia1_mes}", primerDiaMes.ToString("dd/MM/yyyy"));
            resultado = resultado.Replace("{ultimo_dia_mes}", ultimoDiaMes.ToString("dd/MM/yyyy"));

            // Tipo de cambio
            var tipoCambio = await _databaseService.ObtenerTipoCambioActual();
            resultado = resultado.Replace("{tcfixed}", tipoCambio.ToString("F4", CultureInfo.InvariantCulture));
            resultado = resultado.Replace("{tc}", tipoCambio.ToString("F4", CultureInfo.InvariantCulture));

            // Siguiente folio
            if (!string.IsNullOrEmpty(rfcEmisor) && !string.IsNullOrEmpty(serie))
            {
                var siguienteFolio = await _databaseService.ObtenerSiguienteFolio(rfcEmisor, serie);
                resultado = resultado.Replace("{SiguienteFolio}", siguienteFolio.ToString());
            }

            // Datos del cliente si se proporciona
            if (cliente != null)
            {
                resultado = resultado.Replace("{cliente_rfc}", cliente.RFCCliente);
                resultado = resultado.Replace("{cliente_nombre}", cliente.Razon_Social);
                resultado = resultado.Replace("{cliente_email}", cliente.Email);
                resultado = resultado.Replace("{cliente_regimen}", cliente.RegimenFiscalCliente ?? "");
                resultado = resultado.Replace("{cliente_cp}", cliente.DomicilioFiscalCliente ?? "");
            }

            // Comodines de mes en texto
            var meses = new Dictionary<string, string>
            {
                {"{mes_texto}", fechaActual.ToString("MMMM", new CultureInfo("es-ES"))},
                {"{mes_texto_mayus}", fechaActual.ToString("MMMM", new CultureInfo("es-ES")).ToUpper()},
                {"{mes_anterior_texto}", fechaActual.AddMonths(-1).ToString("MMMM", new CultureInfo("es-ES"))},
                {"{mes_siguiente_texto}", fechaActual.AddMonths(1).ToString("MMMM", new CultureInfo("es-ES"))}
            };

            foreach (var mes in meses)
            {
                resultado = resultado.Replace(mes.Key, mes.Value);
            }

            // Comodín SiguienteFolio - requiere RFC y serie
            if (resultado.Contains("{SiguienteFolio}") && !string.IsNullOrEmpty(rfcEmisor) && !string.IsNullOrEmpty(serie))
            {
                var siguienteFolio = await _databaseService.ObtenerSiguienteFolio(rfcEmisor, serie);
                resultado = resultado.Replace("{SiguienteFolio}", siguienteFolio.ToString("000"));
            }

            return resultado;
        }

        public async Task<decimal> EvaluarFormula(string formula, Cliente? cliente = null, Dictionary<string, decimal>? variables = null)
        {
            try
            {
                // Procesar comodines primero
                var formulaProcesada = await ProcesarComodines(formula, cliente);

                // Agregar variables adicionales si se proporcionan
                if (variables != null)
                {
                    foreach (var variable in variables)
                    {
                        formulaProcesada = formulaProcesada.Replace($"{{{variable.Key}}}", variable.Value.ToString(CultureInfo.InvariantCulture));
                    }
                }

                // Evaluar la expresión matemática
                return EvaluarExpresionMatematica(formulaProcesada);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error evaluando fórmula '{formula}': {ex.Message}", ex);
            }
        }

        private decimal EvaluarExpresionMatematica(string expresion)
        {
            // Remover espacios
            expresion = expresion.Replace(" ", "");

            // Si es solo un número, devolverlo directamente
            if (decimal.TryParse(expresion, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal numero))
            {
                return numero;
            }

            // Evaluar operaciones básicas usando regex
            // Soporta: +, -, *, /, paréntesis
            return EvaluarExpresionRecursiva(expresion);
        }

        private decimal EvaluarExpresionRecursiva(string expresion)
        {
            // Manejar paréntesis primero
            while (expresion.Contains('('))
            {
                var match = Regex.Match(expresion, @"\(([^()]+)\)");
                if (match.Success)
                {
                    var subExpresion = match.Groups[1].Value;
                    var subResultado = EvaluarExpresionRecursiva(subExpresion);
                    expresion = expresion.Replace(match.Value, subResultado.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    break;
                }
            }

            // Evaluar multiplicación y división primero
            expresion = EvaluarOperadores(expresion, new[] { "*", "/" });

            // Luego suma y resta
            expresion = EvaluarOperadores(expresion, new[] { "+", "-" });

            if (decimal.TryParse(expresion, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal resultado))
            {
                return resultado;
            }

            throw new InvalidOperationException($"No se pudo evaluar la expresión: {expresion}");
        }

        private string EvaluarOperadores(string expresion, string[] operadores)
        {
            foreach (var operador in operadores)
            {
                var pattern = operador == "*" ? @"(\d+(?:\.\d+)?)\*(\d+(?:\.\d+)?)" :
                             operador == "/" ? @"(\d+(?:\.\d+)?)\/(\d+(?:\.\d+)?)" :
                             operador == "+" ? @"(\d+(?:\.\d+)?)\+(\d+(?:\.\d+)?)" :
                             @"(\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)";

                while (Regex.IsMatch(expresion, pattern))
                {
                    expresion = Regex.Replace(expresion, pattern, match =>
                    {
                        var num1 = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                        var num2 = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

                        decimal resultado = operador switch
                        {
                            "*" => num1 * num2,
                            "/" => num2 != 0 ? num1 / num2 : throw new DivideByZeroException(),
                            "+" => num1 + num2,
                            "-" => num1 - num2,
                            _ => throw new InvalidOperationException($"Operador no soportado: {operador}")
                        };

                        return resultado.ToString(CultureInfo.InvariantCulture);
                    });
                }
            }

            return expresion;
        }

        public List<string> ObtenerComodinesDisponibles()
        {
            return new List<string>
            {
                "{fechaactual}", "{mesactual}", "{añoactual}", "{anoactual}",
                "{dia1_mes}", "{ultimo_dia_mes}",
                "{tcfixed}", "{tc}",
                "{SiguienteFolio}",
                "{cliente_rfc}", "{cliente_nombre}", "{cliente_email}", "{cliente_regimen}", "{cliente_cp}",
                "{mes_texto}", "{mes_texto_mayus}", "{mes_anterior_texto}", "{mes_siguiente_texto}"
            };
        }

        public string GenerarEjemploFormula()
        {
            return "Ejemplos de fórmulas:\n" +
                   "• Precio fijo: 1000\n" +
                   "• Con tipo de cambio: 600*{tcfixed}\n" +
                   "• Con operaciones: (500+100)*{tcfixed}\n" +
                   "• Descripción: Servicios de {mes_texto} {añoactual}\n" +
                   "• Cliente: Factura para {cliente_nombre} ({cliente_rfc})";
        }
    }
}
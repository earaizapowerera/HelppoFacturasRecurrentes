namespace FacturacionRecurrente.Models
{
    public class Cliente
    {
        public int Id_Cliente { get; set; }
        public string RFC { get; set; } = string.Empty;
        public string RFCCliente { get; set; } = string.Empty;
        public string Razon_Social { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DomicilioFiscalCliente { get; set; }
        public string? RegimenFiscalCliente { get; set; }
    }

    public class TipoCambioDOF
    {
        public DateTime Fecha { get; set; }
        public decimal TipoCambio { get; set; }
    }

    public class CatalogoSAT
    {
        public string Codigo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
    }

    public class PropietarioRFC
    {
        public int IdPropietarioRFC { get; set; }
        public string RFC { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public bool Status { get; set; }
        public string RegimenFiscal { get; set; } = string.Empty;
        public string LugarExpedicion { get; set; } = string.Empty;
    }

    public class PlantillaFacturacion
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int ClienteId { get; set; }
        public int EmisorRFCId { get; set; } // Referencia a PropietarioRFC
        public string Serie { get; set; } = string.Empty;
        public string Folio { get; set; } = string.Empty; // Separado de Serie
        public string FormaPago { get; set; } = string.Empty;
        public string UsoCFDI { get; set; } = string.Empty;
        public string LugarExpedicion { get; set; } = string.Empty;
        public string Moneda { get; set; } = string.Empty;
        public string TipoIVA { get; set; } = "ConIVA"; // ConIVA, SinIVA, IVA0
        public string CondicionesPago { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaUltimaEjecucion { get; set; } // Para facturación recurrente
        public bool EsRecurrente { get; set; } = true; // Siempre recurrente
        public string TipoProgramacion { get; set; } = "DiaMes"; // DiaMes, DiaSemana
        public int DiaEjecucion { get; set; } = 1; // Día 1 de cada mes
        public int IntervaloEjecucion { get; set; } = 1; // Cada X días/semanas
        public string DiaSemana { get; set; } = "Martes"; // Para programación semanal
        public List<ConceptoPlantilla> Conceptos { get; set; } = new();
    }

    public class ConceptoPlantilla
    {
        public int Id { get; set; }
        public int PlantillaId { get; set; }
        public string ClaveProdServ { get; set; } = string.Empty;
        public string ClaveUnidad { get; set; } = string.Empty;
        public string CantidadFormula { get; set; } = string.Empty; // Puede contener fórmulas como "1" o "{cantidad_variable}"
        public string Descripcion { get; set; } = string.Empty; // Puede contener comodines
        public string ValorUnitarioFormula { get; set; } = string.Empty; // Fórmulas como "600*{tcfixed}"
        public string ImporteFormula { get; set; } = string.Empty; // Fórmulas como "{cantidad}*{valor_unitario}"
        public int Orden { get; set; }
    }

    public class FacturaGenerada
    {
        public int Id { get; set; }
        public int PlantillaId { get; set; }
        public string UUID { get; set; } = string.Empty;
        public string EmisorRFC { get; set; } = string.Empty;
        public string ReceptorRFC { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public string CadenaOriginal { get; set; } = string.Empty;
        public string RespuestaWebService { get; set; } = string.Empty;
        public bool Exitosa { get; set; }
    }

    public class ProductoRFC
    {
        public int IdProdServ { get; set; }
        public string RFC { get; set; } = string.Empty;
        public string CLAVESAT { get; set; } = string.Empty;
        public string CodigoInterno { get; set; } = string.Empty;
        public string DescripcionInterna { get; set; } = string.Empty;
        public string UnidadSAT { get; set; } = string.Empty;
    }
}
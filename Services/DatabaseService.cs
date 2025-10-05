using System.Data;
using System.Data.SqlClient;
using FacturacionRecurrente.Models;

namespace FacturacionRecurrente.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = "Server=helppo.com.mx;Database=helppo;User Id=uhelppo;Password=H3lpp0;TrustServerCertificate=true;Connection Timeout=30;";
        }

        public async Task<List<Cliente>> ObtenerClientes(string? filtroRFC = null)
        {
            var clientes = new List<Cliente>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var whereClause = "WHERE RFCCliente IS NOT NULL AND RFCCliente != ''";
            if (!string.IsNullOrEmpty(filtroRFC))
            {
                whereClause += " AND RFC = @filtroRFC";
            }

            var query = $@"
                SELECT TOP 50
                    Id_Cliente, RFC, RFCCliente, Razon_Social, Email,
                    DomicilioFiscalCliente, RegimenFiscalCliente
                FROM Clientes
                {whereClause}
                ORDER BY Razon_Social";

            using var command = new SqlCommand(query, connection);
            if (!string.IsNullOrEmpty(filtroRFC))
            {
                command.Parameters.AddWithValue("@filtroRFC", filtroRFC);
            }
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clientes.Add(new Cliente
                {
                    Id_Cliente = reader.GetInt32("Id_Cliente"),
                    RFC = reader.IsDBNull("RFC") ? "" : reader.GetString("RFC"),
                    RFCCliente = reader.IsDBNull("RFCCliente") ? "" : reader.GetString("RFCCliente"),
                    Razon_Social = reader.IsDBNull("Razon_Social") ? "" : reader.GetString("Razon_Social"),
                    Email = reader.IsDBNull("Email") ? "" : reader.GetString("Email"),
                    DomicilioFiscalCliente = reader.IsDBNull("DomicilioFiscalCliente") ? null : reader.GetString("DomicilioFiscalCliente"),
                    RegimenFiscalCliente = reader.IsDBNull("RegimenFiscalCliente") ? null : reader.GetString("RegimenFiscalCliente")
                });
            }

            return clientes;
        }

        public async Task<List<PropietarioRFC>> ObtenerPropietariosRFC()
        {
            var propietarios = new List<PropietarioRFC>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT
                    p.IdPropietarioRFC, p.RFC, p.RazonSocial, p.Correo, p.Status,
                    p.RegimenFiscal, p.LugarExpedicion
                FROM propietarioRFC p
                INNER JOIN Settings_RFC s ON p.RFC = s.RFC
                WHERE p.Status = 1
                ORDER BY p.RazonSocial";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                propietarios.Add(new PropietarioRFC
                {
                    IdPropietarioRFC = reader.GetInt32("IdPropietarioRFC"),
                    RFC = reader.IsDBNull("RFC") ? "" : reader.GetString("RFC"),
                    RazonSocial = reader.IsDBNull("RazonSocial") ? "" : reader.GetString("RazonSocial"),
                    Correo = reader.IsDBNull("Correo") ? "" : reader.GetString("Correo"),
                    Status = reader.GetBoolean("Status"),
                    RegimenFiscal = reader.IsDBNull("RegimenFiscal") ? "" : reader.GetString("RegimenFiscal"),
                    LugarExpedicion = reader.IsDBNull("LugarExpedicion") ? "" : reader.GetString("LugarExpedicion")
                });
            }

            return propietarios;
        }

        public async Task<Cliente?> ObtenerClientePorId(int clienteId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT
                    Id_Cliente, RFC, RFCCliente, Razon_Social, Email,
                    DomicilioFiscalCliente, RegimenFiscalCliente
                FROM Clientes
                WHERE Id_Cliente = @clienteId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@clienteId", clienteId);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Cliente
                {
                    Id_Cliente = reader.GetInt32("Id_Cliente"),
                    RFC = reader.IsDBNull("RFC") ? "" : reader.GetString("RFC"),
                    RFCCliente = reader.IsDBNull("RFCCliente") ? "" : reader.GetString("RFCCliente"),
                    Razon_Social = reader.IsDBNull("Razon_Social") ? "" : reader.GetString("Razon_Social"),
                    Email = reader.IsDBNull("Email") ? "" : reader.GetString("Email"),
                    DomicilioFiscalCliente = reader.IsDBNull("DomicilioFiscalCliente") ? null : reader.GetString("DomicilioFiscalCliente"),
                    RegimenFiscalCliente = reader.IsDBNull("RegimenFiscalCliente") ? null : reader.GetString("RegimenFiscalCliente")
                };
            }

            return null;
        }

        public async Task<PropietarioRFC?> ObtenerPropietarioRFCPorId(int propietarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT
                    IdPropietarioRFC, RFC, RazonSocial, Correo, Status,
                    RegimenFiscal, LugarExpedicion
                FROM propietarioRFC
                WHERE IdPropietarioRFC = @propietarioId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@propietarioId", propietarioId);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new PropietarioRFC
                {
                    IdPropietarioRFC = reader.GetInt32("IdPropietarioRFC"),
                    RFC = reader.GetString("RFC") ?? "",
                    RazonSocial = reader.IsDBNull("RazonSocial") ? "" : reader.GetString("RazonSocial"),
                    Correo = reader.GetString("Correo") ?? "",
                    Status = reader.GetBoolean("Status"),
                    RegimenFiscal = reader.IsDBNull("RegimenFiscal") ? "" : reader.GetString("RegimenFiscal"),
                    LugarExpedicion = reader.IsDBNull("LugarExpedicion") ? "" : reader.GetString("LugarExpedicion")
                };
            }

            return null;
        }

        public async Task<decimal> ObtenerTipoCambioActual()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT TOP 1 TipoCambio
                FROM TipoCambioDOF
                ORDER BY Fecha DESC";

            using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();

            return result != null ? Convert.ToDecimal(result) : 18.0m; // Valor por defecto
        }

        public async Task<List<CatalogoSAT>> ObtenerCatalogo(string nombreTabla)
        {
            var catalogo = new List<CatalogoSAT>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Determinar las columnas según la tabla
            var (codigoCol, descripcionCol) = nombreTabla switch
            {
                "c_FormaPago" => ("c_FormaPago", "Descripcion"),
                "c_UsoCFDI" => ("c_UsoCFDI", "Descripcion"),
                "c_Moneda" => ("c_Moneda", "Descripcion"),
                "c_ClaveProdServ" => ("c_ClaveProdServ", "Descripcion"),
                "c_ClaveUnidad" => ("c_ClaveUnidad", "Descripcion"),
                "c_RegimenFiscal" => ("c_RegimenFiscal", "Descripcion"),
                _ => ("Codigo", "Descripcion")
            };

            var query = $@"
                SELECT TOP 100
                    {codigoCol} as Codigo,
                    {descripcionCol} as Descripcion
                FROM {nombreTabla}
                ORDER BY {codigoCol}";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                catalogo.Add(new CatalogoSAT
                {
                    Codigo = reader.IsDBNull("Codigo") ? "" : reader.GetString("Codigo"),
                    Descripcion = reader.IsDBNull("Descripcion") ? "" : reader.GetString("Descripcion")
                });
            }

            // Reordenar monedas si es el catálogo de monedas
            if (nombreTabla == "c_Moneda")
            {
                var monedaOrdenada = new List<CatalogoSAT>();

                // Primero MXN, USD, EUR
                var prioridades = new[] { "MXN", "USD", "EUR" };
                foreach (var prioridad in prioridades)
                {
                    var moneda = catalogo.FirstOrDefault(m => m.Codigo == prioridad);
                    if (moneda != null)
                    {
                        monedaOrdenada.Add(moneda);
                        catalogo.Remove(moneda);
                    }
                }

                // Luego las demás en orden alfabético
                monedaOrdenada.AddRange(catalogo.OrderBy(m => m.Codigo));
                catalogo = monedaOrdenada;
            }

            return catalogo;
        }

        public async Task<int> ObtenerSiguienteFolio(string rfcEmisor, string serie)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT ISNULL(MAX(Folio), 0) + 1 as SiguienteFolio
                FROM hp_FoliosRFC
                WHERE RFC = @rfc AND Serie = @serie";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@rfc", rfcEmisor);
            command.Parameters.AddWithValue("@serie", serie ?? "");

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 1;
        }

        public async Task<List<Cliente>> BuscarClientes(string busqueda, string? filtroRFC = null)
        {
            var clientes = new List<Cliente>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var whereClause = @"WHERE RFCCliente IS NOT NULL AND RFCCliente != ''
                               AND (Razon_Social LIKE @busqueda OR RFCCliente LIKE @busqueda)";

            if (!string.IsNullOrEmpty(filtroRFC))
            {
                whereClause += " AND RFC = @filtroRFC";
            }

            var query = $@"
                SELECT TOP 20
                    Id_Cliente, RFC, RFCCliente, Razon_Social, Email,
                    DomicilioFiscalCliente, RegimenFiscalCliente
                FROM Clientes
                {whereClause}
                ORDER BY Razon_Social";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@busqueda", $"%{busqueda}%");
            if (!string.IsNullOrEmpty(filtroRFC))
            {
                command.Parameters.AddWithValue("@filtroRFC", filtroRFC);
            }

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clientes.Add(new Cliente
                {
                    Id_Cliente = reader.GetInt32("Id_Cliente"),
                    RFC = reader.IsDBNull("RFC") ? "" : reader.GetString("RFC"),
                    RFCCliente = reader.IsDBNull("RFCCliente") ? "" : reader.GetString("RFCCliente"),
                    Razon_Social = reader.IsDBNull("Razon_Social") ? "" : reader.GetString("Razon_Social"),
                    Email = reader.IsDBNull("Email") ? "" : reader.GetString("Email"),
                    DomicilioFiscalCliente = reader.IsDBNull("DomicilioFiscalCliente") ? null : reader.GetString("DomicilioFiscalCliente"),
                    RegimenFiscalCliente = reader.IsDBNull("RegimenFiscalCliente") ? null : reader.GetString("RegimenFiscalCliente")
                });
            }

            return clientes;
        }

        public async Task<List<ProductoRFC>> ObtenerProductosPorRFC(string rfc)
        {
            var productos = new List<ProductoRFC>();

            var query = @"
                SELECT
                    IdProdServ, RFC, CLAVESAT, CodigoInterno,
                    DescripcionInterna, UnidadSAT
                FROM ProdServRFC
                WHERE RFC = @rfc
                ORDER BY CodigoInterno";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@rfc", rfc);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                productos.Add(new ProductoRFC
                {
                    IdProdServ = reader.GetInt32("IdProdServ"),
                    RFC = reader.IsDBNull("RFC") ? string.Empty : reader.GetString("RFC"),
                    CLAVESAT = reader.IsDBNull("CLAVESAT") ? string.Empty : reader.GetString("CLAVESAT"),
                    CodigoInterno = reader.IsDBNull("CodigoInterno") ? string.Empty : reader.GetString("CodigoInterno"),
                    DescripcionInterna = reader.IsDBNull("DescripcionInterna") ? string.Empty : reader.GetString("DescripcionInterna"),
                    UnidadSAT = reader.IsDBNull("UnidadSAT") ? string.Empty : reader.GetString("UnidadSAT")
                });
            }

            return productos;
        }

    }
}
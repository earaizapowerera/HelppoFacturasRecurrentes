-- Script para crear tablas de Facturación Recurrente
-- Base de datos: helppo
-- Fecha: 2025-10-06

-- Tabla principal de plantillas de facturación recurrente
CREATE TABLE PlantillasFacturacion (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(200) NOT NULL,
    ClienteId INT NOT NULL,
    EmisorRFCId INT NOT NULL,
    Serie NVARCHAR(50),
    Folio NVARCHAR(50),
    FormaPago NVARCHAR(10) NOT NULL,
    UsoCFDI NVARCHAR(10) NOT NULL,
    LugarExpedicion NVARCHAR(10) NOT NULL,
    Moneda NVARCHAR(10) NOT NULL,
    TipoIVA NVARCHAR(20) NOT NULL DEFAULT 'ConIVA',
    CondicionesPago NVARCHAR(500),
    Observaciones NVARCHAR(MAX),

    -- Campos de programación recurrente
    Activa BIT NOT NULL DEFAULT 1,
    EsRecurrente BIT NOT NULL DEFAULT 1,
    TipoProgramacion NVARCHAR(20) NOT NULL DEFAULT 'DiaMes', -- DiaMes, DiaSemana, Quincenal, Mensual
    DiaEjecucion INT, -- Día del mes (1-31) o día de la semana (1-7)
    DiaSemana NVARCHAR(20), -- Lunes, Martes, etc.
    IntervaloEjecucion INT DEFAULT 1, -- Cada cuántos días/semanas/meses
    ProximaEjecucion DATE,

    -- Campos de auditoría
    FechaCreacion DATETIME NOT NULL DEFAULT GETDATE(),
    FechaUltimaModificacion DATETIME,
    FechaUltimaEjecucion DATETIME,
    UsuarioCreacion NVARCHAR(100),

    -- Foreign Keys
    CONSTRAINT FK_PlantillasFacturacion_Cliente
        FOREIGN KEY (ClienteId) REFERENCES Clientes(Id_Cliente),
    CONSTRAINT FK_PlantillasFacturacion_Emisor
        FOREIGN KEY (EmisorRFCId) REFERENCES propietarioRFC(IdPropietarioRFC)
);

-- Tabla de conceptos de la plantilla
CREATE TABLE ConceptosPlantilla (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PlantillaId INT NOT NULL,
    ClaveProdServ NVARCHAR(20) NOT NULL,
    ClaveUnidad NVARCHAR(20) NOT NULL,
    CantidadFormula NVARCHAR(500) NOT NULL DEFAULT '1',
    Descripcion NVARCHAR(MAX) NOT NULL,
    ValorUnitarioFormula NVARCHAR(500) NOT NULL,
    ImporteFormula NVARCHAR(500),
    Orden INT NOT NULL DEFAULT 1,

    CONSTRAINT FK_ConceptosPlantilla_Plantilla
        FOREIGN KEY (PlantillaId) REFERENCES PlantillasFacturacion(Id)
        ON DELETE CASCADE
);

-- Tabla de historial de facturas generadas
CREATE TABLE FacturasGeneradas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PlantillaId INT NOT NULL,
    UUID NVARCHAR(100),
    Serie NVARCHAR(50),
    Folio NVARCHAR(50),
    EmisorRFC NVARCHAR(20) NOT NULL,
    ReceptorRFC NVARCHAR(20) NOT NULL,
    ReceptorNombre NVARCHAR(500),
    Subtotal DECIMAL(18,2) NOT NULL,
    IVA DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    FechaGeneracion DATETIME NOT NULL DEFAULT GETDATE(),
    CadenaOriginal NVARCHAR(MAX),
    RespuestaWebService NVARCHAR(MAX),
    Exitosa BIT NOT NULL DEFAULT 0,
    MensajeError NVARCHAR(MAX),

    CONSTRAINT FK_FacturasGeneradas_Plantilla
        FOREIGN KEY (PlantillaId) REFERENCES PlantillasFacturacion(Id)
);

-- Tabla de programación de ejecuciones
CREATE TABLE ProgramacionEjecuciones (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PlantillaId INT NOT NULL,
    FechaProgramada DATE NOT NULL,
    HoraProgramada TIME DEFAULT '09:00:00',
    Estado NVARCHAR(50) NOT NULL DEFAULT 'Pendiente', -- Pendiente, Ejecutada, Error, Cancelada
    FechaEjecucion DATETIME,
    Resultado NVARCHAR(MAX),

    CONSTRAINT FK_ProgramacionEjecuciones_Plantilla
        FOREIGN KEY (PlantillaId) REFERENCES PlantillasFacturacion(Id)
        ON DELETE CASCADE
);

-- Índices para mejorar rendimiento
CREATE INDEX IX_PlantillasFacturacion_Activa ON PlantillasFacturacion(Activa);
CREATE INDEX IX_PlantillasFacturacion_ProximaEjecucion ON PlantillasFacturacion(ProximaEjecucion);
CREATE INDEX IX_ProgramacionEjecuciones_FechaProgramada ON ProgramacionEjecuciones(FechaProgramada, Estado);
CREATE INDEX IX_FacturasGeneradas_PlantillaId ON FacturasGeneradas(PlantillaId);
CREATE INDEX IX_FacturasGeneradas_UUID ON FacturasGeneradas(UUID);

-- Stored Procedure para obtener plantillas a ejecutar
CREATE PROCEDURE sp_ObtenerPlantillasParaEjecutar
    @FechaEjecucion DATE = NULL
AS
BEGIN
    IF @FechaEjecucion IS NULL
        SET @FechaEjecucion = CAST(GETDATE() AS DATE);

    SELECT
        p.*,
        c.RFCCliente,
        c.Razon_Social,
        c.Email,
        e.RFC as EmisorRFC,
        e.RazonSocial as EmisorRazonSocial
    FROM PlantillasFacturacion p
    INNER JOIN Clientes c ON p.ClienteId = c.Id_Cliente
    INNER JOIN propietarioRFC e ON p.EmisorRFCId = e.IdPropietarioRFC
    WHERE p.Activa = 1
      AND p.EsRecurrente = 1
      AND (p.ProximaEjecucion IS NULL OR p.ProximaEjecucion <= @FechaEjecucion)
    ORDER BY p.Id;
END;

-- Stored Procedure para actualizar próxima ejecución
CREATE PROCEDURE sp_ActualizarProximaEjecucion
    @PlantillaId INT
AS
BEGIN
    DECLARE @TipoProgramacion NVARCHAR(20);
    DECLARE @DiaEjecucion INT;
    DECLARE @IntervaloEjecucion INT;
    DECLARE @ProximaFecha DATE;

    SELECT
        @TipoProgramacion = TipoProgramacion,
        @DiaEjecucion = DiaEjecucion,
        @IntervaloEjecucion = IntervaloEjecucion
    FROM PlantillasFacturacion
    WHERE Id = @PlantillaId;

    -- Calcular próxima fecha según el tipo de programación
    IF @TipoProgramacion = 'DiaMes'
    BEGIN
        -- Próximo mes en el día especificado
        SET @ProximaFecha = DATEADD(MONTH, @IntervaloEjecucion, GETDATE());
        SET @ProximaFecha = DATEFROMPARTS(YEAR(@ProximaFecha), MONTH(@ProximaFecha), @DiaEjecucion);
    END
    ELSE IF @TipoProgramacion = 'Mensual'
    BEGIN
        -- Primer día del próximo mes
        SET @ProximaFecha = DATEADD(MONTH, @IntervaloEjecucion, GETDATE());
        SET @ProximaFecha = DATEFROMPARTS(YEAR(@ProximaFecha), MONTH(@ProximaFecha), 1);
    END
    ELSE IF @TipoProgramacion = 'Quincenal'
    BEGIN
        -- Día 1 o 15 del mes
        IF DAY(GETDATE()) < 15
            SET @ProximaFecha = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 15);
        ELSE
        BEGIN
            SET @ProximaFecha = DATEADD(MONTH, 1, GETDATE());
            SET @ProximaFecha = DATEFROMPARTS(YEAR(@ProximaFecha), MONTH(@ProximaFecha), 1);
        END
    END

    UPDATE PlantillasFacturacion
    SET ProximaEjecucion = @ProximaFecha,
        FechaUltimaEjecucion = GETDATE()
    WHERE Id = @PlantillaId;
END;

-- Permisos (ajustar según necesidad)
-- GRANT EXECUTE ON sp_ObtenerPlantillasParaEjecutar TO [usuario_app];
-- GRANT EXECUTE ON sp_ActualizarProximaEjecucion TO [usuario_app];
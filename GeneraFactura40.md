# Documentación del WebService GeneraFactura40

## Información General

- **URL**: https://ws.helppo.com.mx/v40/GeneraFactura40.asmx
- **Namespace**: https://helppo.com.mx/
- **Protocolo**: SOAP/HTTP
- **Versión CFDI**: 4.0

## Métodos Disponibles

### GenerarFactura

Genera y timbra un CFDI (Comprobante Fiscal Digital por Internet) versión 4.0.

#### Parámetros

- **pData** (String): Cadena de texto con los datos del comprobante y conceptos separados por el delimitador `~`

#### Formato del Parámetro pData

La cadena `pData` tiene la siguiente estructura:

```
DatosGenerales~Concepto1~Concepto2~...~ConceptoN
```

**Datos Generales** (separados por `|`):
1. Serie-Folio o Folio
2. FormaPago (código del catálogo c_FormaPago, ej: "01")
3. RFC Receptor
4. Nombre Receptor
5. País Receptor (ej: "MEX")
6. UsoCFDI (código del catálogo c_UsoCFDI)
7. Emails (separados por coma o punto y coma)
8. RFC Emisor
9. Lugar de Expedición (código postal)
10. Moneda (código del catálogo c_Moneda)
11. Condiciones de Pago
12. Observaciones
13. Tipo de Comprobante (código del catálogo c_TipoDeComprobante)
14. Folios de CFDIs relacionados (separados por coma, opcional)
15. Tipo de Relación (código del catálogo c_TipoRelacion, opcional)
16. Régimen Fiscal Receptor (código del catálogo c_RegimenFiscal)
17. Domicilio Fiscal Receptor (código postal)

**Conceptos** (separados por `|`):
1. [Identificador]
2. ClaveProdServ (código del catálogo c_ClaveProdServ)
3. ClaveUnidad (código del catálogo c_ClaveUnidad)
4. [Campo no utilizado]
5. Cantidad
6. Descripción
7. Valor Unitario
8. Importe
9. Número de Pedimento (15 dígitos, opcional)

#### Respuesta Exitosa

Formato JSON:
```json
{
  "success": "true",
  "emisor_rfc": "RFC del emisor",
  "receptor_rfc": "RFC del receptor",
  "uuid": "UUID del comprobante timbrado"
}
```

#### Respuesta de Error

Formato JSON:
```json
{
  "success": "false",
  "error": "Descripción del error"
}
```

#### Respuesta de CFDI Duplicado

Si ya existe un comprobante con los mismos datos:
```
Success:true|Id=ID_DEL_CFDI
```

## Funcionalidades

1. **Validación de duplicados**: Verifica si existe un CFDI similar ya facturado
2. **Timbrado automático**: Utiliza el PAC Xpress para el timbrado
3. **CFDIs Relacionados**: Soporta relación con otros CFDIs
4. **Información Aduanera**: Permite incluir pedimentos en conceptos
5. **Información Global**: Para público en general (RFC XAXX010101000)
6. **Envío de correos**: Envía automáticamente XML y PDF a los emails especificados
7. **Bitácora**: Registra todas las solicitudes y errores

## Impuestos

El servicio calcula automáticamente:
- **IVA**: Tasa fija del 16% (0.160000)
- **Tipo de Factor**: Tasa
- **Objeto de Impuesto**: 02 (Sí objeto de impuesto)

## Notas Importantes

- El servicio requiere que los certificados (.cer) estén almacenados en la ruta configurada en `pathCer`
- Los emails se envían en un hilo separado para no bloquear la respuesta
- Se genera automáticamente la fecha de emisión con la fecha y hora actual
- El RFC de pruebas "XIA190128J61" utiliza el ambiente de pruebas del PAC

## Ejemplo de Uso

```
Serie-Folio|01|XAXX010101000|PUBLICO EN GENERAL|MEX|S01|correo@ejemplo.com|RFC_EMISOR|12345|MXN|Contado|I|||601|12345~
1|01010101|H87|0|1|Producto ejemplo|100.00|100.00|
```

Esta documentación describe el WebService para la generación de facturas CFDI 4.0 disponible en `GeneraFactura40.asmx`.

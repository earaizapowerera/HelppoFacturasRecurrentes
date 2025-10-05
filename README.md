# 🧾 Sistema de Facturación Recurrente con Comodines

Sistema web para generar facturas CFDI 4.0 con plantillas reutilizables, comodines dinámicos y fórmulas automatizadas.

## 🚀 Inicio Rápido

```bash
cd /Users/enrique/FacturacionRecurrente
dotnet build  # Verificar compilación
dotnet run    # Ejecutar aplicación
```

**URL:** http://localhost:5063

## ✨ Funcionalidades Principales

### 🏢 **Gestión de Emisores**
- Selector de RFC emisor desde tabla `propietarioRFC`
- **Filtrado por `Settings_RFC`** - Solo RFCs que tienen configuración de facturación
- Solo RFCs activos disponibles

### 👥 **Gestión de Clientes**
- Selector de clientes desde tabla `Clientes`
- Datos automáticos: RFC, Razón Social, Email, CP, Régimen Fiscal

### 🏷️ **Sistema de Comodines**
- **Fechas:** `{fechaactual}`, `{mesactual}`, `{añoactual}`
- **Períodos:** `{dia1_mes}`, `{ultimo_dia_mes}`
- **Tipo de Cambio:** `{tcfixed}`, `{tc}` (del día actual)
- **Folios:** `{SiguienteFolio}` (auto-incrementa por RFC y Serie)
- **Cliente:** `{cliente_rfc}`, `{cliente_nombre}`, `{cliente_email}`
- **Texto:** `{mes_texto}`, `{mes_texto_mayus}`, `{mes_anterior_texto}`

### 🧮 **Motor de Fórmulas**
- Operaciones: `+`, `-`, `*`, `/`, paréntesis
- Ejemplos: `600*{tcfixed}`, `(500+100)*{tc}`, `1000`

### 📋 **Catálogos SAT**
- Dropdowns automáticos: c_FormaPago, c_UsoCFDI, c_Moneda
- c_ClaveProdServ, c_ClaveUnidad para conceptos

## 🔗 **Integración WebService**

- **URL:** https://ws.helppo.com.mx/v40/GeneraFactura40.asmx
- **Método:** GenerarFactura
- **Formato:** Cadena delimitada por `~` y `|`
- **Preview:** Muestra cadena antes de enviar

## 📝 **Ejemplo de Uso: Todini Atlantica**

**Configuración:**
- **Emisor:** Seleccionar RFC emisor
- **Cliente:** Buscar "Todini" y seleccionar de la lista filtrada
- **Serie:** `TODINI{mesactual}`
- **Folio:** `{SiguienteFolio}`
- **Forma Pago:** `99` (Por definir)
- **Uso CFDI:** `G03` (Gastos en general)
- **Moneda:** `MXN`

**Concepto:**
- **Clave Producto:** `84111506` (Servicios de consultoría)
- **Clave Unidad:** `E48` (Unidad de servicio)
- **Descripción:** `Servicios de {mes_texto} {añoactual} para Todini Atlantica`
- **Cantidad:** `1`
- **Valor Unitario:** `600*{tcfixed}`

**Resultado Automático:**
- Con TC actual (18.3342): `600*18.3342 = $11,000.52 MXN`
- Descripción: `Servicios de octubre 2025 para Todini Atlantica`
- Serie-Folio: `TODINI10-001` (si es el primer folio de la serie)

**Facturación Recurrente:**
- Se ejecutará automáticamente el día 1 de cada mes
- Folio se auto-incrementa: `001`, `002`, `003`...

## 🗃️ **Base de Datos**

**Conexión:**
- **Server:** helppo.com.mx
- **Database:** helppo
- **Usuario:** uhelppo
- **Password:** H3lpp0

**Tablas Utilizadas:**
- `Clientes` - Información de clientes (filtrados por RFC emisor)
- `propietarioRFC` - RFCs que pueden facturar
- `Settings_RFC` - Configuración de RFCs para facturación (filtro de emisores)
- `TipoCambioDOF` - Tipos de cambio diarios
- `hp_FoliosRFC` - Control de folios por RFC y serie
- `c_*` - Catálogos SAT

## 🔧 **Tecnologías**

- **Backend:** ASP.NET Core 8.0
- **Frontend:** Bootstrap 5, jQuery
- **Base de Datos:** SQL Server
- **WebService:** SOAP/HTTP

## 📊 **Puerto Asignado**

| Proyecto | Puerto | Estado |
|----------|--------|--------|
| FacturacionRecurrente | **5063** | ✅ Activo |

## 🚨 **Notas Importantes**

1. **✅ Compilación Verificada:** Proyecto compila sin errores
2. **✅ Ejecución Verificada:** Aplicación inicia y carga correctamente en puerto 5063
3. **✅ Manejo de NULL:** Corregido para evitar SqlNullValueException
4. **Preview Obligatorio:** Siempre revisar cadena antes de facturar
5. **Conexión Validada:** Base de datos y WebService funcionales
6. **Comodines Dinámicos:** Se evalúan en tiempo real

## 🎯 **Flujo de Trabajo**

### **📋 Configuración de Plantilla Recurrente:**
1. **Seleccionar RFC emisor** - Filtra clientes automáticamente
2. **Buscar y seleccionar cliente** - Búsqueda inteligente por texto
3. **Configurar Serie y Folio** - Usar `{SiguienteFolio}` para auto-incremento
4. **Configurar datos generales** con comodines dinámicos
5. **Agregar conceptos** con fórmulas que incluyen tipo de cambio
6. **Ver Preview** para validar cadena generada
7. **Configurar Facturación Recurrente** - Se ejecuta día 1 de cada mes

### **📅 Ejecución Mensual:**
1. **Emitir Facturas del Mes** - Procesa todas las plantillas configuradas
2. **Auto-generación** el día 1 de cada mes
3. **Control automático de folios** - Se incrementan por serie
4. **Tipo de cambio actualizado** - Del día de ejecución

---

*Sistema desarrollado para automatizar facturación recurrente con cálculos dinámicos de tipo de cambio y plantillas reutilizables.*
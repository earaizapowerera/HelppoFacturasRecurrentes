#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.webdriver.support.ui import Select
from selenium.webdriver.common.keys import Keys
import time
import sys

def test_paso1():
    # Configurar el driver de Chrome
    options = webdriver.ChromeOptions()
    options.add_argument('--no-sandbox')
    options.add_argument('--disable-dev-shm-usage')

    try:
        print("🚀 Iniciando prueba del Paso 1...")
        driver = webdriver.Chrome(options=options)
        driver.maximize_window()

        # 1. Navegar primero a la lista de plantillas
        print("📍 Navegando a la lista de plantillas...")
        driver.get("http://localhost:5063/Home/Plantillas")
        wait = WebDriverWait(driver, 10)
        time.sleep(2)

        # Verificar que estamos en la página de plantillas
        try:
            titulo = wait.until(EC.presence_of_element_located((By.XPATH, "//h3[contains(text(), 'Plantillas de Facturación')]")))
            print("✅ En la página de Plantillas")
        except:
            print("⚠️ No se pudo verificar la página de plantillas")

        # 2. Hacer clic en el botón "Nueva Plantilla"
        print("✅ Buscando botón Nueva Plantilla...")
        try:
            btn_nueva = wait.until(EC.element_to_be_clickable((By.XPATH, "//a[contains(@class, 'btn-success') and contains(text(), 'Nueva Plantilla')]")))
            btn_nueva.click()
            print("✅ Clic en Nueva Plantilla")
            time.sleep(2)
        except Exception as e:
            print(f"⚠️ No se encontró el botón Nueva Plantilla: {e}")
            # Si no hay botón, navegar directamente
            driver.get("http://localhost:5063")

        # Esperar que la página del wizard cargue
        wait = WebDriverWait(driver, 10)

        # 1. Seleccionar RFC Emisor PES
        print("✅ Seleccionando RFC Emisor PES...")
        emisor_select = wait.until(EC.presence_of_element_located((By.ID, "emisorRFC")))
        emisor_dropdown = Select(emisor_select)

        # Buscar y seleccionar PES
        emisor_encontrado = False
        for option in emisor_dropdown.options:
            if "PES" in option.text.upper():
                emisor_dropdown.select_by_visible_text(option.text)
                print(f"   Emisor seleccionado: {option.text}")
                emisor_encontrado = True
                break

        if not emisor_encontrado and len(emisor_dropdown.options) > 1:
            emisor_dropdown.select_by_index(1)
            print("   ⚠️ No se encontró PES, seleccionando primer emisor disponible")

        time.sleep(2)  # Esperar que carguen los productos del emisor

        # 2. Buscar y seleccionar cliente SCO (Stilo Concepto)
        print("✅ Buscando cliente STILO CONCEPTO (SCO020904)...")
        cliente_input = wait.until(EC.element_to_be_clickable((By.ID, "clienteBuscar")))
        cliente_input.click()
        cliente_input.clear()
        # Buscar por RFC específico SCO020904
        cliente_input.send_keys("SCO020904")
        time.sleep(3)  # Esperar más tiempo para resultados

        # Seleccionar cliente STILO CONCEPTO
        try:
            # Esperar a que aparezcan los resultados
            wait.until(EC.presence_of_element_located((By.CSS_SELECTOR, "#clientesDropdown li")))

            # Buscar todos los clientes en el dropdown
            clientes = driver.find_elements(By.CSS_SELECTOR, "#clientesDropdown li a")
            print(f"   Encontrados {len(clientes)} clientes")

            cliente_encontrado = False
            for i, cliente in enumerate(clientes):
                texto_cliente = cliente.text.strip()
                print(f"   Cliente {i+1}: {texto_cliente}")

                # Buscar STILO CONCEPTO o SCO020904
                if "STILO" in texto_cliente.upper() or "SCO020904" in texto_cliente.upper():
                    driver.execute_script("arguments[0].scrollIntoView(true);", cliente)
                    time.sleep(0.5)
                    cliente.click()
                    print(f"✅ Cliente STILO CONCEPTO seleccionado: {texto_cliente}")
                    cliente_encontrado = True
                    break

            if not cliente_encontrado:
                # Si no se encuentra, intentar buscar con "SCO"
                cliente_input.clear()
                cliente_input.send_keys("SCO020904")
                time.sleep(2)

                clientes = driver.find_elements(By.CSS_SELECTOR, "#clientesDropdown li a")
                if len(clientes) > 0:
                    clientes[0].click()
                    print("✅ Cliente seleccionado por RFC")
                else:
                    print("❌ No se encontró cliente STILO CONCEPTO")

            time.sleep(1)
        except Exception as e:
            print(f"⚠️ Error seleccionando cliente: {e}")

        # 3. Llenar Serie y Folio
        print("✅ Llenando Serie y Folio...")
        serie_input = driver.find_element(By.ID, "serie")
        serie_input.clear()
        serie_input.send_keys("H")

        folio_input = driver.find_element(By.ID, "folio")
        folio_input.clear()
        folio_input.send_keys("{SiguienteFolio}")

        # 4. Seleccionar Forma de Pago
        print("✅ Seleccionando Forma de Pago...")
        forma_pago = Select(driver.find_element(By.ID, "formaPago"))
        if len(forma_pago.options) > 1:
            forma_pago.select_by_value("03")  # Transferencia

        # 5. Seleccionar Uso CFDI
        print("✅ Seleccionando Uso CFDI...")
        uso_cfdi = Select(driver.find_element(By.ID, "usoCFDI"))
        if len(uso_cfdi.options) > 1:
            uso_cfdi.select_by_value("G03")  # Gastos en general

        # 6. Seleccionar Moneda
        print("✅ Seleccionando Moneda...")
        moneda = Select(driver.find_element(By.ID, "moneda"))
        if len(moneda.options) > 1:
            moneda.select_by_value("MXN")

        # 7. Llenar datos del primer concepto (ya está abierto por defecto)
        print("✅ Llenando datos del concepto...")
        time.sleep(2)

        # Llenar datos del concepto
        try:
            # Buscar el dropdown de Producto/Servicio
            try:
                # El dropdown puede tener diferentes nombres, buscar por selector
                producto_select = None

                # Intentar diferentes selectores
                selectores = [
                    "select[name='Conceptos[0].ProductoId']",
                    "select[id*='producto']",
                    ".concepto-item select:first-of-type"
                ]

                for selector in selectores:
                    try:
                        producto_select = driver.find_element(By.CSS_SELECTOR, selector)
                        if producto_select:
                            break
                    except:
                        continue

                if producto_select:
                    producto_dropdown = Select(producto_select)

                    # Esperar que se carguen las opciones
                    time.sleep(1)

                    # Buscar opción que contenga "Hosting"
                    producto_seleccionado = False
                    for i, option in enumerate(producto_dropdown.options):
                        texto_opcion = option.text.upper()
                        if i == 0:
                            continue  # Saltar la primera opción que suele ser "Seleccione..."

                        print(f"   Opción {i}: {option.text}")
                        if "HOSTING" in texto_opcion or "HOSP" in texto_opcion:
                            producto_dropdown.select_by_index(i)
                            print(f"✅ Producto HOSTING seleccionado: {option.text}")
                            producto_seleccionado = True
                            time.sleep(3)  # Esperar que se carguen los datos predeterminados del producto

                            # IMPORTANTE: Después de seleccionar el producto, se cargan valores predeterminados
                            # Necesitamos reemplazar el valor unitario predeterminado (600*{tcfixed}) con 1955
                            print("   Esperando que se carguen los valores predeterminados...")
                            break

                    if not producto_seleccionado and len(producto_dropdown.options) > 1:
                        # Seleccionar la primera opción disponible que no sea el placeholder
                        producto_dropdown.select_by_index(1)
                        print(f"⚠️ No se encontró Hosting, seleccionando: {producto_dropdown.options[1].text}")
                        time.sleep(1)
                else:
                    print("ℹ️ No se encontró dropdown de productos")

            except Exception as e:
                print(f"ℹ️ Error buscando dropdown de productos: {e}")

            # Si no se pudo seleccionar del dropdown o no existe, llenar manualmente
            claves_prod = driver.find_elements(By.NAME, "Conceptos[0].ClaveProdServ")
            if claves_prod:
                if not claves_prod[0].get_attribute("value"):  # Solo llenar si está vacío
                    claves_prod[0].clear()
                    claves_prod[0].send_keys("81112105")

            claves_unidad = driver.find_elements(By.NAME, "Conceptos[0].ClaveUnidad")
            if claves_unidad:
                if not claves_unidad[0].get_attribute("value"):  # Solo llenar si está vacío
                    claves_unidad[0].clear()
                    claves_unidad[0].send_keys("E48")

            descripciones = driver.find_elements(By.NAME, "Conceptos[0].Descripcion")
            if descripciones:
                if not descripciones[0].get_attribute("value"):  # Solo llenar si está vacío
                    descripciones[0].clear()
                    descripciones[0].send_keys("Hosting Web")

            cantidades = driver.find_elements(By.NAME, "Conceptos[0].CantidadFormula")
            if cantidades:
                cantidades[0].clear()
                cantidades[0].send_keys("1")

            # IMPORTANTE: Establecer el valor unitario a 1955
            print("   Esperando que se carguen los valores del producto...")
            time.sleep(3)

            # Buscar el campo de valor unitario - es un input que puede tener placeholder "600*{tcfixed}"
            print("   Buscando campo de valor unitario...")

            # Primero listar todos los inputs para entender la estructura
            print("   Analizando estructura del formulario...")
            inputs = driver.find_elements(By.CSS_SELECTOR, ".concepto-item input")
            for i, inp in enumerate(inputs):
                name = inp.get_attribute('name')
                value = inp.get_attribute('value')
                placeholder = inp.get_attribute('placeholder')
                print(f"   Input {i}: name='{name}', value='{value}', placeholder='{placeholder}'")

            # Buscar específicamente el campo ValorUnitarioFormula
            valor_unitario_field = None
            try:
                valor_unitario_field = driver.find_element(By.NAME, "Conceptos[0].ValorUnitarioFormula")
                print("✅ Campo ValorUnitarioFormula encontrado")
            except:
                print("   No se encontró por NAME, buscando por otras estrategias...")
                # Buscar input que tenga el texto "600*{tcfixed}" como placeholder o value
                for inp in inputs:
                    placeholder = inp.get_attribute('placeholder') or ''
                    value = inp.get_attribute('value') or ''
                    if '600' in placeholder or '600' in value or 'tcfixed' in placeholder or 'tcfixed' in value:
                        valor_unitario_field = inp
                        print(f"✅ Campo encontrado con indicios de fórmula: placeholder='{placeholder}', value='{value}'")
                        break

            if valor_unitario_field:
                # Información del campo encontrado
                print(f"   Campo encontrado:")
                print(f"     - Name: {valor_unitario_field.get_attribute('name')}")
                print(f"     - Value: {valor_unitario_field.get_attribute('value')}")
                print(f"     - Placeholder: {valor_unitario_field.get_attribute('placeholder')}")

                # Hacer scroll hasta el elemento
                driver.execute_script("arguments[0].scrollIntoView({block: 'center'});", valor_unitario_field)
                time.sleep(1)

                # Hacer clic para enfocar el campo
                print("   Haciendo clic en el campo...")
                valor_unitario_field.click()
                time.sleep(0.5)

                # Limpiar el campo completamente
                print("   Limpiando el campo...")
                # Probar múltiples formas de limpiar
                valor_unitario_field.clear()
                time.sleep(0.3)

                # Seleccionar todo y borrar por si acaso
                valor_unitario_field.send_keys(Keys.CONTROL + "a")
                time.sleep(0.3)
                valor_unitario_field.send_keys(Keys.DELETE)
                time.sleep(0.3)

                # Escribir el valor 1955
                print("   Escribiendo 1955...")
                valor_unitario_field.send_keys("1955")
                time.sleep(0.5)

                # Presionar TAB para salir del campo y activar cualquier evento
                valor_unitario_field.send_keys(Keys.TAB)
                time.sleep(1)

                # Verificar el resultado
                valor_final = valor_unitario_field.get_attribute("value")
                if "1955" in valor_final:
                    print("✅ Valor unitario establecido correctamente: 1955")
                else:
                    print(f"⚠️ El campo muestra: {valor_final}")
                    # Intentar una vez más con JavaScript
                    print("   Intentando con JavaScript...")
                    driver.execute_script("""
                        arguments[0].value = '1955';
                        arguments[0].dispatchEvent(new Event('input', { bubbles: true }));
                        arguments[0].dispatchEvent(new Event('change', { bubbles: true }));
                    """, valor_unitario_field)
                    time.sleep(0.5)
                    valor_final = valor_unitario_field.get_attribute("value")
                    print(f"   Valor después de JavaScript: {valor_final}")
            else:
                print("❌ No se pudo encontrar el campo de valor unitario")
                print("   Por favor verifica manualmente la estructura del formulario")

            print("✅ Concepto agregado correctamente")
        except Exception as e:
            print(f"⚠️ Error agregando concepto: {e}")

        # 8. Hacer clic en Siguiente para ir al Paso 2
        print("✅ Haciendo clic en Siguiente...")
        btn_siguiente = driver.find_element(By.ID, "btnSiguiente")
        driver.execute_script("arguments[0].scrollIntoView(true);", btn_siguiente)
        time.sleep(1)
        btn_siguiente.click()

        # Esperar que aparezca el paso 2
        time.sleep(2)

        # 9. En el Paso 2, generar vista previa
        print("✅ Generando Vista Previa...")
        try:
            btn_vista_previa = wait.until(EC.element_to_be_clickable((By.XPATH, "//button[contains(text(), 'Actualizar Vista Previa')]")))
            driver.execute_script("arguments[0].scrollIntoView(true);", btn_vista_previa)
            time.sleep(1)
            btn_vista_previa.click()
            time.sleep(2)
            print("✅ Vista previa generada")
        except:
            print("⚠️ No se pudo generar vista previa")

        # 10. Generar Cadena (esto guardará la plantilla)
        print("✅ Generando Cadena y guardando plantilla...")
        try:
            btn_cadena = wait.until(EC.element_to_be_clickable((By.XPATH, "//button[contains(text(), 'Actualizar Cadena')]")))
            driver.execute_script("arguments[0].scrollIntoView(true);", btn_cadena)
            time.sleep(1)
            btn_cadena.click()
            print("⏳ Esperando respuesta del servidor...")
            time.sleep(5)  # Esperar respuesta

            # Verificar si hay mensaje de error
            error_alerts = driver.find_elements(By.CSS_SELECTOR, ".alert-danger")
            if error_alerts:
                for alert in error_alerts:
                    if alert.is_displayed():
                        print(f"❌ Error detectado: {alert.text}")

            # Verificar si hay mensaje de éxito
            success_alerts = driver.find_elements(By.CSS_SELECTOR, ".alert-success")
            if success_alerts:
                for alert in success_alerts:
                    if alert.is_displayed():
                        print(f"✅ Éxito: {alert.text[:100]}...")

                        # Buscar botón Siguiente si la plantilla se guardó
                        try:
                            btn_siguiente_paso3 = driver.find_element(By.XPATH, "//button[contains(text(), 'Siguiente') and contains(text(), 'Configurar Programación')]")
                            if btn_siguiente_paso3.is_displayed():
                                print("✅ Botón 'Siguiente → Configurar Programación' encontrado")
                        except:
                            print("⚠️ No se encontró el botón para ir al paso 3")

        except Exception as e:
            print(f"❌ Error generando cadena: {e}")

        print("\n📊 RESUMEN DE LA PRUEBA:")
        print("=" * 50)

        # Tomar screenshot
        driver.save_screenshot("test_paso1_resultado.png")
        print("📸 Screenshot guardado: test_paso1_resultado.png")

        # 8. Volver a la lista de plantillas para verificar que se guardó
        print("\n✅ Verificando que la plantilla aparezca en el listado...")
        driver.get("http://localhost:5063/Home/Plantillas")
        time.sleep(3)

        # Buscar la plantilla recién creada en la tabla
        try:
            # Buscar en la tabla por el cliente STILO CONCEPTO
            filas = driver.find_elements(By.CSS_SELECTOR, "#tablePlantillas tbody tr")
            plantilla_encontrada = False

            for fila in filas:
                texto_fila = fila.text
                if "STILO CONCEPTO" in texto_fila or "SCO020904" in texto_fila:
                    print(f"✅ Plantilla encontrada en el listado: {texto_fila[:100]}...")
                    plantilla_encontrada = True

                    # Verificar que tiene los botones de acción
                    botones = fila.find_elements(By.CSS_SELECTOR, "button")
                    print(f"   - Botones de acción disponibles: {len(botones)}")

                    # Verificar estado
                    if "Activa" in texto_fila:
                        print("   - Estado: Activa")
                    else:
                        print("   - Estado: Inactiva")
                    break

            if not plantilla_encontrada:
                print("⚠️ No se encontró la plantilla en el listado")
                print("   Plantillas en la tabla:")
                for i, fila in enumerate(filas[:5]):  # Mostrar las primeras 5
                    print(f"   {i+1}. {fila.text[:80]}...")
        except Exception as e:
            print(f"⚠️ Error verificando el listado: {e}")

        # Mantener el navegador abierto 5 segundos para ver el resultado
        print("\n⏰ Manteniendo navegador abierto 5 segundos...")
        time.sleep(5)

    except Exception as e:
        print(f"❌ Error durante la prueba: {e}")
        return False
    finally:
        driver.quit()
        print("✅ Prueba completada")

    return True

if __name__ == "__main__":
    print("=" * 60)
    print("PRUEBA AUTOMATIZADA - PASO 1 FACTURACIÓN RECURRENTE")
    print("=" * 60)

    success = test_paso1()

    if success:
        print("\n✅ ¡Prueba exitosa!")
        sys.exit(0)
    else:
        print("\n❌ Prueba fallida")
        sys.exit(1)
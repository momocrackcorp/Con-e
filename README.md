# Con-E v0.12.0 — diagnóstico

El gestor de particiones se ha extraído a [WinPart](WinPart/README.md), un proyecto y ejecutable independientes. Con-E conserva las tarjetas de hardware, inventario de controladores, logos, informes y herramientas. Ya no contiene la pestaña Mapa de discos ni operaciones de modificación de particiones.

## Compilación y descargas

GitHub Actions genera dos paquetes separados: Con-E-v0.12.0-portable y WinPart-v0.3.0-portable. Requieren .NET Framework 4.8 en Windows. Con-E no necesita WinPart para ejecutarse.

El historial de cambios anterior se conserva en Git. El código extraído está en WinPart/src y sus pruebas de reducción/ampliación se ejecutan sólo sobre un VHD desechable del runner. Con-E mantiene sus pruebas de interfaz y datos simulados. WinPart tiene su propia entrada de aplicación y proyecto, sin compilar los archivos de Con-E.


## RAM por posición v0.12.0

Vista de módulos y posiciones SMBIOS: ocupada, libre informada o ubicación no confirmada. Se decodifican estructuras SMBIOS tipo 17, incluidas posiciones vacías, y se vinculan por nombre con Win32_PhysicalMemory para mostrar capacidad, DDR, velocidad, fabricante, pieza y serie. Cada módulo tiene detalles expandibles. Si sólo se conoce el número total, las posiciones adicionales no se identifican como ranuras físicas accesibles. La RAM soldada puede figurar como dispositivo. El total instalado se suma desde capacidades de módulos; el uso sigue siendo una lectura del sistema operativo.


## Núcleos e hilos v0.12.0

Procesador muestra un cuadro por núcleo físico con el número de hilos lógicos asociados. La relación se obtiene con GetLogicalProcessorInformationEx, sin asumir dos hilos por núcleo. En sistemas híbridos pueden aparecer núcleos de uno y dos hilos; no se infiere que sean P/E ni se presenta uso en tiempo real. Se conservan identificación Intel/AMD/ARM, datos WMI y detalle expandible. Si Windows no proporciona la topología se indica en lugar de generar un esquema inventado. En equipos de más de 256 núcleos el esquema se limita a 256 cuadros, aunque el resumen indica el total.


## Interfaz moderna y CPU en vivo — v0.12.0

Con-E incorpora controles propios en el espacio de nombres `UIModernization`: tarjetas oscuras con bordes redondeados y barras de progreso de trazo suave. No necesita instalar una biblioteca gráfica adicional. La distribución sigue adaptándose al ancho de la ventana; conserva inventario, RAM por ranura, vídeo, discos, BIOS, informes, hardware/controladores y herramientas. El diagrama de núcleos/hilos se despliega en Ver detalle del procesador. Los logotipos conservan una pequeña base clara para mantener sus colores y legibilidad.

La CPU se consulta con `Win32_PerfFormattedData_PerfOS_Processor`, instancia `_Total`, usando `Task.Run`. El ciclo espera la consulta anterior y un intervalo de 1500 ms; no se superponen consultas ni se reconstruye el panel en cada muestra. Después de `await`, la actualización vuelve al hilo WinForms. Se cancela al cerrar; las consultas WMI en curso liberan sus recursos cuando terminan. Los errores muestran Sin lectura y se reintentan; no se presentan porcentajes simulados en ejecución normal. El inventario completo ya se consulta en segundo plano con Task.Run. RAM y discos mantienen la lectura del último inventario, no son monitores continuos.

Las pruebas incluyen muestras controladas en un trabajador, comprobación de actualización en el hilo de interfaz, ausencia de solapamiento, cancelación, estado sin datos y consulta WMI real. Se generan capturas con tarjetas expandidas, ventana ancha/estrecha y escala doble. Las capturas de diseño usan datos simulados y no representan el equipo del usuario.

# Con-E v0.11.0 — diagnóstico

El gestor de particiones se ha extraído a [WinPart](WinPart/README.md), un proyecto y ejecutable independientes. Con-E conserva las tarjetas de hardware, inventario de controladores, logos, informes y herramientas. Ya no contiene la pestaña Mapa de discos ni operaciones de modificación de particiones.

## Compilación y descargas

GitHub Actions genera dos paquetes separados: Con-E-v0.11.0-portable y WinPart-v0.3.0-portable. Requieren .NET Framework 4.8 en Windows. Con-E no necesita WinPart para ejecutarse.

El historial de cambios anterior se conserva en Git. El código extraído está en WinPart/src y sus pruebas de reducción/ampliación se ejecutan sólo sobre un VHD desechable del runner. Con-E mantiene sus pruebas de interfaz y datos simulados. WinPart tiene su propia entrada de aplicación y proyecto, sin compilar los archivos de Con-E.


## RAM por posición v0.11.0

Vista de módulos y posiciones SMBIOS: ocupada, libre informada o ubicación no confirmada. Se decodifican estructuras SMBIOS tipo 17, incluidas posiciones vacías, y se vinculan por nombre con Win32_PhysicalMemory para mostrar capacidad, DDR, velocidad, fabricante, pieza y serie. Cada módulo tiene detalles expandibles. Si sólo se conoce el número total, las posiciones adicionales no se identifican como ranuras físicas accesibles. La RAM soldada puede figurar como dispositivo. El total instalado se suma desde capacidades de módulos; el uso sigue siendo una lectura del sistema operativo.


## Núcleos e hilos v0.11.0

Procesador muestra un cuadro por núcleo físico con el número de hilos lógicos asociados. La relación se obtiene con GetLogicalProcessorInformationEx, sin asumir dos hilos por núcleo. En sistemas híbridos pueden aparecer núcleos de uno y dos hilos; no se infiere que sean P/E ni se presenta uso en tiempo real. Se conservan identificación Intel/AMD/ARM, datos WMI y detalle expandible. Si Windows no proporciona la topología se indica en lugar de generar un esquema inventado. En equipos de más de 256 núcleos el esquema se limita a 256 cuadros, aunque el resumen indica el total.

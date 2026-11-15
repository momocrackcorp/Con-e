# Con-E v0.14.0 — diagnóstico

El gestor de particiones se ha extraído a [WinPart](WinPart/README.md), un proyecto y ejecutable independientes. Con-E conserva las tarjetas de hardware, inventario de controladores, logos, informes y herramientas. Ya no contiene la pestaña Mapa de discos ni operaciones de modificación de particiones.

## Compilación y descargas

GitHub Actions genera dos paquetes separados: Con-E-v0.14.0-portable y WinPart-v0.8.0-portable. Con-E 0.14 requiere Windows de 64 bits y .NET Framework 4.8; WinPart conserva sus requisitos anteriores. Con-E no necesita WinPart para ejecutarse.

El historial de cambios anterior se conserva en Git. El código extraído está en WinPart/src y sus pruebas de reducción/ampliación se ejecutan sólo sobre un VHD desechable del runner. Con-E mantiene sus pruebas de interfaz y datos simulados. WinPart tiene su propia entrada de aplicación y proyecto, sin compilar los archivos de Con-E.


## RAM por posición v0.14.0

Vista de módulos y posiciones SMBIOS: ocupada, libre informada o ubicación no confirmada. Se decodifican estructuras SMBIOS tipo 17, incluidas posiciones vacías, y se vinculan por nombre con Win32_PhysicalMemory para mostrar capacidad, DDR, velocidad, fabricante, pieza y serie. Cada módulo tiene detalles expandibles. Si sólo se conoce el número total, las posiciones adicionales no se identifican como ranuras físicas accesibles. La RAM soldada puede figurar como dispositivo. El total instalado se suma desde capacidades de módulos; el uso sigue siendo una lectura del sistema operativo.


## Núcleos e hilos v0.14.0

Procesador muestra un cuadro por núcleo físico con el número de hilos lógicos asociados. La relación se obtiene con GetLogicalProcessorInformationEx, sin asumir dos hilos por núcleo. En sistemas híbridos pueden aparecer núcleos de uno y dos hilos; no se infiere que sean P/E ni representa uso de cada núcleo en tiempo real; la barra de CPU muestra el uso global. Se conservan identificación Intel/AMD/ARM, datos WMI y detalle expandible. Si Windows no proporciona la topología se indica en lugar de generar un esquema inventado. En equipos de más de 256 núcleos el esquema se limita a 256 cuadros, aunque el resumen indica el total.


## Interfaz moderna y CPU en vivo — v0.14.0

Con-E incorpora controles propios en el espacio de nombres `UIModernization`: tarjetas claras con bordes redondeados y barras de progreso de trazo suave. No necesita instalar una biblioteca gráfica adicional. La distribución sigue adaptándose al ancho de la ventana; conserva inventario, RAM por ranura, vídeo, discos, BIOS, informes, hardware/controladores y herramientas. El diagrama de núcleos/hilos se despliega en Ver detalle del procesador. Los logotipos conservan una pequeña base clara para mantener sus colores y legibilidad.

La CPU se consulta con `Win32_PerfFormattedData_PerfOS_Processor`, instancia `_Total`, usando `Task.Run`. El ciclo espera la consulta anterior y un intervalo de 1500 ms; no se superponen consultas ni se reconstruye el panel en cada muestra. Después de `await`, la actualización vuelve al hilo WinForms. Se cancela al cerrar; las consultas WMI en curso liberan sus recursos cuando terminan. Los errores muestran Sin lectura y se reintentan; no se presentan porcentajes simulados en ejecución normal. El inventario completo ya se consulta en segundo plano con Task.Run. RAM y discos mantienen la lectura del último inventario, no son monitores continuos.

Las pruebas incluyen muestras controladas en un trabajador, comprobación de actualización en el hilo de interfaz, ausencia de solapamiento, cancelación, estado sin datos y consulta WMI real. Se generan capturas con tarjetas expandidas, ventana ancha/estrecha y escala doble. Las capturas de diseño usan datos simulados y no representan el equipo del usuario.


### Tema claro — v0.14.0

Fondo gris claro, tarjetas blancas, textos oscuros y acentos azules. Conserva componentes modernos, métricas WMI en segundo plano, cancelación y tarjetas adaptables. Se retira el tema oscuro como apariencia predeterminada.


## Almacenamiento gráfico — v0.14.0

La tarjeta Almacenamiento incorpora selector de unidad, anillo de porcentaje usado, espacio total/usado/libre y sistema de archivos. Incluye unidades fijas y extraíbles que Windows pueda leer. **Ver uso y analizar carpetas** abre un panel independiente con capacidad, modelo y tipo cuando pueden asociarse, estado de salud informado por Windows, temperatura disponible y lectura/escritura WMI en segundo plano. No se inventa un porcentaje de vida útil; si el controlador o los permisos no entregan salud/temperatura se indica No disponible.

**Analizar carpetas** inicia una lectura cancelable en Task.Run, no automática al abrir Con-E. Sólo consulta metadatos: no abre el contenido ni cambia o elimina archivos. La distribución estima espacio asignado mediante FILE_STANDARD_INFO y usa tamaño comprimido para archivos comprimidos/dispersos. Los enlaces duros se cuentan una vez por identidad de archivo; no se siguen enlaces de directorio ni se descargan marcadores de nube. Las rutas sin acceso o demasiado largas se omiten y se indica lectura parcial. La suma incluye espacio libre y un residual Sistema/sin analizar para metadatos, reservas y espacio no atribuible. Si la suma no es compatible con la capacidad actual, se muestra sólo distribución de archivos leídos en lugar de falsear porcentajes del volumen.

El panel pausa la telemetría cuando está oculto y cancela sus tareas al cerrarse. Los cambios de unidad invalidan resultados anteriores. El análisis no reemplaza una herramienta SMART ni certifica el estado físico del dispositivo. RAID/Storage Spaces o adaptadores USB pueden impedir asociar la telemetría de un único disco.

Pruebas: análisis nativo de una carpeta temporal con archivos y un enlace duro; recuento sin duplicación, conservación de contenido y cancelación; coherencia de suma del gráfico y capturas en varios anchos; consulta de E/S WMI. Los gráficos de ejemplo usan datos simulados.


## Sensores y SMART — v0.14.0

1. Capacidad en anillo y barra horizontal inferior; distribución de carpetas en segmentos y leyenda, sin gráfico de torta.
2. **Temperaturas y sensores** muestra CPU, GPU, placa/chasis y discos con nombre original del sensor, temperaturas, ventiladores en RPM y mínimos/máximos de la sesión. Los máximos de las tarjetas son la mayor temperatura actual entre los sensores de ese grupo, no el límite térmico del equipo.
3. LibreHardwareMonitor 0.9.6 y DiskInfoToolkit consultan SMART: estado, vida restante estimada, temperatura, horas encendido y GB acumulados leídos/escritos cuando existen datos verificables. Un contador no informado no se muestra como cero. No se inventan horas separadas de lectura/escritura.
4. Fabricación sólo se muestra cuando el proveedor de almacenamiento de Windows informa ManufactureDate. No se deduce de la BIOS, del firmware ni de la instalación de Windows.
5. El módulo de sensores se abre a petición y consulta cada 3 segundos en otro proceso. **Habilitar lectura con administrador** eleva sólo ese módulo; la ventana principal permanece abierta sin elevarse. **Detener** cierra su conexión y termina las lecturas. Reiniciar la lectura reinicia los extremos de sesión.
6. Algunos sensores de CPU/placa requieren el controlador oficial PawnIO. Su instalador firmado se incluye en Sensors y sólo se ejecuta al pulsar **Instalar controlador PawnIO…** y confirmar. Instala un componente en Windows; requiere privilegios. Si Windows solicita reinicio para ese controlador, debe respetarse; Con-E no reinicia el equipo. No se usa WinRing0.

Los datos SMART pertenecen al dispositivo físico, no a cada partición. Se asocian únicamente con el número de disco y la serie coincidentes; un volumen sobre varios discos no recibe datos arbitrarios de uno de ellos. USB/RAID, firmware, permisos o discos bloqueados pueden impedir lecturas. El estado SMART y la vida estimada no garantizan ausencia de fallos. Placa/chasis no equivale a temperatura ambiente.

Se mantienen el tema claro, las consultas de CPU/WMI en segundo plano y el análisis de carpetas a petición. Este módulo no modifica particiones ni escribe valores de control en sensores o ventiladores. Los gráficos de pruebas usan datos simulados. Las pruebas de CI verifican lectura IPC real, interfaz y extremos; el runner no permite comprobar la compatibilidad de cada sensor físico ni el diálogo UAC interactivo.

Dependencias y licencias en THIRD_PARTY_NOTICES.md y Licenses/. Extrae el paquete completo: el ejecutable necesita sus DLL acompañantes.

El paquete de sensores 0.14 selecciona las bibliotecas nativas win-x64 de NuGet. No es una compilación para Windows x86 ni ARM64 nativo.

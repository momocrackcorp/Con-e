## Con-E 0.16.3 · diseño claro y detalles por tarjeta

- Navegación sobre la barra de acciones, tarjetas con bordes suaves y CPU con barra degradada.
- Mostrar/ocultar detalles desde el encabezado de cada tarjeta.
- RAM: anillo ampliado, miniaturas agrupadas y un único control para todas las ranuras; capacidad y uso siempre visibles.
- GPU separadas con sus fabricantes y capacidades reales; sin inferir salud o tipo de VRAM.

# Con-E v0.16.3 — diagnóstico

El gestor de particiones se ha extraído a [WinPart](WinPart/README.md), un proyecto y ejecutable independientes. Con-E conserva las tarjetas de hardware, inventario de controladores, logos, informes y herramientas. Ya no contiene la pestaña Mapa de discos ni operaciones de modificación de particiones.

## Compilación y descargas

GitHub Actions genera dos paquetes separados: Con-E-v0.16.3-portable y WinPart-v0.8.0-portable. Con-E 0.14 requiere Windows de 64 bits y .NET Framework 4.8; WinPart conserva sus requisitos anteriores. Con-E no necesita WinPart para ejecutarse.

El historial de cambios anterior se conserva en Git. El código extraído está en WinPart/src y sus pruebas de reducción/ampliación se ejecutan sólo sobre un VHD desechable del runner. Con-E mantiene sus pruebas de interfaz y datos simulados. WinPart tiene su propia entrada de aplicación y proyecto, sin compilar los archivos de Con-E.


## RAM por posición v0.15.2

Vista de módulos y posiciones SMBIOS: ocupada, libre informada o ubicación no confirmada. Se decodifican estructuras SMBIOS tipo 17, incluidas posiciones vacías, y se vinculan por nombre con Win32_PhysicalMemory para mostrar capacidad, DDR, velocidad, fabricante, pieza y serie. Cada módulo tiene detalles expandibles. Si sólo se conoce el número total, las posiciones adicionales no se identifican como ranuras físicas accesibles. La RAM soldada puede figurar como dispositivo. El total instalado se suma desde capacidades de módulos; el uso sigue siendo una lectura del sistema operativo.


## Núcleos e hilos v0.15.2

Procesador muestra un cuadro por núcleo físico con el número de hilos lógicos asociados. La relación se obtiene con GetLogicalProcessorInformationEx, sin asumir dos hilos por núcleo. En sistemas híbridos pueden aparecer núcleos de uno y dos hilos; no se infiere que sean P/E ni representa uso de cada núcleo en tiempo real; la barra de CPU muestra el uso global. Se conservan identificación Intel/AMD/ARM, datos WMI y detalle expandible. Si Windows no proporciona la topología se indica en lugar de generar un esquema inventado. En equipos de más de 256 núcleos el esquema se limita a 256 cuadros, aunque el resumen indica el total.


## Interfaz moderna y CPU en vivo — v0.15.2

Con-E incorpora controles propios en el espacio de nombres `UIModernization`: tarjetas claras con bordes redondeados y barras de progreso de trazo suave. No necesita instalar una biblioteca gráfica adicional. La distribución sigue adaptándose al ancho de la ventana; conserva inventario, RAM por ranura, vídeo, discos, BIOS, informes, hardware/controladores y herramientas. El diagrama de núcleos/hilos se despliega en Ver detalle del procesador. Los logotipos conservan una pequeña base clara para mantener sus colores y legibilidad.

La CPU se consulta con `Win32_PerfFormattedData_PerfOS_Processor`, instancia `_Total`, usando `Task.Run`. El ciclo espera la consulta anterior y un intervalo de 1500 ms; no se superponen consultas ni se reconstruye el panel en cada muestra. Después de `await`, la actualización vuelve al hilo WinForms. Se cancela al cerrar; las consultas WMI en curso liberan sus recursos cuando terminan. Los errores muestran Sin lectura y se reintentan; no se presentan porcentajes simulados en ejecución normal. El inventario completo ya se consulta en segundo plano con Task.Run. RAM y discos mantienen la lectura del último inventario, no son monitores continuos.

Las pruebas incluyen muestras controladas en un trabajador, comprobación de actualización en el hilo de interfaz, ausencia de solapamiento, cancelación, estado sin datos y consulta WMI real. Se generan capturas con tarjetas expandidas, ventana ancha/estrecha y escala doble. Las capturas de diseño usan datos simulados y no representan el equipo del usuario.


### Tema claro — v0.15.2

Fondo gris claro, tarjetas blancas, textos oscuros y acentos azules. Conserva componentes modernos, métricas WMI en segundo plano, cancelación y tarjetas adaptables. Se retira el tema oscuro como apariencia predeterminada.


## Almacenamiento gráfico — v0.15.2

La tarjeta Almacenamiento incorpora selector de unidad, anillo de porcentaje usado, espacio total/usado/libre y sistema de archivos. Incluye unidades fijas y extraíbles que Windows pueda leer. **Ver uso y analizar carpetas** abre un panel independiente con capacidad, modelo y tipo cuando pueden asociarse, estado de salud informado por Windows, temperatura disponible y lectura/escritura WMI en segundo plano. No se inventa un porcentaje de vida útil; si el controlador o los permisos no entregan salud/temperatura se indica No disponible.

**Analizar carpetas** inicia una lectura cancelable en Task.Run, no automática al abrir Con-E. Sólo consulta metadatos: no abre el contenido ni cambia o elimina archivos. La distribución estima espacio asignado mediante FILE_STANDARD_INFO y usa tamaño comprimido para archivos comprimidos/dispersos. Los enlaces duros se cuentan una vez por identidad de archivo; no se siguen enlaces de directorio ni se descargan marcadores de nube. Las rutas sin acceso o demasiado largas se omiten y se indica lectura parcial. La suma incluye espacio libre y un residual Sistema/sin analizar para metadatos, reservas y espacio no atribuible. Si la suma no es compatible con la capacidad actual, se muestra sólo distribución de archivos leídos en lugar de falsear porcentajes del volumen.

El panel pausa la telemetría cuando está oculto y cancela sus tareas al cerrarse. Los cambios de unidad invalidan resultados anteriores. El análisis no reemplaza una herramienta SMART ni certifica el estado físico del dispositivo. RAID/Storage Spaces o adaptadores USB pueden impedir asociar la telemetría de un único disco.

Pruebas: análisis nativo de una carpeta temporal con archivos y un enlace duro; recuento sin duplicación, conservación de contenido y cancelación; coherencia de suma del gráfico y capturas en varios anchos; consulta de E/S WMI. Los gráficos de ejemplo usan datos simulados.


## Sensores y SMART — v0.15.2

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


## Discos físicos y miniaturas compartidas — v0.15.2

El dashboard muestra una tarjeta por disco enumerado por Windows, con número, modelo, capacidad, medio HDD/SSD informado y conexión independiente (SATA, NVMe, USB, RAID, etc.). Los discos virtuales se identifican como virtuales. El medio se vincula por identificador persistente o serie, nunca por la posición de MSFT_PhysicalDisk. M.2 sólo se etiqueta cuando aparece explícitamente en el modelo; NVMe no demuestra M.2. Las miniaturas son ilustrativas, no fotografías del equipo.

**Ver disco y volúmenes** abre Detalle de discos: selector físico y botones para sus particiones, incluidas las que no tienen letra. La capacidad, espacio usado y disponible pertenecen al volumen; SMART, temperatura y horas al disco físico. Si no se conoce el espacio libre, el anillo no inventa un porcentaje. Una partición sin letra conserva sus datos sin montarse ni modificarse; el análisis de carpetas se habilita sólo cuando hay una letra legible.

La barra de contenido está inmediatamente debajo del anillo. Su leyenda seleccionable muestra las carpetas de la raíz, contando también sus subcarpetas, sin listar archivos individuales. Los archivos sueltos se agrupan en Archivos en raíz; metadatos y omisiones aparecen como Sistema/sin analizar cuando la capacidad permite compararlo. Se muestran todas las carpetas leídas, con lista desplazable. El análisis lee metadatos en segundo plano, se puede cancelar y se conserva en caché por identidad de disco y partición, con hora de la lectura. Recorrer muchos archivos puede tardar; no se promete lectura instantánea de la MFT.

Tarjetas con borde oscuro y tarjeta seleccionada con borde azul más grueso. Miniaturas compartidas desde Shared/ en Con-E y WinPart, sin una dependencia gráfica adicional.


## RAM compacta y HDD detrás de RAID — v0.15.2

Las posiciones RAM se muestran en filas con miniatura pequeña, nombre/estado, capacidad/tipo/velocidad y enlace Detalles. Se organizan en dos columnas cuando la tarjeta tiene al menos 420 píxeles lógicos de ancho; en tarjetas estrechas se usa una columna. Los detalles se expanden dentro de la fila. El anillo y los valores de uso comparten una fila. Se mantienen la identificación SMBIOS y la información de módulos, sin reducir la fuente principal.

La detección compartida con WinPart incorpora RPM, un catálogo exacto de modelos WD documentados y la consulta de sólo lectura StorageDeviceSeekPenaltyProperty. Incluye WD10EARX y WD10EAVS, aunque la conexión figure como RAID y MediaType no esté informado. No se supone que RAID sea un medio, que TRIM pruebe SSD ni que ausencia de penalización implique SSD. Fuentes del catálogo y del API en Shared/MODEL_SOURCES.md, también incluido en las licencias del paquete.


## Elevación de WinGet, icono y marcas de GPU — v0.15.2

**Actualizar aplicaciones (administrador)** abre CMD mediante ShellExecute/runas y solicita UAC antes de ejecutar `winget upgrade --all`. Con-E mantiene su ventana y permisos normales. **Consultar aplicaciones actualizables** se abre sin elevarse. La consola parte del directorio de sistema y desactiva AutoRun de CMD. Cancelar UAC muestra un mensaje de cancelación; WinGet ausente y errores de apertura se informan en la barra de estado. El proceso principal no se reinicia. La consola queda abierta para revisar resultados y solicitudes de los instaladores.

Para abrir manualmente otra consola elevada desde CMD:

```cmd
powershell -NoProfile -Command "Start-Process cmd.exe -Verb RunAs"
```

Se genera un icono propio de Con-E (monitor con pulso) durante `dotnet build`: tamaños 16, 20, 24, 32, 40, 48, 64, 96, 128 y 256. Se incrusta en el recurso nativo del EXE y en el formulario, y se incluyen Con-E.ico y Con-E-icon.png para accesos directos. La generación usa sólo PowerShell/System.Drawing en Windows; no requiere paquetes gráficos adicionales.

La tarjeta de video separa cada adaptador físico con su propio logo. DXGI proporciona VendorId y reservas de memoria por adaptador; NVIDIA/Intel/AMD se resuelven por identificador antes que por nombre. Si DXGI no enumera hardware, se conserva identificación WMI y PCI sin inventar cantidades de memoria. Los adaptadores de software, como Microsoft Basic Render Driver, se conservan en los detalles y no se presentan como otra GPU física. No se deduce integrada/dedicada sólo por la marca o por una reserva de memoria.

Pruebas: configuración de procesos sin ejecutar actualizaciones, cancelación UAC simulada, WinGet ausente, logos de dos GPU, lectura DXGI real del runner, iconos nativos y tamaños, capturas en ancho normal/estrecho. La aprobación UAC interactiva y el resultado de actualizaciones reales se deben comprobar en el equipo del usuario.

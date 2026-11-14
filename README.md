# Con-E v0.6.2 — hardware y controladores

Herramienta portátil USB para Windows 10 y 11. Requiere .NET Framework 4.8 disponible en el equipo. Interfaz nativa Windows Forms: no incorpora un navegador, un servicio ni un controlador propio.

## Interfaz

- Tarjetas con ilustraciones de componentes dibujadas localmente, indicador circular de RAM y barras de ocupación de unidades.
- Los detalles de Windows, BIOS, discos e incidencias se despliegan dentro de la misma tarjeta con un segundo clic para ocultarlos. La tarjeta crece y la distribución se reajusta.
- Ayuda sencilla opcional y diseño de una, dos o tres columnas según el ancho.
- Se retiraron la fecha de instalación de Windows y el supuesto último formateo. La fecha de BIOS y las fechas de controladores sí se conservan como datos de esos componentes.

## Explorador de hardware y controladores

La pestaña incluye un árbol de categorías, una lista de componentes y una ficha de propiedades del componente seleccionado:

- Procesadores: modelo, fabricante, núcleos, hilos, socket, frecuencia máxima y cachés informadas.
- Módulos RAM: ranura, fabricante, pieza, serie, capacidad, velocidad y código de tipo SMBIOS.
- Placa base y BIOS: modelo, versión, fabricante y series disponibles.
- Discos físicos: modelo, capacidad, interfaz informada, serie y firmware.
- Gráficos: modelo, procesador gráfico, controlador y resolución informada.
- Red, audio, USB y otros dispositivos PnP.
- Ficha de controlador asociada por ID PnP: proveedor, versión, fecha, INF y firma informada por Windows.
- Categoría Por revisar para dispositivos con código PnP distinto de cero.
- Exportación TXT del resumen, hardware, controladores e incidencias de lectura.

## Dispositivos desconocidos

Se consultan dispositivos PnP aunque falte su controlador firmado. Cuando Windows expone sus IDs se muestran HardwareID, CompatibleID e ID de instancia. Se separan códigos PCI VEN/DEV/SUBSYS/REV o USB VID/PID/REV para facilitar su investigación.

El botón Buscar ID en internet abre el navegador con el primer HardwareID disponible (no usa el ID de instancia como búsqueda). No descarga ni instala controladores. Copiar ficha copia explícitamente todas las propiedades visibles, incluyendo IDs y series cuando existan.

No hay todavía una base de nombres PCI/USB integrada. Los códigos no se traducen automáticamente en un fabricante o modelo comercial. Un dispositivo desconocido no revela necesariamente sus prestaciones: no se inventan capacidad, velocidades o características ausentes. Si Windows no enumera un dispositivo ni proporciona un ID, esta versión no puede identificarlo. El inventario se basa en WMI/SMBIOS de Windows; no equivale a todas las funciones de AIDA32, ni incluye sensores o benchmarks.

Las propiedades dependen de lo informado por el sistema y el fabricante. Un código PnP cero no certifica funcionamiento correcto; no encontrar un registro firmado no demuestra por sí solo que falte un controlador. Capacidad en bytes y frecuencias conservan las unidades informadas por WMI. No se deduce la salud de un disco de su espacio libre. Las lecturas de uso se actualizan al pulsar Actualizar, no continuamente.

## Compilar y probar

GitHub Actions compila el ejecutable y ejecuta pruebas de interfaz con datos simulados. Se comprueba expansión y contracción de tarjetas, extracción de IDs PCI/USB, ficha de dispositivo desconocido, navegación, explicaciones, varios anchos y datos ausentes. Las capturas se guardan en `Con-E-interfaz-pruebas` y no se añaden al paquete portátil.

Descargar `Con-E-v0.6.2-portable`, extraer todo en el USB y abrir Con-E.exe. No ejecutar desde dentro del ZIP.

Pendiente de validar en equipos reales: comparación con Administrador de dispositivos y msinfo32, dispositivos sin controlador, distintas escalas DPI de Windows, permisos limitados, funcionamiento desde USB y comportamiento con proveedores WMI bloqueados. La prueba de escala programática no sustituye la validación en una pantalla con DPI real diferente. El tiempo límite WMI depende del comportamiento de cada proveedor.

La búsqueda/reseña automática del fabricante y una base de modelos para resolver IDs siguen pendientes. No se incluyen programas de terceros.

## Referencias técnicas

- [Win32_PnPEntity — Microsoft](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpentity)
- [Hardware IDs — Microsoft](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/hardware-ids)
- [Identificadores PCI — Microsoft](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/identifiers-for-pci-devices)
- [Identificadores USB — Microsoft](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/standard-usb-identifiers)


## Cambios v0.6.2

Tarjetas ordenadas A–H: CPU, RAM, video, discos, red, BIOS, Windows y usuario. La identificación del equipo queda a continuación. RAM muestra posiciones SMBIOS y módulos por ranura con tipo y capacidad; puede incluir memoria soldada. Storage WMI distingue medio HDD/SSD y bus disponible, sin inferir formato M.2. Particiones y volúmenes se presentan por separado: no se ha implementado todavía su asociación gráfica completa por disco.

DXGI consulta memoria dedicada de video, memoria de sistema dedicada y memoria compartida disponible. Es capacidad informada, no consumo actual. La clasificación integrada/dedicada sigue sin confirmar: una reserva dedicada no demuestra la existencia de chips VRAM separados.

La clave OEM se consulta sólo mediante un botón y no se exporta en el informe. Puede faltar, o ser distinta de la licencia activa. El usuario actual se muestra; no se recuperan contraseñas de Windows. Las marcas se presentan como texto; los logos oficiales y calcomanías de fabricante quedan pendientes.

Herramientas incluye consulta y actualización de aplicaciones con WinGet, si está disponible para ese usuario, y acceso a actualizaciones opcionales de Windows para revisar controladores. La actualización WinGet se inicia sólo al pulsar su botón; no acepta acuerdos automáticamente. No es un actualizador general de drivers.

La navegación hardware tiene divisor ajustable y columnas adaptables para resolver las capturas con textos truncados. Los datos extensos conservan desplazamiento horizontal.


## Logos y miniaturas v0.6.2

11 marcas incluidas sin descargas durante la ejecución: Intel, AMD, ARM, HP, Dell, Lenovo, ASUS, Acer, NVIDIA, Samsung y Qualcomm. Se seleccionan por coincidencia del nombre informado por Windows, no por inferencia de IDs desconocidos. Sin coincidencia se conserva la ilustración genérica. Las imágenes se integran en el ejecutable. No se usan calcomanías de generación Core/Ryzen para no atribuir una certificación o generación no verificada.

Miniaturas vectoriales locales de procesador, módulo RAM, tarjeta de video, HDD, SSD, módulo de almacenamiento NVMe, red y BIOS/placa. Son esquemas orientativos, no fotos del modelo real. La miniatura NVMe no confirma un formato físico M.2. Cuando hay varios discos, el resumen no representa físicamente todos los modelos.

Fuente de logos: Simple Icons (https://github.com/simple-icons/simple-icons), versiones monocromáticas con color identificativo; no implica afiliación. Ver assets/brands/ATTRIBUTION.md. Las pruebas validan selección de marcas y decodificación de los 11 recursos, además del diseño adaptable.


## Mapa de discos v0.6.2

Nueva pestaña dentro de Con-E, accesible también desde Almacenamiento. Cada disco físico tiene mapa proporcional por offset y tamaño, lista seleccionable y ficha inferior. Muestra GPT/MBR, EFI, MSR, recuperación, particiones y los intervalos no particionados de una lectura completa. Estos intervalos pueden contener alineación o metadatos. Las particiones muy pequeñas se pueden seleccionar por la lista; las flechas del teclado recorren el mapa. No se ensancha artificialmente la representación de una partición pequeña para ponerle texto.

La asociación de letras, formato, tamaño del volumen y espacio libre se consulta cuando la partición tiene letra. Los volúmenes sin letra conservan su partición pero sus datos de espacio libre se muestran como no disponibles. No se adivina su ocupación. Los discos con particiones solapadas no reciben un mapa proporcional; se conserva la lista. Una lectura parcial no genera intervalos libres ficticios.

Esta entrega es una vista interactiva de consulta. No crea, elimina, formatea, mueve ni redimensiona particiones. El gestor de edición completo sigue pendiente. Algunas consultas Storage WMI pueden requerir permisos adicionales o no estar disponibles; las incidencias se informan.

Las pruebas automáticas añaden un disco simulado con selección y comprobación de intervalos completos frente a lectura parcial. La distribución real y USB/RAID/dinámicos debe validarse en equipos físicos.


## Corrección v0.6.2

Se corrigió la consulta de MSFT_Partition: MbrType sustituye una propiedad Type inexistente. Se añadió lectura alternativa Win32_DiskPartition y asociación Win32_LogicalDisk cuando Storage WMI falla o no devuelve particiones de un disco inicializado. La lectura alternativa no deduce intervalos sin asignar porque no garantiza todas las particiones reservadas. La cabecera indica la fuente utilizada y el fallo de enumeración. Se mejoraron unidades MiB para bloques pequeños y el ancho DPI de columnas, incluyendo Detalles técnicos.

La validación de GitHub ahora ejecuta también la consulta real sobre los discos del runner Windows y exige encontrar particiones, además de los datos simulados. No sustituye la prueba sobre el equipo del usuario.


## Corrección de altura v0.6.2

El mapa y la lista utilizan un panel de filas con altura calculada a partir de la fuente y el número de particiones, en lugar de un contenedor fijo de 260 píxeles. La lista conserva al menos tres filas visibles y el detalle inferior reserva seis líneas. Se selecciona inicialmente la primera partición, y la selección desde lista o barra actualiza la ficha.

Se añade una prueba de fuente de 20 puntos que regenera el mapa, exige altura útil para filas, comprueba límites y selecciona un intervalo mediante la lista. No equivale a una prueba en todos los monitores DPI reales.


## Edición v0.7.0

Botón Reducir / ampliar partición para GPT de datos básicos con letra y formato NTFS, leído por Storage WMI con identidad de disco. Consulta GetSupportedSize y aplica Resize mediante WMI. Requiere administrador; puede reabrirse mediante UAC y no ejecuta automáticamente ninguna operación al elevar. Muestra tamaño final en MiB, exige escribir APLICAR y muestra confirmación de destino/tamaño. Revalida identidad de disco, tamaño, número y offset de partición antes de aplicar. Reconsulta límites inmediatamente antes del cambio y refresca el mapa al terminar.

No admite crear, formatear, eliminar, mover, convertir ni modificar EFI/MSR/recuperación. No puede ampliar a través de una partición intermedia. Windows puede rechazar el cambio por cifrado, archivos inmovibles o estado del volumen; un respaldo previo sigue siendo necesario. No hay promesa de ausencia de pérdida de datos.

CI incorpora una prueba sobre VHD temporal propio: crea GPT y NTFS, reduce de 180 a 128 MiB, amplía a tamaño inicial y comprueba contenido de un archivo, luego desmonta el VHD. No usa discos del usuario. La consulta original de sólo lectura ahora tiene esta operación de modificación explícita.

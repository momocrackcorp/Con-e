# WinPart v0.9.0 (nombre provisional)

Proyecto Windows independiente extraído de Con-E. Ejecutable WinPart.exe, sin dependencia de Con-E ni herramientas externas de interfaz. Usa .NET Framework 4.8 y Storage WMI de Windows.

## Disponible

Mapa visual de discos, lista de particiones y ficha; editor de tamaño por arrastre o MiB; operaciones pendientes; Aplicar con resumen final y Descartar. Reducción/ampliación de GPT NTFS de datos básicos con letra y permisos de administrador. Revalida identidad de disco, offset, tamaño y límites antes de modificar. Respaldar los datos antes de aplicar. No hay reversión automática de operaciones ya completadas.

## Pendiente para un gestor completo

Mover, copiar particiones y más sistemas de archivos. No es un port completo de GParted: adapta la paleta de Utils::get_color; no incorpora el motor libparted. El objetivo es desarrollar un gestor visual para Windows; cualquier reutilización futura de código GPL requiere revisar dependencias y licencias.

## Compilar

dotnet build WinPart/src/WinPart.csproj -c Release

GitHub genera WinPart-v0.9.0-portable por separado de Con-E. Las pruebas incluyen interfaz, cola sin escritura y reducción/ampliación con conservación de archivo sobre VHD desechable propio. En esta etapa ambos proyectos están en carpetas distintas de un mismo repositorio; WinPart se puede mover a su propio repositorio sin cambios de código.


## Sistemas de archivos v0.4.0

Columnas Partición, Sistema de archivos, Montaje, Etiqueta, Tamaño, Usado, Libre e Indicadores. Windows proporciona tamaños de volúmenes con letra; las particiones sin volumen consultable muestran datos desconocidos.

Sistema de archivos propone NTFS, exFAT y FAT32. La enumeración GetSupportedFileSystems no se usa como lista exhaustiva: algunos proveedores omiten exFAT. La compatibilidad real, el tamaño y la identidad se comprueban al aplicar. No hay ext4/btrfs. Añadir formateo sólo prepara una operación y muestra la transición pendiente. Aplicar muestra un resumen que advierte que FORMATEAR BORRA TODOS LOS ARCHIVOS de cada volumen indicado. No es una conversión conservando datos. Descartar no modifica el disco.

La edición de formato está limitada a volúmenes con letra sobre GPT de datos básicos identificados inequívocamente. Bloquea particiones de sistema/arranque, la unidad de Windows, la unidad donde reside WinPart y volúmenes con paginación activa. Revalida identidad de disco/partición, offset/tamaño, letra, identidad del volumen y compatibilidad antes de escribir. No fuerza desmontaje: Windows puede rechazar un volumen en uso. No pretende reemplazar todas las capacidades de GParted.

Sólo hay una operación pendiente por partición: preparar un formateo sustituye un redimensionamiento pendiente de esa partición y viceversa. La ejecución de varias operaciones no es transaccional y no tiene rollback.

Prueba en VHD desechable: reducir/ampliar NTFS conservando archivo; formatear exFAT y verificar desaparición del archivo y nuevo formato; formatear de nuevo NTFS y verificar escritura. No se realizan estas pruebas sobre datos del usuario.


## Mapa visual y software libre v0.4.0

Bordes de sistemas de archivos con la paleta adaptada de GParted, interior amarillo para usado, blanco para libre y rayado para ocupación desconocida. Selección en naranja, identificadores y tamaños en cada bloque cuando caben; la tabla conserva la información de particiones pequeñas. Las particiones diminutas se ensanchan en pantalla para ser seleccionables; las proporciones visuales son aproximadas y los valores de tabla son los datos leídos.

Dos particiones NTFS comparten color de sistema de archivos, como en GParted. Se distinguen por letra, tamaño, posición y distribución usado/libre. El color no se asigna arbitrariamente por unidad. No se inventa el sistema de archivos de EFI o recuperación: se intenta consultar el volumen asociado aun sin letra y se muestra desconocido si no se obtiene.

WinPart se distribuye gratuitamente como software libre GPL-2.0-or-later. El paquete incluye COPYING, atribuciones, fuente completa de WinPart y fragmento upstream de la paleta. Hay créditos dentro de la aplicación. Es una aplicación Windows independiente, no una versión oficial de GParted; su motor de operaciones sigue siendo Storage WMI de Windows. Mover y copiar particiones siguen pendientes. Nueva partición permite crear en intervalos sin asignar de discos GPT.


## Elevación al aplicar v0.4.0

Preparar tamaño o formato ya no relanza la aplicación ni solicita UAC. Aplicar conserva la ventana principal y lanza un proceso auxiliar elevado del mismo ejecutable, sólo con la cola confirmada. Si la aplicación ya es administradora, no requiere otra elevación. Cancelar UAC conserva las operaciones pendientes.

El auxiliar admite únicamente operaciones conocidas de tamaño/formato. Recibe un plan XML codificado en línea de comandos, no scripts ni rutas de archivos a ejecutar. Revalida identidad, offset/tamaño de partición, límites y protecciones antes de escribir. No usa un servicio privilegiado persistente. Cierra al terminar; la interfaz actualiza el mapa. No hay rollback de operaciones ya completadas si falla una posterior.

Cuando el usuario sin elevar no puede consultar límites exactos o formatos compatibles, puede preparar una propuesta claramente indicada como pendiente de comprobación. El auxiliar la valida antes de la primera modificación y la rechaza si Windows no la admite. Los límites propuestos no implican que una ampliación sea posible a través de otras particiones.

Las pruebas comprueban serialización del destino y ejecutan el proceso auxiliar real sobre un VHD desechable. La prueba automática hereda un token ya elevado del runner; no simula la interacción humana con el cuadro UAC en una sesión estándar. Crear, mover, copiar y borrar siguen pendientes.

## Disco virtual de prueba v0.5.0

El menú **Disco virtual de prueba** permite crear un VHD dinámico nuevo (256–65536 MiB), conectar un VHD existente y desconectarlo conservando el archivo. Cada acción se prepara en la cola y sólo se ejecuta después de confirmar **Aplicar**. El auxiliar elevado se inicia en ese momento; la ventana principal permanece abierta.

La creación prepara GPT, una partición NTFS `WINPART_TEST` que ocupa la mitad de la capacidad virtual y espacio sin asignar para pruebas. El archivo consume espacio real conforme se escriben datos; no modifica las particiones del disco que lo aloja. No se sobrescriben VHD existentes. Guarda el archivo en un disco local con espacio suficiente. Al reconectar se conservan sus particiones y contenido. La desconexión se aplica por separado de cambios de particiones y bloquea discos usados por Windows, paginación o la propia aplicación.

Backend: API nativa `virtdisk.dll` para crear/conectar/desconectar y cmdlets Storage de Windows PowerShell para preparar GPT/NTFS. No requiere Hyper-V ni módulos de pago. Las pruebas de GitHub crean su propio VHD desechable, verifican escritura, desconexión/reconexión y conservación del archivo. El VHD conectado aparece en el mapa al actualizarse después de aplicar.

Esta versión permite probar las operaciones ya disponibles de tamaño y formato; copiar y mover particiones siguen siendo pasos siguientes del proyecto.


## Nueva partición v0.6.0

Selecciona un bloque o fila **Sin asignar** y pulsa **Nueva partición…**. Elige tamaño en MiB, sistema de archivos (NTFS, exFAT, FAT32 o Sin formatear/RAW) y etiqueta opcional. Windows asigna una letra disponible. **Añadir a pendientes** sólo prepara la operación; **Aplicar** muestra el resumen y solicita UAC cuando es necesario, sin reiniciar la ventana. Descartar no escribe nada.

Sólo se admite GPT leído íntegramente, con identidad inequívoca. Se alinean los comienzos a 1 MiB y se reservan los extremos para metadatos. Se rechazan los huecos diminutos, tamaños fuera del intervalo, operaciones superpuestas y cambios de identidad o de espacio libre. La nueva partición no borra las existentes. Si se crea pero falla el formato, se conserva y se informa del resultado parcial para revisarlo en el mapa; no se elimina silenciosamente.

FAT32 está limitado por este motor a 32 MiB–32 GiB; el mínimo general de creación es 16 MiB. RAW significa partición creada sin sistema de archivos. ext4, btrfs y swap no se ofrecen como operaciones disponibles: falta implementar sus motores. ReFS tampoco está implementado.

Las pruebas sobre VHD desechable crean NTFS, FAT32, exFAT y RAW, comprueban el offset, tamaño, etiqueta/letra, escritura en los formatos disponibles, conservación del contenido existente y rechazo de una instantánea obsoleta.


## Barra gráfica y eliminación v0.7.0

Barra con iconos grandes de trazo redondeado, colores por acción y texto debajo: Actualizar, Crear partición, Eliminar, Formatear, Ajustar tamaño, Disco virtual, Aplicar, Descartar y Acerca de. Los glifos vectoriales son originales de WinPart, con estética de contorno similar a Lucide; no se incorporan iconos ni código de WinRAR o Lucide. Se rasterizan al DPI de la ventana sin descargas ni dependencias nuevas. La barra pasa a varias filas en ventanas estrechas.

**Eliminar** prepara en la cola la eliminación de una partición de datos básicos GPT con letra. Se marca en rojo en el mapa y la tabla; el disco se mantiene intacto hasta confirmar Aplicar. El resumen identifica disco, número de partición, letra y tamaño y advierte de la pérdida de los archivos. Descartar no modifica el disco. UAC sólo al aplicar, mediante auxiliar; la ventana sigue abierta. No se fuerza la eliminación de un volumen en uso.

Se bloquean Windows, sistema/arranque, EFI, MSR, recuperación, la partición que contiene WinPart y volúmenes con paginación activa. Se comprueban también rutas de montaje en carpetas. Antes de cada eliminación se revalida identidad del disco y volumen, número, offset, tamaño, letra y tipo GPT. No se ofrece eliminación MBR en esta versión.

Los encabezados indican VHD/imagen respaldada por archivo (BusType 15), disco virtual (14), USB (7) o tipo no informado cuando corresponde. Las filas muestran icono de GPT, primaria/lógica MBR (Win32_DiskPartition.PrimaryPartition, asociada por disco y offset), sistema/EFI/MSR, recuperación o espacio sin asignar. GPT no se etiqueta como primaria/lógica MBR. Se mantiene el color del sistema de archivos como indicador complementario.

Las pruebas usan un VHD propio desechable: crear cuatro particiones, eliminar la exFAT, verificar el hueco resultante y que el contenido de otra partición no cambia. Se comprueban protecciones, serialización, preparación/descarte sin escritura y clasificación de iconos. Las capturas incluyen barra normal y estrecha.


## Operaciones avanzadas v0.9.0

La barra **Avanzadas** permite preparar (1) eliminación de una partición seleccionada aunque no tenga letra o sea EFI, MSR, recuperación o un tipo desconocido por Windows; y (2) vaciar el disco completo y preparar una tabla GPT o MBR. No necesita interpretar ni descifrar el sistema de archivos. La creación se amplía a primarias MBR (hasta cuatro, sin contenedores extendidos; límite conservador de 2 TiB) y la edición de datos NTFS/formatos Windows reconoce MBR además de GPT.

No hay un explorador de archivos Linux/macOS ni desbloqueo de cifrado integrado. Eliminar una partición cifrada no permite leer sus archivos y no es un borrado seguro. El motor no identifica de forma exhaustiva todos los esquemas de cifrado, RAID, LVM o contenedores. Mover/copiar particiones, crear lógicas MBR y redimensionar formatos ajenos al motor nativo siguen pendientes.

La cola describe claramente partición frente a DISCO COMPLETO. Preparar o descartar no escribe. Vaciar sustituye las operaciones pendientes del mismo disco; debe aplicarse por separado de otras operaciones de ese disco. Se solicita administrador sólo al aplicar; la ventana principal continúa abierta. El resultado de DiskPart se comprueba mediante una lectura nueva del esquema, no por textos localizados ni sólo por el código de salida.

Antes de ejecutar se revalidan número, identidad persistente, capacidad y estilo del disco; para eliminar también número, offset, tamaño y tipo de partición. Se bloquean discos usados por el Windows actual para sistema/arranque, WinPart, paginación o rutas de volcado activas. Estos límites se aplican también a discos USB desde los que haya arrancado Windows. Dispositivos desconectados o de sólo lectura requieren resolver su estado antes. No se eliminan volúmenes de discos dinámicos/Storage Spaces con un motor de discos básicos.

La interfaz permite seleccionar un disco vacío pulsando su encabezado. El editor de tamaño aparece dentro de su tarjeta, con **Cancelar edición** siempre disponible aunque no haya pendientes. Cerrar edición cancela la propuesta no añadida; para quitar operaciones ya preparadas se usa Descartar. Se incorpora un icono original al ejecutable y una pantalla inicial de uso técnico, advertencia de pérdida de datos y ausencia de garantía conforme a COPYING.

Pruebas: VHD exclusivo desechable, eliminación EFI/recuperación/MSR/tipo Linux sin letra conservando un archivo en otra partición; preparación MBR, creación NTFS MBR y escritura; preparación GPT; rechazo del disco usado por Windows; serialización de planes; edición local y cancelación sin cola. Los tests no certifican todos los controladores USB ni todos los cifrados.

Para reconstruir con el icono en Windows: ejecutar `./WinPart/build-icon.ps1` antes de `dotnet build WinPart/src/WinPart.csproj -c Release`. El paquete fuente incluye el script y el icono generado.


## Miniaturas compartidas — v0.9.0

Las cabeceras de discos usan las mismas miniaturas de HDD, SSD y M.2 SATA/NVMe que Con-E. El medio HDD/SSD se consulta en Windows y la conexión se informa por separado. M.2 sólo se etiqueta cuando el modelo lo indica; la miniatura es orientativa. USB, SD/MMC, discos virtuales y medios desconocidos conservan representaciones genéricas. La tarjeta seleccionada tiene borde azul y las demás borde oscuro.

Los cambios son de inventario y presentación: las operaciones y sus validaciones conservan el motor existente. El paquete incluye source/WinPart y source/Shared con el recurso gráfico y el código compartido necesarios para recompilar: `dotnet build source/WinPart/src/WinPart.csproj -c Release`.

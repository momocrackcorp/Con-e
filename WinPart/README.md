# WinPart v0.5.0 (nombre provisional)

Proyecto Windows independiente extraído de Con-E. Ejecutable WinPart.exe, sin dependencia de Con-E ni herramientas externas de interfaz. Usa .NET Framework 4.8 y Storage WMI de Windows.

## Disponible

Mapa visual de discos, lista de particiones y ficha; editor de tamaño por arrastre o MiB; operaciones pendientes; Aplicar con resumen final y Descartar. Reducción/ampliación de GPT NTFS de datos básicos con letra y permisos de administrador. Revalida identidad de disco, offset, tamaño y límites antes de modificar. Respaldar los datos antes de aplicar. No hay reversión automática de operaciones ya completadas.

## Pendiente para un gestor completo

Crear, eliminar, formatear, mover, copiar particiones y más sistemas de archivos. No es un port completo de GParted: adapta la paleta de Utils::get_color; no incorpora el motor libparted. El objetivo es desarrollar un gestor visual para Windows; cualquier reutilización futura de código GPL requiere revisar dependencias y licencias.

## Compilar

dotnet build WinPart/src/WinPart.csproj -c Release

GitHub genera WinPart-v0.5.0-portable por separado de Con-E. Las pruebas incluyen interfaz, cola sin escritura y reducción/ampliación con conservación de archivo sobre VHD desechable propio. En esta etapa ambos proyectos están en carpetas distintas de un mismo repositorio; WinPart se puede mover a su propio repositorio sin cambios de código.


## Sistemas de archivos v0.4.0

Columnas Partición, Sistema de archivos, Montaje, Etiqueta, Tamaño, Usado, Libre e Indicadores. Windows proporciona tamaños de volúmenes con letra; las particiones sin volumen consultable muestran datos desconocidos.

Sistema de archivos abre un selector de formatos compatibles informados por MSFT_Volume.GetSupportedFileSystems, limitado por ahora a NTFS, exFAT y FAT32. No hay ext4/btrfs. Añadir formateo sólo prepara una operación y muestra la transición pendiente. Aplicar muestra un resumen que advierte que FORMATEAR BORRA TODOS LOS ARCHIVOS de cada volumen indicado. No es una conversión conservando datos. Descartar no modifica el disco.

La edición de formato está limitada a volúmenes con letra sobre GPT de datos básicos identificados inequívocamente. Bloquea particiones de sistema/arranque, la unidad de Windows, la unidad donde reside WinPart y volúmenes con paginación activa. Revalida identidad de disco/partición, offset/tamaño, letra, identidad del volumen y compatibilidad antes de escribir. No fuerza desmontaje: Windows puede rechazar un volumen en uso. No pretende reemplazar todas las capacidades de GParted.

Sólo hay una operación pendiente por partición: preparar un formateo sustituye un redimensionamiento pendiente de esa partición y viceversa. La ejecución de varias operaciones no es transaccional y no tiene rollback.

Prueba en VHD desechable: reducir/ampliar NTFS conservando archivo; formatear exFAT y verificar desaparición del archivo y nuevo formato; formatear de nuevo NTFS y verificar escritura. No se realizan estas pruebas sobre datos del usuario.


## Mapa visual y software libre v0.4.0

Bordes de sistemas de archivos con la paleta adaptada de GParted, interior amarillo para usado, blanco para libre y rayado para ocupación desconocida. Selección en naranja, identificadores y tamaños en cada bloque cuando caben; la tabla conserva la información de particiones pequeñas. Las particiones diminutas se ensanchan en pantalla para ser seleccionables; las proporciones visuales son aproximadas y los valores de tabla son los datos leídos.

Dos particiones NTFS comparten color de sistema de archivos, como en GParted. Se distinguen por letra, tamaño, posición y distribución usado/libre. El color no se asigna arbitrariamente por unidad. No se inventa el sistema de archivos de EFI o recuperación: se intenta consultar el volumen asociado aun sin letra y se muestra desconocido si no se obtiene.

WinPart se distribuye gratuitamente como software libre GPL-2.0-or-later. El paquete incluye COPYING, atribuciones, fuente completa de WinPart y fragmento upstream de la paleta. Hay créditos dentro de la aplicación. Es una aplicación Windows independiente, no una versión oficial de GParted; su motor de operaciones sigue siendo Storage WMI de Windows. Crear, mover, copiar y borrar particiones siguen pendientes y no se ofrecen como botones funcionales.


## Elevación al aplicar v0.4.0

Preparar tamaño o formato ya no relanza la aplicación ni solicita UAC. Aplicar conserva la ventana principal y lanza un proceso auxiliar elevado del mismo ejecutable, sólo con la cola confirmada. Si la aplicación ya es administradora, no requiere otra elevación. Cancelar UAC conserva las operaciones pendientes.

El auxiliar admite únicamente operaciones conocidas de tamaño/formato. Recibe un plan XML codificado en línea de comandos, no scripts ni rutas de archivos a ejecutar. Revalida identidad, offset/tamaño de partición, límites y protecciones antes de escribir. No usa un servicio privilegiado persistente. Cierra al terminar; la interfaz actualiza el mapa. No hay rollback de operaciones ya completadas si falla una posterior.

Cuando el usuario sin elevar no puede consultar límites exactos o formatos compatibles, puede preparar una propuesta claramente indicada como pendiente de comprobación. El auxiliar la valida antes de la primera modificación y la rechaza si Windows no la admite. Los límites propuestos no implican que una ampliación sea posible a través de otras particiones.

Las pruebas comprueban serialización del destino y ejecutan el proceso auxiliar real sobre un VHD desechable. La prueba automática hereda un token ya elevado del runner; no simula la interacción humana con el cuadro UAC en una sesión estándar. Crear, mover, copiar y borrar siguen pendientes.

## Disco virtual de prueba v0.5.0

El menú **Disco virtual de prueba** permite crear un VHD dinámico nuevo (256–65536 MiB), conectar un VHD existente y desconectarlo conservando el archivo. Cada acción se prepara en la cola y sólo se ejecuta después de confirmar **Aplicar**. El auxiliar elevado se inicia en ese momento; la ventana principal permanece abierta.

La creación prepara GPT, una partición NTFS `WINPART_TEST` que ocupa la mitad de la capacidad virtual y espacio sin asignar para pruebas. El archivo consume espacio real conforme se escriben datos; no modifica las particiones del disco que lo aloja. No se sobrescriben VHD existentes. Guarda el archivo en un disco local con espacio suficiente. Al reconectar se conservan sus particiones y contenido. La desconexión se aplica por separado de cambios de particiones y bloquea discos usados por Windows, paginación o la propia aplicación.

Backend: API nativa `virtdisk.dll` para crear/conectar/desconectar y cmdlets Storage de Windows PowerShell para preparar GPT/NTFS. No requiere Hyper-V ni módulos de pago. Las pruebas de GitHub crean su propio VHD desechable, verifican escritura, desconexión/reconexión y conservación del archivo. El VHD conectado aparece en el mapa al actualizarse después de aplicar.

Esta versión permite probar las operaciones ya disponibles de tamaño y formato; crear/borrar particiones arbitrarias, copiar y mover siguen siendo pasos siguientes del proyecto.

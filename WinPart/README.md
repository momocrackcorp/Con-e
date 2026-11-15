# WinPart v0.2.0 (nombre provisional)

Proyecto Windows independiente extraído de Con-E. Ejecutable WinPart.exe, sin dependencia de Con-E ni herramientas externas de interfaz. Usa .NET Framework 4.8 y Storage WMI de Windows.

## Disponible

Mapa visual de discos, lista de particiones y ficha; editor de tamaño por arrastre o MiB; operaciones pendientes; Aplicar con resumen final y Descartar. Reducción/ampliación de GPT NTFS de datos básicos con letra y permisos de administrador. Revalida identidad de disco, offset, tamaño y límites antes de modificar. Respaldar los datos antes de aplicar. No hay reversión automática de operaciones ya completadas.

## Pendiente para un gestor completo

Crear, eliminar, formatear, mover, copiar particiones y más sistemas de archivos. No es un port de GParted: todavía no incorpora código de GParted/libparted. El objetivo es desarrollar un gestor visual para Windows; cualquier reutilización futura de código GPL requiere revisar dependencias y licencias.

## Compilar

dotnet build WinPart/src/WinPart.csproj -c Release

GitHub genera WinPart-v0.2.0-portable por separado de Con-E. Las pruebas incluyen interfaz, cola sin escritura y reducción/ampliación con conservación de archivo sobre VHD desechable propio. En esta etapa ambos proyectos están en carpetas distintas de un mismo repositorio; WinPart se puede mover a su propio repositorio sin cambios de código.


## Sistemas de archivos v0.2.0

Columnas Partición, Sistema de archivos, Montaje, Etiqueta, Tamaño, Usado, Libre e Indicadores. Windows proporciona tamaños de volúmenes con letra; las particiones sin volumen consultable muestran datos desconocidos.

Sistema de archivos abre un selector de formatos compatibles informados por MSFT_Volume.GetSupportedFileSystems, limitado por ahora a NTFS, exFAT y FAT32. No hay ext4/btrfs. Añadir formateo sólo prepara una operación y muestra la transición pendiente. Aplicar muestra un resumen que advierte que FORMATEAR BORRA TODOS LOS ARCHIVOS de cada volumen indicado. No es una conversión conservando datos. Descartar no modifica el disco.

La edición de formato está limitada a volúmenes con letra sobre GPT de datos básicos identificados inequívocamente. Bloquea particiones de sistema/arranque, la unidad de Windows, la unidad donde reside WinPart y volúmenes con paginación activa. Revalida identidad de disco/partición, offset/tamaño, letra, identidad del volumen y compatibilidad antes de escribir. No fuerza desmontaje: Windows puede rechazar un volumen en uso. No pretende reemplazar todas las capacidades de GParted.

Sólo hay una operación pendiente por partición: preparar un formateo sustituye un redimensionamiento pendiente de esa partición y viceversa. La ejecución de varias operaciones no es transaccional y no tiene rollback.

Prueba en VHD desechable: reducir/ampliar NTFS conservando archivo; formatear exFAT y verificar desaparición del archivo y nuevo formato; formatear de nuevo NTFS y verificar escritura. No se realizan estas pruebas sobre datos del usuario.

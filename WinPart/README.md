# WinPart v0.9.4
Gestor visual de discos y particiones para Windows. Aplicación independiente de [Con‑E](../README.md).

[**Descargar WinPart v0.9.4 portátil**](https://github.com/momocrackcorp/Con-e/releases/tag/winpart-v0.9.4) · [Código fuente](src/) · [Licencia GPL-2.0-or-later](COPYING)

## Funciones
- Mapa gráfico por disco, lista de particiones y ficha de capacidad, formato, montaje, usado y libre cuando Windows los informa.
- Miniaturas compartidas de HDD, SSD, M.2 SATA/NVMe y pendrive USB genérico. Las formas son ilustrativas.
- Distintivos de Windows en uso y tipo Linux cuando existe información suficiente. No se atribuye Windows sólo por NTFS ni se confirma una instalación Linux arrancable mediante su tipo.
- Crear particiones GPT y primarias MBR; formatear volúmenes compatibles como NTFS, exFAT o FAT32, o crear sin formato.
- Reducir/ampliar particiones de datos NTFS compatibles, sujeto a los límites de Windows y al espacio disponible.
- Eliminar particiones; opciones avanzadas para particiones sin letra, protegidas o de tipo desconocido en discos ajenos al Windows actual.
- Vaciar un disco completo y prepararlo como GPT o MBR.
- Crear, conectar y desconectar discos virtuales VHD para pruebas.
- Cola de cambios pendientes, **Descartar**, editor junto al disco y **Cancelar edición**.

## Descargar y utilizar
Windows 10/11 y **.NET Framework 4.8**. WinPart no requiere instalar Con‑E.

1. Descarga el ZIP de la [release de WinPart](https://github.com/momocrackcorp/Con-e/releases/tag/winpart-v0.9.4).
2. Extrae el paquete completo y abre `WinPart.exe`.
3. Selecciona el disco y la partición o el espacio sin asignar.
4. Prepara las operaciones y revisa la cola.
5. Pulsa **Aplicar** y revisa la confirmación. Se solicita administrador sólo al ejecutar los cambios; la ventana principal permanece abierta.

**Eliminar, formatear o vaciar un disco puede destruir sus datos. Mantén una copia de seguridad.** Preparar o descartar no escribe en el disco. Las operaciones ya ejecutadas no tienen reversión automática; una cola puede terminar parcialmente si falla una operación posterior.

## Protecciones y límites
Antes de escribir se revalidan identidad, capacidad, partición, offset, tamaño y condiciones de la operación. Se protegen los discos usados por el Windows actual, paginación, volcado o la propia aplicación. Un volumen en uso, bloqueado o de sólo lectura puede ser rechazado por Windows.

No se incluyen mover/copiar particiones, crear lógicas MBR, explorar archivos, crear medios de arranque ni leer/descifrar contenido cifrado. Identificar un tipo Linux no añade soporte para formatear o redimensionar ext4, btrfs, XFS, LVM o swap. Eliminar una partición cifrada no la descifra ni constituye borrado seguro.

El mapa puede ensanchar las particiones diminutas para seleccionarlas; consulta los tamaños numéricos de la tabla. No se inventa ocupación ni formato cuando Windows no los proporciona.

## Compilar
Desde la raíz del repositorio en Windows, con el SDK .NET 8:
```powershell
./WinPart/build-icon.ps1
dotnet build WinPart/src/WinPart.csproj -c Release -o partition-output
```
El paquete portátil incluye `source/WinPart` y `source/Shared`, las imágenes compartidas, el icono, el script de generación y las atribuciones necesarios para reconstruirlo:
```powershell
dotnet build source/WinPart/src/WinPart.csproj -c Release
```

## Licencia
Software libre **GPL-2.0-or-later**, sin garantía conforme a [COPYING](COPYING). Incluye atribuciones en [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) y la procedencia de la paleta adaptada de GParted en [upstream](upstream/). El motor de operaciones utiliza las interfaces de almacenamiento de Windows; WinPart no es una versión oficial de GParted.

## Validación
La release procede de una compilación Windows aprobada. Las pruebas incluyen interfaz, cola sin escritura, miniaturas y clasificación de particiones. Las operaciones de creación, eliminación, formato y redimensionamiento se comprueban sobre un **VHD exclusivo desechable**, incluida la conservación de un archivo en otra partición. No se prueban sobre discos del usuario ni certifican todos los controladores o sistemas de archivos.

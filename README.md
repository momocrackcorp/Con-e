# Con‑E v0.16.7
Diagnóstico e inventario del equipo para Windows, con una interfaz gráfica en español.

[**Descargar Con‑E v0.16.7 portátil**](https://github.com/momocrackcorp/Con-e/releases/tag/con-e-v0.16.7) · [Historial de publicaciones](https://github.com/momocrackcorp/Con-e/releases) · [WinPart: gestor de particiones independiente](WinPart/README.md)

## Funciones
- Tarjetas de procesador, RAM, video, discos físicos, red, Windows, BIOS y usuario.
- Uso global de CPU en vivo mediante WMI y consultas en segundo plano.
- RAM instalada, ocupación, módulos y posiciones informadas por SMBIOS; un botón despliega los detalles de todas las ranuras.
- Adaptadores gráficos separados, con sus marcas y memoria informada por DXGI o WMI.
- Discos físicos con miniaturas de HDD, SSD, M.2 SATA/NVMe y pendrive USB genérico. Las ilustraciones son referenciales; NVMe por sí solo no confirma el formato físico M.2.
- Tarjetas de disco compactas: miniatura y modelo a la izquierda, barra de particiones con tamaños y porcentajes a la derecha.
- Detalle por volumen: anillo de ocupación, capacidad, usado, disponible y lectura/escritura cuando están disponibles.
- Análisis de carpetas a petición, cancelable y en segundo plano. Agrupa las carpetas de la raíz y sus subcarpetas; no lista archivos individuales.
- Distintivo Windows para la partición del sistema en uso y distintivo Linux cuando el tipo o formato lo identifica. NTFS, exFAT o EFI por sí solos no atribuyen un sistema operativo; el tipo Linux no confirma una instalación arrancable.
- Temperaturas, ventiladores, mínimos/máximos de sesión y SMART cuando el dispositivo, el controlador y los permisos los permiten.
- Tarjeta Usuario con fecha y hora de instalación de Windows y último arranque del sistema.
- Inventario de hardware/controladores, informes y herramientas. La actualización de aplicaciones mediante WinGet solicita administrador al ejecutarse.

## Requisitos y uso
Windows 10/11 de **64 bits** y **.NET Framework 4.8**.

1. Descarga el ZIP de la [release de Con‑E](https://github.com/momocrackcorp/Con-e/releases/tag/con-e-v0.16.7).
2. Extrae **todo el paquete** a una carpeta.
3. Abre `Con-E.exe`. Conserva las DLL y las carpetas acompañantes.
4. Actualiza el inventario con **Actualizar datos**. Para almacenamiento, pulsa **Ver disco y volúmenes** y después **Analizar carpetas**.

La aplicación principal funciona sin elevarse. El módulo de sensores puede solicitar administrador a petición; el controlador PawnIO sólo se instala mediante su botón explícito y confirmación.

## Lecturas y límites
Los datos ausentes se muestran como **No disponible**. USB, RAID, firmware y permisos pueden limitar la telemetría. SMART y vida restante estimada no garantizan ausencia de fallos; corresponden al disco físico, no a cada partición. No se inventan horas separadas de lectura y escritura ni fechas de fabricación.

El análisis de carpetas consulta metadatos y no modifica el contenido. Omite rutas inaccesibles, no sigue enlaces de directorio y evita duplicar enlaces duros. Puede tardar en volúmenes grandes; se indica si la lectura es parcial.

Con‑E no administra ni modifica particiones. Para esas operaciones utiliza [WinPart](WinPart/README.md), distribuido por separado.

## Compilar
Desde Windows, con el SDK .NET 8:
```powershell
dotnet build src/Con-E.csproj -c Release -o output
./build-sensor-dependencies.ps1
```
El proyecto apunta a .NET Framework 4.8. El script prepara dependencias y licencias del lector de sensores. Consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [Licenses](Licenses/) y [recursos compartidos](Shared/README.md).

## Validación
La versión publicada procede de una compilación Windows aprobada: interfaz ancha/estrecha y escala doble, CPU asíncrona, RAM, GPU, almacenamiento, cancelación, sensores IPC y estados sin lectura. Las capturas de pruebas utilizan datos simulados. La compatibilidad con sensores físicos y el diálogo UAC dependen del equipo.

## Acerca de Con-E

Desarrollada por **Omar Águila**, en **Laboratorios Momocrackcorp**, en **noviembre de 2026**, **Pueblo Seco, San Ignacio, Ñuble, Chile**.

El nombre hace un guiño a **Coné**, sobrino de **Condorito**, personajes creados por **René Ríos Boettiger, Pepo**. Los derechos sobre los personajes, sus nombres y las ilustraciones pertenecen a sus respectivos titulares. No se afirma afiliación, patrocinio ni una licencia concedida. El crédito de desarrollo corresponde a la aplicación. El panel se abre mediante **Acerca de Con-E**; esta versión incorpora el homenaje escrito y conserva el icono propio de la aplicación.

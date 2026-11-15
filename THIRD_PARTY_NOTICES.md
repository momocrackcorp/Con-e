# Componentes de terceros — Con-E 0.14.0

* LibreHardwareMonitorLib 0.9.6 — Copyright LibreHardwareMonitor y colaboradores; incluye contribuciones de Michael Möller/Open Hardware Monitor. MPL-2.0. Biblioteca sin modificaciones. Código fuente: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6 . Licencia completa en Licenses/LibreHardwareMonitor-MPL-2.0.txt.
* DiskInfoToolkit (dependencia de LibreHardwareMonitor) — Copyright Florian K. y colaboradores; MPL-2.0. Código: https://github.com/Blacktempel/DiskInfoToolkit . Sus metadatos de versión y licencia se conservan en Licenses/packages.txt y los archivos de licencias del paquete distribuido.
* PawnIO — controlador externo opcional de namazso y colaboradores. Instalador oficial sin modificaciones, procedente del árbol v0.9.6 de LibreHardwareMonitor. Proyecto y fuentes: https://github.com/namazso/PawnIO . El instalador presenta sus condiciones y no se ejecuta automáticamente. Con-E no altera ni distribuye WinRing0.
* Las demás dependencias NuGet conservan sus avisos y licencias en Licenses/. Las versiones concretas y enlaces de proyecto se registran durante la compilación en packages.txt.

Con-E usa interfaces públicas de estas bibliotecas; no atribuye su código a los autores de Con-E. Los fuentes de Con-E y los scripts de compilación están en https://github.com/momocrackcorp/Con-e .

* Módulos PawnIO incorporados sin cambios en LibreHardwareMonitorLib — LGPL-2.1; texto completo en Licenses/PawnIO-modules-LGPL-2.1.txt. Fuentes: https://github.com/namazso/PawnIO.Modules y árbol de recursos del tag v0.9.6 de LibreHardwareMonitor. Se puede sustituir LibreHardwareMonitorLib.dll por una compilación compatible para depurar modificaciones de sus componentes libres.
* El código fuente del controlador PawnIO se publica bajo GPL-2.0-or-later con excepción de enlace; el instalador oficial firmado se distribuye bajo las condiciones de su autor, que permiten redistribuirlo sin modificaciones. No se atribuye al binario oficial la licencia del árbol de fuentes. Información del autor: https://github.com/namazso/PawnIO.Modules/wiki/Using-PawnIO-Modules y https://pawnio.eu/ .

Inventario y miniaturas compartidos con WinPart: código propio bajo MIT; licencia en Licenses/StorageShared-MIT.txt y procedencia del recurso en Licenses/StorageArtwork.txt.

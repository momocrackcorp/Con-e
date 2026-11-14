# WinPart v0.1.0 (nombre provisional)

Proyecto Windows independiente extraído de Con-E. Ejecutable WinPart.exe, sin dependencia de Con-E ni herramientas externas de interfaz. Usa .NET Framework 4.8 y Storage WMI de Windows.

## Disponible

Mapa visual de discos, lista de particiones y ficha; editor de tamaño por arrastre o MiB; operaciones pendientes; Aplicar con resumen final y Descartar. Reducción/ampliación de GPT NTFS de datos básicos con letra y permisos de administrador. Revalida identidad de disco, offset, tamaño y límites antes de modificar. Respaldar los datos antes de aplicar. No hay reversión automática de operaciones ya completadas.

## Pendiente para un gestor completo

Crear, eliminar, formatear, mover, copiar particiones y más sistemas de archivos. No es un port de GParted: todavía no incorpora código de GParted/libparted. El objetivo es desarrollar un gestor visual para Windows; cualquier reutilización futura de código GPL requiere revisar dependencias y licencias.

## Compilar

dotnet build WinPart/src/WinPart.csproj -c Release

GitHub genera WinPart-v0.1.0-portable por separado de Con-E. Las pruebas incluyen interfaz, cola sin escritura y reducción/ampliación con conservación de archivo sobre VHD desechable propio. En esta etapa ambos proyectos están en carpetas distintas de un mismo repositorio; WinPart se puede mover a su propio repositorio sin cambios de código.

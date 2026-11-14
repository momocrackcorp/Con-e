# Con-E v0.1.0 — primera prueba

Launcher portátil de diagnóstico para Windows 10 y 11. Interfaz nativa Windows Forms, sin navegador integrado ni paquetes externos en ejecución. Usa .NET Framework 4.8: debe estar disponible en el equipo; esta entrega no incluye ni instala el framework. Windows XP es referencia visual, no plataforma compatible.

## Incluido

- Resumen de equipo, CPU, RAM y discos físicos.
- Versión de Windows y fecha de instalación registrada.
- Fabricante, versión, fecha y serie informada por BIOS; identidad del equipo y placa base por separado.
- Inventario de controladores y códigos de error PnP. Los dispositivos con incidencias aparecen además como filas de advertencia, aunque no tengan controlador firmado. Puede haber una fila de advertencia y otra de inventario para el mismo dispositivo.
- Informe TXT guardado únicamente cuando el usuario lo solicita.
- Carpeta Tools para herramientas portátiles externas; se abre en el Explorador.
- Botón de búsqueda web con fabricante y modelo. Esta versión abre el navegador: NO obtiene todavía una reseña automática ni verifica coincidencias con fuentes oficiales.

El último formateo no puede establecerse de forma fiable a partir de la fecha de instalación. Los valores SMBIOS dependen de lo registrado por el fabricante y pueden contener marcadores o datos incorrectos. Un código PnP cero no certifica que un dispositivo funcione correctamente. Las consultas se ejecutan fuera del hilo de interfaz; cada consulta tiene un tiempo límite WMI de 20 segundos, sujeto al comportamiento del proveedor.

## Compilación con GitHub

El workflow de GitHub Actions compila al recibir cambios o al ejecutarse manualmente desde Actions. Descargar el artefacto `Con-E-v0.1.0-portable`, extraer todo su contenido en el USB y abrir Con-E.exe. No ejecutar desde dentro del ZIP.

## Pruebas pendientes en Windows

1. Compilación y generación del artefacto: comprobadas en GitHub Actions.
2. Abrir desde USB sin permisos de administrador y comparar modelo, serie, RAM y BIOS con Información del sistema (msinfo32).
3. Comparar controladores y códigos PnP con Administrador de dispositivos.
4. Desconectar internet: la lectura local y el informe deben seguir disponibles.
5. Probar escalas de pantalla de 100%, 150% y 200%, redimensionar y revisar legibilidad.
6. Exportar TXT al USB y verificar datos y acentos; probar también un USB sin permiso de escritura.
7. Comprobar comportamiento con WMI bloqueado o información parcial.
8. Medir tamaño del paquete, RAM y tiempo de inicio; aún no existen mediciones.

## Estado de entrega

Compilación Windows correcta en GitHub Actions: [primera compilación](https://github.com/momocrackcorp/Con-e/actions/runs/43550847741). Paquete portátil generado. Ejecución interactiva y prueba desde USB pendientes. No se incluyen herramientas de terceros. La reseña oficial automática del fabricante y un catálogo de accesos del launcher quedan para una siguiente versión.

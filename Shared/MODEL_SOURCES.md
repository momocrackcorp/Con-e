# Fuentes de detección de medio

El tipo de conexión (RAID, SATA, USB, NVMe) es independiente de que el medio sea HDD o SSD.

Orden: MediaType explícito de Windows; RPM válidas del mismo disco; coincidencia exacta de modelo documentado; consulta de sólo lectura StorageDeviceSeekPenaltyProperty. No se convierte automáticamente en SSD un disco que no informa penalización de búsqueda. TRIM no se usa como prueba de SSD. Los VHD conservan su clasificación virtual.

El catálogo de respaldo contiene sólo los números de modelo WD Caviar Green enumerados en estas fichas **publicadas por Western Digital**, conservadas en distribuidores. No clasifica todos los productos WDC como HDD:

* https://www.farnell.com/datasheets/1522249.pdf — WD Caviar Green, modelos WD30EZRX, WD25EZRX, WD20EARX, WD20EARS, WD15EARX, WD10EZRX, WD10EARX, WD10EARS, WD7500AZRX, WD7500AARX, WD5000AZRX, WD5000AZDX, WD5000AADS, WD3200AZRX, WD3200AZDX; 3,5 pulgadas, IntelliPower.
* https://cdn.inet.se/pdf/4303581_0.pdf — WD Caviar Green, modelos WD20EADS, WD15EADS, WD10EADS, WD10EAVS, WD7500AACS, WD7500AAVS, WD6400AACS, WD6400AAVS, WD5000AACS, WD5000AAVS; 3,5 pulgadas, IntelliPower.

Se compara el modelo base con límites de palabra; un sufijo de firmware como -00N0YB0 no altera la coincidencia. No se usa una búsqueda genérica por marca ni por capacidad.

Documentación técnica:
* https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-device_seek_penalty_descriptor
* https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ne-winioctl-storage_property_id
* https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisk

La consulta no instala controladores, no pide elevación por sí sola ni modifica discos. Si el controlador rechaza la consulta y no hay otra evidencia, permanece «Medio no informado».

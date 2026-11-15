using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

internal sealed class StorageDeviceInfo {public string Model="No disponible",Type="No disponible",Health="No disponible",Serial="";public int? Temperature;public uint? DiskNumber;public ulong? PowerOnHours;public string ManufactureDate="No disponible";}
internal sealed class StorageIo {public double? Read,Write;}
internal static class StorageTelemetry
{
    static string Text(ManagementBaseObject obj,string key){return Convert.ToString(obj[key],CultureInfo.InvariantCulture).Trim();}
    internal static string Letter(string name)
    {if(name==null||name.Length!=2||!char.IsLetter(name[0])||name[1]!=':')throw new ArgumentException("Selecciona una unidad local con letra.");return name.ToUpperInvariant();}
    static ManagementObjectSearcher Query(string scope,string query){return new ManagementObjectSearcher(new ManagementScope(scope),new ObjectQuery(query),new EnumerationOptions {Timeout=TimeSpan.FromSeconds(3),ReturnImmediately=false});}
    internal static StorageIo ReadIo(string name)
    {
        var result=new StorageIo();string letter=Letter(name);
        try{using(var q=Query(@"\\.\root\cimv2","SELECT DiskReadBytesPersec,DiskWriteBytesPersec FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name='"+letter+"'"))using(var rows=q.Get())foreach(ManagementObject row in rows)using(row){double read,write;if(double.TryParse(Text(row,"DiskReadBytesPersec"),NumberStyles.Float,CultureInfo.InvariantCulture,out read)&&read>=0)result.Read=read;if(double.TryParse(Text(row,"DiskWriteBytesPersec"),NumberStyles.Float,CultureInfo.InvariantCulture,out write)&&write>=0)result.Write=write;}}catch{}
        return result;
    }
    internal static StorageDeviceInfo Device(string name)
    {
        var info=new StorageDeviceInfo();string letter=Letter(name);var numbers=new HashSet<uint>();
        try
        {
            using(var q=Query(@"\\.\root\cimv2","SELECT * FROM Win32_LogicalDisk WHERE DeviceID='"+letter+"'"))using(var rows=q.Get())foreach(ManagementObject row in rows)using(row)
            using(var parts=row.GetRelated("Win32_DiskPartition"))foreach(ManagementObject p in parts)using(p)numbers.Add(Convert.ToUInt32(p["DiskIndex"]));
            if(numbers.Count!=1){if(numbers.Count>1)info.Model="Volumen sobre varios discos";return info;}
            uint number=numbers.Single();info.DiskNumber=number;string id="";
            using(var q=Query(@"\\.\root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Disk WHERE Number="+number))using(var rows=q.Get())foreach(ManagementObject disk in rows)using(disk)
            {
                info.Model=Text(disk,"FriendlyName");info.Serial=Text(disk,"SerialNumber");id=Text(disk,"UniqueId");int bus=Convert.ToInt32(disk["BusType"]);
                info.Type=bus==17?"NVMe":bus==11?"SATA":bus==7?"USB":bus==12?"Tarjeta SD":bus==13?"MMC / eMMC":bus==15?"Disco virtual VHD":"Tipo no informado";
                try{using(var counters=disk.GetRelated("MSFT_StorageReliabilityCounter"))foreach(ManagementObject counter in counters)using(counter){int temp;if(int.TryParse(Text(counter,"Temperature"),out temp)&&temp>0&&temp<150)info.Temperature=temp;ulong hours;if(ulong.TryParse(Text(counter,"PowerOnHours"),out hours))info.PowerOnHours=hours;string manufactured=Text(counter,"ManufactureDate");if(!string.IsNullOrWhiteSpace(manufactured))info.ManufactureDate=manufactured+" (informado por dispositivo)";}}catch{}
            }
            // Match actual persistent identifiers, never assume PhysicalDisk.DeviceId equals Disk.Number.
            if(id.Length>0)using(var q=Query(@"\\.\root\Microsoft\Windows\Storage","SELECT UniqueId,MediaType,HealthStatus FROM MSFT_PhysicalDisk"))using(var rows=q.Get())foreach(ManagementObject physical in rows)using(physical)
                if(Text(physical,"UniqueId").Equals(id,StringComparison.OrdinalIgnoreCase))
                {int media=Convert.ToInt32(physical["MediaType"]);if(media==3||media==4)info.Type=(media==3?"HDD":"SSD")+" · "+info.Type;int health;if(int.TryParse(Text(physical,"HealthStatus"),out health))info.Health=health==0?"Correcto según Windows":health==1?"Advertencia según Windows":health==2?"Estado no saludable":"No disponible";}
        }
        catch{}
        return info;
    }
}

// SPDX-License-Identifier: MIT
// Repository-owned read-only storage inventory, shared by Con-E and WinPart.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

internal sealed class PhysicalDiskInfo
{
    public uint Number,MediaType;public ushort BusType;
    public string Model="No disponible",Serial="",UniqueId="",Style="No disponible",MediumSource="",KnownForm="";
    public uint? SpindleSpeed;
    public ulong Size;public bool Complete;readonly string inventoryInstance=Guid.NewGuid().ToString("N");
    public readonly List<PhysicalPartitionInfo> Partitions=new List<PhysicalPartitionInfo>();
    public bool IsVirtual {get{return BusType==14||BusType==15;}}
    public override string ToString(){return "Disco "+Number+" · "+Model;}
    public string Key {get{return Number+"|"+(UniqueId.Length>0?UniqueId:Serial.Length>0?Serial:inventoryInstance);}}
    public bool ModelMentionsM2 {get{return Regex.IsMatch(Model,@"\bM[.]?2\b",RegexOptions.IgnoreCase);}}
    public string Form {get{return KnownForm.Length>0?KnownForm:ModelMentionsM2?"M.2 indicado en modelo":"Formato físico no confirmado";}}
    public string Kind {get{return IsVirtual?"VHD / disco virtual":BusType==17?"SSD NVMe":MediaType==3?"HDD":MediaType==4?"SSD":"Medio no informado";}}
    public string Bus {get{return BusType==17?"NVMe":BusType==11?"SATA":BusType==3?"ATA":BusType==7?"USB":BusType==12?"SD":BusType==13?"MMC / eMMC":BusType==8?"RAID":BusType==14||BusType==15?"Virtual":"Conexión no informada";}}
    public string Art {get{return IsVirtual?"virtual":BusType==12||BusType==13?"sd":BusType==7&&MediaType!=3&&MediaType!=4?"usb":BusType==17?"m2-nvme":MediaType==3?"hdd":ModelMentionsM2&&(BusType==11||BusType==3)?"m2-sata":ModelMentionsM2?"generic":MediaType==4?"ssd":"generic";}}
}
internal sealed class PhysicalPartitionInfo
{
    public uint Number;public ulong Offset,Size;
    public string Letter="",Label="",FileSystem="No disponible",Role="Partición",VolumeId="";
    public ulong? VolumeSize,Free;
    public string Title {get{return Letter.Length>0?Letter+": "+Label:"Partición "+Number+" · "+Role;}}
}
internal static class PhysicalStorage
{
    static string S(ManagementBaseObject o,string name){try{return Convert.ToString(o[name],CultureInfo.InvariantCulture).Trim();}catch{return "";}}
    static ulong U(ManagementBaseObject o,string name){ulong n;return ulong.TryParse(S(o,name),out n)?n:0;}
    static ManagementObjectSearcher Q(string scope,string sql){return new ManagementObjectSearcher(new ManagementScope(scope),new ObjectQuery(sql),new EnumerationOptions {ReturnImmediately=false,Timeout=TimeSpan.FromSeconds(4)});}
    const string Storage=@"\\.\root\Microsoft\Windows\Storage",Cim=@"\\.\root\cimv2";
    internal static string PartitionRole(string guid,string mbr)
    {
        switch((guid??"").Trim('{','}').ToUpperInvariant()){
            case "C12A7328-F81F-11D2-BA4B-00A0C93EC93B":return "Sistema EFI";
            case "E3C9E316-0B5C-4DB8-817D-F92DF00215AE":return "Reservada MSR";
            case "DE94BBA4-06D1-4D40-A16A-BFD50179D6AC":return "Recuperación";
            case "EBD0A0A2-B9E5-4433-87C0-68B6B72699C7":return "Datos básicos";
            default:return "Partición";
        }
    }
    internal static List<PhysicalDiskInfo> Read(List<string> warnings,bool partitions=true)
    {
        var disks=new List<PhysicalDiskInfo>();
        try{using(var q=Q(Storage,"SELECT Number,FriendlyName,SerialNumber,UniqueId,Size,BusType,PartitionStyle FROM MSFT_Disk"))using(var rows=q.Get())foreach(ManagementObject r in rows)using(r)disks.Add(new PhysicalDiskInfo {Number=(uint)U(r,"Number"),Model=S(r,"FriendlyName"),Serial=S(r,"SerialNumber"),UniqueId=S(r,"UniqueId"),Size=U(r,"Size"),BusType=(ushort)U(r,"BusType"),Style=U(r,"PartitionStyle")==2?"GPT":U(r,"PartitionStyle")==1?"MBR":"RAW"});}
        catch(Exception ex){warnings?.Add("Inventario de discos: "+ex.Message);}
        try{using(var q=Q(Cim,"SELECT Index,Model,SerialNumber,Size,InterfaceType FROM Win32_DiskDrive"))using(var rows=q.Get())foreach(ManagementObject r in rows)using(r){uint n=(uint)U(r,"Index");var disk=disks.FirstOrDefault(d=>d.Number==n);if(disk==null){disk=new PhysicalDiskInfo {Number=n,Model=S(r,"Model"),Serial=S(r,"SerialNumber"),Size=U(r,"Size")};disks.Add(disk);}if(disk.Serial.Length==0)disk.Serial=S(r,"SerialNumber");}}
        catch(Exception ex){warnings?.Add("Inventario Win32: "+ex.Message);}
        try{using(var q=Q(Storage,"SELECT * FROM MSFT_PhysicalDisk"))using(var rows=q.Get())foreach(ManagementObject r in rows)using(r){string id=S(r,"UniqueId"),serial=S(r,"SerialNumber");var matches=disks.Where(d=>id.Length>0&&string.Equals(d.UniqueId,id,StringComparison.OrdinalIgnoreCase)||serial.Length>0&&string.Equals(d.Serial,serial,StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length==1){matches[0].MediaType=(uint)U(r,"MediaType");uint rpm;if(uint.TryParse(S(r,"SpindleSpeed"),out rpm))matches[0].SpindleSpeed=rpm;}}}
        catch(Exception ex){warnings?.Add("Tipo HDD/SSD: "+ex.Message);}
        foreach(var d in disks){DiskMediumDetection.Resolve(d);if(!d.IsVirtual&&d.MediaType==0)DiskMediumDetection.Resolve(d,DiskMediumDetection.SeekPenalty(d.Number));}
        if(!partitions)return disks.OrderBy(d=>d.Number).ToList();
        var logical=new Dictionary<string,Tuple<string,string,ulong?,ulong?>>(StringComparer.OrdinalIgnoreCase);
        try{using(var q=Q(Cim,"SELECT DeviceID,FileSystem,VolumeName,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=2 OR DriveType=3"))using(var rows=q.Get())foreach(ManagementObject r in rows)using(r){ulong value;ulong? size=ulong.TryParse(S(r,"Size"),out value)?(ulong?)value:null,free=ulong.TryParse(S(r,"FreeSpace"),out value)?(ulong?)value:null;logical[S(r,"DeviceID").TrimEnd(':')]=Tuple.Create(S(r,"FileSystem"),S(r,"VolumeName"),size,free);}}
        catch(Exception ex){warnings?.Add("Volúmenes: "+ex.Message);}
        try
        {
            using(var q=Q(Storage,"SELECT * FROM MSFT_Partition"))using(var rows=q.Get())foreach(ManagementObject r in rows)using(r)
            {
                var disk=disks.FirstOrDefault(d=>d.Number==U(r,"DiskNumber"));if(disk==null)continue;
                var p=new PhysicalPartitionInfo {Number=(uint)U(r,"PartitionNumber"),Offset=U(r,"Offset"),Size=U(r,"Size"),Letter=S(r,"DriveLetter").Trim('\0',' ',':'),Role=PartitionRole(S(r,"GptType"),S(r,"MbrType"))};Tuple<string,string,ulong?,ulong?> v;
                if(p.Letter.Length>0&&logical.TryGetValue(p.Letter,out v)){p.FileSystem=v.Item1.Length>0?v.Item1:"No disponible";p.Label=v.Item2;p.VolumeSize=v.Item3;p.Free=v.Item4;}
                else try{using(var related=Q(Storage,"ASSOCIATORS OF {"+r.Path.Path+"} WHERE ResultClass=MSFT_Volume"))using(var volumes=related.Get())foreach(ManagementObject vol in volumes)using(vol){p.VolumeId=S(vol,"Path");p.FileSystem=S(vol,"FileSystem");p.Label=S(vol,"FileSystemLabel");ulong size;if(ulong.TryParse(S(vol,"Size"),out size))p.VolumeSize=size;if(ulong.TryParse(S(vol,"SizeRemaining"),out size))p.Free=size;}}catch{}
                disk.Partitions.Add(p);
            }
            foreach(var d in disks)d.Complete=true;
        }
        catch(Exception ex){warnings?.Add("Particiones: "+ex.Message);}
        // Read-only fallback. Never infer unallocated space from an incomplete Win32 inventory.
        foreach(var d in disks.Where(d=>d.Partitions.Count==0&&!d.Complete))try
        {
            using(var q=Q(Cim,"SELECT * FROM Win32_DiskPartition WHERE DiskIndex="+d.Number))using(var rows=q.Get())foreach(ManagementObject r in rows)using(r)
            {
                var p=new PhysicalPartitionInfo {Number=(uint)U(r,"Index")+1,Offset=U(r,"StartingOffset"),Size=U(r,"Size"),Role=S(r,"Type")};
                try{using(var volumes=r.GetRelated("Win32_LogicalDisk"))foreach(ManagementObject vol in volumes)using(vol){p.Letter=S(vol,"DeviceID").TrimEnd(':');p.FileSystem=S(vol,"FileSystem");p.Label=S(vol,"VolumeName");ulong size;if(ulong.TryParse(S(vol,"Size"),out size))p.VolumeSize=size;if(ulong.TryParse(S(vol,"FreeSpace"),out size))p.Free=size;}}catch{}
                d.Partitions.Add(p);
            }
        }catch{}
        return disks.OrderBy(d=>d.Number).ToList();
    }
}

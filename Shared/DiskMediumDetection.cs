// SPDX-License-Identifier: MIT
// Read-only fallbacks: never infer SSD merely from RAID, TRIM or lack of seek penalty.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

internal static class DiskMediumDetection
{
    // Exact base models from manufacturer-authored datasheets; see MODEL_SOURCES.md.
    static readonly HashSet<string> WdGreen=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
        "WD30EZRX","WD25EZRX","WD20EARX","WD20EARS","WD15EARX","WD10EZRX","WD10EARX","WD10EARS","WD7500AZRX","WD7500AARX","WD5000AZRX","WD5000AZDX","WD5000AADS","WD3200AZRX","WD3200AZDX",
        "WD20EADS","WD15EADS","WD10EADS","WD10EAVS","WD7500AACS","WD7500AAVS","WD6400AACS","WD6400AAVS","WD5000AACS","WD5000AAVS"};
    internal static void Resolve(PhysicalDiskInfo disk,bool? seekPenalty=null)
    {
        if(disk.IsVirtual)return;
        if(disk.MediaType==3||disk.MediaType==4){if(disk.MediumSource.Length==0)disk.MediumSource="MediaType informado por Windows";return;}
        if(disk.MediaType!=0)return;
        if(disk.SpindleSpeed.HasValue&&(disk.SpindleSpeed.Value>=300&&disk.SpindleSpeed.Value<=30000||disk.SpindleSpeed.Value==uint.MaxValue)){disk.MediaType=3;disk.MediumSource=disk.SpindleSpeed==uint.MaxValue?"Medio giratorio informado · RPM desconocidas":"Velocidad de giro informada: "+disk.SpindleSpeed+" RPM";return;}
        var match=Regex.Match((disk.Model??"").Replace('_',' '),@"\b(WD[0-9]+[A-Z]+)(?=$|[\s-])",RegexOptions.IgnoreCase);
        if(match.Success&&WdGreen.Contains(match.Groups[1].Value)){disk.MediaType=3;disk.KnownForm="3,5 pulgadas · modelo documentado";disk.MediumSource="Modelo documentado por Western Digital: "+match.Groups[1].Value.ToUpperInvariant();return;}
        if(seekPenalty==true){disk.MediaType=3;disk.MediumSource="Windows informa penalización de búsqueda";}
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool DeviceIoControl(SafeFileHandle device,uint code,byte[] input,uint inputSize,[Out] byte[] output,uint outputSize,out uint returned,IntPtr overlapped);
    internal static bool? SeekPenalty(uint number)
    {
        try{using(var file=CreateFile(@"\\.\PhysicalDrive"+number,0,3,IntPtr.Zero,3,0,IntPtr.Zero)){if(file.IsInvalid)return null;var query=new byte[12];BitConverter.GetBytes(7).CopyTo(query,0);var output=new byte[64];uint bytes;if(!DeviceIoControl(file,0x002D1400,query,(uint)query.Length,output,(uint)output.Length,out bytes,IntPtr.Zero))return null;return DecodeSeekPenalty(output,bytes);}}catch{return null;}
    }
    internal static bool? DecodeSeekPenalty(byte[] descriptor,uint returned)
    {
        if(descriptor==null||descriptor.Length<9||returned<9||returned>descriptor.Length)return null;
        uint version=BitConverter.ToUInt32(descriptor,0),size=BitConverter.ToUInt32(descriptor,4);
        if(version<9||size<9||size>returned||version>size||descriptor[8]>1)return null;
        return descriptor[8]==1;
    }
    internal static void VerifyFallbacks()
    {
        foreach(string model in new[]{"WDC WD10EARX-00N0YB0","WDC WD10EAVS-00D7B1"}){var d=new PhysicalDiskInfo {Model=model,BusType=8};Resolve(d);if(d.MediaType!=3||d.Art!="hdd"||!d.KnownForm.StartsWith("3,5"))throw new Exception("No reconoce HDD WD detrás de RAID: "+model);}
        var unknown=new PhysicalDiskInfo {Model="Unidad desconocida",BusType=8};Resolve(unknown,false);if(unknown.MediaType!=0)throw new Exception("Infiere SSD/HDD sin evidencia");Resolve(unknown,true);if(unknown.MediaType!=3)throw new Exception("Ignora penalización de búsqueda");
        var ssd=new PhysicalDiskInfo {Model="SSD",MediaType=4,BusType=8};Resolve(ssd,true);if(ssd.MediaType!=4)throw new Exception("Sobrescribe MediaType SSD");
        var virtualDisk=new PhysicalDiskInfo {Model="WDC WD10EARX",BusType=15};Resolve(virtualDisk,true);if(virtualDisk.MediaType!=0||virtualDisk.Art!="virtual")throw new Exception("Confunde disco virtual con HDD");
        var rpm=new PhysicalDiskInfo {SpindleSpeed=7200};Resolve(rpm);if(rpm.MediaType!=3)throw new Exception("Ignora RPM de HDD");
        var data=new byte[12];BitConverter.GetBytes(12u).CopyTo(data,0);BitConverter.GetBytes(12u).CopyTo(data,4);data[8]=1;if(DecodeSeekPenalty(data,12)!=true||DecodeSeekPenalty(data,8)!=null)throw new Exception("Descriptor inválido de búsqueda");data[8]=0;if(DecodeSeekPenalty(data,12)!=false)throw new Exception("Descriptor negativo incorrecto");data[8]=2;if(DecodeSeekPenalty(data,12)!=null)throw new Exception("Acepta descriptor mal formado");
    }
}

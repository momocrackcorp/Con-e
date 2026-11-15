// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    readonly Button deletePartition=new Button {Text="Eliminar",AutoSize=true,Enabled=false};
    static bool CanDelete(DiskLayout disk,PartitionLayout part){return CanFormat(disk,part);}
    void BuildDeleteCommand()
    {
        deletePartition.Click+=(s,e)=>
        {
            if(selectedEditDisk!=null&&selectedEditPartition!=null&&CanDelete(selectedEditDisk,selectedEditPartition))StageDelete(selectedEditDisk,selectedEditPartition);
        };
    }
    void StageDelete(DiskLayout disk,PartitionLayout part)
    {
        if(applyingChanges||!CanDelete(disk,part))return;
        pendingResizes.RemoveAll(c=>!c.IsVirtual&&!c.Create&&c.Disk.UniqueId==disk.UniqueId&&c.Part.Number==part.Number);
        pendingResizes.Add(new PendingResize {Delete=true,Disk=disk,Part=part,VolumeId=""});
        editingDisk=null;editingPartition=null;formatEditor.Visible=false;
        foreach(Control control in inlineEditor.Controls)control.Visible=ReferenceEquals(control,pendingList);
        inlineEditor.Visible=true;partitionInfo.Visible=true;UpdatePending();RenderDiskMaps();
        partitionInfo.Text="ELIMINAR PENDIENTE · Disco "+disk.Number+" · partición "+part.Number+" ("+part.Letter+":) · "+Space(part.Size)+Environment.NewLine+
            "Al aplicar y confirmar se eliminará la partición y se perderá el acceso a TODOS sus archivos. Descartar cancela la operación. El disco todavía no se modifica.";
    }
    static void ValidateDelete(PendingResize c)
    {
        if(!c.Delete||c.Create||c.IsVirtual||c.Bytes!=0||c.FileSystem!=null||!CanDelete(c.Disk,c.Part))throw new InvalidOperationException("La partición seleccionada no admite eliminación. Sólo datos básicos GPT con letra; Windows, EFI, recuperación y MSR están protegidos.");
        using(var volume=FormatVolume(c.Disk,c.Part))
        {
            volume.Get();string id=Value(volume,"UniqueId");
            if(id=="No disponible")throw new InvalidOperationException("No se pudo identificar el volumen a eliminar.");
            if(string.IsNullOrEmpty(c.VolumeId))c.VolumeId=id;
            else if(c.VolumeId!=id)throw new InvalidOperationException("La identidad del volumen cambió. Actualiza el mapa.");
        }
    }
    static void ApplyDelete(PendingResize c)
    {
        ValidateDelete(c);
        using(var partition=CurrentPartition(c.Disk,c.Part))
        using(var result=partition.InvokeMethod("DeleteObject",(ManagementBaseObject)null,null))
        {
            uint code=Convert.ToUInt32(result["ReturnValue"]);
            if(code!=0)throw new InvalidOperationException(code==42000?"La partición se eliminó, pero Windows no retiró todas sus rutas de acceso. Revisa el mapa.":"Windows rechazó la eliminación (código "+code+"). No se fuerza un volumen en uso.");
        }
    }
    static bool IsPathOnPartition(ManagementObject partition,string path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        var paths=partition["AccessPaths"] as string[]??new string[0];
        string letter=Convert.ToString(partition["DriveLetter"]).Trim('\0',' ');
        if(letter.Length==1)paths=paths.Concat(new[]{letter+@":\"}).ToArray();
        return paths.Where(p=>!string.IsNullOrEmpty(p)).Any(p=>path.StartsWith(p.EndsWith(@"\")?p:p+@"\",StringComparison.OrdinalIgnoreCase));
    }
    static void DeletePartitionIntegrationTest(string imagePath,string originalFile)
    {
        string content=File.ReadAllText(originalFile);
        uint number=uint.Parse(RunStorageScript(VirtualDiskLookup(imagePath)+"[Console]::Write($disk.Number);").Trim(),System.Globalization.CultureInfo.InvariantCulture);
        var disk=ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number);
        if(disk.BusType!=15)throw new Exception("No identifica el VHD respaldado por archivo");
        var target=disk.Partitions.Single(p=>p.FileSystem.Equals("exFAT",StringComparison.OrdinalIgnoreCase)&&p.Size==64UL*1048576);
        var sentinel=target.Letter+@":\winpart-new-partition.txt";if(!File.Exists(sentinel))throw new Exception("Falta archivo de prueba para eliminar");
        var plan=DecodeOperations(EncodeOperations(new[]{new PendingResize {Delete=true,Disk=disk,Part=target,VolumeId=""}}));
        if(!File.Exists(sentinel))throw new Exception("Preparar eliminación escribe antes de aplicar");
        ExecuteOperations(plan);
        var after=ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number&&d.UniqueId==disk.UniqueId);
        if(after.Partitions.Count!=disk.Partitions.Count-1||after.Partitions.Any(p=>p.Offset==target.Offset)||!after.Segments().Any(g=>g.Gap&&g.Offset<=target.Offset&&g.Size>=target.Size))throw new Exception("La eliminación no libera el intervalo de la partición");
        if(File.ReadAllText(originalFile)!=content)throw new Exception("La eliminación alteró el archivo de otra partición");
        bool staleRejected=false;try{ValidateDelete(plan[0]);}catch{staleRejected=true;}if(!staleRejected)throw new Exception("Acepta eliminar un destino que ya no existe");
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","delete-ok.txt"),"OK: plan sin escritura; eliminar partición exFAT de VHD; intervalo vuelve a sin asignar; contenido de otra partición conservado; rechazo de destino obsoleto; VHD identificado por BusType 15.");
    }
}

// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    readonly Button advancedButton=new Button {Text="Avanzadas ▾",Enabled=false};
    static bool CanAdvancedDisk(DiskLayout d)
    {return d!=null&&d.Complete&&!d.IsBoot&&!d.IsSystem&&!d.IsReadOnly&&!string.IsNullOrWhiteSpace(d.UniqueId)&&d.UniqueId!="No disponible";}
    static bool CanAdvancedDelete(DiskLayout d,PartitionLayout p)
    {return CanAdvancedDisk(d)&&p!=null&&!p.Gap&&p.Number>0&&(d.Style=="GPT"||d.Style=="MBR");}
    void BuildAdvancedCommand()
    {
        var menu=new ContextMenuStrip();
        var remove=menu.Items.Add("Eliminar partición seleccionada (avanzado)",null,(s,e)=>{if(CanAdvancedDelete(selectedEditDisk,selectedEditPartition))StageAdvanced(new PendingResize {Advanced=true,Delete=true,Disk=selectedEditDisk,Part=selectedEditPartition});});
        menu.Items.Add(new ToolStripSeparator());
        var gpt=menu.Items.Add("Vaciar disco completo y preparar como GPT…",null,(s,e)=>StageReset("GPT"));
        var mbr=menu.Items.Add("Vaciar disco completo y preparar como MBR…",null,(s,e)=>StageReset("MBR"));
        advancedButton.Click+=(s,e)=>{remove.Enabled=CanAdvancedDelete(selectedEditDisk,selectedEditPartition);gpt.Enabled=mbr.Enabled=CanAdvancedDisk(selectedEditDisk);menu.Show(advancedButton,new Point(0,advancedButton.Height));};advancedButton.Disposed+=(s,e)=>menu.Dispose();
    }
    void SelectDisk(DiskLayout disk)
    {
        selectedEditDisk=disk;selectedEditPartition=null;UpdateDiskCardBorders();createPartition.Enabled=false;resizePartition.Enabled=false;formatButton.Enabled=false;deletePartition.Enabled=false;advancedButton.Enabled=CanAdvancedDisk(disk);
        partitionInfo.Text="Disco "+disk.Number+" · "+disk.Name+" · "+Space(disk.Size)+" · "+disk.Style+Environment.NewLine+"Selecciona un intervalo para crear o editar una partición, o usa Avanzadas para preparar el disco completo. Vaciar disco elimina TODAS sus particiones.";
    }
    void StageReset(string style)
    {
        if(!CanAdvancedDisk(selectedEditDisk))return;
        StageAdvanced(new PendingResize {Advanced=true,Reset=true,ResetStyle=style,Disk=selectedEditDisk,Part=new PartitionLayout()});
    }
    void StageAdvanced(PendingResize c)
    {
        if(applyingChanges)return;
        if(c.Reset)pendingResizes.RemoveAll(p=>!p.IsVirtual&&p.Disk.UniqueId==c.Disk.UniqueId);
        else pendingResizes.RemoveAll(p=>!p.IsVirtual&&p.Disk.UniqueId==c.Disk.UniqueId&&(p.Reset||(!p.Create&&p.Part.Number==c.Part.Number)));
        pendingResizes.Add(c);editingDisk=null;editingPartition=null;formatEditor.Visible=false;
        foreach(Control control in inlineEditor.Controls)control.Visible=ReferenceEquals(control,pendingList);
        inlineEditor.Visible=true;partitionInfo.Visible=true;UpdatePending();RenderDiskMaps();
        partitionInfo.Text=c.Description+Environment.NewLine+"OPERACIÓN AVANZADA PENDIENTE. Sólo se ejecutará después de confirmar Aplicar. Descartar cancela. No se leen ni descifran los archivos.";
    }
    static ManagementObject FreshDisk(DiskLayout d)
    {
        using(var q=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Disk WHERE Number="+d.Number))
        using(var rows=q.Get())foreach(ManagementObject row in rows)using(row)
        {
            string style=Convert.ToUInt16(row["PartitionStyle"])==2?"GPT":Convert.ToUInt16(row["PartitionStyle"])==1?"MBR":"RAW / no determinado";
            if(Value(row,"UniqueId")!=d.UniqueId||Convert.ToUInt64(row["Size"])!=d.Size||style!=d.Style)throw new InvalidOperationException("El disco cambió. Actualiza el mapa y prepara la operación de nuevo.");
            return new ManagementObject(row.Path);
        }
        throw new InvalidOperationException("El disco ya no está disponible.");
    }
    static void ProtectRunningSystem(DiskLayout d)
    {
        using(var disk=FreshDisk(d))
        {disk.Get();if(Convert.ToBoolean(disk["IsBoot"])||Convert.ToBoolean(disk["IsSystem"]))throw new InvalidOperationException("El Windows actual utiliza este disco para sistema o arranque. La operación avanzada requiere otro entorno de arranque.");if(Convert.ToBoolean(disk["IsReadOnly"])||Convert.ToBoolean(disk["IsOffline"]))throw new InvalidOperationException("El disco está desconectado o protegido contra escritura.");}
        var activePaths=new List<string> {Environment.SystemDirectory,Application.ExecutablePath};
        using(var q=new ManagementObjectSearcher("SELECT Name FROM Win32_PageFileUsage"))using(var rows=q.Get())foreach(ManagementObject row in rows)using(row)activePaths.Add(Value(row,"Name"));
        using(var q=new ManagementObjectSearcher("SELECT DebugInfoType,DebugFilePath,MiniDumpDirectory FROM Win32_OSRecoveryConfiguration"))using(var rows=q.Get())foreach(ManagementObject row in rows)using(row)
            if(Convert.ToUInt32(row["DebugInfoType"])!=0){activePaths.Add(Environment.ExpandEnvironmentVariables(Value(row,"DebugFilePath")));activePaths.Add(Environment.ExpandEnvironmentVariables(Value(row,"MiniDumpDirectory")));}
        using(var q=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Partition WHERE DiskNumber="+d.Number))using(var rows=q.Get())foreach(ManagementObject p in rows)using(p)
            if(activePaths.Any(path=>IsPathOnPartition(p,path)))throw new InvalidOperationException("Este disco contiene Windows, WinPart, paginación o una ruta de volcado activa. No se vaciará ni forzará su eliminación.");
    }
    static void ValidateAdvanced(PendingResize c)
    {
        if(!c.Advanced||c.IsVirtual||c.Create||c.FileSystem!=null||c.Bytes!=0||(c.Delete==c.Reset))throw new InvalidOperationException("Plan avanzado no válido.");
        if(c.Reset&&c.ResetStyle!="GPT"&&c.ResetStyle!="MBR")throw new InvalidOperationException("Tabla de destino no admitida.");
        ProtectRunningSystem(c.Disk);
        if(c.Delete)
        {
            if(c.Part.Gap||c.Part.Number==0)throw new InvalidOperationException("Selecciona una partición real.");
            using(var q=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Partition WHERE DiskNumber="+c.Disk.Number+" AND PartitionNumber="+c.Part.Number))using(var rows=q.Get())
            {
                if(rows.Count!=1)throw new InvalidOperationException("La partición ya no existe.");
                foreach(ManagementObject row in rows)using(row)
                    if(Convert.ToUInt64(row["Offset"])!=c.Part.Offset||Convert.ToUInt64(row["Size"])!=c.Part.Size||PartitionType(Value(row,"GptType"),Value(row,"MbrType"))!=c.Part.Type)throw new InvalidOperationException("La partición cambió. No se ejecutará la eliminación.");
            }
        }
    }
    static void RunDiskPart(params string[] commands)
    {
        // Commands contain only allowlisted literals and validated numeric disk/partition identifiers.
        // No privileged script file can be modified while waiting for UAC.
        using(var p=new Process {StartInfo=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"diskpart.exe")) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}})
        {
            p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
            foreach(string command in commands)p.StandardInput.WriteLine(command);p.StandardInput.WriteLine("exit");p.StandardInput.Close();p.WaitForExit();
            string log=output.GetAwaiter().GetResult(),problem=error.GetAwaiter().GetResult();
            if(p.ExitCode!=0)throw new InvalidOperationException("DiskPart rechazó la operación: "+log+" "+problem);
        }
    }
    static void ApplyAdvanced(PendingResize c)
    {
        ValidateAdvanced(c);
        if(c.Delete)RunDiskPart("select disk "+c.Disk.Number,"select partition "+c.Part.Number,"delete partition override");
        else RunDiskPart("select disk "+c.Disk.Number,"clean","convert "+c.ResetStyle.ToLowerInvariant());
        var snapshot=new Snapshot();var disk=ReadDiskLayouts(snapshot).SingleOrDefault(d=>d.Number==c.Disk.Number&&d.Size==c.Disk.Size&&d.Name==c.Disk.Name&&d.Complete);
        if(disk==null)throw new InvalidOperationException("No se pudo confirmar el resultado. Actualiza el mapa.");
        if(c.Delete&&(disk.UniqueId!=c.Disk.UniqueId||disk.Partitions.Any(p=>p.Offset==c.Part.Offset)))throw new InvalidOperationException("No se confirmó la eliminación. No se asumió éxito a partir del texto de DiskPart.");
        if(c.Reset&&(disk.Style!=c.ResetStyle||disk.Partitions.Any(p=>p.Type!="Reservada MSR")))throw new InvalidOperationException("El disco no quedó vacío con la tabla solicitada. Puede haberse completado sólo parte de la operación; revisa el mapa.");
    }
    static void AdvancedIntegrationTest()
    {
        string path=Path.Combine(Path.GetTempPath(),"winpart-advanced-"+Guid.NewGuid().ToString("N")+".vhd");
        try
        {
            ExecuteOperations(new[]{new PendingResize {VirtualAction="create",VirtualPath=path,Bytes=1024UL*1048576}});
            uint number=uint.Parse(RunStorageScript(VirtualDiskLookup(path)+"[Console]::Write($disk.Number);").Trim());
            RunStorageScript(VirtualDiskLookup(path)+"New-Partition -DiskNumber $disk.Number -Size 32MB -GptType '{C12A7328-F81F-11D2-BA4B-00A0C93EC93B}' | Out-Null; New-Partition -DiskNumber $disk.Number -Size 32MB -GptType '{DE94BBA4-06D1-4D40-A16A-BFD50179D6AC}' | Out-Null; New-Partition -DiskNumber $disk.Number -Size 32MB -GptType '{0FC63DAF-8483-4772-8E79-3D69D8477DE4}' | Out-Null;");
            Func<DiskLayout> reload=()=>ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number);
            var original=reload();var data=original.Partitions.Single(p=>p.Letter.Length==1);string file=data.Letter+@":\advanced-sentinel.txt";File.WriteAllText(file,"Conservar partición de datos");
            foreach(string type in new[]{"EFI","Recuperación","Reservada MSR","Tipo GPT 0FC63DAF-8483-4772-8E79-3D69D8477DE4"})
            {
                var disk=reload();var part=disk.Partitions.SingleOrDefault(p=>p.Type.Equals(type,StringComparison.OrdinalIgnoreCase));if(part==null)throw new Exception("No se encontró "+type+". Tipos leídos: "+string.Join(", ",disk.Partitions.Select(p=>p.Type)));
                ExecuteOperations(DecodeOperations(EncodeOperations(new[]{new PendingResize {Advanced=true,Delete=true,Disk=disk,Part=part}})));
                if(reload().Partitions.Any(p=>p.Offset==part.Offset)||File.ReadAllText(file)!="Conservar partición de datos")throw new Exception("Eliminación avanzada no confirmada o datos ajenos alterados");
            }
            var resetDisk=reload();ExecuteOperations(DecodeOperations(EncodeOperations(new[]{new PendingResize {Advanced=true,Reset=true,ResetStyle="MBR",Disk=resetDisk,Part=new PartitionLayout()}})));
            var mbr=reload();if(mbr.Style!="MBR")throw new Exception("No prepara MBR");
            var gap=mbr.Segments().First(g=>CanCreate(mbr,g));ExecuteOperations(DecodeOperations(EncodeOperations(new[]{NewPartitionChange(mbr,gap,64UL*1048576,"NTFS","TEST_MBR")})));
            mbr=reload();var mp=mbr.Partitions.Single(p=>p.Letter.Length==1);string mfile=mp.Letter+@":\mbr-test.txt";File.WriteAllText(mfile,"MBR operativo");if(File.ReadAllText(mfile)!="MBR operativo")throw new Exception("Nueva partición MBR no utilizable");
            ExecuteOperations(new[]{new PendingResize {Advanced=true,Reset=true,ResetStyle="GPT",Disk=mbr,Part=new PartitionLayout()}});if(reload().Style!="GPT")throw new Exception("No prepara GPT");
            var system=ReadDiskLayouts(new Snapshot()).First(d=>d.IsBoot||d.IsSystem);bool blocked=false;try{ValidateAdvanced(new PendingResize {Advanced=true,Reset=true,ResetStyle="MBR",Disk=system,Part=new PartitionLayout()});}catch{blocked=true;}if(!blocked)throw new Exception("No bloquea disco del Windows actual");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","advanced-ok.txt"),"OK: eliminar EFI, recuperación, MSR y tipo Linux sin letra en VHD; archivo ajeno conservado; vaciar/preparar MBR; crear/formatear/escribir NTFS MBR; vaciar/preparar GPT; bloqueo del disco en uso. No prueba descifrado ni movimiento/copia de particiones.");
        }
        finally{try{using(var h=VhdNative.Open(path))VhdNative.Detach(h);}catch{}if(File.Exists(path))File.Delete(path);}
    }
}

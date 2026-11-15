// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

internal sealed partial class MainForm
{
    Button BuildVirtualButton()
    {
        var button=new Button {Text="Disco virtual de prueba ▾",AutoSize=true};
        var menu=new ContextMenuStrip();
        menu.Items.Add("Crear disco virtual…",null,(s,e)=>PrepareVirtualCreate());
        menu.Items.Add("Conectar archivo VHD…",null,(s,e)=>PrepareVirtualFile("attach"));
        menu.Items.Add("Desconectar VHD (conservar archivo)…",null,(s,e)=>PrepareVirtualFile("detach"));
        button.Click+=(s,e)=>menu.Show(button,new Point(0,button.Height));button.Disposed+=(s,e)=>menu.Dispose();return button;
    }
    void PrepareVirtualCreate()
    {
        using(var picker=new SaveFileDialog {Title="Nuevo disco virtual de prueba",Filter="Disco virtual (*.vhd)|*.vhd",DefaultExt="vhd",AddExtension=true,FileName="WinPart-pruebas.vhd",OverwritePrompt=true})
        {
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            if(File.Exists(picker.FileName)){MessageBox.Show(this,"Elige un archivo nuevo. WinPart no reemplaza discos virtuales existentes.");return;}
            using(var dialog=new Form {Text="Crear disco virtual de prueba",Size=new Size(530,280),MinimumSize=new Size(480,280),StartPosition=FormStartPosition.CenterParent,Font=Font,AutoScaleMode=AutoScaleMode.Dpi,MinimizeBox=false,MaximizeBox=false})
            {
                var layout=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(15),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
                layout.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(460,0),Text="VHD dinámico: el archivo crece al guardar datos. Se creará una partición GPT / NTFS con la mitad del tamaño; la otra mitad quedará sin asignar para pruebas."});
                layout.Controls.Add(new Label {Text="Capacidad virtual total (MiB):",AutoSize=true,Margin=new Padding(3,15,3,3)});
                var size=new NumericUpDown {Minimum=256,Maximum=65536,Value=1024,Increment=256,ThousandsSeparator=true,Width=180};layout.Controls.Add(size);
                layout.Controls.Add(new Label {Text="No se ejecutará hasta confirmar «Aplicar».",AutoSize=true});
                var stage=new Button {Text="Añadir a pendientes",AutoSize=true,DialogResult=DialogResult.OK};layout.Controls.Add(stage);dialog.AcceptButton=stage;dialog.Controls.Add(layout);
                if(dialog.ShowDialog(this)==DialogResult.OK)StageVirtual(new PendingResize {VirtualAction="create",VirtualPath=picker.FileName,Bytes=(ulong)size.Value*1048576UL});
            }
        }
    }
    void PrepareVirtualFile(string action)
    {
        using(var picker=new OpenFileDialog {Title=action=="detach"?"Desconectar disco virtual (no borra el archivo)":"Conectar disco virtual",Filter="Disco virtual (*.vhd)|*.vhd",CheckFileExists=true})
            if(picker.ShowDialog(this)==DialogResult.OK)StageVirtual(new PendingResize {VirtualAction=action,VirtualPath=picker.FileName});
    }
    void StageVirtual(PendingResize change)
    {
        try
        {
            change.VirtualPath=Path.GetFullPath(change.VirtualPath);ValidateVirtualSyntax(change);
            pendingResizes.RemoveAll(p=>p.IsVirtual&&p.VirtualPath.Equals(change.VirtualPath,StringComparison.OrdinalIgnoreCase));pendingResizes.Add(change);
            editingDisk=null;editingPartition=null;formatEditor.Visible=false;
            foreach(Control control in inlineEditor.Controls)control.Visible=control==pendingList;
            inlineEditor.Visible=true;partitionInfo.Visible=true;
            partitionInfo.Text=change.Description+Environment.NewLine+"Pendiente: no se ha creado, conectado ni desconectado ningún disco. Revisa y pulsa Aplicar.";
            UpdatePending();RenderDiskMaps();
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Disco virtual",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
    internal static void ValidateVirtualSyntax(PendingResize c)
    {
        if(!new[]{"create","attach","detach"}.Contains(c.VirtualAction))throw new InvalidOperationException("Acción de disco virtual no válida.");
        string path=c.VirtualPath;
        if(string.IsNullOrWhiteSpace(path)||path.Length>240||!Path.IsPathRooted(path)||path.Length<4||path[1]!=':'||path[2]!='\\'||!char.IsLetter(path[0])||!Path.GetExtension(path).Equals(".vhd",StringComparison.OrdinalIgnoreCase)||path.IndexOfAny(new[]{'\r','\n','\0','"'})>=0||!Path.GetFullPath(path).Equals(path,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Elige una ruta local completa para un archivo .vhd nuevo o existente.");
        if(c.VirtualAction=="create"&&(c.Bytes<256UL*1048576||c.Bytes>65536UL*1048576||c.Bytes%1048576!=0))throw new InvalidOperationException("La capacidad debe ser de 256 a 65536 MiB enteros.");
        if(c.VirtualAction!="create"&&c.Bytes!=0)throw new InvalidOperationException("Plan VHD inválido.");
    }
    static void ValidateVirtual(PendingResize c)
    {
        ValidateVirtualSyntax(c);
        if(c.VirtualAction=="create")
        {
            if(File.Exists(c.VirtualPath)||Directory.Exists(c.VirtualPath))throw new InvalidOperationException("El archivo VHD ya existe; no se sobrescribirá.");
            if(!Directory.Exists(Path.GetDirectoryName(c.VirtualPath)))throw new InvalidOperationException("La carpeta de destino no existe.");
        }
        else
        {
            if(!File.Exists(c.VirtualPath))throw new InvalidOperationException("No se encuentra el archivo VHD.");
            using(var handle=VhdNative.Open(c.VirtualPath)){} // Validates actual VHD format, not just extension.
            if(c.VirtualAction=="detach")RunStorageScript(VirtualDetachGuard(c.VirtualPath));
        }
    }
    static string PsLiteral(string value){return "'"+value.Replace("'","''")+"'";}
    static string VirtualDiskLookup(string path)
    {
        return "$image=Get-DiskImage -ImagePath "+PsLiteral(path)+" -ErrorAction Stop; if(-not $image.Attached){throw 'El VHD no está conectado'}; $disks=@($image | Get-Disk -ErrorAction Stop); if($disks.Count -ne 1){throw 'Identidad de VHD ambigua'}; $disk=$disks[0]; if($disk.IsBoot -or $disk.IsSystem){throw 'No se permite modificar un disco virtual de arranque o sistema'}; ";
    }
    static string VirtualDetachGuard(string path)
    {
        // Rechecked just before detaching, including boot, paging file and the running executable.
        return VirtualDiskLookup(path)+"$letters=@($disk | Get-Partition | Where-Object DriveLetter | ForEach-Object {([string]$_.DriveLetter)+':'}); $protected=@($env:SystemDrive,"+PsLiteral(Path.GetPathRoot(Application.ExecutablePath).TrimEnd('\\'))+"); $protected+=@(Get-CimInstance Win32_PageFileUsage | ForEach-Object {Split-Path -Qualifier $_.Name}); foreach($drive in $protected){if($letters -contains $drive){throw 'El VHD está en uso por Windows, paginación o WinPart'}}; ";
    }
    static void ApplyVirtual(PendingResize c)
    {
        ValidateVirtual(c);
        if(c.VirtualAction=="create")
        {
            // CreateVirtualDisk fails if the file exists. It cannot overwrite a physical disk.
            using(var handle=VhdNative.Create(c.VirtualPath,c.Bytes))VhdNative.Attach(handle);
            try
            {
                RunStorageScript(VirtualDiskLookup(c.VirtualPath)+"if($disk.PartitionStyle -ne 'RAW'){throw 'El disco nuevo no está vacío'}; $disk | Initialize-Disk -PartitionStyle GPT -ErrorAction Stop; $part=New-Partition -DiskNumber $disk.Number -Size "+(c.Bytes/2).ToString(System.Globalization.CultureInfo.InvariantCulture)+" -AssignDriveLetter -ErrorAction Stop; $part | Format-Volume -FileSystem NTFS -NewFileSystemLabel 'WINPART_TEST' -Confirm:$false -ErrorAction Stop | Out-Null; ");
            }
            catch
            {
                // Leave the newly created file for diagnosis, but release its attachment if preparation failed.
                try{using(var handle=VhdNative.Open(c.VirtualPath))VhdNative.Detach(handle);}catch{}
                throw;
            }
        }
        else if(c.VirtualAction=="attach")
        {using(var handle=VhdNative.Open(c.VirtualPath))VhdNative.Attach(handle);}
        else
        {
            RunStorageScript(VirtualDetachGuard(c.VirtualPath));
            using(var handle=VhdNative.Open(c.VirtualPath))VhdNative.Detach(handle);
        }
    }
    static string RunStorageScript(string body)
    {
        // Fixed script templates only; paths are quoted PowerShell literals, never command fragments.
        string script="$ErrorActionPreference='Stop'; try { "+body+" } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
        using(var process=new Process {StartInfo=new ProcessStartInfo(ps,"-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}})
        {
            process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();
            string text=output.GetAwaiter().GetResult(),problem=error.GetAwaiter().GetResult();
            if(process.ExitCode!=0)throw new InvalidOperationException("Windows no completó la operación VHD: "+problem);return text;
        }
    }
    internal static void VirtualDiskIntegrationTest()
    {
        string path=Path.Combine(Path.GetTempPath(),"winpart-integration-"+Guid.NewGuid().ToString("N")+".vhd");
        try
        {
            var plan=DecodeOperations(EncodeOperations(new[]{new PendingResize {VirtualAction="create",VirtualPath=path,Bytes=512UL*1048576}}));
            if(File.Exists(path))throw new Exception("La preparación VHD escribió antes de aplicar");
            ExecuteOperations(plan);
            string drive=RunStorageScript(VirtualDiskLookup(path)+"$parts=@($disk | Get-Partition | Where-Object DriveLetter); if($parts.Count -ne 1){throw 'Falta partición de prueba'}; $vol=$parts[0] | Get-Volume; if($vol.FileSystem -ne 'NTFS' -or $vol.FileSystemLabel -ne 'WINPART_TEST'){throw 'Formato de prueba incorrecto'}; if($parts[0].Size -ne 268435456){throw 'Tamaño de partición incorrecto'}; [Console]::Write($parts[0].DriveLetter);").Trim();
            if(drive.Length!=1)throw new Exception("Letra de VHD no válida");
            string file=drive+@":\winpart-virtual-sentinel.txt";File.WriteAllText(file,"Prueba VHD de WinPart");
            bool overwriteRejected=false;try{ValidateVirtual(plan[0]);}catch{overwriteRejected=true;}if(!overwriteRejected)throw new Exception("Permite sobrescribir VHD");
            ExecuteOperations(DecodeOperations(EncodeOperations(new[]{new PendingResize {VirtualAction="detach",VirtualPath=path}})));
            if(!File.Exists(path)||Directory.Exists(drive+@":\"))throw new Exception("Desconexión VHD incorrecta");
            ExecuteOperations(DecodeOperations(EncodeOperations(new[]{new PendingResize {VirtualAction="attach",VirtualPath=path}})));
            string newDrive=RunStorageScript(VirtualDiskLookup(path)+"[Console]::Write(($disk | Get-Partition | Where-Object DriveLetter | Select-Object -First 1).DriveLetter);").Trim();
            if(File.ReadAllText(newDrive+@":\winpart-virtual-sentinel.txt")!="Prueba VHD de WinPart")throw new Exception("Se perdieron datos al reconectar");
            ExecuteOperations(new[]{new PendingResize {VirtualAction="detach",VirtualPath=path}});
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","virtual-ok.txt"),"OK: plan sin escritura; creación VHD dinámico 512 MiB; partición GPT NTFS 256 MiB; rechazo de sobrescritura; desconexión conserva archivo; reconexión conserva contenido.");
        }
        finally
        {
            try{using(var h=VhdNative.Open(path))VhdNative.Detach(h);}catch{}
            if(File.Exists(path))File.Delete(path); // Only this unique disposable CI image.
        }
    }
}
internal static class VhdNative
{
    // Windows Virtual Disk API. Version 1 creates expandable VHD (not fixed allocation).
    [StructLayout(LayoutKind.Sequential)] struct StorageType {public uint Device;public Guid Vendor;}
    [StructLayout(LayoutKind.Sequential)] struct CreateV1 {public Guid Id;public ulong MaximumSize;public uint BlockSize,SectorSize;public IntPtr ParentPath,SourcePath;}
    [StructLayout(LayoutKind.Sequential)] struct CreateParameters {public uint Version;public CreateV1 V1;}
    [StructLayout(LayoutKind.Sequential)] struct OpenParameters {public uint Version,ReadWriteDepth;}
    [StructLayout(LayoutKind.Sequential)] struct AttachParameters {public uint Version,Reserved;}
    [DllImport("virtdisk.dll",CharSet=CharSet.Unicode)] static extern uint CreateVirtualDisk(ref StorageType type,string path,uint access,IntPtr security,uint flags,uint providerFlags,ref CreateParameters parameters,IntPtr overlapped,out SafeFileHandle handle);
    [DllImport("virtdisk.dll",CharSet=CharSet.Unicode)] static extern uint OpenVirtualDisk(ref StorageType type,string path,uint access,uint flags,ref OpenParameters parameters,out SafeFileHandle handle);
    [DllImport("virtdisk.dll")] static extern uint AttachVirtualDisk(SafeFileHandle handle,IntPtr security,uint flags,uint providerFlags,ref AttachParameters parameters,IntPtr overlapped);
    [DllImport("virtdisk.dll")] static extern uint DetachVirtualDisk(SafeFileHandle handle,uint flags,uint providerFlags);
    static StorageType Type {get{return new StorageType {Device=2,Vendor=new Guid("EC984AEC-A0F9-47E9-901F-71415A66345B")};}}
    static void Check(uint result){if(result!=0)throw new Win32Exception((int)result);}
    public static SafeFileHandle Create(string path,ulong bytes)
    {
        var type=Type;var parameters=new CreateParameters {Version=1,V1=new CreateV1 {Id=Guid.NewGuid(),MaximumSize=bytes,SectorSize=512}};SafeFileHandle handle;
        uint result=CreateVirtualDisk(ref type,path,0x3F0000,IntPtr.Zero,0,0,ref parameters,IntPtr.Zero,out handle);
        if(result!=0){if(handle!=null)handle.Dispose();Check(result);}return handle;
    }
    public static SafeFileHandle Open(string path)
    {
        var type=Type;var parameters=new OpenParameters {Version=1,ReadWriteDepth=1};SafeFileHandle handle;
        uint result=OpenVirtualDisk(ref type,path,0x3F0000,0,ref parameters,out handle);
        if(result!=0){if(handle!=null)handle.Dispose();Check(result);}return handle;
    }
    public static void Attach(SafeFileHandle handle){var p=new AttachParameters {Version=1};Check(AttachVirtualDisk(handle,IntPtr.Zero,4,0,ref p,IntPtr.Zero));}
    public static void Detach(SafeFileHandle handle){Check(DetachVirtualDisk(handle,0,0));}
}

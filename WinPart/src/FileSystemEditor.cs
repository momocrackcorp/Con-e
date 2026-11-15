// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    readonly Button formatButton=new Button {Text="Sistema de archivos…",AutoSize=true,Enabled=false};
    readonly FlowLayoutPanel formatEditor=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new Padding(10),Visible=false,BackColor=Color.FromArgb(255,244,223)};
    readonly ComboBox formatChoice=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
    readonly Label formatWarning=new Label {AutoSize=true,MaximumSize=new Size(600,0)};
    DiskLayout formatDisk;PartitionLayout formatPart;string formatVolumeId;
    static bool CanFormat(DiskLayout disk,PartitionLayout p)
    {
        if(!disk.Complete||disk.UniqueId==""||disk.UniqueId=="No disponible"||p.Gap||p.Type!="Datos básicos"||p.Letter.Length!=1||p.IsBoot||p.IsSystem)return false;
        string root=p.Letter+":";
        return !root.Equals(System.IO.Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)&&!root.Equals(System.IO.Path.GetPathRoot(Application.ExecutablePath).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);
    }
    Control BuildFormatEditor()
    {
        formatEditor.Controls.Add(formatWarning);formatEditor.Controls.Add(formatChoice);
        var stage=new Button {Text="Añadir formateo a pendientes",AutoSize=true};formatEditor.Controls.Add(stage);
        stage.Click+=(s,e)=>{if(formatDisk!=null&&formatChoice.SelectedItem!=null)StageFormat(formatDisk,formatPart,Convert.ToString(formatChoice.SelectedItem),formatVolumeId);};
        formatButton.Click+=async(s,e)=>
        {
            if(selectedEditDisk==null||selectedEditPartition==null)return;
            var disk=selectedEditDisk;var part=selectedEditPartition;
            try
            {
                var options=await System.Threading.Tasks.Task.Run(()=>FormatOptions(disk,part));if(IsDisposed)return;
                formatDisk=disk;formatPart=part;formatVolumeId=options.Item1;formatChoice.Items.Clear();formatChoice.Items.AddRange(options.Item2.Cast<object>().ToArray());
                if(formatChoice.Items.Count==0)throw new InvalidOperationException("Windows no informa formatos compatibles admitidos por WinPart.");
                formatChoice.SelectedIndex=0;formatWarning.Text="FORMATEAR · disco "+disk.Number+" · "+part.Letter+": · actual "+part.FileSystem+Environment.NewLine+"Borrará TODOS los archivos al aplicar y confirmar. Opciones Windows; compatibilidad y tamaño se validan al aplicar. ext4 / swap: no implementados.";
                foreach(Control control in inlineEditor.Controls)control.Visible=ReferenceEquals(control,pendingList);inlineEditor.Visible=true;formatEditor.Visible=true;partitionInfo.Visible=false;
            }
            catch(Exception ex)
            {
                if(!IsAdmin()&&CanFormat(disk,part))
                {formatDisk=disk;formatPart=part;formatVolumeId="";formatChoice.Items.Clear();formatChoice.Items.AddRange(new object[]{"NTFS","exFAT","FAT32"});formatChoice.SelectedIndex=0;formatWarning.Text="FORMATEAR "+part.Letter+": BORRA TODOS LOS ARCHIVOS. La compatibilidad se verificará con permisos al aplicar.";foreach(Control control in inlineEditor.Controls)control.Visible=ReferenceEquals(control,pendingList);inlineEditor.Visible=true;formatEditor.Visible=true;partitionInfo.Visible=false;status.Text="Preparación sin permisos; compatibilidad pendiente.";}
                else status.Text="No se preparó el formateo: "+ex.Message;
            }
        };
        return formatEditor;
    }
    static ManagementObject FormatVolume(DiskLayout disk,PartitionLayout part)
    {
        if(!CanFormat(disk,part))throw new InvalidOperationException("Volumen protegido o no compatible. No se permite formatear Windows, arranque, recuperación o el volumen de WinPart.");
        using(var partition=CurrentPartition(disk,part))
        {
            partition.Get();if(Convert.ToBoolean(partition["IsBoot"])||Convert.ToBoolean(partition["IsSystem"]))throw new InvalidOperationException("Partición crítica protegida.");
            if(IsPathOnPartition(partition,Environment.SystemDirectory)||IsPathOnPartition(partition,Application.ExecutablePath))throw new InvalidOperationException("El volumen contiene Windows o la aplicación en ejecución.");
            string letter=Convert.ToString(partition["DriveLetter"]).Trim('\0',' ');if(letter!=part.Letter)throw new InvalidOperationException("Cambió la letra del volumen. Actualiza el mapa.");
            using(var pages=new ManagementObjectSearcher("SELECT Name FROM Win32_PageFileUsage"))
            using(var rows=pages.Get())foreach(ManagementObject row in rows)using(row)if(IsPathOnPartition(partition,Value(row,"Name")))throw new InvalidOperationException("El volumen contiene un archivo de paginación activo.");
            using(var volumes=partition.GetRelated("MSFT_Volume"))
            {
                if(volumes.Count!=1)throw new InvalidOperationException("No se pudo identificar un único volumen asociado.");
                foreach(ManagementObject volume in volumes)using(volume)return new ManagementObject(volume.Path);
            }
        }
        throw new InvalidOperationException("Volumen no disponible.");
    }
    static Tuple<string,string[]> FormatOptions(DiskLayout disk,PartitionLayout part)
    {
        using(var volume=FormatVolume(disk,part))
        {volume.Get();using(var result=volume.InvokeMethod("GetSupportedFileSystems",(ManagementBaseObject)null,null))
        {
            if(Convert.ToUInt32(result["ReturnValue"])!=0)throw new InvalidOperationException("Windows no pudo consultar formatos compatibles.");
            var formats=(string[])result["SupportedFileSystems"];
            return Tuple.Create(Value(volume,"UniqueId"),formats.Where(f=>new[]{"NTFS","exFAT","FAT32"}.Contains(f,StringComparer.OrdinalIgnoreCase)).Concat(new[]{"NTFS","exFAT","FAT32"}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }}
    }
    void StageFormat(DiskLayout disk,PartitionLayout part,string fs,string volumeId)
    {
        pendingResizes.RemoveAll(p=>!p.IsVirtual&&!p.Create&&p.Disk.UniqueId==disk.UniqueId&&p.Part.Number==part.Number);
        pendingResizes.Add(new PendingResize {Disk=disk,Part=part,FileSystem=fs,VolumeId=volumeId});UpdatePending();RenderDiskMaps();
        inlineEditor.Visible=true;formatEditor.Visible=true;partitionInfo.Visible=false;
    }
    static void ValidateFormat(PendingResize change)
    {
        ValidateFileSystemSize(change.FileSystem,change.Part.Size);
        var options=FormatOptions(change.Disk,change.Part);
        if(change.VolumeId=="")change.VolumeId=options.Item1;
        if(options.Item1!=change.VolumeId||!options.Item2.Contains(change.FileSystem,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("El volumen o sus formatos compatibles cambiaron. Prepara de nuevo la operación.");
    }
    static void ApplyFormat(PendingResize change)
    {
        ValidateFormat(change);
        using(var volume=FormatVolume(change.Disk,change.Part))
        {
            volume.Get();if(Value(volume,"UniqueId")!=change.VolumeId)throw new InvalidOperationException("La identidad del volumen cambió.");
            using(var input=volume.GetMethodParameters("Format"))
            {input["FileSystem"]=change.FileSystem;input["Full"]=false;input["Force"]=false;
             using(var result=volume.InvokeMethod("Format",input,null))if(Convert.ToUInt32(result["ReturnValue"])!=0)throw new InvalidOperationException("Windows rechazó el formateo (código "+result["ReturnValue"]+"). No se fuerza el desmontaje.");}
        }
    }
}

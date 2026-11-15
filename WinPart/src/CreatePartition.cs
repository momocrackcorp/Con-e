// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    readonly Button createPartition=new Button {Text="Nueva partición…",AutoSize=true,Enabled=false};
    static ulong AlignMiB(ulong value){return ((value+1048575UL)/1048576UL)*1048576UL;}
    static ulong CreateStart(PartitionLayout gap){return Math.Max(1048576UL,AlignMiB(gap.Offset));}
    static ulong CreateCapacity(DiskLayout disk,PartitionLayout gap)
    {
        if(disk.Size<2097152UL||gap.Offset>disk.Size||gap.Size>disk.Size-gap.Offset)return 0;
        ulong start=CreateStart(gap),end=Math.Min(gap.Offset+gap.Size,disk.Size-1048576UL);end=end/1048576UL*1048576UL;
        return end>start?end-start:0;
    }
    internal static bool CanCreate(DiskLayout disk,PartitionLayout gap)
    {return disk!=null&&gap!=null&&disk.Complete&&disk.Style=="GPT"&&!string.IsNullOrWhiteSpace(disk.UniqueId)&&disk.UniqueId!="No disponible"&&gap.Gap&&CreateCapacity(disk,gap)>=16UL*1048576;}
    Control BuildCreateButton()
    {
        createPartition.Click+=(s,e)=>{if(selectedEditDisk!=null&&selectedEditPartition!=null)PrepareCreate(selectedEditDisk,selectedEditPartition);};return createPartition;
    }
    void PrepareCreate(DiskLayout disk,PartitionLayout gap)
    {
        if(!CanCreate(disk,gap))return;
        using(var dialog=new Form {Text="Nueva partición · Disco "+disk.Number,Size=new Size(570,380),MinimumSize=new Size(510,360),StartPosition=FormStartPosition.CenterParent,Font=Font,AutoScaleMode=AutoScaleMode.Dpi,MinimizeBox=false,MaximizeBox=false})
        {
            var layout=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(15)};
            layout.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(500,0),Text="Espacio sin asignar: "+Space(gap.Size)+". Inicio alineado: "+Space(CreateStart(gap))+". Se conserva el resto del espacio y las particiones existentes."});
            layout.Controls.Add(new Label {AutoSize=true,Text="Tamaño de la partición (MiB)"});
            var size=new NumericUpDown {Width=200,Minimum=16,Maximum=CreateCapacity(disk,gap)/1048576UL,Value=CreateCapacity(disk,gap)/1048576UL,ThousandsSeparator=true};layout.Controls.Add(size);
            layout.Controls.Add(new Label {AutoSize=true,Text="Sistema de archivos"});var fs=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=250};fs.Items.AddRange(new object[]{"NTFS","exFAT","FAT32","Sin formatear (RAW)"});fs.SelectedIndex=0;layout.Controls.Add(fs);
            layout.Controls.Add(new Label {AutoSize=true,Text="Etiqueta (opcional, hasta 11 caracteres)"});var label=new TextBox {Width=250,MaxLength=11};layout.Controls.Add(label);
            layout.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(500,0),Text="Windows asignará una letra disponible. FAT32: 32 MiB–32 GiB. ext4, btrfs y swap todavía no están implementados. No se escribe nada hasta confirmar Aplicar."});
            var stage=new Button {Text="Añadir a pendientes",AutoSize=true};layout.Controls.Add(stage);dialog.Controls.Add(layout);dialog.AcceptButton=stage;
            stage.Click+=(s,e)=>
            {
                try{string format=fs.SelectedIndex==3?"RAW":Convert.ToString(fs.SelectedItem);var change=NewPartitionChange(disk,gap,(ulong)size.Value*1048576UL,format,label.Text.Trim());ValidateCreateSyntax(change);dialog.DialogResult=DialogResult.OK;StageCreate(change);}
                catch(Exception ex){MessageBox.Show(dialog,ex.Message,"Nueva partición",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            };
            dialog.ShowDialog(this);
        }
    }
    static PendingResize NewPartitionChange(DiskLayout disk,PartitionLayout gap,ulong bytes,string fs,string label)
    {return new PendingResize {Create=true,Disk=disk,Part=gap,StartOffset=CreateStart(gap),Bytes=bytes,FileSystem=fs,NewLabel=label};}
    void StageCreate(PendingResize change)
    {
        ValidateCreateSyntax(change);
        pendingResizes.RemoveAll(p=>p.Create&&p.Disk.UniqueId==change.Disk.UniqueId&&p.Part.Offset==change.Part.Offset);
        pendingResizes.Add(change);editingDisk=null;editingPartition=null;formatEditor.Visible=false;
        foreach(Control control in inlineEditor.Controls)control.Visible=ReferenceEquals(control,pendingList);
        inlineEditor.Visible=true;partitionInfo.Visible=true;UpdatePending();RenderDiskMaps();
        partitionInfo.Text=change.Description+Environment.NewLine+"Pendiente: el espacio sigue sin asignar. Pulsa Aplicar y confirma para crearla. No se borran las particiones existentes.";
    }
    static void ValidateFileSystemSize(string fs,ulong bytes)
    {
        if(!new[]{"NTFS","exFAT","FAT32","RAW"}.Contains(fs,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Sistema de archivos no implementado por WinPart.");
        if(bytes<16UL*1048576)throw new InvalidOperationException("El tamaño mínimo es 16 MiB.");
        if(fs.Equals("FAT32",StringComparison.OrdinalIgnoreCase)&&(bytes<32UL*1048576||bytes>32UL*1073741824))throw new InvalidOperationException("El motor Windows utilizado admite FAT32 de 32 MiB a 32 GiB. Elige NTFS, exFAT o un tamaño menor.");
    }
    static void ValidateCreateSyntax(PendingResize c)
    {
        if(!c.Create||!CanCreate(c.Disk,c.Part)||c.StartOffset!=CreateStart(c.Part)||c.Bytes%1048576!=0||c.Bytes>CreateCapacity(c.Disk,c.Part))throw new InvalidOperationException("La nueva partición no cabe en el intervalo sin asignar o el disco no es GPT compatible.");
        ValidateFileSystemSize(c.FileSystem,c.Bytes);
        if(c.NewLabel==null||c.NewLabel.Length>11||c.NewLabel.Any(ch=>ch<32||"\\/:*?\"<>|".Contains(ch)))throw new InvalidOperationException("La etiqueta admite hasta 11 caracteres, sin símbolos de ruta ni control.");
    }
    static DiskLayout ValidateCreate(PendingResize c)
    {
        ValidateCreateSyntax(c);
        var disk=ReadDiskLayouts(new Snapshot()).SingleOrDefault(d=>d.Number==c.Disk.Number&&d.UniqueId==c.Disk.UniqueId&&d.Size==c.Disk.Size&&d.Complete&&d.Style=="GPT");
        if(disk==null)throw new InvalidOperationException("El disco cambió o ya no está disponible. Actualiza el mapa.");
        if(!disk.Segments().Any(g=>g.Gap&&g.Offset==c.Part.Offset&&g.Size==c.Part.Size))throw new InvalidOperationException("El espacio sin asignar cambió. No se creará la partición; prepara de nuevo la operación.");
        return disk;
    }
    static void ApplyCreate(PendingResize c)
    {
        ValidateCreate(c);
        string number=c.Disk.Number.ToString(CultureInfo.InvariantCulture),offset=c.StartOffset.ToString(CultureInfo.InvariantCulture),bytes=c.Bytes.ToString(CultureInfo.InvariantCulture);
        // The provider rejects overlapping extents. Select an exact disk identity and exact offset, never a default disk.
        string script="$disk=Get-Disk -Number "+number+" -ErrorAction Stop; if($disk.UniqueId -ne "+PsLiteral(c.Disk.UniqueId)+" -or $disk.Size -ne "+c.Disk.Size.ToString(CultureInfo.InvariantCulture)+" -or $disk.PartitionStyle -ne 'GPT' -or $disk.IsOffline -or $disk.IsReadOnly){throw 'El disco cambió, está desconectado o protegido'}; $part=New-Partition -DiskNumber $disk.Number -Offset "+offset+" -Size "+bytes+" -AssignDriveLetter -GptType '{EBD0A0A2-B9E5-4433-87C0-68B6B72699C7}' -ErrorAction Stop; ";
        // Separate creation from formatting so a format failure is reported without silently removing the new partition.
        RunStorageScript(script);
        if(c.FileSystem!="RAW")
        {
            try
            {
                var disk=ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==c.Disk.Number&&d.UniqueId==c.Disk.UniqueId&&d.Size==c.Disk.Size);
                var part=disk.Partitions.Single(p=>p.Offset==c.StartOffset&&p.Size==c.Bytes);
                var options=FormatOptions(disk,part);ApplyFormat(new PendingResize {Disk=disk,Part=part,FileSystem=c.FileSystem,VolumeId=options.Item1});
                if(c.NewLabel.Length>0)RunStorageScript("$part=Get-Partition -DiskNumber "+number+" | Where-Object { $_.Offset -eq "+offset+" -and $_.Size -eq "+bytes+" }; if(@($part).Count -ne 1){throw 'Partición nueva no identificada'}; $part | Get-Volume | Set-Volume -NewFileSystemLabel "+PsLiteral(c.NewLabel)+" -ErrorAction Stop;");
            }
            catch(Exception ex){throw new InvalidOperationException("La partición fue creada, pero no se completó el formato o la etiqueta. Se conserva para revisarla en el mapa. "+ex.Message,ex);}
        }
    }
    static void CreatePartitionIntegrationTest(string imagePath,string originalFile)
    {
        string content=File.ReadAllText(originalFile);
        foreach(string fs in new[]{"NTFS","FAT32","exFAT","RAW"})
        {
            uint number=uint.Parse(RunStorageScript(VirtualDiskLookup(imagePath)+"[Console]::Write($disk.Number);").Trim(),CultureInfo.InvariantCulture);
            var disk=ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number);
            var gap=disk.Segments().Where(g=>CanCreate(disk,g)).OrderByDescending(g=>g.Size).First();
            ulong bytes=(fs=="RAW"?16UL:64UL)*1048576;var change=NewPartitionChange(disk,gap,bytes,fs,fs=="RAW"?"":"TEST_"+fs);
            var plan=DecodeOperations(EncodeOperations(new[]{change}));
            int count=disk.Partitions.Count;ExecuteOperations(plan);
            var after=ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number&&d.UniqueId==disk.UniqueId);
            var created=after.Partitions.Single(p=>p.Offset==change.StartOffset&&p.Size==bytes);
            if(after.Partitions.Count!=count+1||created.Letter.Length!=1)throw new Exception("No se creó una única partición con letra");
            if(fs!="RAW")
            {
                if(!created.FileSystem.Equals(fs,StringComparison.OrdinalIgnoreCase)||created.Label!="TEST_"+fs)throw new Exception("Formato o etiqueta de nueva partición incorrectos: "+created.FileSystem);
                string test=created.Letter+@":\winpart-new-partition.txt";File.WriteAllText(test,"Test "+fs);if(File.ReadAllText(test)!="Test "+fs)throw new Exception("No se puede usar la nueva partición");
            }
            if(File.ReadAllText(originalFile)!=content)throw new Exception("Cambió el contenido de la partición existente");
            bool staleRejected=false;try{ValidateCreate(change);}catch{staleRejected=true;}if(!staleRejected)throw new Exception("Acepta intervalo obsoleto tras crear");
        }
        DeletePartitionIntegrationTest(imagePath,originalFile);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","create-ok.txt"),"OK: creación NTFS, FAT32, exFAT y RAW en intervalos libres de VHD; identidad/offset/tamaño; letra y etiqueta; escritura; contenido original conservado; rechazo de intervalo obsoleto.");
    }
}

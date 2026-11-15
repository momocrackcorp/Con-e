// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    static bool IsAdmin() { using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
    internal static bool CanResize(DiskLayout disk,PartitionLayout p)
    {return disk.Complete && !string.IsNullOrWhiteSpace(disk.UniqueId) && disk.UniqueId!="No disponible" && !p.Gap && p.Letter.Length==1 && p.FileSystem.Equals("NTFS",StringComparison.OrdinalIgnoreCase) && p.Type=="Datos básicos";}
    static ManagementObject CurrentPartition(DiskLayout disk,PartitionLayout part)
    {
        using(var search=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT UniqueId,Size FROM MSFT_Disk WHERE Number="+disk.Number))
        using(var rows=search.Get())
        {
            if(rows.Count!=1)throw new InvalidOperationException("El disco ya no está disponible.");
            foreach(ManagementObject row in rows)using(row)if(Value(row,"UniqueId")!=disk.UniqueId || Convert.ToUInt64(row["Size"])!=disk.Size)throw new InvalidOperationException("El disco cambió. Actualiza el mapa.");
        }
        var path=@"\\.\root\Microsoft\Windows\Storage:MSFT_Partition.DiskNumber="+disk.Number+",PartitionNumber="+part.Number;
        // Use the provider's complete object path; the class has additional inherited keys.
        using(var search=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Partition WHERE DiskNumber="+disk.Number+" AND PartitionNumber="+part.Number))
        using(var rows=search.Get())
        {
            foreach(ManagementObject row in rows)using(row)
            {
                if(Convert.ToUInt64(row["Offset"])!=part.Offset||Convert.ToUInt64(row["Size"])!=part.Size)throw new InvalidOperationException("La partición cambió. Actualiza el mapa.");
                string guid=Value(row,"GptType").Trim('{','}');
                if(!guid.Equals("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Sólo se admite partición GPT de datos básicos.");
                return new ManagementObject(row.Path);
            }
        }
        throw new InvalidOperationException("Partición no encontrada.");
    }
    internal static ulong[] ResizeBounds(DiskLayout disk,PartitionLayout part)
    {
        if(!CanResize(disk,part))throw new InvalidOperationException("Selecciona una partición GPT NTFS de datos básicos con letra, leída mediante Storage WMI.");
        using(var obj=CurrentPartition(disk,part))
        using(var result=obj.InvokeMethod("GetSupportedSize",(ManagementBaseObject)null,null))
        {
            if(Convert.ToUInt32(result["ReturnValue"])!=0)throw new InvalidOperationException("Windows no pudo determinar los tamaños permitidos. Código "+result["ReturnValue"]);
            return new[]{Convert.ToUInt64(result["SizeMin"]),Convert.ToUInt64(result["SizeMax"])};
        }
    }
    internal static void ApplyResize(DiskLayout disk,PartitionLayout part,ulong bytes)
    {
        var bounds=ResizeBounds(disk,part);
        if(bytes<bounds[0]||bytes>bounds[1]||bytes==part.Size)throw new InvalidOperationException("El tamaño está fuera de los límites permitidos o no cambia la partición.");
        using(var obj=CurrentPartition(disk,part))
        using(var input=obj.GetMethodParameters("Resize"))
        {
            input["Size"]=bytes;
            using(var result=obj.InvokeMethod("Resize",input,null))
                if(Convert.ToUInt32(result["ReturnValue"])!=0)throw new InvalidOperationException("Windows rechazó el cambio. Código "+result["ReturnValue"]+". Actualiza el mapa para comprobar el estado.");
        }
    }
    internal static void ResizeIntegrationTest()
    {
        string id=Environment.GetEnvironmentVariable("CONE_TEST_DISK_ID");uint number;
        if(string.IsNullOrWhiteSpace(id)||!uint.TryParse(Environment.GetEnvironmentVariable("CONE_TEST_DISK_NUMBER"),out number))throw new InvalidOperationException("Falta identidad del disco virtual de prueba");
        Func<DiskLayout> reload=()=>ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number&&d.UniqueId==id);
        var disk=reload();var part=disk.Partitions.Single(p=>CanResize(disk,p));
        string file=part.Letter+@":\cone-resize-test.txt";
        System.IO.File.WriteAllText(file,"Con-E: conservar datos durante reducción y ampliación");
        string original=System.IO.File.ReadAllText(file);ulong oldSize=part.Size;
        ApplyResize(disk,part,128UL*1048576);
        if(System.IO.File.ReadAllText(file)!=original)throw new InvalidOperationException("Contenido cambió tras reducción");
        disk=reload();part=disk.Partitions.Single(p=>CanResize(disk,p));
        if(part.Size!=128UL*1048576)throw new InvalidOperationException("Tamaño reducido incorrecto");
        ApplyResize(disk,part,oldSize);
        if(System.IO.File.ReadAllText(file)!=original)throw new InvalidOperationException("Contenido cambió tras ampliación");
        disk=reload();if(!disk.Partitions.Any(p=>p.Size==oldSize))throw new InvalidOperationException("Tamaño ampliado incorrecto");
        disk=reload();part=disk.Partitions.Single(p=>CanResize(disk,p));
        var formats=FormatOptions(disk,part);string alternate=formats.Item2.FirstOrDefault(f=>f.Equals("FAT32",StringComparison.OrdinalIgnoreCase))??formats.Item2.FirstOrDefault(f=>f.Equals("exFAT",StringComparison.OrdinalIgnoreCase))??"NTFS";
        ApplyFormat(new PendingResize {Disk=disk,Part=part,FileSystem=alternate,VolumeId=formats.Item1});
        disk=reload();part=disk.Partitions.Single(p=>p.Letter.Length==1);
        if(!part.FileSystem.Equals(alternate,StringComparison.OrdinalIgnoreCase)||System.IO.File.Exists(file))throw new InvalidOperationException("Formateo no confirmado");
        formats=FormatOptions(disk,part);ApplyFormat(new PendingResize {Disk=disk,Part=part,FileSystem="NTFS",VolumeId=formats.Item1});
        disk=reload();part=disk.Partitions.Single(p=>CanResize(disk,p));
        System.IO.File.WriteAllText(file,"WinPart NTFS verificado");if(System.IO.File.ReadAllText(file)!="WinPart NTFS verificado")throw new InvalidOperationException("Escritura tras formateo incorrecta");
        System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","resize-ok.txt"),"OK: VHD GPT; reducción/ampliación con archivo conservado; formateo "+alternate+" comprobado y vuelta a NTFS con escritura verificada. Formatos compatibles: "+string.Join(", ",formats.Item2));
    }
    readonly System.Collections.Generic.List<PendingResize> pendingResizes=new System.Collections.Generic.List<PendingResize>();
    readonly FlowLayoutPanel inlineEditor=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new Padding(10),Visible=false,BackColor=Color.White};
    readonly Label editLimits=new Label {AutoSize=true,MaximumSize=new Size(500,0)};
    readonly NumericUpDown editSize=new NumericUpDown {Width=150,ThousandsSeparator=true};
    readonly ResizePreview resizePreview=new ResizePreview {Width=300,Height=66};
    readonly ListBox pendingList=new ListBox {Width=420,Height=65,HorizontalScrollbar=true};
    readonly Button applyPending=new Button {Text="Aplicar (0)",AutoSize=true,Enabled=false};
    readonly Button discardPending=new Button {Text="Descartar",AutoSize=true,Enabled=false};
    DiskLayout editingDisk;PartitionLayout editingPartition;
    bool previewUpdating,applyingChanges;
    Control BuildInlineEditor()
    {
        inlineEditor.Controls.Add(editLimits);inlineEditor.Controls.Add(resizePreview);
        inlineEditor.Controls.Add(new Label {Text="Tamaño final (MiB)",AutoSize=true,Margin=new Padding(5,7,3,3)});inlineEditor.Controls.Add(editSize);
        var stage=new Button {Text="Añadir cambio",AutoSize=true};inlineEditor.Controls.Add(stage);inlineEditor.Controls.Add(pendingList);
        editSize.ValueChanged+=(s,e)=> {if(previewUpdating)return;resizePreview.Target=(ulong)editSize.Value*1048576UL;resizePreview.Invalidate();};
        resizePreview.Changed+=(bytes)=>{previewUpdating=true;editSize.Value=Math.Max(editSize.Minimum,Math.Min(editSize.Maximum,decimal.Floor(bytes/1048576m)));previewUpdating=false;resizePreview.Target=(ulong)editSize.Value*1048576UL;resizePreview.Invalidate();};
        stage.Click+=(s,e)=>{if(editingDisk!=null&&editingPartition!=null)StageResize(editingDisk,editingPartition,(ulong)editSize.Value*1048576UL);};
        applyPending.Click+=async(s,e)=>await ApplyPending();
        discardPending.Click+=(s,e)=>{pendingResizes.Clear();inlineEditor.Visible=false;formatEditor.Visible=false;partitionInfo.Visible=true;UpdatePending();RenderDiskMaps();};
        return inlineEditor;
    }
    void StageResize(DiskLayout disk,PartitionLayout part,ulong bytes)
    {
        pendingResizes.RemoveAll(p=>p.Disk.UniqueId==disk.UniqueId&&p.Part.Number==part.Number);
        if(bytes!=part.Size)pendingResizes.Add(new PendingResize {Disk=disk,Part=part,Bytes=bytes});
        UpdatePending();RenderDiskMaps();
    }
    void UpdatePending()
    {
        pendingList.Items.Clear();foreach(var change in pendingResizes)pendingList.Items.Add(change.Description);
        applyPending.Text="Aplicar ("+pendingResizes.Count+")";applyPending.Enabled=pendingResizes.Count>0&&!applyingChanges;
        discardPending.Enabled=pendingResizes.Count>0&&!applyingChanges;
        status.Text=pendingResizes.Count==0?"Sin operaciones pendientes.":pendingResizes.Count+" cambio(s) preparado(s). El disco todavía no se modifica.";
    }
    async System.Threading.Tasks.Task ApplyPending()
    {
        if(applyingChanges||pendingResizes.Count==0)return;
        string summary=string.Join(Environment.NewLine,pendingResizes.Select(p=>p.Description));
        if(MessageBox.Show(this,summary+Environment.NewLine+Environment.NewLine+(pendingResizes.Any(p=>p.FileSystem!=null)?"ATENCIÓN: cada operación FORMATEAR BORRA TODOS LOS ARCHIVOS del volumen indicado. ":"")+"Se modificarán estas particiones. Ten un respaldo antes de continuar. ¿Aplicar todos los cambios?","Aplicar operaciones pendientes",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        applyingChanges=true;diskMaps.Enabled=false;inlineEditor.Enabled=false;actions.Enabled=false;UpdatePending();
        var closeGuard=new FormClosingEventHandler((s,e)=>e.Cancel=true);FormClosing+=closeGuard;
        try
        {
            // Validate all planned targets before performing the first write.
            foreach(var change in pendingResizes){if(change.FileSystem!=null){await System.Threading.Tasks.Task.Run(()=>ValidateFormat(change));continue;}var bounds=await System.Threading.Tasks.Task.Run(()=>ResizeBounds(change.Disk,change.Part));if(change.Bytes<bounds[0]||change.Bytes>bounds[1])throw new InvalidOperationException("Cambió el espacio permitido. Actualiza y prepara de nuevo los cambios.");}
            while(pendingResizes.Count>0)
            {
                var change=pendingResizes[0];partitionInfo.Text="Aplicando: "+change.Description+Environment.NewLine+"No desconectes el disco.";
                await System.Threading.Tasks.Task.Run(()=>{if(change.FileSystem!=null)ApplyFormat(change);else ApplyResize(change.Disk,change.Part,change.Bytes);});pendingResizes.RemoveAt(0);
            }
            partitionInfo.Text="Operaciones completadas.";
        }
        catch(Exception ex){MessageBox.Show(this,"La ejecución se detuvo: "+ex.Message+Environment.NewLine+"Las operaciones terminadas no se deshacen automáticamente. Se actualizará el mapa.","Resultado",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        finally
        {
            FormClosing-=closeGuard;pendingResizes.Clear();applyingChanges=false;inlineEditor.Enabled=true;diskMaps.Enabled=true;actions.Enabled=true;inlineEditor.Visible=false;formatEditor.Visible=false;partitionInfo.Visible=true;UpdatePending();await RefreshData();
        }
    }
    async void EditPartition(DiskLayout disk,PartitionLayout part)
    {
        if(!CanResize(disk,part)){partitionInfo.Text="Selecciona una partición GPT NTFS de datos básicos con letra.";return;}
        if(!IsAdmin())
        {try{Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true,Verb="runas"});Close();}catch(Exception ex){partitionInfo.Text="No se habilitó la edición: "+ex.Message;}return;}
        try
        {
            partitionInfo.Text="Consultando límites…";var bounds=await System.Threading.Tasks.Task.Run(()=>ResizeBounds(disk,part));if(IsDisposed)return;
            ConfigureEditor(disk,part,bounds[0],bounds[1]);
        }
        catch(Exception ex){partitionInfo.Text="No se pudo preparar el tamaño: "+ex.Message;}
    }
    void ConfigureEditor(DiskLayout disk,PartitionLayout part,ulong minimum,ulong maximum)
    {
        decimal min=decimal.Ceiling(minimum/1048576m),max=decimal.Floor(maximum/1048576m);if(min>max)throw new InvalidOperationException("Sin intervalo válido.");
        foreach(Control control in inlineEditor.Controls)control.Visible=true;
        formatEditor.Visible=false;resizePreview.Visible=true;editSize.Visible=true;
        editingDisk=disk;editingPartition=part;previewUpdating=true;
        // Reset the control range before setting a different partition's limits.
        editSize.Minimum=0;editSize.Maximum=decimal.MaxValue;editSize.Value=0;editSize.Maximum=max;editSize.Minimum=min;
        var queued=pendingResizes.FirstOrDefault(p=>p.Disk.UniqueId==disk.UniqueId&&p.Part.Number==part.Number);
        editSize.Value=Math.Max(min,Math.Min(max,decimal.Floor((queued==null?part.Size:queued.Bytes)/1048576m)));
        previewUpdating=false;
        resizePreview.Minimum=(ulong)min*1048576UL;resizePreview.Maximum=(ulong)max*1048576UL;resizePreview.Target=(ulong)editSize.Value*1048576UL;resizePreview.Invalidate();
        editLimits.Text="Disco "+disk.Number+" · "+part.Letter+": · actual "+Space(part.Size)+Environment.NewLine+"Permitido: "+Space(minimum)+" a "+Space(maximum)+". Arrastra el borde derecho.";
        partitionInfo.Visible=false;inlineEditor.Visible=true;UpdatePending();
    }
}
internal sealed class PendingResize
{
    public DiskLayout Disk;public PartitionLayout Part;public ulong Bytes;public string FileSystem,VolumeId;
    public string Description {get{if(FileSystem!=null)return "FORMATEAR · Disco "+Disk.Number+" · "+Part.Letter+": · "+Part.FileSystem+" → "+FileSystem+" · BORRA ARCHIVOS";return "Disco "+Disk.Number+" · "+Disk.Name+" · partición "+Part.Number+" ("+Part.Letter+":): "+(Part.Size/1073741824d).ToString("0.##")+" → "+(Bytes/1073741824d).ToString("0.##")+" GiB";}}
}
internal sealed class ResizePreview : Control
{
    public ulong Minimum,Maximum,Target;public event Action<ulong> Changed;bool dragging;
    public ResizePreview(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.Selectable,true);TabStop=true;AccessibleName="Vista previa del tamaño de partición. También se puede editar con el campo numérico.";}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.Clear(Color.White);var track=new Rectangle(4,8,Math.Max(1,Width-12),Height-22);
        using(var brush=new SolidBrush(Color.FromArgb(224,230,239)))e.Graphics.FillRectangle(brush,track);
        int width=Maximum==0?0:(int)(Target/(double)Maximum*track.Width);using(var brush=new SolidBrush(Color.FromArgb(70,121,201)))e.Graphics.FillRectangle(brush,track.X,track.Y,width,track.Height);
        using(var pen=new Pen(Color.FromArgb(25,45,75),5))e.Graphics.DrawLine(pen,track.X+width,track.Y,track.X+width,track.Bottom);
        TextRenderer.DrawText(e.Graphics,(Target/1073741824d).ToString("0.##")+" GiB (vista previa)",Font,track,Color.FromArgb(20,35,55),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;dragging=true;Capture=true;Adjust(e.X);}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Adjust(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
    void Adjust(int x){if(Maximum==0)return;double ratio=Math.Max(0,Math.Min(1,(x-4d)/Math.Max(1,Width-12)));ulong bytes=Math.Max(Minimum,Math.Min(Maximum,(ulong)(ratio*Maximum)));if(Changed!=null)Changed(bytes);}
}

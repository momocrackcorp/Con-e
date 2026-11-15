// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

internal sealed class DiskLayout
{
    public uint Number;
    public string Name, Style;
    public string UniqueId="";
    public ulong Size;
    public bool Complete;
    public string ReadNote="";
    public readonly List<PartitionLayout> Partitions = new List<PartitionLayout>();
    public List<PartitionLayout> Segments()
    {
        var result = new List<PartitionLayout>(); ulong end = 0;
        foreach (var p in Partitions.OrderBy(p => p.Offset))
        {
            if (p.Offset < end || p.Offset > Size || p.Size > Size - p.Offset) return new List<PartitionLayout>();
            if (Complete && p.Offset > end) result.Add(new PartitionLayout { Offset=end, Size=p.Offset-end, Type="Intervalo no particionado", Gap=true });
            result.Add(p); end=p.Offset+p.Size;
        }
        if (Complete && end < Size) result.Add(new PartitionLayout { Offset=end, Size=Size-end, Type="Intervalo no particionado", Gap=true });
        return result;
    }
}
internal sealed class PartitionLayout
{
    public uint Number;
    public ulong Offset, Size;
    public string Letter="", Type="No determinado", FileSystem="No disponible", Label="";
    public ulong? Free, VolumeSize;
    public bool Gap,IsBoot,IsSystem;
    public string Title { get { return Gap ? Type : (Letter.Length>0 ? Letter + ": " : "Partición " + Number + " · ") + Type; } }
}
internal sealed partial class MainForm
{
    readonly FlowLayoutPanel diskMaps = new FlowLayoutPanel { Dock=DockStyle.Fill, AutoScroll=true, FlowDirection=FlowDirection.TopDown, WrapContents=false, Padding=new Padding(14), BackColor=CanvasColor };
    readonly TextBox partitionInfo = new TextBox { Dock=DockStyle.Bottom, Multiline=true, ReadOnly=true, Height=135, ScrollBars=ScrollBars.Vertical, BackColor=Color.White, BorderStyle=BorderStyle.FixedSingle };
    readonly List<DiskLayout> diskLayouts = new List<DiskLayout>();
    TabPage disksPage;
    readonly Button resizePartition=new Button {Text="Ajustar tamaño",AutoSize=true,Enabled=false};
    DiskLayout selectedEditDisk; PartitionLayout selectedEditPartition;
    Control BuildDiskMap()
    {
        var panel=new Panel { Dock=DockStyle.Fill };
        var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(10)};
        toolbar.Controls.Add(resizePartition);toolbar.Controls.Add(formatButton);toolbar.Controls.Add(applyPending);toolbar.Controls.Add(discardPending);
        var legend=new FlowLayoutPanel {AutoSize=true,WrapContents=true,Margin=new Padding(8,3,3,3)};
        foreach(string fs in new[]{"NTFS","FAT32","EXFAT","UNALLOCATED"})legend.Controls.Add(new Label {Text=fs=="UNALLOCATED"?"Sin asignar":fs,AutoSize=true,BackColor=FileSystemPalette.Get(fs),ForeColor=fs=="EXFAT"?Color.White:Color.Black,Padding=new Padding(4)});
        legend.Controls.Add(new Label {Text="Interior amarillo: usado · blanco: libre · rayado: no disponible",AutoSize=true,Margin=new Padding(6)});toolbar.Controls.Add(legend);
        toolbar.Controls.Add(new Label {Text="Preparar operaciones → revisar pendientes → Aplicar. Formatear borra archivos.",AutoSize=true,Margin=new Padding(8)});
        resizePartition.Click+=(s,e)=>{if(selectedEditDisk!=null&&selectedEditPartition!=null)EditPartition(selectedEditDisk,selectedEditPartition);};
        panel.Controls.Add(diskMaps);panel.Controls.Add(partitionInfo);panel.Controls.Add(BuildInlineEditor());panel.Controls.Add(BuildFormatEditor());panel.Controls.Add(toolbar);
        diskMaps.Resize+=(s,e)=>FitDiskGroups();
        return panel;
    }
    void FitDiskGroups()
    {
        int width=Math.Max(240,diskMaps.ClientSize.Width-diskMaps.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8);
        foreach(var group in diskMaps.Controls.OfType<TableLayoutPanel>()) { group.MinimumSize=new Size(width,0);group.MaximumSize=new Size(width,0);group.Width=width; }
    }
    internal static string MapSpace(ulong bytes){return Space(bytes);}
    static string Space(ulong bytes) { return bytes < 1048576 ? bytes+" bytes" : bytes < 1073741824 ? (bytes/1048576d).ToString("0.##")+" MiB" : (bytes/1073741824d).ToString("0.##")+" GiB"; }
    void SelectPartition(DiskLayout disk, PartitionLayout p)
    {
        selectedEditDisk=disk;selectedEditPartition=p;resizePartition.Enabled=CanResize(disk,p);formatButton.Enabled=CanFormat(disk,p);
        partitionInfo.Text="Disco "+disk.Number+" · "+disk.Name+Environment.NewLine+p.Title+Environment.NewLine+
            "Tamaño del bloque: "+Space(p.Size)+" · Offset: "+p.Offset.ToString(CultureInfo.InvariantCulture)+" bytes"+Environment.NewLine+
            (p.Gap ? "Intervalo sin partición en el mapa leído; puede incluir espacios de alineación o metadatos." :
            "Sistema de archivos: "+p.FileSystem+" · Etiqueta: "+p.Label+Environment.NewLine+
            "Tamaño del volumen: "+(p.VolumeSize.HasValue?Space(p.VolumeSize.Value):"No disponible")+" · Libre: "+(p.Free.HasValue?Space(p.Free.Value):"No disponible")+
            (p.VolumeSize.HasValue&&p.Free.HasValue&&p.Free.Value<=p.VolumeSize.Value?" · Ocupado: "+Space(p.VolumeSize.Value-p.Free.Value):""))+
            Environment.NewLine+(CanResize(disk,p)?"Usa Reducir / ampliar partición para modificar su tamaño.":"Esta selección no admite modificación de tamaño en Con-E.");
    }
    void RenderDiskMaps()
    {
        diskMaps.SuspendLayout();
        try
        {
            foreach(Control old in diskMaps.Controls.Cast<Control>().ToArray())old.Dispose();
            selectedEditDisk=null;selectedEditPartition=null;resizePartition.Enabled=false;formatButton.Enabled=false;
            partitionInfo.Height=Math.Max(140,TextRenderer.MeasureText("Ag",Font).Height*6+12);
            partitionInfo.Text="Selecciona un bloque del mapa o una fila para ver sus datos. Las particiones pequeñas se seleccionan también en la lista.";
            if(diskLayouts.Count==0) { diskMaps.Controls.Add(new Label { Text="No se pudo obtener el mapa físico de discos. Revisa las incidencias de lectura.", AutoSize=true, MaximumSize=new Size(500,0) });return; }
            foreach(var disk in diskLayouts)
            {
                int lineHeight=TextRenderer.MeasureText("Ag",Font).Height;
                var group=new TableLayoutPanel { ColumnCount=1,RowCount=3,Width=Math.Max(240,diskMaps.ClientSize.Width-60),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=Color.White,Margin=new Padding(0,0,0,16),Padding=new Padding(12),Font=Font };
                group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                group.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(90,lineHeight*4+12)));
                var heading=new Label { Text="Disco "+disk.Number+" · "+disk.Name+" · "+Space(disk.Size)+" · "+disk.Style+(disk.ReadNote.Length>0 ? Environment.NewLine+disk.ReadNote : ""), Dock=DockStyle.Top, AutoSize=true, MaximumSize=new Size(group.Width-30,0) };
                var segments=disk.Segments();
                var map=new PartitionStrip(disk,segments) { Dock=DockStyle.Fill, Height=Math.Max(90,lineHeight*4+12), Margin=new Padding(0,8,0,8) };
                var list=MakeList(new[]{"Partición","Sistema de archivos","Montaje","Etiqueta","Tamaño","Usado","Libre","Indicadores"});list.Name="partition-list";
                var swatches=new ImageList {ImageSize=new Size(16,16),ColorDepth=ColorDepth.Depth32Bit};var swatchSources=new List<Bitmap>();list.SmallImageList=swatches;list.Disposed+=(sender,args)=>{swatches.Dispose();foreach(var image in swatchSources)image.Dispose();};list.MultiSelect=false;list.Font=Font;list.GridLines=false;list.ShowItemToolTips=true;
                int[] widths={220,160,90,110,110,110,110,170};for(int c=0;c<widths.Length;c++)list.Columns[c].Width=(int)(widths[c]*DeviceDpi/96f);
                foreach(var p in segments.Count>0?segments:disk.Partitions)
                {
                    var queued=pendingResizes.FirstOrDefault(q=>q.Disk.UniqueId==disk.UniqueId&&q.Part.Number==p.Number&&!p.Gap);
                    string fs=p.Gap?"Sin asignar":p.FileSystem+(queued!=null&&queued.FileSystem!=null?" → "+queued.FileSystem+" (pendiente)":"");
                    var item=new ListViewItem(new[]{p.Title,fs,p.Letter.Length>0?p.Letter+":":"—",p.Label,Space(p.Size),p.VolumeSize.HasValue&&p.Free.HasValue&&p.Free<=p.VolumeSize?Space(p.VolumeSize.Value-p.Free.Value):"—",p.Free.HasValue?Space(p.Free.Value):"—",(p.IsBoot?"Arranque ":"")+(p.IsSystem?"Sistema ":"")+p.Type}) {Tag=p,ToolTipText=p.Title+" · "+p.FileSystem};var swatch=new Bitmap(16,16);using(var graphics=Graphics.FromImage(swatch))using(var brush=new SolidBrush(PartitionStrip.BorderColor(p)))graphics.FillRectangle(brush,1,1,14,14);swatchSources.Add(swatch);swatches.Images.Add(swatch);
                    item.ImageIndex=swatches.Images.Count-1;list.Items.Add(item);
                }
                int rowCount=segments.Count>0?segments.Count:disk.Partitions.Count;
                int listHeight=lineHeight*(Math.Max(3,Math.Min(8,rowCount))+2)+SystemInformation.HorizontalScrollBarHeight+12;
                group.RowStyles.Add(new RowStyle(SizeType.Absolute,listHeight));
                list.Margin=Padding.Empty;
                map.Selected+=(p)=> { SelectPartition(disk,p); foreach(ListViewItem row in list.Items)row.Selected=ReferenceEquals(row.Tag,p); };
                list.SelectedIndexChanged+=(s,e)=>{if(list.SelectedItems.Count==0)return;var p=(PartitionLayout)list.SelectedItems[0].Tag;map.Selection=p;map.Invalidate();SelectPartition(disk,p);};
                list.Resize+=(s,e)=>{int width=Math.Max(260,list.ClientSize.Width-20);list.Columns[0].Width=Math.Max(220,(int)(width*0.22));};
                group.Resize+=(s,e)=>heading.MaximumSize=new Size(Math.Max(200,group.Width-30),0);
                group.Controls.Add(heading,0,0);group.Controls.Add(map,0,1);group.Controls.Add(list,0,2);diskMaps.Controls.Add(group);
                if(list.Items.Count>0){var first=list.Items.Cast<ListViewItem>().FirstOrDefault(r=>!((PartitionLayout)r.Tag).Gap)??list.Items[0];first.Selected=true;map.Selection=(PartitionLayout)first.Tag;SelectPartition(disk,map.Selection);}
            }
        }
        finally{diskMaps.ResumeLayout(true);FitDiskGroups();}
    }
    static List<DiskLayout> ReadDiskLayouts(Snapshot data)
    {
        var result=new List<DiskLayout>();
        try
        {
            using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT Number,FriendlyName,Size,PartitionStyle,UniqueId FROM MSFT_Disk"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
            { result.Add(new DiskLayout { UniqueId=Value(row,"UniqueId"),Number=Convert.ToUInt32(row["Number"]),Name=Value(row,"FriendlyName"),Size=Convert.ToUInt64(row["Size"]),Style=Value(row,"PartitionStyle")=="2"?"GPT":Value(row,"PartitionStyle")=="1"?"MBR":"RAW / no determinado" }); }
            using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT DiskNumber,PartitionNumber,Offset,Size,DriveLetter,GptType,MbrType,IsBoot,IsSystem FROM MSFT_Partition"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
            {
                var disk=result.FirstOrDefault(d=>d.Number==Convert.ToUInt32(row["DiskNumber"]));if(disk==null)continue;
                string letter=Convert.ToString(row["DriveLetter"]).Trim('\0',' ');
                disk.Partitions.Add(new PartitionLayout { IsBoot=Convert.ToBoolean(row["IsBoot"]),IsSystem=Convert.ToBoolean(row["IsSystem"]),Number=Convert.ToUInt32(row["PartitionNumber"]),Offset=Convert.ToUInt64(row["Offset"]),Size=Convert.ToUInt64(row["Size"]),Letter=letter,Type=PartitionType(Value(row,"GptType"),Value(row,"MbrType")) });
            }
            foreach(var disk in result) { disk.Complete=true; disk.ReadNote="Selecciona un bloque o una fila. Las particiones diminutas se amplían visualmente; tamaños exactos en la tabla."; }
        }
        catch(Exception ex) {data.Warnings.Add("Mapa de discos: "+ex.Message);}
        if(result.Count==0 || result.Any(d=>!d.Complete || (d.Partitions.Count==0 && d.Style!="RAW / no determinado")))
        {
            try
            {
                using(var query=new ManagementObjectSearcher("SELECT Index,Model,Size FROM Win32_DiskDrive"))
                using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
                {
                    uint number=Convert.ToUInt32(row["Index"]);if(result.Any(d=>d.Number==number))continue;
                    result.Add(new DiskLayout {Number=number,Name=Value(row,"Model"),Size=Convert.ToUInt64(row["Size"]),Style="Estilo no disponible"});
                }
                var fallback=new Dictionary<uint,List<PartitionLayout>>();
                using(var query=new ManagementObjectSearcher("SELECT DiskIndex,Index,StartingOffset,Size,Type,DeviceID FROM Win32_DiskPartition"))
                using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
                {
                    uint number=Convert.ToUInt32(row["DiskIndex"]);List<PartitionLayout> parts;
                    if(!fallback.TryGetValue(number,out parts)){parts=new List<PartitionLayout>();fallback[number]=parts;}
                    var part=new PartitionLayout {Number=Convert.ToUInt32(row["Index"])+1,Offset=Convert.ToUInt64(row["StartingOffset"]),Size=Convert.ToUInt64(row["Size"]),Type=Value(row,"Type")};
                    try {using(var volumes=row.GetRelated("Win32_LogicalDisk"))foreach(ManagementObject volume in volumes)using(volume)part.Letter=Value(volume,"DeviceID").TrimEnd(':');}catch{}
                    parts.Add(part);
                }
                foreach(var disk in result.Where(d=>!d.Complete || (d.Partitions.Count==0 && d.Style!="RAW / no determinado")))
                {
                    List<PartitionLayout> parts;
                    if(fallback.TryGetValue(disk.Number,out parts) && parts.Count>0)
                    {
                        disk.Partitions.Clear();disk.Partitions.AddRange(parts);disk.Complete=false;
                        disk.ReadNote="Fuente alternativa Win32: intervalos sin asignar no inferidos; selecciona una partición.";
                    }
                    else disk.ReadNote="No se pudieron enumerar las particiones. Consulta las incidencias; puede requerir permisos adicionales.";
                }
            }
            catch(Exception ex){data.Warnings.Add("Lectura alternativa de particiones: "+ex.Message);}
        }
        foreach(var disk in result.Where(d=>d.Complete))foreach(var part in disk.Partitions.Where(p=>p.Letter.Length==0))
        {
            try
            {
                using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Partition WHERE DiskNumber="+disk.Number+" AND PartitionNumber="+part.Number))
                using(var rows=query.Get())foreach(ManagementObject partition in rows)using(partition)
                using(var volumes=partition.GetRelated("MSFT_Volume"))foreach(ManagementObject volume in volumes)using(volume)
                {ulong size;part.FileSystem=Value(volume,"FileSystem");part.Label=Value(volume,"FileSystemLabel");if(ulong.TryParse(Value(volume,"Size"),out size))part.VolumeSize=size;if(ulong.TryParse(Value(volume,"SizeRemaining"),out size))part.Free=size;}
            }catch{}
        }
        foreach(var disk in result)foreach(var p in disk.Partitions.Where(p=>p.Letter.Length==1&&char.IsLetter(p.Letter[0])))
        {
            try
            {
                using(var query=new ManagementObjectSearcher("SELECT Size,FreeSpace,FileSystem,VolumeName FROM Win32_LogicalDisk WHERE DeviceID='"+p.Letter+":'"))
                using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
                {ulong value;if(ulong.TryParse(Value(row,"Size"),out value))p.VolumeSize=value;if(ulong.TryParse(Value(row,"FreeSpace"),out value))p.Free=value;p.FileSystem=Value(row,"FileSystem");p.Label=Value(row,"VolumeName");}
            }catch{data.Warnings.Add("No se pudo leer el volumen "+p.Letter+":");}
        }
        return result;
    }
    static string PartitionType(string guid,string type)
    {
        switch(guid.Trim('{','}').ToUpperInvariant())
        {case "C12A7328-F81F-11D2-BA4B-00A0C93EC93B":return "EFI";case "DE94BBA4-06D1-4D40-A16A-BFD50179D6AC":return "Recuperación";case "E3C9E316-0B5C-4DB8-817D-F92DF00215AE":return "Reservada MSR";case "EBD0A0A2-B9E5-4433-87C0-68B6B72699C7":return "Datos básicos";default:return guid=="No disponible"||guid.Length==0?"Tipo MBR "+type:"Tipo GPT "+guid;}
    }
}
internal sealed class PartitionStrip : Control
{
    readonly DiskLayout disk;readonly List<PartitionLayout> segments;
    public PartitionLayout Selection;
    public event Action<PartitionLayout> Selected;
    public PartitionStrip(DiskLayout d,List<PartitionLayout> p) {disk=d;segments=p;TabStop=true;AccessibleName="Mapa de particiones del disco "+d.Number;SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    internal List<Rectangle> Blocks()
    {
        var boxes=new List<Rectangle>();if(disk.Size==0||segments.Count==0)return boxes;
        int minimum=Math.Min(7,Math.Max(0,Width/segments.Count));int remaining=Math.Max(0,Width-minimum*segments.Count);int x=0;
        for(int i=0;i<segments.Count;i++)
        {
            int width=i==segments.Count-1?Width-x:minimum+(int)(segments[i].Size/(double)disk.Size*remaining);
            boxes.Add(new Rectangle(x,5,Math.Max(0,width),Math.Max(1,Height-10)));x+=width;
        }
        return boxes;
    }
    internal static Color BorderColor(PartitionLayout p)
    {return FileSystemPalette.Get(p.Gap?"UNALLOCATED":p.Type=="Reservada MSR"?"INACTIVE":p.Type=="Recuperación"?"OTHER":p.FileSystem);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var boxes=Blocks();
        if(boxes.Count==0){TextRenderer.DrawText(e.Graphics,"Mapa no disponible: lectura parcial, solapamiento o disco sin tamaño.",Font,ClientRectangle,Color.DimGray,TextFormatFlags.WordBreak);return;}
        for(int i=0;i<segments.Count;i++)
        {
            var p=segments[i];var box=boxes[i];if(box.Width==0)continue;
            using(var fill=new SolidBrush(BorderColor(p)))e.Graphics.FillRectangle(fill,box);
            var inner=Rectangle.Inflate(box,-4,-4);
            if(inner.Width>0&&inner.Height>0)
            {
                using(var unused=new SolidBrush(p.Gap?Color.LightGray:FileSystemPalette.Unused))e.Graphics.FillRectangle(unused,inner);
                if(!p.Gap&&p.Free.HasValue&&p.VolumeSize.HasValue&&p.VolumeSize>0&&p.Free<=p.VolumeSize)
                {
                    int used=(int)((p.VolumeSize.Value-p.Free.Value)/(double)p.VolumeSize.Value*inner.Width);
                    using(var usedBrush=new SolidBrush(FileSystemPalette.Used))e.Graphics.FillRectangle(usedBrush,inner.X,inner.Y,used,inner.Height);
                }
                else if(!p.Gap)using(var unknown=new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.LightDownwardDiagonal,Color.LightGray,Color.White))e.Graphics.FillRectangle(unknown,inner);
                if(inner.Width>65)TextRenderer.DrawText(e.Graphics,(p.Gap?"Sin asignar":(p.Letter.Length>0?p.Letter+":":"Part. "+p.Number)+" · "+p.FileSystem)+Environment.NewLine+MainForm.MapSpace(p.Size),Font,inner,Color.FromArgb(25,35,45),TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
            }
            if(ReferenceEquals(p,Selection))using(var selected=new Pen(Color.FromArgb(230,120,30),3))e.Graphics.DrawRectangle(selected,Rectangle.Inflate(box,-2,-2));
            else using(var separator=new Pen(Color.White,2))e.Graphics.DrawRectangle(separator,box);
        }
        if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();var boxes=Blocks();for(int i=0;i<boxes.Count;i++)if(boxes[i].Contains(e.Location)){Choose(segments[i]);break;}}
    protected override bool IsInputKey(Keys keyData){return keyData==Keys.Left||keyData==Keys.Right||base.IsInputKey(keyData);}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(segments.Count==0)return;int index=segments.IndexOf(Selection);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){index=Math.Max(0,Math.Min(segments.Count-1,index+(e.KeyCode==Keys.Left?-1:1)));Choose(segments[index]);e.Handled=true;}}
    void Choose(PartitionLayout p){Selection=p;Invalidate();if(Selected!=null)Selected(p);}
}

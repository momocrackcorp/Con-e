using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UIModernization;

internal sealed class StorageView : UserControl
{
    readonly ComboBox disks=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=360};
    readonly FlowLayoutPanel toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(10),BackColor=ModernTheme.Surface};
    readonly FlowLayoutPanel content=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14),BackColor=ModernTheme.Canvas};
    readonly ModernCardPanel header=Card(),volumeCard=Card();
    readonly FlowLayoutPanel volumeButtons=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};
    readonly FlowLayoutPanel analysisActions=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=new Padding(0,0,0,10)};
    readonly FlowLayoutPanel row=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};
    readonly FlowLayoutPanel left=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink},stats=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
    readonly Label heading=Label("Disco físico",true),diskType=Label(""),title=Label("Volumen",true),capacity=Label(""),health=Label("Estado: No disponible"),temperature=Label("Temperatura: No disponible"),lifetime=Label("Horas encendido: No disponible"),totals=Label("Datos acumulados: No disponibles"),manufacture=Label("Fabricación: No disponible"),io=Label("Lectura / escritura: No disponible"),notice=Label(""),scanStatus=Label("Pulsa Analizar carpetas para desglosar el contenido."),scanDetail=Label("Sólo carpetas de la raíz, sumando sus subcarpetas. No se muestra ningún archivo individual.");
    readonly DiskMiniature miniature=new DiskMiniature {Width=64,Height=48};
    readonly StorageRing ring=new StorageRing {Height=160};readonly FolderChart folders=new FolderChart();
    readonly Button analyze=Button("Analizar carpetas"),cancel=Button("Cancelar análisis");
    readonly Dictionary<string,StorageScan> cache=new Dictionary<string,StorageScan>();
    PhysicalDiskInfo disk;PhysicalPartitionInfo partition;StorageDeviceInfo device;
    CancellationTokenSource scanCancellation,monitorCancellation;Task scanTask,monitorTask;int version;bool setting;readonly bool live;
    readonly SemaphoreSlim ioGate=new SemaphoreSlim(1,1);
    internal event Action SensorsRequested;
    internal event Action<PhysicalDiskInfo> DiskSelected;
    internal string SelectedDiskKey {get{return disk?.Key;}}
    internal StorageView(bool enableMonitor=true)
    {
        live=enableMonitor;Dock=DockStyle.Fill;BackColor=ModernTheme.Canvas;Font=new Font("Segoe UI",10);ForeColor=ModernTheme.Text;
        var refresh=Button("Actualizar capacidad");var sensors=Button("Sensores / SMART…");sensors.Click+=(s,e)=>SensorsRequested?.Invoke();toolbar.Controls.AddRange(new Control[]{disks,refresh,sensors});cancel.Enabled=false;
        var headingRow=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};headingRow.Controls.AddRange(new Control[]{miniature,heading});header.Selected=true;header.Controls.AddRange(new Control[]{headingRow,diskType,volumeButtons});
        left.Controls.AddRange(new Control[]{ring,Label("Contenido del volumen",true),folders,scanStatus,scanDetail});
        stats.Controls.AddRange(new Control[]{capacity,health,temperature,lifetime,totals,manufacture,io,notice});row.Controls.AddRange(new Control[]{left,stats});analysisActions.Controls.AddRange(new Control[]{analyze,cancel});volumeCard.Controls.AddRange(new Control[]{title,analysisActions,row});content.Controls.AddRange(new Control[]{header,volumeCard});Controls.Add(content);Controls.Add(toolbar);
        disks.DisplayMember="";disks.SelectedIndexChanged+=(s,e)=>{if(!setting)ChooseDisk(disks.SelectedItem as PhysicalDiskInfo);};refresh.Click+=(s,e)=>RefreshCapacity();analyze.Click+=(s,e)=>{if(scanTask==null||scanTask.IsCompleted)scanTask=AnalyzeAsync();};cancel.Click+=(s,e)=>scanCancellation?.Cancel();
        content.Resize+=(s,e)=>Fit();Load+=(s,e)=>StartMonitor();VisibleChanged+=(s,e)=>{if(Visible)StartMonitor();else StopMonitor();};Disposed+=(s,e)=>{scanCancellation?.Cancel();StopMonitor();};
    }
    static ModernCardPanel Card(){return new ModernCardPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12),Margin=new Padding(0,0,0,12)};}
    static Label Label(string text,bool prominent=false){return new Label {Text=text,UseMnemonic=false,AutoSize=true,ForeColor=prominent?ModernTheme.Text:ModernTheme.Muted,Font=new Font("Segoe UI",prominent?12:10,prominent?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,9)};}
    static Button Button(string text){var b=new Button {Text=text,AutoSize=true,FlatStyle=FlatStyle.Flat,Padding=new Padding(5),Margin=new Padding(4),BackColor=ModernTheme.Raised,ForeColor=ModernTheme.Accent};b.FlatAppearance.BorderColor=ModernTheme.Border;return b;}
    void Fit()
    {
        int w=Math.Max(260,content.ClientSize.Width-content.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8),inner=w-24;
        foreach(var c in new[]{header,volumeCard}){c.MinimumSize=c.MaximumSize=new Size(w,0);c.Width=w;}
        analysisActions.MinimumSize=analysisActions.MaximumSize=new Size(inner,0);
        volumeButtons.MinimumSize=volumeButtons.MaximumSize=new Size(inner,0);row.MinimumSize=row.MaximumSize=new Size(inner,0);
        int lw=inner>=690?(int)(inner*.55):inner,rw=inner>=690?inner-lw-15:inner;left.MinimumSize=left.MaximumSize=new Size(lw,0);stats.MinimumSize=stats.MaximumSize=new Size(rw,0);ring.Width=folders.Width=Math.Max(220,lw-8);
        foreach(var l in new[]{heading,diskType,title,capacity,health,temperature,lifetime,totals,manufacture,io,notice,scanStatus,scanDetail})l.MaximumSize=new Size(Math.Max(150,l.Parent==stats?rw-8:l.Parent==left?lw-8:inner-8),0);
        disks.Width=Math.Min(360,Math.Max(200,ClientSize.Width-40));
    }
    internal void SetDisks(IEnumerable<PhysicalDiskInfo> data,string key=null)
    {
        string previous=key??disk?.Key;uint? part=partition?.Number;setting=true;try{disks.Items.Clear();foreach(var d in data)disks.Items.Add(d);int i=disks.Items.Cast<PhysicalDiskInfo>().ToList().FindIndex(d=>d.Key==previous);disks.SelectedIndex=i>=0?i:disks.Items.Count>0?0:-1;}finally{setting=false;}
        ChooseDisk(disks.SelectedItem as PhysicalDiskInfo,part);Fit();
    }
    void ChooseDisk(PhysicalDiskInfo d,uint? chosen=null)
    {
        version++;scanCancellation?.Cancel();StopMonitor();disk=d;partition=null;device=null;foreach(Control old in volumeButtons.Controls.Cast<Control>().ToArray())old.Dispose();
        if(d==null){heading.Text="No se pudieron identificar discos";diskType.Text="Actualiza el inventario desde Mi equipo.";volumeCard.Visible=false;analyze.Enabled=false;return;}
        DiskSelected?.Invoke(d);volumeCard.Visible=true;heading.Text="Disco "+d.Number+" · "+d.Model;diskType.Text=d.Kind+" · "+d.Bus+" · "+StorageFormat.Size(d.Size)+" · "+d.Style+Environment.NewLine+d.Form+" · "+d.MediumSource+" · Miniatura ilustrativa";miniature.Kind=d.Art;miniature.Invalidate();
        foreach(var p in d.Partitions.OrderBy(p=>p.Offset)){var item=p;var b=Button(p.Title+" · "+p.FileSystem);b.Tag=p;b.MaximumSize=new Size(Math.Max(220,ClientSize.Width-80),0);b.Click+=(s,e)=>ChoosePartition(item);volumeButtons.Controls.Add(b);}
        ChoosePartition(d.Partitions.FirstOrDefault(p=>p.Number==chosen)??d.Partitions.FirstOrDefault(p=>p.Letter.Length>0)??d.Partitions.FirstOrDefault());
    }
    void ChoosePartition(PhysicalPartitionInfo p)
    {
        version++;scanCancellation?.Cancel();cancel.Enabled=false;StopMonitor();partition=p;device=null;folders.SetSlices(new List<StorageSlice>());scanStatus.Text="Pulsa Analizar carpetas para desglosar el contenido.";
        foreach(Button b in volumeButtons.Controls.OfType<Button>()){bool active=ReferenceEquals(b.Tag,p);b.FlatAppearance.BorderColor=active?ModernTheme.Accent:Color.FromArgb(45,52,63);b.FlatAppearance.BorderSize=active?2:1;}
        volumeCard.Selected=p!=null;
        if(p==null){title.Text="Sin particiones enumeradas";capacity.Text=disk?.Complete==true?"Disco sin particiones / sin asignar":"Lectura parcial de particiones";ring.SetSpace(0,0);analyze.Enabled=false;ClearTelemetry();Fit();return;}
        title.Text=p.Title+" · "+p.FileSystem;SetCapacity();ClearTelemetry();ApplyAdvanced();analyze.Enabled=p.Letter.Length==1;notice.Text=p.Letter.Length==1?"SMART y horas corresponden al disco físico.":"Partición sin letra: se conservan sus datos; el análisis de carpetas no está disponible.";
        StorageScan previous;if(cache.TryGetValue(CacheKey(),out previous))ShowResult(previous,true);
        Fit();StartMonitor();
    }
    string CacheKey(){return disk.Key+"|"+partition.Number+"|"+partition.Offset+"|"+partition.Letter;}
    void SetCapacity(){if(partition==null)return;ulong total=partition.VolumeSize??partition.Size;bool known=partition.Free.HasValue&&partition.Free<=total;ring.SetSpace(known?total:0,known?partition.Free.Value:0);capacity.Text="Capacidad: "+StorageFormat.Size(total)+Environment.NewLine+(known?"Usado: "+StorageFormat.Size(total-partition.Free.Value)+" · Disponible: "+StorageFormat.Size(partition.Free.Value):"Usado / disponible: No informado");}
    void ClearTelemetry(){health.Text="Estado: No disponible";temperature.Text="Temperatura: No disponible";lifetime.Text="Horas encendido: No disponible";totals.Text="Lectura / escritura acumuladas: No disponibles";manufacture.Text="Fabricación: No disponible";io.Text="Lectura / escritura actual: No disponible";}
    async void RefreshCapacity()
    {
        var p=partition;if(p==null||p.Letter.Length!=1||!live)return;int selected=version;
        try{var d=await Task.Run(()=>{var drive=new DriveInfo(p.Letter+@":\");return Tuple.Create((ulong)drive.TotalSize,(ulong)drive.TotalFreeSpace,drive.DriveFormat);});if(IsDisposed||version!=selected)return;p.VolumeSize=d.Item1;p.Free=d.Item2;p.FileSystem=d.Item3;SetCapacity();scanStatus.Text="Capacidad actualizada. Actualiza el análisis para renovar la distribución.";}
        catch{if(!IsDisposed&&version==selected)notice.Text="La unidad no está lista, está bloqueada o se desconectó.";}
    }
    async Task AnalyzeAsync()
    {
        var p=partition;if(p==null||p.Letter.Length!=1)return;int selected=version;string key=CacheKey();var source=new CancellationTokenSource();scanCancellation=source;analyze.Enabled=false;cancel.Enabled=true;scanStatus.Text="Analizando carpetas…";
        var progress=new Progress<StorageScanProgress>(sample=>{if(!IsDisposed&&selected==version&&!source.IsCancellationRequested)scanStatus.Text="Analizando "+sample.Group+" · "+sample.Skipped+" rutas omitidas";});
        try{var result=await Task.Run(()=>StorageScanner.Scan(p.Letter+@":\",source.Token,progress),source.Token);if(!IsDisposed&&selected==version&&!source.IsCancellationRequested){cache[key]=result;ShowResult(result);}}
        catch(OperationCanceledException){if(!IsDisposed&&selected==version)scanStatus.Text="Análisis cancelado · puedes volver a iniciarlo.";}
        catch(Exception ex){if(!IsDisposed&&selected==version)scanStatus.Text="Análisis no completado: "+ex.Message;}
        finally{if(!IsDisposed&&selected==version){analyze.Enabled=true;cancel.Enabled=false;}if(ReferenceEquals(scanCancellation,source))scanCancellation=null;source.Dispose();}
    }
    internal void ShowResult(StorageScan result,bool cached=false)
    {
        if(partition==null)return;
        if(!cached)cache[CacheKey()]=result;
        if(result.Total>0&&result.Free>=0&&result.Free<=result.Total){partition.VolumeSize=(ulong)result.Total;partition.Free=(ulong)result.Free;SetCapacity();}
        long used=Math.Max(0,result.Total-result.Free),allocated=result.Groups.Sum(g=>g.Bytes);bool whole=result.Total>0&&allocated<=used;var slices=new List<StorageSlice>();
        foreach(var g in result.Groups.OrderByDescending(g=>g.Bytes))slices.Add(new StorageSlice {Name=g.Name,Bytes=g.Bytes,Color=StorageFormat.Palette[StableColor(g.Name)]});
        if(whole){if(used>allocated)slices.Add(new StorageSlice {Name="Sistema / sin analizar",Bytes=used-allocated,Color=StorageFormat.Other});if(result.Free>0)slices.Add(new StorageSlice {Name="Espacio libre",Bytes=result.Free,Color=StorageFormat.Free});}
        folders.SetSlices(slices);scanStatus.Text=(cached?"En caché de "+result.Captured.ToString("HH:mm:ss")+" · ":"")+result.Elapsed.TotalSeconds.ToString("0.0")+" s · "+(result.Skipped>0?"Lectura parcial":"Lectura terminada");scanDetail.Text=(whole?"Carpetas en raíz, sumando sus subcarpetas. ":"Distribución de lo leído; la capacidad cambió durante la consulta. ")+result.Skipped+" rutas omitidas. No se listan archivos ni se siguen enlaces de directorio.";Fit();
    }
    static int StableColor(string text){uint h=2166136261;foreach(char c in text)h=(h^c)*16777619;return (int)(h%(uint)StorageFormat.Palette.Length);}
    internal void SelectVolume(string letter){var p=disk?.Partitions.FirstOrDefault(v=>v.Letter==letter);if(p!=null)ChoosePartition(p);}
    internal void VerifyAnalysisLayout()
    {
        Fit();folders.VerifyLayout();
        if(analyze.Parent!=analysisActions||analysisActions.Top<0||analysisActions.Bottom>row.Top)throw new InvalidOperationException("Acciones de análisis fuera del volumen");
    }
    internal string DiskSelectorText {get{return disks.Text;}}
    internal int VolumeCount {get{return disk?.Partitions.Count??0;}}
    internal string CurrentVolumeLetter {get{return partition?.Letter;}}
    internal long ChartTotal {get{return folders.Slices.Sum(s=>s.Bytes);}}
    internal void FocusAnalysis(){content.ScrollControlIntoView(volumeCard);}
    internal void FocusCapacity(){content.AutoScrollPosition=Point.Empty;}
    internal void ApplyAdvanced(DiskReading fixture=null)
    {
        var frame=SensorClient.Latest;var smart=fixture??(frame==null||disk==null||(DateTime.Now-frame.Time).TotalSeconds>12||string.IsNullOrWhiteSpace(disk.Serial)?null:frame.Disks.FirstOrDefault(d=>d.Number==disk.Number&&string.Equals((d.Serial??"").Trim(),disk.Serial.Trim(),StringComparison.OrdinalIgnoreCase)));
        string state=smart==null||smart.Status=="Unknown"?device?.Health:smart.Status=="Good"?"Correcto según SMART":smart.Status=="Caution"?"Advertencia SMART":"Estado no saludable (SMART)";
        health.Text="Estado: "+(state??"No disponible")+(smart?.Life.HasValue==true?Environment.NewLine+"Vida restante estimada: "+smart.Life.Value.ToString("0")+" %":"");health.ForeColor=(state??"").StartsWith("Correcto")?StorageFormat.Used:(state??"").StartsWith("Advertencia")?Color.DarkOrange:ModernTheme.Muted;
        var temp=smart?.Temperature??device?.Temperature;temperature.Text="Temperatura: "+(temp.HasValue?temp+" °C":"No disponible");var hours=smart?.Hours??device?.PowerOnHours;lifetime.Text="Horas encendido: "+(hours.HasValue?hours.Value.ToString("N0")+" h":"No disponible");
        totals.Text="Lectura acumulada: "+(smart?.ReadGB.HasValue==true?smart.ReadGB.Value.ToString("N0")+" GB":"No disponible")+Environment.NewLine+"Escritura acumulada: "+(smart?.WrittenGB.HasValue==true?smart.WrittenGB.Value.ToString("N0")+" GB":"No disponible");manufacture.Text="Fabricación: "+(device?.ManufactureDate??"No disponible");
    }
    void StartMonitor(){if(!live||!Visible||!IsHandleCreated||IsDisposed||partition==null||monitorCancellation!=null)return;monitorCancellation=new CancellationTokenSource();monitorTask=MonitorAsync(partition,version,monitorCancellation.Token);}
    void StopMonitor(){var old=monitorCancellation;monitorCancellation=null;if(old==null)return;old.Cancel();if(monitorTask!=null)monitorTask.ContinueWith(t=>old.Dispose(),TaskScheduler.Default);else old.Dispose();}
    async Task MonitorAsync(PhysicalPartitionInfo p,int selected,CancellationToken token)
    {
        try
        {
            if(p.Letter.Length==1){await ioGate.WaitAsync(token);try{var d=await Task.Run(()=>StorageTelemetry.Device(p.Letter+":"),token);if(!token.IsCancellationRequested&&!IsDisposed&&version==selected)device=d;}finally{ioGate.Release();}}
            while(!token.IsCancellationRequested)
            {
                StorageIo sample=null;if(p.Letter.Length==1){await ioGate.WaitAsync(token);try{sample=await Task.Run(()=>StorageTelemetry.ReadIo(p.Letter+":"),token);}finally{ioGate.Release();}}
                if(token.IsCancellationRequested||IsDisposed||version!=selected)return;ApplyAdvanced();if(sample!=null)io.Text="Lectura: "+(sample.Read.HasValue?StorageFormat.Size(sample.Read.Value)+"/s":"No disponible")+Environment.NewLine+"Escritura: "+(sample.Write.HasValue?StorageFormat.Size(sample.Write.Value)+"/s":"No disponible");await Task.Delay(1500,token);
            }
        }catch(OperationCanceledException){}catch{if(!IsDisposed&&version==selected)notice.Text="Telemetría no disponible para este dispositivo.";}
    }
}

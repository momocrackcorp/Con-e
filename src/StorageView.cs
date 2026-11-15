using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UIModernization;

internal sealed class StorageSummary : FlowLayoutPanel
{
    readonly ComboBox drives=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList};
    readonly StorageRing ring=new StorageRing();
    readonly Label total=new Label {AutoSize=true},used=new Label {AutoSize=true},free=new Label {AutoSize=true};
    internal StorageSummary(IEnumerable<VolumeInfo> volumes,Action<string> open)
    {
        FlowDirection=FlowDirection.TopDown;WrapContents=false;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Margin=Padding.Empty;BackColor=ModernTheme.Surface;
        var button=new Button {Text="Ver uso y analizar carpetas",AutoSize=true,FlatStyle=FlatStyle.Flat,Padding=new Padding(6),ForeColor=ModernTheme.Accent,BackColor=ModernTheme.Raised};button.FlatAppearance.BorderColor=ModernTheme.Border;
        foreach(var v in volumes)drives.Items.Add(v);drives.SelectedIndexChanged+=(s,e)=>UpdateSpace();button.Click+=(s,e)=>{var v=drives.SelectedItem as VolumeInfo;if(v!=null)open(v.Name);};
        foreach(var label in new[]{total,used,free}){label.ForeColor=ModernTheme.Muted;label.Margin=new Padding(0,0,0,8);}
        Controls.AddRange(new Control[]{drives,ring,total,used,free,button});Resize+=(s,e)=>Fit();if(drives.Items.Count>0)drives.SelectedIndex=0;
    }
    void Fit(){int w=Math.Max(100,ClientSize.Width-Padding.Horizontal);drives.Width=w;ring.Width=w;foreach(var l in new[]{total,used,free})l.MaximumSize=new Size(w,0);}
    void UpdateSpace(){var v=drives.SelectedItem as VolumeInfo;if(v==null)return;ring.SetSpace(v.TotalGiB,v.FreeGiB);total.Text="Total: "+v.TotalGiB.ToString("0.0")+" GiB · "+v.FileSystem;used.Text="● Usado: "+(v.TotalGiB-v.FreeGiB).ToString("0.0")+" GiB";used.ForeColor=StorageFormat.Used;free.Text="Libre: "+v.FreeGiB.ToString("0.0")+" GiB";Fit();}
}
internal sealed class StorageView : UserControl
{
    readonly ComboBox selector=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=230};
    readonly Button analyze=Button("Analizar carpetas"),cancel=Button("Cancelar análisis"),refresh=Button("Actualizar capacidad");
    readonly FlowLayoutPanel content=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14),BackColor=ModernTheme.Canvas};
    readonly ModernCardPanel overview=Card(),analysis=Card();readonly StorageRing ring=new StorageRing {Width=230,Height=220};
    readonly FlowLayoutPanel stats=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
    readonly FlowLayoutPanel overviewRow=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};
    readonly Label title=Label("Almacenamiento",true),capacity=Label("Sin lectura"),health=Label("Estado: no disponible"),temperature=Label("Temperatura: no disponible"),io=Label("Lectura / escritura: no disponible"),notice=Label("El análisis de carpetas sólo comienza cuando lo solicitas."),scanStatus=Label("Sin analizar"),scanDetail=Label("Estimación de espacio asignado. Omite enlaces de directorio y elementos sin acceso.");
    readonly FolderChart pie=new FolderChart();
    CancellationTokenSource scanCancellation,monitorCancellation;Task scanTask,monitorTask;int selectionVersion;readonly bool monitor;
    readonly SemaphoreSlim ioGate=new SemaphoreSlim(1,1);
    internal StorageView(bool enableMonitor=true)
    {
        monitor=enableMonitor;Dock=DockStyle.Fill;BackColor=ModernTheme.Canvas;ForeColor=ModernTheme.Text;Font=new Font("Segoe UI",10);
        var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(12),BackColor=ModernTheme.Surface};toolbar.Controls.AddRange(new Control[]{selector,refresh,analyze,cancel});cancel.Enabled=false;
        stats.Controls.AddRange(new Control[]{capacity,health,temperature,io,notice});overviewRow.Controls.AddRange(new Control[]{ring,stats});overview.Controls.AddRange(new Control[]{title,overviewRow});analysis.Controls.AddRange(new Control[]{Label("¿Qué ocupa espacio?",true),scanStatus,pie,scanDetail});content.Controls.Add(overview);content.Controls.Add(analysis);Controls.Add(content);Controls.Add(toolbar);
        selector.SelectedIndexChanged+=(s,e)=>SelectionChanged();refresh.Click+=(s,e)=>{scanCancellation?.Cancel();pie.SetSlices(new List<StorageSlice>());scanStatus.Text="Sin analizar";RefreshCapacity();};analyze.Click+=(s,e)=>{if(scanTask==null||scanTask.IsCompleted)scanTask=AnalyzeAsync();};cancel.Click+=(s,e)=>scanCancellation?.Cancel();content.Resize+=(s,e)=>FitCards();
        Load+=(s,e)=>StartMonitor();VisibleChanged+=(s,e)=>{if(Visible){StartMonitor();}else StopMonitor();};Disposed+=(s,e)=>{scanCancellation?.Cancel();StopMonitor();};
    }
    static ModernCardPanel Card(){return new ModernCardPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18),Margin=new Padding(0,0,0,14)};}
    static Label Label(string text,bool prominent=false){return new Label {Text=text,UseMnemonic=false,AutoSize=true,ForeColor=prominent?ModernTheme.Text:ModernTheme.Muted,Font=new Font("Segoe UI",prominent?14:10,prominent?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,12)};}
    static Button Button(string text){var b=new Button {Text=text,AutoSize=true,FlatStyle=FlatStyle.Flat,Padding=new Padding(5),Margin=new Padding(4),BackColor=ModernTheme.Raised,ForeColor=ModernTheme.Accent};b.FlatAppearance.BorderColor=ModernTheme.Border;return b;}
    internal void SetVolumes(IEnumerable<VolumeInfo> volumes,string chosen=null)
    {
        string previous=chosen??(selector.SelectedItem as VolumeInfo)?.Name;selector.Items.Clear();foreach(var v in volumes)selector.Items.Add(v);selector.SelectedIndex=selector.Items.Cast<VolumeInfo>().ToList().FindIndex(v=>v.Name==previous);if(selector.SelectedIndex<0&&selector.Items.Count>0)selector.SelectedIndex=0;
        if(selector.Items.Count==0){StopMonitor();analyze.Enabled=refresh.Enabled=false;title.Text="No hay unidades legibles disponibles";}FitCards();
    }
    void FitCards()
    {
        int width=Math.Max(260,content.ClientSize.Width-content.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8),inner=Math.Max(220,width-36);overview.MinimumSize=analysis.MinimumSize=new Size(width,0);overview.MaximumSize=analysis.MaximumSize=new Size(width,0);overview.Width=analysis.Width=width;overviewRow.MinimumSize=overviewRow.MaximumSize=new Size(inner,0);overviewRow.Width=inner;stats.MinimumSize=stats.MaximumSize=new Size(Math.Max(200,inner>=540?inner-250:inner),0);pie.Width=inner;
        foreach(var l in new[]{title,capacity,health,temperature,io,notice,scanStatus,scanDetail})l.MaximumSize=new Size(Math.Max(180,l.Parent==stats?stats.Width:inner),0);
    }
    void SelectionChanged()
    {
        selectionVersion++;scanCancellation?.Cancel();cancel.Enabled=false;StopMonitor();pie.SetSlices(new List<StorageSlice>());scanStatus.Text="Sin analizar";scanDetail.Text="Estimación de espacio asignado. Omite enlaces de directorio y elementos sin acceso.";
        health.Text="Estado: no disponible";health.ForeColor=ModernTheme.Muted;temperature.Text="Temperatura: no disponible";io.Text="Lectura / escritura: no disponible";notice.Text="La salud es la informada por Windows; no representa un porcentaje de vida útil.";RefreshCapacity();StartMonitor();
    }
    async void RefreshCapacity()
    {
        var v=selector.SelectedItem as VolumeInfo;if(v==null)return;int version=selectionVersion;SetCapacity(v);if(!monitor)return;
        try
        {
            var data=await Task.Run(()=>{var d=new DriveInfo(StorageTelemetry.Letter(v.Name)+@"\");if(!d.IsReady)throw new IOException();return Tuple.Create(d.TotalSize/1073741824d,d.TotalFreeSpace/1073741824d,d.DriveFormat,d.VolumeLabel);});
            if(IsDisposed||version!=selectionVersion)return;v.TotalGiB=data.Item1;v.FreeGiB=data.Item2;v.FileSystem=data.Item3;v.Label=data.Item4;SetCapacity(v);analyze.Enabled=true;refresh.Enabled=true;
        }
        catch{if(!IsDisposed&&version==selectionVersion){SetCapacity(v);notice.Text="No se pudo actualizar la capacidad. Se muestra la lectura anterior; comprueba si la unidad está bloqueada o desconectada.";analyze.Enabled=false;}}
    }
    internal void SetCapacity(VolumeInfo v){title.Text=v.ToString()+" · "+v.FileSystem;ring.SetSpace(v.TotalGiB,v.FreeGiB);capacity.Text="Total: "+v.TotalGiB.ToString("0.0")+" GiB"+Environment.NewLine+"Usado: "+(v.TotalGiB-v.FreeGiB).ToString("0.0")+" GiB · Libre: "+v.FreeGiB.ToString("0.0")+" GiB";}
    async Task AnalyzeAsync()
    {
        var v=selector.SelectedItem as VolumeInfo;if(v==null)return;int version=selectionVersion;var source=new CancellationTokenSource();scanCancellation=source;analyze.Enabled=false;cancel.Enabled=true;scanStatus.Text="Analizando… puedes cancelar sin modificar los archivos.";
        var progress=new Progress<StorageScanProgress>(p=>{if(!IsDisposed&&version==selectionVersion&&!source.IsCancellationRequested)scanStatus.Text="Analizando "+p.Group+" · "+p.Files.ToString("N0")+" archivos · "+p.Skipped+" omitidos";});
        try{var result=await Task.Run(()=>StorageScanner.Scan(StorageTelemetry.Letter(v.Name)+@"\",source.Token,progress),source.Token);if(!IsDisposed&&version==selectionVersion&&!source.IsCancellationRequested)ShowResult(v,result);}
        catch(OperationCanceledException){if(!IsDisposed&&version==selectionVersion)scanStatus.Text="Análisis cancelado. No se modificó ningún archivo.";}
        catch(Exception ex){if(!IsDisposed&&version==selectionVersion)scanStatus.Text="No se completó el análisis: "+ex.Message;}
        finally{if(!IsDisposed&&version==selectionVersion){analyze.Enabled=true;cancel.Enabled=false;}if(ReferenceEquals(scanCancellation,source))scanCancellation=null;source.Dispose();}
    }
    internal void ShowResult(VolumeInfo v,StorageScan result)
    {
        if(result.Total>0&&result.Free>=0&&result.Free<=result.Total){v.TotalGiB=result.Total/1073741824d;v.FreeGiB=result.Free/1073741824d;SetCapacity(v);}
        var slices=new List<StorageSlice>();long total=result.Total,used=Math.Max(0,total-result.Free);long allocated=result.Groups.Sum(g=>g.Bytes);bool capacityBasis=total>0&&allocated<=used;
        var ordered=result.Groups.OrderByDescending(g=>g.Bytes).ToArray();int i=0;foreach(var group in ordered.Take(6))slices.Add(new StorageSlice {Name=group.Name,Bytes=group.Bytes,Color=StorageFormat.Palette[i++%StorageFormat.Palette.Length]});long rest=ordered.Skip(6).Sum(g=>g.Bytes);if(rest>0)slices.Add(new StorageSlice {Name="Otras carpetas",Bytes=rest,Color=Color.FromArgb(170,133,103)});
        if(capacityBasis){if(used>allocated)slices.Add(new StorageSlice {Name="Sistema / sin analizar",Bytes=used-allocated,Color=StorageFormat.Other});if(result.Free>0)slices.Add(new StorageSlice {Name="Espacio libre",Bytes=result.Free,Color=StorageFormat.Free});}
        pie.SetSlices(slices);scanStatus.Text=result.Files.ToString("N0")+" archivos · "+result.Elapsed.TotalSeconds.ToString("0.0")+" s · "+(result.Skipped>0?"Lectura parcial":"Lectura terminada");
        scanDetail.Text=(capacityBasis?"El gris incluye metadatos, reservas y espacio que no pudo atribuirse a carpetas.":"El volumen cambió durante la lectura o no permite comparar su capacidad: distribución de archivos leídos.")+Environment.NewLine+result.Skipped+" elementos/rutas omitidos · "+result.LinksSkipped+" enlaces duros duplicados no se volvieron a contar. No se siguen enlaces de directorio ni se descargan archivos de nube.";FitCards();content.PerformLayout();content.ScrollControlIntoView(analysis);
    }
    internal long ChartTotal {get{return pie.Slices.Sum(s=>s.Bytes);}}
    internal void FocusAnalysis(){content.ScrollControlIntoView(analysis);}
    internal void FocusCapacity(){content.AutoScrollPosition=Point.Empty;}
    void StartMonitor()
    {
        var v=selector.SelectedItem as VolumeInfo;if(!monitor||!Visible||!IsHandleCreated||v==null||IsDisposed||monitorCancellation!=null)return;monitorCancellation=new CancellationTokenSource();monitorTask=MonitorAsync(v.Name,selectionVersion,monitorCancellation.Token);
    }
    void StopMonitor(){var source=monitorCancellation;if(source==null)return;monitorCancellation=null;source.Cancel();if(monitorTask!=null)monitorTask.ContinueWith(t=>source.Dispose(),TaskScheduler.Default);else source.Dispose();}
    async Task MonitorAsync(string name,int version,CancellationToken token)
    {
        try
        {
            await ioGate.WaitAsync(token);StorageDeviceInfo device;
            try{device=await Task.Run(()=>StorageTelemetry.Device(name),token);}finally{ioGate.Release();}
            if(token.IsCancellationRequested||IsDisposed||version!=selectionVersion)return;notice.Text=device.Model+" · "+device.Type;health.Text="Estado: "+device.Health;health.ForeColor=device.Health.StartsWith("Correcto")?StorageFormat.Used:device.Health.StartsWith("Advertencia")?Color.DarkOrange:device.Health.StartsWith("Estado no")?Color.Firebrick:ModernTheme.Muted;temperature.Text="Temperatura: "+(device.Temperature.HasValue?device.Temperature+" °C":"no disponible");
            while(!token.IsCancellationRequested)
            {
                await ioGate.WaitAsync(token);StorageIo sample;try{sample=await Task.Run(()=>StorageTelemetry.ReadIo(name),token);}finally{ioGate.Release();}
                if(token.IsCancellationRequested||IsDisposed||version!=selectionVersion)return;
                io.Text="Lectura: "+(sample.Read.HasValue?StorageFormat.Size(sample.Read.Value)+"/s":"no disponible")+Environment.NewLine+"Escritura: "+(sample.Write.HasValue?StorageFormat.Size(sample.Write.Value)+"/s":"no disponible");await Task.Delay(1500,token);
            }
        }catch(OperationCanceledException){}catch{if(!IsDisposed&&version==selectionVersion)notice.Text="No se pudo leer telemetría de este dispositivo.";}
    }
}

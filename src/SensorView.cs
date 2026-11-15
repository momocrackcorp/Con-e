using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using UIModernization;

internal sealed class SensorView : UserControl
{
    readonly SensorClient client=new SensorClient();
    readonly Label state=new Label {AutoSize=true,MaximumSize=new Size(900,0),Margin=new Padding(8),ForeColor=ModernTheme.Muted};
    readonly FlowLayoutPanel cards=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(10),BackColor=ModernTheme.Canvas};
    readonly Label[] values=new Label[4];
    readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=ModernTheme.Surface,BorderStyle=BorderStyle.None,SelectionMode=DataGridViewSelectionMode.FullRowSelect,EnableHeadersVisualStyles=false};
    internal SensorView()
    {
        Dock=DockStyle.Fill;BackColor=ModernTheme.Canvas;
        var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
        Add(toolbar,"Leer sensores",()=>Start(false));Add(toolbar,"Habilitar lectura con administrador",()=>Start(true));Add(toolbar,"Detener",()=>{client.Stop();Render(new SensorFrame {Error="Lectura detenida. Los mínimos y máximos se reinician al iniciar otra sesión."});});
        Add(toolbar,"Instalar controlador PawnIO…",InstallPawnIO);
        var messages=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};messages.Controls.Add(state);
        string[] names={"CPU","GPU","Placa / chasis","Discos"};
        for(int i=0;i<names.Length;i++){
            var c=new ModernCardPanel {Width=222,Height=105,Padding=new Padding(14),Margin=new Padding(6),FlowDirection=FlowDirection.TopDown,WrapContents=false};
            c.Controls.Add(new Label {Text=names[i],AutoSize=true,ForeColor=ModernTheme.Accent});values[i]=new Label {Text="No disponible",AutoSize=true,MaximumSize=new Size(190,0),Font=new Font("Segoe UI",13,FontStyle.Bold),ForeColor=ModernTheme.Text,Margin=new Padding(0,10,0,0)};c.Controls.Add(values[i]);cards.Controls.Add(c);
        }
        grid.ColumnHeadersDefaultCellStyle.BackColor=ModernTheme.Raised;grid.ColumnHeadersDefaultCellStyle.ForeColor=ModernTheme.Text;grid.DefaultCellStyle.BackColor=ModernTheme.Surface;grid.DefaultCellStyle.ForeColor=ModernTheme.Text;grid.DefaultCellStyle.SelectionBackColor=ModernTheme.Raised;grid.DefaultCellStyle.SelectionForeColor=ModernTheme.Text;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        foreach(string column in new[]{"Componente","Sensor / nombre del fabricante","Actual","Mínimo","Máximo"})grid.Columns.Add(column,column);
        grid.Columns[0].FillWeight=120;grid.Columns[1].FillWeight=160;for(int i=2;i<5;i++)grid.Columns[i].FillWeight=65;
        Controls.Add(grid);Controls.Add(cards);Controls.Add(messages);Controls.Add(toolbar);
        client.Updated+=frame=>{if(!IsDisposed)Render(frame);};Disposed+=(s,e)=>client.Dispose();
        Render(new SensorFrame());
    }
    static void Add(FlowLayoutPanel p,string text,Action action){var b=new Button {Text=text,AutoSize=true,FlatStyle=FlatStyle.Flat,BackColor=ModernTheme.Raised,ForeColor=ModernTheme.Accent,Padding=new Padding(5),Margin=new Padding(4)};b.FlatAppearance.BorderColor=ModernTheme.Border;b.Click+=(s,e)=>action();p.Controls.Add(b);}
    internal void Start(bool elevated){state.Text=elevated?"Esperando permiso de administrador para el módulo de lectura…":"Leyendo sensores en segundo plano…";var ignored=client.Start(elevated);}
    async void InstallPawnIO()
    {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Sensors","PawnIO_setup.exe");
        if(!File.Exists(path)){state.Text="No se encontró el instalador oficial de PawnIO junto a Con-E. Vuelve a extraer el paquete completo.";return;}
        if(MessageBox.Show(this,"PawnIO es el controlador que LibreHardwareMonitor utiliza para acceder a ciertos sensores de CPU y placa. Se instalará en Windows y pedirá permiso de administrador.\n\n¿Abrir su instalador oficial?","Controlador de sensores",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
        try{client.Stop();using(var p=Process.Start(new ProcessStartInfo(path){UseShellExecute=true,Verb="runas"})){await Task.Run(()=>p.WaitForExit());}if(!IsDisposed)Start(true);}
        catch(Exception ex){if(!IsDisposed)state.Text="No se instaló PawnIO: "+ex.Message;}
    }
    static string Format(float? value,string unit){return value.HasValue?value.Value.ToString(unit=="RPM"?"N0":"0.0")+" "+unit:"No disponible";}
    internal void Render(SensorFrame frame)
    {
        grid.Rows.Clear();
        foreach(var s in frame.Sensors)grid.Rows.Add(s.Group+" · "+s.Device,s.Name,Format(s.Value,s.Unit),Format(s.Min,s.Unit),Format(s.Max,s.Unit));
        string[] groups={"CPU","GPU","Placa / chasis","Disco"};
        for(int i=0;i<4;i++){
            var available=frame.Sensors.Where(s=>s.Group==groups[i]&&s.Unit=="°C"&&s.Value.HasValue).ToArray();
            if(available.Length==0){values[i].Text="No disponible";continue;}var hottest=available.OrderByDescending(s=>s.Value).First();values[i].Text=Format(hottest.Value,"°C")+(available.Length>1?" · máxima actual":"");
        }
        state.Text=(frame.Time==default(DateTime)?"Sin lecturas todavía":"Actualizado: "+frame.Time.ToString("HH:mm:ss"))+" · Mínimos y máximos de esta sesión."+Environment.NewLine+
            (frame.PawnIO?"PawnIO detectado. ":"Algunos sensores de CPU/placa requieren PawnIO y lectura como administrador. ")+"La placa/chasis muestra los nombres reales del fabricante; no implica temperatura ambiente. "+frame.Error;
    }
}
internal sealed partial class MainForm
{
    SensorView sensorView;TabPage sensorPage;
    void OpenSensors()
    {
        if(sensorView==null||sensorView.IsDisposed){sensorView=new SensorView();sensorPage=new TabPage("Temperaturas y sensores"){BackColor=CanvasColor};sensorPage.Controls.Add(sensorView);tabs.TabPages.Add(sensorPage);if(!Environment.GetCommandLineArgs().Contains("--smoke-test"))sensorView.Start(false);}
        tabs.SelectedTab=sensorPage;
    }
    static void SensorSmokeTest(MainForm form,string directory)
    {
        var client=new SensorClient();SensorFrame actual=null;client.Updated+=frame=>actual=frame;
        var worker=client.Start(false);PumpUntil(()=>actual!=null,45000);
        if(actual==null||actual.Error.StartsWith("Sensores no disponibles:"))throw new Exception("No respondió el trabajador de sensores: "+actual?.Error);
        client.Stop();PumpUntil(()=>worker.IsCompleted,5000);
        if(client.Active)throw new Exception("No se detuvo el lector de sensores");
        var a=new SensorFrame();a.Sensors.Add(new SensorReading {Id="cpu/core/0",Value=42,Unit="°C"});client.ApplyExtrema(a);
        var b=new SensorFrame {Time=DateTime.Now,PawnIO=true,Error="VISTA DE PRUEBA · datos simulados"};
        b.Sensors.Add(new SensorReading {Id="cpu/core/0",Group="CPU",Device="Intel Core · ejemplo",Name="CPU Package",Unit="°C",Value=55});
        b.Sensors.Add(new SensorReading {Id="gpu/core",Group="GPU",Device="GPU · ejemplo",Name="GPU Core",Unit="°C",Value=47});
        b.Sensors.Add(new SensorReading {Id="board/system",Group="Placa / chasis",Device="Placa · ejemplo",Name="System",Unit="°C",Value=34});
        b.Sensors.Add(new SensorReading {Id="board/fan",Group="Placa / chasis",Device="Placa · ejemplo",Name="CPU Fan",Unit="RPM",Value=980});
        b.Sensors.Add(new SensorReading {Id="disk/temp",Group="Disco",Device="NVMe · ejemplo",Name="Composite Temperature",Unit="°C",Value=38});client.ApplyExtrema(b);
        if(b.Sensors[0].Min!=42||b.Sensors[0].Max!=55)throw new Exception("Extremos de sesión incorrectos");
        form.OpenSensors();form.sensorView.Render(b);form.Size=new Size(1100,820);Application.DoEvents();
        using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(directory,"sensores.png"));}
        form.Width=690;Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(directory,"sensores-estrecho.png"));}
        form.sensorView.Render(new SensorFrame {Error="Sin datos de prueba"});client.Dispose();
        File.WriteAllText(Path.Combine(directory,"sensores.txt"),"OK: extremos por ID, estado sin lectura, vista ancha/estrecha con datos simulados. El runner no valida sensores físicos ni elevación UAC interactiva.");
    }
}

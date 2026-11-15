using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    TabPage storagePage;StorageView storageView;
    void OpenStorage(string name)
    {
        if(current==null||current.Volumes.Count==0){status.Text="No hay unidades legibles disponibles.";return;}
        if(storageView==null||storageView.IsDisposed)
        {
            storageView=new StorageView(!Environment.GetCommandLineArgs().Contains("--smoke-test"));storagePage=new TabPage("Uso del disco") {BackColor=CanvasColor};storagePage.Controls.Add(storageView);tabs.TabPages.Add(storagePage);
        }
        tabs.SelectedTab=storagePage;storageView.SetVolumes(current.Volumes,name);
    }
    static void StorageSmokeTest(MainForm form,string dir)
    {
        string path=Path.Combine(Path.GetTempPath(),"cone-storage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(path,"Alpha"));Directory.CreateDirectory(Path.Combine(path,"Beta"));
        try
        {
            string original=Path.Combine(path,"Alpha","one.bin");var bytes=Enumerable.Range(0,131072).Select(i=>(byte)(i%251)).ToArray();File.WriteAllBytes(original,bytes);File.WriteAllBytes(Path.Combine(path,"Beta","two.bin"),new byte[65536]);File.WriteAllBytes(Path.Combine(path,"root.bin"),new byte[32768]);
            bool linked=StorageScanner.CreateHardLink(Path.Combine(path,"Alpha","alias.bin"),original,IntPtr.Zero);if(!linked)throw new Exception("No se pudo preparar el enlace duro de prueba");
            int ui=Thread.CurrentThread.ManagedThreadId,worker=ui;var scan=Task.Run(()=>{worker=Thread.CurrentThread.ManagedThreadId;return StorageScanner.Scan(path,CancellationToken.None);});PumpUntil(()=>scan.IsCompleted,15000);var result=scan.GetAwaiter().GetResult();
            if(worker==ui||result.Files!=3||result.LinksSkipped!=1||result.Groups.Count!=3||result.Groups.Sum(g=>g.Bytes)<=0||!File.ReadAllBytes(original).SequenceEqual(bytes))throw new Exception("Análisis de almacenamiento incorrecto: archivos="+result.Files+", enlaces omitidos="+result.LinksSkipped+", grupos="+result.Groups.Count+", rutas omitidas="+result.Skipped+", bytes="+result.Groups.Sum(g=>g.Bytes));
            using(var canceled=new CancellationTokenSource()){canceled.Cancel();bool stopped=false;try{StorageScanner.Scan(path,canceled.Token);}catch(OperationCanceledException){stopped=true;}if(!stopped)throw new Exception("No cancela el análisis");}
            var io=Task.Run(()=>StorageTelemetry.ReadIo("C:"));PumpUntil(()=>io.IsCompleted,15000);var actual=io.GetAwaiter().GetResult();
            form.Size=new Size(1100,820);form.tabs.SelectedIndex=0;form.dashboard.ScrollControlIntoView(form.cards.First(c=>Convert.ToString(c.Tag)=="Almacenamiento"));Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-tarjeta.png"),ImageFormat.Png);}
            form.OpenStorage("C:");Application.DoEvents();var v=new VolumeInfo {Name="C:",Label="SISTEMA · ejemplo",FileSystem="NTFS",TotalGiB=500,FreeGiB=320};form.storageView.SetVolumes(new[]{v});
            var display=new StorageScan {Total=500L*1073741824,Free=320L*1073741824,Files=12500,Skipped=3,LinksSkipped=100,Elapsed=TimeSpan.FromSeconds(4.2)};
            display.Groups.Add(new StorageGroup {Name="Users",Bytes=90L*1073741824});display.Groups.Add(new StorageGroup {Name="Program Files",Bytes=50L*1073741824});display.Groups.Add(new StorageGroup {Name="Windows",Bytes=30L*1073741824});display.Groups.Add(new StorageGroup {Name="Archivos en raíz",Bytes=2L*1073741824});form.storageView.ShowResult(v,display);Application.DoEvents();
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-carpetas.png"),ImageFormat.Png);}
            form.storageView.FocusCapacity();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-capacidad.png"),ImageFormat.Png);}
            form.Width=690;Application.DoEvents();form.storageView.FocusAnalysis();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-estrecho.png"),ImageFormat.Png);}
            if(form.storageView.ChartTotal!=display.Total)throw new Exception("El gráfico no cuadra con la capacidad total");
            File.WriteAllText(Path.Combine(dir,"almacenamiento.txt"),"OK: análisis real de carpeta temporal en trabajador, 3 archivos únicos, enlace duro no duplicado, sin modificación de contenido, cancelación, gráfico de capacidad consistente, interfaz ancha/estrecha. E/S WMI: "+(actual.Read.HasValue?actual.Read.Value.ToString():"no disponible")+" B/s. Capturas con datos simulados; salud y temperatura no inventadas.");
        }
        finally{Directory.Delete(path,true);}
    }
}

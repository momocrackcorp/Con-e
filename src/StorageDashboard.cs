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
    void EnsureStorageView()
    {
        if(storageView!=null&&!storageView.IsDisposed)return;
        storageView=new StorageView(!Environment.GetCommandLineArgs().Contains("--smoke-test"));storageView.SensorsRequested+=OpenSensors;storageView.DiskSelected+=d=>{selectedPhysicalKey=d.Key;foreach(var c in cards.OfType<UIModernization.ModernCardPanel>())c.Selected=Convert.ToString(c.Tag)=="Disco "+d.Number+" · "+d.Kind;};storagePage=new TabPage("Detalle de discos"){BackColor=CanvasColor};storagePage.Controls.Add(storageView);tabs.TabPages.Add(storagePage);
    }
    void OpenPhysicalStorage(PhysicalDiskInfo disk)
    {
        if(current==null)return;EnsureStorageView();selectedPhysicalKey=disk.Key;storageView.SetDisks(current.PhysicalDisks,disk.Key);tabs.SelectedTab=storagePage;
    }
    void OpenStorage(string letter)
    {
        var disk=current?.PhysicalDisks.FirstOrDefault(d=>d.Partitions.Any(p=>p.Letter+":"==letter));
        if(disk==null){status.Text="No se pudo asociar esta unidad a un disco. Actualiza el inventario.";return;}OpenPhysicalStorage(disk);storageView.SelectVolume(letter.TrimEnd(':'));
    }
    static PhysicalDiskInfo[] StorageFixtures()
    {
        var nvme=new PhysicalDiskInfo {Number=0,Model="SAMSUNG M.2 NVMe · ejemplo",Serial="TEST-NVME",UniqueId="TEST-NVME",MediaType=4,BusType=17,Size=1000UL*1073741824,Style="GPT",Complete=true};
        nvme.Partitions.Add(new PhysicalPartitionInfo {Number=1,Offset=1048576,Size=100UL*1048576,Role="Sistema EFI",FileSystem="FAT32"});
        nvme.Partitions.Add(new PhysicalPartitionInfo {Number=2,Offset=101UL*1048576,Size=500UL*1073741824,VolumeSize=500UL*1073741824,Free=320UL*1073741824,Letter="C",Label="SISTEMA · ejemplo",FileSystem="NTFS"});
        nvme.Partitions.Add(new PhysicalPartitionInfo {Number=3,Offset=501UL*1073741824,Size=498UL*1073741824,VolumeSize=498UL*1073741824,Free=410UL*1073741824,Letter="D",Label="DATOS · ejemplo",FileSystem="NTFS"});
        var hdd=new PhysicalDiskInfo {Number=1,Model="WD Blue · ejemplo",Serial="TEST-HDD",UniqueId="TEST-HDD",MediaType=3,BusType=11,Size=931UL*1073741824,Style="GPT",Complete=true};hdd.Partitions.Add(new PhysicalPartitionInfo {Number=1,Size=930UL*1073741824,VolumeSize=930UL*1073741824,Free=580UL*1073741824,Letter="H",Label="ARCHIVO · ejemplo",FileSystem="NTFS"});
        var ssd=new PhysicalDiskInfo {Number=2,Model="SSD SATA · ejemplo",Serial="TEST-SSD",UniqueId="TEST-SSD",MediaType=4,BusType=11,Size=447UL*1073741824,Style="GPT",Complete=true};ssd.Partitions.Add(new PhysicalPartitionInfo {Number=1,Size=446UL*1073741824,VolumeSize=446UL*1073741824,Free=326UL*1073741824,Letter="F",Label="TRABAJO · ejemplo",FileSystem="exFAT"});
        return new[]{nvme,hdd,ssd};
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
            if(form.helpVisible)form.ToggleHelp();
            form.Size=new Size(1100,820);form.tabs.SelectedIndex=0;form.dashboard.ScrollControlIntoView(form.cards.First(c=>Convert.ToString(c.Tag).StartsWith("Disco 0")));Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-tarjeta.png"),ImageFormat.Png);}
            form.OpenStorage("C:");Application.DoEvents();var v=form.current.PhysicalDisks[0];
            var display=new StorageScan {Total=500L*1073741824,Free=320L*1073741824,Files=12500,Skipped=3,LinksSkipped=100,Elapsed=TimeSpan.FromSeconds(4.2)};
            display.Groups.Add(new StorageGroup {Name="Users",Bytes=90L*1073741824});display.Groups.Add(new StorageGroup {Name="Program Files",Bytes=50L*1073741824});display.Groups.Add(new StorageGroup {Name="Windows",Bytes=30L*1073741824});display.Groups.Add(new StorageGroup {Name="Archivos en raíz",Bytes=2L*1073741824});form.storageView.ShowResult(display);form.storageView.ApplyAdvanced(new DiskReading {Status="Good",Temperature=38,Life=97,Hours=2048,ReadGB=12500,WrittenGB=8200});form.storageView.FocusAnalysis();Application.DoEvents();
            if(!form.storageView.DiskSelectorText.StartsWith("Disco 0"))throw new Exception("Nombre interno en selector de discos");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-carpetas.png"),ImageFormat.Png);}
            form.storageView.FocusCapacity();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-capacidad.png"),ImageFormat.Png);}
            form.Width=690;Application.DoEvents();form.storageView.FocusAnalysis();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"almacenamiento-estrecho.png"),ImageFormat.Png);}
            form.storageView.SelectVolume("D");if(form.storageView.CurrentVolumeLetter!="D"||form.storageView.ChartTotal!=0)throw new Exception("Mezcla análisis de distintos volúmenes");form.storageView.SelectVolume("C");
            if(form.storageView.ChartTotal!=display.Total)throw new Exception("El gráfico no cuadra con la capacidad total");
            form.OpenPhysicalStorage(form.current.PhysicalDisks[1]);if(form.storageView.VolumeCount!=1||form.storageView.CurrentVolumeLetter!="H")throw new Exception("Mezcla volúmenes de diferentes discos");
            var unknown=new PhysicalDiskInfo {BusType=17,MediaType=4,Model="Modelo sin formato"};if(unknown.ModelMentionsM2||unknown.Form.StartsWith("M.2"))throw new Exception("Deduce M.2 desde NVMe");
            using(var gallery=new Form {Text="Miniaturas compartidas · ejemplos",Size=new Size(720,260),BackColor=Color.White}){var flow=new FlowLayoutPanel {Dock=DockStyle.Fill};gallery.Controls.Add(flow);foreach(string kind in new[]{"hdd","ssd","m2-sata","m2-nvme"}){var tile=new FlowLayoutPanel {Width=160,Height=180,FlowDirection=FlowDirection.TopDown};tile.Controls.Add(new DiskMiniature {Kind=kind,Width=150,Height=130});tile.Controls.Add(new Label {Text=kind,AutoSize=true});flow.Controls.Add(tile);}gallery.Show();Application.DoEvents();using(var image=new Bitmap(gallery.Width,gallery.Height)){gallery.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"miniaturas-compartidas.png"));}gallery.Close();}
            File.WriteAllText(Path.Combine(dir,"almacenamiento.txt"),"OK: análisis real de carpeta temporal en trabajador, 3 archivos únicos, enlace duro no duplicado, sin modificación de contenido, cancelación, gráfico de capacidad consistente, interfaz ancha/estrecha. E/S WMI: "+(actual.Read.HasValue?actual.Read.Value.ToString():"no disponible")+" B/s. Capturas con datos simulados; salud y temperatura no inventadas.");
        }
        finally{Directory.Delete(path,true);}
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using UIModernization;

internal sealed partial class MainForm
{
    static void GraphicsAndLaunchSmokeTest(MainForm form,string dir)
    {
        if(WindowsVersionLogo.Detect("Microsoft Windows 10 Pro")!="10"||WindowsVersionLogo.Detect("Windows 11 Pro")!="11"||WindowsVersionLogo.Detect("Windows 12")!="generic"||WindowsVersionLogo.Detect("Windows Server 2025")!="generic")throw new Exception("Logo Windows incorrecto");
        var sampleDisk=new PhysicalDiskInfo {Size=1000000000,Complete=true};sampleDisk.Partitions.Add(new PhysicalPartitionInfo {Size=700000000,Letter="C",FileSystem="NTFS"});
        using(var chart=new PartitionOverview(sampleDisk))if(chart.Slices.Sum(x=>(decimal)x.Bytes)!=sampleDisk.Size||chart.Slices.Count!=2||DiskDisplaySize.Format(1000000000)!="1 GB")throw new Exception("Distribución de particiones incorrecta");
        PartitionIdentity.Verify();
        var query=WingetCommands.Request(false);var update=WingetCommands.Request(true);
        if(!query.UseShellExecute||query.Verb!="open"||query.Arguments.Contains("--all")||!update.UseShellExecute||update.Verb!="runas"||update.Arguments!="/d /k winget upgrade --all"||!Path.IsPathRooted(update.FileName)||update.WorkingDirectory!=Environment.GetFolderPath(Environment.SpecialFolder.System))throw new Exception("Solicitud de actualización/elevación incorrecta");
        int calls=0;string cancelled=WingetCommands.Launch(true,true,info=>{calls++;throw new Win32Exception(1223);});if(calls!=1||!cancelled.Contains("cancelada"))throw new Exception("No maneja cancelación de UAC");
        calls=0;string absent=WingetCommands.Launch(true,false,info=>calls++);if(calls!=0||!absent.Contains("no está disponible"))throw new Exception("Inicia actualización sin WinGet");
        string blocked=WingetCommands.Launch(true,true,info=>{throw new Win32Exception(5);});if(!blocked.StartsWith("No se pudo"))throw new Exception("Oculta error de lanzamiento");
        if(new VideoAdapterInfo {Vendor=0x10DE,Name="Texto Intel de prueba"}.Brand!="nvidia"||new VideoAdapterInfo {Vendor=0x8086,Name="GPU"}.Brand!="intel"||new VideoAdapterInfo {Vendor=0x1002,Name="GPU"}.Brand!="amd"||new VideoAdapterInfo {Vendor=0xFFFF,Name="GPU desconocida"}.Brand!=null||VideoMemory.VendorFromPnp(@"PCI\VEN_10DE&DEV_128B")!=0x10DE)throw new Exception("Fabricante de GPU incorrecto");
        var live=Task.Run(()=>{string issue;var adapters=VideoMemory.ReadAdapters(out issue);return adapters.Count+" adaptadores DXGI; "+issue;});PumpUntil(()=>live.IsCompleted,15000);string actual=live.GetAwaiter().GetResult();
        var previous=form.current;var oldSize=form.Size;var example=new Snapshot {Manufacturer=previous.Manufacturer,Model=previous.Model,RamTotalGiB=previous.RamTotalGiB,RamAvailableGiB=previous.RamAvailableGiB,PnpReadComplete=true};example.Summary.AddRange(previous.Summary);example.CoreThreads.AddRange(previous.CoreThreads);example.RamSlots.AddRange(previous.RamSlots);example.PhysicalDisks.AddRange(previous.PhysicalDisks);
        example.VideoAdapters.Add(new VideoAdapterInfo {Name="NVIDIA GeForce GT 710 · ejemplo",Vendor=0x10DE,DedicatedVideo=(ulong)(2007.13*1048576),DedicatedSystem=0,SharedSystem=(ulong)(8145.33*1048576)});
        example.VideoAdapters.Add(new VideoAdapterInfo {Name="Intel(R) HD Graphics 630 · ejemplo",Vendor=0x8086,DedicatedVideo=128UL*1048576,DedicatedSystem=0,SharedSystem=(ulong)(8145.33*1048576)});
        example.VideoAdapters.Add(new VideoAdapterInfo {Name="Microsoft Basic Render Driver",Vendor=0x1414,IsSoftware=true,DedicatedVideo=0,DedicatedSystem=0,SharedSystem=8145UL*1048576});
        form.current=example;form.Width=1100;form.tabs.SelectedIndex=0;form.RenderDashboard();Application.DoEvents();var card=form.cards.First(c=>Convert.ToString(c.Tag)=="Memoria de video");var panels=card.Controls.OfType<VideoAdapterPanel>().ToArray();
        if(panels.Length!=2||panels[0].VendorBrand!="nvidia"||panels[1].VendorBrand!="intel"||panels.Any(p=>!p.Controls.OfType<FlowLayoutPanel>().SelectMany(c=>c.Controls.OfType<BrandLogo>()).Any()))throw new Exception("No muestra ambas marcas de GPU individualmente");
        form.dashboard.ScrollControlIntoView(card);Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"video-intel-nvidia.png"));}
        form.Width=650;form.ResizeCards();Application.DoEvents();foreach(var p in panels)if(p.Width>card.ClientSize.Width)throw new Exception("Tarjeta de GPU desborda");
        using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"video-estrecho.png"));}
        using(var extracted=Icon.ExtractAssociatedIcon(Application.ExecutablePath))
        using(var bitmap=extracted.ToBitmap())
        {var c=bitmap.GetPixel(bitmap.Width/2,Math.Max(1,bitmap.Height/8));if(c.B<c.R*2)throw new Exception("El ejecutable no contiene el icono azul de Con-E");}
        using(var gallery=new Form {Text="Icono Con-E · tamaños del ejecutable",Size=new Size(650,260),BackColor=ModernTheme.Surface})
        {
            var flow=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(12)};gallery.Controls.Add(flow);
            foreach(int size in new[]{16,24,32,64,128})using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ConE.AppIcon"))using(var icon=new Icon(stream,new Size(size,size))){if(icon.Width!=size)throw new Exception("Falta tamaño del icono: "+size);var tile=new FlowLayoutPanel {Width=110,Height=175,FlowDirection=FlowDirection.TopDown};var box=new PictureBox {Image=icon.ToBitmap(),Size=new Size(100,140),SizeMode=PictureBoxSizeMode.CenterImage};box.Disposed+=(s,e)=>box.Image.Dispose();tile.Controls.Add(box);tile.Controls.Add(new Label {Text=size+" px",AutoSize=true});flow.Controls.Add(tile);}
            gallery.Show();Application.DoEvents();using(var image=new Bitmap(gallery.Width,gallery.Height)){gallery.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"icono-cone.png"));}gallery.Close();
        }
        File.WriteAllText(Path.Combine(dir,"actualizacion-video-icono.txt"),"OK: consulta sin elevación, actualización con runas, directorio del sistema, UAC cancelado y WinGet ausente mediante simulación de lanzamiento (no se ejecutan actualizaciones). Logos NVIDIA/Intel individuales, VID PCI prioritario, adaptador software separado, diseño estrecho, icono incrustado 16/24/32/64/128 y recurso nativo del ejecutable. "+actual+". El diálogo UAC interactivo requiere prueba en Windows del usuario.");
        form.current=previous;form.Size=oldSize;form.RenderDashboard();
    }
}

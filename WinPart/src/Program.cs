// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string qa=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa");
        if(Environment.GetCommandLineArgs().Any(a=>a=="--smoke-test"||a=="--resize-test"))
        {
            Directory.CreateDirectory(qa);
            try {if(Environment.GetCommandLineArgs().Contains("--resize-test"))MainForm.ResizeIntegrationTest();else MainForm.SmokeTest();}
            catch(Exception ex){File.WriteAllText(Path.Combine(qa,"error.txt"),ex.ToString());File.WriteAllText(Path.Combine(qa,"resize-error.txt"),ex.ToString());Environment.ExitCode=1;}return;
        }
        var arguments=Environment.GetCommandLineArgs();int apply=Array.IndexOf(arguments,"--apply-plan");
        if(apply>=0){if(apply+1>=arguments.Length){Environment.ExitCode=2;return;}Environment.ExitCode=MainForm.RunElevatedHelper(arguments[apply+1]);return;}
        using(var notice=new UsageNotice())if(notice.ShowDialog()!=DialogResult.OK)return;
        Application.Run(new MainForm());
    }
}
internal sealed class Snapshot {public readonly List<string> Warnings=new List<string>();}
internal sealed partial class MainForm : Form
{
    static readonly Color CanvasColor=Color.FromArgb(241,244,248);
    readonly Label status=new Label {Dock=DockStyle.Bottom,Height=36,AutoEllipsis=true,Padding=new Padding(8)};
    readonly FlowLayoutPanel actions=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(8)};
    public MainForm()
    {
        Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);Text="WinPart · Gestor de particiones · v0.9.1";Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        Size=new Size(1100,800);MinimumSize=new Size(700,500);StartPosition=FormStartPosition.CenterScreen;BackColor=CanvasColor;
        var about=new Button {Text="Acerca de / GPL",AutoSize=true};about.Click+=(s,e)=>MessageBox.Show(this,"WinPart · software libre GPL-2.0-or-later.\r\nPaleta adaptada de GParted (Bart Hakvoort, Curtis Gedak).\r\nMotor Windows propio; no es GParted oficial.\r\nConsulta COPYING, THIRD_PARTY_NOTICES.md y la carpeta source incluida.","WinPart — Créditos");actions.Controls.Add(about);
        var refresh=new Button {Text="Actualizar discos",AutoSize=true};refresh.Click+=async(s,e)=>await RefreshData();actions.Controls.Add(refresh);
        var title=new Label {Text="WinPart · Particiones y operaciones pendientes",Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(12),ForeColor=Color.FromArgb(40,91,181)};
        Controls.Add(BuildDiskMap());Controls.Add(actions);Controls.Add(title);Controls.Add(status);
        Shown+=async(s,e)=>{if(!Environment.GetCommandLineArgs().Contains("--smoke-test"))await RefreshData();};
    }
    static string Value(ManagementBaseObject obj,string key){var v=Convert.ToString(obj[key]);return string.IsNullOrWhiteSpace(v)?"No disponible":v.Trim();}
    static ListView MakeList(string[] columns)
    {var list=new ListView {Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false};foreach(var c in columns)list.Columns.Add(c,180);return list;}
    async Task RefreshData()
    {
        if(applyingChanges)return;CloseResizeEditor();actions.Enabled=false;pendingResizes.Clear();inlineEditor.Visible=false;formatEditor.Visible=false;partitionInfo.Visible=true;UpdatePending();
        status.Text="Leyendo discos…";
        try{var snapshot=new Snapshot();var data=await Task.Run(()=>ReadDiskLayouts(snapshot));if(IsDisposed)return;diskLayouts.Clear();diskLayouts.AddRange(data);RenderDiskMaps();status.Text=snapshot.Warnings.Count==0?data.Count+" disco(s) · selecciona una partición.":string.Join(" | ",snapshot.Warnings);}
        catch(Exception ex){status.Text=ex.Message;}finally{if(!IsDisposed)actions.Enabled=true;}
    }
    internal static void SmokeTest()
    {
        DiskMediumDetection.VerifyFallbacks();
        using(var form=new MainForm())
        {
            var disk=new DiskLayout {Number=0,Name="Disco simulado",BusType=15,Style="GPT",UniqueId="SIMULATED",Complete=true,Size=500UL*1073741824};
            var p=new PartitionLayout {Number=1,Offset=1048576,Size=200UL*1073741824,Letter="Z",Type="Datos básicos",FileSystem="NTFS",VolumeSize=200UL*1073741824,Free=50UL*1073741824};disk.Partitions.Add(p);
            disk.Partitions.Add(new PartitionLayout {Number=2,Offset=210UL*1073741824,Size=100UL*1073741824,Letter="E",Type="Datos básicos",FileSystem="exFAT",VolumeSize=100UL*1073741824,Free=70UL*1073741824});
            if(FileSystemPalette.Get("NTFS")==FileSystemPalette.Get("exFAT")||FileSystemPalette.Used==FileSystemPalette.Unused)throw new Exception("Paleta sin diferenciación");
            var boxes=new PartitionStrip(disk,disk.Segments()){Width=900,Height=70}.Blocks();if(boxes.Count!=disk.Segments().Count||boxes.Sum(b=>b.Width)!=900)throw new Exception("Mapa de bloques inconsistente");
            form.diskLayouts.Add(disk);form.Show();form.RenderDiskMaps();Application.DoEvents();
            form.ConfigureEditor(disk,p,100UL*1073741824,400UL*1073741824);
            if(form.resizePanel.Parent==null||!ReferenceEquals(form.resizePanel.Parent.Tag,disk))throw new Exception("Editor no está en la tarjeta del disco elegido");
            string editorDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(editorDir,"editor-bajo-disco.png"),ImageFormat.Png);}
            form.cancelEdit.PerformClick();if(form.resizePanel.Parent!=null||form.pendingResizes.Count!=0)throw new Exception("No cancela edición sin pendientes");
            form.ConfigureEditor(disk,p,100UL*1073741824,400UL*1073741824);form.editSize.Value=160*1024;
            form.resizePanel.Controls.OfType<Button>().Single(b=>b.Text=="Añadir cambio").PerformClick();Application.DoEvents();
            if(form.pendingResizes.Count!=1||p.Size!=200UL*1073741824)throw new Exception("Fallo de vista previa");
            var encoded=EncodeOperations(form.pendingResizes);var decoded=DecodeOperations(encoded);
            if(decoded.Count!=1||decoded[0].Disk.UniqueId!=disk.UniqueId||decoded[0].Part.Offset!=p.Offset||decoded[0].Bytes!=160UL*1073741824)throw new Exception("El auxiliar pierde identidad o tamaño del plan");
            bool rejected=false;try{DecodeOperations("invalid-base64");}catch{rejected=true;}if(!rejected)throw new Exception("Plan inválido aceptado");
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"winpart.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0)throw new Exception("Fallo al descartar");
            p.IsBoot=true;if(CanFormat(disk,p))throw new Exception("Permite formatear partición de arranque");p.IsBoot=false;p.IsSystem=true;if(CanFormat(disk,p))throw new Exception("Permite formatear partición sistema");p.IsSystem=false;
            form.StageFormat(disk,p,"exFAT","TEST_VOLUME");if(p.FileSystem!="NTFS"||form.pendingResizes.Count!=1||!form.pendingResizes[0].Description.Contains("BORRA"))throw new Exception("Preparación de formateo incorrecta");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"filesystem-pendiente.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0)throw new Exception("No descarta formateo");
            string virtualPath=Path.Combine(Path.GetTempPath(),"winpart-pending-"+Guid.NewGuid().ToString("N")+".vhd");
            form.StageVirtual(new PendingResize {VirtualAction="create",VirtualPath=virtualPath,Bytes=1024UL*1048576});
            var virtualPlan=DecodeOperations(EncodeOperations(form.pendingResizes));
            if(virtualPlan.Count!=1||virtualPlan[0].VirtualPath!=virtualPath||File.Exists(virtualPath))throw new Exception("La preparación virtual modifica disco o pierde el plan");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"vhd-pendiente.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0||File.Exists(virtualPath))throw new Exception("El descarte virtual modifica disco");
            bool virtualRejected=false;try{DecodeOperations(EncodeOperations(new[]{new PendingResize {VirtualAction="create",VirtualPath=virtualPath,Bytes=1}}));}catch{virtualRejected=true;}if(!virtualRejected)throw new Exception("Acepta tamaño VHD inválido");
            var gap=disk.Segments().First(g=>CanCreate(disk,g));
            form.SelectPartition(disk,gap);if(!form.createPartition.Enabled)throw new Exception("No habilita Nueva partición en espacio libre");
            form.StageCreate(NewPartitionChange(disk,gap,128UL*1048576,"exFAT","PRUEBA"));
            var newPlan=DecodeOperations(EncodeOperations(form.pendingResizes));
            if(newPlan.Count!=1||!newPlan[0].Create||newPlan[0].FileSystem!="exFAT"||disk.Partitions.Count!=2)throw new Exception("Preparación de nueva partición incorrecta");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"nueva-particion-pendiente.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0||disk.Partitions.Count!=2)throw new Exception("Descarte de nueva partición modifica datos");
            if(form.actions.Controls.OfType<Button>().Count()!=10||form.deletePartition.Image==null||form.createPartition.Image==null||form.deletePartition.TextImageRelation!=TextImageRelation.ImageAboveText)throw new Exception("Barra de iconos incompleta");
            p.IsBoot=true;if(CanDelete(disk,p))throw new Exception("Permite eliminar arranque");p.IsBoot=false;p.IsSystem=true;if(CanDelete(disk,p))throw new Exception("Permite eliminar sistema");p.IsSystem=false;
            var protectedPart=new PartitionLayout {Number=3,Letter="Z",Type="EFI"};if(CanDelete(disk,protectedPart))throw new Exception("Permite eliminar EFI");protectedPart.Type="Reservada MSR";if(CanDelete(disk,protectedPart))throw new Exception("Permite eliminar MSR");protectedPart.Type="Recuperación";if(CanDelete(disk,protectedPart))throw new Exception("Permite eliminar recuperación");
            var mbr=new DiskLayout {Style="MBR"};var role=new PartitionLayout {Primary=true};if(CommandIcons.Role(mbr,role)!="Primaria MBR")throw new Exception("Icono primaria incorrecto");role.Primary=false;if(CommandIcons.Role(mbr,role)!="Lógica MBR"||CommandIcons.ForPartition(mbr,role)!="logical")throw new Exception("Icono lógica incorrecto");if(CommandIcons.Role(disk,p)!="Partición GPT"||CommandIcons.DiskKind(disk)!="VHD / imagen virtual")throw new Exception("Clasificación GPT/VHD incorrecta");
            var deleteTarget=disk.Partitions[1];form.SelectPartition(disk,deleteTarget);if(!form.deletePartition.Enabled)throw new Exception("No habilita eliminar datos");form.deletePartition.PerformClick();
            if(form.selectedEditPartition==null||form.selectedEditPartition.Number!=deleteTarget.Number)throw new Exception("La cola cambia la selección a otra partición");form.deletePartition.PerformClick();
            var deletePlan=DecodeOperations(EncodeOperations(form.pendingResizes));if(deletePlan.Count!=1||!deletePlan[0].Delete||deletePlan[0].Part.Number!=deleteTarget.Number||disk.Partitions.Count!=2)throw new Exception("Preparar eliminación modifica datos o pierde plan");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"barra-eliminar-pendiente.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0||disk.Partitions.Count!=2)throw new Exception("Descartar eliminación modifica datos");
            form.Size=new Size(730,650);form.PerformLayout();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"barra-estrecha.png"),ImageFormat.Png);}
            form.Size=new Size(1100,800);form.PerformLayout();Application.DoEvents();
            var foreign=new PartitionLayout {Number=7,Offset=400UL*1073741824,Size=32UL*1048576,Type="EFI"};
            if(!CanAdvancedDelete(disk,foreign))throw new Exception("Bloquea EFI ajena al Windows actual en interfaz avanzada");
            form.StageAdvanced(new PendingResize {Advanced=true,Delete=true,Disk=disk,Part=foreign});
            var ap=DecodeOperations(EncodeOperations(form.pendingResizes));if(ap.Count!=1||!ap[0].Advanced||ap[0].Part.Letter!="")throw new Exception("Pierde eliminación sin letra");
            form.discardPending.PerformClick();form.SelectDisk(disk);form.StageReset("MBR");
            var rp=DecodeOperations(EncodeOperations(form.pendingResizes));if(rp.Count!=1||!rp[0].Reset||rp[0].ResetStyle!="MBR")throw new Exception("Pierde operación disco completo");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"avanzadas-pendiente.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();
            using(var notice=new UsageNotice()){notice.Show();Application.DoEvents();using(var image=new Bitmap(notice.Width,notice.Height)){notice.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"aviso-inicial.png"),ImageFormat.Png);}notice.Close();}
            var sampleKinds=new[]{"hdd","ssd","m2-sata","m2-nvme"};
            for(int i=0;i<sampleKinds.Length;i++){var sample=new DiskLayout {Number=(uint)(i+10),Name=sampleKinds[i]+" · ejemplo",BusType=(ushort)(i==3?17:11),UniqueId="ART-"+i,Style="GPT",Complete=true,Size=240UL*1073741824,Info=new PhysicalDiskInfo {Number=(uint)(i+10),Model=i>1?"M.2 · ejemplo":"Ejemplo",MediaType=(uint)(i==0?3:4),BusType=(ushort)(i==3?17:11)}};sample.Partitions.Add(new PartitionLayout {Number=1,Offset=1048576,Size=200UL*1073741824,FileSystem=i==0?"NTFS":i==1?"exFAT":i==2?"FAT32":"NTFS",Type="Datos básicos"});form.diskLayouts.Add(sample);}
            form.RenderDiskMaps();form.SelectDisk(form.diskLayouts.First(d=>d.Number==10));form.diskMaps.ScrollControlIntoView(form.diskMaps.Controls.OfType<TableLayoutPanel>().First(g=>((DiskLayout)g.Tag).Number==10));Application.DoEvents();
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"miniaturas-discos.png"),ImageFormat.Png);}
            foreach(string kind in sampleKinds)using(var icon=DiskMiniature.Make(kind,64)){if(icon.GetPixel(0,0).A!=0)throw new Exception("Miniatura sin transparencia");}
            var wd=new PhysicalDiskInfo {Number=20,Model="WDC WD10EARX-00N0YB0",BusType=8};DiskMediumDetection.Resolve(wd);var wdDisk=new DiskLayout {Number=20,Name=wd.Model+" · ejemplo",BusType=8,UniqueId="WD-RAID-TEST",Style="MBR",Complete=true,Size=931UL*1073741824,Info=wd};wdDisk.Partitions.Add(new PartitionLayout {Number=1,Offset=1048576,Size=900UL*1073741824,Type="Datos básicos MBR",FileSystem="NTFS"});form.diskLayouts.Add(wdDisk);form.RenderDiskMaps();form.SelectDisk(wdDisk);form.diskMaps.ScrollControlIntoView(form.diskMaps.Controls.OfType<TableLayoutPanel>().First(g=>ReferenceEquals(g.Tag,wdDisk)));Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"hdd-raid-corregido.png"),ImageFormat.Png);}
            var real=ReadDiskLayouts(new Snapshot());if(!real.Any(d=>d.Partitions.Count>0))throw new Exception("La consulta real no obtuvo particiones");
            File.WriteAllText(Path.Combine(dir,"resultado.txt"),"OK: aplicación independiente, cola visual, descarte y lectura real.");form.Close();
        }
    }
}

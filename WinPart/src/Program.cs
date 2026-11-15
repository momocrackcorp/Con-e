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
        Text="WinPart · Gestor de particiones · v0.3.0";Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
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
        if(applyingChanges)return;actions.Enabled=false;pendingResizes.Clear();inlineEditor.Visible=false;formatEditor.Visible=false;partitionInfo.Visible=true;UpdatePending();
        status.Text="Leyendo discos…";
        try{var snapshot=new Snapshot();var data=await Task.Run(()=>ReadDiskLayouts(snapshot));if(IsDisposed)return;diskLayouts.Clear();diskLayouts.AddRange(data);RenderDiskMaps();status.Text=snapshot.Warnings.Count==0?data.Count+" disco(s) · selecciona una partición.":string.Join(" | ",snapshot.Warnings);}
        catch(Exception ex){status.Text=ex.Message;}finally{if(!IsDisposed)actions.Enabled=true;}
    }
    internal static void SmokeTest()
    {
        using(var form=new MainForm())
        {
            var disk=new DiskLayout {Number=0,Name="SSD simulado",Style="GPT",UniqueId="SIMULATED",Complete=true,Size=500UL*1073741824};
            var p=new PartitionLayout {Number=1,Offset=1048576,Size=200UL*1073741824,Letter="D",Type="Datos básicos",FileSystem="NTFS",VolumeSize=200UL*1073741824,Free=50UL*1073741824};disk.Partitions.Add(p);
            disk.Partitions.Add(new PartitionLayout {Number=2,Offset=210UL*1073741824,Size=100UL*1073741824,Letter="E",Type="Datos básicos",FileSystem="exFAT",VolumeSize=100UL*1073741824,Free=70UL*1073741824});
            if(FileSystemPalette.Get("NTFS")==FileSystemPalette.Get("exFAT")||FileSystemPalette.Used==FileSystemPalette.Unused)throw new Exception("Paleta sin diferenciación");
            var boxes=new PartitionStrip(disk,disk.Segments()){Width=900,Height=70}.Blocks();if(boxes.Count!=disk.Segments().Count||boxes.Sum(b=>b.Width)!=900)throw new Exception("Mapa de bloques inconsistente");
            form.diskLayouts.Add(disk);form.Show();form.RenderDiskMaps();Application.DoEvents();
            form.ConfigureEditor(disk,p,100UL*1073741824,400UL*1073741824);form.editSize.Value=160*1024;
            form.inlineEditor.Controls.OfType<Button>().Single(b=>b.Text=="Añadir cambio").PerformClick();Application.DoEvents();
            if(form.pendingResizes.Count!=1||p.Size!=200UL*1073741824)throw new Exception("Fallo de vista previa");
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"winpart.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0)throw new Exception("Fallo al descartar");
            p.IsBoot=true;if(CanFormat(disk,p))throw new Exception("Permite formatear partición de arranque");p.IsBoot=false;p.IsSystem=true;if(CanFormat(disk,p))throw new Exception("Permite formatear partición sistema");p.IsSystem=false;
            form.StageFormat(disk,p,"exFAT","TEST_VOLUME");if(p.FileSystem!="NTFS"||form.pendingResizes.Count!=1||!form.pendingResizes[0].Description.Contains("BORRA"))throw new Exception("Preparación de formateo incorrecta");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"filesystem-pendiente.png"),ImageFormat.Png);}
            form.discardPending.PerformClick();if(form.pendingResizes.Count!=0)throw new Exception("No descarta formateo");
            var real=ReadDiskLayouts(new Snapshot());if(!real.Any(d=>d.Partitions.Count>0))throw new Exception("La consulta real no obtuvo particiones");
            File.WriteAllText(Path.Combine(dir,"resultado.txt"),"OK: aplicación independiente, cola visual, descarte y lectura real.");form.Close();
        }
    }
}

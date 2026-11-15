// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Original vector glyphs in a rounded outline style.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal static class CommandIcons
{
    public static Bitmap Make(string name,Color color,int size)
    {
        var bitmap=new Bitmap(size,size);
        using(var g=Graphics.FromImage(bitmap))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/28f,size/28f);
            using(var pen=new Pen(color,1.8f) {StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
            using(var wash=new SolidBrush(Color.FromArgb(22,color)))
            {
                Action<float,float,float,float> line=(x,y,a,b)=>g.DrawLine(pen,x,y,a,b);
                Action<float,float,float,float> rect=(x,y,w,h)=>{g.FillRectangle(wash,x,y,w,h);g.DrawRectangle(pen,x,y,w,h);};
                if(name=="create") {rect(3,5,15,18);line(7,10,13,10);line(7,14,11,14);g.FillEllipse(Brushes.White,13,13,13,13);g.DrawEllipse(pen,14,14,11,11);line(19.5f,16.5f,19.5f,22.5f);line(16.5f,19.5f,22.5f,19.5f);}
                else if(name=="delete") {line(5,7,23,7);line(11,3,17,3);line(9,4,9,7);line(19,4,19,7);line(7,9,8,24);line(8,24,20,24);line(20,24,21,9);line(12,11,12,20);line(16,11,16,20);}
                else if(name=="format") {rect(3,4,22,20);line(3,11,25,11);line(9,4,9,24);line(16,15,16,22);line(12.5f,18.5f,19.5f,18.5f);using(var dot=new SolidBrush(color))g.FillEllipse(dot,5,6,2,2);}
                else if(name=="resize") {rect(4,8,20,12);line(7,14,21,14);line(7,14,10,11);line(7,14,10,17);line(21,14,18,11);line(21,14,18,17);line(4,4,4,6);line(24,4,24,6);line(4,22,4,24);line(24,22,24,24);}
                else if(name=="apply") {g.FillEllipse(wash,3,3,22,22);g.DrawEllipse(pen,3,3,22,22);line(8,14,12,18);line(12,18,21,9);}
                else if(name=="discard") {g.DrawArc(pen,5,5,19,19,205,285);line(4,4,4,12);line(4,12,12,12);}
                else if(name=="refresh") {g.DrawArc(pen,4,4,20,20,30,300);line(24,5,24,12);line(24,12,17,12);}
                else if(name=="about") {g.DrawEllipse(pen,3,3,22,22);line(14,12,14,20);g.DrawEllipse(pen,13.4f,7,1.2f,1.2f);}
                else if(name=="vhd") {rect(3,8,18,16);g.DrawEllipse(pen,6,12,9,9);line(20,3,25,3);line(25,3,25,17);line(23,19,25,17);line(17,3,17,6);line(17,3,20,3);}
                else if(name=="disk") {rect(3,5,22,18);g.DrawEllipse(pen,6,8,11,11);g.DrawEllipse(pen,10,12,3,3);line(20,17,22,17);line(20,20,22,20);}
                else if(name=="gap") {pen.DashStyle=DashStyle.Dash;g.DrawRectangle(pen,4,5,20,18);pen.DashStyle=DashStyle.Solid;line(10,14,18,14);line(14,10,14,18);}
                else if(name=="logical") {rect(3,4,22,20);rect(9,10,12,10);}
                else if(name=="system") {g.DrawPolygon(pen,new[]{new PointF(14,3),new PointF(24,7),new PointF(22,18),new PointF(14,25),new PointF(6,18),new PointF(4,7)});line(9,13,13,17);line(13,17,19,10);}
                else if(name=="recovery") {g.DrawArc(pen,4,4,20,20,30,300);line(24,5,24,12);line(24,12,17,12);line(10,14,18,14);line(14,10,14,18);}
                else {rect(3,4,22,20);line(3,10,25,10);line(9,10,9,24);line(14,15,20,15);line(14,19,20,19);}
            }
        }
        return bitmap;
    }
    public static string DiskKind(DiskLayout disk)
    {return disk.BusType==15?"VHD / imagen virtual":disk.BusType==14?"Disco virtual":disk.BusType==7?"Disco USB":disk.BusType==0?"Disco · tipo no informado":"Disco físico";}
    public static string Role(DiskLayout disk,PartitionLayout p)
    {
        if(p.Gap)return "Sin asignar";
        if(p.IsSystem||p.Type=="EFI")return "Sistema / EFI";
        if(p.Type=="Reservada MSR")return "Reservada MSR";
        if(p.Type=="Recuperación")return "Recuperación";
        if(disk.Style=="GPT")return "Partición GPT";
        if(disk.Style=="MBR"&&new[]{"Tipo MBR 5","Tipo MBR 15","Tipo MBR 133"}.Contains(p.Type))return "Extendida MBR";
        if(disk.Style=="MBR"&&p.Primary.HasValue)return p.Primary.Value?"Primaria MBR":"Lógica MBR";
        return "Partición · tipo no informado";
    }
    public static string ForPartition(DiskLayout disk,PartitionLayout p)
    {string role=Role(disk,p);return p.Gap?"gap":role=="Sistema / EFI"||role=="Reservada MSR"?"system":role=="Recuperación"?"recovery":(role=="Lógica MBR"||role=="Extendida MBR")?"logical":"partition";}
}
internal sealed partial class MainForm
{
    readonly ToolTip commandTips=new ToolTip {AutoPopDelay=12000,InitialDelay=350,ReshowDelay=100};
    void StyleCommand(Button button,string text,string glyph,Color color,string tip)
    {
        button.Text=text;button.AutoSize=false;button.Size=new Size(104,82);button.Margin=new Padding(2);button.Padding=new Padding(3);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(231,240,255);button.FlatAppearance.MouseDownBackColor=Color.FromArgb(213,228,250);button.BackColor=Color.White;button.ForeColor=Color.FromArgb(32,47,67);button.TextImageRelation=TextImageRelation.ImageAboveText;button.ImageAlign=ContentAlignment.MiddleCenter;button.TextAlign=ContentAlignment.MiddleCenter;button.Font=new Font(Font.FontFamily,9f);button.AccessibleName=text;commandTips.SetToolTip(button,tip);
        Action repaint=()=>{var previous=button.Image;button.Image=CommandIcons.Make(glyph,color,Math.Max(32,(int)(36*DeviceDpi/96f)));if(previous!=null)previous.Dispose();};repaint();
        DpiChanged+=(s,e)=>repaint();button.Disposed+=(s,e)=>{if(button.Image!=null)button.Image.Dispose();};
    }
    void PopulateCommandBar(Button virtualButton)
    {
        var about=actions.Controls.OfType<Button>().First();var refresh=actions.Controls.OfType<Button>().Last();actions.Controls.Clear();actions.BackColor=Color.White;actions.Padding=new Padding(8,3,8,3);actions.WrapContents=true;
        StyleCommand(refresh,"Actualizar","refresh",Color.SteelBlue,"Volver a leer discos y particiones. La cola se descarta.");
        StyleCommand(createPartition,"Crear partición","create",Color.FromArgb(22,150,95),"Selecciona espacio sin asignar para preparar una nueva partición.");
        StyleCommand(deletePartition,"Eliminar","delete",Color.FromArgb(205,58,68),"Prepara la eliminación de la partición de datos seleccionada. Sólo se ejecuta al aplicar y confirmar.");
        StyleCommand(formatButton,"Formatear","format",Color.FromArgb(198,126,25),"Elegir NTFS, exFAT o FAT32. Formatear borra los archivos al aplicar.");
        StyleCommand(resizePartition,"Ajustar tamaño","resize",Color.FromArgb(41,116,197),"Reducir o ampliar una partición NTFS compatible.");
        StyleCommand(virtualButton,"Disco virtual ▾","vhd",Color.FromArgb(128,80,190),"Crear, conectar o desconectar un archivo VHD de prueba.");
        StyleCommand(applyPending,applyPending.Text,"apply",Color.FromArgb(31,147,85),"Revisar la cola, confirmar y solicitar administrador si hace falta. La ventana permanece abierta.");
        StyleCommand(discardPending,"Descartar","discard",Color.FromArgb(133,111,80),"Quitar las operaciones pendientes sin modificar discos.");
        StyleCommand(about,"Acerca de","about",Color.FromArgb(80,128,180),"Información, licencia GPL y créditos de WinPart.");
        actions.Controls.AddRange(new Control[]{refresh,createPartition,deletePartition,formatButton,resizePartition,virtualButton,applyPending,discardPending,about});
        Disposed+=(s,e)=>commandTips.Dispose();
    }
}

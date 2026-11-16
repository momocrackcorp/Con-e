using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using UIModernization;

internal sealed class AboutConE : Form
{
    readonly FlowLayoutPanel text=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20),BackColor=ModernTheme.Surface};
    internal AboutConE()
    {
        string version=typeof(AboutConE).Assembly.GetName().Version.ToString(3);
        Text="Acerca de Con-E";Icon=AppVisualIdentity.MainIcon;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.CenterParent;Size=new Size(760,660);MinimumSize=new Size(580,450);BackColor=ModernTheme.Surface;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(16)};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        var heading=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var picture=new PictureBox {Image=AppVisualIdentity.MainIcon.ToBitmap(),Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(8)};
        picture.Disposed+=(s,e)=>picture.Image.Dispose();heading.Controls.Add(picture,0,0);
        var names=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(10,4,0,0)};
        names.Controls.Add(new Label {Text="Con-E",AutoSize=true,Font=new Font(Font.FontFamily,25,FontStyle.Bold),ForeColor=ModernTheme.Accent,Margin=Padding.Empty});
        names.Controls.Add(new Label {Text="Diagnóstico y herramientas para Windows · v"+version,AutoSize=true,ForeColor=ModernTheme.Muted,Margin=new Padding(0,8,0,0)});
        heading.Controls.Add(names,1,0);layout.Controls.Add(heading,0,0);
        var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var portraitPanel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=new Padding(8)};
        portraitPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));portraitPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));portraitPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        using(var stream=typeof(AboutConE).Assembly.GetManifestResourceStream("ConE.CharacterIllustration"))
        using(var original=Image.FromStream(stream))
        {
            var portrait=new PictureBox {Name="cone-original-portrait",Image=new Bitmap(original),Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,AccessibleName="Coné, ilustración original aportada por el usuario"};
            portrait.Disposed+=(s,e)=>portrait.Image.Dispose();portraitPanel.Controls.Add(portrait,0,0);
        }
        portraitPanel.Controls.Add(new Label {Text="Coné\nUn guiño a Pepo",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=ModernTheme.Muted,Font=new Font(Font.FontFamily,9),UseMnemonic=false},0,1);
        body.Controls.Add(portraitPanel,0,0);body.Controls.Add(text,1,0);layout.Controls.Add(body,0,1);
        Add("Hecha en Ñuble, Chile",true);
        Add("Desarrollada por Omar Águila\nLaboratorios Momocrackcorp\nNoviembre de 2026\nPueblo Seco, San Ignacio, Ñuble, Chile.");
        Add("Un guiño a nuestra historieta",true);
        Add("El nombre Con-E es un guiño a Coné, el sobrino de Condorito, personajes de la historieta chilena creada por René Ríos Boettiger, «Pepo». Un pequeño homenaje al humor y a la identidad de Chile.");
        Add("Créditos y derechos de los personajes",true);
        Add("Coné y Condorito son personajes creados por Pepo. Los derechos sobre los personajes, sus nombres y las ilustraciones pertenecen a sus respectivos titulares. Este homenaje no implica afiliación, patrocinio, autorización ni concesión de una licencia por parte de esos titulares.");
        Add("La autoría indicada corresponde a la aplicación Con-E, no a los personajes ni a sus ilustraciones.");
        Add("Componentes de terceros",true);
        Add("Las bibliotecas, marcas y recursos de terceros conservan sus propias licencias y atribuciones, disponibles en THIRD_PARTY_NOTICES.md y en la carpeta Licenses del paquete.");
        var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,8,0,0)};
        var close=Action("Cerrar",()=>Close());close.DialogResult=DialogResult.OK;AcceptButton=close;CancelButton=close;footer.Controls.Add(close);
        footer.Controls.Add(Action("Licencias",()=>MainForm.OpenAboutLicenses()));
        layout.Controls.Add(footer,0,2);Controls.Add(layout);
        text.SizeChanged+=(s,e)=>FitText();Shown+=(s,e)=>FitText();
    }
    void Add(string value,bool heading=false)
    {
        text.Controls.Add(new Label {Text=value,AutoSize=true,UseMnemonic=false,Font=heading?new Font(Font.FontFamily,12,FontStyle.Bold):Font,ForeColor=heading?ModernTheme.Text:ModernTheme.Muted,Margin=new Padding(0,heading?8:0,0,heading?10:18)});
    }
    void FitText(){int width=Math.Max(120,text.ClientSize.Width-text.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-6);foreach(Control label in text.Controls)label.MaximumSize=new Size(width,0);}
    Button Action(string caption,Action action)
    {
        var button=new Button {Text=caption,AutoSize=true,FlatStyle=FlatStyle.Flat,Padding=new Padding(9,4,9,4),BackColor=ModernTheme.Raised,ForeColor=ModernTheme.Accent,Margin=new Padding(8,0,0,0)};
        button.FlatAppearance.BorderColor=ModernTheme.Border;button.Click+=(s,e)=>action();return button;
    }
}
internal sealed partial class MainForm
{
    void ShowAbout(){using(var dialog=new AboutConE())dialog.ShowDialog(this);}
    internal static void OpenAboutLicenses()
    {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"THIRD_PARTY_NOTICES.md");
        if(File.Exists(path))Open(path);
        else {path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Licenses");if(Directory.Exists(path))Open(path);else MessageBox.Show("Las licencias se incluyen en el paquete completo de Con-E.","Con-E");}
    }
    static void AboutSmokeTest(MainForm owner,string directory)
    {
        using(var dialog=new AboutConE())
        {
            dialog.Show(owner);Application.DoEvents();
            var portraits=dialog.Controls.Find("cone-original-portrait",true);
            if(portraits.Length!=1||((PictureBox)portraits[0]).Image==null||((PictureBox)portraits[0]).Image.Height<=((PictureBox)portraits[0]).Image.Width)throw new InvalidOperationException("La ilustración original no está incorporada");
            foreach(int width in new[]{760,600})
            {
                dialog.Width=width;Application.DoEvents();
                using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(directory,"acerca-de-"+width+".png"));}
            }
            dialog.Close();
        }
    }
}

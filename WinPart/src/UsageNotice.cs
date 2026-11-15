// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class UsageNotice : Form
{
    public UsageNotice()
    {
        Text="WinPart · Uso técnico responsable";Font=new Font("Segoe UI",10);Size=new Size(640,430);MinimumSize=new Size(540,380);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;MinimizeBox=false;MaximizeBox=false;
        Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var body=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White,BorderStyle=BorderStyle.None,Text="WinPart es una herramienta de soporte para personas con conocimientos de discos y particiones.\r\n\r\nEliminar, formatear o vaciar un disco puede causar pérdida de datos y dejar sistemas sin arrancar. Verifica el disco, las particiones y tus respaldos antes de aplicar.\r\n\r\nLas operaciones se preparan en una cola. Sólo se ejecutan tras confirmar Aplicar; si hacen falta permisos, se solicitan en ese momento. Las operaciones ya ejecutadas no se deshacen automáticamente.\r\n\r\nAvanzadas permite eliminar tipos protegidos en discos ajenos al Windows actual. No descifra archivos ni sustituye herramientas especializadas.\r\n\r\nSoftware libre distribuido SIN GARANTÍA conforme a la licencia GPL incluida en COPYING. El usuario debe evaluar las operaciones y emplear la herramienta de forma responsable."};
        var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
        var accept=new Button {Text="Entiendo · Continuar",AutoSize=true,DialogResult=DialogResult.OK};var quit=new Button {Text="Salir",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(accept);buttons.Controls.Add(quit);AcceptButton=accept;CancelButton=quit;
        var pad=new Panel {Dock=DockStyle.Fill,Padding=new Padding(18),BackColor=Color.White};pad.Controls.Add(body);Controls.Add(pad);Controls.Add(buttons);
    }
}

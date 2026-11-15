using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UIModernization;

internal sealed partial class MainForm
{
    string selectedPhysicalKey,selectedCardTitle;
    void WireCard(Control c,ModernCardPanel card)
    {
        c.MouseDown+=(s,e)=>ActivateCard(card);c.GotFocus+=(s,e)=>ActivateCard(card);
        c.ControlAdded+=(s,e)=>WireCard(e.Control,card);foreach(Control child in c.Controls)WireCard(child,card);
    }
    void ActivateCard(ModernCardPanel selected)
    {
        selectedCardTitle=Convert.ToString(selected.Tag);foreach(var card in cards.OfType<ModernCardPanel>())card.Selected=ReferenceEquals(card,selected);
    }
    static void WireDiskOpen(Control control,Action open){if(control is Button)return;control.Cursor=Cursors.Hand;control.Click+=(s,e)=>open();foreach(Control child in control.Controls)WireDiskOpen(child,open);}
    List<FlowLayoutPanel> RenderPhysicalDiskCards()
    {
        var result=new List<FlowLayoutPanel>();
        if(current==null||current.PhysicalDisks.Count==0)
        {var empty=Card("","Almacenamiento");Line(empty,current==null?"Pendiente de lectura":"No se pudo obtener el inventario físico. Revisa las incidencias.");result.Add(empty);return result;}
        if(selectedPhysicalKey==null)selectedPhysicalKey=current.PhysicalDisks[0].Key;
        foreach(var disk in current.PhysicalDisks)
        {
            var d=disk;var card=(ModernCardPanel)Card("","Disco "+d.Number+" · "+d.Kind);result.Add(card);
            card.Controls.Add(new DiskMiniature {Kind=d.Art,Width=170,Height=128,Margin=new Padding(0,0,0,8),AccessibleName=d.Kind+" · miniatura ilustrativa"});
            Line(card,d.Model,true);Line(card,d.Size>0?StorageFormat.Size(d.Size):"Capacidad no disponible",true);Line(card,d.Bus+" · "+d.Style);Line(card,d.Form);
            var volumes=d.Partitions.Where(p=>p.Letter.Length>0).Select(p=>p.Title).ToArray();Line(card,volumes.Length>0?string.Join(" · ",volumes):d.Partitions.Count+" partición(es) · sin letra de unidad");
            Action open=()=>{selectedPhysicalKey=d.Key;ActivateCard(card);OpenPhysicalStorage(d);};
            card.Controls.Add(ActionButton("Ver disco y volúmenes",open));WireDiskOpen(card,open);
            card.Selected=d.Key==selectedPhysicalKey;
            Help(card,"Miniatura orientativa. El modelo y la conexión no prueban el formato físico. El detalle conserva particiones sin letra y no modifica el disco.");
        }
        return result;
    }
}

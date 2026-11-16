using System;
using System.Drawing;
using System.Reflection;

internal static class AppVisualIdentity
{
    static readonly Lazy<Icon> Cached=new Lazy<Icon>(()=>{
        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("ConE.AppIcon"))
        {
            if(resource==null)throw new InvalidOperationException("No se encontró el icono de Con-E.");
            using(var icon=new Icon(resource,new Size(64,64)))return (Icon)icon.Clone();
        }
    });
    internal static Icon MainIcon {get{return Cached.Value;}}
}

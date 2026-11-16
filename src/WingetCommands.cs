using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

internal static class WingetCommands
{
    internal static ProcessStartInfo Request(bool upgrade)
    {
        string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
        return new ProcessStartInfo {
            FileName=Path.Combine(system,"cmd.exe"),WorkingDirectory=system,
            Arguments=upgrade?"/d /k winget upgrade --all":"/d /k winget upgrade",
            UseShellExecute=true,Verb=upgrade?"runas":"open"
        };
    }
    internal static string Launch(bool upgrade,bool available,Action<ProcessStartInfo> starter=null)
    {
        if(!available)return "WinGet no está disponible en este usuario. No se instaló ningún componente.";
        try
        {
            var request=Request(upgrade);
            if(starter!=null)starter(request);else using(var process=Process.Start(request)){}
            return upgrade?"Actualización abierta con solicitud de administrador. Con-E permanece abierto.":"Consulta de actualizaciones abierta sin elevar permisos.";
        }
        catch(Win32Exception ex) when(ex.NativeErrorCode==1223){return "Solicitud de administrador cancelada. No se inició una nueva actualización.";}
        catch(Exception ex){return "No se pudo abrir WinGet: "+ex.Message;}
    }
}

using System;
using System.Globalization;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UIModernization;

internal sealed class CpuMetric
{
    public float? Percent;public string Detail="";public DateTime Time=DateTime.Now;
}
internal sealed partial class MainForm
{
    ModernProgressBar barCpuUsage;
    Label lblCpuValue,lblCpuSample;
    CpuMetric lastCpuMetric=new CpuMetric();
    CancellationTokenSource cpuMonitorCancellation;
    Task cpuMonitorTask;
    void StartCpuMonitor(Func<CpuMetric> reader=null,int interval=1500)
    {
        if(cpuMonitorTask!=null&&!cpuMonitorTask.IsCompleted)return;
        cpuMonitorCancellation=new CancellationTokenSource();cpuMonitorTask=MonitorCpuAsync(reader??GetCpuUsageFromWmi,interval,cpuMonitorCancellation.Token);
    }
    async Task MonitorCpuAsync(Func<CpuMetric> reader,int interval,CancellationToken token)
    {
        try
        {
            while(!token.IsCancellationRequested)
            {
                CpuMetric sample;
                try{sample=await Task.Run(reader,token);}
                catch(OperationCanceledException){break;}
                catch(Exception ex){sample=new CpuMetric {Detail=ex.Message};}
                if(token.IsCancellationRequested||IsDisposed||Disposing)return;
                // Called from Shown: await resumes on the WinForms synchronization context.
                lastCpuMetric=sample;UpdateCpuControls(sample);
                await Task.Delay(interval,token); // One awaited query at a time; no overlapping timer callbacks.
            }
        }
        catch(OperationCanceledException){}
    }
    void StopCpuMonitor()
    {
        var cancellation=cpuMonitorCancellation;if(cancellation==null)return;cpuMonitorCancellation=null;cancellation.Cancel();
        if(cpuMonitorTask!=null)cpuMonitorTask.ContinueWith(t=>cancellation.Dispose(),TaskScheduler.Default);else cancellation.Dispose();
    }
    void UpdateCpuControls(CpuMetric sample)
    {
        if(IsDisposed||Disposing||barCpuUsage==null||barCpuUsage.IsDisposed||lblCpuValue==null)return;
        if(InvokeRequired){if(IsHandleCreated)BeginInvoke(new Action(()=>UpdateCpuControls(sample)));return;}
        barCpuUsage.IsAvailable=sample.Percent.HasValue;
        if(sample.Percent.HasValue)barCpuUsage.Value=sample.Percent.Value;
        barCpuUsage.AccessibleName=sample.Percent.HasValue?"Uso real de CPU: "+sample.Percent.Value.ToString("0.0")+" %":"Uso de CPU no disponible";
        lblCpuValue.Text=sample.Percent.HasValue?sample.Percent.Value.ToString("0.0")+" %":"Sin lectura";
        lblCpuSample.Text=sample.Percent.HasValue?"Uso de CPU · WMI · "+sample.Time.ToString("HH:mm:ss"):"CPU: esperando lectura WMI / reintentando";
        lblCpuSample.AccessibleDescription=sample.Detail;
    }
    static CpuMetric GetCpuUsageFromWmi()
    {
        // Query and dispose WMI objects entirely on the worker thread.
        using(var query=new ManagementObjectSearcher(new ManagementScope(@"\\.\root\cimv2"),new ObjectQuery("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"),new EnumerationOptions {Timeout=TimeSpan.FromSeconds(3),ReturnImmediately=false}))
        using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
        {
            float value;if(float.TryParse(Convert.ToString(row["PercentProcessorTime"],CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value))return new CpuMetric {Percent=Math.Max(0,Math.Min(100,value))};
        }
        return new CpuMetric {Detail="El proveedor WMI no entregó una muestra de CPU."};
    }
}

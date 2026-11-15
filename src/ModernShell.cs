using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UIModernization;

internal sealed partial class MainForm
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    void InitializeModernUI()
    {
        BackColor=ModernTheme.Canvas;ForeColor=ModernTheme.Text;actions.BackColor=ModernTheme.Surface;status.BackColor=ModernTheme.Surface;status.ForeColor=ModernTheme.Muted;
        ApplyModernTheme(this);HandleCreated+=(s,e)=>LightTitleBar();if(IsHandleCreated)LightTitleBar();
        FormClosed+=(s,e)=>StopCpuMonitor();Disposed+=(s,e)=>StopCpuMonitor();
    }
    void LightTitleBar(){try{int enabled=0;DwmSetWindowAttribute(Handle,20,ref enabled,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
    static void ApplyModernTheme(Control root)
    {
        foreach(Control c in root.Controls)
        {
            if(c is ListView||c is TreeView){c.BackColor=ModernTheme.Surface;c.ForeColor=ModernTheme.Text;}
            else if(c is Panel||c is TabPage||c is SplitContainer)
            {if(!(c is ModernCardPanel)&&c.BackColor!=ModernTheme.Surface&&c.BackColor!=ModernTheme.Raised&&c.BackColor!=ModernTheme.Canvas)c.BackColor=ModernTheme.Canvas;}
            if(c is Label&&c.ForeColor==SystemColors.ControlText)c.ForeColor=ModernTheme.Muted;
            ApplyModernTheme(c);
        }
    }
    static void StyleAction(Button button)
    {
        button.FlatStyle=FlatStyle.Flat;button.BackColor=ModernTheme.Raised;button.ForeColor=ModernTheme.Accent;button.FlatAppearance.BorderColor=ModernTheme.Border;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(228,238,252);button.FlatAppearance.MouseDownBackColor=Color.FromArgb(213,229,250);
    }
}

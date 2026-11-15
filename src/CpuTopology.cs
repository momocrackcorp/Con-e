using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
internal static class CpuTopology
{
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetLogicalProcessorInformationEx(int relationship,IntPtr buffer,ref uint length);
    internal static List<int> Read()
    {
        var result=new List<int>();uint bytes=0;GetLogicalProcessorInformationEx(0,IntPtr.Zero,ref bytes);if(bytes==0||bytes>16777216)return result;
        IntPtr buffer=Marshal.AllocHGlobal((int)bytes);
        try
        {
            if(!GetLogicalProcessorInformationEx(0,buffer,ref bytes))return result;
            int offset=0;
            while(offset+32<=bytes)
            {
                IntPtr record=IntPtr.Add(buffer,offset);int size=Marshal.ReadInt32(record,4);if(size<32||offset+size>bytes)break;
                if(Marshal.ReadInt32(record)==0)
                {
                    int groups=(ushort)Marshal.ReadInt16(record,30),threads=0;
                    for(int i=0;i<groups;i++)
                    {
                        int maskOffset=32+i*(IntPtr.Size+8);if(maskOffset+IntPtr.Size>size)break;
                        ulong mask=IntPtr.Size==8?unchecked((ulong)Marshal.ReadInt64(record,maskOffset)):(uint)Marshal.ReadInt32(record,maskOffset);
                        while(mask!=0){threads++;mask&=mask-1;}
                    }
                    if(threads>0)result.Add(threads);
                }
                offset+=size;
            }
        }
        finally{Marshal.FreeHGlobal(buffer);}return result;
    }
}
internal sealed class CoreDiagram : Control
{
    public readonly List<int> Threads;
    public CoreDiagram(IEnumerable<int> threads)
    {Threads=threads.ToList();SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);AccessibleName="Núcleos físicos e hilos lógicos: "+Threads.Count+" núcleos, "+Threads.Sum()+" hilos";Width=240;Resize+=(s,e)=>AdjustHeight();}
    void AdjustHeight(){int tile=Math.Max(82,Font.Height*4);int cols=Math.Max(1,Width/tile);int rows=(Math.Min(Threads.Count,256)+cols-1)/cols;int height=Math.Max(50,rows*(Font.Height*3+18));if(Height!=height)Height=height;}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(Threads!=null)AdjustHeight();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);int columns=Math.Max(1,Width/Math.Max(82,Font.Height*4));int width=Width/columns,height=Font.Height*3+18;
        for(int i=0;i<Math.Min(256,Threads.Count);i++)
        {
            var tile=new Rectangle(i%columns*width+3,i/columns*height+3,width-6,height-6);
            using(var fill=new SolidBrush(Color.FromArgb(233,241,253)))e.Graphics.FillRectangle(fill,tile);
            using(var pen=new Pen(Color.FromArgb(70,121,201)))e.Graphics.DrawRectangle(pen,tile);
            TextRenderer.DrawText(e.Graphics,"Núcleo "+(i+1),Font,new Rectangle(tile.X+3,tile.Y+3,tile.Width-6,Font.Height+4),Color.FromArgb(35,50,71),TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics,Threads[i]+(Threads[i]==1?" hilo":" hilos"),Font,new Rectangle(tile.X+3,tile.Y+Font.Height+7,tile.Width-6,Font.Height+4),Color.FromArgb(40,91,181),TextFormatFlags.HorizontalCenter);
            for(int t=0;t<Math.Min(Threads[i],8);t++)e.Graphics.FillRectangle(Brushes.SteelBlue,tile.X+8+t*10,tile.Bottom-9,6,5);
        }
    }
}

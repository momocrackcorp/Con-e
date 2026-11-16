// SPDX-License-Identifier: MIT
// Display-only partition hints. Does not prove that an OS is installed or bootable.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

internal static class PartitionIdentity
{
    static readonly HashSet<string> LinuxTypes=new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "0FC63DAF-8483-4772-8E79-3D69D8477DE4", // generic Linux filesystem
        "0657FD6D-A4AB-43C4-84E5-0933C84B4F4F", // swap
        "E6D6D379-F507-44C2-A23C-238F2A3DF928", // LVM
        "44479540-F297-41B2-9AF7-D131D5F0458A", // root x86
        "4F68BCE3-E8CD-4DB1-96E7-FBCAF984B709", // root x86-64
        "69DAD710-2CE4-4E3C-B16C-21A1D49ABED3", // root ARM
        "B921B045-1DF0-41C3-AF44-4C6F280D3FAE", // root ARM64
        "933AC7E1-2EB4-4F13-B844-0E14E2AEF915", // home
        "3B8F8425-20E0-4F3B-907F-1A25A76F98E8", // srv
        "A19D880F-05FC-4D3B-A006-743F0F84911E"  // Linux RAID
    };
    internal static string Family(string guid,string mbr,string fs,bool runningWindows)
    {
        if(runningWindows)return "windows";
        string type=(guid??"").Trim().Trim('{','}');
        if(type.Equals("C12A7328-F81F-11D2-BA4B-00A0C93EC93B",StringComparison.OrdinalIgnoreCase))return "";
        int code;bool linuxMbr=int.TryParse(mbr,out code)&&(code==130||code==131||code==142||code==253);
        if(LinuxTypes.Contains(type)||linuxMbr)return "linux";
        string format=(fs??"").Trim().ToLowerInvariant();
        return format=="ext2"||format=="ext3"||format=="ext4"||format=="btrfs"||format=="xfs"||format=="linux-swap"?"linux":"";
    }
    internal static string Caption(string family){return family=="windows"?"Windows en uso":family=="linux"?"Tipo Linux":"";}
    internal static Bitmap Icon(string family,int size)
    {
        var image=new Bitmap(size,size);using(var g=Graphics.FromImage(image))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/32f,size/32f);
            if(family=="windows")using(var blue=new SolidBrush(Color.FromArgb(0,120,215)))
            {g.FillRectangle(blue,3,3,12,12);g.FillRectangle(blue,17,3,12,12);g.FillRectangle(blue,3,17,12,12);g.FillRectangle(blue,17,17,12,12);}
            else if(family=="linux")
            {
                g.FillEllipse(Brushes.Black,5,7,22,23);g.FillEllipse(Brushes.Black,9,1,14,17);
                g.FillEllipse(Brushes.White,9,12,14,17);
                g.FillEllipse(Brushes.White,11,5,4,7);g.FillEllipse(Brushes.White,17,5,4,7);
                g.FillEllipse(Brushes.Black,13,7,2,3);g.FillEllipse(Brushes.Black,17,7,2,3);
                g.FillPolygon(Brushes.Goldenrod,new[]{new Point(12,12),new Point(20,12),new Point(16,16)});
                g.FillEllipse(Brushes.Goldenrod,3,26,12,5);g.FillEllipse(Brushes.Goldenrod,17,26,12,5);
            }
        }
        return image;
    }
    internal static void Verify()
    {
        if(Family("0fc63daf-8483-4772-8e79-3d69d8477de4","","",false)!="linux"||Family("","131","",false)!="linux"||Family("","","ext4",false)!="linux"||Family("","","NTFS",false)!=""||Family("","","exFAT",false)!=""||Family("C12A7328-F81F-11D2-BA4B-00A0C93EC93B","","FAT32",false)!=""||Family("","","NTFS",true)!="windows")throw new InvalidOperationException("Identificación visual de partición incorrecta");
    }
}

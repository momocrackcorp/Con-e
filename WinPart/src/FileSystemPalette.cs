// SPDX-License-Identifier: GPL-2.0-or-later
// Adapted from GParted src/Utils.cc, Utils::get_color(FSType).
// Copyright (C) 2004 Bart 'plors' Hakvoort
// Copyright (C) 2008, 2009, 2010, 2011 Curtis Gedak
// C# adaptation for WinPart, 2026. See COPYING and THIRD_PARTY_NOTICES.md.
using System;
using System.Collections.Generic;
using System.Drawing;
internal static class FileSystemPalette
{
    static readonly Dictionary<string,string> Colors=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        { "UNSUPPORTED", "#000000" },
        { "UNALLOCATED", "#A9A9A9" },
        { "INACTIVE", "#555555" },
        { "UNKNOWN", "#000000" },
        { "UNFORMATTED", "#000000" },
        { "CLEARED", "#000000" },
        { "OTHER", "#000000" },
        { "EXTENDED", "#95E3E5" },
        { "BCACHEFS", "#C26825" },
        { "BTRFS", "#E58749" },
        { "EXFAT", "#267726" },
        { "EXT2", "#7590AE" },
        { "EXT3", "#4B6983" },
        { "EXT4", "#314E6C" },
        { "F2FS", "#DF421E" },
        { "FAT16", "#60C160" },
        { "FAT32", "#46A046" },
        { "HFS", "#D59FD4" },
        { "HFSPLUS", "#B173B0" },
        { "JFS", "#D6B129" },
        { "LINUX_SWAP", "#C1665A" },
        { "LUKS", "#625B81" },
        { "LVM2_PV", "#B39169" },
        { "MINIX", "#9DB8D2" },
        { "NILFS2", "#990000" },
        { "NTFS", "#70D2B1" },
        { "REISER4", "#887FA3" },
        { "REISERFS", "#ADA7C8" },
        { "UDF", "#105210" },
        { "XFS", "#EED680" },
        { "APFS", "#874986" },
        { "ATARAID", "#5A4733" },
        { "BCACHE", "#E0C39E" },
        { "BITLOCKER", "#494066" },
        { "GRUB2_CORE_IMG", "#7F7F7F" },
        { "ISO9660", "#D3D3D3" },
        { "JBD", "#314E6C" },
        { "LINUX_SWRAID", "#826647" },
        { "LINUX_SWSUSPEND", "#884631" },
        { "LVM2_THINPOOL", "#A78463" },
        { "REFS", "#3EA281" },
        { "UFS", "#AA8F2C" },
        { "ZFS", "#984F18" },
        { "USED", "#F8F8BA" },
        { "UNUSED", "#FFFFFF" }
    };
    internal static Color Get(string fs)
    {
        string key=(fs??"UNKNOWN").Replace("-","_").ToUpperInvariant();
        if(key=="FAT")key="FAT16";
        string hex;return ColorTranslator.FromHtml(Colors.TryGetValue(key,out hex)?hex:Colors["UNKNOWN"]);
    }
    internal static readonly Color Used=Get("USED"),Unused=Get("UNUSED");
}

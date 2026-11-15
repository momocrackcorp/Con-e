# GParted — attribution and changes

WinPart is distributed as free software under GPL-2.0-or-later. See COPYING. There is no purchase requirement or paid feature tier in this release.

The C# lookup in src/FileSystemPalette.cs is adapted from `GParted::Utils::get_color(FSType)` in GNOME GParted, src/Utils.cc. Original copyright notices:

- Copyright (C) 2004 Bart 'plors' Hakvoort
- Copyright (C) 2008, 2009, 2010, 2011 Curtis Gedak

GParted license: GNU GPL version 2 or, at your option, any later version. The function was translated from a C++ switch to a C# dictionary and System.Drawing.Color lookup. Original names and palette values are retained. Other drawing and Windows-specific code is WinPart code. GParted/libparted's Linux disk operations and file system utilities are not incorporated or ported in this release.

Upstream: https://gitlab.gnome.org/GNOME/gparted
Mirror: https://github.com/GNOME/gparted

Upstream commit inspected: 484ac562bdaddf0e9c54a1f47b5366f0a72b4dc8
Source: https://github.com/GNOME/gparted/blob/484ac562bdaddf0e9c54a1f47b5366f0a72b4dc8/src/Utils.cc

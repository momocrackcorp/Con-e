"""Read-only Linux inventory. No installers, updates or partition writes."""
from __future__ import annotations
import csv
import datetime as dt
import getpass
import io
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import threading
import time
import psutil

NA = "No disponible"
VERSION = "0.1.0"

def size(value):
    if value is None:
        return NA
    n = float(value)
    for unit in ("B", "KB", "MB", "GB", "TB", "PB"):
        if abs(n) < 1000 or unit == "PB":
            return f"{n:.1f} {unit}" if unit != "B" else f"{n:.0f} B"
        n /= 1000

def read_text(path, default=NA):
    try:
        value = Path(path).read_text(errors="replace").strip()
        return value or default
    except (OSError, ValueError):
        return default

def command(args, timeout=7):
    """Fixed argv only; never execute a shell or use the user's PATH for root."""
    search = "/usr/sbin:/usr/bin:/sbin:/bin" if os.geteuid() == 0 else os.environ.get("PATH", "")
    executable = shutil.which(args[0], path=search)
    if not executable:
        return "", f"{args[0]}: no instalado"
    try:
        result = subprocess.run([executable, *args[1:]], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=timeout,
            env={**os.environ, "LC_ALL": "C"})
        # smartctl's status is a bitmask; JSON may be useful even with nonzero status.
        return result.stdout[:8_000_000], result.stderr.strip()[:1000] if result.returncode else ""
    except (OSError, subprocess.TimeoutExpired) as exc:
        return "", f"{args[0]}: {type(exc).__name__}"

def parse_cpu(text):
    rows = {}
    for line in text.splitlines():
        key, sep, value = line.partition(":")
        if sep:
            rows[key.strip()] = value.strip()
    return rows

def parse_pci(text):
    devices = []
    for line in text.splitlines():
        try:
            fields = shlex.split(line)
        except ValueError:
            continue
        if len(fields) >= 4:
            devices.append({"slot": fields[0], "class": fields[1],
                            "vendor": fields[2], "model": fields[3]})
    return devices

def disk_kind(node):
    name = str(node.get("kname") or node.get("name") or "")
    bus = str(node.get("tran") or "").lower()
    if bus == "usb":
        return "usb", "Dispositivo USB"
    if name.startswith("nvme") or bus == "nvme":
        return "m2-nvme", "SSD NVMe"
    if name.startswith("mmc"):
        return "sd", "SD / eMMC"
    rota = node.get("rota")
    if rota in (True, 1, "1"):
        return "hdd", "HDD"
    if rota in (False, 0, "0") and bus in ("sata", "ata", "sas", "scsi"):
        return "ssd", "SSD"
    return "generic", "Tipo no informado"

LINUX_GUIDS = {
    "0fc63daf-8483-4772-8e79-3d69d8477de4", "0657fd6d-a4ab-43c4-84e5-0933c84b4f4f",
    "4f68bce3-e8cd-4db1-96e7-fbcaf984b709", "44479540-f297-41b2-9af7-d131d5f0458a",
    "e6d6d379-f507-44c2-a23c-238f2a3df928", "933ac7e1-2eb4-4f13-b844-0e14e2aef915",
}
def partition_family(node):
    fs = str(node.get("fstype") or "").lower()
    guid = str(node.get("parttype") or "").lower().strip("{}")
    if guid == "c12a7328-f81f-11d2-ba4b-00a0c93ec93b":
        return "EFI"
    if fs in ("ext2", "ext3", "ext4", "btrfs", "xfs", "swap") or guid in LINUX_GUIDS or guid in ("0x82", "0x83", "0x8e"):
        return "Tipo Linux"
    # NTFS/FAT/exFAT are data formats, not proof of an installed Windows.
    return ""

def normalize_disks(document, usage=psutil.disk_usage):
    disks = []
    for raw in document.get("blockdevices", []):
        if raw.get("type") != "disk" or str(raw.get("name", "")).startswith(("loop", "zram")):
            continue
        kind, label = disk_kind(raw)
        disk = {"name": raw.get("kname") or raw.get("name"), "path": raw.get("path"),
                "model": str(raw.get("model") or raw.get("vendor") or "Dispositivo sin modelo").strip(),
                "size": int(raw.get("size") or 0), "serial": str(raw.get("serial") or "").strip(), "bus": raw.get("tran") or NA,
                "kind": kind, "kind_label": label, "volumes": [], "allocation": []}
        if not disk["path"]:
            disk["path"] = "/dev/" + disk["name"]
        direct = raw.get("children") or []
        allocation = [n for n in direct if n.get("type") == "part"]
        if not direct and raw.get("fstype"):
            allocation = [raw]
        allocated = sum(int(n.get("size") or 0) for n in allocation)
        for node in allocation:
            disk["allocation"].append({"name": node.get("name") or "",
                "size": int(node.get("size") or 0), "type": node.get("fstype") or partition_family(node) or node.get("type") or NA})
        if allocation and allocated <= disk["size"]:
            disk["allocation"].append({"name": "Sin asignar / metadatos", "size": disk["size"]-allocated, "type": ""})
        def descend(node):
            mounts = node.get("mountpoints") or ([node["mountpoint"]] if node.get("mountpoint") else [])
            mounts = [m for m in mounts if isinstance(m, str) and m.startswith("/")]
            volume = {"name": node.get("name") or NA, "type": node.get("type") or NA,
                "size": int(node.get("size") or 0), "fs": node.get("fstype") or NA,
                "label": node.get("label") or "", "mounts": mounts, "total": None,
                "used": None, "free": None, "family": partition_family(node)}
            if "/" in mounts:
                volume["family"] = "Linux en uso"
            if mounts:
                try:
                    u = usage(mounts[0])
                    volume.update(total=u.total, used=u.used, free=u.free)
                except (OSError, PermissionError):
                    pass
            disk["volumes"].append(volume)
            for child in node.get("children") or []:
                descend(child)
        for node in direct or ([raw] if raw.get("fstype") else []):
            descend(node)
        disks.append(disk)
    return disks

def dmi_inventory():
    base = Path("/sys/class/dmi/id")
    fields = {"Fabricante": "sys_vendor", "Modelo": "product_name", "Placa": "board_name",
              "Fabricante placa": "board_vendor", "BIOS / UEFI": "bios_vendor",
              "Versión firmware": "bios_version", "Fecha firmware": "bios_date",
              "Serie equipo": "product_serial", "Serie placa": "board_serial"}
    return {label: read_text(base / filename) for label, filename in fields.items()}

def gpu_inventory(pci):
    result = []
    for device in pci:
        if any(word in device["class"].lower() for word in ("vga", "3d controller", "display controller")):
            gpu = {**device, "vram": None}
            for card in Path("/sys/class/drm").glob("card[0-9]*"):
                if "-" in card.name:
                    continue
                try:
                    slot = (card / "device").resolve().name.removeprefix("0000:")
                    if slot == device["slot"].removeprefix("0000:"):
                        value = read_text(card / "device/mem_info_vram_total", "")
                        if value.isdigit():
                            gpu["vram"] = int(value)
                except OSError:
                    pass
            result.append(gpu)
    return result

def collect(cancel=None):
    cancel = cancel or threading.Event()
    warnings = []
    def get(args):
        if cancel.is_set():
            raise InterruptedError()
        out, error = command(args)
        if error:
            warnings.append(error)
        return out
    cpu = parse_cpu(get(["lscpu"]))
    pci = parse_pci(get(["lspci", "-mm"]))
    raw = get(["lsblk", "--json", "--bytes", "--output",
        "NAME,KNAME,PATH,TYPE,SIZE,MODEL,VENDOR,SERIAL,TRAN,ROTA,RM,FSTYPE,LABEL,PARTTYPE,MOUNTPOINTS"])
    try:
        disks = normalize_disks(json.loads(raw))
    except (ValueError, TypeError, KeyError):
        disks = []
        warnings.append("No se pudo leer el inventario de discos.")
    try:
        release = platform.freedesktop_os_release()
    except OSError:
        release = {}
    networks = []
    stats = psutil.net_if_stats()
    for name, addresses in psutil.net_if_addrs().items():
        if name == "lo":
            continue
        info = stats.get(name)
        networks.append({"name": name, "up": bool(info and info.isup),
            "speed": info.speed if info and info.speed > 0 else None,
            "addresses": [a.address for a in addresses]})
    sound = read_text("/proc/asound/cards")
    usb = get(["lsusb"])
    snapshot = {"captured": dt.datetime.now().astimezone().isoformat(timespec="seconds"),
        "cpu": cpu, "memory": psutil.virtual_memory()._asdict(),
        "gpus": gpu_inventory(pci), "pci": pci, "disks": disks,
        "board": dmi_inventory(), "usb": usb.splitlines(), "audio": sound,
        "networks": networks, "system": {"Distribución": release.get("PRETTY_NAME", NA),
        "Kernel": platform.release(), "Arquitectura": platform.machine(),
        "Equipo": platform.node(), "Usuario": getpass.getuser(),
        "Último arranque": dt.datetime.fromtimestamp(psutil.boot_time()).astimezone().strftime("%d-%m-%Y %H:%M:%S"),
        "Instalación del sistema": NA},
        "modules": [], "smart": {}, "warnings": warnings}
    return snapshot

def metrics():
    memory = psutil.virtual_memory()
    temperatures = []
    fans = []
    try:
        for chip, readings in psutil.sensors_temperatures().items():
            for item in readings:
                if item.current is not None:
                    temperatures.append({"name": f"{chip} · {item.label or 'Sensor'}", "value": item.current})
    except (OSError, AttributeError):
        pass
    try:
        for chip, readings in psutil.sensors_fans().items():
            fans.extend({"name": f"{chip} · {r.label or 'Ventilador'}", "value": r.current} for r in readings)
    except (OSError, AttributeError):
        pass
    try:
        freq = psutil.cpu_freq()
    except (OSError, NotImplementedError):
        freq = None
    result = {"cpu": psutil.cpu_percent(interval=0.15), "memory_percent": memory.percent,
              "memory_used": memory.total-memory.available, "memory_free": memory.available,
              "temperatures": temperatures, "fans": fans,
              "frequency": freq.current if freq else None}
    # nvidia-smi queries only; absence of NVIDIA does not imply an error.
    if shutil.which("nvidia-smi"):
        out, _ = command(["nvidia-smi", "--query-gpu=name,temperature.gpu,memory.total,memory.used",
                          "--format=csv,noheader,nounits"], timeout=3)
        result["nvidia"] = []
        for row in csv.reader(io.StringIO(out)):
            if len(row) == 4:
                try:
                    result["nvidia"].append({"name": row[0].strip(), "temperature": float(row[1]),
                                            "vram": int(row[2])*1048576, "used": int(row[3])*1048576})
                except ValueError:
                    pass
    return result

def parse_memory_modules(text):
    modules = []
    for block in re.split(r"\n\s*\n", text):
        if "Memory Device" not in block:
            continue
        fields = {}
        for line in block.splitlines():
            key, sep, value = line.strip().partition(":")
            if sep:
                fields[key.strip()] = value.strip()
        if not fields or fields.get("Size") in (None, "No Module Installed"):
            continue
        modules.append({k: fields.get(k, NA) for k in
            ("Locator", "Size", "Type", "Speed", "Configured Memory Speed", "Manufacturer", "Part Number")})
    return modules

def parse_smart(document):
    health = document.get("smart_status", {}).get("passed")
    # NVMe critical_warning and percentage_used have explicit semantics.
    nvme = document.get("nvme_smart_health_information_log") or {}
    if health is None and isinstance(nvme.get("critical_warning"), int):
        health = nvme["critical_warning"] == 0
    used = nvme.get("percentage_used")
    return {"serial": str(document.get("serial_number") or "").strip(), "health": "Correcto según SMART" if health is True else "Advertencia SMART" if health is False else NA,
        "temperature": document.get("temperature", {}).get("current", nvme.get("temperature")),
        "hours": document.get("power_on_time", {}).get("hours", nvme.get("power_on_hours")),
        "remaining": max(0, min(100, 100-used)) if isinstance(used, (int, float)) else None,
        "read_bytes": nvme.get("data_units_read")*512000 if isinstance(nvme.get("data_units_read"), (int, float)) else None,
        "written_bytes": nvme.get("data_units_written")*512000 if isinstance(nvme.get("data_units_written"), (int, float)) else None}

def privileged():
    """Called only through the explicit polkit button. All commands are reads."""
    if os.geteuid() != 0:
        raise PermissionError("La lectura ampliada requiere autorización del sistema.")
    text, error = command(["dmidecode", "--type", "memory"])
    result = {"modules": parse_memory_modules(text), "smart": {}, "warnings": [error] if error else []}
    out, error = command(["smartctl", "--scan-open", "--json"])
    try:
        candidates = json.loads(out).get("devices", [])
    except ValueError:
        candidates = []
        result["warnings"].append(error or "smartctl no entregó dispositivos.")
    for item in candidates[:32]:
        path = item.get("name", "")
        if not re.fullmatch(r"/dev/(?:sd[a-z]+|nvme\d+|nvme\d+n\d+|mmcblk\d+|vd[a-z]+)", path):
            continue
        data, error = command(["smartctl", "--all", "--json", path], timeout=8)
        try:
            parsed = json.loads(data)
            result["smart"][path] = parse_smart(parsed)
        except ValueError:
            result["warnings"].append(error or f"Sin lectura SMART: {path}")
    return result

def scan_root(path, cancel=None, progress=None):
    """Allocated bytes, unique file identities, no symlinks or other filesystems."""
    cancel = cancel or threading.Event()
    path = os.path.realpath(path)
    root_dev = os.stat(path).st_dev
    groups, seen_files, seen_dirs = {}, set(), set()
    skipped = 0
    files = 0
    started = time.monotonic()
    def check():
        if cancel.is_set():
            raise InterruptedError("Análisis cancelado")
    with os.scandir(path) as entries:
        roots = sorted(entries, key=lambda e: e.name)
    stack = [(e.path, e.name if e.is_dir(follow_symlinks=False) else "Archivos en raíz") for e in reversed(roots)]
    last = 0
    while stack:
        check()
        current, group = stack.pop()
        try:
            st = os.lstat(current)
            if os.path.islink(current) or st.st_dev != root_dev:
                skipped += 1
                continue
            identity = (st.st_dev, st.st_ino)
            if os.path.isdir(current):
                if identity in seen_dirs:
                    continue
                seen_dirs.add(identity)
                groups.setdefault(group, 0)
                with os.scandir(current) as entries:
                    stack.extend((e.path, group) for e in entries)
            elif os.path.isfile(current):
                if identity in seen_files:
                    continue
                seen_files.add(identity)
                files += 1
                groups[group] = groups.get(group, 0) + st.st_blocks*512
        except OSError:
            skipped += 1
        now = time.monotonic()
        if progress and now-last > 0.3:
            progress(group, files, skipped)
            last = now
    return {"groups": sorted(groups.items(), key=lambda p: p[1], reverse=True), "files": files,
            "skipped": skipped, "seconds": time.monotonic()-started}

def fixture():
    gb = 1_000_000_000
    return {"captured": "2026-11-16T12:00:00", "cpu": {"Model name": "Procesador de ejemplo",
        "CPU(s)": "8", "Core(s) per socket": "4", "Socket(s)": "1", "Architecture": "x86_64"},
        "memory": {"total": 16*gb, "available": 10*gb}, "gpus": [{"slot": "00:02.0",
        "class": "VGA", "vendor": "Intel", "model": "GPU de ejemplo", "vram": None}],
        "pci": [{"slot":"00:02.0","class":"VGA","vendor":"Intel","model":"GPU de ejemplo"}],
        "disks": [{"name": "nvme0n1", "path": "/dev/nvme0n1", "model": "SSD NVMe · ejemplo",
            "size": 500*gb, "bus": "nvme", "kind": "m2-nvme", "kind_label": "SSD NVMe",
            "allocation": [{"name":"nvme0n1p1","size":gb,"type":"vfat"},
                {"name":"nvme0n1p2","size":499*gb,"type":"ext4"}],
            "volumes": [{"name":"nvme0n1p1","type":"part","size":gb,"fs":"vfat","label":"EFI",
                "mounts":["/boot/efi"],"total":gb,"used":100_000_000,"free":900_000_000,"family":"EFI"},
                {"name":"nvme0n1p2","type":"part","size":499*gb,"fs":"ext4","label":"Mint",
                "mounts":["/"],"total":499*gb,"used":125*gb,"free":374*gb,"family":"Linux en uso"}]},
            {"name":"sdb","path":"/dev/sdb","model":"Memoria USB · ejemplo","size":32*gb,"bus":"usb",
            "kind":"usb","kind_label":"Dispositivo USB","allocation":[{"name":"sdb1","size":32*gb,"type":"exFAT"}],
            "volumes":[{"name":"sdb1","type":"part","size":32*gb,"fs":"exfat","label":"USB",
            "mounts":[],"total":None,"used":None,"free":None,"family":""}]}],
        "board": {"Fabricante":"Equipo de ejemplo","Modelo":"Con-E Linux","Placa":"Placa de ejemplo",
            "BIOS / UEFI":"UEFI","Versión firmware":"1.0","Fecha firmware":"No disponible"},
        "usb": ["Bus 001 Device 002: Dispositivo de ejemplo"], "audio": "Audio integrado · ejemplo",
        "networks": [{"name":"enp3s0","up":True,"speed":1000,"addresses":["192.0.2.10"]}],
        "system":{"Distribución":"Linux Mint · datos simulados","Kernel":"Linux · ejemplo",
        "Arquitectura":"x86_64","Equipo":"EJEMPLO","Usuario":"Usuario de ejemplo",
        "Último arranque":"16-11-2026 09:00:00","Instalación del sistema":NA},
        "modules":[],"smart":{},"warnings":[]}

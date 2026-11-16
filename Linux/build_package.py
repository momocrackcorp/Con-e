"""Build tar/deb from the already-tested PyInstaller directory."""
from pathlib import Path
import hashlib
import importlib.metadata as metadata
import shutil
import subprocess
import tarfile
import backend

root=Path(__file__).resolve().parent
bundle=root/"dist/Con-E-Linux"
target=root/"release"
target.mkdir(exist_ok=True)
shutil.copy(root/"README.md",bundle/"README.md")
shutil.copy(root/"THIRD_PARTY_NOTICES.md",bundle/"THIRD_PARTY_NOTICES.md")
source=bundle/"source"
source.mkdir(exist_ok=True)
for filename in ("app.py","backend.py","requirements.txt","test_backend.py","README.md","THIRD_PARTY_NOTICES.md","build_package.py"):
    shutil.copy(root/filename,source/filename)
shutil.copytree(root/"assets",source/"assets",dirs_exist_ok=True)
licenses=bundle/"Licenses"
licenses.mkdir(exist_ok=True)
for name in ("PySide6","PySide6_Essentials","PySide6_Addons","shiboken6","psutil","pyinstaller"):
    dist=metadata.distribution(name)
    for item in dist.files or []:
        if any(word in item.name.lower() for word in ("license","copying","copyright")):
            original=Path(dist.locate_file(item))
            if original.is_file():
                out=licenses/name/Path(*item.parts[-3:])
                out.parent.mkdir(parents=True,exist_ok=True)
                shutil.copy(original,out)
launcher=bundle/"Iniciar-Con-E.sh"
launcher.write_text('#!/bin/sh\napp_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"\nexec "$app_dir/Con-E-Linux" "$@"\n')
launcher.chmod(0o755)
binary=bundle/"Con-E-Linux"
binary.chmod(0o755)
archive=target/f"Con-E-Linux-v{backend.VERSION}-x86_64.tar.gz"
with tarfile.open(archive,"w:gz") as tar:
    tar.add(bundle,arcname="Con-E-Linux")

debroot=root/"deb-root"
if debroot.exists():
    shutil.rmtree(debroot)
shutil.copytree(bundle,debroot/"opt/con-e-linux")
(debroot/"DEBIAN").mkdir()
control=f"""Package: con-e-linux
Version: {backend.VERSION}
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Momocrackcorp <131729248+momocrackcorp@users.noreply.github.com>
Depends: libc6 (>= 2.35), libgl1, libegl1, libglib2.0-0, libdbus-1-3, libfontconfig1, libxkbcommon0, libxkbcommon-x11-0, libxcb1, libxcb-cursor0, libxcb-icccm4, libxcb-image0, libxcb-keysyms1, libxcb-render-util0, libxcb-xinerama0, libxcb-xkb1
Recommends: pciutils, usbutils, dmidecode, smartmontools
Homepage: https://github.com/momocrackcorp/Con-e
Description: Diagnostico grafico de hardware para Ubuntu y Linux Mint
 Primera version de prueba de Con-E Linux. Inventario, CPU, RAM, GPU,
 discos, sensores, dispositivos PCI/USB e informes. Sin gestion de
 particiones, actualizacion de programas ni gestion de controladores.
"""
(debroot/"DEBIAN/control").write_text(control)
desktop=debroot/"usr/share/applications"
desktop.mkdir(parents=True)
(desktop/"con-e-linux.desktop").write_text("""[Desktop Entry]
Type=Application
Name=Con-E Linux
Comment=Diagnóstico e inventario de hardware
Exec=/opt/con-e-linux/Con-E-Linux
Icon=con-e-linux
Terminal=false
Categories=System;Monitor;
""")
icons=debroot/"usr/share/icons/hicolor/scalable/apps"
icons.mkdir(parents=True)
shutil.copy(root/"assets/app.svg",icons/"con-e-linux.svg")
deb=target/f"con-e-linux_{backend.VERSION}_amd64.deb"
subprocess.run(["dpkg-deb","--root-owner-group","--build",str(debroot),str(deb)],check=True)
checks=[]
for artifact in (archive,deb):
    checks.append(hashlib.sha256(artifact.read_bytes()).hexdigest()+"  "+artifact.name)
(target/"SHA256SUMS.txt").write_text("\n".join(checks)+"\n")

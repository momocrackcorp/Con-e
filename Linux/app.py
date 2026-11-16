from __future__ import annotations
import json
import os
from pathlib import Path
import sys
import threading
import traceback
import backend

# The elevated helper runs without creating or importing a graphical application.
if __name__ == "__main__" and "--privileged" in sys.argv:
    try:
        print(json.dumps(backend.privileged(), ensure_ascii=False))
    except Exception as exc:
        print(json.dumps({"error": str(exc)}, ensure_ascii=False))
        sys.exit(1)
    sys.exit(0)

from PySide6 import QtCore, QtGui, QtWidgets, QtSvg

Qt = QtCore.Qt
COLORS = ["#2d65c4", "#209b8b", "#e2a02d", "#8f5fbf", "#cc5e68", "#4f98bc", "#78909c"]
NA = backend.NA

def asset(filename):
    candidates = [Path(getattr(sys, "_MEIPASS", Path(__file__).parent))/"assets"/filename,
        Path(__file__).parent/"assets"/filename,
        Path(__file__).parent.parent/"Shared"/filename,
        Path(__file__).parent.parent/"src/Resources"/filename]
    return next((str(p) for p in candidates if p.exists()), "")

def label(text="", bold=False):
    item = QtWidgets.QLabel(str(text))
    item.setWordWrap(True)
    item.setTextFormat(Qt.PlainText)
    if bold:
        f = item.font()
        f.setBold(True)
        f.setPointSize(12)
        item.setFont(f)
    return item

def lines(data):
    return "\n".join(f"{k}: {v}" for k,v in data.items())

class Worker(QtCore.QThread):
    result = QtCore.Signal(object)
    failed = QtCore.Signal(str)
    progress = QtCore.Signal(str)
    def __init__(self, function):
        super().__init__()
        self.function = function
        self.cancel = threading.Event()
    def run(self):
        try:
            self.result.emit(self.function(self.cancel, self.progress))
        except InterruptedError:
            self.failed.emit("Lectura cancelada.")
        except Exception as exc:
            self.failed.emit(str(exc))

class Donut(QtWidgets.QWidget):
    def __init__(self):
        super().__init__()
        self.percent = None
        self.setMinimumSize(170,170)
        self.setMaximumHeight(190)
    def set_percent(self, value):
        self.percent = value
        self.update()
    def paintEvent(self, event):
        p = QtGui.QPainter(self)
        p.setRenderHint(QtGui.QPainter.Antialiasing)
        side = min(self.width(), self.height())-28
        box = QtCore.QRectF((self.width()-side)/2, (self.height()-side)/2, side, side)
        p.setPen(QtGui.QPen(QtGui.QColor("#e2e9f2"), 12))
        p.drawEllipse(box)
        if self.percent is not None:
            pct = max(0,min(100,float(self.percent)))
            pen=QtGui.QPen(QtGui.QColor("#2d65c4" if pct<90 else "#c67c24"),12)
            pen.setCapStyle(Qt.RoundCap)
            p.setPen(pen)
            p.drawArc(box,90*16,-int(pct*360*16/100))
        p.setPen(QtGui.QColor("#233247"))
        font = p.font()
        font.setPointSize(25)
        font.setBold(True)
        p.setFont(font)
        p.drawText(box,Qt.AlignCenter, f"{self.percent:.0f} %" if self.percent is not None else "—")

class Miniature(QtWidgets.QLabel):
    cache={}
    def __init__(self, kind, side=128):
        super().__init__()
        self.setFixedSize(side,side)
        self.setAlignment(Qt.AlignCenter)
        pix = self.get(kind)
        if not pix.isNull():
            self.setPixmap(pix.scaled(side,side,Qt.KeepAspectRatio,Qt.SmoothTransformation))
        else:
            self.setText("USB" if kind=="usb" else "Disco")
    @classmethod
    def get(cls,kind):
        if kind in cls.cache:
            return cls.cache[kind]
        if kind=="usb":
            pix=QtGui.QPixmap(asset("usb-miniature.png"))
        elif kind in ("hdd","ssd","m2-sata","m2-nvme"):
            atlas=QtGui.QPixmap(asset("disk-miniatures.png"))
            idx=("hdd","ssd","m2-sata","m2-nvme").index(kind)
            w,h=atlas.width()//2,atlas.height()//2
            pix=atlas.copy((idx%2)*w,(idx//2)*h,w,h)
        else:
            pix=QtGui.QPixmap()
        cls.cache[kind]=pix
        return pix

class Allocation(QtWidgets.QWidget):
    def __init__(self, rows, total):
        super().__init__()
        self.rows=rows
        self.total=total
        self.setMinimumHeight(165)
        self.setMinimumWidth(180)
    def paintEvent(self, event):
        p=QtGui.QPainter(self)
        top=6
        height=self.height()-12
        y=float(top)
        if not self.rows or not self.total:
            p.setPen(QtGui.QColor("#506279"))
            p.drawText(self.rect(),Qt.AlignCenter,"Particiones no informadas")
            return
        for i,row in enumerate(self.rows):
            h=max(0,min(height*row["size"]/self.total,top+height-y))
            p.fillRect(QtCore.QRectF(2,y,32,h),QtGui.QColor(COLORS[i%len(COLORS)]))
            y+=h
        p.setPen(QtGui.QColor("#233247"))
        font=p.font()
        font.setPointSize(9)
        p.setFont(font)
        for i,row in enumerate(self.rows[:4]):
            pos=top+i*38
            p.fillRect(43,pos+4,8,8,QtGui.QColor(COLORS[i%len(COLORS)]))
            p.setPen(QtGui.QColor("#233247"))
            title=f'{row["name"]} · {row["type"]}'.strip(" ·")
            rect=QtCore.QRect(57,pos,max(30,self.width()-60),18)
            text=p.fontMetrics().elidedText(title,Qt.ElideRight,rect.width())
            p.drawText(rect,Qt.AlignLeft|Qt.AlignVCenter,text)
            p.setPen(QtGui.QColor("#506279"))
            p.drawText(QtCore.QRect(57,pos+18,max(30,self.width()-60),18),
                Qt.AlignLeft|Qt.AlignVCenter, f'{backend.size(row["size"])} · {row["size"]/self.total*100:.1f} %')
        if len(self.rows)>4:
            p.drawText(43,self.height()-4,f"… {len(self.rows)-4} más en el detalle")

class ColorBar(QtWidgets.QWidget):
    def __init__(self):
        super().__init__()
        self.rows=[]
        self.setMinimumHeight(24)
        self.setMaximumHeight(24)
    def paintEvent(self,event):
        p=QtGui.QPainter(self)
        total=sum(max(0,row[1]) for row in self.rows)
        if not total:
            p.fillRect(self.rect(),QtGui.QColor("#e2e9f2"))
            return
        x=0.0
        for i,(_,count) in enumerate(self.rows):
            w=self.width()*max(0,count)/total
            p.fillRect(QtCore.QRectF(x,0,w,self.height()),QtGui.QColor(COLORS[i%len(COLORS)]))
            x+=w

class Card(QtWidgets.QFrame):
    def __init__(self,title,detail=""):
        super().__init__()
        self.setObjectName("card")
        self.box=QtWidgets.QVBoxLayout(self)
        self.box.setContentsMargins(18,16,18,16)
        self.box.setSpacing(10)
        header=QtWidgets.QHBoxLayout()
        header.addWidget(label(title,True),1)
        self.toggle=QtWidgets.QToolButton()
        self.toggle.setText("Mostrar detalles")
        self.toggle.setCheckable(True)
        header.addWidget(self.toggle,0,Qt.AlignRight)
        self.box.addLayout(header)
        self.details=label(detail)
        self.details.hide()
        self.toggle.toggled.connect(self.show_details)
        self.toggle.setVisible(bool(detail))
    def finish(self):
        self.box.addWidget(self.details)
        self.box.addStretch(1)
    def show_details(self,on):
        self.details.setVisible(on)
        self.toggle.setText("Ocultar detalles" if on else "Mostrar detalles")

class ResponsiveCards(QtWidgets.QWidget):
    def __init__(self,disk=False):
        super().__init__()
        self.grid=QtWidgets.QGridLayout(self)
        self.grid.setContentsMargins(0,0,0,0)
        self.grid.setSpacing(14)
        self.cards=[]
        self.disk=disk
        self.columns=0
    def replace(self,cards):
        while self.grid.count():
            item=self.grid.takeAt(0)
            if item.widget():
                item.widget().deleteLater()
        self.cards=cards
        self.columns=0
        self.arrange()
    def resizeEvent(self,event):
        self.arrange()
        super().resizeEvent(event)
    def arrange(self):
        columns=(2 if self.width()>=850 else 1) if self.disk else (3 if self.width()>=1080 else 2 if self.width()>=740 else 1)
        if columns==self.columns:
            return
        self.columns=columns
        for i,card in enumerate(self.cards):
            self.grid.addWidget(card,i//columns,i%columns)
        for col in range(3):
            self.grid.setColumnStretch(col,1 if col<columns else 0)

def table(headers):
    widget=QtWidgets.QTableWidget(0,len(headers))
    widget.setHorizontalHeaderLabels(headers)
    widget.setEditTriggers(QtWidgets.QAbstractItemView.NoEditTriggers)
    widget.setSelectionBehavior(QtWidgets.QAbstractItemView.SelectRows)
    widget.setAlternatingRowColors(True)
    widget.verticalHeader().hide()
    widget.horizontalHeader().setSectionResizeMode(QtWidgets.QHeaderView.ResizeToContents)
    widget.horizontalHeader().setStretchLastSection(True)
    return widget

def set_rows(widget,rows):
    widget.setRowCount(len(rows))
    for i,row in enumerate(rows):
        for j,value in enumerate(row):
            item=QtWidgets.QTableWidgetItem(str(value))
            item.setToolTip(str(value))
            widget.setItem(i,j,item)

class MainWindow(QtWidgets.QMainWindow):
    def __init__(self,smoke=False):
        super().__init__()
        self.smoke=smoke
        self.snapshot={}
        self.extra={}
        self.workers=[]
        self.metric_worker=None
        self.scan_worker=None
        self.inventory_worker=None
        self.scan_cache={}
        self.extremes={}
        self.priv_process=None
        self.closing=False
        self.setWindowTitle(f"Con-E Linux · Hardware · v{backend.VERSION}")
        self.setWindowIcon(QtGui.QIcon(asset("app.svg")))
        self.resize(1320,900)
        self.setMinimumSize(660,500)
        root=QtWidgets.QWidget()
        layout=QtWidgets.QVBoxLayout(root)
        layout.setContentsMargins(12,12,12,12)
        top=QtWidgets.QHBoxLayout()
        title=label("Con-E · Linux",True)
        top.addWidget(title)
        top.addStretch()
        self.refresh=QtWidgets.QPushButton("Actualizar hardware")
        self.refresh.clicked.connect(self.load_inventory)
        top.addWidget(self.refresh)
        export=QtWidgets.QPushButton("Guardar informe")
        export.clicked.connect(self.export)
        top.addWidget(export)
        self.extended=QtWidgets.QPushButton("Lectura ampliada…")
        self.extended.clicked.connect(self.read_privileged)
        top.addWidget(self.extended)
        layout.addLayout(top)
        self.tabs=QtWidgets.QTabWidget()
        layout.addWidget(self.tabs,1)
        home=QtWidgets.QScrollArea()
        home.setWidgetResizable(True)
        home.setFrameShape(QtWidgets.QFrame.NoFrame)
        homebody=QtWidgets.QWidget()
        homebox=QtWidgets.QVBoxLayout(homebody)
        self.general=ResponsiveCards()
        self.diskcards=ResponsiveCards(True)
        homebox.addWidget(self.general)
        homebox.addWidget(label("Discos físicos",True))
        homebox.addWidget(self.diskcards)
        homebox.addStretch()
        home.setWidget(homebody)
        self.tabs.addTab(home,"Mi equipo")
        self.build_storage()
        self.sensor_table=table(["Sensor","Actual","Mínimo sesión","Máximo sesión","Unidad"])
        self.tabs.addTab(self.sensor_table,"Sensores")
        self.inventory=QtWidgets.QTreeWidget()
        self.inventory.setHeaderLabels(["Componente / propiedad","Información"])
        self.inventory.setColumnWidth(0,310)
        self.tabs.addTab(self.inventory,"Inventario completo")
        self.build_about()
        self.status=QtWidgets.QLabel("Leyendo hardware…")
        self.status.setWordWrap(True)
        layout.addWidget(self.status)
        self.setCentralWidget(root)
        self.timer=QtCore.QTimer(self)
        self.timer.setInterval(1500)
        self.timer.timeout.connect(self.poll_metrics)
        if smoke:
            self.apply_snapshot(backend.fixture())
            self.apply_metrics({"cpu":24,"memory_percent":38,"memory_used":6_000_000_000,
                "memory_free":10_000_000_000,"temperatures":[{"name":"CPU · ejemplo","value":42}],
                "fans":[{"name":"Ventilador · ejemplo","value":1200}],"frequency":2800})
        else:
            self.load_inventory()
            self.timer.start()
    def launch(self,function,callback,message):
        worker=Worker(function)
        self.workers.append(worker)
        worker.result.connect(callback)
        worker.failed.connect(self.show_error)
        worker.finished.connect(lambda w=worker:self.worker_done(w))
        self.status.setText(message)
        worker.start()
        return worker
    def worker_done(self,worker):
        if worker in self.workers:
            self.workers.remove(worker)
        for attr in ("metric_worker","scan_worker","inventory_worker"):
            if getattr(self,attr,None) is worker:
                setattr(self,attr,None)
        if not self.inventory_worker:
            self.refresh.setEnabled(True)
        if not self.scan_worker:
            self.scan_button.setEnabled(bool(self.selected_volume() and self.selected_volume()["mounts"]))
            self.cancel_scan.setEnabled(False)
        worker.deleteLater()
    def show_error(self,message):
        self.status.setText(message)
    def load_inventory(self):
        if self.inventory_worker:
            return
        self.refresh.setEnabled(False)
        self.inventory_worker=self.launch(lambda cancel,progress:backend.collect(cancel),
            self.apply_snapshot,"Leyendo hardware en segundo plano…")
    def apply_snapshot(self,snapshot):
        self.snapshot=snapshot
        if self.extra:
            self.snapshot.update(self.extra)
        self.render_cards()
        self.populate_inventory()
        self.disk_selector.blockSignals(True)
        self.disk_selector.clear()
        for disk in snapshot["disks"]:
            self.disk_selector.addItem(f'{disk["name"]} · {disk["model"]}',disk)
        self.disk_selector.blockSignals(False)
        self.choose_disk()
        warnings=snapshot.get("warnings",[])
        self.status.setText("Lectura parcial · "+" | ".join(warnings[:3]) if warnings else "Lectura completada.")
        self.poll_metrics()
    def render_cards(self):
        s=self.snapshot
        cards=[]
        cpu=Card("Procesador",lines(s["cpu"]))
        self.cpu_value=label("— %",True)
        self.cpu_bar=QtWidgets.QProgressBar()
        self.cpu_bar.setTextVisible(False)
        self.cpu_temp=label("Temperatura: "+NA)
        cpu.box.addWidget(self.cpu_value)
        cpu.box.addWidget(self.cpu_bar)
        cpu.box.addWidget(label(s["cpu"].get("Model name",NA),True))
        cpu.box.addWidget(label(f'{s["cpu"].get("CPU(s)",NA)} procesadores lógicos'))
        cpu.box.addWidget(self.cpu_temp)
        cpu.finish()
        cards.append(cpu)
        modules=s.get("modules",[])
        detail="\n\n".join(lines(m) for m in modules) or "Módulos: lectura ampliada necesaria; depende de SMBIOS y de los permisos."
        ram=Card("Memoria RAM",detail)
        ram.box.addWidget(label(backend.size(s["memory"]["total"]),True))
        self.ram_ring=Donut()
        ram.box.addWidget(self.ram_ring)
        self.ram_text=label("")
        ram.box.addWidget(self.ram_text)
        ram.box.addWidget(label(f"{len(modules)} módulos informados" if modules else "Posiciones físicas: "+NA))
        ram.finish()
        cards.append(ram)
        gpu=Card("Memoria de video")
        self.gpu_text=label("")
        gpu.box.addWidget(self.gpu_text)
        gpu.box.addWidget(label("\n\n".join(f'{g["vendor"]} · {g["model"]}\nVRAM: {backend.size(g["vram"])}' for g in s["gpus"]) or NA))
        gpu.finish()
        cards.append(gpu)
        board=Card("Placa y BIOS / UEFI",lines(s["board"]))
        board.box.addWidget(label(s["board"].get("Fabricante",NA),True))
        board.box.addWidget(label(s["board"].get("Modelo",NA)))
        board.box.addWidget(label("Placa: "+s["board"].get("Placa",NA)))
        board.box.addWidget(label("Firmware: "+s["board"].get("Versión firmware",NA)))
        board.finish()
        cards.append(board)
        net=Card("Red", "\n\n".join(lines(n) for n in s["networks"]))
        net.box.addWidget(label("\n".join(f'{n["name"]} · {"Conectada" if n["up"] else "Sin enlace"}' for n in s["networks"]) or NA))
        net.finish()
        cards.append(net)
        system=Card("Sistema y usuario",lines(s["system"]))
        for key in ("Distribución","Kernel","Usuario","Último arranque"):
            system.box.addWidget(label(f'{key}: {s["system"].get(key,NA)}'))
        system.finish()
        cards.append(system)
        audio=Card("Audio y USB",s["audio"]+"\n\n"+"\n".join(s["usb"]))
        audio.box.addWidget(label(f'{len(s["usb"])} dispositivos USB enumerados'))
        audio.box.addWidget(label(s["audio"][:300]))
        audio.finish()
        cards.append(audio)
        self.general.replace(cards)
        diskcards=[]
        for index,disk in enumerate(s["disks"]):
            card=Card(f'{disk["name"]} · {disk["kind_label"]}')
            card.toggle.setText("Ver disco y volúmenes")
            card.toggle.setCheckable(False)
            card.toggle.show()
            card.toggle.clicked.connect(lambda checked=False,i=index:self.open_disk(i))
            row=QtWidgets.QHBoxLayout()
            left=QtWidgets.QVBoxLayout()
            left.addWidget(Miniature(disk["kind"]))
            left.addWidget(label(disk["model"],True))
            left.addWidget(label("Total: "+backend.size(disk["size"])))
            row.addLayout(left,2)
            row.addWidget(Allocation(disk["allocation"],disk["size"]),3)
            card.box.addLayout(row)
            card.box.addWidget(label(f'Conexión: {disk["bus"]} · Miniatura referencial',False))
            diskcards.append(card)
        self.diskcards.replace(diskcards)
    def poll_metrics(self):
        if self.smoke or self.metric_worker or self.closing:
            return
        self.metric_worker=self.launch(lambda cancel,progress:backend.metrics(),self.apply_metrics,self.status.text())
    def apply_metrics(self,data):
        if not self.snapshot:
            return
        self.cpu_value.setText(f'{data["cpu"]:.0f} %')
        self.cpu_bar.setValue(round(data["cpu"]))
        self.ram_ring.set_percent(data["memory_percent"])
        self.ram_text.setText(f'{backend.size(data["memory_used"])} en uso · Disponible: {backend.size(data["memory_free"])}')
        cpu_t=[x["value"] for x in data["temperatures"] if any(k in x["name"].lower() for k in ("coretemp","k10temp","cpu","tctl","package"))]
        self.cpu_temp.setText("Temperatura CPU: "+(f"{max(cpu_t):.1f} °C" if cpu_t else NA))
        nvidia=data.get("nvidia",[])
        self.gpu_text.setText("\n".join(f'{g["name"]} · {g["temperature"]:.0f} °C\nVRAM: {backend.size(g["used"])} / {backend.size(g["vram"])}' for g in nvidia))
        rows=[]
        for reading in data["temperatures"]:
            name=reading["name"]
            value=reading["value"]
            low,high=self.extremes.get(name,(value,value))
            low,high=min(low,value),max(high,value)
            self.extremes[name]=(low,high)
            rows.append([name,f"{value:.1f}",f"{low:.1f}",f"{high:.1f}","°C"])
        rows.extend([r["name"],r["value"],"—","—","RPM"] for r in data["fans"])
        if not rows:
            rows=[["Sensores no expuestos por el equipo",NA,"—","—",""]]
        set_rows(self.sensor_table,rows)
    def populate_inventory(self):
        self.inventory.clear()
        for title,data in (("CPU",self.snapshot["cpu"]),("Placa y firmware",self.snapshot["board"]),("Sistema",self.snapshot["system"])):
            parent=QtWidgets.QTreeWidgetItem(self.inventory,[title,""])
            for key,value in data.items():
                QtWidgets.QTreeWidgetItem(parent,[str(key),str(value)])
        for title,devices in (("PCI",self.snapshot["pci"]),("GPU",self.snapshot["gpus"]),("Red",self.snapshot["networks"]),("Módulos RAM",self.snapshot.get("modules",[]))):
            parent=QtWidgets.QTreeWidgetItem(self.inventory,[title,f"{len(devices)} dispositivo(s)"])
            for device in devices:
                item=QtWidgets.QTreeWidgetItem(parent,[str(device.get("model") or device.get("name") or device.get("Locator") or "Dispositivo"),""])
                for key,value in device.items():
                    QtWidgets.QTreeWidgetItem(item,[str(key),str(value)])
        parent=QtWidgets.QTreeWidgetItem(self.inventory,["USB",""])
        for usb in self.snapshot["usb"]:
            QtWidgets.QTreeWidgetItem(parent,[usb,""])
        QtWidgets.QTreeWidgetItem(self.inventory,["Audio",self.snapshot["audio"]])
    def build_storage(self):
        page=QtWidgets.QWidget()
        box=QtWidgets.QVBoxLayout(page)
        self.disk_selector=QtWidgets.QComboBox()
        self.disk_selector.currentIndexChanged.connect(self.choose_disk)
        box.addWidget(self.disk_selector)
        self.volume_table=table(["Volumen","Formato","Tipo","Tamaño","Usado","Disponible","Montaje"])
        self.volume_table.setMaximumHeight(220)
        self.volume_table.itemSelectionChanged.connect(self.choose_volume)
        box.addWidget(self.volume_table)
        body=QtWidgets.QSplitter(Qt.Horizontal)
        summary=QtWidgets.QWidget()
        summarybox=QtWidgets.QVBoxLayout(summary)
        self.volume_ring=Donut()
        summarybox.addWidget(self.volume_ring)
        self.capacity_text=label("")
        self.health_text=label("")
        summarybox.addWidget(self.capacity_text)
        summarybox.addWidget(self.health_text)
        summarybox.addStretch()
        analysis=QtWidgets.QWidget()
        abox=QtWidgets.QVBoxLayout(analysis)
        buttons=QtWidgets.QHBoxLayout()
        self.scan_button=QtWidgets.QPushButton("Analizar carpetas")
        self.scan_button.clicked.connect(self.start_scan)
        self.cancel_scan=QtWidgets.QPushButton("Cancelar")
        self.cancel_scan.clicked.connect(self.stop_scan)
        self.cancel_scan.setEnabled(False)
        buttons.addWidget(self.scan_button)
        buttons.addWidget(self.cancel_scan)
        abox.addLayout(buttons)
        abox.addWidget(label("Carpetas de la raíz · sin listar archivos",True))
        self.folder_bar=ColorBar()
        abox.addWidget(self.folder_bar)
        self.folder_table=table(["Carpeta / grupo","Tamaño asignado","% leído"])
        self.folder_table.setMinimumHeight(150)
        abox.addWidget(self.folder_table,1)
        self.scan_status=label("Selecciona un volumen montado y pulsa Analizar carpetas.")
        abox.addWidget(self.scan_status)
        body.addWidget(summary)
        body.addWidget(analysis)
        body.setSizes([340,650])
        box.addWidget(body,1)
        self.tabs.addTab(page,"Discos y volúmenes")
    def selected_disk(self):
        return self.disk_selector.currentData()
    def selected_volume(self):
        disk=self.selected_disk()
        row=self.volume_table.currentRow()
        return disk["volumes"][row] if disk and 0<=row<len(disk["volumes"]) else None
    def open_disk(self,index):
        self.disk_selector.setCurrentIndex(index)
        self.tabs.setCurrentIndex(1)
    def choose_disk(self):
        self.stop_scan()
        disk=self.selected_disk()
        rows=[]
        if disk:
            for v in disk["volumes"]:
                rows.append([v["name"],v["fs"],v["family"] or v["type"],backend.size(v["size"]),
                    backend.size(v["used"]),backend.size(v["free"]),", ".join(v["mounts"]) or "Sin montar"])
        self.volume_table.blockSignals(True)
        set_rows(self.volume_table,rows)
        self.volume_table.blockSignals(False)
        if rows:
            self.volume_table.selectRow(0)
        self.choose_volume()
    def choose_volume(self):
        self.stop_scan()
        disk=self.selected_disk()
        v=self.selected_volume()
        self.folder_bar.rows=[]
        self.folder_bar.update()
        set_rows(self.folder_table,[])
        self.volume_ring.set_percent(v["used"]/v["total"]*100 if v and v["total"] else None)
        self.capacity_text.setText(f'Capacidad: {backend.size(v["total"] or v["size"])}\nUsado: {backend.size(v["used"])}\nDisponible: {backend.size(v["free"])}' if v else "Sin volúmenes enumerados")
        self.scan_button.setEnabled(bool(v and v["mounts"] and not self.scan_worker))
        self.scan_status.setText("Pulsa Analizar carpetas." if v and v["mounts"] else "Volumen sin montar o sin acceso. Con-E no lo monta ni desbloquea.")
        smart={}
        if disk:
            smart=self.snapshot.get("smart",{}).get(disk["path"],{})
            if not smart and disk["name"].startswith("nvme"):
                import re
                smart=self.snapshot.get("smart",{}).get(re.sub(r"n\d+$","",disk["path"]),{})
        if smart and (not disk.get("serial") or not smart.get("serial") or disk["serial"] != smart["serial"]):
            smart={}
        self.health_text.setText("SMART del disco físico\nEstado: "+smart.get("health",NA)+
            "\nTemperatura: "+(f'{smart["temperature"]} °C' if smart.get("temperature") is not None else NA)+
            "\nHoras encendido: "+(str(smart["hours"])+" h" if smart.get("hours") is not None else NA)+
            "\nVida restante estimada: "+(str(smart["remaining"])+" %" if smart.get("remaining") is not None else NA)+
            "\nLeído: "+backend.size(smart.get("read_bytes"))+"\nEscrito: "+backend.size(smart.get("written_bytes")))
        if disk and v:
            key=(disk["path"],v["name"],tuple(v["mounts"]))
            if key in self.scan_cache:
                self.show_scan(self.scan_cache[key])
    def start_scan(self):
        v=self.selected_volume()
        disk=self.selected_disk()
        if not v or not v["mounts"] or self.scan_worker:
            return
        key=(disk["path"],v["name"],tuple(v["mounts"]))
        path=v["mounts"][0]
        self.scan_button.setEnabled(False)
        self.cancel_scan.setEnabled(True)
        def scan(cancel,progress):
            return backend.scan_root(path,cancel,lambda group,files,skipped:progress.emit(f"Analizando {group} · {files} archivos contados · {skipped} rutas omitidas"))
        def finished(result):
            self.scan_cache[key]=result
            current=self.selected_volume()
            currentdisk=self.selected_disk()
            if current and currentdisk and key==(currentdisk["path"],current["name"],tuple(current["mounts"])):
                self.show_scan(result)
        self.scan_worker=self.launch(scan,finished,"Analizando carpetas en segundo plano…")
        self.scan_worker.progress.connect(self.scan_status.setText)
    def stop_scan(self):
        if self.scan_worker:
            self.scan_worker.cancel.set()
    def show_scan(self,result):
        self.folder_bar.rows=result["groups"]
        self.folder_bar.update()
        total=sum(v for _,v in result["groups"])
        rows=[]
        for i,(name,count) in enumerate(result["groups"]):
            rows.append([name,backend.size(count),f"{count/total*100:.1f} %" if total else "0 %"])
        set_rows(self.folder_table,rows)
        for i in range(len(rows)):
            self.folder_table.item(i,0).setForeground(QtGui.QColor(COLORS[i%len(COLORS)]))
        self.scan_status.setText(f'{result["seconds"]:.1f} s · {result["skipped"]} rutas omitidas. Porcentajes sobre lo leído; no incluye otros montajes ni metadatos del sistema.')
    def read_privileged(self):
        if self.priv_process:
            return
        question=QtWidgets.QMessageBox.question(self,"Lectura ampliada",
            "Se solicitará autorización del sistema para consultar módulos RAM y SMART.\n"
            "Sólo se ejecutan dmidecode y smartctl en modo lectura. No se modifican discos.\n¿Continuar?")
        if question!=QtWidgets.QMessageBox.Yes:
            return
        program=sys.executable
        args=["--privileged"] if getattr(sys,"frozen",False) else [str(Path(__file__).resolve()),"--privileged"]
        process=QtCore.QProcess(self)
        self.priv_process=process
        self.extended.setEnabled(False)
        process.finished.connect(self.privileged_done)
        process.errorOccurred.connect(lambda error:self.status.setText("No se pudo abrir la autorización del sistema."))
        process.start("pkexec",[program,*args])
        if not process.waitForStarted(1000):
            self.privileged_done(-1,None)
    def privileged_done(self,code,status):
        process=self.priv_process
        if not process:
            return
        self.priv_process=None
        self.extended.setEnabled(True)
        try:
            document=json.loads(bytes(process.readAllStandardOutput()).decode("utf-8"))
            if code or document.get("error"):
                raise ValueError(document.get("error") or "Autorización cancelada o lectura no disponible.")
            self.extra=document
            self.snapshot.update(document)
            self.render_cards()
            self.populate_inventory()
            self.choose_volume()
            self.status.setText("Lectura ampliada completada." if not document.get("warnings") else "Lectura ampliada parcial · "+" | ".join(document["warnings"][:3]))
        except (ValueError,UnicodeError):
            self.status.setText("Autorización cancelada, herramienta ausente o lectura ampliada no disponible.")
        process.deleteLater()
    def build_about(self):
        page=QtWidgets.QWidget()
        box=QtWidgets.QHBoxLayout(page)
        portrait=QtWidgets.QLabel()
        portrait.setAlignment(Qt.AlignTop|Qt.AlignHCenter)
        pix=QtGui.QPixmap(asset("Char_cone.png"))
        portrait.setPixmap(pix.scaled(150,340,Qt.KeepAspectRatio,Qt.SmoothTransformation))
        portrait.setMinimumWidth(170)
        box.addWidget(portrait)
        text=QtWidgets.QTextBrowser()
        text.setOpenExternalLinks(True)
        text.setHtml(f"""<h1>Con-E Linux <small>v{backend.VERSION}</small></h1>
<h3>Hecha en Ñuble, Chile</h3><p>Desarrollada por <b>Omar Águila</b><br>
Laboratorios Momocrackcorp<br>Noviembre de 2026<br>Pueblo Seco, San Ignacio, Ñuble, Chile.</p>
<h3>Un guiño a nuestra historieta</h3><p>Con-E hace un guiño a Coné, sobrino de Condorito,
personajes creados por René Ríos Boettiger, «Pepo». Un pequeño homenaje al humor y a la identidad de Chile.</p>
<h3>Créditos y derechos</h3><p>Los derechos sobre los personajes, sus nombres y la ilustración pertenecen
a sus respectivos titulares. La autoría de Omar Águila y Momocrackcorp corresponde a la aplicación.
Este homenaje no implica afiliación, patrocinio, autorización ni una licencia concedida por esos titulares.</p>
<h3>Primera versión de prueba</h3><p>Diagnóstico de hardware para Ubuntu y Linux Mint. Las lecturas dependen
del equipo y de los permisos. No disponible no significa cero ni ausencia del componente.</p>
<p>Dependencias: PySide6/Qt (LGPL/GPL/comercial), psutil (BSD) y PyInstaller (GPL con excepción).
Atribuciones y licencias en el paquete. Miniaturas compartidas con Con-E para Windows.</p>""")
        box.addWidget(text,1)
        self.tabs.addTab(page,"Acerca de")
    def export(self):
        path,_=QtWidgets.QFileDialog.getSaveFileName(self,"Guardar informe","Con-E-Linux.json","Informe JSON (*.json)")
        if not path:
            return
        try:
            Path(path).write_text(json.dumps(self.snapshot,ensure_ascii=False,indent=2),encoding="utf-8")
            self.status.setText("Informe guardado. Incluye identificadores del equipo y direcciones de red.")
        except OSError as exc:
            QtWidgets.QMessageBox.warning(self,"Informe",str(exc))
    def closeEvent(self,event):
        self.closing=True
        self.timer.stop()
        for worker in self.workers:
            worker.cancel.set()
        if self.priv_process:
            self.priv_process.terminate()
        if any(w.isRunning() for w in self.workers):
            event.ignore()
            self.status.setText("Cerrando lecturas…")
            QtCore.QTimer.singleShot(150,self.close)
        else:
            event.accept()

STYLE="""
QWidget { font-family: 'DejaVu Sans'; font-size: 10pt; color: #233247; }
QMainWindow, QScrollArea, QTabWidget::pane { background: #f2f5f9; }
QFrame#card { background: white; border: 1px solid #ccd8e6; border-radius: 12px; }
QFrame#card QLabel { border: none; background: transparent; }
QPushButton, QToolButton { background: #eff4fb; border: 1px solid #ccd8e6; border-radius: 4px; padding: 8px; color: #285bb5; }
QPushButton:hover, QToolButton:hover { background: #e1ebfa; }
QPushButton:disabled { color: #8794a6; }
QProgressBar { border: none; border-radius: 6px; background: #e2e9f2; max-height: 14px; min-height: 14px; }
QProgressBar::chunk { background: #2d65c4; border-radius: 6px; }
QTableWidget, QTreeWidget, QTextBrowser { background: white; border: 1px solid #d3dde9; alternate-background-color: #f3f6fa; }
QHeaderView::section { background: #eff4fb; padding: 6px; border: none; }
QComboBox { padding: 8px; background: white; border: 1px solid #ccd8e6; }
QTabBar::tab { padding: 10px 14px; background: #edf1f7; }
QTabBar::tab:selected { background: white; color: #285bb5; }
"""
def main():
    app=QtWidgets.QApplication(sys.argv)
    app.setStyle("Fusion")
    app.setStyleSheet(STYLE)
    app.setFont(QtGui.QFont("DejaVu Sans",10))
    smoke="--smoke-test" in sys.argv
    window=MainWindow(smoke)
    window.show()
    if smoke:
        output=Path(os.environ.get("CONE_QA_DIR","qa-linux"))
        output.mkdir(parents=True,exist_ok=True)
        def capture():
            try:
                if Miniature.get("usb").isNull() or QtGui.QPixmap(asset("Char_cone.png")).isNull():
                    raise RuntimeError("Recursos gráficos no incluidos")
                window.resize(1280,900)
                app.processEvents()
                window.grab().save(str(output/"dashboard.png"))
                window.open_disk(0)
                window.volume_table.selectRow(1)
                window.show_scan({"groups":[("home",60_000_000_000),("usr",30_000_000_000),("var",10_000_000_000)],"seconds":2.1,"skipped":3})
                app.processEvents()
                window.grab().save(str(output/"discos.png"))
                window.tabs.setCurrentIndex(4)
                app.processEvents()
                window.grab().save(str(output/"acerca-de.png"))
                window.resize(760,650)
                window.tabs.setCurrentIndex(0)
                app.processEvents()
                window.grab().save(str(output/"estrecho.png"))
                assert window.workers==[], "Se iniciaron lecturas reales en las capturas de prueba"
                (output/"resultado.txt").write_text("OK: interfaz, recursos, navegación, disco, carpetas, ventana estrecha y Acerca de. Capturas con datos simulados.",encoding="utf-8")
                window.close()
                app.quit()
            except Exception:
                traceback.print_exc()
                app.exit(1)
        QtCore.QTimer.singleShot(250,capture)
    return app.exec()

if __name__=="__main__":
    sys.exit(main())

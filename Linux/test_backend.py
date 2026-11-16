import hashlib
import json
import os
from pathlib import Path
import tempfile
import threading
import unittest
from collections import namedtuple
import backend

class InventoryTests(unittest.TestCase):
    def test_nested_volumes_and_unknown_usage(self):
        doc={"blockdevices":[{"name":"nvme0n1","type":"disk","size":1000000,"tran":"nvme","children":[
            {"name":"nvme0n1p1","type":"part","size":200000,"fstype":"vfat","parttype":"c12a7328-f81f-11d2-ba4b-00a0c93ec93b","mountpoints":[]},
            {"name":"nvme0n1p2","type":"part","size":700000,"fstype":"crypto_LUKS","children":[
                {"name":"cryptroot","type":"crypt","size":600000,"fstype":"ext4","mountpoints":["/"]}]}]}]}
        Usage=namedtuple("Usage","total used free percent")
        disks=backend.normalize_disks(doc,usage=lambda p:Usage(600000,100000,500000,16.7))
        self.assertEqual(len(disks),1)
        self.assertEqual(len(disks[0]["volumes"]),3)
        self.assertIsNone(disks[0]["volumes"][0]["used"])
        self.assertEqual(disks[0]["volumes"][2]["family"],"Linux en uso")
        self.assertEqual(sum(x["size"] for x in disks[0]["allocation"]),1000000)
        self.assertEqual(disks[0]["kind"],"m2-nvme")
    def test_no_os_inference_and_usb(self):
        self.assertEqual(backend.partition_family({"fstype":"ntfs"}),"")
        self.assertEqual(backend.partition_family({"fstype":"exfat"}),"")
        self.assertEqual(backend.partition_family({"fstype":"ext4"}),"Tipo Linux")
        self.assertEqual(backend.disk_kind({"name":"sdb","tran":"usb","rota":False})[0],"usb")
        self.assertEqual(backend.disk_kind({"name":"vda","rota":False})[0],"generic")
    def test_smart_unavailable_and_nvme(self):
        self.assertEqual(backend.parse_smart({})["health"],backend.NA)
        self.assertIsNone(backend.parse_smart({})["hours"])
        smart=backend.parse_smart({"serial_number":"TEST","nvme_smart_health_information_log":{
            "critical_warning":0,"percentage_used":3,"power_on_hours":2048,"data_units_written":100}})
        self.assertEqual(smart["remaining"],97)
        self.assertEqual(smart["written_bytes"],51200000)
        self.assertEqual(smart["serial"],"TEST")
    def test_smbios_modules_not_empty_slots(self):
        result=backend.parse_memory_modules("Memory Device\n    Size: 16 GB\n    Locator: DIMM_A1\n    Type: DDR4\n    Speed: 3200 MT/s\n\nMemory Device\n    Size: No Module Installed\n    Locator: DIMM_A2\n")
        self.assertEqual(len(result),1)
        self.assertEqual(result[0]["Locator"],"DIMM_A1")

class ScanTests(unittest.TestCase):
    def test_hardlinks_symlinks_and_no_changes(self):
        with tempfile.TemporaryDirectory() as root, tempfile.TemporaryDirectory() as external:
            root=Path(root)
            (root/"A").mkdir()
            (root/"B").mkdir()
            data=bytes(range(256))*64
            original=root/"A"/"original.bin"
            original.write_bytes(data)
            os.link(original,root/"B"/"alias.bin")
            (Path(external)/"outside.bin").write_bytes(data*3)
            (root/"external").symlink_to(external,target_is_directory=True)
            (root/"A"/"loop").symlink_to(root,target_is_directory=True)
            before=hashlib.sha256(original.read_bytes()).digest()
            result=backend.scan_root(str(root))
            self.assertEqual(result["files"],1)
            self.assertEqual(sum(v for _,v in result["groups"]),original.stat().st_blocks*512)
            self.assertGreaterEqual(result["skipped"],2)
            self.assertEqual(hashlib.sha256(original.read_bytes()).digest(),before)
    def test_cancelled_before_scan(self):
        with tempfile.TemporaryDirectory() as root:
            (Path(root)/"file").write_bytes(b"x")
            cancel=threading.Event()
            cancel.set()
            with self.assertRaises(InterruptedError):
                backend.scan_root(root,cancel)

if __name__=="__main__":
    unittest.main()

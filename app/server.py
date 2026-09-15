#!/usr/bin/env python3
import http.server
import socketserver
import json
import urllib.request
import urllib.parse
import subprocess
import os
import sys
import time
import ctypes

PORT = 8999
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SUITE_BASE = os.path.dirname(BASE_DIR)
CONFIG_DIR = os.path.join(SUITE_BASE, "config")
SCRIPTS_DIR = os.path.join(SUITE_BASE, "scripts")
DESTINATIONS_FILE = os.path.join(CONFIG_DIR, "destinations.json")
STATIC_DIR = os.path.join(BASE_DIR, "static")
MEDIAMTX_EXE = os.path.join(SUITE_BASE, "bin", "mediamtx.exe")
MEDIAMTX_YML = os.path.join(CONFIG_DIR, "mediamtx.yml")
LOG_FILE = os.path.join(SUITE_BASE, "bin", "mediamtx.log")

RELAY_PROCESS = None
RELAY_LOG_HANDLE = None

def apply_hardware_affinity_and_priority(pid):
    try:
        PROCESS_ALL_ACCESS = 0x1F0FFF
        handle = ctypes.windll.kernel32.OpenProcess(PROCESS_ALL_ACCESS, False, pid)
        if handle:
            # 0xF000 = Threads 12-15 on Ryzen 5800X
            ctypes.windll.kernel32.SetProcessAffinityMask(handle, 0xF000)
            # HIGH_PRIORITY_CLASS = 0x00000080
            ctypes.windll.kernel32.SetPriorityClass(handle, 0x00000080)
            ctypes.windll.kernel32.CloseHandle(handle)
            return True
    except Exception as e:
        print("[Hardware] Could not set affinity for PID", pid, ":", e)
    return False

def sync_mediamtx_config():
    try:
        dest_data = {}
        if os.path.exists(DESTINATIONS_FILE):
            with open(DESTINATIONS_FILE, "r", encoding="utf-8") as f:
                dest_data = json.load(f)

        forward_list = []
        destinations = dest_data.get("destinations", {})
        for k, item in destinations.items():
            if item.get("enabled") and item.get("stream_key", "").strip():
                server_url = item.get("server", "").rstrip("/")
                stream_key = item.get("stream_key", "").strip()
                forward_list.append("      - dest: " + server_url + "/" + stream_key)

        if forward_list:
            forward_section = "    forward:\n" + "\n".join(forward_list)
        else:
            forward_section = "    forward: []"

        yml_content = """# BrodCatsSuite0 High-Performance MediaMTX Master Relay Configuration
api: true
apiAddress: 127.0.0.1:9997

rtmp: true
rtmpAddress: 127.0.0.1:1935
rtmpEncryption: "no"

rtsp: false
hls: false
webrtc: false
srt: true
srtAddress: 127.0.0.1:8890

readTimeout: 10s
writeTimeout: 10s

paths:
  live/master:
""" + forward_section + """
    runOnInit: ""
    runOnDemand: ""
"""
        with open(MEDIAMTX_YML, "w", encoding="utf-8") as f:
            f.write(yml_content)
        return True
    except Exception as e:
        print("[Config] Error syncing mediamtx.yml:", e)
        return False

def start_mediamtx_relay():
    global RELAY_PROCESS, RELAY_LOG_HANDLE
    stop_mediamtx_relay()
    sync_mediamtx_config()

    if not os.path.exists(MEDIAMTX_EXE):
        return False, "mediamtx.exe not found at " + MEDIAMTX_EXE

    try:
        RELAY_LOG_HANDLE = open(LOG_FILE, "a", encoding="utf-8")
        RELAY_PROCESS = subprocess.Popen(
            [MEDIAMTX_EXE, MEDIAMTX_YML],
            cwd=os.path.dirname(MEDIAMTX_EXE),
            stdout=RELAY_LOG_HANDLE,
            stderr=RELAY_LOG_HANDLE,
            stdin=subprocess.DEVNULL
        )
        time.sleep(0.4)
        if RELAY_PROCESS.poll() is None:
            apply_hardware_affinity_and_priority(RELAY_PROCESS.pid)
            print("[Relay] MediaMTX started successfully (PID", RELAY_PROCESS.pid, ", Affinity 0xF000)")
            return True, "Relay running (PID: " + str(RELAY_PROCESS.pid) + ")"
        else:
            return False, "Relay exited prematurely"
    except Exception as e:
        return False, str(e)

def stop_mediamtx_relay():
    global RELAY_PROCESS, RELAY_LOG_HANDLE
    if RELAY_PROCESS and RELAY_PROCESS.poll() is None:
        try:
            RELAY_PROCESS.terminate()
            RELAY_PROCESS.wait(timeout=2)
        except Exception:
            try:
                RELAY_PROCESS.kill()
            except Exception:
                pass
    RELAY_PROCESS = None
    if RELAY_LOG_HANDLE:
        try:
            RELAY_LOG_HANDLE.close()
        except Exception:
            pass
        RELAY_LOG_HANDLE = None

    try:
        subprocess.run(["taskkill", "/F", "/IM", "mediamtx.exe"], capture_output=True)
    except Exception:
        pass

def is_relay_running():
    global RELAY_PROCESS
    if RELAY_PROCESS and RELAY_PROCESS.poll() is None:
        return True, RELAY_PROCESS.pid
    try:
        res = subprocess.run(["tasklist", "/FI", "IMAGENAME eq mediamtx.exe", "/FO", "CSV", "/NH"], capture_output=True, text=True)
        if "mediamtx.exe" in res.stdout:
            for line in res.stdout.splitlines():
                if "mediamtx.exe" in line:
                    parts = line.split('","')
                    if len(parts) >= 2:
                        pid = int(parts[1].replace('"', ''))
                        return True, pid
            return True, None
    except Exception:
        pass
    return False, None

def is_obs_running():
    try:
        res = subprocess.run(["tasklist", "/FI", "IMAGENAME eq obs64.exe", "/FO", "CSV", "/NH"], capture_output=True, text=True)
        if "obs64.exe" in res.stdout:
            for line in res.stdout.splitlines():
                if "obs64.exe" in line:
                    parts = line.split('","')
                    if len(parts) >= 2:
                        pid = int(parts[1].replace('"', ''))
                        return True, pid
            return True, None
    except Exception:
        pass
    return False, None

def get_gpu_telemetry():
    try:
        cmd = [
            "nvidia-smi",
            "--query-gpu=utilization.gpu,utilization.encoder,utilization.decoder,temperature.gpu,memory.used,memory.total",
            "--format=csv,noheader,nounits"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, timeout=1.0)
        if res.returncode == 0 and res.stdout.strip():
            parts = [p.strip() for p in res.stdout.strip().split(",")]
            return {
                "gpu_util": int(parts[0]),
                "nvenc_util": int(parts[1]),
                "nvdec_util": int(parts[2]),
                "temperature": int(parts[3]),
                "vram_used": int(parts[4]),
                "vram_total": int(parts[5])
            }
    except Exception:
        pass
    return {
        "gpu_util": 0,
        "nvenc_util": 0,
        "nvdec_util": 0,
        "temperature": 0,
        "vram_used": 0,
        "vram_total": 16380
    }

def get_mediamtx_telemetry():
    try:
        url = "http://127.0.0.1:9997/v3/paths/list"
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=0.8) as resp:
            data = json.loads(resp.read().decode())
            items = data.get("items", [])
            for item in items:
                if item.get("name") == "live/master":
                    return {
                        "online": item.get("ready", False),
                        "readers": len(item.get("readers", [])),
                        "bytes_received": item.get("bytesReceived", 0),
                        "bytes_sent": item.get("bytesSent", 0)
                    }
    except Exception:
        pass
    return {
        "online": False,
        "readers": 0,
        "bytes_received": 0,
        "bytes_sent": 0
    }

class BrodCatsHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=STATIC_DIR, **kwargs)

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path == "/api/status":
            self.handle_api_status()
        elif parsed.path == "/api/destinations":
            self.handle_api_destinations()
        else:
            super().do_GET()

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        content_len = int(self.headers.get("Content-Length", 0))
        post_data = self.rfile.read(content_len) if content_len > 0 else b"{}"

        if parsed.path == "/api/destinations":
            self.handle_save_destinations(post_data)
        elif parsed.path == "/api/relay/start":
            self.handle_relay_start()
        elif parsed.path == "/api/relay/stop":
            self.handle_relay_stop()
        elif parsed.path == "/api/obs/launch":
            self.handle_obs_launch()
        elif parsed.path == "/api/optimize":
            self.handle_apply_optimize()
        else:
            self.send_error(404, "Endpoint not found")

    def send_json(self, data, code=200):
        body = json.dumps(data).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def handle_api_status(self):
        relay_running, relay_pid = is_relay_running()
        obs_running, obs_pid = is_obs_running()
        gpu_stats = get_gpu_telemetry()
        stream_stats = get_mediamtx_telemetry() if relay_running else {"online": False, "readers": 0, "bytes_received": 0, "bytes_sent": 0}

        destinations = {}
        if os.path.exists(DESTINATIONS_FILE):
            try:
                with open(DESTINATIONS_FILE, "r", encoding="utf-8") as f:
                    destinations = json.load(f)
            except Exception:
                pass

        resp = {
            "success": True,
            "relay": {
                "running": relay_running,
                "pid": relay_pid,
                "ingest_url": "rtmp://127.0.0.1:1935/live/master",
                "telemetry": stream_stats
            },
            "obs": {
                "running": obs_running,
                "pid": obs_pid
            },
            "hardware": {
                "cpu": "AMD Ryzen 7 5800X (8C/16T)",
                "affinity": "Threads 12-15 (0xF000)",
                "gpu": "NVIDIA GeForce RTX 4060 Ti (Ada NVENC)",
                "telemetry": gpu_stats
            },
            "destinations_summary": destinations
        }
        self.send_json(resp)

    def handle_api_destinations(self):
        if os.path.exists(DESTINATIONS_FILE):
            with open(DESTINATIONS_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
            self.send_json(data)
        else:
            self.send_json({"error": "destinations.json not found"}, 404)

    def handle_save_destinations(self, raw_body):
        try:
            payload = json.loads(raw_body.decode("utf-8"))
            with open(DESTINATIONS_FILE, "w", encoding="utf-8") as f:
                json.dump(payload, f, indent=2)
            
            relay_running, _ = is_relay_running()
            if relay_running:
                start_mediamtx_relay()

            self.send_json({"success": True, "message": "Destinations saved and relay synchronized."})
        except Exception as e:
            self.send_json({"success": False, "error": str(e)}, 500)

    def handle_relay_start(self):
        success, msg = start_mediamtx_relay()
        self.send_json({"success": success, "message": msg})

    def handle_relay_stop(self):
        stop_mediamtx_relay()
        self.send_json({"success": True, "message": "MediaMTX Relay stopped."})

    def handle_obs_launch(self):
        script = os.path.join(SCRIPTS_DIR, "04_launch_obs_optimaxx.ps1")
        try:
            subprocess.Popen(["powershell", "-ExecutionPolicy", "Bypass", "-File", script], creationflags=subprocess.CREATE_NO_WINDOW)
            self.send_json({"success": True, "message": "OBS Studio launching with Optimaxx profile."})
        except Exception as e:
            self.send_json({"success": False, "error": str(e)}, 500)

    def handle_apply_optimize(self):
        script = os.path.join(SCRIPTS_DIR, "01_streaming_qos_and_affinity.ps1")
        try:
            subprocess.Popen(["powershell", "-ExecutionPolicy", "Bypass", "-File", script], creationflags=subprocess.CREATE_NO_WINDOW)
            self.send_json({"success": True, "message": "Network QoS & Hardware Scheduler optimization applied."})
        except Exception as e:
            self.send_json({"success": False, "error": str(e)}, 500)

class ThreadedHTTPServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True

if __name__ == "__main__":
    sync_mediamtx_config()
    start_mediamtx_relay()

    server_address = ("127.0.0.1", PORT)
    httpd = ThreadedHTTPServer(server_address, BrodCatsHandler)
    print(f"[BrodCatsSuite0] Master Dashboard active on http://127.0.0.1:{PORT}")
    print("[BrodCatsSuite0] Ready for OBS Browser Dock and external browsers.")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n[BrodCatsSuite0] Server shut down cleanly.")
        stop_mediamtx_relay()
        httpd.server_close()

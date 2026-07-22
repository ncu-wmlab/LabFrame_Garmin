#!/usr/bin/env python3
"""假的 Garmin Streaming 伺服端，用來在沒有 Android 平板時驗證 Unity 接收端。

行為刻意比照 App：被動接聽、收到 start 才開始送資料、每秒一筆 LabGarmin JSON、
每筆結尾帶 \\n、client 斷線後回到 accept 等下一個連線。

用法：
    python mock_garmin_server.py                # 監聽 0.0.0.0:6001
    python mock_garmin_server.py --port 6001
    python mock_garmin_server.py --protocol jsonl   # 送 ExperimentRecord，驗證協定守門
"""

import argparse
import json
import random
import socket
import threading
import time
from datetime import datetime


def build_labgarmin(tag):
    now = datetime.now().strftime("%Y-%m-%d %H-%M-%S-") + f"{datetime.now().microsecond // 1000:03d}"
    return {
        "heartRate": random.randint(60, 100),
        "restingHeartRate": 0,
        "heartRateVariability": random.randint(20, 80),
        "accelerometer_x": random.randint(-10, 10),
        "accelerometer_y": random.randint(-10, 10),
        "accelerometer_z": random.randint(-10, 10),
        "steps": random.randint(0, 5000),
        "calories_Total": 0,
        "calories_Active": 0,
        "floors_Climb": 0,
        "floors_Descend": 0,
        "intensityMinutes_Moderate": 0,
        "intensityMinutes_Vigorous": 0,
        "stressLevel": random.randint(10, 60),
        "SPO2": random.randint(94, 100),
        "bodyBattery": 0,
        "respiration": random.randint(12, 20),
        "time": now,
        "tag": tag if tag else "null",
    }


def build_experiment_record(tag):
    """JSONL 模式。與 LabGarminData 只有 tag 一個欄位重疊，用來驗證協定守門。"""
    return {
        "sessionId": "S-mock",
        "receivedAtEpochMs": int(time.time() * 1000),
        "deviceTimestampMs": int(time.time() * 1000),
        "deviceId": "D-mock",
        "dataType": "HeartRate",
        "scalarValue": float(random.randint(60, 100)),
        "callbackSequence": random.randint(1, 9999),
        "connectionState": "CONNECTED",
        "sdkVersion": "mock",
        "tag": tag,
    }


class Session:
    def __init__(self, conn, addr, protocol):
        self.conn = conn
        self.addr = addr
        self.protocol = protocol
        self.streaming = False
        self.tag = None
        self.alive = True

    def sender(self):
        while self.alive:
            if self.streaming:
                if self.protocol == "jsonl":
                    payload = build_experiment_record(self.tag)
                else:
                    payload = build_labgarmin(self.tag)
                line = json.dumps(payload, separators=(",", ":")) + "\n"
                try:
                    self.conn.sendall(line.encode("utf-8"))
                except OSError:
                    self.alive = False
                    return
            time.sleep(1.0)

    def handle_command(self, message):
        trimmed = message.strip()
        if trimmed.startswith("disconnect"):
            print(f"  <- disconnect")
            self.streaming = False
            self.alive = False
        elif trimmed.startswith("connect"):
            print(f"  <- connect（僅記錄，不動作）")
        elif trimmed.startswith("start"):
            self.streaming = True
            if "," in trimmed:
                self.tag = trimmed.split(",", 1)[1]
            print(f"  <- start（tag={self.tag}）開始送資料")
        elif trimmed.startswith("stop"):
            self.streaming = False
            print(f"  <- stop 停止送資料，連線保持")
        else:
            self.tag = trimmed
            print(f"  <- tag={self.tag}")

    def run(self):
        threading.Thread(target=self.sender, daemon=True).start()
        try:
            while self.alive:
                chunk = self.conn.recv(1024)
                if not chunk:
                    break
                message = chunk.decode("utf-8", errors="replace").strip()
                if message:
                    self.handle_command(message)
        except OSError:
            pass
        finally:
            self.alive = False
            self.conn.close()
            print(f"連線結束：{self.addr}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=6001)
    parser.add_argument("--protocol", choices=["labgarmin", "jsonl"], default="labgarmin")
    args = parser.parse_args()

    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind((args.host, args.port))
    server.listen(1)
    print(f"監聽 {args.host}:{args.port}（protocol={args.protocol}）")
    print("等待 Unity 連進來。送出 start 之前不會有任何資料。")

    try:
        while True:
            conn, addr = server.accept()
            print(f"連線建立：{addr}")
            Session(conn, addr, args.protocol).run()
            print("回到 accept，等待下一個連線")
    except KeyboardInterrupt:
        print("\n關閉")
    finally:
        server.close()


if __name__ == "__main__":
    main()

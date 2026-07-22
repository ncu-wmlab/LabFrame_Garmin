# Changelog

## [1.2.0] - 2026-07-22

### 變更（破壞性）

- **連線設定改由 `GarminConfig` 設定檔提供，`GarminBluetoothManager` 的 Inspector 欄位全部移除。**

  原本把 `transport` / `manualHost` / `manualPort` 等放在套件內 prefab 的
  `[SerializeField]` 上，這在套件實際的散佈方式下行不通：消費端專案透過 scoped
  registry 安裝時，套件會落在 **唯讀的 `Library/PackageCache`**，prefab 改不了；
  而且該 prefab 由 LabFrame 在進入 Play Mode 時才實例化，也無法即時調整。
  再者每個專案、每次實驗的平板位址都不同，本來就不該共用套件裡的同一份值。

  改用 LabFrame 既有的 `LabTools.GetConfig<T>()` 機制，設定檔位置依平台：

  | 平台 | 路徑 |
  |---|---|
  | Editor | `Assets/Resources/Config/GarminConfig.json`（消費端專案自己的） |
  | Windows build | `{遊戲資料夾}/Config/GarminConfig.json` |
  | Android / Quest | `{persistentDataPath}/Config/GarminConfig.json` |

  在 Quest 上換平板不需要重新建置：

  ```bash
  adb push GarminConfig.json /sdcard/Android/data/<包名>/files/Config/
  ```

  `Transport` 欄位刻意用字串（`"Tcp"` / `"Bluetooth"`，大小寫不拘）而非 enum：
  `JsonUtility` 會把 enum 序列化成數字，手改設定檔時看到 `"Transport": 1` 無從理解。
  無法辨識的值會回退 `Tcp` 並輸出說明，不靜默降級。

  `ManagerInit` 會記錄設定檔的實際路徑；`Host` 為空時在啟動當下就警告，
  不必等到連線失敗才發現。

### 移除

- `GarminBluetoothManager` 的 `OnValidate`。`transport` 不再是序列化欄位，
  Editor 期已無從檢查；非 Android 平台選用藍牙時，改由
  `BluetoothGarminLink.BeginConnect()` 以明確訊息失敗。

### 保留

- `SetTcpTarget(host, port)` 仍可於執行期覆蓋設定檔的值，供實驗腳本動態指定。

---

## [1.1.0] - 2026-07-21

### 新增

- **TCP 傳輸**（預設）。以 `TcpClient` 連到 Android 端的 Garmin Streaming App，
  預設 port 6001，不需要任何 native plugin，Windows Editor 與 Quest 3 共用同一份程式碼。
- **`IGarminLink` 傳輸抽象**，`TcpGarminLink` 與 `BluetoothGarminLink` 兩份實作。
  傳輸方式由 Inspector 明確指定，**不會自動切換**——網路不通時明確失敗，
  避免同一場實驗的傳輸特性與上一場不同而難以事後除錯。
- **自動重連**。意外斷線後每 `reconnectIntervalSec` 秒重試，無次數上限，
  連上後自動補送 `start,<tag>` 恢復串流。使用者主動 `disconnect` 則不重連。
- **協定守門**。Android 端若停在 JSONL 模式送 `ExperimentRecord`，
  `JsonUtility.FromJson` 不會拋例外而是回傳全零物件，先前會靜默錄下整場全 0 的資料。
  現在以 `time` 欄位是否存在判別，每條連線報錯一次，並公開 `ProtocolMismatchDetected`。
- **無資料警告**。已連線但 `noDataWarningSec` 秒內零資料時發出警告，
  這是「忘了送 start」與「Android 端沒在送」的唯一外顯訊號。
- `SetTcpTarget(string host, int port)` 供實驗腳本在執行期指定連線目標。
- EditMode 測試涵蓋行緩衝、JSON 解析、session 狀態與 TCP 連線。
- `Tools~/mock_garmin_server.py`，可在沒有 Android 平板時於 Windows Editor 端到端驗證。

### 修正

- **`stop` 先前完全沒有作用**。舊實作只呼叫 `CancelInvoke("StartReceiveData")`，
  而該方法根本不存在；既沒把 `stop` 送給 Android，也沒停掉接收協程。
- **重複的接收協程**。舊實作每收到一則 `start` 開頭的訊息就再開一條 `ReceiveData` 協程，
  沒有防重複。實驗中以 `start,<tag>` 切換階段時，多條協程會搶讀同一個 socket 互相吃掉資料。
- **`DisconnectDevice()` 呼叫 `StopAllCoroutines()`**，會連帶殺掉重連協程。
- 移除寫死的裝置名稱 `"Galaxy Tab A9+"`。
- 移除為「多個 JSON 黏在一起、沒有分隔符」設計的 `DeserializeLabGarminDataList()`
  （`Replace("}{", "},{")`），改為逐行解析。
- 多位元組字元（例如中文 tag）被切在 TCP chunk 邊界時不再解碼失敗。

### 移除

- `device_disconnect` 斷線訊息機制。Android 新版不再送出此訊息。

### 已知限制

- **tag 不得以 `disconnect` / `connect` / `start` / `stop` 開頭**。
  Android 端 `BtCommandParser` 採前綴比對，例如 tag `"stop_condition"` 會被誤判為 `STOP` 指令。
- **Android 端指令黏包**。`CommandServer` 讀的是原始 chunk 而非逐行，
  兩則相鄰指令若落在同一個 read chunk 會被當成一則解析（後者遺失，或 tag 被汙染）。
  本套件以 `NoDelay` 加上指令間隔 `commandGapSec` 降低機率，
  但根治需 Android 端改用逐行框架。
- **Quest 3 的 SPP client 能力尚未實測。**
- 藍牙路徑的 `ReadLine()` 在 `Available() > 0` 但未收滿整行時的行為未經驗證，可能阻塞。
- **無任何認證機制**，僅限受控的實驗室網路使用。
- UDP beacon 自動探索尚未實作，目前需手動指定 IP。

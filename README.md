# Lab Frame 2023 - Garmin 智慧手錶插件

一個專為 LabFrame2023 研究框架設計的 Unity 套件，透過藍牙通訊實現與 Garmin 智慧手錶的整合，可即時收集健康與運動數據。

## 📋 概述

此插件允許 Unity 應用程式連接到 Garmin 智慧手錶，收集各種生物識別和活動數據以供研究使用。專為實驗室環境設計，與 LabFrame2023 生態系統無縫整合。

## ✨ 功能特色

- **即時數據收集**：心率、步數、卡路里、壓力水平等
- **藍牙連接**：與 Garmin 裝置無線連接
- **全方位健康指標**：
  - 心率與靜息心率
  - 心率變異度（RR-interval）
  - 加速度計數據（X、Y、Z 軸）
  - 步數與爬樓梯數據
  - 卡路里消耗（總計與活動）
  - 壓力水平監控
  - 血氧飽和度（SPO2）
  - Body Battery 能量監控
  - 呼吸速率
- **研究框架整合**：專為 LabFrame2023 數據管理而設計
- **Unity 編輯器支援**：與 Unity 2021.3+ 輕鬆整合

## 🛠️ 安裝

### 系統需求

- Unity 2021.3.9f1 或更新版本
- LabFrame2023 框架（v0.0.8+）
- LabFrame Bluetooth 插件（v1.0.3+）
- 具有藍牙功能的 Android 裝置
- 具有 companion app 的 Garmin 智慧手錶

### 套件安裝

1. 開啟 Unity Package Manager
2. 從 git URL 添加套件：
   ```
   https://github.com/ncu-wmlab/LabFrame_Garmin.git
   ```
3. 或將以下內容添加到您的 `manifest.json`：
   ```json
   {
     "dependencies": {
       "com.xrlab.labframe_garmin": "1.0.3"
     }
   }
   ```

### 相依套件

此套件會自動安裝：
- `com.xrlab.labframe`: 0.0.8
- `com.xrlab.labframe_bluetooth`: 1.0.3

## 🚀 快速開始

### 1. 基本設定

```csharp
using UnityEngine;

public class GarminExample : MonoBehaviour
{
    void Start()
    {
        // 初始化數據管理器
        LabDataManager.Instance.LabDataInit("garminTest");
        
        // 連接到預設裝置（Galaxy Tab A9+）
        GarminBluetoothManager.instance.ConnectToDevice();
    }
    
    void Update()
    {
        if (GarminBluetoothManager.instance.connectionStatus)
        {
            var data = GarminBluetoothManager.instance.labGarminData;
            Debug.Log($"心率: {data.heartRate}");
            Debug.Log($"步數: {data.steps}");
        }
    }
}
```

### 2. 自訂裝置連接

```csharp
// 連接到特定裝置
GarminBluetoothManager.instance.ConnectToDevice("您的裝置名稱");

// 檢查連接狀態
bool isConnected = GarminBluetoothManager.instance.connectionStatus;

// 完成後斷開連接
GarminBluetoothManager.instance.DisconnectDevice();
```

### 3. 數據存取

```csharp
var garminData = GarminBluetoothManager.instance.labGarminData;

// 存取個別指標
int heartRate = garminData.heartRate;
int steps = garminData.steps;
int stressLevel = garminData.stressLevel;
int spo2 = garminData.SPO2;

// 加速度計數據
Vector3 acceleration = new Vector3(
    garminData.accelerometer_x,
    garminData.accelerometer_y,
    garminData.accelerometer_z
);
```

## 📊 可用數據欄位

| 欄位 | 類型 | 說明 |
|------|------|------|
| `heartRate` | int | 目前心率（BPM） |
| `restingHeartRate` | int | 靜息心率（BPM） |
| `heartRateVariability` | int | 心率變異度（RR-interval） |
| `accelerometer_x/y/z` | int | 三軸加速度計數據 |
| `steps` | int | 步數 |
| `calories_Total` | int | 總消耗卡路里 |
| `calories_Active` | int | 活動消耗卡路里 |
| `floors_Climb` | int | 爬樓梯數 |
| `floors_Descend` | int | 下樓梯數 |
| `intensityMinutes_Moderate` | int | 中等強度運動分鐘數 |
| `intensityMinutes_Vigorous` | int | 高強度運動分鐘數 |
| `stressLevel` | int | 壓力水平（0-100） |
| `SPO2` | int | 血氧飽和度 |
| `bodyBattery` | int | Garmin Body Battery 水平 |
| `respiration` | int | 呼吸速率 |
| `time` | string | 時間數據 |
| `tag` | string | 數據標籤/識別符 |
| `Timestamp` | DateTime | 數據時間戳 |

## 🎮 範例場景

套件包含一個展示基本功能的範例場景：

1. 從 Package Manager 匯入範例
2. 開啟 `Samples/Sample/GarminSample.unity`
3. 執行場景以測試藍牙連接和數據顯示

### 範例功能
- 裝置名稱輸入欄位
- 連接/斷開按鈕
- 即時數據顯示（中英文）
- 連接狀態監控

## 🔧 API 參考

### GarminBluetoothManager

**屬性：**
- `connectionStatus`: bool - 目前連接狀態
- `labGarminData`: LabGarminData - 目前感測器數據

**方法：**
- `ConnectToDevice(string deviceName = "Galaxy Tab A9+")` - 連接到 Garmin 裝置
- `DisconnectDevice()` - 斷開裝置連接
- `SendData(string data)` - 發送數據到裝置
- `ConnectionManager(string message)` - 透過字串命令處理連接

### LabGarminData

繼承自 `LabDataBase`，包含上述列出的所有生物識別和活動數據欄位。

## 🛡️ 錯誤處理

```csharp
try
{
    GarminBluetoothManager.instance.ConnectToDevice("裝置名稱");
}
catch (Exception e)
{
    Debug.LogError($"連接失敗: {e.Message}");
}

// 存取數據前檢查連接
if (GarminBluetoothManager.instance.connectionStatus)
{
    // 可安全存取數據
    var data = GarminBluetoothManager.instance.labGarminData;
}
```

## ⚠️ 重要注意事項

- **僅限實驗室使用**：此插件專為實驗室研究環境設計
- **Android 平台**：目前僅支援 Android 裝置
- **藍牙權限**：確保目標裝置已授予藍牙權限
- **裝置相容性**：已與 Galaxy Tab A9+ 和各種 Garmin 智慧手錶測試
- **即時數據**：在活動連接期間即時接收和處理數據

## 🔧 疑難排解

### 常見問題

**主要用法請見實驗室文件**

**連接失敗：**
- 確保兩個裝置都已啟用藍牙
- 檢查裝置名稱是否完全匹配
- 驗證 Garmin companion app 是否正在執行
- 確保裝置已在 Android 設定中配對

**沒有收到數據：**
- 檢查裝置是否正確連接
- 驗證 Garmin 手錶是否主動發送數據
- 如需要請重新啟動 companion app

**權限錯誤：**
- 在 Android 設定中授予藍牙權限
- 確保已啟用位置權限（藍牙探索所需）

### 除錯資訊

啟用除錯日誌以排除問題：
```csharp
Debug.Log($"連接狀態: {GarminBluetoothManager.instance.connectionStatus}");
Debug.Log($"可用裝置: {BluetoothManager.Instance.GetAvailableDevices()}");
```

## 📚 文件

- [LabFrame2023 文件](https://github.com/ncu-wmlab/LabFrame2023)
- [Unity 套件文件](https://docs.unity3d.com/Manual/Packages.html)

## 👥 作者

**中央大學 wmlab & xrlab**
- 電子郵件：wmlabwmlab@gmail.com
- 網站：https://staff.csie.ncu.edu.tw/shihchingyeh/index2.html

## 📄 授權

此專案是 LabFrame2023 研究框架的一部分。授權資訊請參考主要專案。

## 🔄 版本歷史

- **1.0.3** - 目前版本，增強藍牙穩定性:主要處理初始化的問題
- **1.0.0** - 初始發布

## 🤝 貢獻

此插件是國立中央大學進行中研究的一部分。如需貢獻或研究合作，請聯繫開發團隊。

---

**注意**：此插件專為實驗室環境中的研究目的而設計。商業使用可能需要額外的授權考量。

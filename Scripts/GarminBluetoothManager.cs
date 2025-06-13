using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using LabFrame2023;
using System.Data.Common;

public class GarminBluetoothManager : LabSingleton<GarminBluetoothManager>, IManager
{
    /// <summary>
    /// 目前是否已連線？
    /// </summary>
    public bool connectionStatus
    {
        get
        {
            return BluetoothManager.Instance != null && BluetoothManager.Instance.IsConnected();
        }
    }

    /// <summary>
    /// 目前的資料
    /// </summary>
    public LabGarminData labGarminData = new LabGarminData();
    
    StringBuilder receivedData = new StringBuilder();

    string incompleteData = "";

    public void ManagerInit()
    {
        StartCoroutine(DelayedInit());
        Debug.Log("[GarminBluetoothManager] GarminBluetoothManager is ManagerInit.");
    }

    public IEnumerator ManagerDispose()
    {
        DisconnectDevice();
        yield break;
    }

    /* -------------------------------------------------------------------------- */
    private IEnumerator DelayedInit()
    {
        yield return new WaitUntil(() => BluetoothManager.Instance != null);
        try 
        {
            BluetoothManager.Instance.CheckPermission(); // 檢查是否有開啟藍牙權限
        }
        catch (Exception e)
        {
            Debug.LogError($"[GarminBluetoothManager] Init failed on: {e.Message}");
        }
    }

    /// <summary>
    /// 藍芽連線的總管，這邊就是用來接收從外部傳來的訊息，並且做出相對應的動作
    /// </summary>
    /// <param name="message"></param>
    public void ConnectionManager(string message)
    {
        if (message.Equals("connect"))
        {
            ConnectToDevice();
            return;
        }
        else if (message.Equals("stop"))
        {
            CancelInvoke("StartReceiveData");
            return;
        }
        else if (message.Equals("disconnect"))
        {
            //CancelInvoke("StartReceiveData");
            DisconnectDevice();
            return;
        }
        StartCoroutine(SafeSendAfterConnect(message));
    }

    /* -------------------------------------------------------------------------- */

    /// <summary>
    /// 連接到藍牙裝置
    /// </summary>
    /// <param name="deviceName">有裝 GarminCompanion2024 裝置的名稱</param>
    public void ConnectToDevice(string deviceName="Galaxy Tab A9+")
    {
        if(BluetoothManager.Instance.IsConnected())
        {
            Debug.LogWarning("[GarminBluetoothManager] Already connected to a device.");
            return;
        }
        else
        {
            Debug.Log("[GarminBluetoothManager] Not connected to a device.");
        }

        // StopAllCoroutines();
        StartCoroutine(ConnectToDeviceAsync(deviceName));  // 裝置名稱
    }

    IEnumerator ConnectToDeviceAsync(string deviceName)
    {
        
        Debug.Log($"[GarminBluetoothManager] Start Connect to \"{deviceName}\"");

        BluetoothManager.Instance.CheckPermission();
        BluetoothManager.Instance.StartDiscovery();

        yield return new WaitForSecondsRealtime(3);
        var devices = BluetoothManager.Instance.GetAvailableDevices();
        var targetDevice = devices.FirstOrDefault(d => d.name == deviceName);

        if (targetDevice.name == null)
        {
            Debug.LogWarning($"[GarminBluetoothManager] Device \"{deviceName}\" not found.");
            yield break;
        }

        Debug.Log($"[GarminBluetoothManager] Found \"{targetDevice.name}\" ({targetDevice.mac}). Connecting...");

        bool success = BluetoothManager.Instance.Connect(targetDevice.mac, ""); // pin 預設空字串

        if (!success)
        {
            Debug.LogError("[GarminBluetoothManager] Connect failed.");
            yield break;
        }

        float timeout = 10f;
        float elapsed = 0f;
        while (!BluetoothManager.Instance.IsConnected() && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (BluetoothManager.Instance.IsConnected())
        {
            Debug.Log("[GarminBluetoothManager] Connected to device: " + BluetoothManager.Instance.GetConnectedDevice());
        }
        else
        {
            Debug.LogError("[GarminBluetoothManager] Connection timed out.");
        }
    }

    private IEnumerator SafeSendAfterConnect(string message)
    {
        float timeout = 10f;
        float timer = 0f;

        // 等待藍牙成功連線
        while (!connectionStatus && timer < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            timer += 0.5f;
        }

        if (!connectionStatus)
        {
            Debug.LogWarning("[GarminBluetoothManager] Failed to send message; device not connected.");
            yield break;
        }

        // 確保資料發送成功
        bool messageIsSent = false;
        while (!messageIsSent)
        {
            messageIsSent = BluetoothManager.Instance.Send(message);
            yield return null;
        }

        Debug.Log($"[GarminBluetoothManager] Message sent: {message}");

        if (CheckStartString(message))
        {
            StartCoroutine(ReceiveData());
        }
    }


    /* -------------------------------------------------------------------------- */

    /// <summary>
    /// 發送數據
    /// </summary>
    /// <param name="data"></param>
    public void SendData(string data)
    {
        if (connectionStatus)
        {
            if (!BluetoothManager.Instance.Send(data))
            {
                Debug.LogWarning("Failed to send data.");
            }
        }
        else
        {
            Debug.LogWarning("No device is connected.");
        }
    }

    // 停止藍牙連接
    public void DisconnectDevice()
    {
        try
        {
            if (connectionStatus)
            {                
                BluetoothManager.Instance.Stop(); // 使用 BluetoothManager 的方法斷開裝置
                // connectionStatus = false;
            }
        }
        catch(Exception ex)
        {
            Debug.LogWarning($"Failed to disconnect device,because of {ex.Message}");
        }
    }
       
    
    // 接收數據
    public IEnumerator ReceiveData()
    {
        while (BluetoothManager.Instance.IsConnected()) // While the device is connected
        {
            while (BluetoothManager.Instance.Available() > 0)
            {
                string data = BluetoothManager.Instance.ReadLine(); // Read data from BluetoothManager
                int endIndex = data.IndexOf('}'); // Find the index of the closing brace

                if (endIndex != -1)
                {
                    receivedData.Append(data.Substring(0, endIndex + 1)); // Append data up to and including the closing brace
                    if(!receivedData.ToString().Equals("error"))
                    {
                        ProcessReceivedData(receivedData.ToString()); // Process the complete data
                    }
                    receivedData.Clear(); // Clear the StringBuilder for the next set of datas

                    if(endIndex + 1 < data.Length)
                    {
                        receivedData.Append(data.Substring(endIndex + 1));
                    }
                }
                else
                {
                    receivedData.Append(data); // Append the entire data if no closing brace is found
                }
            }
            yield return null;
        }
    }


    /// <summary>
    /// 將處理好的資料送到LabDataManager
    /// </summary>
    /// <param name="data"></param>
    private void ProcessReceivedData(string data)
    {
        List<LabGarminData> _dataList = DeserializeLabGarminDataList(data);
        foreach(LabGarminData _data in _dataList)
        {
            LabDataManager.Instance.WriteData(_data);
        }
    }


    //學長寫法，處理收到的資料變成json格式
    List<LabGarminData> DeserializeLabGarminDataList(string jsonString)
    {
        List<LabGarminData> dataList = new List<LabGarminData>();
        jsonString = jsonString.Replace("}{", "},{");
        string[] jsonStrings = jsonString.Split(new[] { "},{" }, System.StringSplitOptions.None);

        // Process incomplete data.
        int _offset = 0;
        if (incompleteData.Length > 0)
        {
            string tmpJson = incompleteData + jsonStrings[0];
            _offset = 1;
            if (!tmpJson.StartsWith("{"))
            {
                tmpJson = "{" + tmpJson;
            }
            if (!tmpJson.EndsWith("}"))
            {
                tmpJson = tmpJson + "}";
            }
            LabGarminData data = new LabGarminData();
            try
            {
                data = JsonUtility.FromJson<LabGarminData>(tmpJson);
                if (CheckDisconnectMessage(data.tag))
                {
                    DisconnectDevice();
                    return dataList;
                }
                else
                {
                    labGarminData = data;
                    dataList.Add(data);
                    incompleteData = "";
                }
            }
            catch(Exception ex)
            {
                Debug.LogWarning($"Failed to deserialize data, because of {ex.Message}");
                incompleteData = "";
            }
        }

        foreach (var jsonStr in jsonStrings.Skip(_offset))
        {
            string json = jsonStr;
            if (!jsonStr.StartsWith("{"))
            {
                json = "{" + json;
            }
            if (!jsonStr.EndsWith("}"))
            {
                json = json + "}";
            }
            LabGarminData data = new LabGarminData();
            try
            {
                data = JsonUtility.FromJson<LabGarminData>(json);
                if (CheckDisconnectMessage(data.tag))
                {
                    DisconnectDevice();
                    return dataList;
                }
                else
                {
                    labGarminData = data;
                    dataList.Add(data);
                }
            }
            catch
            {
                if (jsonStr.StartsWith("{"))
                {
                    json = json.Substring(1);
                }
                if (jsonStr.EndsWith("}"))
                {
                    json = json.Substring(0, json.Length - 1);
                }
                incompleteData = json;
            }
        }
        return dataList;
    }

    //光玄寫法 : 就是用來檢查是否是斷線訊息
    bool CheckDisconnectMessage(string str)
    {
        if (str.Equals("device_disconnect"))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 學長寫法 : 檢查是否是start字串 主要是實驗中會有階段性的問題所以要更新
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    bool CheckStartString(string str)
    {
        if (!string.IsNullOrEmpty(str) && str.Length >= 5)
        {
            return str.Substring(0, 5).Equals("start");
        }
        return false;
    }
}

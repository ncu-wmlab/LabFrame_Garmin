using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using LabFrame2023;

public class GarminBluetoothManager : LabSingleton<GarminBluetoothManager>, IManager
{
    /// <summary>
    /// 目前是否已連線？
    /// </summary>
    public bool connectionStatus => BluetoothManager.Instance.IsConnected();

    /// <summary>
    /// 目前的資料
    /// </summary>
    public LabGarminData labGarminData = new LabGarminData();
    
    
    StringBuilder receivedData = new StringBuilder();

    string incompleteData = "";    


    // 初始化
    // void Start()
    // {

    //     // bluetoothManager = BluetoothManager.Instance; // 使用 BluetoothManager 的單例
    //     // bluetoothManager.ManagerInit(); // 初始化藍牙
    //     bluetoothManager.StartDiscovery(); // 開始搜尋藍牙裝置
    //     try
    //     {
    //         bluetoothManager.CheckPermission(); // 檢查是否有開啟藍牙權限
    //     }
    //     catch (Exception ex)
    //     {
    //         Debug.LogWarning($"Failed to disconnect device,because of {ex.Message}.");
    //     }
    // }


    public void ManagerInit()
    {
        BluetoothManager.Instance.CheckPermission();        
    }

    public IEnumerator ManagerDispose()
    {
        DisconnectDevice();
        yield break;
    }

    /* -------------------------------------------------------------------------- */


    /// <summary>
    /// 透過 message 字串去做相對應的動作。對於使用 UI Button 控制可以很方便。如果是要寫腳本可以不需要用到這個方法。
    /// </summary>
    /// <param name="message"></param>
    public void ConnectionManager(string message)
    {
        if (message.Equals("connect"))
        {
            ConnectToDevice();            
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

        // Case start
        bool messageIsSend = false;
        while(!messageIsSend)
        {
            messageIsSend = BluetoothManager.Instance.Send(message);
        }

        // 開始接收資料: "start"
        if (CheckStartString(message))
        {
            StartCoroutine(ReceiveData());
        }
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

        // StopAllCoroutines();
        StartCoroutine(ConnectToDeviceAsync(deviceName));  // 裝置名稱
    }

    IEnumerator ConnectToDeviceAsync(string deviceName)
    {
        
        Debug.Log($"[GarminBluetoothManager] Start Connect to \"{deviceName}\"");

        BluetoothManager.Instance.CheckPermission();
        BluetoothManager.Instance.StartDiscovery();

        yield return new WaitForSecondsRealtime(3);

        while(!BluetoothManager.Instance.IsConnected())
        {
            var devices = BluetoothManager.Instance.GetAvailableDevices();
            if(devices.Any(d => d.name == deviceName))
            {
                Debug.Log($"[GarminBluetoothManager] Found device \"{deviceName}\", now connecting...");
                BluetoothManager.Instance.Connect(devices.First(d => d.name == deviceName).mac);
            }
            yield return null;
        }

        Debug.Log("[GarminBluetoothManager] Connected to "+ BluetoothManager.Instance.GetConnectedDevice());
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

    /// <summary>
    /// 停止藍牙連接
    /// </summary>
    public void DisconnectDevice()
    {
        Debug.Log("[GarminBluetoothManager] Disconnecting...");
        StopAllCoroutines();
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
            Debug.LogWarning($"Failed to disconnect device: {ex.Message}");
        }
    }

    /* -------------------------------------------------------------------------- */
    
    /// <summary>
    /// 開始接收數據
    /// </summary>
    public void StartReceiveData()
    {
        StartCoroutine(ReceiveData());
    }
    
    // 接收數據
    IEnumerator ReceiveData()
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
    /// 將處理好的資料送到 LabDataManager
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


    /// <summary>
    /// 光玄寫法，處理收到的資料變成json格式
    /// </summary>
    /// <param name="jsonString"></param>
    /// <returns></returns>
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

            try
            {
                LabGarminData data = JsonUtility.FromJson<LabGarminData>(tmpJson);
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

    /// <summary>
    /// 光玄寫法 : 就是用來檢查是否是斷線訊息
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    bool CheckDisconnectMessage(string str)
    {
        if (str.Equals("device_disconnect"))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 光玄寫法 : 檢查是否是start字串，主要是實驗中會有階段性的問題所以要更新
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

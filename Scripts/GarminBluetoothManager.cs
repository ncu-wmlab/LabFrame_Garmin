using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;

public class GarminBluetoothManager : MonoBehaviour
{
    private BluetoothManager bluetoothManager;

    public bool connectionStatus = false;
    public static GarminBluetoothManager instance;
    
    public static LabDataManager labDataManager;

    private StringBuilder receivedData = new StringBuilder();

    string incompleteData = "";

    public LabGarminData labGarminData = new LabGarminData();


    // 初始化
    void Start()
    {
        if (instance)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        bluetoothManager = BluetoothManager.Instance; // 使用 BluetoothManager 的單例
        bluetoothManager.ManagerInit(); // 初始化藍牙
        bluetoothManager.StartDiscovery(); // 開始搜尋藍牙裝置
        try
        {
            bluetoothManager.CheckPermission(); // 檢查是否有開啟藍牙權限
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to disconnect device,because of {ex.Message}.");
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
        }
        else if (message.Equals("stop"))
        {
            CancelInvoke("StartReceiveData");
        }
        else if (message.Equals("disconnect"))
        {
            //CancelInvoke("StartReceiveData");
            DisconnectDevice();
        }

        //Case start
        bool messageIsSend = false;
        while(!messageIsSend)
        {
            messageIsSend = bluetoothManager.Send(message);
        }
        if (CheckStartString(message))
        {
            // Set the data receiving rate 
            StartCoroutine(ReceiveData());
        }


    }

    // 取得藍牙裝置的 MAC 位址
    public string GetMacAddress()
    {
        List<BluetoothDevice> devices = bluetoothManager.GetAvailableDevices();
        foreach (BluetoothDevice device in devices)
        {
            if (device.name == "Galaxy Tab A9+") // 裝置名稱
            {
                return device.mac;
            }
        }
        return null;
    }

    // 連接到藍牙裝置
    public void ConnectToDevice()
    {
        string macAddress = GetMacAddress();
        if (bluetoothManager.CheckAvailable())
        {
            // 使用 BluetoothManager 的方法連接裝置
            if(bluetoothManager.Connect(macAddress))
            {    
                connectionStatus = true;
            }
        }
        else
        {
            Debug.LogWarning("Bluetooth is not available on this device.");
        }
    }

    // 發送數據
    public void SendData(string data)
    {
        if (connectionStatus)
        {
            bool success = bluetoothManager.Send(data); // 使用 BluetoothManager 的 Send 方法
            if (!success)
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
                bluetoothManager.Stop(); // 使用 BluetoothManager 的方法斷開裝置
                connectionStatus = false;
            }
        }
        catch(Exception ex)
        {
            Debug.LogWarning($"Failed to disconnect device,because of {ex.Message}");
        }
    }
    
    


    public void StartReceiveData()
    {
        StartCoroutine(ReceiveData());
    }
    
    // 接收數據
    public IEnumerator ReceiveData()
    {
        while (bluetoothManager.IsConnected()) // While the device is connected
        {
            if (bluetoothManager.Available() > 0)
            {
                string data = bluetoothManager.ReadLine(); // Read data from BluetoothManager
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


    //光玄寫法，處理收到的資料變成json格式
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

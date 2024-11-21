using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GarminSampleManager : MonoBehaviour
{
    [SerializeField] Text _statusText;
    [SerializeField] InputField _deviceNameInput;

    [SerializeField] Button _connectButton;
    [SerializeField] Button _disconnectButton;



    // Start is called before the first frame update
    void Start()
    {
        // 註冊按鈕
        _connectButton.onClick.AddListener(()=>{
            if(string.IsNullOrWhiteSpace(_deviceNameInput.text))
            {
                GarminBluetoothManager.instance.ConnectToDevice();
            }
            else
            {
                // GarminBluetoothManager.Instance.ConnectToDevice(_deviceNameInput.text);
            }
        });
        _disconnectButton.onClick.AddListener(()=>GarminBluetoothManager.instance.DisconnectDevice());

        // 初始化 LabDataManager
        LabDataManager.Instance.LabDataInit("garminTest");     

    }

    // Update is called once per frame
    void Update()
    {

        _statusText.text = "連線狀態：";
        _statusText.text += GarminBluetoothManager.instance.connectionStatus ? "已連線" : "未連線";

        if(GarminBluetoothManager.instance.connectionStatus)
        {
            _statusText.text += "\nheartRate: " + GarminBluetoothManager.instance.labGarminData.heartRate + 
                "\nrestingHeartRate: " + GarminBluetoothManager.instance.labGarminData.restingHeartRate +
                "\nheartRateVariability: " + GarminBluetoothManager.instance.labGarminData.heartRateVariability +
                "\naccelerometer_x: " + GarminBluetoothManager.instance.labGarminData.accelerometer_x +
                "\naccelerometer_y: " + GarminBluetoothManager.instance.labGarminData.accelerometer_y +
                "\naccelerometer_z: " + GarminBluetoothManager.instance.labGarminData.accelerometer_z +
                "\nsteps: " + GarminBluetoothManager.instance.labGarminData.steps +
                "\ncalories_Total: " + GarminBluetoothManager.instance.labGarminData.calories_Total +
                "\ncalories_Active: " + GarminBluetoothManager.instance.labGarminData.calories_Active +
                "\nfloors_Climb: " + GarminBluetoothManager.instance.labGarminData.floors_Climb +
                "\nfloors_Descend: " + GarminBluetoothManager.instance.labGarminData.floors_Descend +
                "\nintensityMinutes_Moderate: " + GarminBluetoothManager.instance.labGarminData.intensityMinutes_Moderate +
                "\nintensityMinutes_Vigorous: " + GarminBluetoothManager.instance.labGarminData.intensityMinutes_Vigorous +
                "\nstressLevel: " + GarminBluetoothManager.instance.labGarminData.stressLevel +
                "\nSPO2: " + GarminBluetoothManager.instance.labGarminData.SPO2 +
                "\nbodyBattery: " + GarminBluetoothManager.instance.labGarminData.bodyBattery +
                "\nrespiration: " + GarminBluetoothManager.instance.labGarminData.respiration +
                "\ntag: " + GarminBluetoothManager.instance.labGarminData.tag +               
                "\ntime: " + GarminBluetoothManager.instance.labGarminData.time +
                "\ntimeStamp: " + GarminBluetoothManager.instance.labGarminData.Timestamp
            ;
        }
    }
}

using LabFrame2023;

[System.Serializable]
public class LabGarminData : LabDataBase
{
    /// <summary> 心率 </summary>
    public int heartRate;
    /// <summary> 靜息心率 </summary>
    public int restingHeartRate;
    /// <summary> 心率變異度(RR-interval) </summary>
    public int heartRateVariability;
    /// <summary> 手錶加速度 (X) </summary>
    public int accelerometer_x;
    /// <summary> 手錶加速度 (Y) </summary>
    public int accelerometer_y;
    /// <summary> 手錶加速度 (Z) </summary>
    public int accelerometer_z;
    /// <summary> 步數 </summary>
    public int steps;    
    /// <summary> 總消耗卡路里 </summary>
    public int calories_Total;
    /// <summary>  </summary>
    public int calories_Active;
    /// <summary> 爬階梯數 </summary>
    public int floors_Climb;
    /// <summary> 下階梯數 </summary>
    public int floors_Descend;
    /// <summary>  </summary>
    public int intensityMinutes_Moderate;
    /// <summary>  </summary>
    public int intensityMinutes_Vigorous;
    /// <summary> 壓力指數 </summary>
    public int stressLevel;
    /// <summary> 血氧濃度 </summary>
    public int SPO2;
    /// <summary> "Body Battery" </summary>
    public int bodyBattery;

    /// <summary> 呼吸速率 </summary>
    public int respiration;
    public string time;
    public string tag;

    public LabGarminData()
    {
        
    }
}

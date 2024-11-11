using LabFrame2023;

[System.Serializable]
public class LabGarminData : LabDataBase
{
    public int heartRate;
    public int restingHeartRate;
    public int heartRateVariability;
    public int accelerometer_x;
    public int accelerometer_y;
    public int accelerometer_z;
    public int steps;
    public int calories_Total;
    public int calories_Active;
    public int floors_Climb;
    public int floors_Descend;
    public int intensityMinutes_Moderate;
    public int intensityMinutes_Vigorous;
    public int stressLevel;
    public int SPO2;
    public int bodyBattery;
    public int respiration;
    public string time;
    public string tag;

    public LabGarminData()
    {
        this.heartRate = 0;
        this.restingHeartRate = 0;
        this.heartRateVariability = 0;
        this.accelerometer_x = 0;
        this.accelerometer_y = 0;
        this.accelerometer_z = 0;
        this.steps = 0;
        this.calories_Total = 0;
        this.calories_Active = 0;
        this.floors_Climb = 0;
        this.floors_Descend = 0;
        this.intensityMinutes_Moderate = 0;
        this.intensityMinutes_Vigorous = 0;
        this.stressLevel = 0;
        this.SPO2 = 0;
        this.bodyBattery = 0;
        this.respiration = 0;
        this.time = "null";
        this.tag = "null";
    }
}

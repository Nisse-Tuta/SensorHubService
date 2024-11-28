using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;

public class TempSensorSenseArduino : TempSensor
{
    public override async Task<SensorVauleDTO?> GetValue(string? indata = null)
    {
        await Task.Yield();
        if (!string.IsNullOrEmpty(indata) && indata.Contains("TempC:"))
        {
            string tmpStr = indata.Substring(indata.IndexOf("TempC:") + 6);
            tmpStr = tmpStr.Substring(0, tmpStr.IndexOf(";"));
            return new SensorVauleDTO
            {
                SensorId = SensorId,
                Tiden = DateTime.Now,
                Unit = Unit,
                Value = Double.Parse(tmpStr)
            };   
        }
        else
        {
            return null; 
        }
    }
}
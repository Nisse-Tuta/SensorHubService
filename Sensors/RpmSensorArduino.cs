using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;

public class RpmSensor : ISensor
{
    public virtual string SensorType { get { return "Rpm"; } }
    public virtual string SensorId { get { return "Rpm"; } }
    public virtual string Unit { get { return "Rpm"; } }
    public virtual Task<SensorVauleDTO> GetValue(string? indata = null)
    {
        throw new NotImplementedException();
    }
}

public class RpmSensorArduino : RpmSensor
{
    public override async Task<SensorVauleDTO?> GetValue(string? indata = null)
    {
        await Task.Yield();
        if (!string.IsNullOrEmpty(indata) && indata.Contains("Rpm:"))
        {
            string tmpStr = indata.Substring(indata.IndexOf("Rpm:") + 4);
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
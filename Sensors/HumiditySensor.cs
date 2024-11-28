using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;

public class HumiditySensor : ISensor
{
    public virtual string SensorType { get { return "Humidity"; } }
    public virtual string SensorId { get { return "Humidity"; } }
    public virtual string Unit { get { return "%"; } }
    public virtual Task<SensorVauleDTO> GetValue(string? indata = null)
    {
        throw new NotImplementedException();
    }
}

public class HumiditySensorDemo : HumiditySensor
{
    private int demoLap = 0;
    private int minValue = 20;
    private int maxValue = 70-1;
    private bool increaseVal = true;
    public override async Task<SensorVauleDTO> GetValue(string? indata = null)
    {
        double value = increaseVal ? minValue + demoLap : maxValue - demoLap;
        demoLap++;

        if (minValue > value || value > maxValue)
        {
            increaseVal = !increaseVal;
            demoLap = 0; 
        }

        return new SensorVauleDTO
        {
            SensorId = SensorId,
            Tiden = DateTime.Now,
            Unit = Unit,
            Value = value
        };
    }
}
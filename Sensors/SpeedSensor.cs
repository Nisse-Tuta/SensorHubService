using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;

public class SpeedSensor : ISensor
{
    public virtual string SensorType { get { return "Speed"; } }
    public virtual string SensorId { get { return "Speed"; } }
    public virtual string Unit { get { return "km/h"; } }
    public virtual Task<SensorVauleDTO> GetValue(string? indata = null)
    {
        throw new NotImplementedException();
    }
}

public class SpeedSensorDemo : SpeedSensor
{
    private int demoLap = 0;
    private int minValue = 0;
    private int maxValue = 120 - 1;
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

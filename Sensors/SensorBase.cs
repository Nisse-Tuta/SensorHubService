using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;

public abstract class SensorBase : ISensor
{
    public virtual string SensorType { get; }
    public virtual string SensorId { get; }

    protected SensorBase(string sensorType, string sensorId)
    {
        SensorType = sensorType;
        SensorId = sensorId; 
    }

    public abstract Task<SensorVauleDTO> GetValue(string? indata = null);
}


using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;

public interface ISensor
{
    string SensorType { get; }
    string SensorId { get; }
    Task<SensorVauleDTO?> GetValue(string? indata = null);
}

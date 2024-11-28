namespace RaspSensorService.DTO; 

public class SensorVauleDTO
{
    public string SensorId { get; set; }
    public Double Value { get; set; } = 0;
    public string Unit { get; set; }
    public DateTime Tiden { get; set; } = DateTime.MinValue;
}

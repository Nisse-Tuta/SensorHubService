using RaspSensorService.DTO;
using SensorHubService.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SensorHubService.Managers; 

public enum OdometerType
{
    Total,
    Yearly, 
    FuelCheck,
    Temporary
}

public class OdometerData
{
    public DateTime Date { get; set; }

    public double TotalKm { get; set; }
    public double YearlyKm { get; set; }
    public double FuelCheckKm { get; set; }
    public double TemporaryKm { get; set; }
}

public class OdometerManager
{
  private readonly string _filePath;
  private OdometerData odometerData = new OdometerData();

  public OdometerManager()
  {
    if (OperatingSystem.IsWindows())
    {
      _filePath = "D:\\tmp\\OdometerFile.json";
    }
    else
    {
      //_filePath = "~/tmp/OdometerFile.txt";
      _filePath = $"/home/chris/tmp/OdometerFile.txt";
    }

    if (File.Exists(_filePath))
    {
      string[] lines = File.ReadAllLines(_filePath);
      odometerData = JsonSerializer.Deserialize<OdometerData>(lines[lines.Length - 1])!;
    }
  }

  public OdometerData? GetOdometerData()
  {
    return odometerData;
  }

  public OdometerData AddOdometerData(double addkm)
  {
    odometerData.Date = DateTime.Now;
    odometerData.TotalKm += addkm;
    odometerData.YearlyKm += addkm;
    odometerData.FuelCheckKm += addkm;
    odometerData.TemporaryKm += addkm;
    File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(odometerData));

    return odometerData;
  }

  public OdometerData ResetOdometerData(OdometerType odometerType)
  {
    switch (odometerType)
    {
      case OdometerType.Yearly:
        odometerData.YearlyKm = 0;
        break;
      case OdometerType.FuelCheck:
        odometerData.FuelCheckKm = 0;
        break;
      case OdometerType.Temporary:
        odometerData.TemporaryKm = 0;
        break;
      default:
        break;
    }
    File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(odometerData));
    return odometerData;
  }

  public void MakeSensorValuesOfOdometer(ListVaulesDTO result)
  {
    SensorVauleDTO gpsSen = new SensorVauleDTO()
    {
      SensorId = "OdometerTotalKm",
      Tiden = DateTime.Now,
      Unit = "km",
      Value = odometerData.TotalKm
    };
    result.SensorValues.Add(gpsSen);

    gpsSen = new SensorVauleDTO()
    {
      SensorId = "OdometerYearlyKm",
      Tiden = DateTime.Now,
      Unit = "km",
      Value = odometerData.YearlyKm
    };
    result.SensorValues.Add(gpsSen);

    gpsSen = new SensorVauleDTO()
    {
      SensorId = "OdometerFuelCheckKm",
      Tiden = DateTime.Now,
      Unit = "km",
      Value = odometerData.FuelCheckKm
    };
    result.SensorValues.Add(gpsSen);

    gpsSen = new SensorVauleDTO()
    {
      SensorId = "OdometerTemporaryKm",
      Tiden = DateTime.Now,
      Unit = "km",
      Value = odometerData.TemporaryKm
    };
    result.SensorValues.Add(gpsSen);
  }

}


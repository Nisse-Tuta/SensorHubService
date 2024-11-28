using System;
using System.Drawing;
using System.Threading;
using Iot.Device.Common;
using Iot.Device.SenseHat;
using UnitsNet;
using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;


public class TempSensorSenseHat : TempSensor
{
    public override async Task<SensorVauleDTO> GetValue(string? indata = null)
    {
        await Task.Yield();
        using SenseHat sh = new();

        sh.SetPixel(1, 2, Color.DarkGoldenrod);

        var preValue = sh.Temperature;
        var retValue = preValue.DegreesCelsius;
        sh.Fill(Color.Black);

        return new SensorVauleDTO
        {
            SensorId = SensorId,
            Tiden = DateTime.Now,
            Unit = Unit,
            Value = retValue
        };

    }

}

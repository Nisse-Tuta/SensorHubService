using System;
using System.Drawing;
using System.Threading;
using Iot.Device.Common;
using Iot.Device.SenseHat;
using UnitsNet;
using RaspSensorService.DTO;

namespace RaspSensorService.Sensors;


public class HumiditySensorSenseHat : HumiditySensor
{
    public override async Task<SensorVauleDTO> GetValue(string? indata = null)
    {
        await Task.Yield();
        using SenseHat sh = new();

        //sh.Fill(Color.DarkBlue);
        sh.SetPixel(1, 1, Color.DarkBlue);

        var preValue = sh.Humidity;
        var humValue = preValue.Percent;
        sh.Fill(Color.Black);

        return new SensorVauleDTO
        {
            SensorId = SensorId,
            Tiden = DateTime.Now,
            Unit = Unit,
            Value = humValue
        };
    }

}

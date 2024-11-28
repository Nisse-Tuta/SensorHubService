using RaspSensorService.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspSensorService.Sensors;

public static class SensorFactory
{
    public static ISensor Sensor(string sensor, bool demo)
    {
        switch (sensor)
        {
            case "HumiditySensor": 
                return demo ? new HumiditySensorDemo() : new HumiditySenseArduino();
            case "SpeedSensor":
                return demo ? new SpeedSensorDemo() : new SpeedSensor();
            case "TempSensor":
                return demo ? new TempSensorDemo() : new TempSensorSenseArduino();
            case "RpmSensor":
                return new RpmSensorArduino();
            default: 
                throw new NotImplementedException(); 
        }
    }
}


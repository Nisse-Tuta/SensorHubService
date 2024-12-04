using Iot.Device.Mcp23xxx;
using Iot.Device.Nmea0183;
using Iot.Device.Pn532;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using RaspSensorService.DTO;
using RaspSensorService.Sensors;
using SensorHubService;
using SensorHubService.DTO;
using SensorHubService.Managers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text.Json;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RaspSensorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IHubContext<RaspSensorHub> _hubContext;
        private IServer _server;
        private SerialPort _arduinoSerialPort;
        private SerialPort _garminSerialPort;
        private string ardResult = "";
        private string garminResult = "";
        private string jsonfile = "";
        private string gpsDatafile = "";
        private string gpsSimfile = "";

        private OdometerManager odometerManager;
        private OdometerData odometerData;
        private Queue<GpsDataDTO> gpsDataQueue = new Queue<GpsDataDTO>();
        private readonly object _gpsLock = new();

        public Worker(ILogger<Worker> logger, IHubContext<RaspSensorHub> hubContext, IServer server)
        {
            _logger = logger;
            _hubContext = hubContext;
            _server = server;
            _arduinoSerialPort = new SerialPort();
        }

 
        void GarminDataReceiveHandler(object sender, SerialDataReceivedEventArgs e)
        {
            lock (_gpsLock)
            {
                garminResult += _garminSerialPort.ReadExisting();
                //Console.WriteLine($"garminResult : {garminResult}");
                //_logger.LogInformation("ardResult : {ardResult}", ardResult);
                File.AppendAllText(gpsDatafile, garminResult);
            }
        }

        void ArduinoDataReceiveHandler(object sender, SerialDataReceivedEventArgs e)
        {
            ardResult = _arduinoSerialPort.ReadExisting();
            //Console.WriteLine($"ardResult : {ardResult}");
            //_logger.LogInformation("ardResult : {ardResult}", ardResult);
        }

        private static SerialPort SetupPort(string port)
        {
            SerialPort _port = new SerialPort(port, 9600);
            _port.StopBits = StopBits.One;
            _port.Parity = Parity.None;
            _port.DataBits = 8;
            _port.ReadTimeout = 500;
            _port.WriteTimeout = 500;
            _port.Handshake = Handshake.None;
            _port.RtsEnable = true;
            _port.DtrEnable = true;  // Viktig 

            int retryCount = 0;
            int maxRetries = 3;
            while (retryCount < maxRetries)
            {
                try
                {
                    _port.Open();
                    Console.WriteLine($"Device: {port} connected");
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException($"Failed to open port {port} after {maxRetries} attempts.", ex);
                    }
                    Thread.Sleep(1000);
                }
            }
            return _port;
        }

        private void ProcessGpsData(GpsDataDTO? gpsData, ListVaulesDTO result)
        {
            SensorVauleDTO kmSen = new SensorVauleDTO()
            {
                SensorId = "Speed",
                Tiden = gpsData?.UTCTime ?? DateTime.Now,
                Unit = "km/h",
                Value = gpsData != null && gpsData.fixValid ? (gpsData.SpeedKph < 1 ? gpsData.SpeedKph * 100 : gpsData.SpeedKph) : 0
            };
            result.SensorValues.Add(kmSen);

            SensorVauleDTO satUsed = new SensorVauleDTO()
            {
                SensorId = "SatelitesUsed",
                Tiden = gpsData?.UTCTime ?? DateTime.Now,
                Unit = "",
                Value = gpsData != null && gpsData.fixValid ? gpsData.SatelitesUsed : 0
            };
            result.SensorValues.Add(satUsed);

            gpsDataQueue.Enqueue(gpsData);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool isDemo = true;
            List<ISensor> sensorList = [];


            string arduinoPort = "";
            string garminPort = "";
            string todayStr = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            if (OperatingSystem.IsWindows())
            {
                arduinoPort = "COM7";
                garminPort = "COM3";
                //garminPort = "COM5";
                jsonfile = $"D:\\tmp\\{todayStr}_Json.json";
                gpsDatafile = $"D:\\tmp\\{todayStr}_GpsData.txt";
            }
            else
            {
                arduinoPort = "/dev/ttyACM0";
                garminPort = "/dev/rfcomm0";
                jsonfile = $"/home/chris/tmp/{todayStr}_Json.json";
                gpsDatafile = $"/home/chris/tmp/{todayStr}_GpsData.txt";
            }


            if (!OperatingSystem.IsWindows())
            {
                _arduinoSerialPort = SetupPort(arduinoPort);
                _arduinoSerialPort.DataReceived += ArduinoDataReceiveHandler; // Add DataReceived Event Handler
            }

            // gpsSimfile = $"D:\\GpsData\\2024-12-04_16-17_GpsData.txt";
            List<string> gpsDataLst = new List<string>();
            if (string.IsNullOrEmpty(gpsSimfile))
            {
                _garminSerialPort = SetupPort(garminPort);
                _garminSerialPort.DataReceived += GarminDataReceiveHandler; // Add DataReceived Event Handler
            }
            else
            {
                gpsDataLst = File.ReadAllLines(gpsSimfile).ToList();
            }

            // lägg till de sensorer vi har 
            sensorList.Add(SensorFactory.Sensor("HumiditySensor", false));
            sensorList.Add(SensorFactory.Sensor("TempSensor", false));
            //sensorList.Add(SensorFactory.Sensor("SpeedSensor", isDemo));
            sensorList.Add(SensorFactory.Sensor("RpmSensor", false));

            //sensorList.Add(SensorFactory.Sensor("HumiditySensor", isDemo));
            //sensorList.Add(SensorFactory.Sensor("TempSensor", isDemo));
            //sensorList.Add(SensorFactory.Sensor("SpeedSensor", isDemo));


            odometerManager = new OdometerManager();
            odometerData = odometerManager.GetOdometerData();

            Stopwatch stopwatch = new Stopwatch();
            ListVaulesDTO result = new ListVaulesDTO();
            int loops = 0;
            long elapsedTotal = 0;
            long elapsedForSecond = 0;
            stopwatch.Restart();
            int antalGps = 0;
            int failGps = 0;
            int okGps = 0;
            int gpsDataIndex = 0;
            while (!stoppingToken.IsCancellationRequested)
            {

                if (gpsDataLst.Count > 0 && gpsDataIndex < gpsDataLst.Count)
                {
                    garminResult = gpsDataLst[gpsDataIndex];
                    gpsDataIndex++;
                    if (gpsDataIndex >= gpsDataLst.Count) { gpsDataIndex = 0; }
                }

                if (string.IsNullOrEmpty(garminResult) && string.IsNullOrEmpty(ardResult)) { continue; }
                loops++;
                result = new();

                lock (_gpsLock)
                {
                    if (!string.IsNullOrEmpty(garminResult) && garminResult.StartsWith("$GN") && garminResult.IndexOf("*") > 0)
                    {
                        GpsDataDTO? gpsData = null;
                        gpsData = GpsManager.ParseNmeaSentence(garminResult);
                        garminResult = string.Empty;
                        antalGps++;
                        ProcessGpsData(gpsData, result);
                    }
                    else if (!garminResult.StartsWith("$GN")) // hamnat i osync med gpsen
                    {
                        failGps++;
                        garminResult = string.Empty;
                    }

                }

                if (!string.IsNullOrEmpty(ardResult))
                {
                    foreach (var sensor in sensorList)
                    {
                        var sensedVal = await sensor.GetValue(ardResult);
                        if (sensedVal != null)
                        {
                            result.SensorValues.Add(sensedVal);
                            if (loops == 500)
                            {
                                _logger.LogInformation("Measure {SensorType} Value {value}", sensor.SensorType, sensedVal.Value);
                            }
                        }
                    }
                    if (ardResult.Contains("count"))
                    {
                        string tmpStr = ardResult.Substring(ardResult.IndexOf("count:") + 6);
                        tmpStr = tmpStr.Substring(0, tmpStr.IndexOf(";"));
                        SensorVauleDTO kmSen = new SensorVauleDTO()
                        {
                            SensorId = "ArduinoCount",
                            Tiden = DateTime.Now,
                            Unit = "",
                            Value = Double.Parse(tmpStr)
                        };
                        result.SensorValues.Add(kmSen);
                    }
                }
                
                // Job som ska gå typ varje sekund
                if (elapsedForSecond > 1000)
                {
                    if (gpsDataQueue.Count >= 10)
                    {
                        double distkm = GpsManager.CalculateDistance(gpsDataQueue);
                        //Console.WriteLine($"km: {distkm}   ");
                        odometerData = odometerManager.AddOdometerDataAsync(distkm);
                        odometerManager.MakeSensorValuesOfOdometer(result);
                        gpsDataQueue.Clear();
                        elapsedForSecond = 0;
                    }
                }

                if (!OperatingSystem.IsWindows())
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveSensorEvents", result);
                    await File.AppendAllTextAsync(jsonfile, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }

                elapsedTotal += stopwatch.ElapsedMilliseconds;

                if (loops >= 100)
                {
                    long avgElapsed = elapsedTotal / loops;
                    _logger.LogInformation("Arduino string: {ardStr}", ardResult);
                    //_logger.LogInformation("SignalR gpsDataLst string: {result}", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                    _logger.LogInformation("GPS antal: {okstr} ok: {okint} fail: {failed} queLen: {qlen} ", antalGps, okGps, failGps, gpsDataQueue.Count);
                    _logger.LogInformation("Mätning och SignalR över {loops} varv snittade : {time}ms  senaste: {lastElapsed}ms", loops, avgElapsed, stopwatch.ElapsedMilliseconds);
                    loops = 0;
                    elapsedTotal = 0;
                }

                int delay = 100 - (int)stopwatch.ElapsedMilliseconds < 0 ? 0 : 100 - (int)stopwatch.ElapsedMilliseconds;
                //Console.WriteLine($"delay: {delay}");
                await Task.Delay(delay, stoppingToken);
                elapsedForSecond += stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();
            }
            _arduinoSerialPort.Close();
            _garminSerialPort.Close();
        }


    }
}

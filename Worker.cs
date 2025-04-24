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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Management;
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
    private int gpsSimfileStrtRow = 0;

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

    // Eventhandler som läser data från Garmin GPS
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

      // Detta om vi vill använda en simulerad gps-fil
      if (OperatingSystem.IsWindows())
      {
        //gpsSimfile = $"D:\\GpsData\\2025-04-24_17-42_GpsData_hem_Ica";
        //gpsSimfile = $"D:\\GpsData\\2025-04-24_17-58_GpsData_Ica_OK.txt";
        gpsSimfile = $"D:\\GpsData\\2025-04-24_18-11_GpsData_OK_Hem.txt";
        gpsSimfileStrtRow = 17800; 
      }
      else
        gpsSimfile = $"/home/chris/GpsData/GpsData_Jul_bussen.txt";

      List<string> gpsDataLst = new List<string>();
      if (string.IsNullOrEmpty(gpsSimfile))
      {
        _garminSerialPort = SetupPort(garminPort);
        _garminSerialPort.DataReceived += GarminDataReceiveHandler; // Add DataReceived Event Handler
      }
      else
      {
        gpsDataLst = File.ReadAllLines(gpsSimfile).ToList();
        if (gpsDataLst.Count > gpsSimfileStrtRow)
        {
          // remove first gpsSimfileStrtRow lines from the list
          gpsDataLst = gpsDataLst.Skip(gpsSimfileStrtRow).ToList();
        }
      }

      // lägg till de sensorer vi har 
      bool isDemo = false;
      //sensorList.Add(SensorFactory.Sensor("HumiditySensor", isDemo));
      //sensorList.Add(SensorFactory.Sensor("TempSensor", isDemo));
      //sensorList.Add(SensorFactory.Sensor("RpmSensor", isDemo));
      sensorList.Add(SensorFactory.Sensor("SpeedSensor", isDemo));

      odometerManager = new OdometerManager();
      odometerData = odometerManager.GetOdometerData();

      Stopwatch stopwatch = new Stopwatch();
      ListVaulesDTO result = new ListVaulesDTO();
      int loops = 0;
      long elapsedTotal = 0;
      long elapsedForMilliSecond = 0;
      stopwatch.Restart();
      int antalGps = 0;
      int failGps = 0;
      int lastNrStatsUsed = -1;
      int gpsDataIndex = 0;
      double tripKm = 0;
      double trimMin = 0;
      bool someZeroSats = false;
      while (!stoppingToken.IsCancellationRequested)
      {

        // Plocka data från sim-filen om vi har en sådan.
        // Annars ska det läst in med eventhandlern ovan
        if (!string.IsNullOrEmpty(gpsSimfile) && gpsDataLst.Count > 0 && gpsDataIndex < gpsDataLst.Count)
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
            if (gpsData == null) { continue; }
            // gpsData.SatelitesUsed sätts bara av GNGGA så vi sparar senaste och sätter på GNRMC
            if (gpsData.SatelitesUsed >= 0) { lastNrStatsUsed = gpsData.SatelitesUsed; continue; }
            if (lastNrStatsUsed >= 0 && gpsData.SatelitesUsed == -1) { gpsData.SatelitesUsed = lastNrStatsUsed; }
            // Kommer vi hit så är det en OK gpsData från GNRMC 

            antalGps++;
            // Denna bygger även gpsDataQueue
            // ProcessGpsData(gpsData, result);
            gpsDataQueue.Enqueue(gpsData);
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

        // Job som inte ska gå varje varv 
        if (elapsedForMilliSecond > 50 && gpsDataQueue.Count > 0)
        {
          const int maxNrForSpeed = 5;
          const int maxNrForOdo = 20;
          var first = gpsDataQueue.First();
          var last = gpsDataQueue.Last();

          if (last.SatelitesUsed < 1)
          {
            //Console.WriteLine($"WARNING! Sats: {last.SatelitesUsed}  len: {gpsDataQueue.Count} {last.UTCTime.ToLocalTime()}");
            someZeroSats = true; 
          }


          // Först speed 
          List<GpsDataDTO> speedQueList = ExtracNrOfOKentries(gpsDataQueue, maxNrForSpeed);
          if (speedQueList?.Count >= maxNrForSpeed)
          {
            double avgSpeed = Math.Round(GpsManager.CalculateSpeed(speedQueList));

            SensorVauleDTO speedSens = new SensorVauleDTO()
            {
              SensorId = "Speed",
              Tiden = speedQueList.First().UTCTime,
              Unit = "kph",
              Value = avgSpeed
            };
            result.SensorValues.Add(speedSens);
          }

          // Nu sträckan, odometer och trip
          List<GpsDataDTO> odoQueList = ExtracNrOfOKentries(gpsDataQueue, maxNrForOdo);
          if (odoQueList.Count >= maxNrForOdo)
          {
            double avgSpeed = GpsManager.CalculateSpeed(odoQueList);

            // get time elapsed between first and last gps data in the odoQueList
            // OBS! ExtracNrOfOKentries gör att senaste är först
            var timeElapsed = Math.Round((odoQueList.First().UTCTime - odoQueList.Last().UTCTime).TotalSeconds, 2);
            trimMin += timeElapsed / 60;
            // distance in km by speed in kph times time elapsed in seconds
            double distKm = avgSpeed * timeElapsed / 3600;
            if (distKm < 0) { continue; } // Uppenbbart FEL 
            double distM = Math.Round(distKm * 1000, 1);
            tripKm += distKm;
            int satsUsed = gpsDataQueue.Last().SatelitesUsed;

            Console.WriteLine($"speed: {Math.Round(avgSpeed)} km/h  kph  dist: {distM} m  timeElapsed: {timeElapsed}   trip: {Math.Round(tripKm, 2)} km  {Math.Round(trimMin, 1)} min  Sats: {satsUsed}  {gpsDataQueue.Last().UTCTime.ToLocalTime()}");
            odometerData = odometerManager.AddOdometerData(distKm);
            // MakeSensorValuesOfOdometer gör om till sensordata och sparar i result 
            odometerManager.MakeSensorValuesOfOdometer(result);

            if (someZeroSats)
            {
              // Om vi kommer in hit efter att someZeroSats varit true ska vi rensa gpsDataQueue utom sista entryt
              for (int i = gpsDataQueue.Count; i > 1; i--)
              {
                gpsDataQueue.Dequeue();
              }
              someZeroSats = false;
            }
            else
            {
              // Rensa från datatkön de som vi använt just nu förutom de vi vill ha kvar för speed
              for (int i = 0; i < maxNrForOdo - maxNrForSpeed; i++)
              {
                gpsDataQueue.Dequeue();
              }
            }

            elapsedForMilliSecond = 0;
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
          //_logger.LogInformation("Arduino string: {ardStr}", ardResult);
          //_logger.LogInformation("SignalR gpsDataLst string: {result}", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
          //_logger.LogInformation("GPS antal: {okstr} ok: {okint} fail: {failed} queLen: {qlen} ", antalGps, okGps, failGps, gpsDataQueue.Count);
          //_logger.LogInformation("Mätning och SignalR över {loops} varv snittade : {time}ms  senaste: {lastElapsed}ms", loops, avgElapsed, stopwatch.ElapsedMilliseconds);
          loops = 0;
          elapsedTotal = 0;
        }

        int delay = 50 - (int)stopwatch.ElapsedMilliseconds < 0 ? 0 : 50 - (int)stopwatch.ElapsedMilliseconds;
        //Console.WriteLine($"delay: {delay}");
        await Task.Delay(delay, stoppingToken);
        elapsedForMilliSecond += stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
      }
      _arduinoSerialPort.Close();
      _garminSerialPort.Close();
    }

    private List<GpsDataDTO> ExtracNrOfOKentries(Queue<GpsDataDTO> gpsDataQueue, int maxNrOfEntries)
    {
      List<GpsDataDTO> speedQueList = [];
      var queueArray = gpsDataQueue.ToArray();

      // Iterate backwards through the queue to get most recent entries first
      for (int i = queueArray.Length - 1; i >= 0; i--)
      {
        var gpsData = queueArray[i];
        if (gpsData.fixValid && gpsData.SatelitesUsed >= 2)
        {
          speedQueList.Add(gpsData);
          if (speedQueList.Count >= maxNrOfEntries)
          {
            break;
          }
        }
      }
      return speedQueList;
    }
  }
}

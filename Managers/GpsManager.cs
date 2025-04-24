using SensorHubService.DTO;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace SensorHubService.Managers;

public static class GpsManager
{
  const double RADIUS = 6371;
  private static readonly int MIN_SATS_ACCEPTED = 2;


  // Convert degrees in double to Radians
  public static double Radians(double x)
  {
    return x * Math.PI / 180;
  }


  public static GpsDataDTO ParseNmeaSentence(string rawData)
  {
    GpsDataDTO wrkObj = new GpsDataDTO();

    try
    {
      if (!rawData.StartsWith("$GN") || rawData.IndexOf("*") < 0) return wrkObj;
      ReadOnlySpan<char> rawDataSpan = rawData.AsSpan();
      ReadOnlySpan<char> sentenceSpan;
      ReadOnlySpan<char> wrkStr;
      ReadOnlySpan<char> nmeaKind;
      string[] splitted;

      while (rawDataSpan.Length > 0)
      {

        int sentenceEnd = rawDataSpan.IndexOf("\r\n".AsSpan());
        if (sentenceEnd == -1) sentenceEnd = rawDataSpan.Length;
        sentenceSpan = rawDataSpan.Slice(0, sentenceEnd);
        rawDataSpan = sentenceEnd == rawDataSpan.Length ? string.Empty : rawDataSpan.Slice(sentenceEnd + 2);

        if (!CheckNmeaChecksum(sentenceSpan.ToString())) continue;

        wrkStr = sentenceSpan.Slice(1, sentenceSpan.IndexOf("*") - 1);
        nmeaKind = wrkStr.Slice(0, 5);
        splitted = wrkStr.ToString().Split(",");

        switch (nmeaKind.ToString())
        {
          case "GNGGA":
            // Just satelitesUsed is used now 
            wrkObj.UTCTime = ParseNmeaTimetoUtcDateTime(splitted[1]);
            wrkObj.SatelitesUsed = int.Parse(splitted[7]);
            return wrkObj;
            wrkObj.fixValid = wrkObj.SatelitesUsed >= MIN_SATS_ACCEPTED && !splitted[6].Equals("0");
            //if (!wrkObj.fixValid)
            //{
            //    wrkObj.SpeedKph = -1;
            //    return wrkObj;
            //}

            wrkObj.Latitude = 0.01 * (splitted[3] == "S" ? -1 : 1) * System.Convert.ToDouble(splitted[2], CultureInfo.InvariantCulture);
            wrkObj.LatIndicator = splitted[3];
            wrkObj.Longitude = 0.01 * (splitted[5] == "W" ? -1 : 1) * System.Convert.ToDouble(splitted[4], CultureInfo.InvariantCulture);
            wrkObj.LongIndicator = splitted[5];

            return wrkObj;
            break;

          case "GNVTG":
            // Not used now
            break;
            double tmpVtg = splitted[7].Length > 0 ? System.Convert.ToDouble(splitted[7], CultureInfo.InvariantCulture) : -1;
            if (tmpVtg > 0) { wrkObj.SpeedKph = tmpVtg; }
            break;

          case "GNRMC":
            wrkObj.UTCTime = ParseNmeaTimetoUtcDateTime(splitted[1]);
            wrkObj.SatelitesUsed = -1; // Markerar att vi ska plocka från tidigare GNGGA
            wrkObj.fixValid = splitted[2].Equals("A");
            if (!wrkObj.fixValid) { return null; }

            wrkObj.Latitude = 0.01 * (splitted[4] == "S" ? -1 : 1) * System.Convert.ToDouble(splitted[3], CultureInfo.InvariantCulture);
            wrkObj.LatIndicator = splitted[3];
            wrkObj.Longitude = 0.01 * (splitted[6] == "W" ? -1 : 1) * System.Convert.ToDouble(splitted[5], CultureInfo.InvariantCulture);
            wrkObj.LongIndicator = splitted[5];

            double tmpRmc = splitted[7].Length > 0 ? System.Convert.ToDouble(splitted[7], CultureInfo.InvariantCulture) : -1;
            // knots to kph
            wrkObj.SpeedKph = tmpRmc > 1 ? tmpRmc * 1.852 : 0;

            return wrkObj;

          default:
            break;
        }
      }
    }
    catch
    {
    }
    return null;
  }

  public static bool CheckNmeaChecksum(string sentence)
  {
    if (sentence.IndexOf("*") < 0) return false;
    string[] splitted = sentence.Split("*");
    string checksum = splitted[1];
    byte checkSum = 0;
    // Substring(1) för att skippa inledande $
    foreach (char c in splitted[0].Substring(1))
    {
      checkSum ^= (byte)c;
    }
    return checkSum.ToString("X2") == checksum;
  }


  private static DateTime ParseNmeaTimetoUtcDateTime(string input)
  {
    var hh = input.AsSpan(0, 2);
    var mm = input.AsSpan(2, 2);
    var ss = input.AsSpan(4, 2);
    var ff = input.AsSpan(7, 1);
    ReadOnlySpan<char> timeStr = [.. hh, .. ":", .. mm, .. ":", .. ss, .. ".", .. ff];
    return DateTime.Parse(timeStr);
  }

  public static double CalculateDistance(List<GpsDataDTO> gpsQue)
  {
    //if (gpsQue.Count < 10) return 0;
    GpsDataDTO[] gpsArr = gpsQue.Where(s => s.fixValid).ToArray();
    GpsDataDTO pos1 = gpsQue.ElementAt(0);
    GpsDataDTO pos2 = gpsQue.Last();

    double dlon = Radians(pos2.Longitude - pos1.Longitude);
    double dlat = Radians(pos2.Latitude - pos1.Latitude);

    double a = (Math.Sin(dlat / 2) * Math.Sin(dlat / 2)) + Math.Cos(Radians(pos1.Latitude)) * Math.Cos(Radians(pos2.Latitude)) * (Math.Sin(dlon / 2) * Math.Sin(dlon / 2));
    double angle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    return angle * RADIUS;
  }

  public static double CalculateDistanceCoPilot(List<GpsDataDTO> gpsQue)
  {
    //if (gpsQue.Count < 10) return 0;
    GpsDataDTO[] gpsArr = gpsQue.Where(s => s.fixValid).ToArray();
    GpsDataDTO pos1 = gpsQue.ElementAt(0);
    GpsDataDTO pos2 = gpsQue.Last();

    // calculate distance in km between two points using decimal notation do not include the earth radius in the calculation
    double dlon = Radians(pos2.Longitude - pos1.Longitude);
    double dlat = Radians(pos2.Latitude - pos1.Latitude);
    double a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) + Math.Cos(Radians(pos1.Latitude)) * Math.Cos(Radians(pos2.Latitude)) * Math.Sin(dlon / 2) * Math.Sin(dlon / 2);
    double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    double distance = c * RADIUS;
    return distance;
  }

  public static double CalculateDistanceAvg(List<GpsDataDTO> gpsQue)
  {
    GpsDataDTO[] gpsArr = gpsQue.Where(s => s.fixValid).ToArray();
    double distance = 0;
    int count = 0;

    GpsDataDTO pos1 = gpsArr.First();
    GpsDataDTO pos2 = gpsArr.Last();

    // Haversine formula implementation
    double dlon = Radians(pos2.Longitude - pos1.Longitude);
    double dlat = Radians(pos2.Latitude - pos1.Latitude);
    double lat1 = Radians(pos1.Latitude);
    double lat2 = Radians(pos2.Latitude);

    // a = sin²(Δlat/2) + cos(lat1) * cos(lat2) * sin²(Δlong/2)
    double a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
              Math.Cos(lat1) * Math.Cos(lat2) *
              Math.Sin(dlon / 2) * Math.Sin(dlon / 2);

    // c = 2 * atan2(√a, √(1−a))
    double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

    // Distance = R * c where R is earth's radius (mean radius = 6371km)
    double segmentDistance = RADIUS * c;

    return segmentDistance > 0.001 ? segmentDistance : 0;
  }

  public static double CalculateSpeed(List<GpsDataDTO> gpsQue)
  {
    //if (gpsQue.Count < 10) return 0;
    GpsDataDTO[] gpsArr = gpsQue.ToArray();
    // average speed between the first and last element in the queue using property SpeedKph
    double speed = 0;
    int count = 0;
    for (int i = 0; i < gpsArr.Length - 1; i++)
    {
      if (gpsArr[i].SpeedKph > 1)
      {
        speed += gpsArr[i].SpeedKph;
        count++;
      }
    }
    double avgSpeed = count > 0 ? speed / count : 0;
    avgSpeed = Math.Round(avgSpeed, 3);
    return avgSpeed;
  }

}

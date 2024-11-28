using SensorHubService.DTO;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace SensorHubService.Managers;

public static class GpsManager
{
    const double RADIUS = 6371;


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
                rawDataSpan = rawDataSpan.Slice(sentenceEnd + 2);

                //if (!CheckNmeaChecksum(sentenceSpan.ToString())) continue;

                wrkStr = sentenceSpan.Slice(1, sentenceSpan.IndexOf("*") - 1);
                nmeaKind = wrkStr.Slice(0, 5);
                splitted = wrkStr.ToString().Split(",");

                switch (nmeaKind.ToString())
                {
                    case "GNGGA":
                        wrkObj.UTCTime = ParseNmeaTimetoUtcDateTime(splitted[1]);
                        wrkObj.SatelitesUsed = int.Parse(splitted[7]);
                        wrkObj.fixValid = wrkObj.SatelitesUsed > 2 && !splitted[6].Equals("0");
                        if (!wrkObj.fixValid)
                        {
                            wrkObj.SpeedKph = -1;
                            return wrkObj;
                        }
                        else if (wrkObj.fixValid && wrkObj.Latitude != 0 && wrkObj.Longitude != 0)
                        {
                            //Console.Write("f");
                            //Console.WriteLine($"Not valid  sats: {wrkObj.SatelitesUsed} spltstr: {splitted[6]} ");
                        }
                        wrkObj.Latitude = 0.01 * (splitted[3] == "S" ? -1 : 1) * System.Convert.ToDouble(splitted[2], CultureInfo.InvariantCulture);
                        wrkObj.LatIndicator = splitted[3];
                        wrkObj.Longitude = 0.01 * (splitted[5] == "W" ? -1 : 1) * System.Convert.ToDouble(splitted[4], CultureInfo.InvariantCulture);
                        wrkObj.LongIndicator = splitted[5];
                        break;

                    case "GNVTG":
                        wrkObj.SpeedKph = System.Convert.ToDouble(splitted[7], CultureInfo.InvariantCulture);
                        break;

                    default:
                        break;
                }
            }
        }
        catch
        {
        }
        return wrkObj;
    }

    // function to check nmea checksum in the sentence
    public static bool CheckNmeaChecksum(string sentence)
    {
        if (sentence.IndexOf("*") < 0) return false;
        string[] splitted = sentence.Split("*");
        string checksum = splitted[1];
        byte checkSum = 0;
        foreach (char c in splitted[0])
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

    public static double CalculateDistance(Queue<GpsDataDTO> gpsQue)
    {
        if (gpsQue.Count < 10) return 0;
        GpsDataDTO[] gpsArr = gpsQue.ToArray();
        GpsDataDTO pos1 = gpsQue.ElementAt(0);
        GpsDataDTO pos2 = gpsQue.Last();

        double dlon = Radians(pos2.Longitude - pos1.Longitude);
        double dlat = Radians(pos2.Latitude - pos1.Latitude);

        double a = (Math.Sin(dlat / 2) * Math.Sin(dlat / 2)) + Math.Cos(Radians(pos1.Latitude)) * Math.Cos(Radians(pos2.Latitude)) * (Math.Sin(dlon / 2) * Math.Sin(dlon / 2));
        double angle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return angle * RADIUS;
    }

}

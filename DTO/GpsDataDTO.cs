using System;

namespace SensorHubService.DTO
{
    public class GpsDataDTO
    {
        public bool fixValid { get; set; } = false;
        public DateTime UTCTime { get; set; } = DateTime.MinValue;
        public Double Latitude { get; set; } = 0;
        public string LatIndicator { get; set; } = "N"; // North/South 
        public Double Longitude { get; set; } = 0;
        public string LongIndicator { get; set; } = "E"; // East/West 
        public int SatelitesUsed { get; set; } = 0;
        public double SpeedKph { get; set; } = 0;

        private double ConvertDegreeAngleToDouble(double coord, string indicator)
        {
            var multiplier = (indicator.Contains("W") || indicator.Contains("S") ? -1 : 1);
            coord *= 100;
            var _deg = (double)Math.Floor(coord / 100);
            var minutes = Math.Round(coord % 100);
            var seconds = (coord % 100) - Math.Round(coord % 100);
            var result = _deg + (minutes / 60) + (seconds / 3600);
            return result * multiplier;
        }

        public double ToDecLat
        {
            get { 
               double lat = ConvertDegreeAngleToDouble(Latitude, LatIndicator);
               return lat;
            }
        }

        public double ToDecLong
        {
            get
            {
                double lon = ConvertDegreeAngleToDouble(Longitude, LongIndicator);
                return lon;
            }
        }

        public bool complete = false; // Used at parsing nmea sentences 
    }
}

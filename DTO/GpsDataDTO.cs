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
        public bool complete = false; // Used at parsing nmea sentences 
    }
}

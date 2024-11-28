using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspSensorService.DTO
{
    public class ListVaulesDTO
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<SensorVauleDTO> SensorValues { get; set; } = []; 
    }
}

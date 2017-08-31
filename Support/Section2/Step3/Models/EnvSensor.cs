using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IoTWeb.Models
{
    public class EnvSensor
    {
        public string DeviceId { get; set; }
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
    }
}

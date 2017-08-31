using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DevMgmtWeb.Models
{
    public class DeviceModel : IModeledDevice
    {
        public string Id { get; set; }
        public string TelemetryCycle { get; set; }
        public string Reported { get; set; }

        public string DesiredPropertiesToJson()
        {
            var props = new
            {
                dmConfig = new
                {
                    TelemetryCycle = TelemetryCycle,
                }
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(props);
        }

        public void SetDesiredProperties(string json)
        {
            var desiredProps = Newtonsoft.Json.JsonConvert.DeserializeObject(json) as Newtonsoft.Json.Linq.JObject;
            var dmConfig = desiredProps.Value<Newtonsoft.Json.Linq.JObject>("dmConfig");
            if (dmConfig != null)
            {
                TelemetryCycle = dmConfig.Value<string>("TelemetryCycle");
            }
        }
    }
}
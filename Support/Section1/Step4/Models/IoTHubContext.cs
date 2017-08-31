using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace DevMgmtWeb.Models
{
    public class IoTHubContext<TDevice> : IDisposable where TDevice : class, IModeledDevice, new()
    {

        protected Microsoft.Azure.Devices.RegistryManager registryManager;
        public IoTHubContext(string connectionString)
        {
            registryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(connectionString);
            registryManager.OpenAsync().Wait();
        }

        //        public IoTDeviceEntry<TDevice> Entry<TDevice>(TDevice device) where TDevice : IModeledDevice
        public async Task<IoTDeviceEntry<TDevice>> EntryAsync(TDevice device)
        {
            var entry = await modelDevices.FindEntityAsync(device);
            
                 //       var entry = entryTask.GetResult();

            return entry;
        }

        public async Task Add(TDevice device)
        {
            await modelDevices.AddAsync(device);
        }

        public async Task Update(TDevice device)
        {
            await modelDevices.AddAsync(device);
        }

        public async Task<int> SaveChangesAsync()
        {
            int modified = 0;
            string twinJson = "";
            foreach (var modifiedDevice in modelDevices.modifiedDevices)
            {
                if (modifiedDevice.State == EntityState.Added || modifiedDevice.State == EntityState.Modified)
                {
                    twinJson = "{\"properties\":{\"desired\":" + modifiedDevice.Device.DesiredPropertiesToJson() + "}}";
                    var test = Newtonsoft.Json.JsonConvert.DeserializeObject(twinJson);
                }
                switch (modifiedDevice.State)
                {
                    case EntityState.Added:
                        var newDevice = new Microsoft.Azure.Devices.Device(modifiedDevice.Device.Id);
                        newDevice = await registryManager.AddDeviceAsync(newDevice);
                        var twin = await registryManager.GetTwinAsync(newDevice.Id);
                        await registryManager.UpdateTwinAsync(newDevice.Id, twinJson, twin.ETag);
                        modified++;
                        break;
                    case EntityState.Modified:
                        var managedDevice = await registryManager.GetDeviceAsync(modifiedDevice.Device.Id);
                        var manageDeviceTwin = await registryManager.GetTwinAsync(modifiedDevice.Device.Id);

                        string etag = manageDeviceTwin.ETag;
                        await registryManager.UpdateTwinAsync(modifiedDevice.Device.Id, twinJson, etag);
                        modified++;
                        break;
                    case EntityState.Deleted:
                        var registered = await registryManager.GetDeviceAsync(modifiedDevice.Device.Id);
                        await registryManager.RemoveDeviceAsync(registered);
                        modified++;
                        break;
                }
            }
            modelDevices.modifiedDevices.Clear();

            return modified;
        }

        public void Dispose()
        {
            registryManager.CloseAsync();
        }

        internal IoTHubDeviceSet<TDevice> modelDevices { get; set; }
    }

    public interface IModeledDevice
    {
        string Id { get; set; }
        string Reported { get; set; }

        string DesiredPropertiesToJson();
        void SetDesiredProperties(string json);
    }

    public class IoTDeviceEntry<TDevice> where TDevice : class, IModeledDevice
    {
        public TDevice Device { get; set; }
        public EntityState State { get; set; }
    }

    public class IoTHubDeviceSet<TDevice> where TDevice : class, IModeledDevice, new()
    {
        private Microsoft.Azure.Devices.RegistryManager registryManager;
        private List<IoTDeviceEntry<TDevice>> devices = null;
        internal List<IoTDeviceEntry<TDevice>> modifiedDevices { get; set; }

        public IoTHubDeviceSet(Microsoft.Azure.Devices.RegistryManager rm)
        {
            registryManager = rm;
            devices = new List<IoTDeviceEntry<TDevice>>();
            modifiedDevices = new List<IoTDeviceEntry<TDevice>>();
        }
        public async Task<IEnumerable<TDevice>> ToListAsync()
        {
            List<TDevice> resultSet = await LoadIoTHubDevicesAsync();
            return resultSet;
        }

        private async Task<List<TDevice>> LoadIoTHubDevicesAsync()
        {
            List<TDevice> resultSet = new List<TDevice>();

            devices.Clear();
            string devIdQuery = "SELECT deviceId FROM devices";
            var query = registryManager.CreateQuery(devIdQuery);
            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsJsonAsync();

                foreach (var twin in page)
                {
                    var jdevice = Newtonsoft.Json.JsonConvert.DeserializeObject(twin) as Newtonsoft.Json.Linq.JObject;
                    var deviceId = jdevice.Value<string>("DeviceId");
                    var registedDevice = await registryManager.GetDeviceAsync(deviceId);
                    var registedDeviceTwin = await registryManager.GetTwinAsync(deviceId);
                    var device = new TDevice()
                    {
                        Id = registedDevice.Id
                    };
                    device.SetDesiredProperties(registedDeviceTwin.Properties.Desired.ToJson());

                    // Set Reported Properties.
                    var rpJSON = (Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(registedDeviceTwin.Properties.Reported.ToJson(Newtonsoft.Json.Formatting.None));
                    rpJSON.Remove("$metadata");
                    rpJSON.Remove("$version");
                    device.Reported = Newtonsoft.Json.JsonConvert.SerializeObject(rpJSON);

                    devices.Add(new IoTDeviceEntry<TDevice>()
                    {
                        Device = device,
                        State = EntityState.Unchanged
                    });
                    resultSet.Add(device);
                }
            }

            return resultSet;
        }

        public async Task<IoTDeviceEntry<TDevice>> FindEntityAsync(TDevice device)
        {
            IoTDeviceEntry<TDevice> target = null;
            await LoadIoTHubDevicesAsync();
            var candidate = from d in devices where d.Device.Id == device.Id select d;
            if (candidate.Count() > 0)
            {
                target = candidate.First();
                target.Device = device;
                modifiedDevices.Add(target);
            }
            return target;
        }

        public async Task<TDevice> FindAsync(string Id)
        {
            TDevice result = null;
            await LoadIoTHubDevicesAsync();
            var candidate = from d in devices where d.Device.Id == Id select d;
            if (candidate.Count() > 0)
            {
                result = candidate.First().Device;
            }
            return result;
        }

        public async Task<TDevice> AddAsync(TDevice device)
        {
            await LoadIoTHubDevicesAsync();
            var initem = await FindAsync(device.Id);
            if (initem == null)
            {
                var entryDevice = new IoTDeviceEntry<TDevice>()
                {
                    Device = device,
                    State = EntityState.Added
                };
                devices.Add(entryDevice);
                modifiedDevices.Add(entryDevice);
            }
            return device;
        }

        public void Remove(TDevice device)
        {
            //     await LoadIoTHubDevicesAsync();
            var candidate = from d in devices where d.Device.Id == device.Id select d;
            var target = candidate.First();
            if (target != null)
            {
                devices.Remove(target);
                target.State = EntityState.Deleted;
                modifiedDevices.Add(target);
            }
        }

        public bool Any(string id)
        {
            var candidate = from d in devices where d.Device.Id == id select d;
            return candidate.First() != null;
        }

    }

}
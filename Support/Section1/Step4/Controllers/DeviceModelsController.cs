using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using DevMgmtWeb.Models;

namespace DevMgmtWeb.Controllers
{
    public class DeviceModelsIoTHubContext : IoTHubContext<DeviceModel>
    {
        public DeviceModelsIoTHubContext(string conn) : base(connectionString: conn)
        {
            DeviceModels = new IoTHubDeviceSet<DeviceModel>(registryManager);
            modelDevices = DeviceModels;
        }
        public IoTHubDeviceSet<DeviceModel> DeviceModels { get; set; }

    }

    public class DeviceModelsController : Controller
    {
        private DeviceModelsIoTHubContext db =
            new DeviceModelsIoTHubContext(System.Configuration.ConfigurationManager.AppSettings["IoTHubConnectionString"]);

        // GET: DeviceModels
        public async Task<ActionResult> Index()
        {
            return View(await db.DeviceModels.ToListAsync());
        }

        // GET: DeviceModels/Details/5
        public async Task<ActionResult> Details(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DeviceModel deviceModel = await db.DeviceModels.FindAsync(id);
            if (deviceModel == null)
            {
                return HttpNotFound();
            }
            return View(deviceModel);
        }

        // GET: DeviceModels/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: DeviceModels/Create
        // 過多ポスティング攻撃を防止するには、バインド先とする特定のプロパティを有効にしてください。
        // 詳細については、https://go.microsoft.com/fwlink/?LinkId=317598 を参照してください。
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Id,TelemetryCycle")] DeviceModel deviceModel)
        {
            if (ModelState.IsValid)
            {
                await db.DeviceModels.AddAsync(deviceModel);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            return View(deviceModel);
        }

        // GET: DeviceModels/Edit/5
        public async Task<ActionResult> Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DeviceModel deviceModel = await db.DeviceModels.FindAsync(id);
            if (deviceModel == null)
            {
                return HttpNotFound();
            }
            return View(deviceModel);
        }

        // POST: DeviceModels/Edit/5
        // 過多ポスティング攻撃を防止するには、バインド先とする特定のプロパティを有効にしてください。
        // 詳細については、https://go.microsoft.com/fwlink/?LinkId=317598 を参照してください。
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Id,TelemetryCycle")] DeviceModel deviceModel)
        {
            if (ModelState.IsValid)
            {
                (await db.EntryAsync(deviceModel)).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(deviceModel);
        }

        // GET: DeviceModels/Delete/5
        public async Task<ActionResult> Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DeviceModel deviceModel = await db.DeviceModels.FindAsync(id);
            if (deviceModel == null)
            {
                return HttpNotFound();
            }
            return View(deviceModel);
        }

        // POST: DeviceModels/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(string id)
        {
            DeviceModel deviceModel = await db.DeviceModels.FindAsync(id);
            db.DeviceModels.Remove(deviceModel);
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

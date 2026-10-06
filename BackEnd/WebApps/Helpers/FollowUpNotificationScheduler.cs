using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using Autofac;
using HygieneAudit.Application.Services;

namespace WebApps.Helpers
{
    // Penjadwal pengingat follow up: tiap menit memeriksa apakah jam kirim (diatur Admin di UI) sudah tiba,
    // lalu mengirim email sekali per hari. Berjalan di dalam proses aplikasi (IIS), jadi pool aplikasi harus
    // tetap hidup (Idle Timeout = 0 / AlwaysRunning) agar pengiriman tepat waktu.
    public sealed class FollowUpNotificationScheduler : IRegisteredObject
    {
        private static FollowUpNotificationScheduler _instance;
        private readonly Timer _timer;
        private int _running;
        private volatile bool _stopping;

        private FollowUpNotificationScheduler()
        {
            _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
        }

        public static void Start()
        {
            if (_instance != null) return;
            _instance = new FollowUpNotificationScheduler();
            HostingEnvironment.RegisterObject(_instance);
        }

        private void Tick()
        {
            if (_stopping) return;
            // Cegah tick tumpang tindih bila pengiriman sebelumnya masih berjalan.
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
            try
            {
                using (var scope = AutofacConfig.Container.BeginLifetimeScope())
                {
                    var service = scope.Resolve<INotificationService>();
                    service.RunScheduledAsync(DateTime.Now).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("FollowUpNotificationScheduler: " + ex);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        public void Stop(bool immediate)
        {
            _stopping = true;
            _timer.Dispose();
            HostingEnvironment.UnregisterObject(this);
        }
    }
}

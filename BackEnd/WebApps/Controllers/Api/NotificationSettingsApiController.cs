using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;

namespace WebApps.Controllers.Api
{
    // Pengaturan notifikasi email follow up — hanya Admin / SuperAdmin.
    [RoutePrefix("api/settings/notifications")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class NotificationSettingsApiController : ApiController
    {
        private readonly INotificationService _service;

        public NotificationSettingsApiController(INotificationService service) => _service = service;

        [HttpGet, Route("")]
        public async Task<IHttpActionResult> Get() => Ok(await _service.GetSettingsAsync());

        [HttpPut, Route("")]
        public async Task<IHttpActionResult> Save([FromBody] SaveNotificationSettingsRequest req)
            => Ok(await _service.SaveSettingsAsync(req));

        [HttpPost, Route("test")]
        public async Task<IHttpActionResult> Test([FromBody] SendTestEmailRequest req)
        {
            await _service.SendTestEmailAsync(req?.To);
            return Ok(new { message = "Email uji terkirim ke " + req.To.Trim() + "." });
        }

        // Kirim sekarang (di luar jadwal) ke semua penerima untuk temuan yang sudah jatuh tempo.
        [HttpPost, Route("run")]
        public async Task<IHttpActionResult> Run() => Ok(await _service.RunNowAsync(DateTime.Now));
    }
}

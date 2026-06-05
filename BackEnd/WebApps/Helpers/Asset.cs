using System.IO;
using System.Web;
using System.Web.Hosting;

namespace WebApps.Helpers
{
    /// <summary>
    /// Helper untuk cache-busting aset statis (JS/CSS).
    /// </summary>
    public static class Asset
    {
        /// <summary>
        /// Mengembalikan URL app-absolute untuk aset statis dengan stempel "?v="
        /// berdasarkan waktu modifikasi file. Setiap deploy mengubah stempel ini,
        /// sehingga browser SELALU mengambil file terbaru — mencegah JS/CSS basi
        /// dari cache. (Mismatch JS lama vs HTML baru itulah yang menyebabkan error
        /// binding Knockout seperti "savingDraft is not defined" → ko.applyBindings
        /// gagal dan seluruh halaman rusak.)
        /// </summary>
        public static string V(string virtualPath)
        {
            var url = VirtualPathUtility.ToAbsolute(virtualPath);
            try
            {
                var physical = HostingEnvironment.MapPath(virtualPath);
                if (physical != null && File.Exists(physical))
                    url += "?v=" + File.GetLastWriteTimeUtc(physical).Ticks;
            }
            catch { /* fallback ke URL tanpa versi */ }
            return url;
        }
    }
}

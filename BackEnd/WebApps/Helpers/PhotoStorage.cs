using System;
using System.Configuration;
using System.IO;
using System.Web.Hosting;

namespace WebApps.Helpers
{
    public static class PhotoStorage
    {
        /// <summary>
        /// Folder where uploaded photos are written. Deliberately OUTSIDE the web
        /// application directory: writing files inside the app root (even App_Data)
        /// can trip ASP.NET's FileChangesMonitor, which recycles the AppDomain and
        /// kills the debugger mid-session. Override with the "PhotoUploadPath"
        /// appSetting if you want a specific absolute location.
        /// </summary>
        public static string UploadsFolder
        {
            get
            {
                var configured = ConfigurationManager.AppSettings["PhotoUploadPath"];
                if (!string.IsNullOrWhiteSpace(configured))
                    return Environment.ExpandEnvironmentVariables(configured);

                // Default: %ProgramData%\HygieneAudit\Uploads — persistent, writable,
                // outside the monitored web root, survives re-deploys.
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "HygieneAudit", "Uploads");
            }
        }

        /// <summary>
        /// Previous storage location (inside the app). Still read from so photos
        /// saved before this change keep resolving. No longer written to.
        /// </summary>
        public static string LegacyAppDataFolder =>
            HostingEnvironment.MapPath("~/App_Data/Uploads");

        public static string SaveFromDataUrl(string dataUrl) =>
            SaveFromDataUrl(dataUrl, UploadsFolder);

        public static string SaveFromDataUrl(string dataUrl, string uploadsPath)
        {
            if (string.IsNullOrEmpty(uploadsPath))
                throw new InvalidOperationException("Uploads path could not be resolved.");

            // Safety net: ensure the folder exists right before writing.
            Directory.CreateDirectory(uploadsPath);

            var base64 = dataUrl;
            var comma  = dataUrl.IndexOf(',');
            if (dataUrl.StartsWith("data:") && comma > 0)
                base64 = dataUrl.Substring(comma + 1);
            var filename = Guid.NewGuid().ToString("N") + ".jpg";
            File.WriteAllBytes(Path.Combine(uploadsPath, filename),
                               Convert.FromBase64String(base64));
            return filename;
        }

        public static bool IsFileName(string v) =>
            !string.IsNullOrWhiteSpace(v) && !v.StartsWith("data:") && !v.StartsWith("/");

        /// <summary>
        /// Resolves a stored filename to an existing path, checking the current
        /// uploads folder first, then the legacy App_Data location. Returns null
        /// if the file is not found in either.
        /// </summary>
        public static string ResolveExistingPath(string fileName)
        {
            var safeName = Path.GetFileName(fileName); // guard against path traversal
            var primary = Path.Combine(UploadsFolder, safeName);
            if (File.Exists(primary)) return primary;

            var legacyDir = LegacyAppDataFolder;
            if (!string.IsNullOrEmpty(legacyDir))
            {
                var legacy = Path.Combine(legacyDir, safeName);
                if (File.Exists(legacy)) return legacy;
            }
            return null;
        }
    }
}

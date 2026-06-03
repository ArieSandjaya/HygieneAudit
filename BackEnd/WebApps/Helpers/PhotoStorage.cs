using System;
using System.IO;
using System.Web.Hosting;

namespace WebApps.Helpers
{
    public static class PhotoStorage
    {
        public static string UploadsFolder =>
            HostingEnvironment.MapPath("~/Uploads");

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
    }
}

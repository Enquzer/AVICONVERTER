using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace AviConverter
{
    internal static class SetupFfmpeg
    {
        public const string Url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        public static void Run(Action<string> log)
        {
            string work = Path.Combine(Path.GetTempPath(), "avi-ffmpeg-" + Guid.NewGuid().ToString("N"));
            string zip = Path.Combine(work, "ffmpeg.zip");
            string bin = Path.Combine(AppInfo.BaseDir, "bin");

            Directory.CreateDirectory(work);
            Directory.CreateDirectory(bin);

            try
            {
                log("Downloading FFmpeg (about 110 MB)...");
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "FashionFlowAVIConverter/" + AppInfo.Version);
                    client.DownloadFile(Url, zip);
                }

                log("Extracting...");
                ZipFile.ExtractToDirectory(zip, work);

                string found = null;
                foreach (string file in Directory.GetFiles(work, "ffmpeg.exe", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "bin",
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        found = file;
                        break;
                    }
                }

                if (found == null)
                {
                    throw new InvalidOperationException("ffmpeg.exe was not found inside the downloaded archive.");
                }

                string sourceDir = Path.GetDirectoryName(found);
                File.Copy(found, Path.Combine(bin, "ffmpeg.exe"), true);

                string probe = Path.Combine(sourceDir, "ffprobe.exe");
                if (File.Exists(probe))
                {
                    File.Copy(probe, Path.Combine(bin, "ffprobe.exe"), true);
                }

                log("Installed into " + bin);
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { }
            }
        }
    }
}

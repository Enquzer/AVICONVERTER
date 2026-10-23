using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AviConverter
{
    internal sealed class MediaInfo
    {
        public string Path = "";
        public double Duration;
        public long SizeBytes;
        public int Width;
        public int Height;
        public double Fps;
        public string VideoCodec = "";
        public string PixFmt = "";
        public string FieldOrder = "";
        public bool HasVideo;
        public bool HasAudio;
        public string AudioCodec = "";
        public int AudioChannels;

        public bool IsInterlaced
        {
            get
            {
                string f = (FieldOrder ?? "").Trim().ToLowerInvariant();
                return f == "tt" || f == "bb" || f == "tb" || f == "bt";
            }
        }

        public static MediaInfo Read(string path)
        {
            string ffprobe = AppInfo.FfprobePath;
            if (string.IsNullOrEmpty(ffprobe))
            {
                throw new InvalidOperationException("ffprobe.exe was not found. Place it in the bin folder next to the app.");
            }

            MediaInfo info = new MediaInfo();
            info.Path = path;

            // First non-attached video stream.
            Dictionary<string, string> video = Probe(ffprobe,
                "-v error -select_streams V:0 " +
                "-show_entries stream=width,height,avg_frame_rate,r_frame_rate,field_order,pix_fmt,codec_name,duration " +
                "-of flat -- " + CommandLine.Quote(path));

            const string vp = "streams.stream.0.";
            if (video.ContainsKey(vp + "width"))
            {
                info.HasVideo = true;
                info.Width = ParseInt(video, vp + "width");
                info.Height = ParseInt(video, vp + "height");
                info.Fps = ParseRate(video, vp + "avg_frame_rate");
                if (info.Fps <= 0) { info.Fps = ParseRate(video, vp + "r_frame_rate"); }
                info.VideoCodec = Get(video, vp + "codec_name");
                info.PixFmt = Get(video, vp + "pix_fmt");
                info.FieldOrder = Get(video, vp + "field_order");
                double d = ParseDouble(video, vp + "duration");
                if (d > 0) { info.Duration = d; }
            }

            if (!info.HasVideo)
            {
                throw new InvalidOperationException("No video stream found.");
            }

            // Container duration/size and the first audio stream.
            Dictionary<string, string> format = Probe(ffprobe,
                "-v error -show_entries format=duration,size " +
                "-show_entries stream=codec_type,codec_name,channels -of flat -- " + CommandLine.Quote(path));

            double fd = ParseDouble(format, "format.duration");
            if (fd > 0) { info.Duration = fd; }
            info.SizeBytes = (long)ParseDouble(format, "format.size");

            for (int i = 0; i < 64; i++)
            {
                string prefix = "streams.stream." + i.ToString(CultureInfo.InvariantCulture) + ".";
                if (!format.ContainsKey(prefix + "codec_type")) { break; }
                if (Get(format, prefix + "codec_type") == "audio")
                {
                    info.HasAudio = true;
                    info.AudioCodec = Get(format, prefix + "codec_name");
                    info.AudioChannels = ParseInt(format, prefix + "channels");
                    break;
                }
            }

            return info;
        }

        /// <summary>Reads only the duration (used to sanity check the result).</summary>
        public static double ReadDuration(string path)
        {
            string ffprobe = AppInfo.FfprobePath;
            if (string.IsNullOrEmpty(ffprobe)) { return 0; }

            Dictionary<string, string> data = Probe(ffprobe,
                "-v error -show_entries format=duration -of flat -- " + CommandLine.Quote(path));
            return ParseDouble(data, "format.duration");
        }

        private static Dictionary<string, string> Probe(string exe, string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            string errorText = "";
            string output;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) { errorText += e.Data + "\n"; }
                };

                p.Start();
                p.BeginErrorReadLine();
                output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode != 0 && output.Trim().Length == 0)
                {
                    string detail = FirstLine(errorText);
                    throw new InvalidOperationException(
                        "ffprobe could not read the file." + (detail.Length > 0 ? " " + detail : ""));
                }
            }

            return ParseFlat(output);
        }

        private static Dictionary<string, string> ParseFlat(string text)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) { return result; }

            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in lines)
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) { continue; }

                string key = raw.Substring(0, eq).Trim();
                string value = raw.Substring(eq + 1).Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }
                result[key] = value;
            }
            return result;
        }

        private static string Get(Dictionary<string, string> data, string key)
        {
            string value;
            return data.TryGetValue(key, out value) ? value : "";
        }

        private static int ParseInt(Dictionary<string, string> data, string key)
        {
            int value;
            return int.TryParse(Get(data, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static double ParseDouble(Dictionary<string, string> data, string key)
        {
            double value;
            return double.TryParse(Get(data, key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static double ParseRate(Dictionary<string, string> data, string key)
        {
            string text = Get(data, key);
            if (string.IsNullOrEmpty(text)) { return 0; }

            int slash = text.IndexOf('/');
            if (slash > 0)
            {
                double num;
                double den;
                if (double.TryParse(text.Substring(0, slash), NumberStyles.Float, CultureInfo.InvariantCulture, out num) &&
                    double.TryParse(text.Substring(slash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out den) &&
                    den != 0)
                {
                    return num / den;
                }
            }

            double plain;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out plain) ? plain : 0;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) { return ""; }
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[0].Trim() : "";
        }
    }
}

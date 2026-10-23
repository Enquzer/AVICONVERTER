using System;
using System.Collections.Generic;
using System.Globalization;

namespace AviConverter
{
    internal sealed class Preset
    {
        public readonly string Key;
        public readonly string Label;
        public readonly string Description;
        public readonly int Width;
        public readonly int Height;
        public readonly string VCodec;
        public readonly string VTag;
        public readonly int VBits;
        public readonly int Quality;
        public readonly string PixFmt;
        public readonly int AudioKbps;
        public readonly int FpsCap;

        public Preset(string key, string label, string description, int width, int height,
                      string vcodec, string vtag, int vbits, int quality, string pixfmt,
                      int audioKbps, int fpsCap)
        {
            Key = key;
            Label = label;
            Description = description;
            Width = width;
            Height = height;
            VCodec = vcodec;
            VTag = vtag;
            VBits = vbits;
            Quality = quality;
            PixFmt = pixfmt;
            AudioKbps = audioKbps;
            FpsCap = fpsCap;
        }
    }

    internal static class Presets
    {
        private static readonly Preset[] Items = new Preset[]
        {
            new Preset("safe", "Safe 480p - Xvid/MPEG-4 (recommended)",
                "854x480 widescreen, about 2.2 Mbit/s. Plays on virtually every AVI projector.",
                854, 480, "mpeg4", "XVID", 2200, 0, "yuv420p", 128, 30),

            new Preset("hd", "HD 720p - Xvid/MPEG-4",
                "1280x720, about 4.5 Mbit/s. For projectors that advertise 720p/1080p support.",
                1280, 720, "mpeg4", "XVID", 4500, 0, "yuv420p", 128, 30),

            new Preset("4x3", "4:3 720x540 - Xvid/MPEG-4",
                "Square-pixel 4:3 frame for old 4:3 projectors and TVs, about 3 Mbit/s.",
                720, 540, "mpeg4", "XVID", 3000, 0, "yuv420p", 128, 30),

            new Preset("mjpeg", "Fallback 640x480 - MJPEG",
                "Motion-JPEG in AVI. Bigger files, but the codec that the fussiest players accept.",
                640, 480, "mjpeg", "MJPG", 0, 6, "yuvj420p", 128, 25),

            new Preset("small", "Small file 640x360 - Xvid/MPEG-4",
                "640x360, about 0.9 Mbit/s. Good for USB sticks with little free space.",
                640, 360, "mpeg4", "XVID", 900, 0, "yuv420p", 96, 30)
        };

        public static Preset[] List
        {
            get { return Items; }
        }

        public static Preset Get(string name)
        {
            if (string.IsNullOrEmpty(name)) { return Items[0]; }

            string n = name.Trim().ToLowerInvariant();
            foreach (Preset p in Items)
            {
                if (p.Key == n) { return p; }
            }

            if (n.StartsWith("720") || n.StartsWith("hd")) { return ByKey("hd"); }
            if (n.StartsWith("480") || n.StartsWith("safe") || n.StartsWith("default")) { return ByKey("safe"); }
            if (n.StartsWith("4x3") || n.StartsWith("four")) { return ByKey("4x3"); }
            if (n.StartsWith("mjp") || n.StartsWith("fallback")) { return ByKey("mjpeg"); }
            if (n.StartsWith("small") || n.StartsWith("tiny")) { return ByKey("small"); }

            throw new ArgumentException("Unknown preset '" + name + "'. Use: " + Keys());
        }

        public static string Keys()
        {
            List<string> keys = new List<string>();
            foreach (Preset p in Items) { keys.Add(p.Key); }
            return string.Join(", ", keys.ToArray());
        }

        private static Preset ByKey(string key)
        {
            foreach (Preset p in Items)
            {
                if (p.Key == key) { return p; }
            }
            return Items[0];
        }

        public static string FitName(int index)
        {
            switch (index)
            {
                case 1: return "crop";
                case 2: return "stretch";
                default: return "pad";
            }
        }

        public static int FitIndex(string fit)
        {
            if (string.Equals(fit, "crop", StringComparison.OrdinalIgnoreCase)) { return 1; }
            if (string.Equals(fit, "stretch", StringComparison.OrdinalIgnoreCase)) { return 2; }
            return 0;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
            {
                return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("N2", CultureInfo.CurrentCulture) + " GB";
            }
            if (bytes >= 1024L * 1024L)
            {
                return (bytes / (1024.0 * 1024.0)).ToString("N1", CultureInfo.CurrentCulture) + " MB";
            }
            return (bytes / 1024.0).ToString("N0", CultureInfo.CurrentCulture) + " KB";
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0) { return "--:--"; }
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", ts.Minutes, ts.Seconds);
        }
    }
}

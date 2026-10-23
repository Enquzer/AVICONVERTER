using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace AviConverter
{
    internal sealed class ConvertOptions
    {
        public string Preset = "safe";
        public string Fit = "pad";
        public int Width;
        public int Height;
        public double Fps;
        public int VideoKbps;
        public int AudioKbps;
        public string AudioFormat = "mp3";
        public bool NoAudio;
        public bool Deinterlace;
        public bool Force;
        public bool DryRun;
    }

    internal sealed class ProgressInfo
    {
        public int Percent;
        public string Speed = "";
        public double OutSeconds;
    }

    internal sealed class ConvertResult
    {
        public bool Success;
        public bool Skipped;
        public bool Cancelled;
        public bool DryRun;
        public string Input = "";
        public string Output = "";
        public string Message = "";
        public string Warning = "";
        public double Seconds;
        public long SizeBytes;
        public MediaInfo Info;
        public MediaInfo OutputInfo;
    }

    internal static class Converter
    {
        public static string OutputPath(string inputFile, string outputDirectory, string suffix)
        {
            FileInfo fi = new FileInfo(inputFile);
            string name = Path.GetFileNameWithoutExtension(fi.Name);
            string dir = string.IsNullOrEmpty(outputDirectory) ? fi.DirectoryName : outputDirectory;
            string candidate = Path.Combine(dir, name + (suffix ?? "") + ".avi");

            if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(fi.FullName),
                              StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.Combine(dir, name + ".converted.avi");
            }
            return candidate;
        }

        // Keeps two sources with the same name from overwriting each other in one run.
        public static string UniqueOutputPath(string candidate, HashSet<string> used)
        {
            string path = candidate;
            if (!used.Contains(path))
            {
                used.Add(path);
                return path;
            }

            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            for (int i = 2; i < 10000; i++)
            {
                string test = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!used.Contains(test))
                {
                    used.Add(test);
                    return test;
                }
            }

            used.Add(path);
            return path;
        }

        public static string FilterChain(int width, int height, string fit, bool deinterlace)
        {
            string prefix = deinterlace ? "yadif=mode=send_frame," : "";
            switch ((fit ?? "pad").ToLowerInvariant())
            {
                case "crop":
                    return prefix + "scale=" + width + ":" + height +
                        ":force_original_aspect_ratio=increase:force_divisible_by=2:flags=lanczos,crop=" +
                        width + ":" + height + ",setsar=1";
                case "stretch":
                    return prefix + "scale=" + width + ":" + height + ":flags=lanczos,setsar=1";
                default:
                    return prefix + "scale=" + width + ":" + height +
                        ":force_original_aspect_ratio=decrease:force_divisible_by=2:flags=lanczos,pad=" +
                        width + ":" + height + ":(ow-iw)/2:(oh-ih)/2:color=black,setsar=1";
            }
        }

        public static List<string> BuildArgs(MediaInfo info, ConvertOptions opt, Preset preset,
                                             string outputFile, out double targetFps)
        {
            int width = opt.Width > 0 ? opt.Width : preset.Width;
            int height = opt.Height > 0 ? opt.Height : preset.Height;
            int audioKbps = opt.AudioKbps > 0 ? opt.AudioKbps : preset.AudioKbps;

            // yuv420p / mpeg4 require even dimensions.
            width = (width / 2) * 2;
            height = (height / 2) * 2;

            targetFps = opt.Fps;
            if (targetFps <= 0)
            {
                targetFps = info.Fps;
                if (targetFps <= 0) { targetFps = 25; }
                if (targetFps > preset.FpsCap) { targetFps = preset.FpsCap; }
                if (targetFps < 15) { targetFps = 15; }
            }

            List<string> a = new List<string>();
            a.Add("-hide_banner");
            a.Add("-nostdin");
            a.Add("-loglevel");
            a.Add("error");
            a.Add(opt.Force ? "-y" : "-n");
            a.Add("-progress");
            a.Add("pipe:1");
            a.Add("-stats_period");
            a.Add("0.5");
            a.Add("-i");
            a.Add(info.Path);
            a.Add("-map");
            a.Add("0:V:0");
            if (!opt.NoAudio)
            {
                a.Add("-map");
                a.Add("0:a:0?");
            }
            a.Add("-vf");
            a.Add(FilterChain(width, height, opt.Fit, opt.Deinterlace || info.IsInterlaced));
            a.Add("-r");
            a.Add(targetFps.ToString("0.###", CultureInfo.InvariantCulture));

            if (preset.VCodec == "mjpeg")
            {
                a.Add("-c:v");
                a.Add("mjpeg");
                a.Add("-vtag");
                a.Add("MJPG");
                a.Add("-q:v");
                a.Add(preset.Quality.ToString(CultureInfo.InvariantCulture));
                a.Add("-pix_fmt");
                a.Add("yuvj420p");
            }
            else
            {
                int bits = opt.VideoKbps > 0 ? opt.VideoKbps : preset.VBits;
                a.Add("-c:v");
                a.Add(preset.VCodec);
                a.Add("-vtag");
                a.Add(preset.VTag);
                a.Add("-b:v");
                a.Add(bits.ToString(CultureInfo.InvariantCulture) + "k");
                a.Add("-maxrate");
                a.Add(((int)(bits * 1.5)).ToString(CultureInfo.InvariantCulture) + "k");
                a.Add("-bufsize");
                a.Add(((int)(bits * 2)).ToString(CultureInfo.InvariantCulture) + "k");
                a.Add("-bf");
                a.Add("0");
                a.Add("-g");
                a.Add("250");
                a.Add("-pix_fmt");
                a.Add(preset.PixFmt);
            }

            if (opt.NoAudio)
            {
                a.Add("-an");
            }
            else if (string.Equals(opt.AudioFormat, "pcm", StringComparison.OrdinalIgnoreCase))
            {
                a.Add("-c:a");
                a.Add("pcm_s16le");
                a.Add("-ar");
                a.Add("44100");
                a.Add("-ac");
                a.Add("2");
            }
            else
            {
                a.Add("-c:a");
                a.Add("libmp3lame");
                a.Add("-b:a");
                a.Add(audioKbps.ToString(CultureInfo.InvariantCulture) + "k");
                a.Add("-ar");
                a.Add("44100");
                a.Add("-ac");
                a.Add("2");
            }

            a.Add("-sn");
            a.Add("-dn");
            a.Add("-map_metadata");
            a.Add("-1");
            a.Add("-f");
            a.Add("avi");
            a.Add(outputFile);
            return a;
        }

        public static ConvertResult Convert(string inputFile, string outputFile, ConvertOptions opt,
                                            Action<ProgressInfo> onProgress, Func<bool> cancelled,
                                            Action<string> onLog)
        {
            Stopwatch watch = Stopwatch.StartNew();
            ConvertResult result = new ConvertResult();
            result.Input = inputFile;
            result.Output = outputFile;

            Preset preset;
            try
            {
                preset = Presets.Get(opt.Preset);
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }

            if (File.Exists(outputFile) && !opt.Force && !opt.DryRun)
            {
                result.Skipped = true;
                result.Message = "Output already exists (use -Force to overwrite).";
                return result;
            }

            string ffmpeg = AppInfo.FfmpegPath;
            if (string.IsNullOrEmpty(ffmpeg))
            {
                result.Message = "FFmpeg was not found. Place ffmpeg.exe in the bin folder next to the app.";
                return result;
            }

            MediaInfo info;
            try
            {
                info = MediaInfo.Read(inputFile);
            }
            catch (Exception ex)
            {
                result.Message = "Cannot read input: " + ex.Message;
                return result;
            }
            result.Info = info;

            if (!info.HasVideo)
            {
                result.Message = "File has no video stream.";
                return result;
            }

            double targetFps;
            List<string> args = BuildArgs(info, opt, preset, outputFile, out targetFps);

            if (onLog != null)
            {
                onLog("ffmpeg " + CommandLine.Join(args));
            }

            if (opt.DryRun)
            {
                result.Success = true;
                result.DryRun = true;
                result.Message = "dry run";
                return result;
            }

            try
            {
                string dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            }
            catch (Exception ex)
            {
                result.Message = "Cannot create the output folder: " + ex.Message;
                return result;
            }

            Process process = null;
            StringBuilder errorText = new StringBuilder();
            object errorLock = new object();
            ProgressParser progress = new ProgressParser(info.Duration);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(ffmpeg, CommandLine.Join(args));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                process = new Process();
                process.StartInfo = psi;

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { return; }
                    lock (errorLock)
                    {
                        errorText.AppendLine(e.Data);
                        if (errorText.Length > 20000)
                        {
                            errorText.Remove(0, errorText.Length - 14000);
                        }
                    }
                };

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null || onProgress == null) { return; }
                    ProgressInfo pi = progress.Feed(e.Data);
                    if (pi != null) { onProgress(pi); }
                };

                if (!process.Start())
                {
                    result.Message = "ffmpeg did not start.";
                    return result;
                }

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                while (!process.WaitForExit(200))
                {
                    if (cancelled != null && cancelled())
                    {
                        try { process.Kill(); } catch { }
                        try { process.WaitForExit(5000); } catch { }
                        TryDelete(outputFile);

                        result.Cancelled = true;
                        result.Message = "Cancelled.";
                        result.Seconds = watch.Elapsed.TotalSeconds;
                        return result;
                    }
                }

                // Flush the asynchronous output handlers.
                process.WaitForExit();
                int exitCode = process.ExitCode;

                if (exitCode != 0)
                {
                    TryDelete(outputFile);
                    string detail;
                    lock (errorLock) { detail = LastLines(errorText.ToString(), 4); }

                    result.Message = "ffmpeg exited with code " + exitCode + "." +
                                     (detail.Length > 0 ? " " + detail : "");
                    result.Seconds = watch.Elapsed.TotalSeconds;
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (process != null)
                {
                    try { if (!process.HasExited) { process.Kill(); } } catch { }
                    try { process.Dispose(); } catch { }
                }
                TryDelete(outputFile);

                result.Message = "ffmpeg failed to start: " + ex.Message;
                result.Seconds = watch.Elapsed.TotalSeconds;
                return result;
            }
            finally
            {
                if (process != null) { try { process.Dispose(); } catch { } }
            }

            if (!File.Exists(outputFile))
            {
                result.Message = "ffmpeg reported success but produced no file.";
                result.Seconds = watch.Elapsed.TotalSeconds;
                return result;
            }

            // Sanity check: a truncated AVI is worse than an error message.
            try
            {
                MediaInfo outInfo = MediaInfo.Read(outputFile);
                result.OutputInfo = outInfo;

                double tolerance = Math.Max(2.0, info.Duration * 0.05);
                if (info.Duration > 0 && outInfo.Duration > 0 &&
                    outInfo.Duration < (info.Duration - tolerance))
                {
                    TryDelete(outputFile);
                    result.Message = "Output looks truncated (" +
                        Presets.FormatDuration(outInfo.Duration) + " vs " +
                        Presets.FormatDuration(info.Duration) + ").";
                    result.Seconds = watch.Elapsed.TotalSeconds;
                    return result;
                }
            }
            catch
            {
                // A failed verification is not fatal; keep the file.
            }

            long size = 0;
            try { size = new FileInfo(outputFile).Length; } catch { }

            if (size > 4L * 1024L * 1024L * 1024L)
            {
                result.Warning = "Output is larger than 4 GB: a FAT32 USB stick cannot store it.";
            }
            else if (size > 2L * 1024L * 1024L * 1024L)
            {
                result.Warning = "Output is larger than 2 GB: some older players stop at this limit.";
            }

            result.Success = true;
            result.Message = "OK";
            result.SizeBytes = size;
            result.Seconds = watch.Elapsed.TotalSeconds;
            return result;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) { File.Delete(path); } } catch { }
        }

        private static string LastLines(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) { return ""; }

            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> tail = new List<string>();
            for (int i = lines.Length - 1; i >= 0 && tail.Count < count; i--)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) { continue; }
                if (line.Length > 300) { line = line.Substring(0, 300) + "..."; }
                tail.Insert(0, line);
            }
            return string.Join(" / ", tail.ToArray());
        }

        private sealed class ProgressParser
        {
            private readonly double duration;
            private double outSeconds;
            private string speed = "";

            public ProgressParser(double durationSeconds)
            {
                duration = durationSeconds;
            }

            public ProgressInfo Feed(string line)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) { return null; }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key == "out_time_us" || key == "out_time_ms")
                {
                    double us;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out us))
                    {
                        outSeconds = us / 1000000.0;
                    }
                    return null;
                }

                if (key == "speed")
                {
                    speed = value;
                    return null;
                }

                if (key != "progress") { return null; }

                int percent = 0;
                if (duration > 0)
                {
                    percent = (int)Math.Min(100.0, Math.Max(0.0, (outSeconds / duration) * 100.0));
                }
                if (value == "end") { percent = 100; }

                ProgressInfo info = new ProgressInfo();
                info.Percent = percent;
                info.OutSeconds = outSeconds;
                info.Speed = speed == "N/A" ? "" : speed;
                return info;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace AviConverter
{
    internal static class CliProgram
    {
        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }
        }

        private static int Run(string[] args)
        {
            string presetName = "safe";
            string fit = "pad";
            string outDir = null;
            string suffix = "";
            int width = 0, height = 0, videoKbps = 0, audioKbps = 0;
            double fps = 0;
            string audio = "mp3";
            bool noAudio = false, recurse = false, force = false, noClobber = false;
            bool dryRun = false, deinterlace = false, quiet = false;

            bool listPresets = false, version = false, help = false;
            bool install = false, uninstall = false, setupFfmpeg = false, package = false;

            InstallOptions installOptions = new InstallOptions();
            List<string> paths = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string raw = args[i];

                if (raw == "--")
                {
                    for (i++; i < args.Length; i++) { paths.Add(args[i]); }
                    break;
                }

                if (raw.Length > 0 && (raw[0] == '-' || raw[0] == '/'))
                {
                    string key = raw.TrimStart('-', '/').ToLowerInvariant();
                    switch (key)
                    {
                        case "preset": presetName = Next(args, ref i, raw); break;
                        case "fit": fit = Next(args, ref i, raw); break;
                        case "outdir": outDir = Next(args, ref i, raw); break;
                        case "suffix": suffix = Next(args, ref i, raw); break;
                        case "width": width = NextInt(args, ref i, raw); break;
                        case "height": height = NextInt(args, ref i, raw); break;
                        case "videokbps": videoKbps = NextInt(args, ref i, raw); break;
                        case "audiokbps": audioKbps = NextInt(args, ref i, raw); break;
                        case "fps": fps = NextDouble(args, ref i, raw); break;
                        case "audio": audio = Next(args, ref i, raw); break;
                        case "noaudio": noAudio = true; break;
                        case "recurse": recurse = true; break;
                        case "force": force = true; break;
                        case "noclobber": noClobber = true; break;
                        case "dryrun": dryRun = true; break;
                        case "deinterlace": deinterlace = true; break;
                        case "listpresets": listPresets = true; break;
                        case "version": version = true; break;
                        case "help": case "h": case "?": help = true; break;
                        case "install": install = true; break;
                        case "uninstall": uninstall = true; break;
                        case "quiet": quiet = true; break;
                        case "setup-ffmpeg": setupFfmpeg = true; break;
                        case "package": package = true; break;
                        case "dir": installOptions.Destination = Next(args, ref i, raw); break;
                        case "desktop-dir": installOptions.DesktopDir = Next(args, ref i, raw); break;
                        case "start-menu-dir": installOptions.StartMenuDir = Next(args, ref i, raw); break;
                        case "no-desktop": installOptions.NoDesktopShortcut = true; break;
                        case "no-start-menu": installOptions.NoStartMenu = true; break;
                        case "no-registry": installOptions.NoRegistry = true; break;
                        case "launch": installOptions.Launch = true; break;
                        default:
                            Console.Error.WriteLine("Unknown option: " + raw);
                            Console.Error.WriteLine();
                            PrintUsage();
                            return 2;
                    }
                    continue;
                }

                paths.Add(raw);
            }

            if (version)
            {
                Console.WriteLine();
                Console.WriteLine("  " + AppInfo.FullName + " v" + AppInfo.Version);
                Console.WriteLine("  " + AppInfo.Description);
                Console.WriteLine();
                Console.WriteLine("  " + AppInfo.Tagline);
                Console.WriteLine("  " + AppInfo.Credit);
                Console.WriteLine("  " + AppInfo.Copyright);
                Console.WriteLine();
                return 0;
            }

            if (help)
            {
                PrintUsage();
                return 0;
            }

            if (listPresets)
            {
                PrintPresets();
                return 0;
            }

            if (setupFfmpeg)
            {
                try
                {
                    SetupFfmpeg.Run(Console.WriteLine);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("ERROR: " + ex.Message);
                    return 2;
                }
            }

            if (package)
            {
                return BuildPackage();
            }

            if (install)
            {
                installOptions.Quiet = quiet;
                InstallResult result = Installer.Install(installOptions, quiet ? null : new Action<string>(Console.WriteLine));
                if (!result.Success)
                {
                    Console.Error.WriteLine("ERROR: " + result.Message);
                    return 1;
                }
                return 0;
            }

            if (uninstall)
            {
                Func<string, bool> confirm = null;
                if (!quiet)
                {
                    confirm = delegate(string question)
                    {
                        Console.WriteLine();
                        Console.WriteLine(question);
                        Console.Write("Remove? [y/N] ");
                        string answer = Console.ReadLine();
                        if (answer == null) { return false; }
                        answer = answer.Trim().ToLowerInvariant();
                        return answer == "y" || answer == "yes";
                    };
                }

                UninstallResult result = Installer.Uninstall(quiet, confirm,
                    quiet ? null : new Action<string>(Console.WriteLine));

                if (result.Cancelled) { return 0; }
                if (!result.Success)
                {
                    if (quiet) { Console.Error.WriteLine(result.Message); }
                    return 1;
                }
                return 0;
            }

            if (paths.Count == 0)
            {
                PrintUsage();
                return 2;
            }

            Preset preset;
            try
            {
                preset = Presets.Get(presetName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }

            fit = (fit ?? "pad").ToLowerInvariant();
            if (fit != "pad" && fit != "crop" && fit != "stretch")
            {
                Console.Error.WriteLine("ERROR: -Fit must be pad, crop or stretch.");
                return 2;
            }

            audio = (audio ?? "mp3").ToLowerInvariant();
            if (audio != "mp3" && audio != "pcm")
            {
                Console.Error.WriteLine("ERROR: -Audio must be mp3 or pcm.");
                return 2;
            }

            if (string.IsNullOrEmpty(AppInfo.FfmpegPath) || string.IsNullOrEmpty(AppInfo.FfprobePath))
            {
                Console.Error.WriteLine("ERROR: FFmpeg was not found. Place ffmpeg.exe and ffprobe.exe in the bin folder.");
                return 2;
            }

            string[] files = ResolveInputs(paths.ToArray(), recurse);
            if (files.Length == 0)
            {
                Console.Error.WriteLine("No supported video files were found.");
                return 2;
            }

            if (outDir != null && outDir.Trim().Length > 0)
            {
                try { outDir = Path.GetFullPath(outDir); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("ERROR: invalid output folder: " + ex.Message);
                    return 2;
                }
            }
            else
            {
                outDir = null;
            }

            Console.WriteLine();
            Console.WriteLine(string.Format("AVI Converter v{0}  -  preset: {1}  ({2}x{3})",
                AppInfo.Version, preset.Key, preset.Width, preset.Height));
            Console.WriteLine(AppInfo.Tagline + "  |  " + AppInfo.Credit);
            Console.WriteLine(files.Length + " file(s)");
            Console.WriteLine();

            int ok = 0, failed = 0, skipped = 0;
            bool overwriteAll = force;
            HashSet<string> plannedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stopwatch totalTime = Stopwatch.StartNew();
            int index = 0;

            foreach (string file in files)
            {
                index++;
                string name = Path.GetFileName(file);
                string output = Converter.UniqueOutputPath(
                    Converter.OutputPath(file, outDir, suffix), plannedOutputs);

                if (File.Exists(output) && !dryRun)
                {
                    if (noClobber)
                    {
                        Console.WriteLine("[" + index + "/" + files.Length + "] " + name);
                        Console.WriteLine("        skipped: output exists (-NoClobber).");
                        skipped++;
                        continue;
                    }

                    if (!overwriteAll)
                    {
                        if (Console.IsInputRedirected)
                        {
                            Console.WriteLine("[" + index + "/" + files.Length + "] " + name);
                            Console.WriteLine("        skipped: output exists (use -Force to overwrite).");
                            skipped++;
                            continue;
                        }

                        Console.WriteLine();
                        Console.WriteLine("Output exists: " + output);
                        Console.Write("Overwrite? [y/N/a] ");
                        string answer = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                        if (answer == "a" || answer == "all")
                        {
                            overwriteAll = true;
                        }
                        else if (answer != "y" && answer != "yes")
                        {
                            Console.WriteLine("        skipped.");
                            skipped++;
                            continue;
                        }
                    }
                }

                Console.WriteLine("[" + index + "/" + files.Length + "] " + name);

                ConvertOptions options = new ConvertOptions();
                options.Preset = preset.Key;
                options.Fit = fit;
                options.Width = width;
                options.Height = height;
                options.Fps = fps;
                options.VideoKbps = videoKbps;
                options.AudioKbps = audioKbps;
                options.AudioFormat = audio;
                options.NoAudio = noAudio;
                options.Deinterlace = deinterlace;
                options.Force = overwriteAll || File.Exists(output);
                options.DryRun = dryRun;

                string shortName = Truncate(name, 46);
                Action<ProgressInfo> progress = delegate(ProgressInfo pi)
                {
                    if (dryRun) { return; }
                    string speed = string.IsNullOrEmpty(pi.Speed) ? "" : "  " + pi.Speed;
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "        {0,3}%{1}  {2}", pi.Percent, speed, shortName);
                    Console.Write("\r" + line.PadRight(78));
                };

                Action<string> logger = dryRun ? new Action<string>(Console.WriteLine) : null;

                ConvertResult result = Converter.Convert(file, output, options, progress,
                    delegate { return false; }, logger);

                if (!dryRun) { Console.WriteLine(); }

                if (result.Cancelled)
                {
                    Console.WriteLine("        cancelled.");
                    break;
                }

                if (result.Skipped)
                {
                    Console.WriteLine("        skipped: " + result.Message);
                    skipped++;
                    continue;
                }

                if (result.DryRun)
                {
                    Console.WriteLine("        (dry run)");
                    ok++;
                    continue;
                }

                if (!result.Success)
                {
                    Console.WriteLine("        FAILED: " + result.Message);
                    failed++;
                    continue;
                }

                Console.WriteLine(string.Format("        OK  {0}  ({1:0.0}s)",
                    Presets.FormatSize(result.SizeBytes), result.Seconds));

                if (!string.IsNullOrEmpty(result.Warning))
                {
                    Console.WriteLine("        WARNING: " + result.Warning);
                }
                ok++;
            }

            totalTime.Stop();
            Console.WriteLine();
            Console.WriteLine(string.Format("Done: {0} ok, {1} failed, {2} skipped.  Time {3}",
                ok, failed, skipped, Presets.FormatDuration(totalTime.Elapsed.TotalSeconds)));
            Console.WriteLine();

            return failed > 0 ? 1 : 0;
        }

        private static string[] ResolveInputs(string[] paths, bool recurse)
        {
            string[] extensions = new string[]
            {
                ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg",
                ".ts", ".m2ts", ".mts", ".3gp", ".3g2", ".ogv", ".rm", ".rmvb", ".asf", ".vob",
                ".divx", ".xvid", ".f4v", ".mxf", ".dv"
            };

            List<string> found = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                string full = path;
                try { full = Path.GetFullPath(path); }
                catch { }

                if (Directory.Exists(full))
                {
                    SearchOption option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    string[] files;
                    try { files = Directory.GetFiles(full, "*", option); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Cannot read folder " + full + ": " + ex.Message);
                        continue;
                    }

                    foreach (string file in files)
                    {
                        if (Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant()) >= 0 &&
                            seen.Add(file))
                        {
                            found.Add(file);
                        }
                    }
                }
                else if (File.Exists(full))
                {
                    if (seen.Add(full)) { found.Add(full); }
                }
                else
                {
                    Console.Error.WriteLine("Not found: " + path);
                }
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found.ToArray();
        }

        private static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("  " + AppInfo.FullName + " v" + AppInfo.Version);
            Console.WriteLine("  " + AppInfo.Description);
            Console.WriteLine();
            Console.WriteLine("  Usage:");
            Console.WriteLine("    avi-convert.exe <file or folder> [more...] [options]");
            Console.WriteLine("    avi-convert.exe --install [--quiet]");
            Console.WriteLine("    avi-convert.exe --uninstall [--quiet]");
            Console.WriteLine("    avi-convert.exe --setup-ffmpeg");
            Console.WriteLine("    avi-convert.exe --package");
            Console.WriteLine("    avi-convert.exe --version");
            Console.WriteLine();
            Console.WriteLine("  Options:");
            Console.WriteLine("    -Preset safe|hd|4x3|mjpeg|small   Output preset (default safe).");
            Console.WriteLine("    -Fit pad|crop|stretch             How to fit a different aspect ratio.");
            Console.WriteLine("    -OutDir <folder>                  Where to write the .avi files.");
            Console.WriteLine("    -Suffix <text>                    Add text to the output name.");
            Console.WriteLine("    -Width <n> -Height <n>            Override the frame size.");
            Console.WriteLine("    -VideoKbps <n> -AudioKbps <n>     Override the bitrates.");
            Console.WriteLine("    -Audio mp3|pcm                    Audio codec (pcm = uncompressed).");
            Console.WriteLine("    -NoAudio                          Drop the audio track.");
            Console.WriteLine("    -Fps <n>                          Force a frame rate.");
            Console.WriteLine("    -Recurse                          Include sub-folders.");
            Console.WriteLine("    -Force                            Overwrite output files.");
            Console.WriteLine("    -NoClobber                        Never overwrite, just skip.");
            Console.WriteLine("    -DryRun                           Show the ffmpeg command only.");
            Console.WriteLine("    -ListPresets                      Print the presets and exit.");
            Console.WriteLine();
            Console.WriteLine("  Example:");
            Console.WriteLine("    avi-convert.exe \"D:\\Movies\" -Preset hd -Recurse -OutDir \"E:\\USB\"");
            Console.WriteLine();
            Console.WriteLine("  " + AppInfo.Tagline + "  |  " + AppInfo.Credit);
            Console.WriteLine();
        }

        private static void PrintPresets()
        {
            Console.WriteLine();
            Console.WriteLine("  Presets (all output is AVI with MP3 stereo audio):");
            Console.WriteLine();
            foreach (Preset p in Presets.List)
            {
                Console.WriteLine(string.Format("  {0,-8} {1}", p.Key.ToUpperInvariant(), p.Label));
                Console.WriteLine("           " + p.Description);
                Console.WriteLine();
            }
            Console.WriteLine("  If the projector says \"unsupported format\", try MJPEG.");
            Console.WriteLine("  " + AppInfo.Tagline + "  |  " + AppInfo.Credit);
            Console.WriteLine();
        }

        private static int BuildPackage()
        {
            string versionDir = "FashionFlow-AVI-Converter-" + AppInfo.Version;
            string stage = Path.Combine(Path.GetTempPath(), "ffavi-package-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(stage, versionDir);

            try
            {
                Directory.CreateDirectory(root);

                string[] files = new string[]
                {
                    "AVIConverter.exe", "avi-convert.exe", "Install.bat", "Uninstall.bat"
                };
                foreach (string file in files)
                {
                    string from = Path.Combine(AppInfo.BaseDir, file);
                    if (File.Exists(from))
                    {
                        File.Copy(from, Path.Combine(root, file), true);
                    }
                }

                // The README is kept in docs\ in the project. The package has it at the
                // root, so fix the logo path for the root location.
                string readme = Path.Combine(AppInfo.BaseDir, @"docs\README.md");
                if (File.Exists(readme))
                {
                    string text = File.ReadAllText(readme);
                    text = text.Replace("../assets/logo.png", "assets/logo.png");
                    File.WriteAllText(Path.Combine(root, "README.md"), text);
                }

                foreach (string dir in new string[] { "bin", "assets", "src", "build", "tools" })
                {
                    string from = Path.Combine(AppInfo.BaseDir, dir);
                    if (Directory.Exists(from))
                    {
                        CopyDirectory(from, Path.Combine(root, dir));
                    }
                }

                string note = string.Join("\r\n", new string[]
                {
                    AppInfo.FullName + " " + AppInfo.Version,
                    AppInfo.Tagline,
                    AppInfo.Credit,
                    "",
                    "QUICK START",
                    "  1. Extract this ZIP anywhere (for example D:\\Tools).",
                    "  2. Double-click \"AVIConverter.exe\" to run it - no install needed.",
                    "     Or double-click \"Install.bat\" to add Start Menu and Desktop shortcuts.",
                    "  3. Drag your movie files onto the window and press Convert to AVI.",
                    "",
                    "DRAG AND DROP",
                    "  Drop files or folders onto tools\\Convert to AVI (drag and drop).bat",
                    "",
                    "UNINSTALL",
                    "  Double-click \"Uninstall.bat\", or use Settings > Apps > " + AppInfo.FullName,
                    "  or Start Menu > Fashion Flow > Uninstall AVI Converter.",
                    "",
                    "Read README.md for presets and troubleshooting."
                });
                File.WriteAllText(Path.Combine(root, "INSTALL.txt"), note);

                string outDir = Path.Combine(AppInfo.BaseDir, "dist");
                Directory.CreateDirectory(outDir);
                string zipPath = Path.Combine(outDir, versionDir + ".zip");
                if (File.Exists(zipPath)) { File.Delete(zipPath); }

                Console.WriteLine();
                Console.WriteLine("  Building " + Path.GetFileName(zipPath));
                Console.WriteLine("  Compressing (FFmpeg is about 200 MB, this takes a moment)...");
                ZipFile.CreateFromDirectory(root, zipPath, CompressionLevel.Optimal, true);

                long size = new FileInfo(zipPath).Length;
                Console.WriteLine();
                Console.WriteLine("  Package: " + zipPath);
                Console.WriteLine("  Size:    " + Presets.FormatSize(size));
                Console.WriteLine();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
            finally
            {
                try { Directory.Delete(stage, true); } catch { }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
            foreach (string dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
            }
        }

        private static string Next(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length) { throw new ArgumentException("Missing value for " + option); }
            index++;
            return args[index];
        }

        private static int NextInt(string[] args, ref int index, string option)
        {
            string value = Next(args, ref index, option);
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                throw new ArgumentException("Invalid number for " + option + ": " + value);
            }
            return parsed;
        }

        private static double NextDouble(string[] args, ref int index, string option)
        {
            string value = Next(args, ref index, option);
            double parsed;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                throw new ArgumentException("Invalid number for " + option + ": " + value);
            }
            return parsed;
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) { return text ?? ""; }
            return text.Substring(0, max - 3) + "...";
        }
    }
}

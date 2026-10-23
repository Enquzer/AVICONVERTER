using System;
using System.IO;
using System.Reflection;

[assembly: AssemblyTitle("Fashion Flow AVI Converter")]
[assembly: AssemblyDescription("Converts movies to projector-friendly AVI files.")]
[assembly: AssemblyCompany("Fashion Flow")]
[assembly: AssemblyProduct("Fashion Flow AVI Converter")]
[assembly: AssemblyCopyright("(c) 2026 Fashion Flow")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AviConverter
{
    internal static class AppInfo
    {
        public const string Name = "AVI Converter";
        public const string FullName = "Fashion Flow AVI Converter";
        public const string Version = "1.0.0";
        public const string Publisher = "Fashion Flow";
        public const string Developer = "Enquzer Getachew";
        public const string Tagline = "Powered by Fashion Flow 2026";
        public const string Credit = "Developed by Enquzer Getachew";
        public const string Copyright = "(c) 2026 Fashion Flow";
        public const string Description = "Converts movies to projector-friendly AVI files.";
        public const string ShortcutName = "AVI Converter";
        public const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FashionFlowAVIConverter";

        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string ExePath
        {
            get { return Assembly.GetExecutingAssembly().Location; }
        }

        public static string GuiExePath
        {
            get { return Path.Combine(BaseDir, "AVIConverter.exe"); }
        }

        public static string InstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.Combine("Programs", Path.Combine("FashionFlow", "AVI Converter")));
            }
        }

        public static string StartMenuDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Fashion Flow");
            }
        }

        public static string DesktopDir
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }
        }

        public static string FfmpegPath
        {
            get { return FindTool("ffmpeg.exe"); }
        }

        public static string FfprobePath
        {
            get { return FindTool("ffprobe.exe"); }
        }

        private static string FindTool(string fileName)
        {
            string local = Path.Combine(BaseDir, Path.Combine("bin", fileName));
            if (File.Exists(local)) { return local; }

            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                string[] dirs = path.Split(';');
                foreach (string raw in dirs)
                {
                    string dir = raw.Trim().Trim('"');
                    if (dir.Length == 0) { continue; }
                    try
                    {
                        string candidate = Path.Combine(dir, fileName);
                        if (File.Exists(candidate)) { return candidate; }
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}

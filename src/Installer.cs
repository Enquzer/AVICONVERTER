using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AviConverter
{
    internal sealed class InstallOptions
    {
        public string Destination;
        public string DesktopDir;
        public string StartMenuDir;
        public bool NoDesktopShortcut;
        public bool NoStartMenu;
        public bool NoRegistry;
        public bool Launch;
        public bool Quiet;
    }

    internal sealed class InstallResult
    {
        public bool Success;
        public string Message = "";
        public string InstallDir = "";
    }

    internal sealed class UninstallResult
    {
        public bool Success;
        public bool Cancelled;
        public string Message = "";
    }

    internal static class Installer
    {
        public static bool IsInstalled
        {
            get
            {
                try
                {
                    if (File.Exists(Path.Combine(AppInfo.InstallDir, ".ffavi-install"))) { return true; }
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKeyPath))
                    {
                        return key != null;
                    }
                }
                catch { return false; }
            }
        }

        public static bool IsInstalledCopy
        {
            get
            {
                try
                {
                    return File.Exists(Path.Combine(AppInfo.BaseDir, ".ffavi-install"));
                }
                catch { return false; }
            }
        }

        public static string InstalledVersion
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKeyPath))
                    {
                        if (key == null) { return ""; }
                        object value = key.GetValue("DisplayVersion");
                        return value == null ? "" : value.ToString();
                    }
                }
                catch { return ""; }
            }
        }

        public static InstallResult Install(InstallOptions options, Action<string> log)
        {
            InstallResult result = new InstallResult();
            if (options == null) { options = new InstallOptions(); }

            string source = AppInfo.BaseDir;
            string destination = string.IsNullOrEmpty(options.Destination) ? AppInfo.InstallDir : options.Destination;
            try { destination = Path.GetFullPath(destination).TrimEnd('\\'); }
            catch (Exception ex)
            {
                result.Message = "Invalid install folder: " + ex.Message;
                return result;
            }
            result.InstallDir = destination;

            string sourceFull;
            try { sourceFull = Path.GetFullPath(source).TrimEnd('\\'); }
            catch { sourceFull = source; }

            if (destination.StartsWith(sourceFull + "\\", StringComparison.OrdinalIgnoreCase))
            {
                result.Message = "The install folder cannot be inside the app folder.";
                return result;
            }

            string guiExe = Path.Combine(source, "AVIConverter.exe");
            if (!File.Exists(guiExe))
            {
                result.Message = "AVIConverter.exe is missing. Extract the whole package and try again.";
                return result;
            }
            if (!File.Exists(Path.Combine(source, "bin\\ffmpeg.exe")) ||
                !File.Exists(Path.Combine(source, "bin\\ffprobe.exe")))
            {
                result.Message = "The bundled FFmpeg (bin\\ffmpeg.exe and bin\\ffprobe.exe) is missing.";
                return result;
            }

            bool repair = string.Equals(sourceFull, destination, StringComparison.OrdinalIgnoreCase);

            try
            {
                Log(log, "");
                Log(log, "  " + AppInfo.FullName + " v" + AppInfo.Version);
                Log(log, "  " + AppInfo.Tagline);
                Log(log, "  " + AppInfo.Credit);
                Log(log, "  " + new string('-', 58));

                if (!repair)
                {
                    Log(log, "Installing to " + destination);
                    Directory.CreateDirectory(destination);

                    string[] files = new string[] { "AVIConverter.exe", "avi-convert.exe" };
                    foreach (string file in files)
                    {
                        string from = Path.Combine(source, file);
                        if (File.Exists(from))
                        {
                            File.Copy(from, Path.Combine(destination, file), true);
                        }
                    }

                    // The README lives in docs\ in the project layout. Put a copy in the
                    // installed folder so users can still read it there.
                    string readme = Path.Combine(source, @"docs\README.md");
                    if (!File.Exists(readme)) { readme = Path.Combine(source, "README.md"); }
                    if (File.Exists(readme))
                    {
                        string text = File.ReadAllText(readme);
                        text = text.Replace("../assets/logo.png", "assets/logo.png");
                        File.WriteAllText(Path.Combine(destination, "README.md"), text);
                    }

                    string[] dirs = new string[] { "bin", "assets" };
                    foreach (string dir in dirs)
                    {
                        string from = Path.Combine(source, dir);
                        if (Directory.Exists(from))
                        {
                            CopyDirectory(from, Path.Combine(destination, dir));
                        }
                    }
                    Log(log, "  Files copied.");
                }
                else
                {
                    Log(log, "  Files are already in place (repair mode).");
                }

                // Marker used by the uninstaller to recognise a real installation.
                // It is hidden, so clear that attribute before rewriting it on a reinstall.
                string marker = Path.Combine(destination, ".ffavi-install");
                if (File.Exists(marker))
                {
                    try { File.SetAttributes(marker, FileAttributes.Normal); } catch { }
                }
                File.WriteAllText(marker, AppInfo.Version);
                try { File.SetAttributes(marker, FileAttributes.Hidden); } catch { }

                string targetExe = Path.Combine(destination, "AVIConverter.exe");
                string icon = targetExe + ",0";
                string description = AppInfo.FullName + " - " + AppInfo.Tagline;
                List<string> shortcutPaths = new List<string>();

                if (!options.NoStartMenu)
                {
                    string startDir = string.IsNullOrEmpty(options.StartMenuDir) ? AppInfo.StartMenuDir : options.StartMenuDir;
                    string appLink = Path.Combine(startDir, AppInfo.ShortcutName + ".lnk");
                    string uninstallLink = Path.Combine(startDir, "Uninstall AVI Converter.lnk");

                    CreateShortcut(appLink, targetExe, "", destination, icon, description);
                    CreateShortcut(uninstallLink, targetExe, "--uninstall", destination, icon,
                        "Uninstall " + AppInfo.FullName);

                    shortcutPaths.Add(appLink);
                    shortcutPaths.Add(uninstallLink);
                    Log(log, "  Start Menu shortcuts created.");
                }

                if (!options.NoDesktopShortcut)
                {
                    string desktop = string.IsNullOrEmpty(options.DesktopDir) ? AppInfo.DesktopDir : options.DesktopDir;
                    if (!string.IsNullOrEmpty(desktop))
                    {
                        string desktopLink = Path.Combine(desktop, AppInfo.ShortcutName + ".lnk");
                        CreateShortcut(desktopLink, targetExe, "", destination, icon, description);
                        shortcutPaths.Add(desktopLink);
                        Log(log, "  Desktop shortcut created.");
                    }
                }

                if (!options.NoRegistry)
                {
                    WriteUninstallEntry(destination, targetExe, icon, shortcutPaths);
                    Log(log, "  Added to Apps & features.");
                }

                Log(log, "");
                Log(log, "  Installation complete.");
                Log(log, "  Start Menu > Fashion Flow > " + AppInfo.ShortcutName);
                Log(log, "  Remove: Settings > Apps > " + AppInfo.FullName);
                Log(log, "");

                if (options.Launch)
                {
                    try { Process.Start(targetExe); } catch { }
                }

                result.Success = true;
                result.Message = "Installed to " + destination;
                return result;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(log, "ERROR: " + ex.Message);
                Log(log, "If the app is open, close it and run the installer again.");
                return result;
            }
        }

        public static UninstallResult Uninstall(bool quiet, Func<string, bool> confirm, Action<string> log)
        {
            UninstallResult result = new UninstallResult();
            string dir = AppInfo.BaseDir;
            string marker = Path.Combine(dir, ".ffavi-install");
            string registered = "";
            string registeredShortcuts = "";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKeyPath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("InstallLocation");
                        if (value != null) { registered = value.ToString().TrimEnd('\\'); }

                        object shortcuts = key.GetValue("ShortcutPaths");
                        if (shortcuts != null) { registeredShortcuts = shortcuts.ToString(); }
                    }
                }
            }
            catch { }

            bool allowed = File.Exists(marker) ||
                string.Equals(registered, dir, StringComparison.OrdinalIgnoreCase);

            if (!allowed)
            {
                result.Message = "This folder is not an installed copy of " + AppInfo.FullName + ". Nothing was removed.";
                Log(log, "");
                Log(log, result.Message);
                return result;
            }

            if (!quiet && confirm != null)
            {
                string question = "Remove " + AppInfo.FullName + " v" + AppInfo.Version + "?\r\n\r\nFolder: " + dir;
                if (!confirm(question))
                {
                    result.Cancelled = true;
                    result.Message = "Cancelled.";
                    Log(log, "Cancelled.");
                    return result;
                }
            }

            try
            {
                Log(log, "");
                Log(log, "  Uninstall " + AppInfo.FullName + " v" + AppInfo.Version);
                Log(log, "  Folder: " + dir);
                Log(log, "");

                // Leave the folder so it can be deleted.
                try { Environment.CurrentDirectory = Path.GetTempPath(); } catch { }

                KillRunningCopies(dir);

                string installedExe = Path.Combine(dir, "AVIConverter.exe");
                HashSet<string> startDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string shortcut in registeredShortcuts.Split(
                    new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    RemoveShortcut(shortcut, installedExe, null);

                    string shortcutDir = Path.GetDirectoryName(shortcut);
                    if (!string.IsNullOrEmpty(shortcutDir)) { startDirs.Add(shortcutDir); }
                }

                string desktop = AppInfo.DesktopDir;
                if (!string.IsNullOrEmpty(desktop))
                {
                    RemoveShortcut(Path.Combine(desktop, AppInfo.ShortcutName + ".lnk"), installedExe, null);
                }

                string startDir = AppInfo.StartMenuDir;
                RemoveShortcut(Path.Combine(startDir, AppInfo.ShortcutName + ".lnk"), installedExe, null);
                RemoveShortcut(Path.Combine(startDir, "Uninstall AVI Converter.lnk"), installedExe, "--uninstall");
                startDirs.Add(startDir);

                string startMenuRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu");

                foreach (string folder in startDirs)
                {
                    if (string.IsNullOrEmpty(folder)) { continue; }
                    if (!folder.StartsWith(startMenuRoot, StringComparison.OrdinalIgnoreCase)) { continue; }
                    if (!string.IsNullOrEmpty(desktop) &&
                        string.Equals(folder.TrimEnd('\\'), desktop.TrimEnd('\\'),
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                        {
                            Directory.Delete(folder);
                        }
                    }
                    catch { }
                }

                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(AppInfo.UninstallKeyPath, false);
                }
                catch { }

                Log(log, "  Shortcuts and registry entry removed.");
                Log(log, "  Removing program files...");

                // Delete the folder after this process exits (the exe lives inside it).
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                    "/c ping -n 3 127.0.0.1 >nul & rd /s /q \"" + dir + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetTempPath();
                Process.Start(psi);

                Log(log, "");
                Log(log, "  Uninstalled. Thank you for using " + AppInfo.FullName + ".");
                Log(log, "  " + AppInfo.Tagline + "  |  " + AppInfo.Credit);
                Log(log, "");

                result.Success = true;
                result.Message = "Uninstalled.";
                return result;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(log, "ERROR: " + ex.Message);
                return result;
            }
        }

        private static void KillRunningCopies(string dir)
        {
            string prefix = dir.TrimEnd('\\') + "\\";
            string[] names = new string[] { "AVIConverter", "avi-convert", "ffmpeg", "ffprobe" };
            int self = Process.GetCurrentProcess().Id;

            foreach (string name in names)
            {
                Process[] processes;
                try { processes = Process.GetProcessesByName(name); }
                catch { continue; }

                foreach (Process p in processes)
                {
                    if (p.Id == self) { continue; }
                    try
                    {
                        string file = p.MainModule.FileName;
                        if (file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }

            System.Threading.Thread.Sleep(400);
        }

        private static void WriteUninstallEntry(string destination, string targetExe, string icon,
                                                List<string> shortcutPaths)
        {
            long bytes = 0;
            try
            {
                foreach (string file in Directory.GetFiles(destination, "*", SearchOption.AllDirectories))
                {
                    try { bytes += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }

            int sizeKb = (int)Math.Min(int.MaxValue, bytes / 1024L);

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallKeyPath))
            {
                key.SetValue("DisplayName", AppInfo.FullName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", AppInfo.Version, RegistryValueKind.String);
                key.SetValue("Publisher", AppInfo.Publisher, RegistryValueKind.String);
                key.SetValue("DisplayIcon", icon, RegistryValueKind.String);
                key.SetValue("InstallLocation", destination, RegistryValueKind.String);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                key.SetValue("Comments", AppInfo.Tagline + ". " + AppInfo.Credit + ".", RegistryValueKind.String);
                key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("UninstallString", "\"" + targetExe + "\" --uninstall", RegistryValueKind.String);
                key.SetValue("QuietUninstallString", "\"" + targetExe + "\" --uninstall --quiet", RegistryValueKind.String);
                if (shortcutPaths != null && shortcutPaths.Count > 0)
                {
                    key.SetValue("ShortcutPaths", string.Join("|", shortcutPaths.ToArray()),
                        RegistryValueKind.String);
                }
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

        private static void CreateShortcut(string path, string target, string arguments,
                                           string workingDirectory, string icon, string description)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) { throw new InvalidOperationException("Windows Script Host is not available."); }

            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                dynamic dynamicShell = shell;
                shortcut = dynamicShell.CreateShortcut(path);
                dynamic lnk = shortcut;
                lnk.TargetPath = target;
                lnk.Arguments = arguments ?? "";
                lnk.WorkingDirectory = workingDirectory;
                lnk.IconLocation = icon;
                lnk.Description = description;
                lnk.Save();
            }
            finally
            {
                if (shortcut != null) { try { Marshal.ReleaseComObject(shortcut); } catch { } }
                try { Marshal.ReleaseComObject(shell); } catch { }
            }
        }

        private static void RemoveShortcut(string path, string expectedTarget, string requiredArgument)
        {
            if (!File.Exists(path)) { return; }

            bool ours = false;
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                object shell = null;
                object shortcut = null;
                try
                {
                    shell = Activator.CreateInstance(shellType);
                    dynamic dynamicShell = shell;
                    shortcut = dynamicShell.CreateShortcut(path);
                    dynamic lnk = shortcut;

                    string target = (string)lnk.TargetPath;
                    string arguments = (string)lnk.Arguments;

                    if (string.Equals(target, expectedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        if (requiredArgument == null ||
                            (arguments != null && arguments.IndexOf(requiredArgument, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            ours = true;
                        }
                    }
                }
                catch { }
                finally
                {
                    if (shortcut != null) { try { Marshal.ReleaseComObject(shortcut); } catch { } }
                    if (shell != null) { try { Marshal.ReleaseComObject(shell); } catch { } }
                }
            }

            if (ours)
            {
                try { File.Delete(path); } catch { }
            }
        }

        private static void Log(Action<string> log, string text)
        {
            if (log != null) { log(text); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AviConverter
{
    internal static class GuiProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0)
            {
                string first = args[0].TrimStart('-', '/').ToLowerInvariant();
                if (first == "install") { RunInstall(args); return; }
                if (first == "uninstall") { RunUninstall(args); return; }
                if (first == "version") { ShowVersion(); return; }
                if (first == "help" || first == "?") { ShowHelp(); return; }
            }

            List<string> files = new List<string>();
            bool selfTest = false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    selfTest = true;
                    continue;
                }
                files.Add(arg);
            }

            MainForm form = new MainForm(files.ToArray());

            if (selfTest)
            {
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 6000;
                timer.Tick += delegate
                {
                    timer.Stop();
                    form.Close();
                };
                form.Shown += delegate { timer.Start(); };
            }

            Application.Run(form);
        }

        private static void RunInstall(string[] args)
        {
            bool quiet = false;
            InstallOptions options = new InstallOptions();

            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i].TrimStart('-', '/').ToLowerInvariant();
                switch (key)
                {
                    case "quiet": quiet = true; break;
                    case "dir":
                        if (i + 1 < args.Length) { options.Destination = args[++i]; }
                        break;
                    case "desktop-dir":
                        if (i + 1 < args.Length) { options.DesktopDir = args[++i]; }
                        break;
                    case "start-menu-dir":
                        if (i + 1 < args.Length) { options.StartMenuDir = args[++i]; }
                        break;
                    case "no-desktop": options.NoDesktopShortcut = true; break;
                    case "no-start-menu": options.NoStartMenu = true; break;
                    case "no-registry": options.NoRegistry = true; break;
                    case "launch": options.Launch = true; break;
                }
            }

            options.Quiet = quiet;
            InstallResult result = Installer.Install(options, null);

            if (!quiet)
            {
                if (result.Success)
                {
                    MessageBox.Show(
                        "Installed to:\r\n" + result.InstallDir +
                        "\r\n\r\nStart Menu > Fashion Flow > " + AppInfo.ShortcutName +
                        "\r\nA Desktop shortcut was created too." +
                        "\r\n\r\nTo uninstall: Settings > Apps > " + AppInfo.FullName + ".",
                        "Installation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        "The installation did not finish:\r\n\r\n" + result.Message +
                        "\r\n\r\nIf the app is open, close it and try again.",
                        "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            Environment.Exit(result.Success ? 0 : 1);
        }

        private static void RunUninstall(string[] args)
        {
            bool quiet = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i].TrimStart('-', '/'), "quiet", StringComparison.OrdinalIgnoreCase))
                {
                    quiet = true;
                }
            }

            Func<string, bool> confirm = null;
            if (!quiet)
            {
                confirm = delegate(string question)
                {
                    return MessageBox.Show(question, "Uninstall " + AppInfo.FullName,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                };
            }

            UninstallResult result = Installer.Uninstall(quiet, confirm, null);

            if (!quiet && !result.Success && !result.Cancelled)
            {
                MessageBox.Show(result.Message, "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Do not keep a dialog open here: the program folder is deleted a moment
            // after this process exits.
            Environment.Exit(result.Success || result.Cancelled ? 0 : 1);
        }

        private static void ShowVersion()
        {
            MessageBox.Show(
                AppInfo.FullName + " v" + AppInfo.Version + "\r\n\r\n" +
                AppInfo.Description + "\r\n\r\n" +
                AppInfo.Tagline + "\r\n" +
                AppInfo.Credit + "\r\n" +
                AppInfo.Copyright,
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowHelp()
        {
            MessageBox.Show(
                AppInfo.FullName + " v" + AppInfo.Version + "\r\n\r\n" +
                "Drag video files or folders onto the window, pick a preset and press Convert to AVI.\r\n\r\n" +
                "For the command line, use avi-convert.exe --help.\r\n\r\n" +
                "avi-convert.exe --install     install for this user\r\n" +
                "avi-convert.exe --uninstall   remove the installed copy\r\n\r\n" +
                AppInfo.Tagline + "  |  " + AppInfo.Credit,
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    internal static class Art
    {
        public static Image LoadLogo()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AVIConverter.logo.png"))
                {
                    if (stream == null) { return null; }
                    using (Image temporary = Image.FromStream(stream))
                    {
                        return new Bitmap(temporary);
                    }
                }
            }
            catch { return null; }
        }

        public static Icon LoadAppIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color PageColor = Color.FromArgb(246, 247, 249);
        private static readonly Color MutedColor = Color.FromArgb(96, 102, 112);
        private static readonly Color HintColor = Color.FromArgb(110, 115, 125);
        private static readonly Color BrandColor = Color.FromArgb(122, 90, 166);
        private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);

        private readonly string[] initialFiles;

        private ListView list;
        private ComboBox presetBox;
        private ComboBox fitBox;
        private TextBox outputBox;
        private CheckBox overwriteBox;
        private ProgressBar progress;
        private Label statusLabel;
        private Label doneLabel;
        private NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer notifyTimer;
        private RichTextBox logBox;
        private Panel actionPanel;
        private Button convertButton;
        private Button cancelButton;
        private Button openButton;
        private Button presetInfoButton;
        private Button aboutButton;
        private Button installButton;
        private ToolTip tooltip;

        private readonly HashSet<string> knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private volatile bool converting;
        private volatile bool cancelRequested;
        private bool closeWhenDone;
        private Thread worker;
        private string lastOutput = "";

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint flags);
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        private const uint FLASHW_ALL = 0x00000003;
        private const uint FLASHW_TIMERNOFG = 0x0000000C;

        public MainForm(string[] files)
        {
            initialFiles = files ?? new string[0];

            Text = AppInfo.Name + " - Fashion Flow";
            Size = new Size(960, 720);
            MinimumSize = new Size(820, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = PageColor;
            AllowDrop = true;

            Icon icon = Art.LoadAppIcon();
            if (icon != null) { Icon = icon; }

            BuildUi();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                if (notifyTimer != null)
                {
                    notifyTimer.Stop();
                    notifyTimer.Dispose();
                    notifyTimer = null;
                }
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                    notifyIcon = null;
                }
            };

            Shown += delegate
            {
                LayoutActions();
                AdjustColumns();
                UpdateInstallButton();

                try
                {
                    string ffmpeg = AppInfo.FfmpegPath;
                    string ffprobe = AppInfo.FfprobePath;
                    if (string.IsNullOrEmpty(ffmpeg) || string.IsNullOrEmpty(ffprobe))
                    {
                        throw new InvalidOperationException(
                            "FFmpeg was not found.\r\n\r\nPlace ffmpeg.exe and ffprobe.exe in the bin folder next to the app.");
                    }

                    Log(AppInfo.Name + " v" + AppInfo.Version + "  |  " + AppInfo.Tagline, Color.DimGray);
                    Log(AppInfo.Credit + "  |  " + AppInfo.Copyright, Color.DimGray);
                    Log("FFmpeg ready: " + ffmpeg, Color.DimGray);
                    Log("Add videos, choose a preset, then press \"Convert to AVI\".", Color.DarkSlateGray);
                }
                catch (Exception ex)
                {
                    Log("ERROR: " + ex.Message, Color.Red);
                    MessageBox.Show(this, ex.Message, "FFmpeg missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if (initialFiles.Length > 0)
                {
                    AddFiles(initialFiles);
                }
            };
        }

        // -------------------------------------------------------------------
        // UI construction
        // -------------------------------------------------------------------
        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 8;
            root.Padding = new Padding(12, 8, 12, 10);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            Controls.Add(root);

            tooltip = new ToolTip();
            tooltip.AutoPopDelay = 12000;
            tooltip.InitialDelay = 300;
            tooltip.ReshowDelay = 100;

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildList(), 0, 1);
            root.Controls.Add(BuildListButtons(), 0, 2);
            root.Controls.Add(BuildSettings(), 0, 3);
            root.Controls.Add(BuildProgress(), 0, 4);
            root.Controls.Add(BuildLog(), 0, 5);
            root.Controls.Add(BuildActions(), 0, 6);
            root.Controls.Add(BuildFooter(), 0, 7);

            EnableDrop(root);
            EnableDrop(logBox);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0, 0, 0, 6);

            Image logo = Art.LoadLogo();
            if (logo != null)
            {
                PictureBox picture = new PictureBox();
                picture.Image = logo;
                picture.SizeMode = PictureBoxSizeMode.Zoom;
                picture.Location = new Point(0, 1);
                picture.Size = new Size(46, 46);
                picture.BackColor = Color.Transparent;
                header.Controls.Add(picture);
            }

            Label title = new Label();
            title.Text = "Drag && drop video files or folders here";
            title.Font = new Font("Segoe UI Semibold", 13F);
            title.AutoSize = true;
            title.Location = new Point(56, 1);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "MP4, MKV, MOV, WMV, WEBM, big AVI ...  ->  projector-friendly AVI (Xvid / MPEG-4 + MP3)";
            subtitle.ForeColor = MutedColor;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(58, 29);
            header.Controls.Add(subtitle);

            EnableDrop(header);
            return header;
        }

        private Control BuildList()
        {
            list = new ListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.GridLines = true;
            list.CheckBoxes = true;
            list.HideSelection = false;
            list.ShowItemToolTips = true;
            list.AllowDrop = true;
            list.Margin = new Padding(0, 0, 0, 4);
            list.Columns.Add("File", 420, HorizontalAlignment.Left);
            list.Columns.Add("Size", 90, HorizontalAlignment.Right);
            list.Columns.Add("Length", 80, HorizontalAlignment.Right);
            list.Columns.Add("Status", 190, HorizontalAlignment.Left);
            list.Resize += delegate { AdjustColumns(); };
            list.DragEnter += OnDragEnter;
            list.DragDrop += OnDragDrop;
            return list;
        }

        private Control BuildListButtons()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = false;
            panel.Margin = new Padding(0, 0, 0, 6);

            Button addFiles = SmallButton("Add files...", 100);
            addFiles.Click += delegate { ChooseFiles(); };

            Button addFolder = SmallButton("Add folder...", 105);
            addFolder.Click += delegate { ChooseFolder(); };

            Button remove = SmallButton("Remove", 80);
            remove.Click += delegate { RemoveSelected(); };

            Button clear = SmallButton("Clear list", 90);
            clear.Click += delegate { ClearList(); };

            panel.Controls.Add(addFiles);
            panel.Controls.Add(addFolder);
            panel.Controls.Add(remove);
            panel.Controls.Add(clear);
            return panel;
        }

        private static Button SmallButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 28;
            button.FlatStyle = FlatStyle.System;
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private Control BuildSettings()
        {
            GroupBox group = new GroupBox();
            group.Text = "Output settings";
            group.Dock = DockStyle.Fill;
            group.Margin = new Padding(0, 0, 0, 8);
            group.Padding = new Padding(10, 4, 10, 6);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 5;
            grid.RowCount = 3;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            group.Controls.Add(grid);

            Label presetLabel = FieldLabel("Preset");
            grid.Controls.Add(presetLabel, 0, 0);

            presetBox = new ComboBox();
            presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
            presetBox.Dock = DockStyle.Fill;
            presetBox.Margin = new Padding(0, 3, 8, 3);
            foreach (Preset p in Presets.List) { presetBox.Items.Add(p.Label); }
            presetBox.SelectedIndex = 0;
            presetBox.SelectedIndexChanged += delegate
            {
                Preset p = Presets.List[presetBox.SelectedIndex];
                tooltip.SetToolTip(presetBox, p.Description);
            };
            grid.Controls.Add(presetBox, 1, 0);
            tooltip.SetToolTip(presetBox, Presets.List[0].Description);

            Label fitLabel = FieldLabel("Picture");
            grid.Controls.Add(fitLabel, 2, 0);

            fitBox = new ComboBox();
            fitBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fitBox.Dock = DockStyle.Fill;
            fitBox.Margin = new Padding(0, 3, 0, 3);
            fitBox.Items.Add("Pad (black bars)");
            fitBox.Items.Add("Crop (fill frame)");
            fitBox.Items.Add("Stretch (distort)");
            fitBox.SelectedIndex = 0;
            tooltip.SetToolTip(fitBox,
                "Pad keeps the whole picture and adds black bars. Crop fills the frame but cuts edges. Stretch distorts.");
            grid.Controls.Add(fitBox, 3, 0);

            Label outputLabel = FieldLabel("Save to");
            grid.Controls.Add(outputLabel, 0, 1);

            outputBox = new TextBox();
            outputBox.Dock = DockStyle.Fill;
            outputBox.Margin = new Padding(0, 4, 6, 4);
            grid.Controls.Add(outputBox, 1, 1);
            grid.SetColumnSpan(outputBox, 3);

            Button browse = new Button();
            browse.Text = "Browse...";
            browse.Dock = DockStyle.Fill;
            browse.Margin = new Padding(0, 4, 0, 4);
            browse.Click += delegate { ChooseOutputFolder(); };
            grid.Controls.Add(browse, 4, 1);

            overwriteBox = new CheckBox();
            overwriteBox.Text = "Overwrite existing .avi files";
            overwriteBox.Dock = DockStyle.Fill;
            overwriteBox.Margin = new Padding(0, 2, 0, 0);
            grid.Controls.Add(overwriteBox, 1, 2);

            Label hint = new Label();
            hint.Text = "Leave empty to save next to each source file.";
            hint.ForeColor = HintColor;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Dock = DockStyle.Fill;
            grid.Controls.Add(hint, 2, 2);
            grid.SetColumnSpan(hint, 3);

            EnableDrop(group);
            return group;
        }

        private static Label FieldLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            return label;
        }

        private Control BuildProgress()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 6);

            TableLayoutPanel statusRow = new TableLayoutPanel();
            statusRow.Dock = DockStyle.Top;
            statusRow.Height = 20;
            statusRow.ColumnCount = 2;
            statusRow.RowCount = 1;
            statusRow.Margin = new Padding(0);
            statusRow.Padding = new Padding(0);
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            statusRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            statusLabel = new Label();
            statusLabel.Text = "Ready.";
            statusLabel.AutoEllipsis = true;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Margin = new Padding(0);
            statusRow.Controls.Add(statusLabel, 0, 0);

            doneLabel = new Label();
            doneLabel.Text = "";
            doneLabel.Visible = false;
            doneLabel.Dock = DockStyle.Fill;
            doneLabel.TextAlign = ContentAlignment.MiddleRight;
            doneLabel.Font = new Font(Font, FontStyle.Bold);
            doneLabel.ForeColor = Color.FromArgb(30, 140, 80);
            doneLabel.Margin = new Padding(0);
            statusRow.Controls.Add(doneLabel, 1, 0);

            panel.Controls.Add(statusRow);

            progress = new ProgressBar();
            progress.Dock = DockStyle.Bottom;
            progress.Height = 16;
            progress.Minimum = 0;
            progress.Maximum = 100;
            panel.Controls.Add(progress);
            return panel;
        }

        private Control BuildLog()
        {
            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.BackColor = Color.White;
            logBox.Font = new Font("Consolas", 8.5F);
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.HideSelection = false;
            logBox.Margin = new Padding(0, 0, 0, 8);
            return logBox;
        }

        private Control BuildActions()
        {
            actionPanel = new Panel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.Margin = new Padding(0);

            convertButton = new Button();
            convertButton.Text = "Convert to AVI";
            convertButton.Width = 150;
            convertButton.Height = 38;
            convertButton.Font = new Font("Segoe UI Semibold", 10F);
            convertButton.BackColor = AccentColor;
            convertButton.ForeColor = Color.White;
            convertButton.FlatStyle = FlatStyle.Flat;
            convertButton.FlatAppearance.BorderSize = 0;
            convertButton.Click += delegate { StartConversion(); };
            actionPanel.Controls.Add(convertButton);

            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Width = 90;
            cancelButton.Height = 38;
            cancelButton.Enabled = false;
            cancelButton.Click += delegate
            {
                if (converting)
                {
                    cancelRequested = true;
                    cancelButton.Enabled = false;
                    statusLabel.Text = "Cancelling...";
                }
            };
            actionPanel.Controls.Add(cancelButton);

            openButton = new Button();
            openButton.Text = "Open output folder";
            openButton.Width = 140;
            openButton.Height = 38;
            openButton.Click += delegate { OpenOutputFolder(); };
            actionPanel.Controls.Add(openButton);

            presetInfoButton = new Button();
            presetInfoButton.Text = "Preset info";
            presetInfoButton.Width = 95;
            presetInfoButton.Height = 38;
            presetInfoButton.Click += delegate { ShowPresetInfo(); };
            actionPanel.Controls.Add(presetInfoButton);

            aboutButton = new Button();
            aboutButton.Text = "About";
            aboutButton.Width = 68;
            aboutButton.Height = 38;
            aboutButton.Click += delegate { ShowAbout(); };
            actionPanel.Controls.Add(aboutButton);

            installButton = new Button();
            installButton.Text = "Install app...";
            installButton.Width = 112;
            installButton.Height = 38;
            installButton.Click += delegate { InstallApp(); };
            actionPanel.Controls.Add(installButton);

            actionPanel.Resize += delegate { LayoutActions(); };
            return actionPanel;
        }

        private Control BuildFooter()
        {
            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 2;
            footer.RowCount = 1;
            footer.Margin = new Padding(0, 2, 0, 0);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            Label brand = new Label();
            brand.Text = AppInfo.Tagline;
            brand.Dock = DockStyle.Fill;
            brand.TextAlign = ContentAlignment.MiddleLeft;
            brand.ForeColor = BrandColor;
            brand.Font = new Font("Segoe UI Semibold", 8.5F);
            footer.Controls.Add(brand, 0, 0);

            Label credit = new Label();
            credit.Text = AppInfo.Credit + "  |  v" + AppInfo.Version;
            credit.Dock = DockStyle.Fill;
            credit.TextAlign = ContentAlignment.MiddleRight;
            credit.ForeColor = Color.FromArgb(120, 126, 136);
            credit.Font = new Font("Segoe UI", 8.5F);
            footer.Controls.Add(credit, 1, 0);

            EnableDrop(footer);
            return footer;
        }

        private void LayoutActions()
        {
            int width = actionPanel.ClientSize.Width;
            convertButton.Location = new Point(width - convertButton.Width, 0);
            cancelButton.Location = new Point(width - convertButton.Width - 8 - cancelButton.Width, 0);
            openButton.Location = new Point(
                width - convertButton.Width - 8 - cancelButton.Width - 8 - openButton.Width, 0);

            presetInfoButton.Location = new Point(0, 0);
            aboutButton.Location = new Point(presetInfoButton.Width + 8, 0);
            installButton.Location = new Point(presetInfoButton.Width + 8 + aboutButton.Width + 8, 0);
        }

        private void AdjustColumns()
        {
            if (list == null) { return; }
            int width = list.ClientSize.Width - 90 - 80 - 190 - 24;
            if (width > 180) { list.Columns[0].Width = width; }
        }

        private void EnableDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += OnDragEnter;
            control.DragDrop += OnDragDrop;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!converting && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null) { AddFiles(paths); }
        }

        // -------------------------------------------------------------------
        // Files
        // -------------------------------------------------------------------
        private static readonly string[] Extensions = new string[]
        {
            ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg",
            ".ts", ".m2ts", ".mts", ".3gp", ".3g2", ".ogv", ".rm", ".rmvb", ".asf", ".vob",
            ".divx", ".xvid", ".f4v", ".mxf", ".dv"
        };

        private static List<string> ResolveFiles(string[] paths)
        {
            List<string> found = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                string full = path;
                try { full = Path.GetFullPath(path); }
                catch { }

                if (Directory.Exists(full))
                {
                    string[] files;
                    try { files = Directory.GetFiles(full, "*", SearchOption.AllDirectories); }
                    catch { continue; }

                    foreach (string file in files)
                    {
                        if (Array.IndexOf(Extensions, Path.GetExtension(file).ToLowerInvariant()) >= 0 &&
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
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        private void AddFiles(string[] paths)
        {
            if (converting || paths == null || paths.Length == 0) { return; }

            List<string> files = ResolveFiles(paths);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "No supported video files were found.", AppInfo.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Cursor = Cursors.WaitCursor;
            int added = 0;
            int unreadable = 0;

            try
            {
                foreach (string file in files)
                {
                    if (knownPaths.Contains(file)) { continue; }
                    knownPaths.Add(file);

                    MediaInfo info = null;
                    try { info = MediaInfo.Read(file); }
                    catch { }

                    ListViewItem item = new ListViewItem(Path.GetFileName(file));
                    item.SubItems.Add(info != null && info.SizeBytes > 0 ? Presets.FormatSize(info.SizeBytes) : "-");
                    item.SubItems.Add(info != null && info.Duration > 0 ? Presets.FormatDuration(info.Duration) : "-");

                    if (info == null || !info.HasVideo)
                    {
                        item.SubItems.Add("No video stream");
                        item.ForeColor = Color.Gray;
                        item.Checked = false;
                        unreadable++;
                    }
                    else
                    {
                        item.SubItems.Add("Ready");
                        item.Checked = true;
                    }

                    item.Tag = file;
                    item.ToolTipText = file;
                    list.Items.Add(item);
                    added++;

                    if ((added % 8) == 0)
                    {
                        statusLabel.Text = "Scanning... " + added + " file(s)";
                        Application.DoEvents();
                    }
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            statusLabel.Text = list.Items.Count > 0
                ? list.Items.Count + " file(s) in the list."
                : "Ready.";

            if (added > 0)
            {
                ClearDoneIndicator();
                Log("Added " + added + " file(s).", Color.DarkGreen);
                if (unreadable > 0)
                {
                    Log(unreadable + " file(s) have no readable video stream and were unchecked.", Color.DarkOrange);
                }
            }
            else if (files.Count > 0)
            {
                Log("Those files are already in the list.", Color.DimGray);
            }
        }

        private void ChooseFiles()
        {
            if (converting) { return; }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Choose video files";
            dialog.Multiselect = true;
            dialog.Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts;*.mts;*.3gp;*.ogv;*.vob;*.divx|All files|*.*";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void ChooseFolder()
        {
            if (converting) { return; }

            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Choose a folder with video files (sub-folders are included)";
            dialog.ShowNewFolderButton = false;
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                AddFiles(new string[] { dialog.SelectedPath });
            }
        }

        private void RemoveSelected()
        {
            if (converting) { return; }

            bool removedAny = false;
            foreach (ListViewItem item in new List<ListViewItem>(SelectedItems()))
            {
                knownPaths.Remove((string)item.Tag);
                list.Items.Remove(item);
                removedAny = true;
            }
            if (removedAny) { ClearDoneIndicator(); }
            statusLabel.Text = list.Items.Count > 0 ? list.Items.Count + " file(s) in the list." : "Ready.";
        }

        private List<ListViewItem> SelectedItems()
        {
            List<ListViewItem> items = new List<ListViewItem>();
            foreach (ListViewItem item in list.SelectedItems) { items.Add(item); }
            return items;
        }

        private List<ListViewItem> CheckedItems()
        {
            List<ListViewItem> items = new List<ListViewItem>();
            foreach (ListViewItem item in list.Items)
            {
                if (item.Checked) { items.Add(item); }
            }
            return items;
        }

        private void ClearList()
        {
            if (converting) { return; }
            list.Items.Clear();
            knownPaths.Clear();
            ClearDoneIndicator();
            statusLabel.Text = "Ready.";
        }

        private void ChooseOutputFolder()
        {
            if (converting) { return; }

            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Choose where the .avi files should be saved";
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                outputBox.Text = dialog.SelectedPath;
            }
        }

        private void OpenOutputFolder()
        {
            string folder = null;
            if (!string.IsNullOrEmpty(lastOutput)) { folder = Path.GetDirectoryName(lastOutput); }
            if (string.IsNullOrEmpty(folder) && outputBox.Text.Trim().Length > 0)
            {
                folder = outputBox.Text.Trim();
            }
            if (string.IsNullOrEmpty(folder))
            {
                List<ListViewItem> items = CheckedItems();
                if (items.Count > 0) { folder = Path.GetDirectoryName((string)items[0].Tag); }
            }

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                try { Process.Start("explorer.exe", "\"" + folder + "\""); }
                catch { }
            }
            else
            {
                MessageBox.Show(this, "Choose an output folder first, or add a video.", AppInfo.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // -------------------------------------------------------------------
        // Conversion
        // -------------------------------------------------------------------
        private void StartConversion()
        {
            if (converting) { return; }

            List<ListViewItem> items = CheckedItems();
            if (items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one video file and tick it.", "Nothing to convert",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(AppInfo.FfmpegPath) || string.IsNullOrEmpty(AppInfo.FfprobePath))
            {
                MessageBox.Show(this,
                    "FFmpeg was not found.\r\n\r\nPlace ffmpeg.exe and ffprobe.exe in the bin folder next to the app.",
                    "FFmpeg missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string outputDirectory = outputBox.Text.Trim();
            if (outputDirectory.Length > 0)
            {
                try
                {
                    outputDirectory = Path.GetFullPath(outputDirectory);
                    if (!Directory.Exists(outputDirectory)) { Directory.CreateDirectory(outputDirectory); }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Cannot use the output folder:\r\n" + outputDirectory +
                        "\r\n\r\n" + ex.Message, "Invalid folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                outputDirectory = null;
            }

            Preset preset = Presets.List[presetBox.SelectedIndex];
            string fit = Presets.FitName(fitBox.SelectedIndex);
            bool overwrite = overwriteBox.Checked;

            cancelRequested = false;
            closeWhenDone = false;
            converting = true;
            SetBusy(true);
            KeepAwake(true);
            progress.Value = 0;
            ClearDoneIndicator();

            Log("", Color.Black);
            Log("--- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  |  preset " + preset.Key +
                " (" + preset.Width + "x" + preset.Height + ")  |  fit " + fit + " ---", Color.DarkSlateGray);

            worker = new Thread(delegate()
            {
                RunConversions(items, outputDirectory, preset, fit, overwrite);
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunConversions(List<ListViewItem> items, string outputDirectory,
                                    Preset preset, string fit, bool overwrite)
        {
            int ok = 0;
            int failed = 0;
            int skipped = 0;
            int cancelled = 0;
            HashSet<string> plannedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stopwatch total = Stopwatch.StartNew();

            try
            {
                for (int index = 0; index < items.Count; index++)
                {
                    if (cancelRequested) { break; }

                    ListViewItem item = items[index];
                    string file = (string)item.Tag;
                    string name = Path.GetFileName(file);
                    int myIndex = index + 1;

                    Ui(delegate
                    {
                        item.SubItems[3].Text = "Converting...";
                        statusLabel.Text = "(" + myIndex + "/" + items.Count + ") " + name;
                        progress.Value = 0;
                    });

                    string output = Converter.UniqueOutputPath(
                        Converter.OutputPath(file, outputDirectory, ""), plannedOutputs);
                    LogUi("[" + myIndex + "/" + items.Count + "] " + file, Color.Black);

                    if (File.Exists(output) && !overwrite)
                    {
                        Ui(delegate { item.SubItems[3].Text = "Exists - skipped"; });
                        LogUi("        skipped: output already exists (tick \"Overwrite\")", Color.DarkOrange);
                        skipped++;
                        continue;
                    }

                    ConvertOptions options = new ConvertOptions();
                    options.Preset = preset.Key;
                    options.Fit = fit;
                    options.Force = overwrite || File.Exists(output);
                    options.DryRun = false;

                    Action<ProgressInfo> progressHandler = delegate(ProgressInfo pi)
                    {
                        Ui(delegate
                        {
                            progress.Value = Math.Min(100, Math.Max(0, pi.Percent));
                            string speed = string.IsNullOrEmpty(pi.Speed) ? "" : "  " + pi.Speed;
                            statusLabel.Text = "(" + myIndex + "/" + items.Count + ") " + name +
                                "  -  " + pi.Percent + "%" + speed;
                        });
                    };

                    ConvertResult result = Converter.Convert(file, output, options, progressHandler,
                        delegate { return cancelRequested; }, null);

                    if (result.Cancelled)
                    {
                        Ui(delegate
                        {
                            item.SubItems[3].Text = "Cancelled";
                            statusLabel.Text = "Cancelled.";
                        });
                        LogUi("        cancelled.", Color.DarkOrange);
                        cancelled++;
                        break;
                    }

                    if (result.Skipped)
                    {
                        Ui(delegate { item.SubItems[3].Text = "Exists - skipped"; });
                        LogUi("        skipped: " + result.Message, Color.DarkOrange);
                        skipped++;
                        continue;
                    }

                    if (!result.Success)
                    {
                        Ui(delegate
                        {
                            item.SubItems[3].Text = "Failed";
                            item.ForeColor = Color.DarkRed;
                        });
                        LogUi("        FAILED: " + result.Message, Color.Red);
                        failed++;
                        continue;
                    }

                    lastOutput = output;
                    Ui(delegate
                    {
                        item.SubItems[3].Text = "\u2713 Done";
                        item.ForeColor = Color.DarkGreen;
                    });
                    LogUi(string.Format(CultureInfo.CurrentCulture,
                        "        OK  {0}  ({1:0.0}s)", Presets.FormatSize(result.SizeBytes), result.Seconds),
                        Color.DarkGreen);

                    if (!string.IsNullOrEmpty(result.Warning))
                    {
                        LogUi("        WARNING: " + result.Warning, Color.DarkOrange);
                    }
                    ok++;
                }
            }
            catch (Exception ex)
            {
                LogUi("ERROR: " + ex.Message, Color.Red);
                failed++;
            }
            finally
            {
                total.Stop();
                double seconds = total.Elapsed.TotalSeconds;
                string summary = "Finished: " + ok + " ok, " + failed + " failed, " +
                    skipped + " skipped" +
                    (cancelled > 0 ? ", " + cancelled + " cancelled" : "") + ".";

                Ui(delegate
                {
                    converting = false;
                    SetBusy(false);
                    KeepAwake(false);
                    progress.Value = cancelRequested ? 0 : 100;
                    statusLabel.Text = summary;
                    if (!closeWhenDone) { ShowCompletionFeedback(ok, failed, cancelled, summary); }
                    Log("", Color.Black);
                    Log(summary + "  Time " + Presets.FormatDuration(seconds), Color.DarkSlateGray);
                    UpdateInstallButton();

                    if (closeWhenDone) { Close(); }
                });
            }
        }

        private void Ui(Action action)
        {
            if (IsDisposed || !IsHandleCreated) { return; }
            try
            {
                if (InvokeRequired) { BeginInvoke(action); }
                else { action(); }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void SetBusy(bool busy)
        {
            convertButton.Enabled = !busy;
            cancelButton.Enabled = busy;
            presetInfoButton.Enabled = !busy;
            aboutButton.Enabled = !busy;
            installButton.Enabled = !busy && !Installer.IsInstalledCopy;
            presetBox.Enabled = !busy;
            fitBox.Enabled = !busy;
            outputBox.Enabled = !busy;
            overwriteBox.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void ClearDoneIndicator()
        {
            if (doneLabel == null) { return; }
            doneLabel.Visible = false;
            doneLabel.Text = "";
        }

        private void ShowCompletionFeedback(int ok, int failed, int cancelled, string summary)
        {
            if (doneLabel == null) { return; }

            if (failed > 0)
            {
                doneLabel.Text = "! Finished with errors";
                doneLabel.ForeColor = Color.FromArgb(190, 55, 55);
            }
            else if (cancelled > 0)
            {
                doneLabel.Text = "Cancelled";
                doneLabel.ForeColor = Color.FromArgb(200, 120, 0);
            }
            else if (ok > 0)
            {
                doneLabel.Text = "\u2713 Complete";
                doneLabel.ForeColor = Color.FromArgb(30, 140, 80);
            }
            else
            {
                doneLabel.Text = "Finished";
                doneLabel.ForeColor = MutedColor;
            }

            doneLabel.Visible = true;

            // If the window is not in front, flash the taskbar button and show a
            // small tray balloon so the user notices that the batch has finished.
            bool inBackground = !ContainsFocus || WindowState == FormWindowState.Minimized;
            if (inBackground && (ok > 0 || failed > 0))
            {
                FlashTaskbar();
                ShowTrayNotification(
                    failed > 0 ? "Finished with errors" : "Conversion complete",
                    summary,
                    failed > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
        }

        private void ShowTrayNotification(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (notifyIcon == null)
                {
                    notifyIcon = new NotifyIcon();
                    notifyIcon.Icon = Icon != null ? Icon : SystemIcons.Application;
                    notifyIcon.Text = AppInfo.Name;
                }

                notifyIcon.BalloonTipTitle = title;
                notifyIcon.BalloonTipText = text;
                notifyIcon.BalloonTipIcon = icon;
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(6000);

                if (notifyTimer == null)
                {
                    notifyTimer = new System.Windows.Forms.Timer();
                    notifyTimer.Interval = 8000;
                    notifyTimer.Tick += delegate
                    {
                        notifyTimer.Stop();
                        if (notifyIcon != null) { notifyIcon.Visible = false; }
                    };
                }
                notifyTimer.Stop();
                notifyTimer.Start();
            }
            catch { }
        }

        private void FlashTaskbar()
        {
            try
            {
                FLASHWINFO info = new FLASHWINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO));
                info.hwnd = Handle;
                info.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                info.uCount = 3;
                info.dwTimeout = 0;
                FlashWindowEx(ref info);
            }
            catch { }
        }

        private static void KeepAwake(bool on)
        {
            try
            {
                SetThreadExecutionState(on ? (ES_CONTINUOUS | ES_SYSTEM_REQUIRED) : ES_CONTINUOUS);
            }
            catch { }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!converting) { return; }

            DialogResult answer = MessageBox.Show(this,
                "A conversion is running.\r\n\r\nStop it and exit?", AppInfo.Name,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            cancelRequested = true;
            closeWhenDone = true;
            e.Cancel = true;
            SetBusy(true);
            statusLabel.Text = "Cancelling...";
        }

        // -------------------------------------------------------------------
        // Log / dialogs
        // -------------------------------------------------------------------
        private void Log(string text, Color color)
        {
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = color;
            logBox.AppendText(text + "\r\n");
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
        }

        private void LogUi(string text, Color color)
        {
            Ui(delegate { Log(text, color); });
        }

        private void ShowPresetInfo()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Presets (all output is AVI with MP3 stereo audio):");
            text.AppendLine();
            foreach (Preset p in Presets.List)
            {
                text.AppendLine(p.Key.ToUpperInvariant() + "  -  " + p.Label);
                text.AppendLine("   " + p.Description);
                text.AppendLine();
            }
            text.AppendLine("If the projector says \"unsupported format\", try the FALLBACK preset (MJPEG).");
            text.AppendLine("Old players are happiest with 480p or lower and MP3 audio.");
            text.AppendLine();
            text.AppendLine(AppInfo.Name + "  v" + AppInfo.Version);
            text.AppendLine(AppInfo.Tagline + "  |  " + AppInfo.Credit);

            MessageBox.Show(this, text.ToString(), "Preset information",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowAbout()
        {
            using (AboutForm form = new AboutForm())
            {
                form.ShowDialog(this);
            }
        }

        private void UpdateInstallButton()
        {
            if (Installer.IsInstalledCopy)
            {
                installButton.Text = "Installed";
                installButton.Enabled = false;
                tooltip.SetToolTip(installButton,
                    "This is the installed copy. Uninstall it from Settings > Apps, or from the Fashion Flow folder in the Start Menu.");
                return;
            }

            if (Installer.IsInstalled)
            {
                string version = Installer.InstalledVersion;
                if (string.IsNullOrEmpty(version)) { version = AppInfo.Version; }
                installButton.Text = "Reinstall app...";
                installButton.Enabled = true;
                tooltip.SetToolTip(installButton,
                    "Version " + version + " is installed in " + AppInfo.InstallDir +
                    ". Click to update or reinstall.");
            }
            else
            {
                installButton.Text = "Install app...";
                installButton.Enabled = true;
                tooltip.SetToolTip(installButton,
                    "Installs the app for this user with Start Menu and Desktop shortcuts. No administrator rights needed.");
            }
        }

        private void InstallApp()
        {
            if (Installer.IsInstalledCopy)
            {
                MessageBox.Show(this,
                    "This is already the installed copy.\r\n\r\nTo remove it, use Settings > Apps > " +
                    AppInfo.FullName + ", or the uninstall shortcut in the Fashion Flow Start Menu folder.",
                    "Already installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            InstallResult result = null;
            Cursor previousCursor = Cursor;
            string previousStatus = statusLabel.Text;

            try
            {
                Cursor = Cursors.WaitCursor;
                statusLabel.Text = "Installing app... copying FFmpeg and creating shortcuts.";
                statusLabel.Refresh();
                Application.DoEvents();

                result = Installer.Install(new InstallOptions(), null);
            }
            catch (Exception ex)
            {
                result = new InstallResult();
                result.Message = ex.Message;
            }
            finally
            {
                Cursor = previousCursor;
                statusLabel.Text = previousStatus;
            }

            UpdateInstallButton();

            if (result != null && result.Success)
            {
                MessageBox.Show(this,
                    "Installed to:\r\n" + result.InstallDir +
                    "\r\n\r\nStart Menu > Fashion Flow > " + AppInfo.ShortcutName +
                    "\r\nA Desktop shortcut was created too." +
                    "\r\n\r\nTo uninstall later: Settings > Apps > " + AppInfo.FullName + ".",
                    "Installation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string message = result != null && !string.IsNullOrEmpty(result.Message)
                    ? result.Message
                    : "Unknown error.";
                MessageBox.Show(this,
                    "The installation did not finish:\r\n\r\n" + message +
                    "\r\n\r\nIf the app is open, close it and try again.",
                    "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    internal sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About " + AppInfo.Name;
            Size = new Size(470, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            Icon icon = Art.LoadAppIcon();
            if (icon != null) { Icon = icon; }

            Image logo = Art.LoadLogo();
            if (logo != null)
            {
                PictureBox picture = new PictureBox();
                picture.Image = logo;
                picture.SizeMode = PictureBoxSizeMode.Zoom;
                picture.Location = new Point(22, 20);
                picture.Size = new Size(84, 84);
                Controls.Add(picture);
            }

            Label title = new Label();
            title.Text = AppInfo.FullName;
            title.Font = new Font("Segoe UI Semibold", 13F);
            title.AutoSize = true;
            title.Location = new Point(122, 24);
            Controls.Add(title);

            Label version = new Label();
            version.Text = "Version " + AppInfo.Version;
            version.ForeColor = Color.FromArgb(96, 102, 112);
            version.AutoSize = true;
            version.Location = new Point(124, 52);
            Controls.Add(version);

            Label description = new Label();
            description.Text = AppInfo.Description + "\r\nMPEG-4 (Xvid) or MJPEG video, MP3 or PCM audio.";
            description.AutoSize = false;
            description.Location = new Point(24, 118);
            description.Size = new Size(410, 40);
            Controls.Add(description);

            Label tagline = new Label();
            tagline.Text = AppInfo.Tagline;
            tagline.ForeColor = Color.FromArgb(122, 90, 166);
            tagline.Font = new Font("Segoe UI Semibold", 9.5F);
            tagline.AutoSize = true;
            tagline.Location = new Point(24, 166);
            Controls.Add(tagline);

            Label credit = new Label();
            credit.Text = AppInfo.Credit + "\r\n" + AppInfo.Copyright;
            credit.AutoSize = true;
            credit.Location = new Point(24, 188);
            Controls.Add(credit);

            Label state = new Label();
            string installState;
            if (Installer.IsInstalledCopy) { installState = "Installed copy"; }
            else if (Installer.IsInstalled) { installState = "Installed in " + AppInfo.InstallDir; }
            else { installState = "Portable - not installed"; }
            state.Text = "Install state: " + installState;
            state.AutoSize = true;
            state.Location = new Point(24, 232);
            Controls.Add(state);

            Label ffmpeg = new Label();
            ffmpeg.Text = "FFmpeg: " + FfmpegVersion();
            ffmpeg.AutoSize = true;
            ffmpeg.Location = new Point(24, 254);
            Controls.Add(ffmpeg);

            Button ok = new Button();
            ok.Text = "OK";
            ok.Size = new Size(84, 30);
            ok.Location = new Point(348, 296);
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);

            AcceptButton = ok;
            CancelButton = ok;
        }

        private static string FfmpegVersion()
        {
            try
            {
                string exe = AppInfo.FfmpegPath;
                if (string.IsNullOrEmpty(exe)) { return "not found"; }

                ProcessStartInfo psi = new ProcessStartInfo(exe, "-hide_banner -version");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;

                using (Process process = Process.Start(psi))
                {
                    string line = process.StandardOutput.ReadLine();
                    process.WaitForExit();
                    return string.IsNullOrEmpty(line) ? "unknown" : line.Trim();
                }
            }
            catch { return "not found"; }
        }
    }
}

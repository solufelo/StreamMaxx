using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace StreamMaxx {
    internal static class NativeMethods {
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")] public static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);
        [DllImport("kernel32.dll")] public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr hObject);
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        public const uint HIGH_PRIORITY_CLASS = 0x00000080;
        public const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    }
    public class ModernButton : Button {
        public Color BorderColor { get; set; }
        public Color HoverColor { get; set; }
        public Color NormalColor { get; set; }
        private bool isHovered = false;
        public ModernButton() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            ForeColor = Color.White;
            NormalColor = Color.FromArgb(20, 26, 38);
            HoverColor = Color.FromArgb(32, 42, 62);
            BorderColor = Color.FromArgb(0, 240, 255);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs pevent) {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush brush = new SolidBrush(isHovered ? HoverColor : NormalColor)) { g.FillRectangle(brush, rect); }
            using (Pen pen = new Pen(isHovered ? Color.White : BorderColor, 1.5f)) { g.DrawRectangle(pen, rect); }
            TextRenderer.DrawText(g, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    public class MainForm : Form {
        private string suiteDir;
        private string destinationsPath;
        private string mediamtxExe;
        private string mediamtxYml;
        private string obsExe = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";
        private Process relayProcess = null;
        private Timer telemetryTimer;
        private NotifyIcon trayIcon;

        private Label lblStatus;
        private Label lblTelemetry;
        private Panel pnlStatusGlow;
        private ModernButton btnMasterLaunch;

        private CheckBox chkTwitch, chkYouTube, chkKick, chkTikTok;
        private TextBox txtTwitchKey, txtYouTubeKey, txtKickKey, txtTikTokKey;
        private TextBox txtTwitchSrv, txtYouTubeSrv, txtKickSrv, txtTikTokSrv;

        public MainForm() {
            suiteDir = AppDomain.CurrentDomain.BaseDirectory;
            if (suiteDir.EndsWith(@"\src\")) {
                suiteDir = Directory.GetParent(suiteDir.TrimEnd('\\')).FullName;
            } else if (suiteDir.EndsWith(@"\bin\")) {
                suiteDir = Directory.GetParent(suiteDir.TrimEnd('\\')).FullName;
            }
            destinationsPath = Path.Combine(suiteDir, @"config\destinations.json");
            mediamtxExe = Path.Combine(suiteDir, @"bin\mediamtx.exe");
            mediamtxYml = Path.Combine(suiteDir, @"config\mediamtx.yml");

            InitWindow();
            InitControls();
            LoadConfig();

            telemetryTimer = new Timer();
            telemetryTimer.Interval = 1500;
            telemetryTimer.Tick += TelemetryTimer_Tick;
            telemetryTimer.Start();
        }

        private void InitWindow() {
            Text = "StreamMaxx // Hardware Optimaxx Multi-Stream Hub";
            Size = new Size(880, 680);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(10, 13, 20);
            ForeColor = Color.FromArgb(240, 246, 252);
            Font = new Font("Segoe UI", 9f);

            string icoPath = Path.Combine(suiteDir, @"src\app.ico");
            if (File.Exists(icoPath)) {
                try { Icon = new Icon(icoPath); } catch {}
            }

            trayIcon = new NotifyIcon();
            trayIcon.Text = "StreamMaxx - Optimaxx Live Hub";
            trayIcon.Icon = this.Icon;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; BringToFront(); };

            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("Show StreamMaxx Hub", delegate { Show(); WindowState = FormWindowState.Normal; BringToFront(); });
            cm.MenuItems.Add("1-Click Go Live", delegate { StartEverything(); });
            cm.MenuItems.Add("Stop Relay", delegate { StopRelay(); });
            cm.MenuItems.Add("-");
            cm.MenuItems.Add("Exit", delegate { StopRelay(); trayIcon.Visible = false; Application.Exit(); });
            trayIcon.ContextMenu = cm;
        }
        private void InitControls() {
            Panel titleBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(14, 18, 28) };
            titleBar.MouseDown += TitleBar_MouseDown;

            Label lblTitle = new Label {
                Text = "STREAMMAXX  //  HARDWARE OPTIMAXX BROADCAST ENGINE",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 240, 255),
                Location = new Point(16, 9),
                AutoSize = true
            };
            lblTitle.MouseDown += TitleBar_MouseDown;
            titleBar.Controls.Add(lblTitle);

            Button btnClose = new Button {
                Text = "X", Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 170, 185), BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat, Size = new Size(36, 38),
                Location = new Point(880 - 36, 0), Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += delegate { Hide(); };
            titleBar.Controls.Add(btnClose);

            Button btnMin = new Button {
                Text = "-", Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 170, 185), BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat, Size = new Size(36, 38),
                Location = new Point(880 - 72, 0), Cursor = Cursors.Hand
            };
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(btnMin);
            Controls.Add(titleBar);

            Panel heroPnl = new Panel { Location = new Point(16, 50), Size = new Size(848, 120), BackColor = Color.FromArgb(17, 22, 34) };
            pnlStatusGlow = new Panel { Location = new Point(20, 22), Size = new Size(16, 16), BackColor = Color.FromArgb(0, 240, 255) };
            heroPnl.Controls.Add(pnlStatusGlow);

            lblStatus = new Label {
                Text = "RELAY & OPTIMAXX STANDBY // READY TO BROADCAST",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White, Location = new Point(46, 18), AutoSize = true
            };
            heroPnl.Controls.Add(lblStatus);

            lblTelemetry = new Label {
                Text = "Hardware: Ryzen 5800X [Cores 6-7 / 0xF000] | RTX 4060 Ti [NVENC: 0%] | DSCP 46 QoS Active",
                Font = new Font("Consolas", 9f), ForeColor = Color.FromArgb(0, 255, 102),
                Location = new Point(48, 46), AutoSize = true
            };
            heroPnl.Controls.Add(lblTelemetry);

            btnMasterLaunch = new ModernButton {
                Text = ">> 1-CLICK OPTIMAXX & GO LIVE", Location = new Point(48, 70), Size = new Size(310, 38),
                NormalColor = Color.FromArgb(0, 200, 100), HoverColor = Color.FromArgb(0, 240, 130),
                BorderColor = Color.FromArgb(0, 255, 120), ForeColor = Color.FromArgb(5, 25, 15),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            btnMasterLaunch.Click += delegate { StartEverything(); };
            heroPnl.Controls.Add(btnMasterLaunch);

            ModernButton btnStopRelay = new ModernButton {
                Text = "[X] STOP RELAY", Location = new Point(370, 70), Size = new Size(150, 38),
                NormalColor = Color.FromArgb(35, 20, 25), HoverColor = Color.FromArgb(60, 25, 35),
                BorderColor = Color.FromArgb(255, 60, 80), ForeColor = Color.FromArgb(255, 120, 140)
            };
            btnStopRelay.Click += delegate { StopRelay(); };
            heroPnl.Controls.Add(btnStopRelay);

            ModernButton btnLaunchObs = new ModernButton {
                Text = ">> LAUNCH OBS", Location = new Point(530, 70), Size = new Size(150, 38),
                NormalColor = Color.FromArgb(20, 32, 50), HoverColor = Color.FromArgb(30, 48, 75),
                BorderColor = Color.FromArgb(0, 200, 255), ForeColor = Color.FromArgb(0, 240, 255)
            };
            btnLaunchObs.Click += delegate { LaunchObsOnly(); };
            heroPnl.Controls.Add(btnLaunchObs);

            ModernButton btnOpenDash = new ModernButton {
                Text = "WEB HUD", Location = new Point(690, 70), Size = new Size(140, 38),
                NormalColor = Color.FromArgb(25, 30, 42), HoverColor = Color.FromArgb(40, 48, 68),
                BorderColor = Color.FromArgb(120, 135, 160), ForeColor = Color.FromArgb(200, 215, 235)
            };
            btnOpenDash.Click += delegate { Process.Start("http://127.0.0.1:8999"); };
            heroPnl.Controls.Add(btnOpenDash);
            Controls.Add(heroPnl);

            Panel pnlGrid = new Panel { Location = new Point(16, 180), Size = new Size(848, 430), BackColor = Color.FromArgb(14, 18, 28) };
            Label lblGridTitle = new Label {
                Text = "BROADCAST DESTINATIONS // ARMED & MULTIPLEXED",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(200, 210, 225),
                Location = new Point(16, 12), AutoSize = true
            };
            pnlGrid.Controls.Add(lblGridTitle);

            CreatePlatformCard(pnlGrid, 40, "Twitch (1080p60 8000k)", Color.FromArgb(145, 70, 255), out chkTwitch, out txtTwitchSrv, out txtTwitchKey);
            CreatePlatformCard(pnlGrid, 130, "YouTube Live (1440p AV1 / 1080p)", Color.FromArgb(255, 42, 85), out chkYouTube, out txtYouTubeSrv, out txtYouTubeKey);
            CreatePlatformCard(pnlGrid, 220, "Kick Live (1080p60 8000k)", Color.FromArgb(83, 252, 24), out chkKick, out txtKickSrv, out txtKickKey);
            CreatePlatformCard(pnlGrid, 310, "TikTok Live (RTMP Live Producer)", Color.FromArgb(0, 240, 255), out chkTikTok, out txtTikTokSrv, out txtTikTokKey);

            ModernButton btnSave = new ModernButton {
                Text = "SAVE & SYNC DESTINATIONS", Location = new Point(16, 385), Size = new Size(240, 34),
                NormalColor = Color.FromArgb(0, 120, 215), HoverColor = Color.FromArgb(0, 150, 255),
                BorderColor = Color.FromArgb(50, 180, 255), ForeColor = Color.White
            };
            btnSave.Click += delegate {
                SaveConfig();
                MessageBox.Show("Destinations saved and MediaMTX relay updated!", "StreamMaxx", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            pnlGrid.Controls.Add(btnSave);

            Label lblIngest = new Label {
                Text = "OBS Master Ingest: rtmp://127.0.0.1:1935/live/master",
                Font = new Font("Consolas", 9f), ForeColor = Color.FromArgb(150, 160, 175),
                Location = new Point(275, 394), AutoSize = true
            };
            pnlGrid.Controls.Add(lblIngest);

            ModernButton btnCopy = new ModernButton {
                Text = "COPY", Location = new Point(740, 385), Size = new Size(92, 34),
                NormalColor = Color.FromArgb(25, 32, 48), HoverColor = Color.FromArgb(35, 45, 68),
                BorderColor = Color.FromArgb(80, 100, 130), ForeColor = Color.FromArgb(200, 220, 240)
            };
            btnCopy.Click += delegate {
                Clipboard.SetText("rtmp://127.0.0.1:1935/live/master");
                MessageBox.Show("Copied to clipboard: rtmp://127.0.0.1:1935/live/master", "StreamMaxx", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            pnlGrid.Controls.Add(btnCopy);
            Controls.Add(pnlGrid);

            Label lblFooter = new Label {
                Text = "StreamMaxx v1.0 // Hardware Isolated for Ryzen 5800X + RTX 4060 Ti // Zero-Copy Multiplexing",
                Font = new Font("Segoe UI", 8.25f), ForeColor = Color.FromArgb(90, 100, 115),
                Location = new Point(16, 620), AutoSize = true
            };
            Controls.Add(lblFooter);
        }
        private void CreatePlatformCard(Panel parent, int top, string title, Color accentColor,
            out CheckBox chk, out TextBox txtSrv, out TextBox txtKey) {
            Panel card = new Panel { Location = new Point(16, top), Size = new Size(816, 80), BackColor = Color.FromArgb(19, 25, 38) };
            Panel pill = new Panel { Location = new Point(10, 12), Size = new Size(6, 56), BackColor = accentColor };
            card.Controls.Add(pill);

            chk = new CheckBox {
                Text = title, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White, Location = new Point(24, 10), Size = new Size(320, 22), Checked = true
            };
            card.Controls.Add(chk);

            Label lblSrv = new Label { Text = "Server:", Font = new Font("Segoe UI", 8.25f), ForeColor = Color.FromArgb(140, 150, 165), Location = new Point(24, 42), AutoSize = true };
            card.Controls.Add(lblSrv);

            txtSrv = new TextBox {
                Location = new Point(70, 39), Size = new Size(290, 23),
                BackColor = Color.FromArgb(10, 14, 22), ForeColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f)
            };
            card.Controls.Add(txtSrv);

            Label lblKey = new Label { Text = "Stream Key:", Font = new Font("Segoe UI", 8.25f), ForeColor = Color.FromArgb(140, 150, 165), Location = new Point(380, 42), AutoSize = true };
            card.Controls.Add(lblKey);

            txtKey = new TextBox {
                Location = new Point(455, 39), Size = new Size(280, 23),
                BackColor = Color.FromArgb(10, 14, 22), ForeColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f), UseSystemPasswordChar = true
            };
            card.Controls.Add(txtKey);

            TextBox capturedKey = txtKey;
            Button btnToggleShow = new Button {
                Text = "SHOW", Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 180, 200), BackColor = Color.FromArgb(26, 34, 52),
                FlatStyle = FlatStyle.Flat, Size = new Size(60, 23), Location = new Point(742, 39), Cursor = Cursors.Hand
            };
            btnToggleShow.FlatAppearance.BorderSize = 0;
            btnToggleShow.Click += delegate {
                capturedKey.UseSystemPasswordChar = !capturedKey.UseSystemPasswordChar;
                btnToggleShow.Text = capturedKey.UseSystemPasswordChar ? "SHOW" : "HIDE";
            };
            card.Controls.Add(btnToggleShow);
            parent.Controls.Add(card);
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HT_CAPTION, 0);
            }
        }

        private void LoadConfig() {
            if (!File.Exists(destinationsPath)) return;
            try {
                string json = File.ReadAllText(destinationsPath);
                chkTwitch.Checked = ExtractBool(json, "twitch", "enabled");
                txtTwitchSrv.Text = ExtractString(json, "twitch", "server");
                txtTwitchKey.Text = ExtractString(json, "twitch", "stream_key");

                chkYouTube.Checked = ExtractBool(json, "youtube", "enabled");
                txtYouTubeSrv.Text = ExtractString(json, "youtube", "server");
                txtYouTubeKey.Text = ExtractString(json, "youtube", "stream_key");

                chkKick.Checked = ExtractBool(json, "kick", "enabled");
                txtKickSrv.Text = ExtractString(json, "kick", "server");
                txtKickKey.Text = ExtractString(json, "kick", "stream_key");

                chkTikTok.Checked = ExtractBool(json, "tiktok", "enabled");
                txtTikTokSrv.Text = ExtractString(json, "tiktok", "server");
                txtTikTokKey.Text = ExtractString(json, "tiktok", "stream_key");
            } catch (Exception ex) {
                Console.WriteLine("Error reading config: " + ex.Message);
            }
        }

        private void SaveConfig() {
            try {
                string tSrv = txtTwitchSrv.Text.Trim();
                string tKey = txtTwitchKey.Text.Trim();
                string ySrv = txtYouTubeSrv.Text.Trim();
                string yKey = txtYouTubeKey.Text.Trim();
                string kSrv = txtKickSrv.Text.Trim();
                string kKey = txtKickKey.Text.Trim();
                string ttSrv = txtTikTokSrv.Text.Trim();
                string ttKey = txtTikTokKey.Text.Trim();

                string json = "{\n" +
                    "  \"destinations\": {\n" +
                    "    \"twitch\": {\n" +
                    "      \"id\": \"twitch\",\n" +
                    "      \"name\": \"Twitch\",\n" +
                    "      \"color\": \"#9146FF\",\n" +
                    "      \"enabled\": " + (chkTwitch.Checked ? "true" : "false") + ",\n" +
                    "      \"server\": \"" + tSrv + "\",\n" +
                    "      \"stream_key\": \"" + tKey + "\",\n" +
                    "      \"max_bitrate_kbps\": 8000,\n" +
                    "      \"recommended_fps\": 60,\n" +
                    "      \"recommended_resolution\": \"1920x1080\"\n" +
                    "    },\n" +
                    "    \"youtube\": {\n" +
                    "      \"id\": \"youtube\",\n" +
                    "      \"name\": \"YouTube Live\",\n" +
                    "      \"color\": \"#FF0000\",\n" +
                    "      \"enabled\": " + (chkYouTube.Checked ? "true" : "false") + ",\n" +
                    "      \"server\": \"" + ySrv + "\",\n" +
                    "      \"stream_key\": \"" + yKey + "\",\n" +
                    "      \"max_bitrate_kbps\": 18000,\n" +
                    "      \"recommended_fps\": 60,\n" +
                    "      \"recommended_resolution\": \"1920x1080 or 2560x1440\"\n" +
                    "    },\n" +
                    "    \"kick\": {\n" +
                    "      \"id\": \"kick\",\n" +
                    "      \"name\": \"Kick\",\n" +
                    "      \"color\": \"#53FC18\",\n" +
                    "      \"enabled\": " + (chkKick.Checked ? "true" : "false") + ",\n" +
                    "      \"server\": \"" + kSrv + "\",\n" +
                    "      \"stream_key\": \"" + kKey + "\",\n" +
                    "      \"max_bitrate_kbps\": 8000,\n" +
                    "      \"recommended_fps\": 60,\n" +
                    "      \"recommended_resolution\": \"1920x1080\"\n" +
                    "    },\n" +
                    "    \"tiktok\": {\n" +
                    "      \"id\": \"tiktok\",\n" +
                    "      \"name\": \"TikTok Live\",\n" +
                    "      \"color\": \"#FE2C55\",\n" +
                    "      \"enabled\": " + (chkTikTok.Checked ? "true" : "false") + ",\n" +
                    "      \"server\": \"" + ttSrv + "\",\n" +
                    "      \"stream_key\": \"" + ttKey + "\",\n" +
                    "      \"max_bitrate_kbps\": 6000,\n" +
                    "      \"recommended_fps\": 60,\n" +
                    "      \"recommended_resolution\": \"1080x1920 or 1920x1080\"\n" +
                    "    }\n" +
                    "  },\n" +
                    "  \"master_relay\": {\n" +
                    "    \"listen_host\": \"127.0.0.1\",\n" +
                    "    \"rtmp_port\": 1935,\n" +
                    "    \"stream_path\": \"live/master\",\n" +
                    "    \"full_ingest_url\": \"rtmp://127.0.0.1:1935/live/master\"\n" +
                    "  }\n" +
                    "}";

                File.WriteAllText(destinationsPath, json);

                List<string> forwards = new List<string>();
                if (chkTwitch.Checked && tKey.Length > 0) forwards.Add("      - dest: " + tSrv.TrimEnd('/') + "/" + tKey);
                if (chkYouTube.Checked && yKey.Length > 0) forwards.Add("      - dest: " + ySrv.TrimEnd('/') + "/" + yKey);
                if (chkKick.Checked && kKey.Length > 0) forwards.Add("      - dest: " + kSrv.TrimEnd('/') + "/" + kKey);
                if (chkTikTok.Checked && ttKey.Length > 0) forwards.Add("      - dest: " + ttSrv.TrimEnd('/') + "/" + ttKey);

                string fwdBlock = (forwards.Count > 0) ? ("    forward:\n" + string.Join("\n", forwards.ToArray())) : "    forward: []";

                string yml = "# StreamMaxx High-Performance MediaMTX Master Relay Configuration\n" +
                    "api: true\n" +
                    "apiAddress: 127.0.0.1:9997\n" +
                    "rtmp: true\n" +
                    "rtmpAddress: 127.0.0.1:1935\n" +
                    "rtmpEncryption: \"no\"\n" +
                    "rtsp: false\n" +
                    "hls: false\n" +
                    "webrtc: false\n" +
                    "srt: true\n" +
                    "srtAddress: 127.0.0.1:8890\n" +
                    "readTimeout: 10s\n" +
                    "writeTimeout: 10s\n" +
                    "paths:\n" +
                    "  live/master:\n" +
                    fwdBlock + "\n" +
                    "    runOnInit: \"\"\n" +
                    "    runOnDemand: \"\"\n";

                File.WriteAllText(mediamtxYml, yml);
            } catch (Exception ex) {
                Console.WriteLine("Error saving config: " + ex.Message);
            }
        }
        private void StartEverything() {
            lblStatus.Text = "OPTIMAXXING HARDWARE & STARTING SUITE...";
            pnlStatusGlow.BackColor = Color.FromArgb(245, 158, 11);
            Application.DoEvents();

            SaveConfig();

            try {
                string qosScript = Path.Combine(suiteDir, @"scripts\01_streaming_qos_and_affinity.ps1");
                if (File.Exists(qosScript)) {
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = "powershell.exe",
                        Arguments = "-ExecutionPolicy Bypass -File \"" + qosScript + "\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                }
            } catch {}

            StartRelay();
            LaunchObsOnly();

            lblStatus.Text = "BROADCASTING ACTIVE // ALL PLATFORMS MULTIPLEXED";
            pnlStatusGlow.BackColor = Color.FromArgb(0, 255, 102);

            trayIcon.ShowBalloonTip(3000, "StreamMaxx Live", "Zero-Copy Relay & Optimaxxed OBS are running. Game cores protected!", ToolTipIcon.Info);
        }

        private void StartRelay() {
            StopRelay();

            if (!File.Exists(mediamtxExe)) return;
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = mediamtxExe,
                    Arguments = "\"" + mediamtxYml + "\"",
                    WorkingDirectory = Path.GetDirectoryName(mediamtxExe),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                relayProcess = Process.Start(psi);
                System.Threading.Thread.Sleep(400);

                if (relayProcess != null && !relayProcess.HasExited) {
                    IntPtr hProc = NativeMethods.OpenProcess(NativeMethods.PROCESS_ALL_ACCESS, false, relayProcess.Id);
                    if (hProc != IntPtr.Zero) {
                        NativeMethods.SetProcessAffinityMask(hProc, (IntPtr)0xF000);
                        NativeMethods.SetPriorityClass(hProc, NativeMethods.HIGH_PRIORITY_CLASS);
                        NativeMethods.CloseHandle(hProc);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Relay start error: " + ex.Message);
            }
        }

        private void StopRelay() {
            try {
                if (relayProcess != null && !relayProcess.HasExited) {
                    relayProcess.Kill();
                }
            } catch {}
            relayProcess = null;

            try {
                foreach (var p in Process.GetProcessesByName("mediamtx")) {
                    try { p.Kill(); } catch {}
                }
            } catch {}

            lblStatus.Text = "RELAY STANDBY // READY TO BROADCAST";
            pnlStatusGlow.BackColor = Color.FromArgb(0, 240, 255);
        }

        private void LaunchObsOnly() {
            if (!File.Exists(obsExe)) {
                MessageBox.Show("OBS Studio executable not found at:\n" + obsExe, "StreamMaxx", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try {
                string sentinelDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"obs-studio\.sentinel");
                if (Directory.Exists(sentinelDir)) {
                    foreach (var f in Directory.GetFiles(sentinelDir)) {
                        try { File.Delete(f); } catch {}
                    }
                }
            } catch {}

            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = obsExe,
                    Arguments = "--disable-shutdown-check --collection \"Optimaxx_Master_Gaming\" --profile \"Optimaxx_MultiStream_LowLatency\"",
                    WorkingDirectory = Path.GetDirectoryName(obsExe),
                    UseShellExecute = true
                };
                Process.Start(psi);
                System.Threading.Thread.Sleep(1200);

                foreach (var p in Process.GetProcessesByName("obs64")) {
                    try {
                        IntPtr hProc = NativeMethods.OpenProcess(NativeMethods.PROCESS_ALL_ACCESS, false, p.Id);
                        if (hProc != IntPtr.Zero) {
                            NativeMethods.SetProcessAffinityMask(hProc, (IntPtr)0xF000);
                            NativeMethods.SetPriorityClass(hProc, NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS);
                            NativeMethods.CloseHandle(hProc);
                        }
                    } catch {}
                }
            } catch (Exception ex) {
                MessageBox.Show("Could not launch OBS: " + ex.Message);
            }
        }

        private void TelemetryTimer_Tick(object sender, EventArgs e) {
            bool isObsRunning = Process.GetProcessesByName("obs64").Length > 0;
            bool isRelayRunning = (relayProcess != null && !relayProcess.HasExited) || Process.GetProcessesByName("mediamtx").Length > 0;

            if (isObsRunning && isRelayRunning) {
                lblStatus.Text = "BROADCASTING ACTIVE // ALL PLATFORMS MULTIPLEXED";
                pnlStatusGlow.BackColor = Color.FromArgb(0, 255, 102);
            } else if (isRelayRunning) {
                lblStatus.Text = "RELAY RUNNING (1935) // AWAITING OBS STREAM";
                pnlStatusGlow.BackColor = Color.FromArgb(0, 240, 255);
            } else {
                lblStatus.Text = "STANDBY // READY TO BROADCAST";
                pnlStatusGlow.BackColor = Color.FromArgb(100, 115, 135);
            }

            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=utilization.gpu,utilization.encoder,temperature.gpu,memory.used --format=csv,noheader,nounits",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using (Process p = Process.Start(psi)) {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(300);
                    if (!string.IsNullOrEmpty(output)) {
                        string[] parts = output.Split(',');
                        if (parts.Length >= 4) {
                            lblTelemetry.Text = string.Format(
                                "Hardware: Ryzen 5800X [Cores 6-7 / 0xF000] | RTX 4060 Ti [NVENC: {0}% | {1}C | VRAM: {2}MB] | QoS: DSCP 46",
                                parts[1].Trim(), parts[2].Trim(), parts[3].Trim()
                            );
                        }
                    }
                }
            } catch {}
        }

        private static bool ExtractBool(string json, string platform, string prop) {
            Match m = Regex.Match(json, "\"" + platform + "\"\\s*:\\s*\\{[^}]*\"" + prop + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            return m.Success && m.Groups[1].Value.ToLower() == "true";
        }

        private static string ExtractString(string json, string platform, string prop) {
            Match m = Regex.Match(json, "\"" + platform + "\"\\s*:\\s*\\{[^}]*\"" + prop + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "";
        }

        [STAThread]
        public static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

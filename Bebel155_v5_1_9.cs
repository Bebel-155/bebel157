
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BebelEquipe155
{

    static class BrandResources
    {
        public static Image Load(string resourceName)
        {
            try
            {
                Assembly a = Assembly.GetExecutingAssembly();
                using (Stream s = a.GetManifestResourceStream(resourceName))
                {
                    if (s == null) return null;
                    using (Image img = Image.FromStream(s)) return new Bitmap(img);
                }
            }
            catch { return null; }
        }
    }

    static class LocationService
    {
        static readonly object Sync = new object();
        static string cachedCity = null;

        public static string CachedCity
        {
            get
            {
                lock (Sync)
                    return string.IsNullOrWhiteSpace(cachedCity) ? "Local" : cachedCity;
            }
        }

        static string JsonValue(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            try
            {
                Match m = Regex.Match(
                    json,
                    "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                return m.Success ? Regex.Unescape(m.Groups[1].Value).Trim() : "";
            }
            catch { return ""; }
        }

        static string DownloadJson(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 4500;
            request.ReadWriteTimeout = 4500;
            request.UserAgent = "BebelEquipe155-v5.1.9";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        public static string ResolveCity()
        {
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(cachedCity))
                    return cachedCity;
            }

            string city = "";

            // IP geolocation is approximate; a VPN/proxy can change the detected city.
            try
            {
                string json = DownloadJson("https://ipapi.co/json/");
                city = JsonValue(json, "city");
            }
            catch { }

            if (string.IsNullOrWhiteSpace(city))
            {
                try
                {
                    string json = DownloadJson("https://ipwho.is/");
                    city = JsonValue(json, "city");
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(city))
                city = "Local";

            lock (Sync)
            {
                cachedCity = city;
                return cachedCity;
            }
        }

        public static string ClockText(string city)
        {
            string label = string.IsNullOrWhiteSpace(city) ? "Local" : city.Trim();
            DateTime now = DateTime.Now;
            return label + "  •  " + now.ToString("dd/MM/yyyy") + "  •  " + now.ToString("HH:mm:ss");
        }
    }

    public class SplashForm : Form
    {
        System.Windows.Forms.Timer timer;
        ProgressBar progress;
        Label status;
        int value = 0;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(980, 552);
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.0;

            PictureBox banner = new PictureBox();
            banner.Dock = DockStyle.Fill;
            banner.SizeMode = PictureBoxSizeMode.Zoom;
            banner.BackColor = Color.Black;
            banner.Image = BrandResources.Load("Bebel155.Banner");
            Controls.Add(banner);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 64;
            bottom.BackColor = Color.FromArgb(8, 11, 17);
            Controls.Add(bottom);
            bottom.BringToFront();

            status = new Label();
            status.Text = "Iniciando Bebel Equipe Do Mais Novo 155...";
            status.ForeColor = Color.FromArgb(210, 218, 232);
            status.Font = new Font("Segoe UI", 9.5F);
            status.AutoSize = true;
            status.Location = new Point(22, 12);
            bottom.Controls.Add(status);

            progress = new ProgressBar();
            progress.Minimum = 0;
            progress.Maximum = 100;
            progress.Value = 0;
            progress.Style = ProgressBarStyle.Continuous;
            progress.Location = new Point(22, 38);
            progress.Size = new Size(936, 10);
            progress.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            bottom.Resize += delegate { progress.Width = Math.Max(100, bottom.ClientSize.Width - 44); };
            bottom.Controls.Add(progress);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 35;
            timer.Tick += delegate
            {
                if (Opacity < 1.0) Opacity = Math.Min(1.0, Opacity + 0.08);
                value += 2;
                if (value > 100) value = 100;
                progress.Value = value;

                if (value < 30) status.Text = "Carregando interface e identidade B155...";
                else if (value < 60) status.Text = "Preparando diagnóstico USB, Android e iOS...";
                else if (value < 86) status.Text = "Verificando módulos e atualizações...";
                else status.Text = "Pronto.";

                if (value >= 100)
                {
                    timer.Stop();
                    System.Windows.Forms.Timer closeTimer = new System.Windows.Forms.Timer();
                    closeTimer.Interval = 260;
                    closeTimer.Tick += delegate
                    {
                        closeTimer.Stop();
                        Close();
                    };
                    closeTimer.Start();
                }
            };
            Shown += delegate { timer.Start(); };
        }
    }

    public class LoginForm : Form
    {
        const string ValidUser = "Bebel";
        const string PasswordSha256 = "bc3df08d35d963ca80a24d71262eb057a13627cf7a2b12984f7be440a2721782";
        TextBox userBox, passwordBox;
        Label errorLabel;
        Label localClockLabel;
        System.Windows.Forms.Timer localClockTimer;
        int attempts = 0;

        public LoginForm()
        {
            Text = "Login • Bebel Equipe Do Mais Novo 155";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(490, 540);
            BackColor = Color.FromArgb(10, 14, 20);
            ForeColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // Relógio compacto de Brasília: fica na borda superior e não altera o layout.
            localClockLabel = new Label();
            localClockLabel.Text = "Localizando cidade...";
            localClockLabel.ForeColor = Color.FromArgb(148, 163, 184);
            localClockLabel.Font = new Font("Segoe UI", 8.2F);
            localClockLabel.TextAlign = ContentAlignment.MiddleRight;
            localClockLabel.Size = new Size(452, 18);
            localClockLabel.Location = new Point(18, 4);
            localClockLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(localClockLabel);

            UpdateLocalClock();
            ResolveLoginCityAsync();
            localClockTimer = new System.Windows.Forms.Timer();
            localClockTimer.Interval = 1000;
            localClockTimer.Tick += delegate { UpdateLocalClock(); };
            localClockTimer.Start();

            FormClosed += delegate
            {
                try
                {
                    if (localClockTimer != null)
                    {
                        localClockTimer.Stop();
                        localClockTimer.Dispose();
                    }
                }
                catch { }
            };

            PictureBox logo = new PictureBox();
            logo.Image = BrandResources.Load("Bebel155.Logo");
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Size = new Size(190, 190);
            logo.Location = new Point(150, 24);
            Controls.Add(logo);

            Label title = new Label();
            title.Text = "ACESSO B155";
            title.Font = new Font("Segoe UI Semibold", 18F);
            title.ForeColor = Color.FromArgb(238, 183, 58);
            title.AutoSize = true;
            title.Location = new Point(170, 222);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Entre para acessar o sistema";
            sub.Font = new Font("Segoe UI", 9.5F);
            sub.ForeColor = Color.FromArgb(148, 163, 184);
            sub.AutoSize = true;
            sub.Location = new Point(148, 260);
            Controls.Add(sub);

            Label lu = NewLabel("Usuário", 60, 306);
            Controls.Add(lu);
            userBox = NewTextBox(60, 330, false);
            userBox.Text = "Bebel";
            Controls.Add(userBox);

            Label lp = NewLabel("Senha", 60, 374);
            Controls.Add(lp);
            passwordBox = NewTextBox(60, 398, true);
            Controls.Add(passwordBox);

            CheckBox show = new CheckBox();
            show.Text = "Mostrar senha";
            show.ForeColor = Color.FromArgb(180, 190, 205);
            show.BackColor = BackColor;
            show.AutoSize = true;
            show.Location = new Point(60, 438);
            show.CheckedChanged += delegate { passwordBox.UseSystemPasswordChar = !show.Checked; };
            Controls.Add(show);

            Button login = new Button();
            login.Text = "ENTRAR";
            login.Size = new Size(370, 42);
            login.Location = new Point(60, 468);
            login.FlatStyle = FlatStyle.Flat;
            login.FlatAppearance.BorderSize = 0;
            login.BackColor = Color.FromArgb(23, 118, 255);
            login.ForeColor = Color.White;
            login.Font = new Font("Segoe UI Semibold", 10F);
            login.Click += delegate { TryLogin(); };
            Controls.Add(login);

            errorLabel = new Label();
            errorLabel.Text = "";
            errorLabel.ForeColor = Color.FromArgb(239, 68, 68);
            errorLabel.AutoSize = true;
            errorLabel.Location = new Point(60, 517);
            Controls.Add(errorLabel);

            AcceptButton = login;
        }

        void ResolveLoginCityAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string city = LocationService.ResolveCity();
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            localClockLabel.Text = LocationService.ClockText(city);
                        });
                    }
                }
                catch { }
            });
        }

        void UpdateLocalClock()
        {
            try
            {
                localClockLabel.Text = LocationService.ClockText(LocationService.CachedCity);
            }
            catch { }
        }

        Label NewLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = Color.FromArgb(210, 218, 232);
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        TextBox NewTextBox(int x, int y, bool password)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(370, 28);
            t.Font = new Font("Segoe UI", 10F);
            t.BackColor = Color.FromArgb(23, 30, 40);
            t.ForeColor = Color.White;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.UseSystemPasswordChar = password;
            return t;
        }

        string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        void TryLogin()
        {
            if (string.Equals(userBox.Text, ValidUser, StringComparison.Ordinal) &&
                string.Equals(Sha256(passwordBox.Text), PasswordSha256, StringComparison.OrdinalIgnoreCase))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            attempts++;
            errorLabel.Text = "Usuário ou senha incorretos. Tentativa " + attempts + "/5.";
            passwordBox.Clear();
            passwordBox.Focus();

            if (attempts >= 5)
            {
                MessageBox.Show("Número máximo de tentativas atingido. Abra o sistema novamente.", "B155", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }
    }

    public class UpdateManifest
    {
        public string version { get; set; }
        public string downloadUrl { get; set; }
        public string sha256 { get; set; }
        public string notes { get; set; }
    }

    public class MainForm : Form
    {
        readonly string AppName = "Bebel Equipe Do Mais Novo 155";
        readonly string AppVersion = "5.1.9";

        string BaseDir, LogDir, ReportDir, BackupDir, ToolsDir, DownloadDir, ScreenshotDir, ExportDir, HistoryDir, PlatformToolsDir, UpdateDir, SettingsFile, LogFile;
        UpdateManifest pendingManifest;
        string lastGithubUpdateProblem = "";

        TableLayoutPanel root;
        Panel sidebar, header, statusBar, contentHost;
        Label statusLabel, headerDeviceLabel;
        Label statusUsb, statusAdb, statusIos, statusSystem;
        ProgressBar progress;
        Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        Dictionary<string, RichTextBox> outputs = new Dictionary<string, RichTextBox>();
        Dictionary<string, Button> navButtons = new Dictionary<string, Button>();

        System.Windows.Forms.Timer autoTimer;
        System.Windows.Forms.Timer sidebarClockTimer;
        Label sidebarClockLabel;
        string operatingCity = "Local";

        readonly object ownedProcessLock = new object();
        readonly HashSet<int> ownedProcessIds = new HashSet<int>();
        bool cleanupStarted = false;
        bool isShuttingDown = false;

        readonly Color CBackground = Color.FromArgb(12, 16, 22);
        readonly Color CCard = Color.FromArgb(20, 26, 35);
        readonly Color CBorder = Color.FromArgb(48, 59, 76);
        readonly Color CNav = Color.FromArgb(7, 10, 15);
        readonly Color CNavHover = Color.FromArgb(25, 35, 50);
        readonly Color CAccent = Color.FromArgb(23, 118, 255);
        readonly Color CGreen = Color.FromArgb(34, 197, 94);
        readonly Color CYellow = Color.FromArgb(245, 158, 11);
        readonly Color CRed = Color.FromArgb(239, 68, 68);
        readonly Color CText = Color.FromArgb(239, 243, 249);
        readonly Color CMuted = Color.FromArgb(148, 163, 184);

        Font FBody = new Font("Segoe UI", 9.5F);
        Font FSmall = new Font("Segoe UI", 8.5F);
        Font FNav = new Font("Segoe UI", 10F);
        Font FTitle = new Font("Segoe UI Semibold", 20F);
        Font FSection = new Font("Segoe UI Semibold", 15F);
        Font FCardTitle = new Font("Segoe UI Semibold", 11F);
        Font FCardValue = new Font("Segoe UI Semibold", 16F);
        Font FMono = new Font("Consolas", 9.2F);

        Label cardUsbValue, cardAndroidValue, cardIosValue, cardSystemValue, cardDriverValue;
        Label deviceNameValue, deviceModeValue, deviceVendorValue, deviceIdValue, deviceOsValue;
        Label assistantHeadline, assistantSeverity;
        RichTextBox assistantText;
        PictureBox deviceRenderBox;
        Label deviceRenderStatus;
        string lastDeviceRenderKey = "";
        Label infoSystemValue, infoBatteryValue, infoRamValue, infoStorageValue, infoResolutionValue;

        public MainForm()
        {
            BaseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BebelEquipe155");
            string documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Bebel Equipe Do Mais Novo 155");
            LogDir = Path.Combine(localData, "logs");
            ToolsDir = Path.Combine(localData, "tools");
            DownloadDir = Path.Combine(localData, "downloads");
            UpdateDir = Path.Combine(localData, "updates");
            SettingsFile = Path.Combine(localData, "settings.ini");
            ReportDir = Path.Combine(documents, "relatorios");
            BackupDir = Path.Combine(documents, "backups");
            ScreenshotDir = Path.Combine(documents, "capturas");
            ExportDir = Path.Combine(documents, "exportacoes");
            HistoryDir = Path.Combine(documents, "historico");
            PlatformToolsDir = Path.Combine(ToolsDir, "platform-tools");

            foreach (string d in new[] { LogDir, ReportDir, BackupDir, ToolsDir, DownloadDir, ScreenshotDir, ExportDir, HistoryDir, UpdateDir })
                Directory.CreateDirectory(d);

            LogFile = Path.Combine(LogDir, "bebel155_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            EnsureSettings();
            MigrateLegacySettings();

            Text = AppName;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1120, 720);
            Size = new Size(1360, 860);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = FBody;
            BackColor = CBackground;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Application; }

            BuildUI();
            ShowPage("Painel");

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                isShuttingDown = true;
                CleanupOwnedProcesses();
            };

            Shown += delegate
            {
                Log("Aplicativo iniciado.");
                RefreshDashboardAsync();
                ThreadPool.QueueUserWorkItem(delegate { AutoUpdateStartup(); });
                ResolveOperatingCityAsync();
            };

            autoTimer = new System.Windows.Forms.Timer();
            autoTimer.Interval = 5000;
            autoTimer.Tick += delegate
            {
                if (pages.ContainsKey("Painel") && pages["Painel"].Visible && !progress.Visible)
                    RefreshQuickIndicators();
            };
            autoTimer.Start();
        }

        void BuildUI()
        {
            root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 3;
            root.ColumnCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            BuildBrand();
            BuildHeader();
            BuildSidebar();
            BuildStatusBar();

            contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = CBackground;
            root.Controls.Add(contentHost, 1, 1);

            BuildPages();
        }

        void BuildBrand()
        {
            Panel brand = new Panel();
            brand.Dock = DockStyle.Fill;
            brand.BackColor = CNav;
            root.Controls.Add(brand, 0, 0);

            PictureBox logo = new PictureBox();
            logo.Image = LoadEmbeddedImage("Bebel155.Logo");
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Size = new Size(76, 76);
            logo.Location = new Point(10, 13);
            brand.Controls.Add(logo);

            Label l1 = new Label();
            l1.Text = "B155";
            l1.ForeColor = Color.FromArgb(238, 183, 58);
            l1.Font = new Font("Segoe UI Semibold", 18F);
            l1.AutoSize = true;
            l1.Location = new Point(94, 24);
            brand.Controls.Add(l1);

            Label l2 = new Label();
            l2.Text = "Bebel Equipe\nDo Mais Novo 155";
            l2.ForeColor = Color.FromArgb(181, 192, 210);
            l2.Font = new Font("Segoe UI", 8.5F);
            l2.AutoSize = true;
            l2.Location = new Point(96, 55);
            brand.Controls.Add(l2);
        }

        void BuildHeader()
        {
            header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = CCard;
            root.Controls.Add(header, 1, 0);

            Label title = new Label();
            title.Text = AppName;
            title.Font = FTitle;
            title.ForeColor = CText;
            title.AutoSize = true;
            title.Location = new Point(26, 17);
            header.Controls.Add(title);

            headerDeviceLabel = new Label();
            headerDeviceLabel.Text = "Aguardando dispositivo • v" + AppVersion;
            headerDeviceLabel.ForeColor = CMuted;
            headerDeviceLabel.AutoSize = true;
            headerDeviceLabel.Location = new Point(29, 58);
            header.Controls.Add(headerDeviceLabel);

            Button exitButton = new Button();
            exitButton.Text = "⏻  Encerrar";
            exitButton.FlatStyle = FlatStyle.Flat;
            exitButton.FlatAppearance.BorderColor = Color.FromArgb(120, 55, 62);
            exitButton.BackColor = Color.FromArgb(61, 29, 35);
            exitButton.ForeColor = Color.FromArgb(255, 220, 224);
            exitButton.Size = new Size(122, 38);
            exitButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            exitButton.Location = new Point(header.Width - 148, 30);
            exitButton.Click += delegate { ShutdownApplication(); };
            header.Controls.Add(exitButton);

            Button refresh = new Button();
            refresh.Text = "↻  Atualizar";
            refresh.FlatStyle = FlatStyle.Flat;
            refresh.FlatAppearance.BorderColor = CBorder;
            refresh.BackColor = Color.FromArgb(27, 35, 47);
            refresh.ForeColor = CText;
            refresh.Size = new Size(122, 38);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Location = new Point(header.Width - 280, 30);
            refresh.Click += delegate { RefreshDashboardAsync(); };
            header.Controls.Add(refresh);

            header.Resize += delegate
            {
                exitButton.Left = Math.Max(150, header.ClientSize.Width - exitButton.Width - 24);
                refresh.Left = Math.Max(20, exitButton.Left - refresh.Width - 10);
            };
        }

        void BuildSidebar()
        {
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.BackColor = CNav;
            sidebar.AutoScroll = true;
            root.Controls.Add(sidebar, 0, 1);

            sidebarClockLabel = new Label();
            sidebarClockLabel.Text = LocationService.ClockText(LocationService.CachedCity);
            sidebarClockLabel.ForeColor = Color.FromArgb(164, 177, 197);
            sidebarClockLabel.Font = new Font("Segoe UI", 8.2F);
            sidebarClockLabel.AutoEllipsis = true;
            sidebarClockLabel.Size = new Size(216, 22);
            sidebarClockLabel.Location = new Point(14, 10);
            sidebarClockLabel.TextAlign = ContentAlignment.MiddleLeft;
            sidebar.Controls.Add(sidebarClockLabel);

            sidebarClockTimer = new System.Windows.Forms.Timer();
            sidebarClockTimer.Interval = 1000;
            sidebarClockTimer.Tick += delegate
            {
                if (sidebarClockLabel != null)
                    sidebarClockLabel.Text = LocationService.ClockText(operatingCity);
            };
            sidebarClockTimer.Start();

            string[] names = { "Painel", "Informações", "Android", "iPhone / iOS", "Diagnóstico", "Recuperação", "Backup", "Ferramentas", "Atualizações", "Relatórios" };
            int top = 42;
            foreach (string n in names)
            {
                Button b = new Button();
                b.Text = "   " + n;
                b.Tag = n;
                b.Font = FNav;
                b.ForeColor = Color.White;
                b.BackColor = CNav;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = CNavHover;
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.Size = new Size(240, 48);
                b.Location = new Point(0, top);
                b.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                b.Click += delegate { ShowPage((string)b.Tag); };
                sidebar.Controls.Add(b);
                navButtons[n] = b;
                top += 50;
            }

            Label footer = new Label();
            footer.Text = "v5.1.9 • USB / Android / iOS";
            footer.ForeColor = Color.FromArgb(145, 155, 172);
            footer.Font = FSmall;
            footer.AutoSize = true;
            footer.Location = new Point(18, top + 20);
            sidebar.Controls.Add(footer);
        }

        void ResolveOperatingCityAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string city = LocationService.ResolveCity();
                operatingCity = city;

                try
                {
                    if (!isShuttingDown && !IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (sidebarClockLabel != null)
                                sidebarClockLabel.Text = LocationService.ClockText(operatingCity);
                        });
                    }
                }
                catch { }
            });
        }

        void RegisterOwnedProcess(Process p)
        {
            try
            {
                if (p == null) return;
                lock (ownedProcessLock) ownedProcessIds.Add(p.Id);
            }
            catch { }
        }

        void UnregisterOwnedProcess(Process p)
        {
            try
            {
                if (p == null) return;
                lock (ownedProcessLock) ownedProcessIds.Remove(p.Id);
            }
            catch { }
        }

        void StopAdbServerOnExit()
        {
            try
            {
                string adb = ToolPath("adb.exe");
                if (string.IsNullOrWhiteSpace(adb) || !File.Exists(adb)) return;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = adb;
                psi.Arguments = "kill-server";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                using (Process p = Process.Start(psi))
                {
                    if (p != null && !p.WaitForExit(3000))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch { }
        }

        bool IsBebelToolProcess(Process p)
        {
            try
            {
                if (p == null || p.HasExited || p.MainModule == null) return false;
                string file = p.MainModule.FileName ?? "";
                if (string.IsNullOrWhiteSpace(file)) return false;

                return file.StartsWith(ToolsDir, StringComparison.OrdinalIgnoreCase) ||
                       file.StartsWith(PlatformToolsDir, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        void CleanupOwnedProcesses()
        {
            if (cleanupStarted) return;
            cleanupStarted = true;

            try { if (autoTimer != null) autoTimer.Stop(); } catch { }
            try { if (sidebarClockTimer != null) sidebarClockTimer.Stop(); } catch { }

            StopAdbServerOnExit();

            List<int> processIds;
            lock (ownedProcessLock)
                processIds = ownedProcessIds.ToList();

            foreach (int pid in processIds)
            {
                try
                {
                    Process p = Process.GetProcessById(pid);
                    if (!p.HasExited)
                    {
                        p.Kill();
                        p.WaitForExit(1500);
                    }
                }
                catch { }
            }

            // Fallback only for adb/fastboot executables located inside the Bebel tools directory.
            foreach (string processName in new[] { "adb", "fastboot" })
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(processName))
                    {
                        try
                        {
                            if (IsBebelToolProcess(p) && !p.HasExited)
                            {
                                p.Kill();
                                p.WaitForExit(1200);
                            }
                        }
                        catch { }
                        finally { try { p.Dispose(); } catch { } }
                    }
                }
                catch { }
            }

            try { Log("Encerramento: timers e processos auxiliares do Bebel 155 finalizados."); } catch { }
        }

        void ShutdownApplication()
        {
            DialogResult answer = MessageBox.Show(
                "Encerrar completamente o Bebel 155 e finalizar os processos auxiliares iniciados pelo programa?",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            isShuttingDown = true;
            Close();
        }

        void BuildStatusBar()
        {
            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.BackColor = CNav;
            root.Controls.Add(left, 0, 2);

            statusBar = new Panel();
            statusBar.Dock = DockStyle.Fill;
            statusBar.BackColor = Color.FromArgb(10, 14, 20);
            root.Controls.Add(statusBar, 1, 2);

            // First lane: text and status badges only.
            Panel statusTopRow = new Panel();
            statusTopRow.Name = "statusTopRow";
            statusTopRow.Dock = DockStyle.Top;
            statusTopRow.Height = 34;
            statusTopRow.BackColor = statusBar.BackColor;
            statusBar.Controls.Add(statusTopRow);

            statusLabel = new Label();
            statusLabel.Text = "● Pronto";
            statusLabel.ForeColor = CMuted;
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(18, 9);
            statusTopRow.Controls.Add(statusLabel);

            statusUsb = NewStatusBadge("● USB", CMuted);
            statusAdb = NewStatusBadge("● ADB", CMuted);
            statusIos = NewStatusBadge("● iOS", CMuted);
            statusSystem = NewStatusBadge("Sistema: —", CMuted);
            statusTopRow.Controls.Add(statusUsb);
            statusTopRow.Controls.Add(statusAdb);
            statusTopRow.Controls.Add(statusIos);
            statusTopRow.Controls.Add(statusSystem);

            // Second lane: progress only. It never shares the text baseline.
            Panel progressLane = new Panel();
            progressLane.Dock = DockStyle.Bottom;
            progressLane.Height = 24;
            progressLane.BackColor = statusBar.BackColor;
            statusBar.Controls.Add(progressLane);

            progress = new ProgressBar();
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 25;
            progress.Visible = false;
            progress.Size = new Size(180, 10);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            progress.Top = 36;
            progressLane.Controls.Add(progress);

            Action reposition = delegate
            {
                int right = statusTopRow.ClientSize.Width - 16;
                statusSystem.Left = right - statusSystem.Width; statusSystem.Top = 8; right -= statusSystem.Width + 18;
                statusIos.Left = right - statusIos.Width; statusIos.Top = 8; right -= statusIos.Width + 18;
                statusAdb.Left = right - statusAdb.Width; statusAdb.Top = 8; right -= statusAdb.Width + 18;
                statusUsb.Left = right - statusUsb.Width; statusUsb.Top = 8;

                // Progress lane uses its own coordinates and stays clear of all labels.
                progress.Left = Math.Max(12, progressLane.ClientSize.Width - progress.Width - 16);
                progress.Top = 7;
            };
            statusTopRow.Resize += delegate { reposition(); };
            progressLane.Resize += delegate { reposition(); };
            reposition();
        }

        Label NewStatusBadge(string text, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = color;
            l.Font = FSmall;
            l.AutoSize = true;
            return l;
        }

        Image LoadEmbeddedImage(string resourceName)
        {
            try
            {
                Assembly a = Assembly.GetExecutingAssembly();
                using (Stream s = a.GetManifestResourceStream(resourceName))
                {
                    if (s == null) return null;
                    using (Image img = Image.FromStream(s)) return new Bitmap(img);
                }
            }
            catch { return null; }
        }

        void BuildPages()
        {
            BuildDashboardPage();
            BuildDeviceInfoPage();
            BuildAndroidPage();
            BuildIosPage();
            BuildDiagnosticPage();
            BuildRecoveryPage();
            BuildBackupPage();
            BuildToolsPage();
            BuildUpdatesPage();
            BuildReportsPage();

            foreach (Panel p in pages.Values)
            {
                p.Visible = false;
                contentHost.Controls.Add(p);
            }
        }

        Panel NewBasePage(string title, string subtitle)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = CBackground;
            p.Padding = new Padding(24);
            p.AutoScroll = true;

            Label t = new Label();
            t.Text = title;
            t.Font = FSection;
            t.ForeColor = CText;
            t.AutoSize = true;
            t.Location = new Point(24, 18);
            p.Controls.Add(t);

            Label s = new Label();
            s.Text = subtitle;
            s.Font = FBody;
            s.ForeColor = CMuted;
            s.AutoSize = true;
            s.Location = new Point(27, 50);
            p.Controls.Add(s);

            return p;
        }

        Panel MakeCard(string title, string value, int width, out Label valueLabel)
        {
            Panel card = new Panel();
            card.BackColor = CCard;
            card.Size = new Size(width, 112);
            card.Margin = new Padding(0, 0, 14, 14);
            card.Padding = new Padding(16);
            card.BorderStyle = BorderStyle.FixedSingle;

            Label t = new Label();
            t.Text = title;
            t.Font = FCardTitle;
            t.ForeColor = CMuted;
            t.AutoSize = true;
            t.Location = new Point(14, 15);
            card.Controls.Add(t);

            valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.Font = FCardValue;
            valueLabel.ForeColor = CText;
            valueLabel.AutoSize = true;
            valueLabel.Location = new Point(14, 49);
            card.Controls.Add(valueLabel);

            return card;
        }

        Panel MakeInfoPanel(string title, out TableLayoutPanel fields)
        {
            Panel card = new Panel();
            card.BackColor = CCard;
            card.BorderStyle = BorderStyle.FixedSingle;
            card.Padding = new Padding(16);

            Label t = new Label();
            t.Text = title;
            t.Font = FCardTitle;
            t.ForeColor = CText;
            t.AutoSize = true;
            t.Dock = DockStyle.Top;
            card.Controls.Add(t);

            fields = new TableLayoutPanel();
            fields.Dock = DockStyle.Fill;
            fields.Padding = new Padding(0, 30, 0, 0);
            fields.ColumnCount = 2;
            fields.RowCount = 5;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(fields);
            return card;
        }

        void AddField(TableLayoutPanel t, int row, string label, out Label value)
        {
            Label l = new Label();
            l.Text = label;
            l.ForeColor = CMuted;
            l.AutoSize = true;
            l.Margin = new Padding(0, 8, 8, 8);
            t.Controls.Add(l, 0, row);

            value = new Label();
            value.Text = "—";
            value.ForeColor = CText;
            value.AutoEllipsis = true;
            value.Dock = DockStyle.Fill;
            value.Margin = new Padding(0, 8, 0, 8);
            t.Controls.Add(value, 1, row);
        }

        void BuildDashboardPage()
        {
            Panel p = NewBasePage("Painel do aparelho", "Status, versão do sistema, identidade USB e diagnóstico inteligente.");
            pages["Painel"] = p;

            PictureBox banner = new PictureBox();
            banner.Image = LoadEmbeddedImage("Bebel155.Banner");
            banner.SizeMode = PictureBoxSizeMode.Zoom;
            banner.BackColor = Color.Black;
            banner.Location = new Point(24, 78);
            banner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            banner.Size = new Size(Math.Max(600, p.ClientSize.Width - 48), 150);
            p.Resize += delegate { banner.Width = Math.Max(600, p.ClientSize.Width - 48); };
            p.Controls.Add(banner);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Location = new Point(24, 242);
            layout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            layout.Size = new Size(p.ClientSize.Width - 48, p.ClientSize.Height - 266);
            layout.ColumnCount = 2;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            p.Resize += delegate
            {
                layout.Size = new Size(Math.Max(600, p.ClientSize.Width - 48), Math.Max(360, p.ClientSize.Height - 266));
            };
            p.Controls.Add(layout);

            FlowLayoutPanel cards = new FlowLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.WrapContents = true;
            cards.AutoScroll = false;
            cards.BackColor = CBackground;
            layout.Controls.Add(cards, 0, 0);
            layout.SetColumnSpan(cards, 2);

            cards.Controls.Add(MakeCard("USB", "Aguardando", 150, out cardUsbValue));
            cards.Controls.Add(MakeCard("Android", "Aguardando", 150, out cardAndroidValue));
            cards.Controls.Add(MakeCard("iPhone / iOS", "Aguardando", 150, out cardIosValue));
            cards.Controls.Add(MakeCard("Sistema", "—", 190, out cardSystemValue));
            cards.Controls.Add(MakeCard("Driver", "Aguardando", 150, out cardDriverValue));

            TableLayoutPanel fields;
            Panel devCard = MakeInfoPanel("Dispositivo principal", out fields);
            devCard.Dock = DockStyle.Fill;
            devCard.Margin = new Padding(0, 0, 12, 12);
            AddField(fields, 0, "Nome", out deviceNameValue);
            AddField(fields, 1, "Fabricante", out deviceVendorValue);
            AddField(fields, 2, "Modo", out deviceModeValue);
            AddField(fields, 3, "Sistema", out deviceOsValue);
            AddField(fields, 4, "ID USB", out deviceIdValue);

            deviceRenderBox = new PictureBox();
            deviceRenderBox.Dock = DockStyle.Right;
            deviceRenderBox.Width = 190;
            deviceRenderBox.SizeMode = PictureBoxSizeMode.Zoom;
            deviceRenderBox.BackColor = Color.FromArgb(12, 17, 24);
            deviceRenderBox.Image = BrandResources.Load("Bebel155.Logo");
            devCard.Controls.Add(deviceRenderBox);
            deviceRenderBox.BringToFront();

            deviceRenderStatus = new Label();
            deviceRenderStatus.Text = "Render do aparelho";
            deviceRenderStatus.ForeColor = Color.FromArgb(185, 195, 210);
            deviceRenderStatus.BackColor = Color.FromArgb(20, 24, 32);
            deviceRenderStatus.Font = FSmall;
            deviceRenderStatus.TextAlign = ContentAlignment.MiddleCenter;
            deviceRenderStatus.Dock = DockStyle.Bottom;
            deviceRenderStatus.Height = 34;
            deviceRenderStatus.AutoEllipsis = true;
            deviceRenderBox.Controls.Add(deviceRenderStatus);

            fields.Padding = new Padding(0, 30, 195, 0);
            layout.Controls.Add(devCard, 0, 1);

            Panel assistant = new Panel();
            assistant.Dock = DockStyle.Fill;
            assistant.Margin = new Padding(0, 0, 0, 12);
            assistant.BackColor = CCard;
            assistant.BorderStyle = BorderStyle.FixedSingle;
            assistant.Padding = new Padding(16);
            layout.Controls.Add(assistant, 1, 1);

            assistantSeverity = new Label();
            assistantSeverity.Text = "ANÁLISE";
            assistantSeverity.Font = FSmall;
            assistantSeverity.ForeColor = CAccent;
            assistantSeverity.AutoSize = true;
            assistantSeverity.Location = new Point(16, 14);
            assistant.Controls.Add(assistantSeverity);

            assistantHeadline = new Label();
            assistantHeadline.Text = "Conecte um aparelho";
            assistantHeadline.Font = FCardTitle;
            assistantHeadline.ForeColor = CText;
            assistantHeadline.AutoSize = true;
            assistantHeadline.MaximumSize = new Size(450, 0);
            assistantHeadline.Location = new Point(16, 38);
            assistant.Controls.Add(assistantHeadline);

            assistantText = new RichTextBox();
            assistantText.ReadOnly = true;
            assistantText.BorderStyle = BorderStyle.None;
            assistantText.BackColor = CCard;
            assistantText.ForeColor = CMuted;
            assistantText.Font = FBody;
            assistantText.Location = new Point(13, 72);
            assistantText.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            assistantText.Size = new Size(assistant.Width - 28, assistant.Height - 84);
            assistant.Resize += delegate
            {
                assistantText.Size = new Size(Math.Max(100, assistant.ClientSize.Width - 28), Math.Max(70, assistant.ClientSize.Height - 84));
            };
            assistant.Controls.Add(assistantText);

            RichTextBox output = NewOutput();
            output.Dock = DockStyle.Fill;
            layout.Controls.Add(output, 0, 2);
            layout.SetColumnSpan(output, 2);
            outputs["Painel"] = output;
        }


        void BuildDeviceInfoPage()
        {
            Panel p = NewBasePage("Informações detalhadas do aparelho", "Cards gráficos e dados técnicos em unidades legíveis: GB, MB, %, resolução, serial e build.");
            pages["Informações"] = p;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Location = new Point(24, 84);
            layout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            layout.Size = new Size(p.ClientSize.Width - 48, p.ClientSize.Height - 108);
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            p.Resize += delegate
            {
                layout.Size = new Size(Math.Max(520, p.ClientSize.Width - 48), Math.Max(430, p.ClientSize.Height - 108));
            };
            p.Controls.Add(layout);

            FlowLayoutPanel cards = new FlowLayoutPanel();
            cards.Dock = DockStyle.Top;
            cards.AutoSize = true;
            cards.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            cards.WrapContents = true;
            cards.BackColor = CBackground;
            cards.Padding = new Padding(0, 0, 0, 8);
            layout.Controls.Add(cards, 0, 0);

            cards.Controls.Add(MakeCard("Sistema", "Aguardando", 185, out infoSystemValue));
            cards.Controls.Add(MakeCard("Bateria", "—", 165, out infoBatteryValue));
            cards.Controls.Add(MakeCard("RAM", "—", 165, out infoRamValue));
            cards.Controls.Add(MakeCard("Armazenamento", "—", 215, out infoStorageValue));
            cards.Controls.Add(MakeCard("Resolução", "—", 185, out infoResolutionValue));

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Top;
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.WrapContents = true;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.Padding = new Padding(0, 0, 0, 10);
            actions.BackColor = CBackground;
            layout.Controls.Add(actions, 0, 1);

            AddAction(actions, "Detectar automaticamente", delegate { RefreshDetailedInfo("auto"); });
            AddAction(actions, "Detalhes Android", delegate { RefreshDetailedInfo("android"); });
            AddAction(actions, "Detalhes iPhone", delegate { RefreshDetailedInfo("ios"); });
            AddAction(actions, "Exportar TXT", delegate { RunBackground("Informações", "Exportando informações...", ExportDetailedDeviceInfo); });
            AddAction(actions, "Verificar coerência", delegate { RunBackground("Informações", "Comparando identidade do aparelho...", GetIdentityConsistencyReport); });
            AddAction(actions, "Atualizar painel", delegate { RefreshDashboardAsync(); });

            RichTextBox output = NewOutput();
            output.Dock = DockStyle.Fill;
            layout.Controls.Add(output, 0, 2);
            outputs["Informações"] = output;
        }

        void BuildAndroidPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Android Toolkit", "ADB, Fastboot, backup, captura e informações técnicas.", out actions, out output);
            pages["Android"] = p;
            outputs["Android"] = output;

            AddAction(actions, "Informações ADB", delegate { RunBackground("Android", "Consultando Android...", GetAndroidInfo); });
            AddAction(actions, "Detalhes completos", delegate { ShowPage("Informações"); RefreshDetailedInfo("android"); });
            AddAction(actions, "Fastboot", delegate { RunBackground("Android", "Consultando Fastboot...", GetFastbootInfo); });
            AddAction(actions, "Listar apps", delegate { RunBackground("Android", "Listando apps...", ExportAndroidApps); });
            AddAction(actions, "Capturar tela", delegate { RunBackground("Android", "Capturando tela...", CaptureAndroidScreen); });
            AddAction(actions, "Backup DCIM", delegate { RunBackground("Android", "Executando backup...", BackupAndroidDCIM); });
            AddAction(actions, "Reiniciar ADB", delegate { RunBackground("Android", "Reiniciando ADB...", RestartAdb); });
            AddAction(actions, "Reiniciar aparelho", delegate { ConfirmAndroidReboot(""); });
            AddAction(actions, "Recovery", delegate { ConfirmAndroidReboot("recovery"); });
            AddAction(actions, "Bootloader", delegate { ConfirmAndroidReboot("bootloader"); });
        }

        void BuildIosPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("iPhone / iOS", "Verificação autorizada, Recovery/DFU e backup.", out actions, out output);
            pages["iPhone / iOS"] = p;
            outputs["iPhone / iOS"] = output;

            AddAction(actions, "Informações iOS", delegate { RunBackground("iPhone / iOS", "Consultando iPhone...", GetIOSInfo); });
            AddAction(actions, "Detalhes completos", delegate { ShowPage("Informações"); RefreshDetailedInfo("ios"); });
            AddAction(actions, "iDevice Verification", delegate { RunBackground("iPhone / iOS", "Verificando iDevice...", GetIDeviceVerification); });
            AddAction(actions, "Cor / capacidade / bateria", delegate { RunBackground("iPhone / iOS", "Lendo especificações do iPhone...", GetIosHardwareDetails); });
            AddAction(actions, "Recovery / DFU", delegate { RunBackground("iPhone / iOS", "Analisando Recovery/DFU...", GetIOSRecoveryDFU); });
            AddAction(actions, "Backup autorizado", delegate { RunBackground("iPhone / iOS", "Executando backup...", BackupIOS); });
            AddAction(actions, "Apple Devices", delegate { OpenAppleDevicesOrStore(); });
            AddAction(actions, "Guia de restauração", delegate { OpenUrl("https://support.apple.com/iphone/restore"); });
        }

        void BuildDiagnosticPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Assistente automático de diagnóstico", "Identifica o problema provável e recomenda o próximo passo.", out actions, out output);
            pages["Diagnóstico"] = p;
            outputs["Diagnóstico"] = output;

            AddAction(actions, "Diagnosticar agora", delegate { RunBackground("Diagnóstico", "Analisando USB e drivers...", GetDriverDiagnosis); });
            AddAction(actions, "Solução recomendada", delegate { RunBackground("Diagnóstico", "Calculando solução...", GetSmartAssistantReport); });
            AddAction(actions, "Reexaminar hardware", delegate { RunBackground("Diagnóstico", "Reexaminando dispositivos...", ScanDevices); });
            AddAction(actions, "Gerenciador de Dispositivos", delegate { StartSimple("devmgmt.msc", ""); });
            AddAction(actions, "Copiar resultado", delegate { if (!string.IsNullOrWhiteSpace(output.Text)) Clipboard.SetText(output.Text); });
        }

        void BuildRecoveryPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Recuperação oficial de contas e bloqueios", "Orientação para proprietários legítimos. Não executa bypass de FRP, Apple ID ou Activation Lock.", out actions, out output);
            pages["Recuperação"] = p;
            outputs["Recuperação"] = output;

            AddAction(actions, "Analisar aparelho conectado", delegate { RunBackground("Recuperação", "Identificando cenário...", GetConnectedRecoveryGuide); });
            AddAction(actions, "Android / FRP oficial", delegate { output.Text = GetAndroidRecoveryGuide(); });
            AddAction(actions, "Apple ID / Activation Lock", delegate { output.Text = GetAppleRecoveryGuide(); });
            AddAction(actions, "Dispositivos suportados", delegate { output.Text = GetRecoverySupportMatrix(); });
            AddAction(actions, "Recuperação Conta Google", delegate { OpenUrl("https://accounts.google.com/signin/recovery"); });
            AddAction(actions, "Suporte Activation Lock", delegate { OpenUrl("https://support.apple.com/pt-br/108934"); });
        }

        void BuildBackupPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Central de backup", "Backups autorizados e exportações locais.", out actions, out output);
            pages["Backup"] = p;
            outputs["Backup"] = output;

            AddAction(actions, "Backup Android DCIM", delegate { RunBackground("Backup", "Backup Android...", BackupAndroidDCIM); });
            AddAction(actions, "Backup iPhone", delegate { RunBackground("Backup", "Backup iPhone...", BackupIOS); });
            AddAction(actions, "Abrir backups", delegate { OpenFolder(BackupDir); });
            AddAction(actions, "Abrir capturas", delegate { OpenFolder(ScreenshotDir); });
            AddAction(actions, "Abrir exportações", delegate { OpenFolder(ExportDir); });
        }

        void BuildToolsPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Configuração e dependências", "Prepare Android e iPhone e resolva os componentes que aparecem como AUSENTE.", out actions, out output);
            pages["Ferramentas"] = p;
            outputs["Ferramentas"] = output;

            AddAction(actions, "Verificar dependências", delegate { RunBackground("Ferramentas", "Verificando...", CheckDependencies); });
            AddAction(actions, "Reparar WinGet / App Installer", delegate { RepairWingetInteractive(); });
            AddAction(actions, "Instalar ADB/Fastboot", delegate { RunBackground("Ferramentas", "Baixando Platform Tools...", InstallPlatformTools); });
            AddAction(actions, "Instalar Apple Devices", delegate { RunBackground("Ferramentas", "Instalando Apple Devices...", InstallAppleDevices); });
            AddAction(actions, "Instalar iOS Tools", delegate
            {
                if (MessageBox.Show("Os iOS Tools serão baixados de uma build comunitária do libimobiledevice hospedada no GitHub (jrjr/libimobiledevice-windows). Deseja continuar?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    RunBackground("Ferramentas", "Baixando iOS Tools...", InstallIosCommunityTools);
            });
            AddAction(actions, "Configurar iPhone", delegate
            {
                if (MessageBox.Show("Isto tentará instalar Apple Devices e depois os iOS Tools comunitários. Continuar?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    RunBackground("Ferramentas", "Configurando iPhone...", ConfigureIphone);
            });
            AddAction(actions, "Configurar Android", delegate { RunBackground("Ferramentas", "Configurando Android...", InstallPlatformTools); });
            AddAction(actions, "Abrir dados do sistema", delegate { OpenFolder(Path.GetDirectoryName(LogDir)); });
            AddAction(actions, "Abrir documentos", delegate { OpenFolder(Path.GetDirectoryName(BackupDir)); });
        }


        void BuildUpdatesPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Central de atualizações", "Atualização do Bebel 155 e manutenção das dependências.", out actions, out output);
            pages["Atualizações"] = p;
            outputs["Atualizações"] = output;

            AddAction(actions, "Verificar Bebel 155", delegate { RunBackground("Atualizações", "Verificando atualização do sistema...", CheckAppUpdate); });
            AddAction(actions, "Baixar / aplicar update", delegate { ApplyPendingUpdate(); });
            AddAction(actions, "Atualizar dependências", delegate { RunBackground("Atualizações", "Atualizando dependências...", UpdateAllDependencies); });
            AddAction(actions, "Verificar dependências", delegate { RunBackground("Atualizações", "Verificando dependências...", CheckDependencies); });
            AddAction(actions, "Configurar update", delegate { OpenUpdateSettings(); });
            AddAction(actions, "Abrir GitHub Bebel155", delegate { OpenUrl("https://github.com/Bebel-155/bebel157"); });
            AddAction(actions, "Abrir pasta de updates", delegate { OpenFolder(UpdateDir); });
        }

        void BuildReportsPage()
        {
            FlowLayoutPanel actions;
            RichTextBox output;
            Panel p = BuildActionPage("Relatórios e histórico", "TXT, HTML e histórico CSV de dispositivos.", out actions, out output);
            pages["Relatórios"] = p;
            outputs["Relatórios"] = output;

            AddAction(actions, "Relatório TXT", delegate { RunBackground("Relatórios", "Gerando TXT...", GenerateTxtReport); });
            AddAction(actions, "Relatório HTML", delegate { RunBackground("Relatórios", "Gerando HTML...", GenerateHtmlReport); });
            AddAction(actions, "Registrar histórico", delegate { RunBackground("Relatórios", "Registrando dispositivo...", SaveDeviceHistory); });
            AddAction(actions, "Abrir relatórios", delegate { OpenFolder(ReportDir); });
            AddAction(actions, "Abrir logs", delegate { OpenFolder(LogDir); });
        }

        Panel BuildActionPage(string title, string subtitle, out FlowLayoutPanel actions, out RichTextBox output)
        {
            Panel p = NewBasePage(title, subtitle);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Location = new Point(24, 84);
            layout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            layout.Size = new Size(p.ClientSize.Width - 48, p.ClientSize.Height - 108);
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            p.Resize += delegate
            {
                layout.Size = new Size(Math.Max(500, p.ClientSize.Width - 48), Math.Max(420, p.ClientSize.Height - 108));
            };
            p.Controls.Add(layout);

            actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Top;
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.WrapContents = true;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.Padding = new Padding(0, 0, 0, 10);
            actions.BackColor = CBackground;
            layout.Controls.Add(actions, 0, 0);

            output = NewOutput();
            output.Dock = DockStyle.Fill;
            layout.Controls.Add(output, 0, 1);

            return p;
        }

        RichTextBox NewOutput()
        {
            RichTextBox r = new RichTextBox();
            r.ReadOnly = true;
            r.Font = FMono;
            r.BackColor = CCard;
            r.ForeColor = CText;
            r.BorderStyle = BorderStyle.FixedSingle;
            r.WordWrap = false;
            r.DetectUrls = true;
            r.Text = "Selecione uma ação.";
            return r;
        }

        void AddAction(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.MinimumSize = new Size(150, 40);
            b.Height = 40;
            b.Padding = new Padding(12, 0, 12, 0);
            b.Margin = new Padding(0, 0, 10, 10);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = CBorder;
            b.BackColor = Color.FromArgb(28, 36, 48);
            b.ForeColor = CText;
            b.Font = FBody;
            b.Click += handler;
            panel.Controls.Add(b);
        }

        void ShowPage(string name)
        {
            foreach (Panel p in pages.Values) p.Visible = false;
            if (!pages.ContainsKey(name)) return;

            pages[name].Visible = true;
            pages[name].BringToFront();

            foreach (KeyValuePair<string, Button> kv in navButtons)
                kv.Value.BackColor = kv.Key == name ? CNavHover : CNav;

            statusLabel.Text = name;
        }

        void SetBusy(bool busy, string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { SetBusy(busy, text); });
                return;
            }
            statusLabel.Text = text;
            progress.Visible = busy;
        }

        void RunBackground(string page, string busyText, Func<string> work)
        {
            SetBusy(true, busyText);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try { result = work(); }
                catch (Exception ex)
                {
                    result = "Erro: " + ex.Message;
                    Log("Erro em " + page + ": " + ex);
                }

                if (isShuttingDown || IsDisposed || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (outputs.ContainsKey(page))
                    {
                        outputs[page].Text = result;
                        outputs[page].SelectionStart = 0;
                    }
                    SetBusy(false, "Pronto");
                });
            });
        }

        void RefreshDashboardAsync()
        {
            SetBusy(true, "Analisando dispositivo...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                DashboardState state = BuildDashboardState();
                string diag = GetDriverDiagnosis();

                if (isShuttingDown || IsDisposed || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    ApplyDashboardState(state);
                    outputs["Painel"].Text = diag;
                    outputs["Painel"].SelectionStart = 0;
                    SetBusy(false, "Pronto");
                });
            });
        }

        DashboardState BuildDashboardState()
        {
            List<UsbDevice> devices = GetUsbDevices();
            string adb = AdbState();
            string fb = FastbootState();
            string ios = IosState();
            string systemVersion = DetectSystemVersion(adb, ios, devices);

            UsbDevice primary = ChoosePrimaryDevice(devices);

            DashboardState s = new DashboardState();
            s.Usb = devices.Count > 0 ? "Detectado" : "Ausente";
            s.Android = adb == "ADB CONECTADO" ? "ADB conectado" :
                        fb == "FASTBOOT CONECTADO" ? "Fastboot" :
                        adb == "ADB NÃO AUTORIZADO" ? "Não autorizado" : "Inativo";
            s.Ios = ios == "IPHONE AUTORIZADO" ? "Conectado" :
                    devices.Any(x => IsApple(x)) ? "USB detectado" : "Inativo";
            s.Driver = devices.Any(x => x.ErrorCode != 0) ? "Com problema" : devices.Count > 0 ? "OK" : "Aguardando";
            s.SystemVersion = systemVersion;

            if (primary != null)
            {
                s.Name = GetExactConnectedModel();

                if (adb == "ADB CONECTADO")
                {
                    string adbExe = ToolPath("adb.exe");
                    s.Vendor = GetExactAndroidManufacturer(adbExe);
                }
                else if (ios == "IPHONE AUTORIZADO" || IsApple(primary))
                {
                    s.Vendor = "Apple";
                }
                else
                {
                    s.Vendor = VendorFromId(primary.PnpId);
                }

                s.Id = primary.PnpId;
                s.Mode = InferMode(primary, adb, fb, ios);
            }
            else
            {
                s.Name = "Nenhum dispositivo";
                s.Vendor = "—";
                s.Id = "—";
                s.Mode = "—";
            }

            AssistantResult ar = BuildSmartAssistant(devices, adb, fb, ios);
            s.AssistantTitle = ar.Title;
            s.AssistantText = ar.Text;
            s.AssistantSeverity = ar.Severity;
            return s;
        }

        void ApplyDashboardState(DashboardState s)
        {
            cardUsbValue.Text = s.Usb;
            cardAndroidValue.Text = s.Android;
            cardIosValue.Text = s.Ios;
            cardSystemValue.Text = s.SystemVersion;
            cardDriverValue.Text = s.Driver;

            cardUsbValue.ForeColor = s.Usb == "Detectado" ? CGreen : CMuted;
            cardAndroidValue.ForeColor = s.Android.Contains("conectado") || s.Android == "Fastboot" ? CGreen :
                                         s.Android.Contains("autorizado") ? CYellow : CMuted;
            cardIosValue.ForeColor = s.Ios == "Conectado" ? CGreen : s.Ios == "USB detectado" ? CYellow : CMuted;
            cardSystemValue.ForeColor = s.SystemVersion == "Não identificado" ? CMuted : Color.FromArgb(238, 183, 58);
            cardDriverValue.ForeColor = s.Driver == "OK" ? CGreen : s.Driver == "Com problema" ? CRed : CMuted;

            deviceNameValue.Text = s.Name;
            deviceVendorValue.Text = s.Vendor;
            deviceModeValue.Text = s.Mode;
            deviceOsValue.Text = s.SystemVersion;
            deviceIdValue.Text = s.Id;

            headerDeviceLabel.Text = s.Name + (s.SystemVersion != "Não identificado" ? " • " + s.SystemVersion : "") + " • v" + AppVersion;

            RefreshDeviceRenderAsync();

            assistantHeadline.Text = s.AssistantTitle;
            assistantText.Text = s.AssistantText;
            assistantSeverity.Text = s.AssistantSeverity.ToUpperInvariant();

            if (s.AssistantSeverity == "Crítico") assistantSeverity.ForeColor = CRed;
            else if (s.AssistantSeverity == "Atenção") assistantSeverity.ForeColor = CYellow;
            else assistantSeverity.ForeColor = CGreen;

            statusUsb.Text = "● USB"; statusUsb.ForeColor = s.Usb == "Detectado" ? CGreen : CMuted;
            statusAdb.Text = "● ADB"; statusAdb.ForeColor = s.Android == "ADB conectado" ? CGreen : s.Android == "Não autorizado" ? CYellow : CMuted;
            statusIos.Text = "● iOS"; statusIos.ForeColor = s.Ios == "Conectado" ? CGreen : s.Ios == "USB detectado" ? CYellow : CMuted;
            statusSystem.Text = "Sistema: " + s.SystemVersion; statusSystem.ForeColor = s.SystemVersion == "Não identificado" ? CMuted : CText;
        }

        void RefreshQuickIndicators()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                DashboardState s = BuildDashboardState();
                BeginInvoke((MethodInvoker)delegate { ApplyDashboardState(s); });
            });
        }

        void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }

        string ToolPath(string exe)
        {
            string local = Path.Combine(PlatformToolsDir, exe);
            if (File.Exists(local)) return local;

            string[] extras = { Path.Combine(BaseDir, exe), Path.Combine(ToolsDir, exe) };
            foreach (string e in extras) if (File.Exists(e)) return e;

            // WinGet can be installed correctly but missing from the current PATH.
            if (string.Equals(exe, "winget.exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string windowsApps = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Microsoft", "WindowsApps", "winget.exe");
                    if (File.Exists(windowsApps)) return windowsApps;
                }
                catch { }

                try
                {
                    string ps = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "System32", "WindowsPowerShell", "v1.0", "powershell.exe");

                    string query =
                        "-NoProfile -Command \"$p=Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue | " +
                        "Sort-Object Version -Descending | Select-Object -First 1; " +
                        "if($p){$x=Join-Path $p.InstallLocation 'winget.exe'; if(Test-Path $x){Write-Output $x}}\"";

                    string foundWinget = Run(ps, query, 20).Trim();
                    string[] lines = foundWinget.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string candidate = line.Trim();
                        if (candidate.EndsWith("winget.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                            return candidate;
                    }
                }
                catch { }
            }

            try
            {
                if (Directory.Exists(ToolsDir))
                {
                    string found = Directory.GetFiles(ToolsDir, exe, SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }
            catch { }

            string env = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string p in env.Split(';'))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    string f = Path.Combine(p.Trim(), exe);
                    if (File.Exists(f)) return f;
                }
                catch { }
            }
            return null;
        }

        string Run(string file, string args, int timeoutSec)
        {
            if (string.IsNullOrEmpty(file)) return "Executável não encontrado.";
            Process p = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                p = Process.Start(psi);
                if (p == null) return "Não foi possível iniciar o processo.";

                RegisterOwnedProcess(p);

                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();

                if (!p.WaitForExit(timeoutSec * 1000))
                {
                    try { p.Kill(); } catch { }
                    return "TIMEOUT após " + timeoutSec + " segundos.";
                }
                return (stdout + Environment.NewLine + stderr).Trim();
            }
            catch (Exception ex) { return "Erro: " + ex.Message; }
            finally
            {
                UnregisterOwnedProcess(p);
                try { if (p != null) p.Dispose(); } catch { }
            }
        }

        void StartSimple(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            StartSimple("explorer.exe", "\"" + path + "\"");
        }

        void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        List<UsbDevice> GetUsbDevices()
        {
            List<UsbDevice> list = new List<UsbDevice>();
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Name,Manufacturer,Status,ConfigManagerErrorCode,PNPDeviceID FROM Win32_PnPEntity");

                foreach (ManagementObject m in s.Get())
                {
                    string id = Convert.ToString(m["PNPDeviceID"]);
                    string name = Convert.ToString(m["Name"]);
                    if (string.IsNullOrEmpty(id)) continue;

                    bool usb = id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase);
                    int code = 0;
                    try { code = Convert.ToInt32(m["ConfigManagerErrorCode"]); } catch { }

                    bool relevant =
                        name.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("ADB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Fastboot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("DFU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Portable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Samsung", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Xiaomi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Motorola", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("OPPO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Realme", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("vivo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Huawei", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Qualcomm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MediaTek", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("ZTE", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (usb && (relevant || code != 0 || KnownVid(id)))
                    {
                        list.Add(new UsbDevice
                        {
                            Name = string.IsNullOrWhiteSpace(name) ? "Dispositivo USB" : name,
                            Manufacturer = Convert.ToString(m["Manufacturer"]),
                            Status = Convert.ToString(m["Status"]),
                            ErrorCode = code,
                            PnpId = id
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        bool KnownVid(string id)
        {
            string u = (id ?? "").ToUpperInvariant();
            string[] vids = { "VID_05AC", "VID_18D1", "VID_04E8", "VID_2717", "VID_22B8", "VID_17EF", "VID_12D1", "VID_0FCE", "VID_2A70", "VID_1004", "VID_0BB4", "VID_22D9", "VID_2D95", "VID_0B05", "VID_19D2", "VID_1949", "VID_05C6", "VID_0E8D", "VID_1782" };
            return vids.Any(v => u.Contains(v));
        }

        string VendorFromId(string id)
        {
            string u = (id ?? "").ToUpperInvariant();
            if (u.Contains("VID_05AC")) return "Apple";
            if (u.Contains("VID_18D1")) return "Google / Android";
            if (u.Contains("VID_04E8")) return "Samsung";
            if (u.Contains("VID_2717")) return "Xiaomi";
            if (u.Contains("VID_22B8")) return "Motorola / Lenovo";
            if (u.Contains("VID_17EF")) return "Lenovo";
            if (u.Contains("VID_12D1")) return "Huawei";
            if (u.Contains("VID_0FCE")) return "Sony";
            if (u.Contains("VID_2A70")) return "OnePlus";
            if (u.Contains("VID_1004")) return "LG";
            if (u.Contains("VID_0BB4")) return "HTC";
            if (u.Contains("VID_22D9")) return "OPPO / Realme";
            if (u.Contains("VID_2D95")) return "vivo";
            if (u.Contains("VID_0B05")) return "ASUS";
            if (u.Contains("VID_19D2")) return "ZTE";
            if (u.Contains("VID_1949")) return "Amazon";
            if (u.Contains("VID_05C6")) return "Qualcomm";
            if (u.Contains("VID_0E8D")) return "MediaTek";
            if (u.Contains("VID_1782")) return "UNISOC / Spreadtrum";
            return "Fabricante não mapeado";
        }

        bool IsApple(UsbDevice d)
        {
            return (d.PnpId ?? "").ToUpperInvariant().Contains("VID_05AC") ||
                   d.Name.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   d.Name.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        UsbDevice ChoosePrimaryDevice(List<UsbDevice> list)
        {
            if (list == null || list.Count == 0) return null;

            UsbDevice mobile = list.FirstOrDefault(x =>
                x.Name.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("MTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("ADB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("Fastboot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("DFU", StringComparison.OrdinalIgnoreCase) >= 0);

            return mobile ?? list[0];
        }

        string InferMode(UsbDevice d, string adb, string fb, string ios)
        {
            if (d == null) return "—";
            if (d.Name.IndexOf("DFU", StringComparison.OrdinalIgnoreCase) >= 0) return "DFU";
            if (d.Name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0) return "Recovery";
            if (fb == "FASTBOOT CONECTADO") return "Fastboot";
            if (adb == "ADB CONECTADO") return "ADB";
            if (adb == "ADB NÃO AUTORIZADO") return "ADB não autorizado";
            if (ios == "IPHONE AUTORIZADO") return "iOS autorizado";
            if (d.Name.IndexOf("MTP", StringComparison.OrdinalIgnoreCase) >= 0) return "MTP";
            return "USB";
        }

        string AdbState()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null) return "ADB AUSENTE";
            string x = Run(adb, "devices -l", 10);
            if (x.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0) return "ADB NÃO AUTORIZADO";
            if (x.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0) return "ADB OFFLINE";
            string[] lines = x.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
                if (line.IndexOf("\tdevice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf(" device ", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "ADB CONECTADO";
            return "ADB SEM DISPOSITIVO";
        }

        string FastbootState()
        {
            string fb = ToolPath("fastboot.exe");
            if (fb == null) return "FASTBOOT AUSENTE";
            string x = Run(fb, "devices", 10);
            return string.IsNullOrWhiteSpace(x) ? "FASTBOOT SEM DISPOSITIVO" : "FASTBOOT CONECTADO";
        }

        string IosState()
        {
            string id = ToolPath("idevice_id.exe");
            if (id == null) return "LIBIMOBILEDEVICE AUSENTE";
            string x = Run(id, "-l", 10);
            return string.IsNullOrWhiteSpace(x) ? "IPHONE NÃO AUTORIZADO / NÃO DETECTADO" : "IPHONE AUTORIZADO";
        }

        AssistantResult BuildSmartAssistant(List<UsbDevice> devices, string adb, string fb, string ios)
        {
            AssistantResult r = new AssistantResult();
            UsbDevice primary = ChoosePrimaryDevice(devices);

            if (devices.Count == 0)
            {
                r.Severity = "Atenção";
                r.Title = "Nenhum telefone foi enumerado pelo Windows";
                r.Text = "Solução recomendada:\r\n" +
                         "1. Use um cabo USB de dados, não apenas carga.\r\n" +
                         "2. Teste outra porta USB.\r\n" +
                         "3. Desbloqueie o aparelho.\r\n" +
                         "4. Em Android, selecione transferência de arquivos/MTP.\r\n" +
                         "5. Em iPhone, verifique se Apple Devices/drivers Apple estão instalados.";
                return r;
            }

            UsbDevice bad = devices.FirstOrDefault(x => x.ErrorCode != 0);
            if (bad != null)
            {
                string vendor = VendorFromId(bad.PnpId);
                r.Severity = "Crítico";
                r.Title = "Falha de driver detectada: " + vendor;
                r.Text = "O Windows enumerou o dispositivo, mas retornou código PnP " + bad.ErrorCode + " (" + PnpCodeDescription(bad.ErrorCode) + ").\r\n\r\n" +
                         PnpCodeRecommendation(bad.ErrorCode, vendor);
                return r;
            }

            string pVendor = primary != null ? VendorFromId(primary.PnpId) : "Desconhecido";

            if (pVendor == "Samsung" && adb == "ADB SEM DISPOSITIVO")
            {
                r.Severity = "Atenção";
                r.Title = "Driver Samsung ADB ausente ou Depuração USB desativada";
                r.Text = "Solução recomendada:\r\n" +
                         "1. Confirme se o telefone aparece por MTP.\r\n" +
                         "2. Ative Opções do desenvolvedor > Depuração USB.\r\n" +
                         "3. Instale/atualize o driver USB oficial da Samsung.\r\n" +
                         "4. Desconecte e conecte novamente.\r\n" +
                         "5. Aceite a chave RSA no telefone.";
                return r;
            }

            if ((pVendor == "Xiaomi" || pVendor == "Motorola / Lenovo" || pVendor == "Lenovo" || pVendor == "Huawei" ||
                 pVendor == "Google / Android" || pVendor == "OPPO / Realme" || pVendor == "vivo" || pVendor == "ASUS" ||
                 pVendor == "ZTE" || pVendor == "Sony" || pVendor == "OnePlus" || pVendor == "LG" || pVendor == "HTC" ||
                 pVendor == "Amazon") && adb == "ADB SEM DISPOSITIVO")
            {
                r.Severity = "Atenção";
                r.Title = "Android detectado, mas ADB não está disponível";
                r.Text = VendorDriverRecommendation(pVendor, false) +
                         "\r\n\r\nTambém confirme Depuração USB e a autorização RSA.";
                return r;
            }

            if (adb == "ADB NÃO AUTORIZADO")
            {
                r.Severity = "Atenção";
                r.Title = "Android detectado, aguardando autorização";
                r.Text = "O ADB já reconheceu o aparelho, mas o PC ainda não foi autorizado.\r\n\r\n" +
                         "Solução: desbloqueie o telefone e aceite a janela 'Permitir depuração USB'.";
                return r;
            }

            if (adb == "ADB OFFLINE")
            {
                r.Severity = "Atenção";
                r.Title = "ADB está offline";
                r.Text = "Solução recomendada:\r\n" +
                         "1. Reinicie o servidor ADB.\r\n" +
                         "2. Reconecte o cabo.\r\n" +
                         "3. Revogue autorizações de depuração USB no Android e autorize novamente.";
                return r;
            }

            if (fb == "FASTBOOT CONECTADO")
            {
                r.Severity = "OK";
                r.Title = "Android em modo Fastboot";
                r.Text = "O dispositivo está sendo reconhecido corretamente em Fastboot. Use apenas comandos compatíveis com o fabricante e o estado do bootloader.";
                return r;
            }

            if (adb == "ADB CONECTADO")
            {
                r.Severity = "OK";
                r.Title = "Android conectado e autorizado";
                r.Text = "ADB está operacional. Informações, captura de tela e backup DCIM podem ser usados.";
                return r;
            }

            bool apple = devices.Any(IsApple);
            if (apple && ios == "LIBIMOBILEDEVICE AUSENTE")
            {
                r.Severity = "Atenção";
                r.Title = "iPhone detectado, componente iOS avançado ausente";
                r.Text = "O Windows está vendo hardware Apple.\r\n\r\n" +
                         "Solução recomendada:\r\n" +
                         "1. Instale Apple Devices/drivers Apple.\r\n" +
                         "2. Para ideviceinfo/backup avançado, instale libimobiledevice de forma confiável.\r\n" +
                         "3. Desbloqueie o iPhone e escolha 'Confiar neste computador'.";
                return r;
            }

            if (apple && ios != "IPHONE AUTORIZADO")
            {
                r.Severity = "Atenção";
                r.Title = "iPhone detectado, mas não autorizado";
                r.Text = "O USB Apple está presente, porém a comunicação iOS autorizada não foi estabelecida.\r\n\r\n" +
                         "Desbloqueie o iPhone, toque em 'Confiar' e confirme o código do aparelho.";
                return r;
            }

            if (ios == "IPHONE AUTORIZADO")
            {
                r.Severity = "OK";
                r.Title = "iPhone conectado e autorizado";
                r.Text = "A comunicação de nível iOS está disponível para informações e backup compatível.";
                return r;
            }

            if (primary != null && primary.Name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                r.Severity = "OK";
                r.Title = "Modo Recovery detectado";
                r.Text = "O Windows reconheceu o dispositivo em Recovery. Use Apple Devices ou o procedimento oficial do fabricante.";
                return r;
            }

            if (primary != null && primary.Name.IndexOf("DFU", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                r.Severity = "OK";
                r.Title = "Modo DFU detectado";
                r.Text = "O Windows reconheceu o dispositivo em DFU. Nesse modo, as informações disponíveis são limitadas.";
                return r;
            }

            r.Severity = "OK";
            r.Title = "USB detectado";
            r.Text = "O Windows reconheceu o dispositivo sem erro PnP. Se uma função específica não estiver disponível, verifique o modo USB, autorização e dependências.";
            return r;
        }

        string VendorDriverRecommendation(string vendor, bool pnpError)
        {
            StringBuilder sb = new StringBuilder();

            if (vendor == "Samsung")
                sb.Append("Instale/atualize o Samsung Android USB Driver, confirme Depuração USB e reconecte o aparelho.");
            else if (vendor == "Xiaomi")
                sb.Append("Atualize o driver ADB/USB da Xiaomi, confirme MTP/Depuração USB e reconecte.");
            else if (vendor == "Motorola / Lenovo" || vendor == "Lenovo")
                sb.Append("Atualize o driver USB/ADB da Motorola/Lenovo e confirme Depuração USB.");
            else if (vendor == "Huawei")
                sb.Append("Atualize os drivers USB da Huawei e confirme a interface de dados/depuração.");
            else if (vendor == "OPPO / Realme")
                sb.Append("Atualize o driver USB/ADB do OPPO/Realme, confirme MTP e Depuração USB.");
            else if (vendor == "vivo")
                sb.Append("Atualize o driver USB/ADB da vivo e confirme MTP/Depuração USB.");
            else if (vendor == "ASUS")
                sb.Append("Atualize o driver USB/ADB da ASUS e confirme a Depuração USB.");
            else if (vendor == "ZTE")
                sb.Append("Atualize o driver USB da ZTE e confirme o modo USB de dados.");
            else if (vendor == "Sony")
                sb.Append("Atualize o driver USB da Sony e confirme o modo de transferência/ADB.");
            else if (vendor == "OnePlus")
                sb.Append("Atualize o driver USB/ADB da OnePlus e confirme Depuração USB.");
            else if (vendor == "LG")
                sb.Append("Atualize o driver USB da LG e reconecte o aparelho.");
            else if (vendor == "Google / Android")
                sb.Append("Instale o Android Platform Tools e confirme um driver ADB compatível com o aparelho.");
            else if (vendor == "Apple")
                sb.Append("Instale/atualize Apple Devices e o driver Apple Mobile Device. Depois desbloqueie o iPhone e toque em Confiar.");
            else if (vendor == "Qualcomm")
                sb.Append("Interface Qualcomm detectada. Verifique o driver Qualcomm correspondente ao modo atual do aparelho e a documentação do fabricante.");
            else if (vendor == "MediaTek")
                sb.Append("Interface MediaTek detectada. Verifique o driver USB correspondente ao modo atual e a documentação do fabricante.");
            else if (vendor == "UNISOC / Spreadtrum")
                sb.Append("Interface UNISOC/Spreadtrum detectada. Instale o driver oficial/compatível do fabricante do aparelho.");
            else
                sb.Append("Atualize o driver USB oficial do fabricante e teste outro cabo/porta USB.");

            if (pnpError)
                sb.Append("\r\nDepois, use 'Reexaminar hardware'. Se o erro continuar, abra o Gerenciador de Dispositivos e revise apenas a interface com problema.");

            return sb.ToString();
        }

        string GetSmartAssistantReport()
        {
            List<UsbDevice> devices = GetUsbDevices();
            string adb = AdbState();
            string fb = FastbootState();
            string ios = IosState();
            AssistantResult r = BuildSmartAssistant(devices, adb, fb, ios);

            return "ASSISTENTE AUTOMÁTICO\r\n=====================\r\n" +
                   "Severidade: " + r.Severity + "\r\n" +
                   "Diagnóstico: " + r.Title + "\r\n\r\n" + r.Text;
        }

        string GetDriverDiagnosis()
        {
            List<UsbDevice> devices = GetUsbDevices();
            string adb = AdbState();
            string fb = FastbootState();
            string ios = IosState();
            string system = DetectSystemVersion(adb, ios, devices);
            AssistantResult smart = BuildSmartAssistant(devices, adb, fb, ios);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DIAGNÓSTICO AUTOMÁTICO - BEBEL 155 v5");
            sb.AppendLine("=====================================");
            sb.AppendLine("Data: " + DateTime.Now);
            sb.AppendLine("Sistema detectado: " + system);
            sb.AppendLine();

            if (devices.Count == 0)
            {
                sb.AppendLine("Nenhum telefone/interface relevante foi enumerado pelo Windows.");
            }
            else
            {
                sb.AppendLine("HARDWARE / DRIVERS");
                sb.AppendLine("------------------");
                foreach (UsbDevice d in devices)
                {
                    sb.AppendLine("- " + d.Name);
                    sb.AppendLine("  Fabricante: " + VendorFromId(d.PnpId));
                    sb.AppendLine("  Status: " + d.Status);
                    sb.AppendLine("  Código PnP: " + d.ErrorCode + " - " + PnpCodeDescription(d.ErrorCode));
                    sb.AppendLine("  ID: " + d.PnpId);
                    if (d.ErrorCode != 0)
                        sb.AppendLine("  Solução: " + PnpCodeRecommendation(d.ErrorCode, VendorFromId(d.PnpId)));
                }
            }

            sb.AppendLine();
            sb.AppendLine("ANDROID");
            sb.AppendLine("-------");
            sb.AppendLine(adb);
            sb.AppendLine(fb);

            sb.AppendLine();
            sb.AppendLine("IPHONE / IOS");
            sb.AppendLine("------------");
            sb.AppendLine(ios);
            sb.AppendLine("Apple Mobile Device: " + GetAppleServiceStatus());

            sb.AppendLine();
            sb.AppendLine("ASSISTENTE");
            sb.AppendLine("----------");
            sb.AppendLine(smart.Title);
            sb.AppendLine(smart.Text);

            return sb.ToString();
        }

        string FirstNonEmptyAndroidProp(string adb, params string[] props)
        {
            foreach (string prop in props)
            {
                string value = Run(adb, "shell getprop " + prop, 8).Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)) continue;
                return value;
            }
            return "";
        }

        string GetExactAndroidModel(string adb)
        {
            // Prefer names explicitly reported by the connected device.
            string market = FirstNonEmptyAndroidProp(adb,
                "ro.product.marketname",
                "ro.product.vendor.marketname",
                "ro.product.odm.marketname");

            string model = FirstNonEmptyAndroidProp(adb,
                "ro.product.model",
                "ro.product.vendor.model",
                "ro.product.product.model",
                "ro.product.system.model");

            if (!string.IsNullOrWhiteSpace(market) && !string.Equals(market, model, StringComparison.OrdinalIgnoreCase))
                return market + " (" + model + ")";

            if (!string.IsNullOrWhiteSpace(model)) return model;

            // Do not guess a commercial model from chipset/USB VID.
            return "Android (modelo exato não informado pelo aparelho)";
        }

        string GetExactAndroidManufacturer(string adb)
        {
            string m = FirstNonEmptyAndroidProp(adb,
                "ro.product.manufacturer",
                "ro.product.vendor.manufacturer",
                "ro.product.product.manufacturer");
            return string.IsNullOrWhiteSpace(m) ? "Android" : m;
        }

        string ResolveAppleMarketingName(string productType)
        {
            if (string.IsNullOrWhiteSpace(productType)) return "";

            // ProductType is generic hardware data (e.g. iPhone15,2); no serial/UDID is sent.
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string url = "https://api.ipsw.me/v4/device/" + Uri.EscapeDataString(productType.Trim());
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5.1.3");
                    string json = wc.DownloadString(url);
                    string name = JsonStringValue(json, "name");
                    if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                }
            }
            catch { }

            // If online resolution fails, returning the technical identifier is safer than guessing.
            return productType.Trim();
        }

        string GetExactAppleModel(string infoExe)
        {
            if (string.IsNullOrWhiteSpace(infoExe)) return "iPhone / iPad";
            string productType = Run(infoExe, "-k ProductType", 10).Trim();
            if (string.IsNullOrWhiteSpace(productType)) return "iPhone / iPad";
            return ResolveAppleMarketingName(productType);
        }

        string GetExactConnectedModel()
        {
            if (AdbState() == "ADB CONECTADO")
            {
                string adb = ToolPath("adb.exe");
                return GetExactAndroidModel(adb);
            }

            if (IosState() == "IPHONE AUTORIZADO")
            {
                string info = ToolPath("ideviceinfo.exe");
                string exact = GetExactAppleModel(info);
                string productType = Run(info, "-k ProductType", 10).Trim();
                return string.IsNullOrWhiteSpace(productType) || string.Equals(exact, productType, StringComparison.OrdinalIgnoreCase)
                    ? exact
                    : exact + " (" + productType + ")";
            }

            List<UsbDevice> devices = GetUsbDevices();
            UsbDevice primary = ChoosePrimaryDevice(devices);
            if (primary == null) return "Nenhum dispositivo";

            if (IsApple(primary))
                return "iPhone/iPad detectado • modelo exato requer identificação iOS";

            return primary.Name + " • modelo exato requer ADB";
        }

        void RepairWingetInteractive()
        {
            string existing = ToolPath("winget.exe");
            if (!string.IsNullOrWhiteSpace(existing))
            {
                outputs["Ferramentas"].Text =
                    "WinGet encontrado.\r\n\r\nCaminho:\r\n" + existing +
                    "\r\n\r\nVersão:\r\n" + Run(existing, "--version", 30);
                return;
            }

            DialogResult r = MessageBox.Show(
                "O WinGet não foi localizado.\r\n\r\n" +
                "Ele é distribuído pelo componente App Installer do Windows.\r\n" +
                "O Bebel 155 pode abrir a Microsoft Store na busca por App Installer.\r\n\r\n" +
                "Depois de instalar/atualizar o App Installer, feche e abra novamente o Bebel 155.\r\n\r\nAbrir a Microsoft Store agora?",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (r == DialogResult.Yes)
            {
                OpenUrl("ms-windows-store://search/?query=App%20Installer");
                outputs["Ferramentas"].Text =
                    "Microsoft Store aberta para App Installer.\r\n\r\n" +
                    "Instale/atualize o App Installer. Em seguida, reinicie o Bebel 155 e clique em Verificar dependências.";
            }
            else
            {
                outputs["Ferramentas"].Text =
                    "WinGet continua ausente.\r\n\r\n" +
                    "Você pode instalar/atualizar o App Installer pela Microsoft Store e reabrir este programa.";
            }
        }

        string NormalizeIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "");
        }

        bool ValuesConsistent(IEnumerable<string> values)
        {
            List<string> vals = values.Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x.Trim(), "unknown", StringComparison.OrdinalIgnoreCase)).Select(NormalizeIdentity).Where(x => x.Length > 0).Distinct().ToList();
            return vals.Count <= 1;
        }

        string AndroidCodename(string adb)
        {
            return FirstNonEmptyAndroidProp(adb, "ro.product.device", "ro.product.vendor.device", "ro.product.product.device", "ro.build.product");
        }

        string GetIdentityConsistencyReport()
        {
            if (AdbState() == "ADB CONECTADO")
            {
                string adb = ToolPath("adb.exe");
                string[] models = { Run(adb,"shell getprop ro.product.model",8).Trim(), Run(adb,"shell getprop ro.product.vendor.model",8).Trim(), Run(adb,"shell getprop ro.product.product.model",8).Trim(), Run(adb,"shell getprop ro.product.system.model",8).Trim() };
                string[] devices = { Run(adb,"shell getprop ro.product.device",8).Trim(), Run(adb,"shell getprop ro.product.vendor.device",8).Trim(), Run(adb,"shell getprop ro.product.product.device",8).Trim(), Run(adb,"shell getprop ro.build.product",8).Trim() };
                string[] makers = { Run(adb,"shell getprop ro.product.manufacturer",8).Trim(), Run(adb,"shell getprop ro.product.vendor.manufacturer",8).Trim(), Run(adb,"shell getprop ro.product.product.manufacturer",8).Trim() };
                bool modelOk=ValuesConsistent(models), deviceOk=ValuesConsistent(devices), makerOk=ValuesConsistent(makers);
                StringBuilder sb=new StringBuilder();
                sb.AppendLine("COERÊNCIA DE IDENTIDADE - ANDROID"); sb.AppendLine("================================");
                sb.AppendLine("Modelo reportado: "+GetExactAndroidModel(adb)); sb.AppendLine("Codinome principal: "+AndroidCodename(adb)); sb.AppendLine();
                sb.AppendLine("Campos de modelo: "+(modelOk?"COERENTES":"INCONSISTENTES")); foreach(string x in models.Where(x=>!string.IsNullOrWhiteSpace(x))) sb.AppendLine("  - "+x);
                sb.AppendLine("Campos de codinome: "+(deviceOk?"COERENTES":"INCONSISTENTES")); foreach(string x in devices.Where(x=>!string.IsNullOrWhiteSpace(x))) sb.AppendLine("  - "+x);
                sb.AppendLine("Fabricante: "+(makerOk?"COERENTE":"INCONSISTENTE")); foreach(string x in makers.Where(x=>!string.IsNullOrWhiteSpace(x))) sb.AppendLine("  - "+x);
                sb.AppendLine(); sb.AppendLine((modelOk&&deviceOk&&makerOk)?"RESULTADO: identidade interna coerente.":"RESULTADO: propriedades divergentes; os valores reais foram preservados.");
                return sb.ToString();
            }
            if (IosState() == "IPHONE AUTORIZADO")
            {
                string info=ToolPath("ideviceinfo.exe"); string pt=Run(info,"-k ProductType",10).Trim(); string hw=Run(info,"-k HardwareModel",10).Trim(); string mn=Run(info,"-k ModelNumber",10).Trim();
                string commercial=ResolveAppleMarketingName(pt), expectedBoard="", expectedIdentifier="";
                try { using(WebClient wc=new WebClient()) { wc.Headers.Add("User-Agent","BebelEquipe155-v5.1.9"); string j=wc.DownloadString("https://ipsw.info/api/v1/device/"+Uri.EscapeDataString(pt)); expectedBoard=JsonStringValue(j,"boardconfig"); if(!string.IsNullOrWhiteSpace(mn)){ try { string mj=wc.DownloadString("https://ipsw.info/api/v1/model/"+Uri.EscapeDataString(mn)); expectedIdentifier=JsonStringValue(mj,"identifier"); } catch{} } } } catch{}
                bool boardOk=string.IsNullOrWhiteSpace(expectedBoard)||string.IsNullOrWhiteSpace(hw)||NormalizeIdentity(expectedBoard)==NormalizeIdentity(hw);
                bool skuOk=string.IsNullOrWhiteSpace(expectedIdentifier)||string.Equals(expectedIdentifier,pt,StringComparison.OrdinalIgnoreCase);
                StringBuilder sb=new StringBuilder(); sb.AppendLine("COERÊNCIA DE IDENTIDADE - IPHONE / IOS"); sb.AppendLine("======================================"); sb.AppendLine("Modelo comercial: "+commercial); sb.AppendLine("ProductType: "+pt); sb.AppendLine("HardwareModel: "+hw); sb.AppendLine("ModelNumber: "+mn); sb.AppendLine("ProductType × board config: "+(boardOk?"COERENTE":"INCONSISTENTE")); if(!string.IsNullOrWhiteSpace(expectedBoard)) sb.AppendLine("Board esperado: "+expectedBoard); sb.AppendLine("ModelNumber × ProductType: "+(skuOk?"COERENTE / SEM CONFLITO":"INCONSISTENTE")); if(!string.IsNullOrWhiteSpace(expectedIdentifier)) sb.AppendLine("Identificador esperado pelo SKU: "+expectedIdentifier); sb.AppendLine(); sb.AppendLine((boardOk&&skuOk)?"RESULTADO: nenhum conflito conhecido.":"RESULTADO: identificadores divergentes; valores reais preservados."); return sb.ToString();
            }
            return "A verificação exige Android autorizado via ADB ou iPhone autorizado via ideviceinfo.";
        }

        string GetIosBatteryHealthDetails(string info)
        {
            StringBuilder sb=new StringBuilder(); string pct=Run(info,"-q com.apple.mobile.battery -k BatteryCurrentCapacity",10).Trim(); string charging=Run(info,"-q com.apple.mobile.battery -k BatteryIsCharging",10).Trim();
            sb.AppendLine("Carga atual: "+(string.IsNullOrWhiteSpace(pct)?"não disponível":pct+"%")); sb.AppendLine("Carregando: "+(string.IsNullOrWhiteSpace(charging)?"não disponível":charging));
            string diag=ToolPath("idevicediagnostics.exe"); if(diag!=null){ string raw=Run(diag,"ioregentry AppleSmartBattery",30); Match cycle=Regex.Match(raw,@"<key>CycleCount</key>\s*<integer>(\d+)</integer>",RegexOptions.IgnoreCase); Match health=Regex.Match(raw,@"<key>(?:MaximumCapacityPercent|MaxCapacity)</key>\s*<integer>(\d+)</integer>",RegexOptions.IgnoreCase); if(cycle.Success) sb.AppendLine("Ciclos: "+cycle.Groups[1].Value); if(health.Success) sb.AppendLine("Saúde/capacidade máxima: "+health.Groups[1].Value+"%"); if(!cycle.Success&&!health.Success) sb.AppendLine("Saúde máxima/ciclos: não expostos nesta versão."); } else sb.AppendLine("Saúde máxima/ciclos: idevicediagnostics não disponível."); return sb.ToString();
        }

        string GetIosHardwareDetails()
        {
            string info=ToolPath("ideviceinfo.exe"), id=ToolPath("idevice_id.exe"); if(info==null||id==null) return "iOS Tools ausentes."; if(string.IsNullOrWhiteSpace(Run(id,"-l",10))) return "iPhone/iPad não autorizado.";
            Func<string,string> key=delegate(string k){return Run(info,"-k "+k,10).Trim();}; Func<string,string,string> q=delegate(string d,string k){return Run(info,"-q "+d+" -k "+k,10).Trim();};
            string pt=key("ProductType"), model=ResolveAppleMarketingName(pt), dc=key("DeviceColor"), ec=key("EnclosureColor"), mn=key("ModelNumber"), region=key("RegionInfo"); long total=ParseLongSafe(q("com.apple.disk_usage","TotalDiskCapacity")), free=ParseLongSafe(q("com.apple.disk_usage","AmountDataAvailable"));
            StringBuilder sb=new StringBuilder(); sb.AppendLine("IPHONE / IOS - ESPECIFICAÇÕES"); sb.AppendLine("============================="); sb.AppendLine("Modelo: "+model); sb.AppendLine("ProductType: "+pt); sb.AppendLine("Número do modelo / SKU: "+(string.IsNullOrWhiteSpace(mn)?"não disponível":mn)); sb.AppendLine("Região: "+(string.IsNullOrWhiteSpace(region)?"não disponível":region)); sb.AppendLine("Cor do dispositivo: "+(string.IsNullOrWhiteSpace(dc)?"não disponível":dc)); sb.AppendLine("Cor do gabinete: "+(string.IsNullOrWhiteSpace(ec)?"não disponível":ec)); sb.AppendLine("Capacidade total: "+(total>=0?FormatBytes(total):"não disponível")); sb.AppendLine("Espaço livre: "+(free>=0?FormatBytes(free):"não disponível")); sb.AppendLine(); sb.AppendLine("BATERIA"); sb.AppendLine("-------"); sb.AppendLine(GetIosBatteryHealthDetails(info)); return sb.ToString();
        }

        string GithubRawUrl(string path){ string repo=ReadSetting("github_repo","Bebel-155/bebel157").Trim(); return "https://raw.githubusercontent.com/"+repo+"/main/"+path.TrimStart('/'); }
        string ResolveAndroidRenderUrl(string model,string codename)
        {
            try { using(WebClient wc=new WebClient()){ wc.Headers.Add("User-Agent","BebelEquipe155-v5.1.9"); string json=wc.DownloadString(GithubRawUrl("device-images.json")); foreach(Match obj in Regex.Matches(json,@"\{[^{}]*\}",RegexOptions.Singleline)){ string match=JsonStringValue(obj.Value,"match"), c=JsonStringValue(obj.Value,"codename"), image=JsonStringValue(obj.Value,"image"); bool mm=!string.IsNullOrWhiteSpace(match)&&(NormalizeIdentity(model).Contains(NormalizeIdentity(match))||NormalizeIdentity(match).Contains(NormalizeIdentity(model))); bool cm=!string.IsNullOrWhiteSpace(c)&&NormalizeIdentity(c)==NormalizeIdentity(codename); if((mm||cm)&&Uri.IsWellFormedUriString(image,UriKind.Absolute)) return image; } } } catch{} return "";
        }
        DeviceRenderData GetDeviceRenderData()
        {
            DeviceRenderData d=new DeviceRenderData(); if(AdbState()=="ADB CONECTADO"){ string adb=ToolPath("adb.exe"), model=GetExactAndroidModel(adb), codename=AndroidCodename(adb); d.Key="android|"+model+"|"+codename; d.Caption=model; d.Url=ResolveAndroidRenderUrl(model,codename); d.Source=string.IsNullOrWhiteSpace(d.Url)?"Imagem não cadastrada":"Render do repositório"; return d; }
            if(IosState()=="IPHONE AUTORIZADO"){ string info=ToolPath("ideviceinfo.exe"), pt=Run(info,"-k ProductType",10).Trim(); d.Key="apple|"+pt; d.Caption=ResolveAppleMarketingName(pt); try{ using(WebClient wc=new WebClient()){ wc.Headers.Add("User-Agent","BebelEquipe155-v5.1.9"); string json=wc.DownloadString("https://ipsw.info/api/v1/device/"+Uri.EscapeDataString(pt)); d.Url=JsonStringValue(json,"apple_image"); if(string.IsNullOrWhiteSpace(d.Url)) d.Url=JsonStringValue(json,"imageurl"); } } catch{} d.Source=string.IsNullOrWhiteSpace(d.Url)?"Imagem indisponível":"Render público do modelo"; return d; }
            d.Key="none"; d.Caption="B155"; d.Source="Autorize o aparelho para identificar"; return d;
        }
        void RefreshDeviceRenderAsync()
        {
            if(deviceRenderBox==null||deviceRenderStatus==null) return; ThreadPool.QueueUserWorkItem(delegate{ DeviceRenderData d=GetDeviceRenderData(); if(d.Key==lastDeviceRenderKey) return; Image img=null; if(!string.IsNullOrWhiteSpace(d.Url)){ try{ using(WebClient wc=new WebClient()) using(MemoryStream ms=new MemoryStream(wc.DownloadData(d.Url))) using(Image temp=Image.FromStream(ms)) img=new Bitmap(temp); }catch{} } if(img==null) img=BrandResources.Load("Bebel155.Logo"); BeginInvoke((MethodInvoker)delegate{ Image old=deviceRenderBox.Image; deviceRenderBox.Image=img; deviceRenderStatus.Text=d.Caption+" • "+d.Source; lastDeviceRenderKey=d.Key; try{if(old!=null&&old!=img)old.Dispose();}catch{} }); });
        }

        string GetAndroidInfo()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null) return "ADB não instalado. Abra Ferramentas > Instalar ADB/Fastboot.";

            string list = Run(adb, "devices -l", 10);
            if (list.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Android detectado, porém NÃO AUTORIZADO.\r\nDesbloqueie o aparelho e aceite a autorização RSA.\r\n\r\n" + list;

            if (AdbState() != "ADB CONECTADO")
                return "Nenhum Android conectado via ADB.\r\n\r\n" + list;

            string exactManufacturer = GetExactAndroidManufacturer(adb);
            string exactModel = GetExactAndroidModel(adb);

            string[,] props = {
                {"Codinome","ro.product.device"},
                {"Android","ro.build.version.release"},
                {"SDK","ro.build.version.sdk"},
                {"Build","ro.build.display.id"},
                {"Patch de segurança","ro.build.version.security_patch"},
                {"ABI","ro.product.cpu.abi"},
                {"Bootloader","ro.bootloader"}
            };

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ANDROID - INFORMAÇÕES");
            sb.AppendLine("=====================");
            sb.AppendLine("Fabricante: " + exactManufacturer);
            sb.AppendLine("Modelo reportado pelo aparelho: " + exactModel);
            for (int i = 0; i < props.GetLength(0); i++)
                sb.AppendLine(props[i, 0] + ": " + Run(adb, "shell getprop " + props[i, 1], 8).Trim());

            sb.AppendLine("Serial: " + Run(adb, "get-serialno", 8).Trim());
            sb.AppendLine();
            sb.AppendLine("BATERIA");
            sb.AppendLine("-------");
            sb.AppendLine(Run(adb, "shell dumpsys battery", 15));
            sb.AppendLine();
            sb.AppendLine("ARMAZENAMENTO");
            sb.AppendLine("-------------");
            sb.AppendLine(Run(adb, "shell df -h /data", 15));
            return sb.ToString();
        }

        string GetFastbootInfo()
        {
            string fb = ToolPath("fastboot.exe");
            if (fb == null) return "Fastboot não instalado.";

            string d = Run(fb, "devices", 10);
            if (string.IsNullOrWhiteSpace(d)) return "Nenhum dispositivo em Fastboot detectado.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FASTBOOT");
            sb.AppendLine("========");
            sb.AppendLine(d);
            sb.AppendLine();
            sb.AppendLine(Run(fb, "getvar product", 10));
            sb.AppendLine(Run(fb, "getvar version-bootloader", 10));
            sb.AppendLine(Run(fb, "getvar secure", 10));
            return sb.ToString();
        }

        string ExportAndroidApps()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null || AdbState() != "ADB CONECTADO") return "Android não autorizado/conectado via ADB.";

            string raw = Run(adb, "shell pm list packages -3", 60);
            string file = Path.Combine(ExportDir, "Apps_Android_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(file, raw, Encoding.UTF8);
            return "Lista de aplicativos exportada:\r\n" + file + "\r\n\r\n" + raw;
        }

        string CaptureAndroidScreen()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null || AdbState() != "ADB CONECTADO") return "Android não autorizado/conectado via ADB.";

            string remote = "/sdcard/bebel155_screen.png";
            string local = Path.Combine(ScreenshotDir, "Android_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

            Run(adb, "shell screencap -p " + remote, 20);
            string pull = Run(adb, "pull " + remote + " \"" + local + "\"", 60);
            Run(adb, "shell rm " + remote, 10);

            return File.Exists(local)
                ? "Captura salva em:\r\n" + local + "\r\n\r\n" + pull
                : "Não foi possível salvar a captura.\r\n" + pull;
        }

        string BackupAndroidDCIM()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null || AdbState() != "ADB CONECTADO") return "Android não autorizado/conectado via ADB.";

            string dest = Path.Combine(BackupDir, "Android_DCIM_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dest);
            string r = Run(adb, "pull /sdcard/DCIM \"" + dest + "\"", 7200);
            return "Backup DCIM concluído/tentado.\r\nDestino:\r\n" + dest + "\r\n\r\n" + r;
        }

        string RestartAdb()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null) return "ADB não instalado.";

            string a = Run(adb, "kill-server", 15);
            string b = Run(adb, "start-server", 20);
            return "ADB reiniciado.\r\n\r\n" + a + "\r\n" + b;
        }

        void ConfirmAndroidReboot(string mode)
        {
            string label = string.IsNullOrEmpty(mode) ? "reiniciar o aparelho" : "reiniciar em " + mode;
            if (MessageBox.Show("Deseja " + label + " via ADB?\r\n\r\nO aparelho precisa estar autorizado.",
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            RunBackground("Android", "Enviando comando...", delegate
            {
                string adb = ToolPath("adb.exe");
                if (adb == null || AdbState() != "ADB CONECTADO") return "Android não autorizado/conectado via ADB.";
                return Run(adb, string.IsNullOrEmpty(mode) ? "reboot" : "reboot " + mode, 20);
            });
        }

        string GetIOSInfo()
        {
            string info = ToolPath("ideviceinfo.exe");
            string id = ToolPath("idevice_id.exe");
            if (info == null || id == null)
                return "libimobiledevice não instalado.\r\nA detecção USB/Recovery/DFU continua disponível.";

            string ids = Run(id, "-l", 10);
            if (string.IsNullOrWhiteSpace(ids))
                return "Nenhum iPhone autorizado. Desbloqueie o aparelho e escolha 'Confiar neste computador'.";

            string productType = Run(info, "-k ProductType", 10).Trim();
            string marketingName = ResolveAppleMarketingName(productType);

            string[] keys = { "DeviceName", "ProductVersion", "BuildVersion", "SerialNumber", "HardwareModel", "UniqueDeviceID" };
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("IPHONE / IOS");
            sb.AppendLine("============");
            sb.AppendLine("Modelo comercial: " + marketingName);
            sb.AppendLine("ProductType: " + productType);
            foreach (string k in keys)
                sb.AppendLine(k + ": " + Run(info, "-k " + k, 10).Trim());
            sb.AppendLine();
            sb.AppendLine("O nome comercial é resolvido a partir do ProductType real. Se a consulta online falhar, o sistema mostra apenas o identificador técnico em vez de adivinhar.");
            return sb.ToString();
        }

        string GetIDeviceVerification()
        {
            string info = ToolPath("ideviceinfo.exe");
            string id = ToolPath("idevice_id.exe");
            if (info == null || id == null)
                return "Para iDevice Verification detalhado, instale libimobiledevice.";

            if (string.IsNullOrWhiteSpace(Run(id, "-l", 10)))
                return "iPhone não autorizado ou indisponível em modo normal.";

            string[] keys = {
                "DeviceName","ProductType","ProductVersion","BuildVersion","SerialNumber",
                "ActivationState","PasswordProtected","TelephonyCapability","BasebandVersion",
                "SIMStatus","RegionInfo","UniqueDeviceID"
            };

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("IDEVICE VERIFICATION");
            sb.AppendLine("====================");
            foreach (string k in keys)
            {
                string v = Run(info, "-k " + k, 10).Trim();
                if (!string.IsNullOrWhiteSpace(v))
                    sb.AppendLine(k + ": " + v);
            }

            sb.AppendLine();
            sb.AppendLine("ActivationState é apenas um estado reportado pelo aparelho; não remove Activation Lock.");
            return sb.ToString();
        }

        string GetIOSRecoveryDFU()
        {
            List<UsbDevice> list = GetUsbDevices().Where(x =>
                IsApple(x) ||
                x.Name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Name.IndexOf("DFU", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("IPHONE / RECOVERY / DFU");
            sb.AppendLine("=======================");
            if (list.Count == 0) sb.AppendLine("Nenhum hardware Apple relevante encontrado.");

            foreach (UsbDevice x in list)
            {
                sb.AppendLine(x.Name + " | PnP=" + x.ErrorCode);
                sb.AppendLine(x.PnpId);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        string BackupIOS()
        {
            string backup = ToolPath("idevicebackup2.exe");
            if (backup == null) return "idevicebackup2 não instalado.";

            string dest = Path.Combine(BackupDir, "iPhone_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dest);
            string r = Run(backup, "backup \"" + dest + "\"", 14400);
            return "Backup iPhone concluído/tentado.\r\nDestino:\r\n" + dest + "\r\n\r\n" + r;
        }


        string FormatBytes(long bytes)
        {
            if (bytes < 0) return "—";
            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (value >= 1024.0 && i < units.Length - 1)
            {
                value /= 1024.0;
                i++;
            }
            if (i == 0) return ((long)value).ToString() + " " + units[i];
            return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00") + " " + units[i];
        }

        long ParseLongSafe(string value)
        {
            long n;
            return long.TryParse((value ?? "").Trim(), out n) ? n : -1;
        }

        long AndroidMemTotalBytes(string adb)
        {
            string mem = Run(adb, "shell cat /proc/meminfo", 15);
            foreach (string raw in mem.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (!line.StartsWith("MemTotal", StringComparison.OrdinalIgnoreCase)) continue;
                Match m = Regex.Match(line, @"MemTotal:\s*(\d+)\s*kB", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    long kb = ParseLongSafe(m.Groups[1].Value);
                    if (kb >= 0) return kb * 1024L;
                }
            }
            return -1;
        }

        void AndroidStorageBytes(string adb, out long total, out long used, out long free)
        {
            total = used = free = -1;
            string df = Run(adb, "shell df -k /data", 20);
            string[] lines = df.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase)) continue;
                string[] p = Regex.Split(line, @"\s+");
                if (p.Length >= 4)
                {
                    long t = ParseLongSafe(p[1]);
                    long u = ParseLongSafe(p[2]);
                    long f = ParseLongSafe(p[3]);
                    if (t >= 0)
                    {
                        total = t * 1024L;
                        used = u >= 0 ? u * 1024L : -1;
                        free = f >= 0 ? f * 1024L : -1;
                        return;
                    }
                }
            }
        }

        string AndroidBatteryPercent(string adb)
        {
            string data = Run(adb, "shell dumpsys battery", 15);
            Match m = Regex.Match(data, @"(?im)^\s*level:\s*(\d+)");
            return m.Success ? m.Groups[1].Value + "%" : "—";
        }

        string AndroidResolution(string adb)
        {
            string data = Run(adb, "shell wm size", 12);
            Match m = Regex.Match(data, @"(?i)(?:Physical|Override)\s+size:\s*(\d+x\d+)");
            if (!m.Success) m = Regex.Match(data, @"(\d+x\d+)");
            return m.Success ? m.Groups[1].Value : "—";
        }

        DeviceMetrics GetAndroidMetrics()
        {
            DeviceMetrics m = new DeviceMetrics();
            string adb = ToolPath("adb.exe");
            if (adb == null || AdbState() != "ADB CONECTADO")
            {
                m.System = "Android";
                m.Battery = m.Ram = m.Storage = m.Resolution = "Requer ADB";
                return m;
            }

            string version = Run(adb, "shell getprop ro.build.version.release", 8).Trim();
            string sdk = Run(adb, "shell getprop ro.build.version.sdk", 8).Trim();
            m.System = "Android " + version + (string.IsNullOrWhiteSpace(sdk) ? "" : " • SDK " + sdk);
            m.Battery = AndroidBatteryPercent(adb);

            long ram = AndroidMemTotalBytes(adb);
            m.Ram = ram >= 0 ? FormatBytes(ram) : "—";

            long total, used, free;
            AndroidStorageBytes(adb, out total, out used, out free);
            if (total >= 0)
            {
                m.Storage = FormatBytes(used) + " / " + FormatBytes(total);
                m.StorageDetail = "Usado " + FormatBytes(used) + " • Livre " + FormatBytes(free) + " • Total " + FormatBytes(total);
            }
            else m.Storage = "—";

            m.Resolution = AndroidResolution(adb);
            return m;
        }

        DeviceMetrics GetIosMetrics()
        {
            DeviceMetrics m = new DeviceMetrics();
            string info = ToolPath("ideviceinfo.exe");
            string id = ToolPath("idevice_id.exe");
            if (info == null || id == null || string.IsNullOrWhiteSpace(Run(id, "-l", 10)))
            {
                m.System = "iOS";
                m.Battery = m.Ram = m.Storage = m.Resolution = "Requer autorização";
                return m;
            }

            Func<string, string> key = delegate(string k) { return Run(info, "-k " + k, 10).Trim(); };
            Func<string, string, string> qkey = delegate(string q, string k) { return Run(info, "-q " + q + " -k " + k, 10).Trim(); };

            string version = key("ProductVersion");
            m.System = "iOS " + version;

            string bat = qkey("com.apple.mobile.battery", "BatteryCurrentCapacity");
            m.Battery = string.IsNullOrWhiteSpace(bat) ? "—" : bat.Trim() + "%";

            m.Ram = "Não exposta";

            long total = ParseLongSafe(qkey("com.apple.disk_usage", "TotalDiskCapacity"));
            long free = ParseLongSafe(qkey("com.apple.disk_usage", "AmountDataAvailable"));
            if (total >= 0)
            {
                long used = free >= 0 ? Math.Max(0, total - free) : -1;
                m.Storage = used >= 0 ? FormatBytes(used) + " / " + FormatBytes(total) : FormatBytes(total);
                m.StorageDetail = "Total " + FormatBytes(total) + (free >= 0 ? " • Livre " + FormatBytes(free) : "");
            }
            else m.Storage = "—";

            string dims = qkey("com.apple.mobile.iTunes", "ScreenDimensions");
            m.Resolution = string.IsNullOrWhiteSpace(dims) ? "Não exposta" : dims;
            return m;
        }

        void RefreshDetailedInfo(string mode)
        {
            SetBusy(true, "Lendo informações detalhadas...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string details;
                DeviceMetrics metrics;

                if (mode == "android")
                {
                    metrics = GetAndroidMetrics();
                    details = GetDetailedAndroidInfo();
                }
                else if (mode == "ios")
                {
                    metrics = GetIosMetrics();
                    details = GetDetailedIosInfo();
                }
                else
                {
                    if (AdbState() == "ADB CONECTADO")
                    {
                        metrics = GetAndroidMetrics();
                        details = GetDetailedAndroidInfo();
                    }
                    else if (IosState() == "IPHONE AUTORIZADO")
                    {
                        metrics = GetIosMetrics();
                        details = GetDetailedIosInfo();
                    }
                    else
                    {
                        metrics = new DeviceMetrics();
                        metrics.System = "Não identificado";
                        metrics.Battery = metrics.Ram = metrics.Storage = metrics.Resolution = "—";
                        details = GetDetailedDeviceInfo();
                    }
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    infoSystemValue.Text = metrics.System;
                    infoBatteryValue.Text = metrics.Battery;
                    infoRamValue.Text = metrics.Ram;
                    infoStorageValue.Text = metrics.Storage;
                    infoResolutionValue.Text = metrics.Resolution;
                    outputs["Informações"].Text = details +
                        (string.IsNullOrWhiteSpace(metrics.StorageDetail) ? "" : "\r\n\r\nRESUMO DO ARMAZENAMENTO\r\n----------------------\r\n" + metrics.StorageDetail);
                    outputs["Informações"].SelectionStart = 0;
                    SetBusy(false, "Pronto");
                });
            });
        }

        string GetConnectedRecoveryGuide()
        {
            List<UsbDevice> devices = GetUsbDevices();
            UsbDevice primary = ChoosePrimaryDevice(devices);
            if (primary == null)
                return "Nenhum aparelho identificado. Conecte o telefone por USB e execute novamente.";

            string vendor = VendorFromId(primary.PnpId);
            if (IsApple(primary))
                return "Aparelho Apple detectado (" + vendor + ").\r\n\r\n" + GetAppleRecoveryGuide();

            return "Android / fabricante provável: " + vendor + "\r\n\r\n" + GetAndroidRecoveryGuide() +
                   "\r\n\r\nORIENTAÇÃO DO FABRICANTE\r\n------------------------\r\n" + VendorDriverRecommendation(vendor, false);
        }

        string GetAndroidRecoveryGuide()
        {
            return "ANDROID / FRP - RECUPERAÇÃO OFICIAL\r\n" +
                   "=================================\r\n\r\n" +
                   "O Bebel 155 não remove nem contorna FRP. Para um aparelho que é seu:\r\n" +
                   "1. Use a Conta Google que estava sincronizada antes da redefinição.\r\n" +
                   "2. Se esqueceu usuário/senha, use a recuperação oficial da Conta Google.\r\n" +
                   "3. Se a senha foi alterada recentemente, aguarde o período de segurança indicado pelo Google antes de tentar novamente.\r\n" +
                   "4. Em aparelhos corporativos ou gerenciados, procure o administrador.\r\n" +
                   "5. Se necessário, procure o suporte autorizado do fabricante com prova de propriedade.\r\n\r\n" +
                   "Fabricantes abrangidos pelo diagnóstico: Samsung, Google/Pixel, Xiaomi, Motorola/Lenovo, Huawei, Sony, OnePlus, LG, HTC, OPPO/Realme, vivo, ASUS, ZTE, Qualcomm/MediaTek/UNISOC e outros detectados via USB.";
        }

        string GetAppleRecoveryGuide()
        {
            return "APPLE ID / ACTIVATION LOCK - RECUPERAÇÃO OFICIAL\r\n" +
                   "===============================================\r\n\r\n" +
                   "O Bebel 155 não remove nem contorna Apple ID/Activation Lock, nem replica módulos de bypass de ferramentas como Dr.Fone.\r\n" +
                   "Para um dispositivo que é seu:\r\n" +
                   "1. Insira a Conta Apple e a senha usadas na configuração do aparelho.\r\n" +
                   "2. Se esqueceu a conta/senha, use a recuperação oficial da Apple.\r\n" +
                   "3. Se o aparelho não está com você, remova-o da sua conta pelo Buscar/iCloud quando aplicável.\r\n" +
                   "4. Para aparelho de empresa/escola, contate o administrador responsável.\r\n" +
                   "5. Se você possui comprovante de compra, a Apple oferece um fluxo de solicitação de suporte para Activation Lock.\r\n\r\n" +
                   "O painel pode mostrar ActivationState quando o próprio iPhone autorizado expõe esse campo; isso é apenas diagnóstico.";
        }

        string GetRecoverySupportMatrix()
        {
            return "SUPORTE DO MENU DE RECUPERAÇÃO\r\n" +
                   "==============================\r\n\r\n" +
                   "ANDROID:\r\n" +
                   "- Samsung, Google/Pixel, Xiaomi, Motorola/Lenovo, Huawei, Sony, OnePlus, LG, HTC, OPPO/Realme, vivo, ASUS, ZTE e outros.\r\n" +
                   "- O suporte significa: identificação USB, diagnóstico de driver, versão do Android quando ADB é autorizado e orientação de recuperação oficial.\r\n" +
                   "- Não significa bypass universal de FRP.\r\n\r\n" +
                   "IPHONE / IPAD:\r\n" +
                   "- Modelos reconhecidos pelo driver Apple / libimobiledevice quando compatível.\r\n" +
                   "- Informações de iOS, Recovery/DFU e ActivationState quando expostos.\r\n" +
                   "- Recuperação de Apple ID/Activation Lock é feita pelos fluxos oficiais da Apple, não por bypass.\r\n\r\n" +
                   "A disponibilidade de dados varia conforme versão do sistema, drivers, estado do aparelho e autorização.";
        }

        string GetDetailedDeviceInfo()
        {
            string adb = AdbState();
            string ios = IosState();
            if (adb == "ADB CONECTADO") return GetDetailedAndroidInfo();
            if (ios == "IPHONE AUTORIZADO") return GetDetailedIosInfo();

            List<UsbDevice> devices = GetUsbDevices();
            if (devices.Any(x => IsApple(x)))
                return "iPhone/iPad detectado por USB, mas as informações detalhadas exigem o aparelho desbloqueado, a relação de confiança e ideviceinfo.\r\n\r\n" + GetIOSRecoveryDFU();

            if (devices.Count > 0)
                return "Dispositivo detectado por USB, mas sem canal autorizado para informações detalhadas.\r\n\r\n" + GetDriverDiagnosis();

            return "Nenhum aparelho compatível foi detectado.";
        }

        string GetDetailedAndroidInfo()
        {
            string adb = ToolPath("adb.exe");
            if (adb == null) return "ADB ausente. Abra Ferramentas e instale ADB/Fastboot.";
            if (AdbState() != "ADB CONECTADO") return "Android não conectado/autorizado via ADB.";

            string manufacturer = GetExactAndroidManufacturer(adb);
            string model = GetExactAndroidModel(adb);
            string version = Run(adb, "shell getprop ro.build.version.release", 8).Trim();
            string sdk = Run(adb, "shell getprop ro.build.version.sdk", 8).Trim();
            string build = Run(adb, "shell getprop ro.build.display.id", 8).Trim();
            string serial = Run(adb, "get-serialno", 8).Trim();
            string soc = Run(adb, "shell getprop ro.soc.model", 8).Trim();
            if (string.IsNullOrWhiteSpace(soc)) soc = Run(adb, "shell getprop ro.hardware", 8).Trim();
            string abi = Run(adb, "shell getprop ro.product.cpu.abi", 8).Trim();
            string mem = Run(adb, "shell cat /proc/meminfo", 15);
            string memTotal = "não disponível";
            foreach (string line in mem.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (line.StartsWith("MemTotal", StringComparison.OrdinalIgnoreCase)) { memTotal = line.Trim(); break; }
            string storage = Run(adb, "shell df -h /data", 20).Trim();
            string battery = Run(adb, "shell dumpsys battery", 20).Trim();
            string resolution = Run(adb, "shell wm size", 12).Trim();
            string density = Run(adb, "shell wm density", 12).Trim();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("INFORMAÇÕES DETALHADAS - ANDROID");
            sb.AppendLine("================================");
            sb.AppendLine("Fabricante: " + manufacturer);
            sb.AppendLine("Modelo: " + model);
            sb.AppendLine("Sistema: Android " + version + (string.IsNullOrWhiteSpace(sdk) ? "" : " • SDK " + sdk));
            sb.AppendLine("Build: " + build);
            sb.AppendLine("Serial: " + serial);
            sb.AppendLine("CPU / SoC: " + soc);
            sb.AppendLine("Arquitetura: " + abi);
            long ramBytes = AndroidMemTotalBytes(adb);
            sb.AppendLine("RAM: " + (ramBytes >= 0 ? FormatBytes(ramBytes) : memTotal));
            sb.AppendLine("Resolução: " + resolution);
            sb.AppendLine("Densidade: " + density);
            sb.AppendLine();
            sb.AppendLine("ARMAZENAMENTO /data");
            sb.AppendLine("--------------------");
            long stTotal, stUsed, stFree;
            AndroidStorageBytes(adb, out stTotal, out stUsed, out stFree);
            if (stTotal >= 0)
            {
                sb.AppendLine("Total: " + FormatBytes(stTotal));
                sb.AppendLine("Usado: " + FormatBytes(stUsed));
                sb.AppendLine("Livre: " + FormatBytes(stFree));
            }
            else sb.AppendLine(storage);
            sb.AppendLine();
            sb.AppendLine("BATERIA");
            sb.AppendLine("-------");
            sb.AppendLine(battery);
            return sb.ToString();
        }

        string GetDetailedIosInfo()
        {
            string info = ToolPath("ideviceinfo.exe");
            string id = ToolPath("idevice_id.exe");
            if (info == null || id == null) return "iOS Tools ausentes. Abra Ferramentas > Configurar iPhone.";
            if (string.IsNullOrWhiteSpace(Run(id, "-l", 10))) return "iPhone/iPad não autorizado ou indisponível em modo normal.";

            Func<string, string> key = delegate(string k) { return Run(info, "-k " + k, 10).Trim(); };
            Func<string, string, string> qkey = delegate(string q, string k) { return Run(info, "-q " + q + " -k " + k, 10).Trim(); };

            string name = key("DeviceName");
            string product = key("ProductType");
            string commercialModel = ResolveAppleMarketingName(product);
            string version = key("ProductVersion");
            string build = key("BuildVersion");
            string serial = key("SerialNumber");
            string hardware = key("HardwareModel");
            string cpu = key("CPUArchitecture");
            string udid = key("UniqueDeviceID");

            string totalDisk = qkey("com.apple.disk_usage", "TotalDiskCapacity");
            string dataAvailable = qkey("com.apple.disk_usage", "AmountDataAvailable");
            string battery = qkey("com.apple.mobile.battery", "BatteryCurrentCapacity");
            string charging = qkey("com.apple.mobile.battery", "BatteryIsCharging");
            string resolution = qkey("com.apple.mobile.iTunes", "ScreenDimensions");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("INFORMAÇÕES DETALHADAS - IPHONE / IOS");
            sb.AppendLine("=====================================");
            sb.AppendLine("Nome: " + name);
            sb.AppendLine("Modelo comercial: " + commercialModel);
            sb.AppendLine("Identificador técnico: " + product);
            sb.AppendLine("Hardware: " + hardware);
            sb.AppendLine("Sistema: iOS " + version);
            sb.AppendLine("Build: " + build);
            sb.AppendLine("Serial: " + serial);
            sb.AppendLine("UDID: " + udid);
            sb.AppendLine("CPU / arquitetura: " + (string.IsNullOrWhiteSpace(cpu) ? "não exposta" : cpu));
            sb.AppendLine("RAM: não exposta de forma confiável pelo serviço padrão do iOS");
            long iosTotal = ParseLongSafe(totalDisk);
            long iosFree = ParseLongSafe(dataAvailable);
            sb.AppendLine("Armazenamento total: " + (iosTotal >= 0 ? FormatBytes(iosTotal) : "não disponível"));
            sb.AppendLine("Armazenamento disponível: " + (iosFree >= 0 ? FormatBytes(iosFree) : "não disponível"));
            sb.AppendLine("Bateria: " + (string.IsNullOrWhiteSpace(battery) ? "não disponível" : battery + "%"));
            sb.AppendLine("Carregando: " + (string.IsNullOrWhiteSpace(charging) ? "não disponível" : charging));
            sb.AppendLine("Resolução: " + (string.IsNullOrWhiteSpace(resolution) ? "não disponível pela interface atual" : resolution));
            sb.AppendLine();
            sb.AppendLine("Observação: alguns campos variam conforme versão do iOS, estado do aparelho e serviços expostos pelo lockdownd.");
            sb.AppendLine();
            sb.AppendLine(GetIosHardwareDetails());
            return sb.ToString();
        }

        string ExportDetailedDeviceInfo()
        {
            string data = GetDetailedDeviceInfo();
            string file = Path.Combine(ExportDir, "Detalhes_Aparelho_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(file, data, Encoding.UTF8);
            return "Informações exportadas para:\r\n" + file + "\r\n\r\n" + data;
        }

        void EnsureSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    string text =
                        "# Bebel Equipe Do Mais Novo 155 v5.1.7\r\n" +
                        "# Para update online, informe a URL HTTPS de um manifest.json.\r\n" +
                        "update_source=github\r\n" +
                        "github_repo=Bebel-155/bebel157\r\n" +
                        "update_manifest_url=\r\n" +
                        "auto_check_updates=true\r\n" +
                        "auto_check_dependencies=true\r\n" +
                        "auto_install_app_updates=false\r\n" +
                        "auto_install_dependencies=false\r\n";
                    File.WriteAllText(SettingsFile, text, Encoding.UTF8);
                }
            }
            catch { }
        }

        void MigrateLegacySettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;

                string[] lines = File.ReadAllLines(SettingsFile);
                bool changed = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = (lines[i] ?? "").Trim();

                    if (trimmed.StartsWith("github_repo=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = trimmed.Substring("github_repo=".Length).Trim();
                        if (string.Equals(value, "Bebel-155/bebel155", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(value))
                        {
                            lines[i] = "github_repo=Bebel-155/bebel157";
                            changed = true;
                        }
                    }

                    if (trimmed.StartsWith("update_source=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = trimmed.Substring("update_source=".Length).Trim();
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            lines[i] = "update_source=github";
                            changed = true;
                        }
                    }
                }

                bool hasRepo = lines.Any(x =>
                    (x ?? "").Trim().StartsWith("github_repo=", StringComparison.OrdinalIgnoreCase));

                if (!hasRepo)
                {
                    List<string> updated = lines.ToList();
                    updated.Add("github_repo=Bebel-155/bebel157");
                    lines = updated.ToArray();
                    changed = true;
                }

                if (changed)
                {
                    File.WriteAllLines(SettingsFile, lines, Encoding.UTF8);
                    Log("Configuração de update migrada para Bebel-155/bebel157.");
                }
            }
            catch (Exception ex)
            {
                Log("Falha ao migrar settings.ini: " + ex.Message);
            }
        }

        string ReadSetting(string name, string fallback)
        {
            try
            {
                if (!File.Exists(SettingsFile)) return fallback;
                foreach (string raw in File.ReadAllLines(SettingsFile))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int pos = line.IndexOf('=');
                    if (pos <= 0) continue;
                    string k = line.Substring(0, pos).Trim();
                    string v = line.Substring(pos + 1).Trim();
                    if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return v;
                }
            }
            catch { }
            return fallback;
        }

        bool SettingBool(string name, bool fallback)
        {
            string v = ReadSetting(name, fallback ? "true" : "false");
            bool b;
            return bool.TryParse(v, out b) ? b : fallback;
        }

        void OpenUpdateSettings()
        {
            EnsureSettings();
            StartSimple("notepad.exe", "\"" + SettingsFile + "\"");
        }

        bool GithubRepositoryAccessible(string repo)
        {
            try
            {
                string api = "https://api.github.com/repos/" + repo;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5.1.9");
                    wc.Headers.Add("Accept", "application/vnd.github+json");
                    wc.DownloadString(api);
                    return true;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound) return false;
                return false;
            }
            catch { return false; }
        }

        UpdateManifest GetGithubLatestReleaseManifest()
        {
            lastGithubUpdateProblem = "";
            string repo = ReadSetting("github_repo", "Bebel-155/bebel157").Trim();
            if (string.IsNullOrWhiteSpace(repo))
            {
                lastGithubUpdateProblem = "O repositório de atualização não está configurado.";
                return null;
            }

            string api = "https://api.github.com/repos/" + repo + "/releases/latest";
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string json;

            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5.1.9");
                    wc.Headers.Add("Accept", "application/vnd.github+json");
                    json = wc.DownloadString(api);
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    if (GithubRepositoryAccessible(repo))
                    {
                        lastGithubUpdateProblem =
                            "Nenhuma Release publicada ainda.\r\n\r\n" +
                            "O repositório foi encontrado, mas ainda não existe uma Release marcada como latest.\r\n" +
                            "Depois que o GitHub Actions publicar a primeira Release, o botão Verificar Bebel 155 passará a encontrá-la automaticamente.";
                    }
                    else
                    {
                        lastGithubUpdateProblem =
                            "Repositório privado ou inacessível.\r\n\r\n" +
                            "O GitHub respondeu 404 para o repositório configurado. Para atualização anônima pelo aplicativo, o repositório/release precisa estar acessível publicamente, ou você deve usar outra fonte de atualização configurada no settings.ini.";
                    }
                    return null;
                }

                if (response != null && (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized))
                {
                    lastGithubUpdateProblem =
                        "GitHub recusou o acesso à atualização (HTTP " + ((int)response.StatusCode) + ").\r\n\r\n" +
                        "Verifique se o repositório está público e se a API do GitHub está acessível neste computador.";
                    return null;
                }

                throw;
            }

            string tag = JsonStringValue(json, "tag_name");
            string body = JsonStringValue(json, "body");
            if (string.IsNullOrWhiteSpace(tag))
            {
                lastGithubUpdateProblem = "A Release encontrada não possui tag de versão válida.";
                return null;
            }

            string version = tag.Trim();
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) version = version.Substring(1);
            string assetUrl = "", digest = "";

            foreach (Match obj in Regex.Matches(json, @"\{[^{}]*\}", RegexOptions.Singleline))
            {
                string name = JsonStringValue(obj.Value, "name");
                string url = JsonStringValue(obj.Value, "browser_download_url");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                bool shortName =
                    name.StartsWith("Bebel-155_V", StringComparison.OrdinalIgnoreCase);

                bool legacyName =
                    name.IndexOf("Bebel_Equipe_Do_Mais_Novo_155", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!shortName && !legacyName) continue;

                assetUrl = url;
                string d = JsonStringValue(obj.Value, "digest");
                if (!string.IsNullOrWhiteSpace(d) && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) digest = d.Substring(7);
                break;
            }

            if (string.IsNullOrWhiteSpace(assetUrl))
            {
                lastGithubUpdateProblem =
                    "Release encontrada, mas sem o EXE do Bebel 155.\r\n\r\n" +
                    "A Release existe, porém não contém um EXE compatível. São aceitos Bebel-155_V*.exe e o nome legado Bebel_Equipe_Do_Mais_Novo_155*.exe.\r\n" +
                    "Verifique o resultado do GitHub Actions e os arquivos anexados à Release.";
                return null;
            }

            UpdateManifest m = new UpdateManifest();
            m.version = version;
            m.downloadUrl = assetUrl;
            m.sha256 = digest;
            m.notes = string.IsNullOrWhiteSpace(body) ? "Release publicada em " + repo : body;
            return m;
        }

        string LoadManifestJson()
        {
            string url = ReadSetting("update_manifest_url", "").Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5.1");
                    return wc.DownloadString(url);
                }
            }

            string local = Path.Combine(UpdateDir, "manifest.json");
            if (File.Exists(local)) return File.ReadAllText(local, Encoding.UTF8);
            return null;
        }

        string JsonStringValue(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key)) return "";
            try
            {
                string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"";
                Match m = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success) return "";
                return Regex.Unescape(m.Groups[1].Value);
            }
            catch { return ""; }
        }

        UpdateManifest ParseUpdateManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            UpdateManifest m = new UpdateManifest();
            m.version = JsonStringValue(json, "version");
            m.downloadUrl = JsonStringValue(json, "downloadUrl");
            m.sha256 = JsonStringValue(json, "sha256");
            m.notes = JsonStringValue(json, "notes");
            return m;
        }

        string CheckAppUpdate()
        {
            try
            {
                UpdateManifest m = null;
                string source = ReadSetting("update_source", "github").Trim();
                if (string.Equals(source, "github", StringComparison.OrdinalIgnoreCase))
                {
                    m = GetGithubLatestReleaseManifest();
                    if (m == null)
                        return string.IsNullOrWhiteSpace(lastGithubUpdateProblem)
                            ? "Não foi possível localizar uma atualização no GitHub."
                            : lastGithubUpdateProblem;
                }
                else
                {
                    string json = LoadManifestJson();
                    if (string.IsNullOrWhiteSpace(json)) return "Nenhuma fonte de atualização está configurada.";
                    m = ParseUpdateManifest(json);
                }
                if (m == null || string.IsNullOrWhiteSpace(m.version)) return "A fonte de atualização não informou uma versão válida.";

                Version current, available;
                if (!Version.TryParse(AppVersion, out current) || !Version.TryParse(m.version, out available))
                    return "Não foi possível comparar versões. Atual=" + AppVersion + " / disponível=" + m.version;

                if (available <= current)
                {
                    pendingManifest = null;
                    return "Bebel 155 está atualizado.\r\nVersão atual: " + AppVersion + "\r\nVersão disponível: " + m.version;
                }

                pendingManifest = m;
                return "NOVA VERSÃO DISPONÍVEL\r\n======================\r\nAtual: " + AppVersion +
                       "\r\nNova: " + m.version +
                       "\r\n\r\n" + (m.notes ?? "") +
                       "\r\n\r\nUse 'Baixar / aplicar update' para continuar.";
            }
            catch (Exception ex)
            {
                return "Falha ao verificar atualização:\r\n" + ex.Message;
            }
        }

        string FileSha256(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(file))
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        string StageAppUpdate()
        {
            if (pendingManifest == null) return "Nenhuma atualização pendente. Execute 'Verificar Bebel 155' primeiro.";
            if (string.IsNullOrWhiteSpace(pendingManifest.downloadUrl)) return "O manifest.json não possui downloadUrl.";

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string safeVersion = (pendingManifest.version ?? "update").Replace(".", "_").Replace("-", "_");
            string newExe = Path.Combine(UpdateDir, "Bebel-155_V" + safeVersion + ".exe");
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "BebelEquipe155-v5.1");
                wc.DownloadFile(pendingManifest.downloadUrl, newExe);
            }

            if (!File.Exists(newExe)) return "O download da atualização não foi criado.";
            if (!string.IsNullOrWhiteSpace(pendingManifest.sha256))
            {
                string actual = FileSha256(newExe);
                if (!string.Equals(actual, pendingManifest.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(newExe); } catch { }
                    return "Falha de integridade: SHA-256 não corresponde ao manifest.json.";
                }
            }

            string current = Application.ExecutablePath;
            string script = Path.Combine(UpdateDir, "aplicar_update.cmd");
            int pid = Process.GetCurrentProcess().Id;
            string content =
                "@echo off\r\n" +
                "setlocal\r\n" +
                ":wait\r\n" +
                "tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul\r\n" +
                "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
                "copy /y \"" + current + "\" \"" + current + ".bak\" >nul 2>&1\r\n" +
                "copy /y \"" + newExe + "\" \"" + current + "\"\r\n" +
                "start \"\" \"" + current + "\"\r\n" +
                "del /q \"%~f0\"\r\n";
            File.WriteAllText(script, content, Encoding.Default);
            return script;
        }

        void ApplyPendingUpdate()
        {
            if (pendingManifest == null)
            {
                MessageBox.Show("Primeiro clique em 'Verificar Bebel 155'.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string hashWarning = string.IsNullOrWhiteSpace(pendingManifest.sha256)
                ? "\r\n\r\nAVISO: o manifesto não informou SHA-256. Confirme que a fonte é sua e confiável."
                : "";

            if (MessageBox.Show("Baixar e preparar a versão " + pendingManifest.version + "?" + hashWarning,
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            SetBusy(true, "Baixando atualização do Bebel 155...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try { result = StageAppUpdate(); }
                catch (Exception ex) { result = "ERRO: " + ex.Message; }

                BeginInvoke((MethodInvoker)delegate
                {
                    SetBusy(false, "Pronto");
                    if (!result.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || !File.Exists(result))
                    {
                        outputs["Atualizações"].Text = result;
                        return;
                    }

                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + result + "\"\"");
                        psi.UseShellExecute = true;
                        psi.Verb = "runas";
                        Process.Start(psi);
                        Application.Exit();
                    }
                    catch (Exception ex)
                    {
                        outputs["Atualizações"].Text = "Não foi possível iniciar o atualizador:\r\n" + ex.Message;
                    }
                });
            });
        }

        string UpgradeAppleDevices()
        {
            string winget = ToolPath("winget.exe");
            if (winget == null) return "winget ausente; Apple Devices não foi atualizado.";
            string up = Run(winget, "upgrade --name \"Apple Devices\" --source msstore --accept-source-agreements --accept-package-agreements", 1800);
            if (up.IndexOf("No installed package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                up.IndexOf("Nenhum pacote", StringComparison.OrdinalIgnoreCase) >= 0)
                return InstallAppleDevices();
            return up;
        }

        string UpdateAllDependencies()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ATUALIZAÇÃO DE DEPENDÊNCIAS");
            sb.AppendLine("===========================");
            sb.AppendLine();
            sb.AppendLine("ANDROID PLATFORM TOOLS");
            sb.AppendLine(InstallPlatformTools());
            sb.AppendLine();
            sb.AppendLine("APPLE DEVICES");
            sb.AppendLine(UpgradeAppleDevices());
            sb.AppendLine();

            if (ToolPath("ideviceinfo.exe") != null || ToolPath("idevice_id.exe") != null)
            {
                sb.AppendLine("IOS TOOLS");
                sb.AppendLine(InstallIosCommunityTools());
            }
            else
            {
                sb.AppendLine("IOS TOOLS: não instalados; atualização automática foi ignorada. Use Ferramentas > Configurar iPhone se desejar instalá-los.");
            }
            return sb.ToString();
        }

        void AutoUpdateStartup()
        {
            try
            {
                Thread.Sleep(1800);
                string updateResult = SettingBool("auto_check_updates", true) ? CheckAppUpdate() : "Verificação automática de app desativada.";
                string deps = SettingBool("auto_check_dependencies", true) ? CheckDependencies() : "Verificação automática de dependências desativada.";

                if (SettingBool("auto_install_dependencies", false))
                    deps += "\r\n\r\n" + UpdateAllDependencies();

                bool autoApplyApp = pendingManifest != null && SettingBool("auto_install_app_updates", false);
                string staged = null;
                if (autoApplyApp)
                {
                    try { staged = StageAppUpdate(); } catch (Exception ex) { staged = "ERRO: " + ex.Message; }
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    if (outputs.ContainsKey("Atualizações"))
                        outputs["Atualizações"].Text = updateResult + "\r\n\r\n" + deps;

                    if (pendingManifest != null)
                        statusLabel.Text = "● Nova versão " + pendingManifest.version + " disponível";

                    if (autoApplyApp && !string.IsNullOrWhiteSpace(staged) && staged.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) && File.Exists(staged))
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + staged + "\"\"");
                            psi.UseShellExecute = true;
                            psi.Verb = "runas";
                            Process.Start(psi);
                            Application.Exit();
                        }
                        catch (Exception ex)
                        {
                            if (outputs.ContainsKey("Atualizações")) outputs["Atualizações"].Text += "\r\n\r\nFalha ao aplicar update automático: " + ex.Message;
                        }
                    }
                });
            }
            catch { }
        }

        void AddUserPath(string dir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
                string userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
                List<string> parts = userPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToList();

                if (!parts.Any(x => string.Equals(x, dir, StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add(dir);
                    Environment.SetEnvironmentVariable(
                        "Path",
                        string.Join(";", parts.ToArray()),
                        EnvironmentVariableTarget.User);
                }

                string processPath = Environment.GetEnvironmentVariable("Path") ?? "";
                if (!processPath.Split(';').Any(x => string.Equals(x.Trim(), dir, StringComparison.OrdinalIgnoreCase)))
                    Environment.SetEnvironmentVariable("Path", dir + ";" + processPath);
            }
            catch { }
        }

        string CheckDependencies()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DEPENDÊNCIAS - BEBEL 155 v5");
            sb.AppendLine("===========================");
            sb.AppendLine("adb.exe: " + (ToolPath("adb.exe") != null ? "OK" : "AUSENTE"));
            sb.AppendLine("fastboot.exe: " + (ToolPath("fastboot.exe") != null ? "OK" : "AUSENTE"));
            sb.AppendLine("idevice_id.exe: " + (ToolPath("idevice_id.exe") != null ? "OK" : "AUSENTE"));
            sb.AppendLine("ideviceinfo.exe: " + (ToolPath("ideviceinfo.exe") != null ? "OK" : "AUSENTE"));
            sb.AppendLine("idevicebackup2.exe: " + (ToolPath("idevicebackup2.exe") != null ? "OK" : "AUSENTE"));
            sb.AppendLine("winget.exe: " + (ToolPath("winget.exe") != null ? "OK" : "AUSENTE"));
            sb.AppendLine("Apple Mobile Device Service: " + GetAppleServiceStatus());
            sb.AppendLine();
            sb.AppendLine("Android: use 'Instalar ADB/Fastboot'.");
            sb.AppendLine("iPhone: use 'Configurar iPhone' para Apple Devices + iOS Tools.");
            sb.AppendLine("Os iOS Tools opcionais usam uma build comunitária de libimobiledevice hospedada no GitHub e exigem confirmação.");
            return sb.ToString();
        }

        string InstallPlatformTools()
        {
            string stageRoot = null;
            string backupDir = null;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                // Evita "acesso negado" quando adb.exe ainda está em execução.
                try
                {
                    string currentAdb = ToolPath("adb.exe");
                    if (!string.IsNullOrWhiteSpace(currentAdb) && File.Exists(currentAdb))
                        Run(currentAdb, "kill-server", 15);
                }
                catch { }

                try
                {
                    foreach (Process p in Process.GetProcessesByName("adb"))
                    {
                        try { p.Kill(); p.WaitForExit(3000); } catch { }
                    }
                    foreach (Process p in Process.GetProcessesByName("fastboot"))
                    {
                        try { p.Kill(); p.WaitForExit(3000); } catch { }
                    }
                }
                catch { }

                Thread.Sleep(600);

                string zip = Path.Combine(DownloadDir, "platform-tools-latest-windows.zip");
                string url = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
                stageRoot = Path.Combine(ToolsDir, "platform-tools_stage_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                backupDir = Path.Combine(ToolsDir, "platform-tools_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                if (File.Exists(zip))
                {
                    try { File.Delete(zip); } catch { }
                }

                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5.1.9");
                    wc.DownloadFile(url, zip);
                }

                Directory.CreateDirectory(stageRoot);
                ZipFile.ExtractToDirectory(zip, stageRoot);

                string stagedTools = Path.Combine(stageRoot, "platform-tools");
                string stagedAdb = Path.Combine(stagedTools, "adb.exe");
                string stagedFastboot = Path.Combine(stagedTools, "fastboot.exe");

                if (!File.Exists(stagedAdb) || !File.Exists(stagedFastboot))
                    return "O pacote foi baixado, mas adb.exe/fastboot.exe não foram encontrados.";

                string version = Run(stagedAdb, "version", 20);

                if (Directory.Exists(PlatformToolsDir))
                    Directory.Move(PlatformToolsDir, backupDir);

                try
                {
                    Directory.Move(stagedTools, PlatformToolsDir);
                }
                catch
                {
                    if (!Directory.Exists(PlatformToolsDir) && Directory.Exists(backupDir))
                        Directory.Move(backupDir, PlatformToolsDir);
                    throw;
                }

                try
                {
                    if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true);
                }
                catch { }

                AddUserPath(PlatformToolsDir);

                return "ADB/Fastboot atualizados com segurança.\\r\\n\\r\\n" +
                       version + "\\r\\n\\r\\nPasta:\\r\\n" + PlatformToolsDir +
                       (Directory.Exists(backupDir)
                           ? "\\r\\n\\r\\nBackup da versão anterior:\\r\\n" + backupDir
                           : "");
            }
            catch (Exception ex)
            {
                try
                {
                    if (!Directory.Exists(PlatformToolsDir) &&
                        !string.IsNullOrWhiteSpace(backupDir) &&
                        Directory.Exists(backupDir))
                        Directory.Move(backupDir, PlatformToolsDir);
                }
                catch { }

                return "Falha ao instalar/atualizar Platform Tools:\\r\\n" +
                       ex.Message +
                       "\\r\\n\\r\\nO atualizador tentou preservar/restaurar a versão anterior.";
            }
        }

        string InstallAppleDevices()
        {
            string winget = ToolPath("winget.exe");
            if (winget == null)
            {
                try { OpenUrl("ms-windows-store://search/?query=Apple%20Devices"); } catch { }
                return "WinGet não foi encontrado.\r\n\r\n" +
                       "A Microsoft Store foi aberta na busca por Apple Devices.\r\n" +
                       "Instale o app oficial da Apple e, se o WinGet também estiver ausente, instale/atualize o App Installer.";
            }

            string search = Run(winget, "search --name \"Apple Devices\" --source msstore --accept-source-agreements", 120);
            string install = Run(winget, "install --name \"Apple Devices\" --source msstore --accept-source-agreements --accept-package-agreements", 1800);

            return "WINGET:\r\n" + winget + "\r\n\r\nBUSCA:\r\n" + search + "\r\n\r\nINSTALAÇÃO:\r\n" + install;
        }

        void ShowLibimobiledeviceHelp()
        {
            outputs["Ferramentas"].Text =
                "LIBIMOBILEDEVICE NO WINDOWS\r\n===========================\r\n\r\n" +
                "O sistema não baixa executáveis aleatórios de terceiros.\r\n" +
                "A detecção USB/Recovery/DFU funciona sem ele.\r\n\r\n" +
                "Para ideviceinfo e idevicebackup2, use uma instalação confiável ou o método oficial do projeto.";

            if (MessageBox.Show("Abrir o site oficial do libimobiledevice?", AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                OpenUrl("https://libimobiledevice.org/");
        }

        void OpenAppleDevicesOrStore()
        {
            StartSimple("explorer.exe", "shell:AppsFolder");
        }

        string ScanDevices()
        {
            string pnputil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\pnputil.exe");
            if (!File.Exists(pnputil)) return "pnputil.exe não encontrado.";
            return Run(pnputil, "/scan-devices", 120);
        }

        string DetectSystemVersion(string adbState, string iosState, List<UsbDevice> devices)
        {
            if (adbState == "ADB CONECTADO")
            {
                string adb = ToolPath("adb.exe");
                string ver = Run(adb, "shell getprop ro.build.version.release", 8).Trim();
                string sdk = Run(adb, "shell getprop ro.build.version.sdk", 8).Trim();
                if (!string.IsNullOrWhiteSpace(ver))
                    return "Android " + ver + (!string.IsNullOrWhiteSpace(sdk) ? " • SDK " + sdk : "");
            }

            if (iosState == "IPHONE AUTORIZADO")
            {
                string info = ToolPath("ideviceinfo.exe");
                if (info != null)
                {
                    string ver = Run(info, "-k ProductVersion", 10).Trim();
                    if (!string.IsNullOrWhiteSpace(ver)) return "iOS " + ver;
                }
            }

            if (devices.Any(x => IsApple(x)))
            {
                if (devices.Any(x => x.Name.IndexOf("DFU", StringComparison.OrdinalIgnoreCase) >= 0)) return "iOS • DFU";
                if (devices.Any(x => x.Name.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0)) return "iOS • Recovery";
                return "iOS • versão requer autorização";
            }

            if (devices.Any(x => !IsApple(x) && (x.Name.IndexOf("MTP", StringComparison.OrdinalIgnoreCase) >= 0 || KnownVid(x.PnpId))))
                return "Android • versão requer ADB";

            return "Não identificado";
        }

        string GetAppleServiceStatus()
        {
            try
            {
                ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name,State,StartMode FROM Win32_Service WHERE Name='Apple Mobile Device Service' OR DisplayName='Apple Mobile Device Service'");
                foreach (ManagementObject m in s.Get())
                    return Convert.ToString(m["State"]) + " / " + Convert.ToString(m["StartMode"]);
            }
            catch { }
            return "não encontrado / não aplicável";
        }

        string PnpCodeDescription(int code)
        {
            switch (code)
            {
                case 0: return "Funcionando corretamente";
                case 1: return "Dispositivo não configurado corretamente";
                case 3: return "Driver possivelmente corrompido ou ausente";
                case 10: return "O dispositivo não pôde iniciar";
                case 12: return "Recursos insuficientes";
                case 14: return "Reinicialização necessária";
                case 18: return "Reinstalação do driver recomendada";
                case 19: return "Configuração do Registro inconsistente";
                case 22: return "Dispositivo desabilitado";
                case 24: return "Dispositivo ausente, com falha ou driver incompleto";
                case 28: return "Drivers não instalados";
                case 31: return "Windows não conseguiu carregar os drivers necessários";
                case 32: return "Serviço/driver desabilitado";
                case 37: return "Falha ao inicializar o driver";
                case 39: return "Driver corrompido ou ausente";
                case 41: return "Driver carregado, hardware não encontrado";
                case 43: return "Windows interrompeu o dispositivo por erro";
                case 45: return "Dispositivo não conectado";
                case 47: return "Dispositivo preparado para remoção segura";
                case 48: return "Software/driver bloqueado pelo Windows";
                case 52: return "Assinatura digital do driver não verificada";
                default: return "Código PnP " + code;
            }
        }

        string PnpCodeRecommendation(int code, string vendor)
        {
            if (code == 0) return "Nenhuma ação necessária.";
            if (code == 14) return "Reinicie o Windows e teste novamente.";
            if (code == 22) return "Ative o dispositivo no Gerenciador de Dispositivos.";
            if (code == 28 || code == 31 || code == 37 || code == 39 || code == 52)
                return VendorDriverRecommendation(vendor, true);
            if (code == 10 || code == 43)
                return "Reconecte o cabo, troque a porta USB, reinicie o aparelho e atualize o driver. " + VendorDriverRecommendation(vendor, false);
            if (code == 45)
                return "Reconecte fisicamente o aparelho e teste outro cabo/porta.";
            return VendorDriverRecommendation(vendor, true);
        }

        string InstallIosCommunityTools()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string target = Path.Combine(ToolsDir, "libimobiledevice");
                string zip = Path.Combine(DownloadDir, "libimobiledevice-windows-latest.zip");
                string api = "https://api.github.com/repos/jrjr/libimobiledevice-windows/releases/latest";

                string json;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5");
                    json = wc.DownloadString(api);
                }

                MatchCollection matches = Regex.Matches(json, "\\\"browser_download_url\\\"\\s*:\\s*\\\"([^\\\"]+\\.zip)\\\"");
                if (matches.Count == 0)
                    return "A versão mais recente foi localizada, mas nenhum arquivo ZIP compatível apareceu na release.";

                string url = matches[0].Groups[1].Value.Replace("\\/", "/");
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "BebelEquipe155-v5");
                    wc.DownloadFile(url, zip);
                }

                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.CreateDirectory(target);
                ZipFile.ExtractToDirectory(zip, target);

                string i1 = ToolPath("idevice_id.exe");
                string i2 = ToolPath("ideviceinfo.exe");
                string i3 = ToolPath("idevicebackup2.exe");

                if (i1 != null && i2 != null)
                    return "iOS Tools instalados com sucesso.\r\n\r\nFonte comunitária: GitHub jrjr/libimobiledevice-windows\r\nidevice_id: " + i1 + "\r\nideviceinfo: " + i2 + "\r\nidevicebackup2: " + (i3 ?? "não localizado nesta build");

                return "O pacote foi extraído, mas as ferramentas esperadas não foram localizadas. Verifique a release baixada em: " + target;
            }
            catch (Exception ex)
            {
                return "Falha ao instalar iOS Tools comunitários:\r\n" + ex.Message;
            }
        }

        string ConfigureIphone()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ETAPA 1 - APPLE DEVICES");
            sb.AppendLine(InstallAppleDevices());
            sb.AppendLine();
            sb.AppendLine("ETAPA 2 - IOS TOOLS");
            sb.AppendLine(InstallIosCommunityTools());
            sb.AppendLine();
            sb.AppendLine("Depois, reconecte o iPhone, desbloqueie e toque em 'Confiar neste computador'.");
            return sb.ToString();
        }

        string GenerateTxtReport()
        {
            string file = Path.Combine(ReportDir, "Relatorio_Bebel155_v5_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(AppName);
            sb.AppendLine("Relatório gerado em " + DateTime.Now);
            sb.AppendLine();
            sb.AppendLine(GetDriverDiagnosis());
            sb.AppendLine();
            sb.AppendLine(GetAndroidInfo());
            sb.AppendLine();
            sb.AppendLine(GetIOSInfo());
            sb.AppendLine();
            sb.AppendLine(GetIDeviceVerification());

            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            return "Relatório TXT criado:\r\n" + file;
        }

        string HtmlEncode(string x)
        {
            if (x == null) return "";
            return x.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        string GenerateHtmlReport()
        {
            string file = Path.Combine(ReportDir, "Relatorio_Bebel155_v5_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html");
            string body = GetDriverDiagnosis() + "\r\n\r\n" + GetAndroidInfo() + "\r\n\r\n" + GetIOSInfo() + "\r\n\r\n" + GetIDeviceVerification();

            string html =
                "<!doctype html><html><head><meta charset='utf-8'><title>" + HtmlEncode(AppName) + "</title>" +
                "<style>body{font-family:Segoe UI,Arial;margin:40px;background:#f5f7fa;color:#1f2937}" +
                ".card{background:#fff;border:1px solid #e1e5eb;border-radius:16px;padding:28px;max-width:1100px;margin:auto}" +
                "h1{margin-top:0}pre{white-space:pre-wrap;font-family:Consolas,monospace;background:#f8fafc;padding:18px;border-radius:10px}</style>" +
                "</head><body><div class='card'><h1>" + HtmlEncode(AppName) + "</h1><p>Relatório gerado em " +
                HtmlEncode(DateTime.Now.ToString()) + "</p><pre>" + HtmlEncode(body) + "</pre></div></body></html>";

            File.WriteAllText(file, html, Encoding.UTF8);
            return "Relatório HTML criado:\r\n" + file;
        }

        string SaveDeviceHistory()
        {
            string file = Path.Combine(HistoryDir, "historico_dispositivos.csv");
            bool exists = File.Exists(file);
            StringBuilder sb = new StringBuilder();

            if (!exists)
                sb.AppendLine("Data,Nome,Fabricante,Modo,Status,CodigoPnP,PNPDeviceID");

            string adb = AdbState();
            string fb = FastbootState();
            string ios = IosState();

            foreach (UsbDevice d in GetUsbDevices())
            {
                sb.AppendLine(
                    Csv(DateTime.Now.ToString("s")) + "," +
                    Csv(d.Name) + "," +
                    Csv(VendorFromId(d.PnpId)) + "," +
                    Csv(InferMode(d, adb, fb, ios)) + "," +
                    Csv(d.Status) + "," +
                    d.ErrorCode + "," +
                    Csv(d.PnpId)
                );
            }

            File.AppendAllText(file, sb.ToString(), Encoding.UTF8);
            return "Histórico atualizado:\r\n" + file;
        }

        string Csv(string x)
        {
            if (x == null) x = "";
            return "\"" + x.Replace("\"", "\"\"") + "\"";
        }

        class UsbDevice
        {
            public string Name;
            public string Manufacturer;
            public string Status;
            public int ErrorCode;
            public string PnpId;
        }

        class AssistantResult
        {
            public string Severity;
            public string Title;
            public string Text;
        }

        class DeviceRenderData
        {
            public string Key=""; public string Caption=""; public string Url=""; public string Source="";
        }

        class DeviceMetrics
        {
            public string System = "—";
            public string Battery = "—";
            public string Ram = "—";
            public string Storage = "—";
            public string StorageDetail = "";
            public string Resolution = "—";
        }

        class DashboardState
        {
            public string Usb, Android, Ios, Driver, SystemVersion;
            public string Name, Vendor, Mode, Id;
            public string AssistantSeverity, AssistantTitle, AssistantText;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (SplashForm splash = new SplashForm())
                splash.ShowDialog();

            using (LoginForm login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK) return;
            }

            Application.Run(new MainForm());
        }
    }
}

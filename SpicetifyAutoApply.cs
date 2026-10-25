// SpicetifyAutoApply.cs
// Grafischer Installer (eine einzige, eigenstaendige Windows-Exe):
//   Doppelklick        -> dunkles Fenster mit Status, Optionen und Aktionen
//   --apply            -> Standardmodus: spicetify apply (Fallback: backup apply)
//   --apply-fresh      -> Gruendlich: spicetify restore backup apply
//   --uninstall        -> entfernt den Autostart
// Kompilieren (csc liegt bei Windows bei), WICHTIG: /codepage:65001 fuer Umlaute:
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /codepage:65001 ^
//     /out:SpicetifyAutoApply.exe /r:System.dll /r:System.Drawing.dll ^
//     /r:System.Windows.Forms.dll SpicetifyAutoApply.cs

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--apply")        return Worker.Apply(false);
        if (args.Length > 0 && args[0] == "--apply-fresh")  return Worker.Apply(true);
        if (args.Length > 0 && args[0] == "--uninstall")    { Worker.Uninstall(); return 0; }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }
}

// ----------------------------------------------------------------------------
//  Logik (auch ohne GUI nutzbar - vom Task-Aufruf --apply verwendet)
// ----------------------------------------------------------------------------
static class Worker
{
    public const string TaskName = "Spicetify Auto-Apply";

    public static string LocalAppData
    { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }

    public static string SpicetifyExe
    { get { return Path.Combine(LocalAppData, "spicetify", "spicetify.exe"); } }

    public static string LogPath
    { get { return Path.Combine(LocalAppData, "spicetify-autoapply", "autoapply.log"); } }

    public static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine,
                Encoding.UTF8);
        }
        catch { }
    }

    public static bool SpicetifyInstalled { get { return File.Exists(SpicetifyExe); } }

    public static string SpicetifyVersion()
    {
        try
        {
            string outp, err; int code;
            Run(SpicetifyExe, "-v", out outp, out err, out code);
            string v = (outp + " " + err).Trim();
            if (v.Length == 0) return "installiert";
            int nl = v.IndexOf('\n');
            if (nl > 0) v = v.Substring(0, nl).Trim();
            return "v" + v.Replace("v", "").Trim();
        }
        catch { return "installiert"; }
    }

    public static bool TaskExists()
    {
        try
        {
            string outp, err; int code;
            Run("schtasks.exe", "/Query /TN \"" + TaskName + "\"", out outp, out err, out code);
            return code == 0;
        }
        catch { return false; }
    }

    public static int Apply(bool fresh)
    {
        try
        {
            if (!SpicetifyInstalled)
            {
                Log("Spicetify nicht gefunden unter " + SpicetifyExe + ".");
                return 1;
            }

            int code;
            if (fresh)
            {
                Log("Wende Spicetify an (restore backup apply)...");
                code = RunSpicetify("restore backup apply");
            }
            else
            {
                Log("Wende Spicetify an (apply)...");
                code = RunSpicetify("apply");
                if (code != 0)
                {
                    Log("apply fehlgeschlagen (ExitCode " + code + ") - versuche backup apply...");
                    code = RunSpicetify("backup apply");
                }
            }
            Log("Fertig (ExitCode " + code + ").");
            return code;
        }
        catch (Exception ex)
        {
            Log("FEHLER: " + ex.Message);
            return 1;
        }
    }

    public static int Install(string applyArg, bool logon, int delaySec, bool daily, DateTime dailyTime, out string error)
    {
        string exe = Process.GetCurrentProcess().MainModule.FileName;
        string xml = BuildTaskXml(exe, applyArg, logon, delaySec, daily, dailyTime);
        string xmlPath = Path.Combine(Path.GetTempPath(), "spicetify-autoapply-task.xml");
        File.WriteAllText(xmlPath, xml, Encoding.Unicode);

        string outp; int code;
        Run("schtasks.exe", "/Create /TN \"" + TaskName + "\" /XML \"" + xmlPath + "\" /F",
            out outp, out error, out code);
        try { File.Delete(xmlPath); } catch { }
        return code;
    }

    public static int Uninstall()
    {
        string outp, err; int code;
        Run("schtasks.exe", "/Delete /TN \"" + TaskName + "\" /F", out outp, out err, out code);
        return code;
    }

    public static int RunSpicetify(string arguments)
    {
        string outp, err; int code;
        Run(SpicetifyExe, arguments, out outp, out err, out code);
        return code;
    }

    public static void Run(string file, string arguments, out string stdout, out string stderr, out int exit)
    {
        var psi = new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var p = Process.Start(psi))
        {
            stdout = p.StandardOutput.ReadToEnd();
            stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            exit = p.ExitCode;
        }
    }

    static string BuildTaskXml(string exePath, string applyArg, bool logon, int delaySec, bool daily, DateTime dailyTime)
    {
        string cmd = SecurityElement.Escape(exePath);
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n");
        sb.Append("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n");
        sb.Append("  <RegistrationInfo>\r\n");
        sb.Append("    <Description>Wendet Spicetify nach Login/Zeitplan erneut an (z. B. nach Spotify-Updates).</Description>\r\n");
        sb.Append("  </RegistrationInfo>\r\n");
        sb.Append("  <Triggers>\r\n");
        if (logon)
        {
            sb.Append("    <LogonTrigger>\r\n      <Enabled>true</Enabled>\r\n");
            if (delaySec > 0) sb.Append("      <Delay>PT" + delaySec + "S</Delay>\r\n");
            sb.Append("    </LogonTrigger>\r\n");
        }
        if (daily)
        {
            string start = DateTime.Now.ToString("yyyy-MM-dd") + "T" + dailyTime.ToString("HH:mm:ss");
            sb.Append("    <CalendarTrigger>\r\n");
            sb.Append("      <StartBoundary>" + start + "</StartBoundary>\r\n");
            sb.Append("      <Enabled>true</Enabled>\r\n");
            sb.Append("      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>\r\n");
            sb.Append("    </CalendarTrigger>\r\n");
        }
        sb.Append("  </Triggers>\r\n");
        sb.Append("  <Principals>\r\n    <Principal id=\"Author\">\r\n");
        sb.Append("      <LogonType>InteractiveToken</LogonType>\r\n");
        sb.Append("      <RunLevel>LeastPrivilege</RunLevel>\r\n");
        sb.Append("    </Principal>\r\n  </Principals>\r\n");
        sb.Append("  <Settings>\r\n");
        sb.Append("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n");
        sb.Append("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n");
        sb.Append("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n");
        sb.Append("    <StartWhenAvailable>true</StartWhenAvailable>\r\n");
        sb.Append("    <Enabled>true</Enabled>\r\n");
        sb.Append("  </Settings>\r\n");
        sb.Append("  <Actions Context=\"Author\">\r\n    <Exec>\r\n");
        sb.Append("      <Command>" + cmd + "</Command>\r\n");
        sb.Append("      <Arguments>" + applyArg + "</Arguments>\r\n");
        sb.Append("    </Exec>\r\n  </Actions>\r\n</Task>\r\n");
        return sb.ToString();
    }
}

// ----------------------------------------------------------------------------
//  GUI
// ----------------------------------------------------------------------------
class MainForm : Form
{
    static readonly Color Bg      = Color.FromArgb(24, 24, 24);
    static readonly Color Panel   = Color.FromArgb(34, 34, 34);
    static readonly Color Accent  = Color.FromArgb(29, 185, 84);
    static readonly Color TextCol = Color.FromArgb(236, 236, 236);
    static readonly Color Subtle  = Color.FromArgb(165, 165, 165);
    static readonly Color BtnDark = Color.FromArgb(48, 48, 48);

    Label lblSpice, lblTask;
    CheckBox chkLogon, chkDaily;
    ComboBox cboDelay;
    DateTimePicker dtDaily;
    RadioButton rbStandard, rbFresh;
    Button btnInstall, btnApply, btnRemove, btnInstallSpice, btnLog, btnScheduler;
    TextBox txtLog;

    public MainForm()
    {
        Text = "Spicetify Auto-Apply";
        BackColor = Bg;
        ForeColor = TextCol;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(580, 660);

        BuildUi();
        RefreshStatus();
        AppendLog("Bereit.");
    }

    void BuildUi()
    {
        var header = new Panel { Location = new Point(0, 0), Size = new Size(580, 78), BackColor = Panel };
        var accentBar = new Panel { Location = new Point(0, 0), Size = new Size(6, 78), BackColor = Accent };
        var title = new Label
        {
            Text = "Spicetify Auto-Apply",
            Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
            ForeColor = TextCol, AutoSize = true, Location = new Point(24, 14)
        };
        var subtitle = new Label
        {
            Text = "Hält deine Spicetify-Anpassungen nach Spotify-Updates aktiv.",
            ForeColor = Subtle, AutoSize = true, Location = new Point(26, 48)
        };
        header.Controls.Add(accentBar);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        Controls.Add(header);

        int y = 94;

        y = Section("Status", y);
        lblSpice = NewLabel("Spicetify: ...", 36, y); y += 24;
        lblTask  = NewLabel("Autostart: ...", 36, y); y += 34;

        y = Section("Wann soll angewendet werden?", y);
        chkLogon = NewCheck("Bei jeder Anmeldung", 36, y, true);
        var lblDelay = NewLabel("Verzögerung:", 300, y + 2);
        cboDelay = new ComboBox
        {
            Location = new Point(392, y - 2), Size = new Size(140, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat, BackColor = BtnDark, ForeColor = TextCol
        };
        cboDelay.Items.AddRange(new object[] { "Sofort", "15 Sekunden", "30 Sekunden", "1 Minute", "2 Minuten" });
        cboDelay.SelectedIndex = 2;
        Controls.Add(lblDelay); Controls.Add(cboDelay);
        y += 34;

        chkDaily = NewCheck("Zusätzlich täglich um", 36, y, false);
        dtDaily = new DateTimePicker
        {
            Location = new Point(230, y - 2), Size = new Size(110, 24),
            Format = DateTimePickerFormat.Time, ShowUpDown = true,
            CalendarMonthBackground = Panel
        };
        dtDaily.Value = DateTime.Today.AddHours(13);
        Controls.Add(dtDaily);
        y += 38;

        y = Section("Modus", y);
        rbStandard = NewRadio("Standard  -  spicetify apply (empfohlen, schnell)", 36, y, true); y += 26;
        rbFresh    = NewRadio("Gründlich  -  restore backup apply (vollständig neu)", 36, y, false); y += 38;

        y = Section("Aktionen", y);
        btnInstall = NewButton("Einrichten / Aktualisieren", 36, y, 220, true);
        btnApply   = NewButton("Jetzt anwenden", 266, y, 140, false);
        btnRemove  = NewButton("Entfernen", 416, y, 128, false);
        y += 44;
        btnInstallSpice = NewLink("Spicetify installieren", 36, y);
        btnLog          = NewLink("Log öffnen", 210, y);
        btnScheduler    = NewLink("Aufgabenplanung", 330, y);
        y += 34;

        btnInstall.Click      += delegate { DoInstall(); };
        btnApply.Click        += delegate { DoApplyNow(); };
        btnRemove.Click       += delegate { DoRemove(); };
        btnInstallSpice.Click += delegate { DoInstallSpicetify(); };
        btnLog.Click          += delegate { OpenLog(); };
        btnScheduler.Click    += delegate { try { Process.Start("taskschd.msc"); } catch { } };

        y = Section("Protokoll", y);
        txtLog = new TextBox
        {
            Location = new Point(36, y), Size = new Size(508, 120),
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(18, 18, 18), ForeColor = Subtle,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 8.5f)
        };
        Controls.Add(txtLog);
    }

    int Section(string text, int y)
    {
        var l = new Label
        {
            Text = text, ForeColor = Accent, AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            Location = new Point(24, y)
        };
        Controls.Add(l);
        var line = new Panel { Location = new Point(24, y + 20), Size = new Size(532, 1), BackColor = Color.FromArgb(55, 55, 55) };
        Controls.Add(line);
        return y + 30;
    }

    Label NewLabel(string t, int x, int y)
    {
        var l = new Label { Text = t, ForeColor = TextCol, AutoSize = true, Location = new Point(x, y) };
        Controls.Add(l); return l;
    }

    CheckBox NewCheck(string t, int x, int y, bool chk)
    {
        var c = new CheckBox
        {
            Text = t, Checked = chk, AutoSize = true, Location = new Point(x, y),
            ForeColor = TextCol, FlatStyle = FlatStyle.Flat
        };
        Controls.Add(c); return c;
    }

    RadioButton NewRadio(string t, int x, int y, bool chk)
    {
        var r = new RadioButton
        {
            Text = t, Checked = chk, AutoSize = true, Location = new Point(x, y),
            ForeColor = TextCol, FlatStyle = FlatStyle.Flat
        };
        Controls.Add(r); return r;
    }

    Button NewButton(string t, int x, int y, int w, bool primary)
    {
        var b = new Button
        {
            Text = t, Location = new Point(x, y), Size = new Size(w, 34),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, primary ? FontStyle.Bold : FontStyle.Regular)
        };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
        b.BackColor = primary ? Accent : BtnDark;
        b.ForeColor = primary ? Color.Black : TextCol;
        Controls.Add(b); return b;
    }

    Button NewLink(string t, int x, int y)
    {
        var b = new Button
        {
            Text = t, Location = new Point(x, y), Size = new Size(TextRenderer.MeasureText(t, Font).Width + 20, 26),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, ForeColor = Accent, BackColor = Bg
        };
        b.FlatAppearance.BorderSize = 0;
        Controls.Add(b); return b;
    }

    void RefreshStatus()
    {
        if (InvokeRequired) { BeginInvoke((Action)RefreshStatus); return; }
        if (Worker.SpicetifyInstalled)
        {
            lblSpice.Text = "Spicetify: installiert (" + Worker.SpicetifyVersion() + ")";
            lblSpice.ForeColor = Accent;
            btnInstallSpice.Visible = false;
        }
        else
        {
            lblSpice.Text = "Spicetify: nicht gefunden";
            lblSpice.ForeColor = Color.FromArgb(235, 130, 90);
            btnInstallSpice.Visible = true;
        }
        bool exists = Worker.TaskExists();
        lblTask.Text = exists ? "Autostart: eingerichtet" : "Autostart: nicht eingerichtet";
        lblTask.ForeColor = exists ? Accent : Subtle;
        btnInstall.Text = exists ? "Aktualisieren" : "Einrichten";
        btnRemove.Enabled = exists;
    }

    void AppendLog(string msg)
    {
        if (txtLog.InvokeRequired) { txtLog.BeginInvoke((Action)delegate { AppendLog(msg); }); return; }
        txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine);
    }

    void SetBusy(bool busy, string what)
    {
        btnInstall.Enabled = btnApply.Enabled = btnInstallSpice.Enabled = !busy;
        btnRemove.Enabled = !busy && Worker.TaskExists();
        UseWaitCursor = busy;
        if (busy && what != null) AppendLog(what);
    }

    void RunAsync(string busyMsg, ThreadStart work)
    {
        SetBusy(true, busyMsg);
        var t = new Thread(delegate ()
        {
            try { work(); }
            catch (Exception ex) { AppendLog("FEHLER: " + ex.Message); }
            BeginInvoke((Action)delegate { SetBusy(false, null); RefreshStatus(); });
        });
        t.IsBackground = true;
        t.Start();
    }

    int DelaySeconds()
    {
        switch (cboDelay.SelectedIndex)
        {
            case 0: return 0;
            case 1: return 15;
            case 2: return 30;
            case 3: return 60;
            case 4: return 120;
            default: return 30;
        }
    }

    void DoInstall()
    {
        if (!chkLogon.Checked && !chkDaily.Checked)
        {
            MessageBox.Show("Bitte mindestens einen Zeitpunkt wählen (Anmeldung und/oder täglich).",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string applyArg = rbFresh.Checked ? "--apply-fresh" : "--apply";
        bool logon = chkLogon.Checked;
        int delay = DelaySeconds();
        bool daily = chkDaily.Checked;
        DateTime dt = dtDaily.Value;

        RunAsync("Richte Autostart ein...", delegate
        {
            string err;
            int code = Worker.Install(applyArg, logon, delay, daily, dt, out err);
            if (code != 0) { AppendLog("Fehler beim Einrichten: " + err.Trim()); return; }
            AppendLog("Autostart eingerichtet (" + (logon ? "Anmeldung" : "") +
                      (logon && daily ? " + " : "") + (daily ? "täglich " + dt.ToString("HH:mm") : "") + ").");
            int ac = Worker.Apply(rbFresh.Checked);
            AppendLog("Spicetify angewendet (ExitCode " + ac + ").");
        });
    }

    void DoApplyNow()
    {
        if (!Worker.SpicetifyInstalled)
        {
            MessageBox.Show("Spicetify ist nicht installiert. Bitte zuerst \"Spicetify installieren\".",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        bool fresh = rbFresh.Checked;
        RunAsync("Wende Spicetify an...", delegate
        {
            int ac = Worker.Apply(fresh);
            AppendLog("Spicetify angewendet (ExitCode " + ac + ").");
        });
    }

    void DoRemove()
    {
        if (MessageBox.Show("Autostart wirklich entfernen? Spicetify selbst bleibt installiert.",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        RunAsync("Entferne Autostart...", delegate
        {
            int code = Worker.Uninstall();
            AppendLog(code == 0 ? "Autostart entfernt." : "Konnte Autostart nicht entfernen (Code " + code + ").");
        });
    }

    void DoInstallSpicetify()
    {
        if (MessageBox.Show(
            "Der offizielle Spicetify-Installer von spicetify.app wird ausgeführt. Fortfahren?",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        RunAsync("Installiere Spicetify (offizielles Skript)...", delegate
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"iwr -useb https://raw.githubusercontent.com/spicetify/cli/main/install.ps1 | iex\"")
            { UseShellExecute = true };
            using (var p = Process.Start(psi)) { p.WaitForExit(); }
            AppendLog(Worker.SpicetifyInstalled ? "Spicetify ist jetzt installiert." : "Spicetify-Installation beendet.");
        });
    }

    void OpenLog()
    {
        try
        {
            if (!File.Exists(Worker.LogPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Worker.LogPath));
                File.WriteAllText(Worker.LogPath, "");
            }
            Process.Start("notepad.exe", Worker.LogPath);
        }
        catch (Exception ex) { AppendLog("Konnte Log nicht öffnen: " + ex.Message); }
    }
}

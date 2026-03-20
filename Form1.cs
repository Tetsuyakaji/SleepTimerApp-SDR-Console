using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SDRAlarm
{
    public partial class Form1 : Form
    {
        private DateTime startTimer2;
        private bool startTimerActive = false;
        private int remainingSeconds = 0;
        private int remainingSecondsTimer2 = 0;

        private readonly string settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SDRAlarm");
        private readonly string settingsFile;

        private string programPath;

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        public Form1()
        {
            settingsFile = Path.Combine(settingsDir, "settings.txt");

            InitializeComponent();

            // ensure timers configured (designer already sets Interval and some handlers)
            timer1.Interval = 1000;
            timer1.Tick += timer1_Tick;
            timer2.Interval = 1000;
            timer2.Tick += timer2_Tick;
            timer3.Interval = 1000;
            timer3.Tick += timer3_Tick;

            LoadProcesses();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    var lines = File.ReadAllLines(settingsFile);
                    if (lines.Length > 0)
                    {
                        programPath = lines[0].Trim();
                        if (!string.IsNullOrEmpty(programPath))
                            txtProgramPath.Text = programPath;
                    }
                }
            }
            catch
            {
                // ignore errors reading settings
            }
        }

        private void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(settingsDir))
                    Directory.CreateDirectory(settingsDir);

                File.WriteAllText(settingsFile, programPath ?? string.Empty);
            }
            catch
            {
                // ignore write errors
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dlg.Title = "Select program to launch";
                dlg.CheckFileExists = true;

                if (!string.IsNullOrEmpty(programPath))
                {
                    try
                    {
                        dlg.InitialDirectory = Path.GetDirectoryName(programPath);
                    }
                    catch { }
                }

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    programPath = dlg.FileName;
                    txtProgramPath.Text = programPath;
                    SaveSettings();
                    lblStatus.Text = "Program selection saved.";
                }
            }
        }

        private void LoadProcesses()
        {
            cmbProcesses.Items.Clear();

            var processes = Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(p => p.ToLower().Contains("sdr") ? 0 : 1)
                .ThenBy(p => p);

            foreach (var p in processes)
            {
                cmbProcesses.Items.Add(p);
            }

            if (cmbProcesses.Items.Count > 0)
                cmbProcesses.SelectedIndex = 0;
        }

        private void label1_Click(object sender, EventArgs e)
        {
            LoadProcesses();
            lblStatus.Text = "Process list loaded.";
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadProcesses();
            lblStatus.Text = "Process list refreshed.";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            lblCountdown.Text = "00:00:00";
            lblStatus.Text = "Timer canceled.";
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (cmbProcesses.SelectedItem == null)
            {
                MessageBox.Show("Select a program first.");
                return;
            }

            int hours = (int)numHours.Value;
            int minutes = (int)numMinutes.Value;

            remainingSeconds = (hours * 3600) + (minutes * 60);

            if (remainingSeconds <= 0)
            {
                MessageBox.Show("Set a time first.");
                return;
            }

            timer1.Start();
            lblStatus.Text = "Timer running...";
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            lblCountdown.Text = "00:00:00";
            lblStatus.Text = "Timer canceled.";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            remainingSeconds--;

            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
            lblCountdown.Text = time.ToString(@"hh\:mm\:ss");

            if (remainingSeconds <= 0)
            {
                timer1.Stop();
                CloseSelectedProcess();
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            // check and launch when alarm reached
            if (startTimerActive && DateTime.Now >= startTimer2)
            {
                startTimerActive = false;
                timer2.Stop();
                timer3.Stop();
                LaunchSDR();
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            // update alarm countdown display
            if (!startTimerActive)
                return;

            remainingSecondsTimer2--;

            if (remainingSecondsTimer2 <= 0)
            {
                remainingSecondsTimer2 = 0;
                lblAlarmCountdown.Text = "00:00:00";
                timer3.Stop();
                return;
            }

            TimeSpan ts = TimeSpan.FromSeconds(remainingSecondsTimer2);
            lblAlarmCountdown.Text = ts.ToString(@"hh\:mm\:ss");
        }

        private void LaunchSDR()
        {
            try
            {
                var pathToLaunch = programPath;
                // fall back to the original default if user hasn't picked anything
                if (string.IsNullOrEmpty(pathToLaunch))
                    pathToLaunch = @"C:\Program Files\SDR-Radio.com (V3)\SDR Console.exe";

                if (string.IsNullOrEmpty(pathToLaunch) || !File.Exists(pathToLaunch))
                {
                    MessageBox.Show("Program path not set or file not found. Please select a program to launch.", "Launch error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = "Launch failed: program not set.";
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = pathToLaunch,
                    UseShellExecute = true
                };
                Process.Start(psi);
                lblStatus.Text = "Program Started.";
                lblAlarmCountdown.Text = "00:00:00";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error starting program", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CloseSelectedProcess()
        {
            try
            {
                if (cmbProcesses.SelectedItem == null)
                    return;

                string processName = cmbProcesses.SelectedItem.ToString();

                var processes = Process.GetProcessesByName(processName);

                foreach (var p in processes)
                {
                    p.Kill();
                }

                lblStatus.Text = "Program closed. Sleep timer finished.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void lblCountdown_Click(object sender, EventArgs e)
        {
        }

        private void lblProcess_Click(object sender, EventArgs e)
        {
        }

        private void btnSetAlarm_Click(object sender, EventArgs e)
        {
            try
            {
                // compute the next occurrence of the selected time (today or tomorrow)
                var chosenTime = DateTime.Today + timeStart.Value.TimeOfDay;
                if (chosenTime <= DateTime.Now)
                    chosenTime = chosenTime.AddDays(1);

                startTimer2 = chosenTime;
                startTimerActive = true;

                // initialize countdown so timer3 can update UI
                remainingSecondsTimer2 = (int)Math.Max(0, (startTimer2 - DateTime.Now).TotalSeconds);

                // if the scheduled time is effectively now, launch immediately
                if (remainingSecondsTimer2 == 0)
                {
                    startTimerActive = false;
                    LaunchSDR();
                    lblStatus.Text = $"Program started at {DateTime.Now:hh:mm tt}.";
                    return;
                }

                timer2.Start(); // used to evaluate and launch when reached
                timer3.Start(); // used to update countdown display

                var remaining = TimeSpan.FromSeconds(remainingSecondsTimer2);
                lblAlarmCountdown.Text = remaining.ToString(@"hh\:mm\:ss");
                lblStatus.Text = $"Start scheduled: {startTimer2:hh:mm tt} (in {remaining:hh\\:mm\\:ss})";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error scheduling start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;



namespace SleepTimerApp
{
    public partial class Form1 : Form
    {
        private DateTime startTime;
        private DateTime startTimer2;
        private bool startTimerActive = false;
        private int remainingSeconds = 0;
        private int remainingSecondsTimer2 = 0;
        public Form1()
        {
            InitializeComponent();
            timer1.Interval = 1000;
            timer1.Tick += timer1_Tick;
            timer2.Interval = 1000;
            timer2.Tick += timer2_Tick;
            LoadProcesses();
        }
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

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
            /*// launch SDR if alarm reached
            if (startTimerActive && DateTime.Now >= startTime)
            {
                startTimerActive = false;
                LaunchSDR();
            }
            */
            remainingSeconds--;
            
            TimeSpan time = TimeSpan.FromSeconds(remainingSeconds);
            lblCountdown.Text = time.ToString(@"hh\:mm\:ss");

            if (remainingSeconds <= 0)
            {
                timer1.Stop();
                CloseSelectedProcess();
            }
        }
        ///Start APP
        private void timer2_Tick(object sender, EventArgs e)
        {
            // launch SDR if alarm reached
            if (startTimerActive && DateTime.Now >= startTimer2)
            {
                startTimerActive = false;
                LaunchSDR();
            }
            remainingSecondsTimer2--;
        }
        private void LaunchSDR()
        {
            try
            {
                Process.Start(@"C:\Program Files\SDR-Radio.com (V3)\SDR Console.exe");
                lblStatus.Text = "SDR Console started.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CloseSelectedProcess()
        {
            try
            {
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

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void lblProcess_Click(object sender, EventArgs e)
        {

        }


        private void btnSetAlarm_Click(object sender, EventArgs e)
        {
            startTimer2 = DateTime.Today + timeStart.Value.TimeOfDay;

            if (startTimer2 < DateTime.Now)
                startTimer2 = startTime.AddDays(1);

            startTimerActive = true;

            timer2.Start();

            lblStatus.Text = "SDR start scheduled."+ startTimer2.ToString("hh:mm tt");
        }
    }
}

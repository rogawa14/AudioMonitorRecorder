using NAudio.Wave;
using NAudio.Lame;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace AudioMonitorRecorder
{
    public partial class MainWindow : Window
    {
        WaveInEvent waveIn;
        WaveOutEvent waveOut;
        BufferedWaveProvider buffer;
        WaveFileWriter writer;

        string folderPath = "";
        string tempWav;

        DispatcherTimer timer;
        TimeSpan recordTime;

        DispatcherTimer recBlink;

        public MainWindow()
        {
            InitializeComponent();

            for (int i = 0; i < WaveIn.DeviceCount; i++)
                InputDeviceBox.Items.Add(WaveIn.GetCapabilities(i).ProductName);

            InputDeviceBox.SelectedIndex = 0;
            FormatBox.SelectedIndex = 0;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) =>
            {
                recordTime += TimeSpan.FromSeconds(1);
                TimeText.Text = recordTime.ToString(@"hh\:mm\:ss");
            };

            recBlink = new DispatcherTimer();
            recBlink.Interval = TimeSpan.FromMilliseconds(500);
            recBlink.Tick += (s, e) =>
            {
                RecText.Foreground = RecText.Foreground == System.Windows.Media.Brushes.Red
                    ? System.Windows.Media.Brushes.Gray
                    : System.Windows.Media.Brushes.Red;
            };

            LoadSettings();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                System.Windows.MessageBox.Show("保存先を選択してください");
                return;
            }

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;

            recordTime = TimeSpan.Zero;

            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = InputDeviceBox.SelectedIndex;
            waveIn.WaveFormat = new WaveFormat(44100, 2);

            buffer = new BufferedWaveProvider(waveIn.WaveFormat);

            if (MonitorCheck.IsChecked == true)
            {
                waveOut = new WaveOutEvent();
                waveOut.Init(buffer);
                waveOut.Play();
            }

            tempWav = Path.Combine(folderPath, "temp.wav");
            writer = new WaveFileWriter(tempWav, waveIn.WaveFormat);

            waveIn.DataAvailable += (s, a) =>
            {
                buffer.AddSamples(a.Buffer, 0, a.BytesRecorded);
                writer.Write(a.Buffer, 0, a.BytesRecorded);
            };

            waveIn.StartRecording();

            timer.Start();
            recBlink.Start();
            RecText.Text = "● REC";
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show("保存しますか？", "確認", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            waveIn?.StopRecording();
            waveOut?.Stop();
            writer?.Dispose();

            timer.Stop();
            recBlink.Stop();
            RecText.Text = "● STOP";
            RecText.Foreground = System.Windows.Media.Brushes.Gray;

            string name = FileNameBox.Text;
            if (string.IsNullOrWhiteSpace(name))
                name = "record_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string format = ((System.Windows.Controls.ComboBoxItem)FormatBox.SelectedItem).Content.ToString();
            string output = GetUniqueFilePath(Path.Combine(folderPath, name + "." + format.ToLower()));

            if (format == "WAV")
            {
                File.Move(tempWav, output, true);
            }
            else
            {
                using (var reader = new AudioFileReader(tempWav))
                using (var mp3 = new LameMP3FileWriter(output, reader.WaveFormat, 128))
                {
                    reader.CopyTo(mp3);
                }
                File.Delete(tempWav);
            }

            SaveSettings();

            System.Windows.MessageBox.Show("保存完了");

            Process.Start("explorer.exe", folderPath);

            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
        }

        private string GetUniqueFilePath(string path)
        {
            int count = 1;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            while (File.Exists(path))
            {
                path = Path.Combine(dir, $"{name} ({count}){ext}");
                count++;
            }
            return path;
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                folderPath = dialog.SelectedPath;
                FolderPathBox.Text = folderPath;
            }
        }

        private void SaveSettings()
        {
            File.WriteAllText("settings.txt", folderPath);
        }

        private void LoadSettings()
        {
            if (File.Exists("settings.txt"))
            {
                folderPath = File.ReadAllText("settings.txt");
                FolderPathBox.Text = folderPath;
            }
        }
    }
}
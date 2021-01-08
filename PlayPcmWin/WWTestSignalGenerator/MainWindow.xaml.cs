using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using WavRWLib2;

namespace WWTestSignalGenerator {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        BackgroundWorker mBW = new BackgroundWorker();

        public MainWindow() {
            InitializeComponent();

            textBoxLog.Text = "Ready.";

            mBW.DoWork += new DoWorkEventHandler(BwDoWork);
            mBW.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BwRunWorkerCompleted);

            textBoxMagnitude.Text  = string.Format("{0}", Properties.Settings.Default.SineMagnitudeDB);
            textBoxDuration.Text   = string.Format("{0}", Properties.Settings.Default.SignalDuration);
            textBoxOutputPath.Text = string.Format("{0}", Properties.Settings.Default.OutputPath);
        }

        private void LogMsg(string s) {
            textBoxLog.AppendText("\n" + s);
            textBoxLog.ScrollToEnd();
        }

        private void ErrMsg(string s) {
            LogMsg(s);
            MessageBox.Show(s);
        }

        class BackgroundWorkerArgs {
            public string path;
            public double magnitude;
            public double durationSec;
            public string errMsg;
            public BackgroundWorkerArgs(string aPath, double aMagnitude, double aDurationSec) {
                path = aPath;
                magnitude = aMagnitude;
                durationSec = aDurationSec;
                errMsg = "";
            }
        };


        private void buttonCreate_Click(object sender, RoutedEventArgs e) {
            string path = textBoxOutputPath.Text;

            double magnitude = -0.1;
            if (!double.TryParse(textBoxMagnitude.Text, out magnitude)){
                ErrMsg("Error: Sound Magnitude is not number");
                return;
            }
            if (0 <= magnitude) {
                ErrMsg("Error: Sound Magnitude is too big. should be less than 0");
                return;
            }

            double durationSec = 1;
            if (!double.TryParse(textBoxDuration.Text, out durationSec)){
                ErrMsg("Error: Sound Duration is not number");
                return;
            }

            LogMsg("Started.");

            buttonCreate.IsEnabled = false;
            groupBoxSettings.IsEnabled = false;
            var args = new BackgroundWorkerArgs(path, magnitude, durationSec);
            mBW.RunWorkerAsync(args);
        }

        void BwDoWork(object sender, DoWorkEventArgs e) {
            var args = e.Argument as BackgroundWorkerArgs;
            e.Result = args;

            short nCh = 1;
            int sampleFreq = 44100;

#if true
            // Square wave gen, max magnitude
            double signalFreq = 50; // 50Hz
            double duty = 0.25; // デューティー比。

            long nPeriod = (long)(sampleFreq / signalFreq);
            long nHalfPeriod = nPeriod / 2;
            long nSignalNum = (long)(nHalfPeriod * duty);
            long counter = 0;
            long nFrames = (long)(args.durationSec * sampleFreq);
            long nFrameBytes = nFrames * nCh * 4; // 8==sizeof float
#else
            // 11025Hz signal gen
            int signalFreq = sampleFreq / 4;
            long nPeriod = (long)(args.durationSec * signalFreq);
            long nFrames = nPeriod * 4;
            long nFrameBytes = nFrames * nCh * 4; // 8==sizeof float
#endif

            if (0x7fff0000 < nFrameBytes) {
                args.errMsg = "Error: Sound Duration is too long!";
            }

            try {
                using (var bw = new BinaryWriter(File.Open(args.path, FileMode.Create))) {
                    var wwl = new WavWriterLowLevel();
                    int dataChunkSize = (int)(nFrameBytes);
                    int riffChunkSize = 4 /* RIFF */
                        + 26 /* fmt */
                        + 8  /* DATA */
                        + dataChunkSize;

                    wwl.RiffChunkWrite(bw, riffChunkSize);
                    wwl.FmtChunkWriteEx(bw, nCh, sampleFreq, 32, WavWriterLowLevel.WAVE_FORMAT_IEEE_FLOAT, 0);
                    wwl.DataChunkHeaderWrite(bw, dataChunkSize);
#if true
                    // Square wave gen, max magnitude
                    for (int i = 0; i < nFrames; ++i) {
                        float v = 0.0f;
                        if (0 == (i % nHalfPeriod)) {
                            if (0 == (i % nPeriod)) {
                                // +の信号出力開始。
                                counter = nSignalNum;
                            } else {
                                // -の信号出力開始。
                                counter = -nSignalNum;
                            }
                        }

                        if (0 < counter) {
                            // +の信号を出力。
                            v = 32767.0f / 32768.0f;
                            --counter;
                        } else if (counter < 0) {
                            // -の信号を出力。
                            v = -32767.0f / 32768.0f;
                            ++counter;
                        } else {
                            // 0を出力。
                            v = 0.0f;
                        }

                        bw.Write(v);
                    }
#else
                    // 11025Hz signal gen
                    for (int i = 0; i < nPeriod; ++i) {
                        var signal = new float[] {
                            0.0f,
                            +(float)Math.Pow(10, args.magnitude / 20.0),
                            0.0f,
                            -(float)Math.Pow(10, args.magnitude / 20.0),
                        };
                        bw.Write(signal[0]);
                        bw.Write(signal[1]);
                        bw.Write(signal[2]);
                        bw.Write(signal[3]);
                    }
#endif
                }
            } catch (System.Exception ex) {
                args.errMsg = ex.ToString();
            }
            args.errMsg = "";
        }

        void BwRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            buttonCreate.IsEnabled = true;
            groupBoxSettings.IsEnabled = true;

            var r = e.Result as BackgroundWorkerArgs;
            if (0 < r.errMsg.Length) {
                ErrMsg(r.errMsg);
            } else {
                LogMsg(string.Format("Success: \"{0}\" {1} dBFS peak, {2} sec", r.path, r.magnitude, r.durationSec));
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            double signalMagnitudeDB = -3.0102999566398;
            double signalDuration    = 1800;

            if (double.TryParse(textBoxMagnitude.Text, out signalMagnitudeDB)) {
                Properties.Settings.Default.SineMagnitudeDB = signalMagnitudeDB;
            }

            if (double.TryParse(textBoxDuration.Text, out signalDuration)) {
                Properties.Settings.Default.SignalDuration = signalDuration;
            }

            Properties.Settings.Default.OutputPath = textBoxOutputPath.Text;

            Properties.Settings.Default.Save();
        }
    }
}

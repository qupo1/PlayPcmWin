﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;

namespace SCPIAcquisition {
    public partial class MainWindow : Window {
        private SerialRW mSerial = new SerialRW();
        private const int MAX_LOG_LINES = 1000;
        private List<string> mLogStringList = new List<string>();
        BackgroundWorker mBW;

        private ScpiCommands.MeasureType mMeasureType = ScpiCommands.MeasureType.DC_V;

        private ScpiCommands mScpi = new ScpiCommands();

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private void AddLog(string s) {
            mLogStringList.Add(s);
            while (MAX_LOG_LINES < mLogStringList.Count) {
                mLogStringList.RemoveAt(0);
            }

            var sb = new StringBuilder();
            foreach (var item in mLogStringList) {
                sb.Append(item);
            }
            textBoxLog.Text = sb.ToString();
            textBoxLog.ScrollToEnd();
        }

        public MainWindow() {
            InitializeComponent();
            mBW = new BackgroundWorker();
            mBW.DoWork += new DoWorkEventHandler(mBW_DoWork);
            mBW.WorkerReportsProgress = true;
            mBW.ProgressChanged += new ProgressChangedEventHandler(mBW_ProgressChanged);
            mBW.WorkerSupportsCancellation = true;
            mBW.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mBW_RunWorkerCompleted);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            AddLog(string.Format("SCPIAcquisition version {0}\n", AssemblyVersion));
            UpdateComList();
        }

        private void UpdateComList() {
            var comPortList = mSerial.EnumerateComPorts();
            comboBoxComPorts.Items.Clear();
            foreach (var s in comPortList) {
                comboBoxComPorts.Items.Add(s);
            }

            if (0 < comPortList.Length) {
                comboBoxComPorts.SelectedIndex = 0;
                buttonConnect.IsEnabled = true;
            } else {
                buttonConnect.IsEnabled = false;
            }

            AddLog("Updated com port list.\n");
        }

        private void ButtonUpdateComList_Click(object sender, RoutedEventArgs e) {
            UpdateComList();
        }

        private int[] mBaudRateList = new int[] {
            9600,
            115200,
        };

        private System.IO.Ports.StopBits[] mStopBitList = new System.IO.Ports.StopBits[] {
            System.IO.Ports.StopBits.One,
            System.IO.Ports.StopBits.Two,
        };

        class BWArgs {
            public int portIdx;
            public int baud;
            public int dataBits;
            public System.IO.Ports.StopBits stopBits;
            public System.IO.Ports.Parity parity;
            public BWArgs(int aPortIdx, int aBaud, int aDataBits, System.IO.Ports.StopBits aStopBits, System.IO.Ports.Parity aParity) {
                portIdx = aPortIdx;
                baud = aBaud;
                dataBits = aDataBits;
                stopBits = aStopBits;
                parity = aParity;
            }
        };

        private void buttonConnect_Click(object sender, RoutedEventArgs e) {
            int portIdx = comboBoxComPorts.SelectedIndex;
            int baud = mBaudRateList[comboBoxComSpeed.SelectedIndex];
            int dataBits = 8;
            System.IO.Ports.StopBits stopBits = mStopBitList[comboBoxComStopBits.SelectedIndex];

            mBW.RunWorkerAsync(new BWArgs(portIdx, baud, dataBits, stopBits, System.IO.Ports.Parity.None));
        }

        private void Window_Closed(object sender, EventArgs e) {
            mBW.CancelAsync();

            while (mBW.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));
                System.Threading.Thread.Sleep(100);
            }

            mBW.Dispose();
            mBW = null;
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // シリアル通信するスレッド。

        private const int REPORT_CONNECTED = 0;
        private const int REPORT_CMD_RESULT = 10;

        void mBW_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = "";
            var args = e.Argument as BWArgs;

            System.Diagnostics.Debug.Assert(!mSerial.IsConnected);

            try {
                mSerial.Connect(args.portIdx, args.baud, args.parity, args.dataBits,args.stopBits);
            } catch (Exception ex) {
                mSerial.Disconnect();
                e.Result = ex.ToString();
                return;
            }

            // 成功。
            mScpi.SetSerial(mSerial);

            // IDの問い合わせ。
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.IDN));

            mBW.ReportProgress(REPORT_CONNECTED, args);
            System.Threading.Thread.Sleep(500);

            while (!mBW.CancellationPending) {
                if (!mSerial.IsConnected) {
                    return;
                }

                int nCmd = mScpi.ExecCmd();
                if (0 < nCmd) {
                    // 実行結果を取り出してUIにフィードバックする。
                    var cmdList = mScpi.GetResults(nCmd);
                    foreach (var c in cmdList) {
                        if (0 < c.result.Length) {
                            mBW.ReportProgress(REPORT_CMD_RESULT, c);
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                } else {
                    // 測定する。
                    mScpi.SetCmd(new ScpiCommands.Cmd(mMeasureType));
                }
            }

            if (mSerial.IsConnected) {
                mScpi.Term();
                mSerial.Disconnect();
            }
        }

        void mBW_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            if (REPORT_CONNECTED == e.ProgressPercentage) {
                // 接続成功。
                var bwArgs = e.UserState as BWArgs;
                groupBoxConnection.IsEnabled = false;
                buttonConnect.IsEnabled = false;
                groupBoxControls.IsEnabled = true;
                AddLog(string.Format("Connected. {0}, {1} baud, {2} bit, Stop bit={3}, Parity={4}\n",
                    (string)comboBoxComPorts.SelectedValue, bwArgs.baud, bwArgs.dataBits, bwArgs.stopBits, bwArgs.parity));
            }
            if (REPORT_CMD_RESULT == e.ProgressPercentage) {
                // 測定結果表示。
                var cmd = e.UserState as ScpiCommands.Cmd;
                //AddLog(string.Format("{0} {1} {2}\n", cmd.ct, cmd.mt, cmd.result));
                ResultDisp(cmd);
            }
        }

        private string FormatNumber(string s) {
            double v = 0;

            string unit = "";
            if (double.TryParse(s, out v)) {
                bool bMinus = false;
                if (v < 0) {
                    bMinus = true;
                    v = -v;
                }
                if (10e15 < v) {
                    return string.Format("{0} ∞ ", bMinus ? "-" : "");
                }else if (v < 0.001 * 0.001 * 0.001) {
                    unit = "p";
                    v *= 1000.0 * 1000 * 1000 * 1000;
                } else if (v < 0.001 * 0.001) {
                    unit = "n";
                    v *= 1000.0 * 1000 * 1000;
                } else if (v < 0.001) {
                    unit = "μ";
                    v *= 1000.0 * 1000;
                } else if (v < 1) {
                    unit = "m";
                    v *= 1000.0;
                } else if (1000.0 * 1000 * 1000 <= v) {
                    unit = "G";
                    v /= 1000.0 * 1000 * 1000;
                } else if (1000.0 * 1000 <= v) {
                    unit = "M";
                    v /= 1000.0 * 1000;
                } else if (1000.0 <= v) {
                    unit = "k";
                    v /= 1000.0;
                }

                if (v < 10) {
                    return string.Format("{0}{1:0.0000000} {2}", bMinus ? "-" : "+", v, unit);
                }
                if (v < 100) {
                    return string.Format("{0}{1:0.000000} {2}", bMinus ? "-" : "+", v, unit);
                }
                return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "+", v, unit);
            } else {
                return "Err ";
            }
        }

        private void ResultDisp(ScpiCommands.Cmd cmd) {
            string number = FormatNumber(cmd.result);

            switch (cmd.ct) {
            case ScpiCommands.CmdType.IDN:
                AddLog(string.Format("IDN: {0}\n", cmd.result));
                return;
            case ScpiCommands.CmdType.Measure:
                switch (cmd.mt) {
                case ScpiCommands.MeasureType.DC_V:
                    textBlockMeasureType.Text = "DC Voltage";
                    textBlockMeasuredValue.Text = string.Format("{0}V", number);
                    break;
                case ScpiCommands.MeasureType.AC_V:
                    textBlockMeasureType.Text = "AC Voltage";
                    textBlockMeasuredValue.Text = string.Format("{0}V", number);
                    break;
                case ScpiCommands.MeasureType.DC_A:
                    textBlockMeasureType.Text = "DC Current";
                    textBlockMeasuredValue.Text = string.Format("{0}A", number);
                    break;
                case ScpiCommands.MeasureType.AC_A:
                    textBlockMeasureType.Text = "AC Current";
                    textBlockMeasuredValue.Text = string.Format("{0}A", number);
                    break;
                case ScpiCommands.MeasureType.Resistance:
                    textBlockMeasureType.Text = "Resistance";
                    textBlockMeasuredValue.Text = string.Format("{0}Ω", number);
                    break;
                case ScpiCommands.MeasureType.Capacitance:
                    textBlockMeasureType.Text = "Capacitance";
                    textBlockMeasuredValue.Text = string.Format("{0}F", number);
                    break;
                case ScpiCommands.MeasureType.Frequency:
                    textBlockMeasureType.Text = "Frequency";
                    textBlockMeasuredValue.Text = string.Format("{0}Hz", number);
                    break;
                }
                return;
            default:
                return;
            }

        }

        void mBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var result = e.Result as string;
            if (0 < result.Length) {
                // エラーメッセージ。
                AddLog(result);
            }

            buttonConnect.IsEnabled = true;
            groupBoxControls.IsEnabled = false;
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        private void buttonBeep_Click(object sender, RoutedEventArgs e) {
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.Beep));
        }

        private void checkBoxDisplay_Checked(object sender, RoutedEventArgs e) {
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.LcdDisplayOn));
        }

        private void checkBoxDisplay_Unchecked(object sender, RoutedEventArgs e) {
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.LcdDisplayOff));
        }

        private void buttonReset_Click(object sender, RoutedEventArgs e) {
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.Reset));
        }

        private void radioButtonDCV_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.DC_V;
        }

        private void radioButtonACV_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.AC_V;
        }

        private void radioButtonResistance_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.Resistance;
        }

        private void radioButtonDCA_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.DC_A;
        }

        private void radioButtonACA_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.AC_A;
        }

        private void radioButtonFrequency_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.Frequency;
        }

        private void radioButtonCapacitance_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.Capacitance;
        }

    }
}

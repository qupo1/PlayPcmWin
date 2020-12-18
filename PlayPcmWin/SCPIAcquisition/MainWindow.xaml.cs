using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace SCPIAcquisition {
    public partial class MainWindow : Window {
        private bool mInitialized = false;

        private const int MAX_LOG_LINES = 1000;
        private const int MEASUREMENT_CHANGED_DISCARD_NUM = 1;

        private SerialRW mSerial = new SerialRW();
        private List<string> mLogStringList = new List<string>();
        private BackgroundWorker mBW;

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
            Title = string.Format("SCPIAcquisition {0}\n", AssemblyVersion);

            AddLog(string.Format("SCPIAcquisition version {0}\n", AssemblyVersion));
            UpdateComList();

            // 設定を読んでUIに反映します。
            // MeasureTypeラジオボタンの何れかがCheckされ、MeasureTypeChanged()が呼び出されます。
            SettingsToUI();

            // UIのローカライズ実行。
            LocalizeUI();

            graph.GraphTitle = Properties.Resources.DCVoltage;
            graph.XAxisText = string.Format("{0} ({1})", Properties.Resources.Time, Properties.Resources.Unit_Time);
            graph.YAxisText = string.Format("{0} ({1})", Properties.Resources.DCVoltage, Properties.Resources.Unit_Voltage);
            graph.Clear();
            graph.Add(new WWMath.WWVectorD2(0, 0));
            graph.Redraw();

            mInitialized = true;

            // ディスプレイのON/OFFの設定値を計測器に送出。
            mScpi.SetCmd(new ScpiCommands.Cmd(
                Properties.Settings.Default.FrontPanelDisp ?
                    ScpiCommands.CmdType.LcdDisplayOn:
                    ScpiCommands.CmdType.LcdDisplayOff));
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

            UIToSettings();
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

            AddLog("Updated Com port list.\n");
        }

        private void ButtonUpdateComList_Click(object sender, RoutedEventArgs e) {
            UpdateComList();
        }

        // ComboBoxの項目と同じ順に並べる。
        private int[] mBaudRateList = new int[] {
            9600,
            115200,
        };

        private int BaudRateToIdx(int baud) {
            switch (baud) {
            case 9600:
            default: //< 設定値はファイルから読みだされるので異常な値が来ることがある。
                return 0;
            case 115200:
                return 1;
            }
        }

        private System.IO.Ports.StopBits[] mStopBitList = new System.IO.Ports.StopBits[] {
            System.IO.Ports.StopBits.One,
            System.IO.Ports.StopBits.Two,
        };

        private int StopBitsStrToIdx(string stopBits) {
            switch (stopBits) {
            case "One":
            default: //< 設定値はファイルから読みだされるので異常な値が来ることがある。
                return 0;
            case "Two":
                return 1;
            }
        }

        private System.IO.Ports.Parity[] mParityList = new System.IO.Ports.Parity[] {
            System.IO.Ports.Parity.None,
            System.IO.Ports.Parity.Odd,
            System.IO.Ports.Parity.Even,
        };

        private int ParityStrToIdx(string parityStr) {
            switch (parityStr) {
            case "None":
            default: //< 設定値はファイルから読みだされるので異常な値が来ることがある。
                return 0;
            case "Odd":
                return 1;
            case "Even":
                return 2;
            }
        }

        private int[] mDataBitList = new int[] {
            7,
            8,
        };

        private int DataBitsToIdx(int dataBits) {
            switch (dataBits) {
            case 7:
                return 0;
            case 8:
            default: //< 設定値はファイルから読みだされるので異常な値が来ることがある。
                return 1;
            }
        }

        private int[] mDispDigitsList = new int[] {
            4,
            5,
            6,
            7,
            8
        };

        private int DispDigitsToIdx(int dispDigits) {
            switch (dispDigits) {
            case 4:
                return 0;
            case 5:
                return 1;
            case 6:
                return 2;
            case 7:
                return 3;
            case 8:
            default: //< 設定値はファイルから読みだされるので異常な値が来ることがある。
                return 4;
            }
        }

        private void SettingsToUI() {
            for (int i = 0; i < comboBoxComPorts.Items.Count;++i) {
                var s = (string)comboBoxComPorts.Items[i];
                if (0 == string.Compare(s, Properties.Settings.Default.ComPortName)) {
                    comboBoxComPorts.SelectedIndex = i;
                }
            }

            comboBoxComBaudRate.SelectedIndex = BaudRateToIdx(Properties.Settings.Default.BaudRate);
            comboBoxComDataBits.SelectedIndex = DataBitsToIdx(Properties.Settings.Default.DataBits);
            comboBoxComParity.SelectedIndex = ParityStrToIdx(Properties.Settings.Default.Parity);
            comboBoxComStopBits.SelectedIndex = StopBitsStrToIdx(Properties.Settings.Default.StopBits);

            comboBoxDispDigits.SelectedIndex = DispDigitsToIdx(Properties.Settings.Default.DispDigits);

            checkBoxDisplay.IsChecked = Properties.Settings.Default.FrontPanelDisp;

            switch (Properties.Settings.Default.MeasurementFunction) {
            case "DCV":
            default: //< 設定値はファイルから読みだされるので異常な値が来ることがある。
                radioButtonDCV.IsChecked = true;
                break;
            case "ACV":
                radioButtonACV.IsChecked = true;
                break;
            case "DCA":
                radioButtonDCA.IsChecked = true;
                break;
            case "ACA":
                radioButtonACA.IsChecked = true;
                break;
            case "Resistance":
                radioButtonResistance.IsChecked = true;
                break;
            case "Capacitance":
                radioButtonCapacitance.IsChecked = true;
                break;
            case "Frequency":
                radioButtonFrequency.IsChecked = true;
                break;
            }

            graph.ShowGrid = Properties.Settings.Default.ShowGrid;
            graph.ShowStartEndTime = Properties.Settings.Default.ShowStartEndTime;
        }

        private void UIToSettings() {
            Properties.Settings.Default.ComPortName = (string)comboBoxComPorts.SelectedItem;

            Properties.Settings.Default.BaudRate = mBaudRateList[comboBoxComBaudRate.SelectedIndex];
            Properties.Settings.Default.DataBits = mDataBitList[comboBoxComDataBits.SelectedIndex];
            Properties.Settings.Default.Parity = mParityList[comboBoxComParity.SelectedIndex].ToString();
            Properties.Settings.Default.StopBits = mStopBitList[comboBoxComStopBits.SelectedIndex].ToString();

            Properties.Settings.Default.DispDigits = mDispDigitsList[comboBoxDispDigits.SelectedIndex];
            Properties.Settings.Default.FrontPanelDisp = checkBoxDisplay.IsChecked == true ? true : false;

            if (radioButtonDCV.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "DCV";
            } else if (radioButtonACV.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "ACV";
            } else if (radioButtonDCA.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "DCA";
            } else if (radioButtonACA.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "ACA";
            } else if (radioButtonResistance.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "Resistance";
            } else if (radioButtonCapacitance.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "Capacitance";
            } else if (radioButtonFrequency.IsChecked == true) {
                Properties.Settings.Default.MeasurementFunction = "Frequency";
            } else {
                throw new NotImplementedException();
            }

            Properties.Settings.Default.ShowGrid = graph.ShowGrid;
            Properties.Settings.Default.ShowStartEndTime = graph.ShowStartEndTime;

            // セット完了。保存する。

            Properties.Settings.Default.Save();
        }

        private void LocalizeUI() {
            groupBoxSettings.Header = Properties.Resources.Settings;
            groupBoxConnection.Header = Properties.Resources.ConnectionSettings;
            groupBoxControls.Header = Properties.Resources.Controls;
            groupBoxLog.Header = Properties.Resources.Log;
            groupBoxMeasuredValue.Header = Properties.Resources.LastMeasuredValue;
            groupBoxMeasurementFunction.Header = Properties.Resources.MeasurementFunction;
            buttonUpdate.Content = Properties.Resources.Update;
            buttonConnect.Content = Properties.Resources.Connect;
            buttonReset.Content = Properties.Resources.Reset;
            buttonBeep.Content = Properties.Resources.Beep;
            buttonSaveAs.Content = Properties.Resources.SaveDataAsCsv;
            checkBoxDisplay.Content = Properties.Resources.FrontPanelDisplay;
            radioButtonACA.Content = Properties.Resources.ACCurrent;
            radioButtonACV.Content = Properties.Resources.ACVoltage;
            radioButtonDCA.Content = Properties.Resources.DCCurrent;
            radioButtonDCV.Content = Properties.Resources.DCVoltageRadioItem;
            radioButtonFrequency.Content = Properties.Resources.Frequency;
            radioButtonCapacitance.Content = Properties.Resources.Capacitance;
            radioButtonResistance.Content = Properties.Resources.Resistance;
            cbItem4Digits.Content = string.Format("4 {0}", Properties.Resources.Digits);
            cbItem5Digits.Content = string.Format("5 {0}", Properties.Resources.Digits);
            cbItem6Digits.Content = string.Format("6 {0}", Properties.Resources.Digits);
            cbItem7Digits.Content = string.Format("7 {0}", Properties.Resources.Digits);
            cbItem8Digits.Content = string.Format("8 {0}", Properties.Resources.Digits);
        }

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

        Stopwatch mSW = new Stopwatch();

        private void buttonConnect_Click(object sender, RoutedEventArgs e) {
            groupBoxConnection.IsEnabled = false;
            buttonConnect.IsEnabled = false;
            groupBoxControls.IsEnabled = false;

            int portIdx = comboBoxComPorts.SelectedIndex;
            int baud = mBaudRateList[comboBoxComBaudRate.SelectedIndex];
            int dataBits = mDataBitList[comboBoxComDataBits.SelectedIndex];
            var parity = mParityList[comboBoxComParity.SelectedIndex];
            var stopBits = mStopBitList[comboBoxComStopBits.SelectedIndex];

            graph.Clear();
            mSW.Restart();
            graph.Redraw();

            mBW.RunWorkerAsync(new BWArgs(portIdx, baud, dataBits, stopBits, parity));
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
                mSerial.Connect(args.portIdx, args.baud, args.parity, args.dataBits, args.stopBits);
            } catch (Exception ex) {
                // 接続が失敗。
                mSerial.Disconnect();
                e.Result = ex.ToString();
                return;
            }

            // 接続成功。
            mScpi.SetSerial(mSerial);

            mBW.ReportProgress(REPORT_CONNECTED, args);
            System.Threading.Thread.Sleep(500);

            while (!mBW.CancellationPending) {
                if (!mSerial.IsConnected) {
                    return;
                }

                int nCmd = 0;
                try {
                    nCmd = mScpi.ExecCmd();
                } catch (TimeoutException ex) {
                    // 機器が接続されていない等。
                    // スレッドがエラー終了する。
                    e.Result = ex.ToString();
                    break;
                }

                if (0 < nCmd) {
                    // 実行結果を取り出してUIにフィードバックする。
                    var cmdList = mScpi.GetResults(nCmd);
                    foreach (var c in cmdList) {
                        mBW.ReportProgress(REPORT_CMD_RESULT, c);
                        System.Threading.Thread.Sleep(500);
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
                AddLog(string.Format("Connected. {0}, {1} Baud, Data bits={2} bit, Stop bits={3}, Parity={4}.\n",
                    (string)comboBoxComPorts.SelectedValue, bwArgs.baud, bwArgs.dataBits, bwArgs.stopBits, bwArgs.parity));
            }
            if (REPORT_CMD_RESULT == e.ProgressPercentage) {
                // 測定結果表示。
                var cmd = e.UserState as ScpiCommands.Cmd;
                //AddLog(string.Format("{0} {1} {2}\n", cmd.ct, cmd.mt, cmd.result));
                ResultDisp(cmd);
            }
        }

        void mBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var result = e.Result as string;
            if (0 < result.Length) {
                // エラーメッセージ。
                AddLog(result);
                MessageBox.Show(result, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            groupBoxConnection.IsEnabled = true;
            buttonConnect.IsEnabled = true;
            groupBoxControls.IsEnabled = false;

            mSW.Stop();
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // 画面表示。

        /// <summary>
        /// UIスレッドから呼び出す必要あり。
        /// </summary>
        private int DispDigits() {
            return mDispDigitsList[comboBoxDispDigits.SelectedIndex];
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
                } else if (v < 0.001 * 0.001 * 0.001) {
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

                switch (DispDigits()) {
                case 4:
                    if (v < 10) {
                        return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    if (v < 100) {
                        return string.Format("{0}{1:0.00} {2}", bMinus ? "-" : "", v, unit);
                    }
                    return string.Format("{0}{1:0.0} {2}", bMinus ? "-" : "", v, unit);
                case 5:
                    if (v < 10) {
                        return string.Format("{0}{1:0.0000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    if (v < 100) {
                        return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    return string.Format("{0}{1:0.00} {2}", bMinus ? "-" : "", v, unit);
                case 6:
                    if (v < 10) {
                        return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    if (v < 100) {
                        return string.Format("{0}{1:0.0000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
                case 7:
                    if (v < 10) {
                        return string.Format("{0}{1:0.000000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    if (v < 100) {
                        return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    return string.Format("{0}{1:0.0000} {2}", bMinus ? "-" : "", v, unit);
                case 8:
                    if (v < 10) {
                        return string.Format("{0}{1:0.0000000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    if (v < 100) {
                        return string.Format("{0}{1:0.000000} {2}", bMinus ? "-" : "", v, unit);
                    }
                    return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "", v, unit);
                default:
                    throw new ArgumentException();
                }
            } else {
                return "Err ";
            }
        }

        private void PlotNewValue(ScpiCommands.Cmd cmd, string measureTypeStr, string unitStr) {
            string numberStr = cmd.result;

            {
                string numberWithPrefix = FormatNumber(numberStr);

                textBlockMeasureType.Text = measureTypeStr;
                textBlockMeasuredValue.Text = string.Format("{0}{1}", numberWithPrefix, unitStr);
            }

            if (mMeasureType != cmd.mt) {
                // 計測種類の切り替え直後に戻る計測値は、切り替え前の値の場合がある。
                // グラフに追加しない。
                return;
            }

            {
                double v;
                graph.GraphTitle = measureTypeStr;
                graph.YAxisText = string.Format("{0} ({1})", measureTypeStr, unitStr);
                if (double.TryParse(numberStr, out v)) {
                    graph.Add(new WWMath.WWVectorD2(0.001 * mSW.ElapsedMilliseconds, v));
                    graph.Redraw();
                }
            }
        }

        private void ResultDisp(ScpiCommands.Cmd cmd) {
            switch (cmd.ct) {
                case ScpiCommands.CmdType.IDN:
                    //AddLog(string.Format("IDN: {0}\n", cmd.result));
                    return;
                case ScpiCommands.CmdType.Reset:
                    AddLog("Reset command sent.\n");
                    break;
                case ScpiCommands.CmdType.Beep:
                    AddLog("Beep command sent.\n");
                    break;
                case ScpiCommands.CmdType.LcdDisplayOff:
                    AddLog("Display Off command sent.\n");
                    break;
                case ScpiCommands.CmdType.LcdDisplayOn:
                    AddLog("Display On command sent.\n");
                    break;
                case ScpiCommands.CmdType.Measure:
                    switch (cmd.mt) {
                        case ScpiCommands.MeasureType.DC_V:
                            PlotNewValue(cmd, Properties.Resources.DCVoltage, Properties.Resources.Unit_Voltage);
                            break;
                        case ScpiCommands.MeasureType.AC_V:
                            PlotNewValue(cmd, Properties.Resources.ACVoltage, Properties.Resources.Unit_Voltage);
                            break;
                        case ScpiCommands.MeasureType.DC_A:
                            PlotNewValue(cmd, Properties.Resources.DCCurrent, Properties.Resources.Unit_Current);
                            break;
                        case ScpiCommands.MeasureType.AC_A:
                            PlotNewValue(cmd, Properties.Resources.ACCurrent, Properties.Resources.Unit_Current);
                            break;
                        case ScpiCommands.MeasureType.Resistance:
                            PlotNewValue(cmd, Properties.Resources.Resistance, Properties.Resources.Unit_Resistance);
                            break;
                        case ScpiCommands.MeasureType.Capacitance:
                            PlotNewValue(cmd, Properties.Resources.Capacitance, Properties.Resources.Unit_Capacitance);
                            break;
                        case ScpiCommands.MeasureType.Frequency:
                            PlotNewValue(cmd, Properties.Resources.Frequency, Properties.Resources.Unit_Frequency);
                            break;
                    }
                    return;
                default:
                    return;
            }
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        private void buttonBeep_Click(object sender, RoutedEventArgs e) {
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.Beep));
        }

        private void checkBoxDisplay_Checked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.LcdDisplayOn));
        }

        private void checkBoxDisplay_Unchecked(object sender, RoutedEventArgs e) {
            if (!mInitialized) {
                return;
            }
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.LcdDisplayOff));
        }

        private void buttonReset_Click(object sender, RoutedEventArgs e) {
            mScpi.SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.Reset));
        }

        private void MeasureTypeChanged() {
            graph.Clear();
            graph.Redraw();
            mSW.Restart();
        }

        private void radioButtonDCV_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.DC_V;
            MeasureTypeChanged();
        }

        private void radioButtonACV_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.AC_V;
            MeasureTypeChanged();
        }

        private void radioButtonResistance_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.Resistance;
            MeasureTypeChanged();
        }

        private void radioButtonDCA_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.DC_A;
            MeasureTypeChanged();
        }

        private void radioButtonACA_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.AC_A;
            MeasureTypeChanged();
        }

        private void radioButtonFrequency_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.Frequency;
        }

        private void radioButtonCapacitance_Checked(object sender, RoutedEventArgs e) {
            mMeasureType = ScpiCommands.MeasureType.Capacitance;
            MeasureTypeChanged();
        }

        private void buttonSaveAs_Click(object sender, RoutedEventArgs e) {
            var sfd = new SaveFileDialog();
            sfd.AddExtension = true;
            sfd.DefaultExt=".csv";
            sfd.ValidateNames = true;
            sfd.Filter = "CSV File|*.csv";
            var r = sfd.ShowDialog();

            if (r != true) {
                return;
            }

            bool bSuccess = false;
            try {
                using (var sw = new StreamWriter(sfd.FileName)) {
                    sw.WriteLine("Measurement started at {0}", graph.StartDateTime);
                    sw.WriteLine("Time(seconds), {0}", graph.YAxisText);

                    var data = graph.PlotData();
                    foreach (var v in data) {
                        sw.WriteLine("{0}, {1}", v.X, v.Y);
                    }
                }

                bSuccess = true;
            } catch (IOException ex) {
                MessageBox.Show(ex.ToString(), "File Write Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (bSuccess) {
                AddLog(string.Format("Save as {0}", sfd.FileName));
            } else {
                AddLog(string.Format("Error: Failed to save {0}", sfd.FileName));
            }
        }
    }
}

﻿// 日本語。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WWSpatialAudioUserCs;

namespace WWSpatialAudioPlayer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable {
        private bool mInitialized = false;
        private List<VirtualSpeakerProperty> mVirtualSpeakerList = new List<VirtualSpeakerProperty>();
        private SpatialAudioPlayer mPlayer = new SpatialAudioPlayer();
        private BackgroundWorker mBwLoad = new BackgroundWorker();
        private BackgroundWorker mBwPlay = new BackgroundWorker();

        /// <summary>
        /// ログの表示行数。
        /// </summary>
        private const int LOG_LINE_NUM = 100;

        private List<string> mLogList = new List<string>();
        private Stopwatch mSWProgressReport = new Stopwatch();
        private const int MESSAGE_INTERVAL_MS = 1000;
        private StringBuilder mBwMsgSB = new StringBuilder();
        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private void AddLog(string s) {
            // Console.Write(s);

            // ログを適当なエントリ数で流れるようにする。
            // sは複数行の文字列が入っていたり、改行が入っていなかったりするので、行数制限にはなっていない。
            mLogList.Add(s);
            while (LOG_LINE_NUM < mLogList.Count) {
                mLogList.RemoveAt(0);
            }

            var sb = new StringBuilder();
            foreach (var item in mLogList) {
                sb.Append(item);
            }

            mTextBoxLog.Text = sb.ToString();
            mTextBoxLog.ScrollToEnd();
        }

        public MainWindow() {
            InitializeComponent();

            mTextBoxLog.Text = "";
            AddLog(string.Format(CultureInfo.InvariantCulture, "WWSpatialAudioPlayer {0} {1}{2}",
                AssemblyVersion, IntPtr.Size == 8 ? "64bit" : "32bit", Environment.NewLine));

            mBwLoad.DoWork += MBwLoad_DoWork;
            mBwLoad.RunWorkerCompleted += MBwLoad_RunWorkerCompleted;
            mBwLoad.WorkerReportsProgress = true;
            mBwLoad.ProgressChanged += MBwLoad_ProgressChanged;
            mBwLoad.WorkerSupportsCancellation = true;

            mBwPlay.DoWork += MBwPlay_DoWork;
            mBwPlay.RunWorkerCompleted += MBwPlay_RunWorkerCompleted;
            mBwPlay.WorkerReportsProgress = true;
            mBwPlay.ProgressChanged += MBwPlay_ProgressChanged;
            mBwPlay.WorkerSupportsCancellation = true;

            UpdateDeviceList();
            mSWProgressReport.Restart();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            {
                // sliderのTrackをクリックしてThumbがクリック位置に移動した時Thumbがつままれた状態になるようにする
                mSliderPlayPosion.ApplyTemplate();
                (mSliderPlayPosion.Template.FindName("PART_Track", mSliderPlayPosion) as Track).Thumb.MouseEnter += new MouseEventHandler((sliderSender, se) => {
                    if (se.LeftButton == MouseButtonState.Pressed && se.MouseDevice.Captured == null) {
                        var args = new MouseButtonEventArgs(se.MouseDevice, se.Timestamp, MouseButton.Left);
                        args.RoutedEvent = MouseLeftButtonDownEvent;
                        (sliderSender as Thumb).RaiseEvent(args);
                    }
                });
            }

            mInitialized = true;
        }

        /// <summary>
        ///  プログラムを即時終了する。
        /// </summary>
        private void Exit() {
            Close();
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    mBwPlay.CancelAsync();
                    while (mBwPlay.IsBusy) {
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                                System.Windows.Threading.DispatcherPriority.Background,
                                new System.Threading.ThreadStart(delegate { }));
                        System.Threading.Thread.Sleep(100);
                    }

                    mBwLoad.CancelAsync();
                    while (mBwLoad.IsBusy) {
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                                System.Windows.Threading.DispatcherPriority.Background,
                                new System.Threading.ThreadStart(delegate { }));
                        System.Threading.Thread.Sleep(100);
                    }

                    /*
                    if (mBwLoad != null) {
                        mBwLoad.Dispose();
                        mBwLoad = null;
                    }
                    */

                    if (mPlayer != null) {
                        mPlayer.Dispose();
                        mPlayer = null;
                    }
                }

                disposedValue = true;
            }
        }

        void IDisposable.Dispose() {
            // Do not change this code.
            Dispose(true);
        }

        private void Window_Closed(object sender, EventArgs e) {
            Dispose(true);
        }

        #endregion

        private void UpdateDeviceList() {
            AddLog("UpdateDeviceList()\n");

            // 以前選択されていたデバイスのIdStr
            var lastSelectedIdStr = "";
            if (0 <= mListBoxPlaybackDevices.SelectedIndex) {
                lastSelectedIdStr = mPlayer.SpatialAudio.DevicePropertyList[
                    mListBoxPlaybackDevices.SelectedIndex].devIdStr;
            }

            mPlayer.SpatialAudio.UpdateDeviceList();
            mListBoxPlaybackDevices.Items.Clear();
            foreach (var item in mPlayer.SpatialAudio.DevicePropertyList) {
                mListBoxPlaybackDevices.Items.Add(string.Format("{0}", item.name));
                if (0 == item.devIdStr.CompareTo(lastSelectedIdStr)) {
                    // 以前選択されていたデバイスを選択状態にする。
                    mListBoxPlaybackDevices.SelectedIndex = mListBoxPlaybackDevices.Items.Count - 1;
                }
            }

            if (0 < mListBoxPlaybackDevices.Items.Count) {
                mButtonActivate.IsEnabled = true;
                mButtonDeactivate.IsEnabled = false;
            } else {
                mButtonActivate.IsEnabled = false;
                mButtonDeactivate.IsEnabled = false;
            }
        }

        #region File Read worker thread stuff
        class LoadParams {
            public string path;
        }

        class LoadResult {
            public int hr;
        }

        private void ReportProgress(int percent, string s) {
            if (MESSAGE_INTERVAL_MS < mSWProgressReport.ElapsedMilliseconds) {
                // OK
                mBwMsgSB.Append(s);
                mBwLoad.ReportProgress(percent, mBwMsgSB.ToString());
                mBwMsgSB.Clear();
                mSWProgressReport.Restart();
            } else {
                mBwMsgSB.Append(s);
            }
        }

        const int E_ABORT = -128;

        private void MBwLoad_DoWork(object sender, DoWorkEventArgs e) {
            mBwMsgSB.Clear();

            var param = e.Argument as LoadParams;
            var r = new LoadResult();
            e.Result = r;
            r.hr = 0;

            ReportProgress(10, string.Format("Reading {0}\n", param.path));

            int hr = mPlayer.ReadAudioFile(param.path);
            if (hr < 0) {
                r.hr = hr;
                return;
            }
            if (e.Cancel) {
                r.hr = E_ABORT;
                return;
            }

            ReportProgress(66, "  Resampling...\n");

            hr = mPlayer.Resample();
            if (hr < 0) {
                r.hr = hr;
                return;
            }
            if (e.Cancel) {
                r.hr = E_ABORT;
                return;
            }

            ReportProgress(90, "  Storing to native buffer...\n");
            hr = mPlayer.StoreSamplesToNativeBuffer();
            if (hr < 0) {
                r.hr = hr;
                return;
            }
        }
        private void MBwLoad_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            var param = e.UserState as string;
            AddLog(param);
            mProgressbar.Value = e.ProgressPercentage;
        }

        private void MBwLoad_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (e.Cancelled) {
                return;
            }

            var r = e.Result as LoadResult;

            if (0 < mPlayer.NumChannels && !mPlayer.IsChannelSupported(mPlayer.NumChannels)) {
                string msg = string.Format(
                    "Error: Audio File of {0}ch is currently not supported!",
                    mPlayer.NumChannels);
                MessageBox.Show(msg);
                AddLog(msg + "\n");
                r.hr = -1;
            }

            if (r.hr < 0) {
                string msg = string.Format(
                    "Error: Read file failed with error code {0:X8} : {1}",
                    r.hr, mTextBoxInputFileName.Text);
                MessageBox.Show(msg);
                AddLog(msg + "\n");

                mGroupBoxPlaybackDevice.IsEnabled = false;
            } else {
                // 成功。
                AddLog(string.Format("Read succeeded : {0}\n", mTextBoxInputFileName.Text));
                mLabelInputAudioFmt.Content = string.Format("File contains {0} ch PCM, channel to speaker map = {1}",
                    mPlayer.NumChannels, DwChannelMaskToStr(mPlayer.DwChannelMask));

                mGroupBoxPlaybackDevice.IsEnabled = true;
            }

            mProgressbar.Value = 0;
        }

        private string DwChannelMaskToStr(int dwChannelMask) {
            var sb = new StringBuilder();

            foreach (var item in WWSpatialAudioUser.DwChannelMaskToList(dwChannelMask)) {
                sb.AppendFormat("{0} ", WWSpatialAudioUser.DwChannelMaskShortStr(item));
            }

            return sb.ToString().TrimEnd(new char[] {' '});
        }

        #endregion

        private void ReadFile() {
            var param = new LoadParams();
            param.path = mTextBoxInputFileName.Text;

            mBwLoad.RunWorkerAsync(param);
        }

        private void FilenameTextBoxUpdated() {
            // 特にない。
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // Play time update

        #region Play time update 

        const int PLAY_TIME_INTERVAL_MS = 100;

        private void MBwPlay_DoWork(object sender, DoWorkEventArgs e) {
            while (!mBwPlay.CancellationPending) {
                System.Threading.Thread.Sleep(100);
                mBwPlay.ReportProgress(0);

                int hr = mPlayer.SpatialAudio.GetThreadErcd();
                if (hr < 0) {
                    break;
                }

                int trackNr = mPlayer.SpatialAudio.GetPlayingTrackNr(0);
                if (trackNr == (int)WWSpatialAudioUser.PlayingTrackEnum.None) {
                    break;
                }

            }

            if (mBwPlay.CancellationPending) {
                e.Cancel = true;
            }
        }

        private static string SecondsToMSString(int seconds) {
            int m = seconds / 60;
            int s = seconds - m * 60;
            return string.Format(CultureInfo.CurrentCulture, "{0:D2}:{1:D2}", m, s);
        }

        private void MBwPlay_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            if (mBwPlay.CancellationPending || mPlayer == null) {
                return;
            }

            var pp = mPlayer.SpatialAudio.GetPlayStatus(0);

            string sD = SecondsToMSString((int)(pp.TotalFrameNum / mPlayer.PlaySampleRate));
            string sP = SecondsToMSString((int)(pp.PosFrame / mPlayer.PlaySampleRate));
            string s = string.Format("{0} / {1}", sP, sD);

            if (0 == s.CompareTo(mLabelPlayingTime.Content.ToString())) {
                // 時間が変わっていないので描画更新しない。
                return;
            }

            UpdateSliderPosition(pp);

            mLabelPlayingTime.Content = s;
        }

        private void MBwPlay_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (e.Cancelled) {
                return;
            }

            mLabelPlayingTime.Content = "--:-- / --:--";

            int hr = mPlayer.SpatialAudio.GetThreadErcd();
            if (hr < 0) {  
                MessageBox.Show(string.Format("Unrecoverable error: Playback thread encountered error {0:X8} !\nProgram will exit.", hr));
                Exit();
                return;
            }

            // 全ての再生データ出力完了。
            Stop();
        }

        #endregion

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // Event handling

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e) {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Audio files(*wav;*.flac)|*.wav;*.flac";
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            mTextBoxInputFileName.Text = dlg.FileName;
            FilenameTextBoxUpdated();
            ReadFile();
        }

        private void ButtonPlay_Click(object sender, RoutedEventArgs e) {
            int hr = mPlayer.Start();
            if (hr < 0) {
                string msg = string.Format("Error: ISpatialAudioObjectRenderStream::Start failed {0:X8}\n", hr);
                MessageBox.Show(msg);
                AddLog(msg);
                return;
            }

            AddLog(string.Format("Start Success {0:X8}\n", hr));

            mGroupBoxInputAudioFile.IsEnabled = false;
            mButtonUpdatePlaybackDeviceList.IsEnabled = false;
            mButtonDeactivate.IsEnabled = false;
            mButtonPlay.IsEnabled = false;
            mButtonStop.IsEnabled = true;
            mBwPlay.RunWorkerAsync();
        }

        private void Stop() {
            int hr = mPlayer.Stop();
            if (hr < 0) {
                string msg = string.Format("Error: ISpatialAudioObjectRenderStream::Stop failed {0:X8}\n", hr);
                MessageBox.Show(msg);
                AddLog(msg);
                return;
            }

            AddLog(string.Format("Stop Success {0:X8}\n", hr));

            mGroupBoxInputAudioFile.IsEnabled = true;
            mButtonUpdatePlaybackDeviceList.IsEnabled = false;
            mButtonDeactivate.IsEnabled = true;
            mButtonPlay.IsEnabled = true;
            mButtonStop.IsEnabled = false;
            mBwPlay.CancelAsync();
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e) {
            mBwPlay.CancelAsync();
        }

        private void ButtonUpdatePlaybackDeviceList_Click(object sender, RoutedEventArgs e) {
            UpdateDeviceList();
        }

        private void ButtonRead_Click(object sender, RoutedEventArgs e) {
            ReadFile();
        }

        const uint E_UNSUPPORTED_TYPE = 0x8007065e;

        private void ButtonActivateDevice_Click(object sender, RoutedEventArgs e) {
            int devIdx = mListBoxPlaybackDevices.SelectedIndex;

            AddLog(string.Format("Activating device #{0} ...\n", devIdx));

            int maxDynObjCount = 0;
            int staticObjMask = WWSpatialAudioUser.DwChannelMaskToAudioObjectTypeMask(mPlayer.DwChannelMask);

            mGroupBoxDeviceList.IsEnabled = false;
            mButtonActivate.IsEnabled = false;
            int hr = mPlayer.SpatialAudio.ChooseDevice(devIdx, maxDynObjCount, staticObjMask);
            if (0 <= hr) {
                // 成功。
                AddLog(string.Format("SpatialAudio.ChooseDevice({0}) success.\n", devIdx));
                mGroupBoxDeviceList.IsEnabled = false;
                mButtonUpdatePlaybackDeviceList.IsEnabled = false;
                mButtonActivate.IsEnabled = false;
                mButtonDeactivate.IsEnabled = true;
                mButtonPlay.IsEnabled = true;
                mButtonStop.IsEnabled = false;
            } else {
                // 失敗。
                if (E_UNSUPPORTED_TYPE == (uint)hr) {
                    var s = string.Format("Error: Spatial Audio of the specified device is not enabled! Please enable Spatial Audio of the device.\n", devIdx);
                    AddLog(s);
                    MessageBox.Show(s);
                } else {
                    var s = string.Format("SpatialAudio.ChooseDevice({0}) failed with error {1:X8}.\n", devIdx, hr);
                    AddLog(s);
                    MessageBox.Show(s);
                }
                mGroupBoxDeviceList.IsEnabled = true;
                mButtonUpdatePlaybackDeviceList.IsEnabled = true;
                mButtonActivate.IsEnabled = true;
                mButtonDeactivate.IsEnabled = false;
                mButtonPlay.IsEnabled = false;
                mButtonStop.IsEnabled = false;
            }
        }

        private void ButtonDeactivateDevice_Click(object sender, RoutedEventArgs e) {
            int hr = mPlayer.SpatialAudio.ChooseDevice(-1, 0, 0);
            AddLog(string.Format("SpatialAudio.ChooseDevice(-1) hr={0:X8}\n", hr));

            mGroupBoxDeviceList.IsEnabled = true;
            mButtonUpdatePlaybackDeviceList.IsEnabled = true;
            mButtonActivate.IsEnabled = true;
            mButtonDeactivate.IsEnabled = false;
            mButtonPlay.IsEnabled = false;
            mButtonStop.IsEnabled = false;

            UpdateDeviceList();
        }

        private void ListBoxSpeakerConfig_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!mInitialized) {
                return;
            }
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // Slider event

        private long mLastSliderValue = 0;
        private bool mSliderSliding = false;
        private long mLastSliderPositionUpdateTime = 0;
        
        /// <summary>
        /// スライダー位置の更新頻度 (500ミリ秒)
        /// </summary>
        private const long SLIDER_UPDATE_TICKS = 500 * 10000;

        private void UpdateSliderPosition(WWSpatialAudioUser.PlayStatus playPos) {
            long now = DateTime.Now.Ticks;
            if (now - mLastSliderPositionUpdateTime > SLIDER_UPDATE_TICKS) {
                // スライダー位置の更新。0.5秒に1回

                //Console.WriteLine("SliderPos={0} / {1}", playPos.PosFrame, playPos.TotalFrameNum);

                mSliderPlayPosion.Maximum = playPos.TotalFrameNum;
                if (!mSliderSliding || playPos.TotalFrameNum <= mSliderPlayPosion.Value) {
                    mSliderPlayPosion.Value = playPos.PosFrame;
                }

                mLastSliderPositionUpdateTime = now;
            }
        }

        private void MSliderPlayPosion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.Source != mSliderPlayPosion) {
                return;
            }

            mLastSliderValue = (long)mSliderPlayPosion.Value;
            mSliderSliding = true;
        }

        private void MSliderPlayPosion_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
            if (e.Source != mSliderPlayPosion) {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed) {
                if (!mButtonPlay.IsEnabled &&
                        mLastSliderValue != (long)mSliderPlayPosion.Value) {
                    // 再生中。再生位置を変更する。
                    mPlayer.SpatialAudio.SetPlayPos((long)mSliderPlayPosion.Value);
                    mLastSliderValue = (long)mSliderPlayPosion.Value;
                }
            }
        }

        private void MSliderPlayPosion_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.Source != mSliderPlayPosion) {
                return;
            }

            if (!mButtonPlay.IsEnabled &&
                    mLastSliderValue != (long)mSliderPlayPosion.Value) {
                // 再生中。再生位置を変更する。
                mPlayer.SpatialAudio.SetPlayPos((long)mSliderPlayPosion.Value);
            }

            mLastSliderValue = 0;
            mSliderSliding = false;
        }
    }
}

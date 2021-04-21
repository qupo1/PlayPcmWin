using System.Windows;
using Wasapi;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Windows.Threading;
using System;
using System.Threading;
using System.ComponentModel;

namespace WWSoundRouter {

    public partial class MainWindow : Window {
        private bool mInit = false;
        private WasapiCS mRec = new WasapiCS();
        private WasapiCS mPlay = new WasapiCS();

        private List<string> mRecDeviceIdStrList  = new List<string>();
        private List<string> mPlayDeviceIdStrList = new List<string>();

        private WasapiCS.MixFormat mRecMixFmt;
        private WasapiCS.MixFormat mPlayMixFmt;

        private StringBuilder mSBLog = new StringBuilder();

        private object mLogLock = new object();

        private int mBufferUnderrunCount = 0;

        private BackgroundWorker mBW;


        private List<string> mLogList = new List<string>();

        private void AppendLogToSB(string s) {
            lock (mLogLock) {
                mLogList.Add(s);
                while (10 < mLogList.Count) {
                    mLogList.RemoveAt(0);
                }
                mSBLog.Clear();
                foreach (var l in mLogList) {
                    mSBLog.Append(l);
                }
            }
        }

        /// <summary>
        /// UIスレッドから呼ぶ必要あり。
        /// </summary>
        private void AddLog(string s, bool bForceUpdate = false) {
            AppendLogToSB(s);

            lock (mLogLock) {
                mTextBoxMsg.Text = mSBLog.ToString();
            }
                
            mTextBoxMsg.ScrollToEnd();
        }

        private void ClearLog() {
            lock (mLogLock) {
                mSBLog.Clear();
            }

            mTextBoxMsg.Text = "";
        }

        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mRec.Init();
            mPlay.Init();

            mRec.EnumerateDevices(WasapiCS.DeviceType.Rec);
            for (int i = 0; i < mRec.GetDeviceCount(); ++i) {
                var attr = mRec.GetDeviceAttributes(i);
                mRecDeviceIdStrList.Add(attr.DeviceIdString);

                mComboBoxRecDevices.Items.Add(attr.Name);
                if (0 == Properties.Settings.Default.RecDeviceIdStr.CompareTo(attr.DeviceIdString)) {
                    mComboBoxRecDevices.SelectedIndex = i;
                }
            }
            if (mRec.GetDeviceCount() == 0) {
                MessageBox.Show("No Recording device available!");
                Close();
            }
            if (mComboBoxRecDevices.SelectedIndex < 0) {
                mComboBoxRecDevices.SelectedIndex = 0;
            }

            mPlay.EnumerateDevices(WasapiCS.DeviceType.Play);
            for (int i = 0; i < mPlay.GetDeviceCount(); ++i) {
                var attr = mPlay.GetDeviceAttributes(i);
                mPlayDeviceIdStrList.Add(attr.DeviceIdString);
                mComboBoxPlayDevices.Items.Add(attr.Name);

                if (0 == Properties.Settings.Default.PlayDeviceIdStr.CompareTo(attr.DeviceIdString)) {
                    mComboBoxPlayDevices.SelectedIndex = i;
                }
            }
            if (mPlay.GetDeviceCount() == 0) {
                MessageBox.Show("No Playback device available!");
                Close();
            }
            if (mComboBoxPlayDevices.SelectedIndex < 0) {
                mComboBoxPlayDevices.SelectedIndex = 0;
            }

            mInit = true;
        }

        private Stopwatch mSW = new Stopwatch();

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            int hr = 0;

            int buffMilliSec = 100;

            mBufferUnderrunCount = 0;

            ClearLog();

            int recIdx  = mComboBoxRecDevices.SelectedIndex;
            int playIdx = mComboBoxPlayDevices.SelectedIndex;

            mRecMixFmt  = mRec.GetMixFormat( recIdx);
            mPlayMixFmt = mPlay.GetMixFormat(playIdx);
            var recDevName  = (string)mComboBoxRecDevices.Items[ recIdx];
            var playDevName = (string)mComboBoxPlayDevices.Items[playIdx];

            AddLog(string.Format("Rec:  {0}Hz {1} {2}ch. {3}\n", mRecMixFmt.sampleRate,  mRecMixFmt.sampleFormat,  mRecMixFmt.numChannels,  recDevName));
            AddLog(string.Format("Play: {0}Hz {1} {2}ch. {3}\n", mPlayMixFmt.sampleRate, mPlayMixFmt.sampleFormat, mPlayMixFmt.numChannels, playDevName));
            AddLog("", true);

            if (!mRecMixFmt.IsTheSameTo(mPlayMixFmt)) {
                MessageBox.Show("Error: Rec and Play sample format does not match!");
                return;
            }

            mSW.Start();

            mRec.RegisterCaptureCallback(CaptureCallback);
            mRec.RegisterStateChangedCallback(RecStateChangedCallback);

            hr = mRec.Setup(recIdx, WasapiCS.DeviceType.Rec, WasapiCS.StreamType.PCM, mRecMixFmt.sampleRate,
                    mRecMixFmt.sampleFormat, mRecMixFmt.numChannels, mRecMixFmt.dwChannelMask,
                    WasapiCS.MMCSSCallType.Enable, WasapiCS.MMThreadPriorityType.High,
                    WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Shared,
                    WasapiCS.DataFeedMode.EventDriven, buffMilliSec, 0, 10000, true);
            if (hr < 0) {
                MessageBox.Show(string.Format("Error: Rec setup failed {0:X8}!", hr));
                mRec.Unsetup();
                return;
            }

            mPlay.RegisterRenderCallback(RenderCallback);
            mPlay.RegisterStateChangedCallback(PlayStateChangedCallback);

            hr = mPlay.Setup(playIdx, WasapiCS.DeviceType.Play, WasapiCS.StreamType.PCM, mPlayMixFmt.sampleRate,
                    mPlayMixFmt.sampleFormat, mPlayMixFmt.numChannels, mPlayMixFmt.dwChannelMask,
                    WasapiCS.MMCSSCallType.Enable, WasapiCS.MMThreadPriorityType.High,
                    WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Shared,
                    WasapiCS.DataFeedMode.EventDriven, buffMilliSec, 0, 10000, true);
            if (hr < 0) {
                MessageBox.Show(string.Format("Error: Play setup failed {0:X8}!", hr));
                mPlay.Unsetup();
                return;
            }

#if false
            // FIFOバッファに無音を1秒入れておく。
            int bytesPerFrame = WasapiCS.SampleFormatTypeToUseBitsPerSample(mRecMixFmt.sampleFormat) * mRecMixFmt.numChannels;
            lock (mPcmBufLock) {
                mPcmBuf.Clear();
                // 10分の1秒を10個入れる。
                for (int i = 0; i < 10; ++i) {
                    mPcmBuf.Add(new byte[mRecMixFmt.sampleRate * bytesPerFrame / 10]);
                }
            }
#endif

            mRec.StartRecording();
            mPlay.StartPlayback(-1);

            mButtonStart.IsEnabled = false;
            mButtonStop.IsEnabled = true;

            mBW = new BackgroundWorker();
            mBW.WorkerReportsProgress = true;
            mBW.DoWork += new DoWorkEventHandler(mBW_DoWork);
            mBW.ProgressChanged += new ProgressChangedEventHandler(mBW_ProgressChanged);
            mBW.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mBW_RunWorkerCompleted);
            mBW.WorkerSupportsCancellation = true;
            mBW.RunWorkerAsync();
        }

        void mBW_DoWork(object sender, DoWorkEventArgs e) {
            Console.WriteLine("mBW_DoWork started.");

            while (!mBW.CancellationPending) {
                mBW.ReportProgress(1);
                Thread.Sleep(500);
            }

            Console.WriteLine("mBW_DoWork cancelled.");
        }

        void mBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            Console.WriteLine("mBW completed.");
        }

        void mBW_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            mStatusBarItem.Content = string.Format("{0:0.000} sec: BufferUnderrunCount={1}. Buffered {2} frames. \n",
                mSW.ElapsedMilliseconds * 0.001, mBufferUnderrunCount, AvailablePcmFrames());
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            mRec.Stop();
            mRec.Unsetup();

            mPlay.Stop();
            mPlay.Unsetup();

            mBW.CancelAsync();

            mButtonStart.IsEnabled = true;
            mButtonStop.IsEnabled = false;

            mStatusBarItem.Content = "Ready.";
        }

        List<byte[]> mPcmBuf = new List<byte[]>();
        private object mPcmBufLock = new object();

        private int AvailablePcmFrames() {
            int bytes = 0;

            lock (mPcmBufLock) {
                foreach (var b in mPcmBuf) {
                    bytes += b.Length;
                }
            }

            return bytes / mRecMixFmt.UseBytesPerFrame;
        }
        
        private float mX = 0;

        private byte[] GetPlayPcm(int wantBytes) {
            var r = new byte[wantBytes];
#if false
            // テスト用。float型のPCMデータを作る。
            for (int i=0; i<wantBytes/4; ++i) {
                mX += 3.14159265f / 100.0f;
                while (3.14159265f < mX) {
                    mX -= 2.0f * 3.14159265f;
                }

                float f = 0.5f * (float)Math.Sin(mX);
                var fb = BitConverter.GetBytes(f);
                Array.Copy(fb, 0, r, i * 4, 4);
            }
#else
            int remainBytes = wantBytes;
            int pos = 0;

            lock (mPcmBufLock) {

                while (0 < remainBytes) {
                    if (mPcmBuf.Count == 0) {
                        // PCMデータが取り出せません！バッファーアンダーラン。
                        ++mBufferUnderrunCount;
                        break;
                    }
                    // PCMを取り出します。
                    byte[] buf = null;
                    lock (mPcmBufLock) {
                        buf = mPcmBuf[0];
                        mPcmBuf.RemoveAt(0);
                    }

                    if (remainBytes < buf.Length) {
                        // バッファのサイズが残り必要バイト数よりも多い。
                        // バッファの一部をrにコピーし、残りをmPcmBufに戻す。
                        // 必要データが全て揃った。
                        Array.Copy(buf, 0, r, pos, remainBytes);

                        var remainBuf = new byte[buf.Length - remainBytes];
                        Array.Copy(buf, remainBytes, remainBuf, 0, remainBuf.Length);

                        mPcmBuf.Insert(0, remainBuf);
                        break;
                    } else {
                        // bufの内容がrに全て収容できる。
                        Array.Copy(buf, 0, r, pos, buf.Length);
                        remainBytes -= buf.Length;
                        pos += buf.Length;
                    }
                }
            }
#endif
            return r;
        }

        private void CaptureCallback(byte[] data) {
            lock (mPcmBufLock) {
                mPcmBuf.Add(data);
            }
#if false
            AppendLogToSB(string.Format("{0:0.000}: {1} bytes, buffered {2} frames. BufferUnderrunCount={3}\n",
                mSW.ElapsedMilliseconds * 0.001, data.Length, AvailablePcmBytes(), mBufferUnderrunCount));

            Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background, new Action(() => {
                // ここはMainWindowのUIスレッド。
                // ログを描画する。
                AddLog("");
            }));
#endif
        }

        private byte[] RenderCallback(int wantBytes) {
            return GetPlayPcm(wantBytes);
        }

        private void RecStateChangedCallback(StringBuilder idStr, int dwNewState) {
            StopTermAll();
            Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background, new Action(() => {
                mButtonStart.IsEnabled = true;
                mButtonStop.IsEnabled = false;
            }));
        }

        private void PlayStateChangedCallback(StringBuilder idStr, int dwNewState) {
            StopTermAll();
            Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background, new Action(() => {
                mButtonStart.IsEnabled = true;
                mButtonStop.IsEnabled = false;
            }));
        }

        private void StopTermAll() {
            mRec.Stop();
            mRec.Unsetup();
            mRec.Term();

            mPlay.Stop();
            mPlay.Unsetup();
            mPlay.Term();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            // 設定を保存。
            Properties.Settings.Default.Save();

            StopTermAll();
        }

        private void mComboBoxSourceDevices_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (!mInit) {
                return;
            }

            Properties.Settings.Default.RecDeviceIdStr = mRecDeviceIdStrList[mComboBoxRecDevices.SelectedIndex];
        }

        private void mComboBoxSinkDevices_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (!mInit) {
                return;
            }

            Properties.Settings.Default.PlayDeviceIdStr = mPlayDeviceIdStrList[mComboBoxPlayDevices.SelectedIndex];
        }

    }
}

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using PcmDataLib;

namespace PlayPcmWin {
    public sealed partial class MainWindow : Window {

        // PPWServer ■■■■■■■■■■■■■■■■■■■■■■■■■

        private const int PPWSERVER_LISTEN_PORT = 2002;
        private BackgroundWorker mBWPPWServer = new BackgroundWorker();
        private PPWServer mPPWServer = null;

        private void PPWServerWorker_ShowSettingsWindow() {
            var sw = new PPWServerSettingsWindow();

            if (mPPWServer != null) {
                sw.SetServerState(PPWServerSettingsWindow.ServerState.Started,
                    mPPWServer.ListenIPAddress, mPPWServer.ListenPort);
            } else {
                sw.SetServerState(PPWServerSettingsWindow.ServerState.Stopped, "", -1);
            }

            var r = sw.ShowDialog();
            if (r != true) {
                return;
            }

            menuPPWServer.IsEnabled = false;

            if (mPPWServer == null) {
                // サーバー起動。
                mBWPPWServer = new BackgroundWorker();
                mBWPPWServer.DoWork += new DoWorkEventHandler(mBWPPWServer_DoWork);
                mBWPPWServer.WorkerSupportsCancellation = true;
                mBWPPWServer.WorkerReportsProgress = true;
                mBWPPWServer.ProgressChanged += new ProgressChangedEventHandler(mBWPPWServer_ProgressChanged);
                mBWPPWServer.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mBWPPWServer_RunWorkerCompleted);
                mBWPPWServer.RunWorkerAsync();
            } else {
                // サーバー終了。
                mBWPPWServer.CancelAsync();
                while (mBWPPWServer.IsBusy) {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new System.Threading.ThreadStart(delegate { }));
                    System.Threading.Thread.Sleep(100);
                }
                mBWPPWServer = null;

                menuPPWServer.IsEnabled = true;
            }
        }

        void mBWPPWServer_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            menuPPWServer.IsEnabled = true;
        }

        // mPPWServer.Runの中から呼び出される。
        // ReportProgress(0, ...)で、サーバー開始完了
        // ReportProgress(10, ...)は、メッセージの表示。
        void mBWPPWServer_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            if (e.ProgressPercentage == PPWServer.PROGRESS_STARTED) {
                menuPPWServer.IsEnabled = true;
            }

            string s = e.UserState as string;
            AddLogText(s);
        }

        void mBWPPWServer_DoWork(object sender, DoWorkEventArgs e) {
            mPPWServer = new PPWServer();
            mPPWServer.Run(new PPWServer.RemoteCmdRecvDelegate(PPWServerRemoteCmdRecv), mBWPPWServer, PPWSERVER_LISTEN_PORT);
            mPPWServer = null;
        }

        private void PPWServerRemoteCmdRecv(RemoteCommand cmd) {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(() => {
                    // ここはMainWindowのUIスレッド。
                    //Console.WriteLine("PPWServerRemoteCmdRecv {0} trackIdx={1}", cmd.cmd, cmd.trackIdx);

                    switch (cmd.cmd) {
                    case RemoteCommandType.PlaylistWant:
                        // 再生リストを送る。
                        PPWServerSendPlaylist();
                        break;
                    case RemoteCommandType.Play:
                        // 再生曲切り替え。
                        if (buttonPlay.IsEnabled && !buttonStop.IsEnabled) {
                            ButtonPlayClicked();
                        } else if (mState == State.再生一時停止中) {
                            // 一時停止中は一時停止ボタンを押すと再生再開する。
                            ButtonPauseClicked();
                        }

                        if (0 <= cmd.trackIdx && cmd.trackIdx < dataGridPlayList.Items.Count) {
                            mPlayListMouseDown = true;
                            dataGridPlayList.SelectedIndex = cmd.trackIdx;
                        }
                        break;
                    case RemoteCommandType.SelectTrack:
                        // 再生曲切り替え。
                        if (0 <= cmd.trackIdx && cmd.trackIdx < dataGridPlayList.Items.Count) {
                            mPlayListMouseDown = true;
                            dataGridPlayList.SelectedIndex = cmd.trackIdx;
                        }
                        break;
                    case RemoteCommandType.Pause:
                        if (buttonPause.IsEnabled) {
                            ButtonPauseClicked();
                        }
                        break;
                    case RemoteCommandType.Seek:
                        if (!buttonPlay.IsEnabled) {
                            double ratio = (double)cmd.positionMillisec / cmd.trackMillisec;
                            long pos = (long)(ratio * slider1.Maximum);
                            //Console.WriteLine("SEEK {0} {1} {2}", ratio, slider1.Maximum, pos);
                            mAp.wasapi.SetPosFrame(pos);
                        }
                        // TODO
                        break;
                    }
                }));
        }

        // MainWindowのUIスレッドから呼び出して下さい。
        private void PPWServerSendPlaylist() {
            //Console.WriteLine("PPWServerSendPlaylist() start {0}", DateTime.Now.Second);

            if (mPPWServer == null) {
                return;
            }

            var cmd = new RemoteCommand(RemoteCommandType.PlaylistData);
            cmd.trackIdx = dataGridPlayList.SelectedIndex;

            // 大体の再生状態。
            switch (mState) {
            case State.再生中:
                cmd.state = RemoteCommand.PlaybackState.Playing; break;
            case State.再生一時停止中:
                cmd.state = RemoteCommand.PlaybackState.Paused; break;
            default:
                cmd.state = RemoteCommand.PlaybackState.Stopped; break;
            }

            foreach (var a in mPlayListItems) {
                int sampleRate = a.PcmData().SampleRate;
                int bitDepth = a.PcmData().ValidBitsPerSample;
                if (a.PcmData().SampleDataType == PcmData.DataType.DoP) {
                    sampleRate *= 16;
                    bitDepth = 1;
                }
                var p = new RemoteCommandPlayListItem(
                    a.PcmData().DurationMilliSec, sampleRate, bitDepth,
                    a.AlbumTitle, a.ArtistName, a.Title, a.PcmData().PictureData);
                cmd.playlist.Add(p);
            }
            //Console.WriteLine("PPWServerSendPlaylist() SendAsync start {0}", DateTime.Now.Second);
            mPPWServer.SendAsync(cmd);
            //Console.WriteLine("PPWServerSendPlaylist() SendAsync end {0}", DateTime.Now.Second);
        }

        // あらゆるスレッドから呼び出し可。
        private void PPWServerSendCommand(RemoteCommand cmd) {
            if (mPPWServer == null) {
                return;
            }

            //Console.WriteLine("PPWServerSendCommand() SendAsync start {0}", DateTime.Now.Second);
            mPPWServer.SendAsync(cmd);
        }

    }
}

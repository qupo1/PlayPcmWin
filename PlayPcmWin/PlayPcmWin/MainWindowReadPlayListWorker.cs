using System.ComponentModel;
using System.Windows;

namespace PlayPcmWin {
    public sealed partial class MainWindow : Window {
        private BackgroundWorker mPlaylistReadWorker;

        private void ReadPlayListWorkerSetup() {
            mPlaylistReadWorker = new BackgroundWorker();
            mPlaylistReadWorker.WorkerReportsProgress = true;
            mPlaylistReadWorker.DoWork += new DoWorkEventHandler(PlaylistReadWorker_DoWork);
            mPlaylistReadWorker.ProgressChanged += new ProgressChangedEventHandler(PlaylistReadWorker_ProgressChanged);
            mPlaylistReadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlaylistReadWorker_RunWorkerCompleted);
            mPlaylistReadWorker.WorkerSupportsCancellation = false;
        }

        enum ReadPpwPlaylistMode {
            RestorePlaylistOnProgramStart,
            AppendLoad,
        };

        private class PlaylistReadWorkerArg {
            public PlaylistSave3 pl;
            public ReadPpwPlaylistMode mode;
            public PlaylistReadWorkerArg(PlaylistSave3 pl, ReadPpwPlaylistMode mode) {
                this.pl = pl;
                this.mode = mode;
            }
        };

        private void ReadPlayListRunAsync(PlaylistSave3 pl, ReadPpwPlaylistMode mode) {
            mPlaylistReadWorker.RunWorkerAsync(new PlaylistReadWorkerArg(pl, mode));
        }

        /// <summary>
        /// Playlist read worker thread
        /// </summary>
        /// <param name="sender">not used</param>
        /// <param name="e">PlaylistSave instance</param>
        void PlaylistReadWorker_DoWork(object sender, DoWorkEventArgs e) {
            var arg = e.Argument as PlaylistReadWorkerArg;
            e.Result = arg;

            if (null == arg.pl) {
                return;
            }

            int readAttemptCount = 0;
            int readSuccessCount = 0;
            foreach (var p in arg.pl.Items) {
                int errCount = ReadFileHeader(p.PathName, WWSoundFileRW.WWPcmHeaderReader.ReadHeaderMode.OnlyConcreteFile, null);
                if (0 == errCount && 0 < mAp.PcmDataListForDisp.Count()) {
                    // 読み込み成功。読み込んだPcmDataの曲名、アーティスト名、アルバム名、startTick等を上書きする。

                    // pcmDataのメンバ。
                    var pcmData = mAp.PcmDataListForDisp.Last();
                    pcmData.DisplayName = p.Title;
                    pcmData.AlbumTitle = p.AlbumName;
                    pcmData.ArtistName = p.ArtistName;
                    pcmData.ComposerName = p.ComposerName;
                    pcmData.StartTick = p.StartTick;
                    pcmData.EndTick = p.EndTick;
                    pcmData.CueSheetIndex = p.CueSheetIndex;
                    pcmData.TrackId = p.TrackId;

                    // playList表のメンバ。
                    var playListItem = mPlayListItems[readSuccessCount];
                    playListItem.ReadSeparaterAfter = p.ReadSeparaterAfter;
                    ++readSuccessCount;
                }

                ++readAttemptCount;
                mPlaylistReadWorker.ReportProgress(100 * readAttemptCount / arg.pl.Items.Count);
            }
        }

        void PlaylistReadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBar1.Value = e.ProgressPercentage;
        }

        void PlaylistReadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var arg = e.Result as PlaylistReadWorkerArg;

            // Showing error MessageBox must be delayed until Window Loaded state because SplashScreen closes all MessageBoxes whose owner is DesktopWindow
            if (0 < mLoadErrMsg.Length) {
                AddLogText(mLoadErrMsg.ToString());
                MessageBox.Show(mLoadErrMsg.ToString(), Properties.Resources.RestoreFailedFiles, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            mLoadErrMsg = null;
            progressBar1.Visibility = System.Windows.Visibility.Collapsed;

            switch (arg.mode) {
            case ReadPpwPlaylistMode.RestorePlaylistOnProgramStart:
                EnableDataGridPlaylist();
                break;
            default:
                break;
            }

            if (0 < mPlayListItems.Count) {
                ChangeState(State.再生リストあり);
            } else {
                ChangeState(State.再生リストなし);
            }
            UpdateUIStatus();
        }
    }
}

﻿using System.ComponentModel;
using System.Windows;

namespace PlayPcmWin {
    public sealed partial class MainWindow : Window {
        private BackgroundWorker m_playlistReadWorker;

        private void ReadPlayListWorkerSetup() {
            m_playlistReadWorker = new BackgroundWorker();
            m_playlistReadWorker.WorkerReportsProgress = true;
            m_playlistReadWorker.DoWork += new DoWorkEventHandler(PlaylistReadWorker_DoWork);
            m_playlistReadWorker.ProgressChanged += new ProgressChangedEventHandler(PlaylistReadWorker_ProgressChanged);
            m_playlistReadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlaylistReadWorker_RunWorkerCompleted);
            m_playlistReadWorker.WorkerSupportsCancellation = false;
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
            m_playlistReadWorker.RunWorkerAsync(new PlaylistReadWorkerArg(pl, mode));
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
                if (0 == errCount && 0 < ap.PcmDataListForDisp.Count()) {
                    // 読み込み成功。読み込んだPcmDataの曲名、アーティスト名、アルバム名、startTick等を上書きする。

                    // pcmDataのメンバ。
                    var pcmData = ap.PcmDataListForDisp.Last();
                    pcmData.DisplayName = p.Title;
                    pcmData.AlbumTitle = p.AlbumName;
                    pcmData.ArtistName = p.ArtistName;
                    pcmData.ComposerName = p.ComposerName;
                    pcmData.StartTick = p.StartTick;
                    pcmData.EndTick = p.EndTick;
                    pcmData.CueSheetIndex = p.CueSheetIndex;
                    pcmData.TrackId = p.TrackId;

                    // playList表のメンバ。
                    var playListItem = m_playListItems[readSuccessCount];
                    playListItem.ReadSeparaterAfter = p.ReadSeparaterAfter;
                    ++readSuccessCount;
                }

                ++readAttemptCount;
                m_playlistReadWorker.ReportProgress(100 * readAttemptCount / arg.pl.Items.Count);
            }
        }

        void PlaylistReadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBar1.Value = e.ProgressPercentage;
        }

        void PlaylistReadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            var arg = e.Result as PlaylistReadWorkerArg;

            // Showing error MessageBox must be delayed until Window Loaded state because SplashScreen closes all MessageBoxes whose owner is DesktopWindow
            if (0 < m_loadErrorMessages.Length) {
                AddLogText(m_loadErrorMessages.ToString());
                MessageBox.Show(m_loadErrorMessages.ToString(), Properties.Resources.RestoreFailedFiles, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            m_loadErrorMessages = null;
            progressBar1.Visibility = System.Windows.Visibility.Collapsed;

            switch (arg.mode) {
            case ReadPpwPlaylistMode.RestorePlaylistOnProgramStart:
                EnableDataGridPlaylist();
                break;
            default:
                break;
            }

            if (0 < m_playListItems.Count) {
                ChangeState(State.再生リストあり);
            } else {
                ChangeState(State.再生リストなし);
            }
            UpdateUIStatus();
        }
    }
}

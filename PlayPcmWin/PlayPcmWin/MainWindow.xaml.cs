// 日本語UTF-8

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PcmDataLib;
using Wasapi;
using WasapiPcmUtil;
using WWUtil;
using MultipleAppInstanceComm;
using WWSoundFileRW;

namespace PlayPcmWin
{
    public sealed partial class MainWindow : Window
    {
        private const string PLAYING_TIME_UNKNOWN = "--:-- / --:--";
        private const string PLAYING_TIME_ALLZERO = "00:00 / 00:00";

        /// <summary>
        /// ログの表示行数。
        /// </summary>
        private const int LOG_LINE_NUM = 100;

        AudioPlayer mAp = new AudioPlayer();

        /// <summary>
        /// スライダー位置の更新頻度 (500ミリ秒)
        /// </summary>
        private const long SLIDER_UPDATE_TICKS = 500 * 10000;

        /// <summary>
        /// 共有モードの音量制限。
        /// </summary>
        const double SHARED_MAX_AMPLITUDE = 0.98;

        private Wasapi.WasapiCS.StateChangedCallback mWasapiStateChangedDelegate;

        private Preference mPreference = new Preference();

        class PlayListColumnInfo {
            public string Name { get; set; }
            public DataGridLength Width { get; set; }
            public PlayListColumnInfo(string name, DataGridLength width) {
                Name = name;
                Width = width;
            }
        };
        private PlayListColumnInfo[] mPlaylistColumnDefaults = {
            new PlayListColumnInfo("Title", DataGridLength.Auto),
            new PlayListColumnInfo("Duration", DataGridLength.Auto),
            new PlayListColumnInfo("Artist", DataGridLength.Auto),
            new PlayListColumnInfo("AlbumTitle", DataGridLength.Auto),
            new PlayListColumnInfo("ComposerName", DataGridLength.Auto),

            new PlayListColumnInfo("SampleRate", DataGridLength.Auto),
            new PlayListColumnInfo("QuantizationBitRate", DataGridLength.Auto),
            new PlayListColumnInfo("NumChannels", DataGridLength.SizeToCells),
            new PlayListColumnInfo("BitRate", DataGridLength.Auto),
            new PlayListColumnInfo("TrackNr", DataGridLength.SizeToCells),

            new PlayListColumnInfo("IndexNr", DataGridLength.SizeToCells),
            new PlayListColumnInfo("FileExtension", DataGridLength.Auto),
            new PlayListColumnInfo("ReadSeparaterAfter", DataGridLength.SizeToCells)
        };

        /// <summary>
        /// 再生リスト項目情報。
        /// </summary>
        private ObservableCollection<PlayListItemInfo> mPlayListItems = new ObservableCollection<PlayListItemInfo>();

        private System.Diagnostics.Stopwatch mSw = new System.Diagnostics.Stopwatch();
        private bool mPlayListMouseDown = false;

        /// <summary>
        /// 次にプレイリストにAddしたファイルに振られるGroupId。
        /// </summary>
        private int mGroupIdNextAdd = 0;

        /// <summary>
        /// メモリ上に読み込まれているGroupId。
        /// </summary>
        private int mLoadedGroupId = -1;
        
        /// <summary>
        /// PCMデータ読み込み中グループIDまたは読み込み完了したグループID
        /// </summary>
        private int mLoadingGroupId = -1;

        /// <summary>
        /// デバイスSetup情報。サンプリングレート、量子化ビット数…。
        /// </summary>
        DeviceSetupParams mDeviceSetupParams = new DeviceSetupParams();

        DeviceAttributes mUseDevice;
        bool mDeviceListUpdatePending;

        NextTask mTaskAfterStop = new NextTask();

        /// <summary>
        /// true: slider is dragging
        /// </summary>
        private bool mSliderSliding = false;

        List<PreferenceAudioFilter> mPreferenceAudioFilterList = new List<PreferenceAudioFilter>();

        MultipleAppInstanceMgr mMultiAppInstMgr = new MultipleAppInstanceMgr("PlayPcmWin");
        List<string> mDelayedAddFiles = new List<string>();

        enum State {
            未初期化,
            再生リスト読み込み中,
            再生リストなし,
            再生リストあり,

            // これ以降の状態にいる場合、再生リストに新しいファイルを追加できない。
            デバイスSetup完了,
            ファイル読み込み完了,
            再生中,
            再生一時停止中,
            再生停止開始,
            再生グループ読み込み中,
        }

        /// <summary>
        /// UIの状態。
        /// </summary>
        private State mState = State.未初期化;

        /// <summary>
        /// 再生モードコンボボックスの項目
        /// </summary>
        enum ComboBoxPlayModeType {
            AllTracks,
            AllTracksRepeat,
            OneTrack,
            OneTrackRepeat,
            Shuffle,
            ShuffleRepeat,
            NUM
        };

        enum PlayListClearMode {
            // プレイリストをクリアーし、UI状態も更新する。(通常はこちらを使用。)
            ClearWithUpdateUI,

            // ワーカースレッドから呼ぶためUIを操作しない。UIは内部状態とは矛盾した状態になるため
            // この後UIスレッドであらためてClearPlayList(ClearWithUpdateUI)する必要あり。
            ClearWithoutUpdateUI,
        }

        private StringBuilder mLoadErrMsg;

        struct ReadProgressInf {
            public int pcmDataId;
            public long startFrame;
            public long endFrame;
            public int trackCount;
            public int trackNum;

            public long readFrames;

            public long WantFramesTotal {
                get {
                    return endFrame - startFrame;
                }
            }

            public ReadProgressInf(int pcmDataId, long startFrame, long endFrame, int trackCount, int trackNum) {
                this.pcmDataId = pcmDataId;
                this.startFrame = startFrame;
                this.endFrame = endFrame;
                this.trackCount = trackCount;
                this.trackNum = trackNum;
                this.readFrames = 0;
            }

            public void FileReadStart(int pcmDataId, long startFrame, long endFrame) {
                this.pcmDataId = pcmDataId;
                this.startFrame = startFrame;
                this.endFrame = endFrame;
                this.readFrames = 0;
            }
        };

        /// <summary>
        /// ファイル読み出しの進捗状況
        /// </summary>
        ReadProgressInf mReadProgressInf;

        /// <summary>
        /// ビットフォーマット変換クラス。
        /// ノイズシェイピングのerror値を持っているので都度作らないようにする。
        /// </summary>
        private WasapiPcmUtil.PcmUtil mPcmUtil;

        private void ChangeState(State nowState) {
            mState = nowState;
        }

        /// <summary>
        /// 指定されたWavDataIdの、再生リスト位置番号(再生リスト内のindex)を戻す。
        /// </summary>
        /// <param name="pcmDataId">再生リスト位置番号を知りたいPcmDataのId</param>
        /// <returns>再生リスト位置番号(再生リスト内のindex)。見つからないときは-1</returns>
        private int GetPlayListIndexOfPcmDataId(int pcmDataId) {
            for (int i = 0; i < mPlayListItems.Count(); ++i) {
                if (mPlayListItems[i].PcmData() != null
                        && mPlayListItems[i].PcmData().Id == pcmDataId) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// ノンブロッキング版 PPW再生リスト読み込み。
        /// 読み込みを開始した時点で制御が戻り、再生リスト読み込み中状態になる。
        /// その後、バックグラウンドで再生リストを読み込む。完了すると再生リストあり状態に遷移する。
        /// </summary>
        /// <param name="path">string.Emptyのとき: IsolatedStorageに保存された再生リストを読む。</param>
        private void ReadPpwPlaylistStart(string path, ReadPpwPlaylistMode mode) {
            ChangeState(State.再生リスト読み込み中);
            UpdateUIStatus();

            mLoadErrMsg = new StringBuilder();

            PlaylistSave3 pl;
            if (path.Length == 0) {
                pl = PpwPlaylistRW.Load();
            } else {
                pl = PpwPlaylistRW.LoadFrom(path);
            }

            progressBar1.Visibility = System.Windows.Visibility.Visible;

            ReadPlayListRunAsync(pl, mode);
        }

        private void EnableDataGridPlaylist() {
            dataGridPlayList.IsEnabled = true;
            dataGridPlayList.ItemsSource = mPlayListItems;

            if (0 <= mPreference.LastPlayItemIndex &&
                    mPreference.LastPlayItemIndex < dataGridPlayList.Items.Count) {
                dataGridPlayList.SelectedIndex = mPreference.LastPlayItemIndex;
                dataGridPlayList.ScrollIntoView(dataGridPlayList.SelectedItem);
            }
        }

        private bool SavePpwPlaylist(string path) {
            var s = new PlaylistSave3();

            for (int i=0; i<mAp.PcmDataListForDisp.Count(); ++i) {
                var p = mAp.PcmDataListForDisp.At(i);
                var playListItem = mPlayListItems[i];

                s.Add(new PlaylistItemSave3().Set(
                        p.DisplayName, p.AlbumTitle, p.ArtistName, p.ComposerName, p.FullPath,
                        p.CueSheetIndex, p.StartTick, p.EndTick, playListItem.ReadSeparaterAfter, p.LastWriteTime, p.TrackId));
            }

            if (path.Length == 0) {
                return PpwPlaylistRW.Save(s);
            } else {
                return PpwPlaylistRW.SaveAs(s, path);
            }
        }

        /// <summary>
        /// 他のアプリからのコマンドライン引数を受け取るコールバック関数。
        /// </summary>
        private void MultipleAppInstanceRecvMsg(object cbObject, MultipleAppInstanceMgr.ReceivedMessage msg) {
            // 後から起動したアプリのコマンドライン引数を受け取った。
            var self = (MainWindow)cbObject;

            self.CommandlineArgsReceived(msg);
        }

        /// <summary>
        /// コマンドライン引数から音声ファイル名を抽出します。
        /// </summary>
        private static string[] GetFileListFromCommandlineArgs(List<string> args) {
            var r = new List<string>();

            // args[0]はプログラムの名前なのでスキップ。1から始める。
            for (int i = 1; i < args.Count; ++i) {
                if (args[i].Length == 0 || args[i][0] == '-') {
                    // コマンドラインのオプション。
                    continue;
                }
                // ファイル名なので追加。
                r.Add(args[i]);
            }

            return r.ToArray();
        }

        public void CommandlineArgsReceived(MultipleAppInstanceMgr.ReceivedMessage msg) {
            Dispatcher.BeginInvoke(new Action(delegate() {
                // UIスレッドで実行される。

                var fileList = GetFileListFromCommandlineArgs(msg.args);
                if (0 < fileList.Length) {
                    // ファイルを再生リストに追加。後で追加しても良い。
                    AddFilesToPlaylist(fileList, false);
                }
            }));
        }

        /// <summary>
        /// 溜まっていたファイル追加リクエストをまとめて処理します。
        /// 再生停止時にUIスレッドから実行。
        /// </summary>
        private void ProcAddFilesMsg() {
            // 再生リストに今すぐ追加しなければならない。
            bool b = AddFilesToPlaylist(mDelayedAddFiles.ToArray(), true);
            System.Diagnostics.Debug.Assert(b);

            mDelayedAddFiles.Clear();
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        public MainWindow()
        {
            InitializeComponent();
            SetLocalizedTextToUI();

            this.AddHandler(Slider.MouseLeftButtonDownEvent, new MouseButtonEventHandler(slider1_MouseLeftButtonDown), true);
            this.AddHandler(Slider.MouseLeftButtonUpEvent, new MouseButtonEventHandler(slider1_MouseLeftButtonUp), true);

            // ■■ 順番注意：InitializeComponent()によって、チェックボックスのチェックイベントが発生し
            // ■■ mPreferenceの内容が変わるので、InitializeComponent()の後にロードする。

            mPreference = PreferenceStore.Load();

            if (!mPreference.AllowsMultipleAppInstances) {
                // 多重起動の防止をする。
                if (mMultiAppInstMgr.IsAppAlreadyRunning()) {
                    // 既に起動しているアプリ インスタンスにコマンドライン引数を送って終了する。
                    mMultiAppInstMgr.ClientSendMsgToServer(Environment.GetCommandLineArgs());
                    Exit();
                } else {
                    // 名前付きパイプを作り、後から起動したアプリのコマンドライン引数を受け取ります。
                    mMultiAppInstMgr.ServerStart(MultipleAppInstanceRecvMsg, this);
                }
            }

            if (mPreference.ManuallySetMainWindowDimension) {
                // 記録されているウィンドウ形状が、一部分でも画面に入っていたら、そのウィンドウ形状に設定する。
                var windowRect = new System.Drawing.Rectangle(
                        (int)mPreference.MainWindowLeft,
                        (int)mPreference.MainWindowTop,
                        (int)mPreference.MainWindowWidth,
                        (int)mPreference.MainWindowHeight);

                bool inScreen = false;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens) {
                    if (!System.Drawing.Rectangle.Intersect(windowRect, screen.Bounds).IsEmpty) {
                        inScreen = true;
                        break;
                    }
                }
                if (inScreen) {
                    Left = mPreference.MainWindowLeft;
                    Top = mPreference.MainWindowTop;
                    if (100 <= mPreference.MainWindowWidth) {
                        Width = mPreference.MainWindowWidth;
                    }
                    if (100 <= mPreference.MainWindowHeight) {
                        Height = mPreference.MainWindowHeight;
                    }
                }
            }

            if (!mPreference.SettingsIsExpanded) {
                expanderSettings.IsExpanded = false;
            }

            AddLogText(string.Format(CultureInfo.InvariantCulture, "PlayPcmWin {0} {1}{2}",
                    AssemblyVersion, IntPtr.Size == 8 ? "64bit" : "32bit", Environment.NewLine));
            if (IntPtr.Size == 8) {
                var asm = new WWAsmCs.WWAsm();
                AddLogText(string.Format(CultureInfo.InstalledUICulture, "{0}{1}", asm.CpuCapabilityStr(), Environment.NewLine));
            }

            int hr = mAp.WasapiInit();
            AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.Init() {0:X8}{1}", hr, Environment.NewLine));

            mWasapiStateChangedDelegate = new Wasapi.WasapiCS.StateChangedCallback(WasapiStatusChanged);
            mAp.wasapi.RegisterStateChangedCallback(mWasapiStateChangedDelegate);

            textBoxLatency.Text = string.Format(CultureInfo.InvariantCulture, "{0}", mPreference.LatencyMillisec);

            checkBoxSoundEffects.IsChecked = mPreference.SoundEffectsEnabled;
            buttonSoundEffectsSettings.IsEnabled = mPreference.SoundEffectsEnabled;
            mPreferenceAudioFilterList = PreferenceAudioFilterStore.Load();
            UpdateSoundEffects(mPreference.SoundEffectsEnabled);

            switch (mPreference.WasapiSharedOrExclusive) {
            case WasapiSharedOrExclusiveType.Exclusive:
                radioButtonExclusive.IsChecked = true;
                break;
            case WasapiSharedOrExclusiveType.Shared:
                radioButtonShared.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            switch (mPreference.WasapiDataFeedMode) {
            case WasapiDataFeedModeType.EventDriven:
                radioButtonEventDriven.IsChecked = true;
                break;
            case WasapiDataFeedModeType.TimerDriven:
                radioButtonTimerDriven.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            UpdatePlaymodeComboBoxFromPreference();

            UpdateDeviceList();

            RestorePlaylistColumnOrderFromPreference();

            SetupBackgroundWorkers();

            PlayListItemInfo.SetNextRowId(1);
            mGroupIdNextAdd = 0;

            PreferenceUpdated();

            AddKeyListener();
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        private const int WM_DEVICECHANGE = 0x219;
        private const uint DBT_DEVICEREMOVECOMPLETE = 0x8004u;

        // WM_DEVICECHANGE イベントを取得する。
        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParamP, IntPtr lParamP, ref bool handled) {
            uint wParam = (uint)wParamP.ToInt64();

            switch (msg) {
            case WM_DEVICECHANGE:
                if (wParam == DBT_DEVICEREMOVECOMPLETE) {
                    Console.WriteLine("WM_DEVICECHANGE DBG_DEVICEREMOVECOMPLETE");
                    FileDisappearedEventProc("");
                }
                break;
            }
            
            return IntPtr.Zero;
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        private void Issue130() {
            EnableDataGridPlaylist();
            ChangeState(State.再生リストなし);
            UpdateUIStatus();
        }

        private void Window_Loaded(object wSender, RoutedEventArgs we) {
            {
                var tc = new DarkModeCtrl();
                if (!tc.IsDarkMode()) {
                    // Dark mode


                }
            }

            {
                // slider1のTrackをクリックしてThumbがクリック位置に移動した時Thumbがつままれた状態になるようにする
                slider1.ApplyTemplate();
                (slider1.Template.FindName("PART_Track", slider1) as Track).Thumb.MouseEnter += new MouseEventHandler((sliderSender, se) => {
                    if (se.LeftButton == MouseButtonState.Pressed && se.MouseDevice.Captured == null) {
                        var args = new MouseButtonEventArgs(se.MouseDevice, se.Timestamp, MouseButton.Left);
                        args.RoutedEvent = MouseLeftButtonDownEvent;
                        (sliderSender as Thumb).RaiseEvent(args);
                    }
                });
            }

            if (ProcessCommandline(Environment.GetCommandLineArgs())) {
                // コマンドラインの命令を実行した場合。
            } else {
                // コマンドライン命令無し。

                if (mPreference.StorePlaylistContent) {
                    ReadPpwPlaylistStart(string.Empty, ReadPpwPlaylistMode.RestorePlaylistOnProgramStart);
                } else {
                    Issue130();
                }
            }
        }

        private void PrintUsage() {
            AddLogText("Commandline Usage: PlayPcmWin [-stop] [-play] [soundFile1 [soundFile2 ...] ] \r\n");
        }

        /// <param name="args">1個目がPPWの名前。</param>
        private bool ProcessCommandline(string[] args) {
            if (args.Length == 1) {
                return false;
            }

            Issue130();

            bool bPlay = true;

            var fileList = new List<string>();

            for (int i = 1; i < args.Length; ++i) {
                string s = args[i];
                switch (s) {
                case "-stop":
                    // 停止状態で起動。
                    bPlay = false;
                    break;
                case "-play":
                    // 再生状態で起動。
                    bPlay = true;
                    break;
                default:
                    if (File.Exists(s)){
                        fileList.Add(s);
                    } else {
                        AddLogText(string.Format("Error: Unknown command: {0}\r\n", s));
                        PrintUsage();
                        return false;
                    }
                    break;
                }
            }

            if (0 < fileList.Count()) {
                ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
                int loadFileCount = 0;
                foreach (var f in fileList) {
                    int errCount = ReadFileHeader(f, WWSoundFileRW.WWPcmHeaderReader.ReadHeaderMode.OnlyConcreteFile, null);
                    if (0 == errCount) {
                        ++loadFileCount;
                    }
                }

                if (0 < loadFileCount) {
                    if (bPlay) {
                        ButtonPlayClicked();
                    } else {
                        ChangeState(State.再生リストあり);
                        UpdateUIStatus();
                    }
                }
                return true;
            }

            // コマンドラインに有効な命令無し。
            return false;
        }

        private void SetLocalizedTextToUI() {
            comboBoxPlayMode.Items.Clear();
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeAllTracks);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeAllTracksRepeat);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeOneTrack);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeOneTrackRepeat);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeShuffle);
            comboBoxPlayMode.Items.Add(Properties.Resources.MainPlayModeShuffleRepeat);

            cmenuPlayListClear.Header = Properties.Resources.MainCMenuPlaylistClear;
            cmenuPlayListEditMode.Header = Properties.Resources.MainCMenuPlayListEditMode;

            menuFile.Header = Properties.Resources.MenuFile;
            menuItemFileNew.Header = Properties.Resources.MenuItemFileNew;
            menuItemFileOpen.Header = Properties.Resources.MenuItemFileOpen;
            menuItemFileSaveAs.Header = Properties.Resources.MenuItemFileSaveAs;
            menuItemFileSaveCueAs.Header = Properties.Resources.MenuItemFileSaveCueAs;
            menuItemFileExit.Header = Properties.Resources.MenuItemFileExit;

            menuTool.Header = Properties.Resources.MenuTool;
            menuItemToolSettings.Header = Properties.Resources.MenuItemToolSettings;

            menuPlayList.Header = Properties.Resources.MenuPlayList;
            menuItemPlayListClear.Header = Properties.Resources.MenuItemPlayListClear;
            menuItemPlayListItemEditMode.Header = Properties.Resources.MenuItemPlayListItemEditMode;

            menuHelp.Header = Properties.Resources.MenuHelp;
            menuItemHelpAbout.Header = Properties.Resources.MenuItemHelpAbout;
            menuItemHelpWeb.Header = Properties.Resources.MenuItemHelpWeb;

            groupBoxLog.Header = Properties.Resources.MainGroupBoxLog;
            groupBoxOutputDevices.Header = Properties.Resources.MainGroupBoxOutputDevices;
            groupBoxPlaybackControl.Header = Properties.Resources.MainGroupBoxPlaybackControl;
            groupBoxPlaylist.Header = Properties.Resources.MainGroupBoxPlaylist;
            groupBoxWasapiDataFeedMode.Header = Properties.Resources.MainGroupBoxWasapiDataFeedMode;

            groupBoxWasapiOperationMode.Header = Properties.Resources.MainGroupBoxWasapiOperationMode;
            groupBoxWasapiOutputLatency.Header = Properties.Resources.MainGroupBoxWasapiOutputLatency;
            groupBoxWasapiSettings.Header = Properties.Resources.MainGroupBoxWasapiSettings;

            buttonClearPlayList.Content = Properties.Resources.MainButtonClearPlayList;
            buttonDelistSelected.Content = Properties.Resources.MainButtonDelistSelected;
            buttonInspectDevice.Content = Properties.Resources.MainButtonInspectDevice;
            buttonNext.Content = Properties.Resources.MainButtonNext;
            buttonPause.Content = Properties.Resources.MainButtonPause;

            buttonPlay.Content = Properties.Resources.MainButtonPlay;
            buttonPrev.Content = Properties.Resources.MainButtonPrev;
            buttonSettings.Content = Properties.Resources.MainButtonSettings;
            buttonStop.Content = Properties.Resources.MainButtonStop;

            radioButtonEventDriven.Content = Properties.Resources.MainRadioButtonEventDriven;
            radioButtonExclusive.Content = Properties.Resources.MainRadioButtonExclusive;
            radioButtonShared.Content = Properties.Resources.MainRadioButtonShared;
            radioButtonTimerDriven.Content = Properties.Resources.MainRadioButtonTimerDriven;

            expanderSettings.Header = Properties.Resources.MainExpanderSettings;

            dataGridColumnAlbumTitle.Header = Properties.Resources.MainDataGridColumnAlbumTitle;
            dataGridColumnArtist.Header = Properties.Resources.MainDataGridColumnArtist;
            dataGridColumnComposerName.Header = Properties.Resources.MainDataGridColumnComposer;
            dataGridColumnBitRate.Header = Properties.Resources.MainDataGridColumnBitRate;
            dataGridColumnDuration.Header = Properties.Resources.MainDataGridColumnDuration;
            dataGridColumnIndexNr.Header = Properties.Resources.MainDataGridColumnIndexNr;

            dataGridColumnNumChannels.Header = Properties.Resources.MainDataGridColumnNumChannels;
            dataGridColumnQuantizationBitRate.Header = Properties.Resources.MainDataGridColumnQuantizationBitRate;
            dataGridColumnReadSeparaterAfter.Header = Properties.Resources.MainDataGridColumnReadSeparaterAfter;
            dataGridColumnSampleRate.Header = Properties.Resources.MainDataGridColumnSampleRate;
            dataGridColumnTitle.Header = Properties.Resources.MainDataGridColumnTitle;
            dataGridColumnFileExtension.Header = Properties.Resources.MainDataGridColumnFileExtension;

            labelLoadingPlaylist.Content = Properties.Resources.MainStatusReadingPlaylist;

            groupBoxWasapiSoundEffects.Header = Properties.Resources.GroupBoxSoundEffects;
            buttonSoundEffectsSettings.Content = Properties.Resources.ButtonSoundEffectsSettings;
            checkBoxSoundEffects.Content = Properties.Resources.CheckBoxSoundEffects;
        }

        private bool IsPlayModeAllTracks() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.AllTracks
                || t == ComboBoxPlayModeType.AllTracksRepeat;
        }

        private bool IsPlayModeOneTrack() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.OneTrack
                || t == ComboBoxPlayModeType.OneTrackRepeat;
        }

        private bool IsPlayModeShuffle() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.Shuffle
                || t == ComboBoxPlayModeType.ShuffleRepeat;
        }

        private bool IsPlayModeRepeat() {
            ComboBoxPlayModeType t = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            return t == ComboBoxPlayModeType.AllTracksRepeat
                || t == ComboBoxPlayModeType.OneTrackRepeat
                || t == ComboBoxPlayModeType.ShuffleRepeat;
        }

        private void UpdatePlaymodeComboBoxFromPreference() {
            if (mPreference.Shuffle) {
                 if (mPreference.PlayRepeat) {
                     comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.ShuffleRepeat;
                 } else {
                     comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.Shuffle;
                 }
            } else if (mPreference.PlayAllTracks) {
                if (mPreference.PlayRepeat) {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.AllTracksRepeat;
                } else {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.AllTracks;
                }
            } else {
                if (mPreference.PlayRepeat) {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.OneTrackRepeat;
                } else {
                    comboBoxPlayMode.SelectedIndex = (int)ComboBoxPlayModeType.OneTrack;
                }
            }
        }

        private void SetPreferencePlaymodeFromComboBox() {
            mPreference.PlayAllTracks = IsPlayModeAllTracks();
            mPreference.PlayRepeat    = IsPlayModeRepeat();
            mPreference.Shuffle       = IsPlayModeShuffle();
        }

        // 再生リストの列の順番設定の保存
        private void SavePlaylistColumnOrderToPreference() {
            var idxNameTable = new Dictionary<int, string>();
            int i=0;
            foreach (var item in dataGridPlayList.Columns) {
                idxNameTable.Add(item.DisplayIndex, mPlaylistColumnDefaults[i].Name);
                ++i;
            }

            mPreference.PlayListColumnsOrder.Clear();
            foreach (var item in idxNameTable.OrderBy(x => x.Key)) {
                mPreference.PlayListColumnsOrder.Add(item.Value);
            }
        }

        // 再生リストの列の順番を設定から読み出し適用する
        private bool RestorePlaylistColumnOrderFromPreference() {
            var nameIdxTable = new Dictionary<string, int>();
            {
                int i=0;
                foreach (var item in mPreference.PlayListColumnsOrder) {
                    nameIdxTable.Add(item, i);
                    ++i;
                }
            }
            var columnIdxList = new List<int>();
            foreach (var item in mPlaylistColumnDefaults) {
                int idx;
                if (!nameIdxTable.TryGetValue(item.Name, out idx)) {
                    Console.WriteLine("E: unknown playlist column name {0}", item.Name);
                    System.Diagnostics.Debug.Assert(false);
                    return false;
                }
                columnIdxList.Add(idx);
            }

            if (columnIdxList.Count != dataGridPlayList.Columns.Count) {
                Console.WriteLine("E: playlist column count mismatch {0}", columnIdxList.Count);
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            {
                int i=0;
                foreach (var item in dataGridPlayList.Columns) {
                    item.DisplayIndex = columnIdxList[i];
                    ++i;
                }
            }
            return true;
        }

        private void SetupBackgroundWorkers() {
            ReadFileWorkerSetup();
            ReadPlayListWorkerSetup();
        }

        private void Window_Closed(object sender, EventArgs e) {
            Term();
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e) {
            Exit();
        }

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private static string SampleFormatTypeToStr(WasapiCS.SampleFormatType t) {
            switch (t) {
            case WasapiCS.SampleFormatType.Sfloat:
                return "32bit"+Properties.Resources.FloatingPointNumbers;
            case WasapiCS.SampleFormatType.Sint16:
                return "16bit";
            case WasapiCS.SampleFormatType.Sint24:
                return "24bit";
            case WasapiCS.SampleFormatType.Sint32:
                return "32bit";
            case WasapiCS.SampleFormatType.Sint32V24:
                return "32bit("+Properties.Resources.ValidBits + "=24)";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private void DispCoverart(byte[] pictureData) {

            if (null == pictureData || pictureData.Length <= 0) {
                imageCoverArt.Source = null;
                // imageCoverArt.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            try {
                using (var stream = new MemoryStream(pictureData)) {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = null;
                    bi.StreamSource = stream;
                    bi.EndInit();

                    imageCoverArt.Source = bi;
                    // imageCoverArt.Visibility = System.Windows.Visibility.Visible;
                }
            } catch (IOException ex) {
                Console.WriteLine("D: DispCoverart {0}", ex);
                imageCoverArt.Source = null;
            } catch (System.IO.FileFormatException ex) {
                Console.WriteLine("D: DispCoverart {0}", ex);
                imageCoverArt.Source = null;
            } catch (System.NotSupportedException ex) {
                Console.WriteLine("D: DispCoverart {0}", ex);
                imageCoverArt.Source = null;
            }
        }

        private void UpdateCoverart() {
            if (!mPreference.DispCoverart) {
                // do not display coverart
                imageCoverArt.Source = null;
                imageCoverArt.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            // display coverart
            imageCoverArt.Visibility = System.Windows.Visibility.Visible;

            if (dataGridPlayList.SelectedIndex < 0) {
                DispCoverart(null);
                return;
            }
            PcmDataLib.PcmData w = mPlayListItems[dataGridPlayList.SelectedIndex].PcmData();
            if (null != w && 0 < w.PictureBytes) {
                DispCoverart(w.PictureData);
            } else {
                DispCoverart(null);
            }
        }

        // 初期状態。再生リストなし。
        private void UpdateUIToInitialState() {
            cmenuPlayListClear.IsEnabled     = false;
            cmenuPlayListEditMode.IsEnabled  = true;
            menuItemFileNew.IsEnabled        = false;
            menuItemFileOpen.IsEnabled       = true;
            menuItemFileSaveAs.IsEnabled     = false;
            menuItemFileSaveCueAs.IsEnabled  = false;
            menuItemPlayListClear.IsEnabled = false;
            menuItemPlayListItemEditMode.IsEnabled = true;

            buttonPlay.IsEnabled             = false;
            buttonStop.IsEnabled             = false;
            buttonPause.IsEnabled            = false;
            comboBoxPlayMode.IsEnabled       = true;

            buttonNext.IsEnabled             = false;
            buttonPrev.IsEnabled             = false;
            groupBoxWasapiOperationMode.IsEnabled = true;
            groupBoxWasapiDataFeedMode.IsEnabled = true;
            groupBoxWasapiOutputLatency.IsEnabled = true;

            buttonClearPlayList.IsEnabled    = false;
            buttonDelistSelected.IsEnabled = false;

            buttonInspectDevice.IsEnabled = 0 < listBoxDevices.Items.Count;

            buttonSettings.IsEnabled = true;
            menuItemToolSettings.IsEnabled = true;

            labelLoadingPlaylist.Visibility = System.Windows.Visibility.Collapsed;
        }

        // 再生リストあり。再生していない状態。
        private void UpdateUIToEditableState() {
            cmenuPlayListClear.IsEnabled = true;
            cmenuPlayListEditMode.IsEnabled = true;
            menuItemFileNew.IsEnabled = true;
            menuItemFileOpen.IsEnabled = true;
            menuItemFileSaveAs.IsEnabled = true;
            menuItemFileSaveCueAs.IsEnabled = true;
            menuItemPlayListClear.IsEnabled = true;
            menuItemPlayListItemEditMode.IsEnabled = true;

            if (0 == listBoxDevices.Items.Count) {
                // 再生デバイスが全く存在しない時
                buttonPlay.IsEnabled = false;
            } else {
                buttonPlay.IsEnabled = true;
            }

            buttonStop.IsEnabled = false;
            buttonPause.IsEnabled = false;
            comboBoxPlayMode.IsEnabled = true;

            buttonNext.IsEnabled = true;
            buttonPrev.IsEnabled = true;
            groupBoxWasapiOperationMode.IsEnabled = true;
            groupBoxWasapiDataFeedMode.IsEnabled = true;
            groupBoxWasapiOutputLatency.IsEnabled = true;

            buttonClearPlayList.IsEnabled = true;
            buttonDelistSelected.IsEnabled = (dataGridPlayList.SelectedIndex >= 0);
            buttonInspectDevice.IsEnabled = 0 < listBoxDevices.Items.Count;

            buttonSettings.IsEnabled = true;
            menuItemToolSettings.IsEnabled = true;

            labelLoadingPlaylist.Visibility = System.Windows.Visibility.Collapsed;
        }

        // 再生リストあり。再生開始処理中。
        private void UpdateUIToNonEditableState() {
            cmenuPlayListClear.IsEnabled = false;
            cmenuPlayListEditMode.IsEnabled = false;
            menuItemFileNew.IsEnabled = false;
            menuItemFileOpen.IsEnabled = false;
            menuItemFileSaveAs.IsEnabled = false;
            menuItemFileSaveCueAs.IsEnabled = false;
            menuItemPlayListClear.IsEnabled = false;
            menuItemPlayListItemEditMode.IsEnabled = false;
            buttonPlay.IsEnabled = false;
            buttonStop.IsEnabled = false;
            buttonPause.IsEnabled = false;
            comboBoxPlayMode.IsEnabled = false;

            buttonNext.IsEnabled = false;
            buttonPrev.IsEnabled = false;
            groupBoxWasapiOperationMode.IsEnabled = false;
            groupBoxWasapiDataFeedMode.IsEnabled = false;
            groupBoxWasapiOutputLatency.IsEnabled = false;

            buttonClearPlayList.IsEnabled = false;
            buttonDelistSelected.IsEnabled = false;
            buttonInspectDevice.IsEnabled = false;

            buttonSettings.IsEnabled = false;
            menuItemToolSettings.IsEnabled = false;

            labelLoadingPlaylist.Visibility = System.Windows.Visibility.Collapsed;
        }

        // 再生中。
        private void UpdateUIToPlayingState() {
            cmenuPlayListClear.IsEnabled = false;
            cmenuPlayListEditMode.IsEnabled = false;
            menuItemFileNew.IsEnabled = false;
            menuItemFileOpen.IsEnabled = false;
            menuItemFileSaveAs.IsEnabled = false;
            menuItemFileSaveCueAs.IsEnabled = false;
            menuItemPlayListClear.IsEnabled = false;
            menuItemPlayListItemEditMode.IsEnabled = false;
            buttonPlay.IsEnabled = false;
            buttonStop.IsEnabled = true;
            buttonPause.IsEnabled = true;
            buttonPause.Content = Properties.Resources.MainButtonPause;
            comboBoxPlayMode.IsEnabled = false;

            buttonNext.IsEnabled = true;
            buttonPrev.IsEnabled = true;
            groupBoxWasapiOperationMode.IsEnabled = false;
            groupBoxWasapiDataFeedMode.IsEnabled = false;
            groupBoxWasapiOutputLatency.IsEnabled = false;

            buttonClearPlayList.IsEnabled = false;
            buttonDelistSelected.IsEnabled = false;
            buttonInspectDevice.IsEnabled = false;

            buttonSettings.IsEnabled = false;
            menuItemToolSettings.IsEnabled = false;

            labelLoadingPlaylist.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void UpdateUIStatus() {
            dataGridPlayList.UpdateLayout();
            UpdateCoverart();
            PPWServerSendPlaylist();

            slider1.IsEnabled = true;
            labelPlayingTime.Content = PLAYING_TIME_ALLZERO;

            switch (mState) {
            case State.再生リストなし:
                UpdateUIToInitialState();
                statusBarText.Content = Properties.Resources.MainStatusPleaseCreatePlaylist;
                break;
            case State.再生リスト読み込み中:
                UpdateUIToInitialState();
                statusBarText.Content = Properties.Resources.MainStatusReadingPlaylist;
                dataGridPlayList.IsEnabled = false;
                if (0 == dataGridPlayList.Items.Count) {
                    labelLoadingPlaylist.Visibility = System.Windows.Visibility.Visible;
                }
                break;
            case State.再生リストあり:
                UpdateUIToEditableState();
                if (0 < dataGridPlayList.Items.Count &&
                        dataGridPlayList.SelectedIndex < 0) {
                    // プレイリストに項目があり、選択されている曲が存在しない時、最初の曲を選択状態にする
                    dataGridPlayList.SelectedIndex = 0;
                }
                statusBarText.Content = Properties.Resources.MainStatusPressPlayButton;
                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.デバイスSetup完了:
                // 一覧のクリアーとデバイスの選択、再生リストの作成関連を押せなくする。
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusReadingFiles;
                break;
            case State.ファイル読み込み完了:
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusReadCompleted;

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                slider1.Value = 0;
                labelPlayingTime.Content = PLAYING_TIME_UNKNOWN;
                break;
            case State.再生中: {
                    UpdateUIToPlayingState();

                    var stat = mAp.wasapi.GetSessionStatus();
                    if (WasapiCS.StreamType.DoP == stat.StreamType) {
                        statusBarText.Content = string.Format(CultureInfo.InvariantCulture, "{0} WASAPI{1} {2}kHz {3} {4}ch DoP DSD {5:F1}MHz. Audio buffer size={6:F1}ms",
                                Properties.Resources.MainStatusPlaying,
                                radioButtonShared.IsChecked == true ? Properties.Resources.Shared : Properties.Resources.Exclusive,
                                stat.DeviceSampleRate * 0.001,
                                SampleFormatTypeToStr(stat.DeviceSampleFormat),
                                stat.DeviceNumChannels, stat.DeviceSampleRate * 0.000016,
                                1000.0 * stat.EndpointBufferFrameNum / stat.DeviceSampleRate);
                    } else {
                        statusBarText.Content = string.Format(CultureInfo.InvariantCulture, "{0} WASAPI{1} {2}kHz {3} {4}ch PCM {5:F2}dB. Audio buffer size={6:F1}ms",
                                Properties.Resources.MainStatusPlaying,
                                radioButtonShared.IsChecked == true ? Properties.Resources.Shared : Properties.Resources.Exclusive,
                                stat.DeviceSampleRate * 0.001,
                                SampleFormatTypeToStr(stat.DeviceSampleFormat),
                                stat.DeviceNumChannels,
                                20.0 * Math.Log10(mAp.wasapi.GetScalePcmAmplitude()),
                                1000.0 * stat.EndpointBufferFrameNum / stat.DeviceSampleRate);
                    }

                    progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                }
                break;
            case State.再生一時停止中:
                UpdateUIToPlayingState();
                buttonPause.Content = Properties.Resources.MainButtonResume;
                buttonNext.IsEnabled             = true;
                buttonPrev.IsEnabled             = true;
                statusBarText.Content = Properties.Resources.MainStatusPaused;

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.再生停止開始:
                UpdateUIToNonEditableState();
                statusBarText.Content = Properties.Resources.MainStatusStopping;
                break;
            case State.再生グループ読み込み中:
                UpdateUIToNonEditableState();
                if (radioButtonShared.IsChecked == true) {
                    // 共有モード。
                    statusBarText.Content = Properties.Resources.MainStatusReadingFiles;
                } else {
                    // 排他モード。
                    switch (mDeviceSetupParams.StreamType) {
                    case WasapiCS.StreamType.PCM:
                        statusBarText.Content = Properties.Resources.MainStatusReadingFiles;
                        break;
                    case WasapiCS.StreamType.DoP:
                        statusBarText.Content = Properties.Resources.MainStatusReadingFilesDoP;
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                    }
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            // 再生リストモード更新
            if (dataGridPlayList.IsReadOnly) {
                // 再生モード
                menuItemPlayListItemEditMode.IsChecked = false;
                cmenuPlayListEditMode.IsChecked = false;
            } else {
                // 編集モード
                menuItemPlayListItemEditMode.IsChecked = true;
                cmenuPlayListEditMode.IsChecked = true;
            }

            if (mPreference.AlternatingRowBackground) {
                dataGridPlayList.AlternatingRowBackground
                        = new SolidColorBrush(Util.ColorFromArgb(mPreference.AlternatingRowBackgroundArgb));
            } else {
                dataGridPlayList.AlternatingRowBackground = null;
            }
        }

        /// <summary>
        /// デバイス一覧を取得し、デバイス一覧リストを更新する。
        /// 同一デバイスのデバイス番号がずれるので注意。
        /// </summary>
        private void UpdateDeviceList() {
            int hr;

            int selectedIndex = -1;

            listBoxDevices.Items.Clear();

            hr = mAp.wasapi.EnumerateDevices(WasapiCS.DeviceType.Play);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.DoDeviceEnumeration(Play) {0:X8}{1}", hr, Environment.NewLine));

            int nDevices = mAp.wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                var attr = mAp.wasapi.GetDeviceAttributes(i);
                listBoxDevices.Items.Add(new DeviceAttributes(i, attr.Name, attr.DeviceIdString));

                if (0 < mPreference.PreferredDeviceName.Length
                        && 0 == string.CompareOrdinal(mPreference.PreferredDeviceName, attr.Name)) {
                    // PreferredDeviceIdStringは3.0.60で追加されたので、存在しないことがある
                    // 存在するときだけチェックする
                    if (0 < mPreference.PreferredDeviceIdString.Length
                            && 0 != string.CompareOrdinal(mPreference.PreferredDeviceIdString, attr.DeviceIdString)) {
                        continue;
                    }

                    // お気に入りデバイスを選択状態にする。
                    selectedIndex = i;
                }
            }

            if (0 < nDevices) {
                if (0 <= selectedIndex && selectedIndex < listBoxDevices.Items.Count) {
                    listBoxDevices.SelectedIndex = selectedIndex;
                } else {
                    listBoxDevices.SelectedIndex = 0;
                }

                buttonInspectDevice.IsEnabled = true;
            }

            if (0 < mPlayListItems.Count) {
                ChangeState(State.再生リストあり);
            } else {
                ChangeState(State.再生リストなし);
            }

            UpdateUIStatus();
        }

        /// <summary>
        /// 再生中の場合は、停止。
        /// 再生中でない場合は、再生停止後イベントtaskAfterStopをここで実行する。
        /// 再生中の場合は、停止完了後にtaskAfterStopを実行する。
        /// </summary>
        void Stop(NextTask taskAfterStop, bool stopGently) {
            mTaskAfterStop = taskAfterStop;

            if ( mAp.IsPlayWorkerBusy()) {
                mAp.PlayStop(stopGently);
                // 再生停止したらPlayRunWorkerCompletedでイベントを開始する。
            } else {
                // 再生停止後イベントをここで、いますぐ開始。
                PerformPlayCompletedTask();
            }
        }

        void StopBlocking()
        {
            Stop(new NextTask(NextTaskType.None), false);
            ReadFileWorkerCancelBlocking();
        }

        /// <summary>
        /// デバイス選択を解除する。再生停止中に呼ぶ。
        /// </summary>
        private void DeviceDeselect() {
            System.Diagnostics.Debug.Assert(!mAp.IsPlayWorkerBusy());

            mUseDevice = null;
            UnsetupDevice();

            mLoadedGroupId = -1;
            mLoadingGroupId = -1;

            if (0 < mPlayListItems.Count) {
                ChangeState(State.再生リストあり);
            } else {
                ChangeState(State.再生リストなし);
            }
            UpdateUIStatus();
        }

        private void Term() {

            try {
                DeleteKeyListener();
                FileDisappearCheck.Clear();

                mMultiAppInstMgr.Term();

                if (mAp.wasapi != null) {
                    // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせするブロッキング版のStopを呼ぶ。
                    // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
                    // この後のUnsetupの呼出によって開放されてしまい問題が起きる。
                    mAp.SetPlayEventCallback(null);
                    StopBlocking();
                    UnsetupDevice();
                    mAp.WasapiTerm();

                    // ウィンドウの位置とサイズを保存
                    mPreference.SetMainWindowLeftTopWidthHeight(Left, Top, Width, Height);

                    // 再生リピート設定を保存
                    SetPreferencePlaymodeFromComboBox();

                    // 設定画面の表示状態を保存
                    mPreference.SettingsIsExpanded = expanderSettings.IsExpanded;

                    // 再生リストの列の並び順を覚える
                    SavePlaylistColumnOrderToPreference();

                    // 最後に再生していた曲の番号
                    mPreference.LastPlayItemIndex = dataGridPlayList.SelectedIndex;

                    // 設定ファイルを書き出す。
                    PreferenceStore.Save(mPreference);

                    PreferenceAudioFilterStore.Save(mPreferenceAudioFilterList);

                    // 再生リストをIsolatedStorageに保存。
                    SavePpwPlaylist(string.Empty);
                }

            } catch (System.Exception ex) {
                Console.WriteLine("{0}", ex);
            }
        }

        private void Exit() {
            Term();
            // Application.Current.Shutdown();
            Close();
        }

        /// <summary>
        /// mAp.wasapi.Unsetupを行う。
        /// 既にUnsetup状態の場合は、空振りする。
        /// </summary>
        private void UnsetupDevice() {
            if (!mDeviceSetupParams.IsSetuped()) {
                return;
            }

            mAp.wasapi.Unsetup();
            AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.Unsetup(){0}", Environment.NewLine));
            mDeviceSetupParams.Unsetuped();
        }

        private int PcmChannelsToSetupChannels(int numChannels) {
            int ch = numChannels;

            if (mPreference.AddSilentForEvenChannel) {
                // 偶数チャンネルに繰り上げする。
                ch = (ch + 1) & (~1);
            }

            // モノラル1chのPCMデータはMonoToStereo()によってステレオ2chに変換してから再生する。
            if (1 == numChannels) {
                ch = 2;
            }

            switch (mPreference.ChannelCount2) {
            case ChannelCount2Type.SourceChannelCount:
                break;
            case ChannelCount2Type.Ch2:
            case ChannelCount2Type.Ch4:
            case ChannelCount2Type.Ch6:
            case ChannelCount2Type.Ch8:
            case ChannelCount2Type.Ch10:
            case ChannelCount2Type.Ch16:
            case ChannelCount2Type.Ch18:
            case ChannelCount2Type.Ch24:
            case ChannelCount2Type.Ch26:
            case ChannelCount2Type.Ch32:
                // チャンネル数変更。
                ch = (int)mPreference.ChannelCount2;
                break;
            case ChannelCount2Type.MixFormatChannelCount: {
                    // ミックスフォーマットのチャンネル数にする。
                    var mixFormat = mAp.wasapi.GetMixFormat(listBoxDevices.SelectedIndex);
                    if (mixFormat == null) {
                        // 異常だが、この後ログが出るのでここではスルーする。
                        ch = 2;
                    } else {
                        ch = mixFormat.numChannels;
                    }
                }
                break;
            }

            return ch;
        }

        /// <summary>
        /// デバイスSetupを行う。
        /// すでに同一フォーマットのSetupがなされている場合は空振りする。
        /// </summary>
        /// <param name="loadGroupId">再生するグループ番号。この番号のWAVファイルのフォーマットでSetupする。</param>
        /// <returns>false: デバイスSetup失敗。よく起こる。</returns>
        private bool SetupDevice(int loadGroupId) {
            int useDeviceId = listBoxDevices.SelectedIndex;

            int latencyMillisec = 0;
            if (!Int32.TryParse(textBoxLatency.Text, NumberStyles.Number,
                    CultureInfo.CurrentCulture, out latencyMillisec) || latencyMillisec <= 0) {
                latencyMillisec = Preference.DefaultLatencyMilliseconds;
                textBoxLatency.Text = string.Format(CultureInfo.InvariantCulture, "{0}", latencyMillisec);
            }
            mPreference.LatencyMillisec = latencyMillisec;

            int startWavDataId = mAp.PcmDataListForPlay.GetFirstPcmDataIdOnGroup(loadGroupId);
            System.Diagnostics.Debug.Assert(0 <= startWavDataId);

            var startPcmData = mAp.PcmDataListForPlay.FindById(startWavDataId);

            // 1つのフォーマットに対して複数(candidateNum個)のSetup()設定選択肢がありうる。

            int candidateNum = SampleFormatInfo.GetSetupSampleFormatCandidateNum(
                    mPreference.WasapiSharedOrExclusive,
                    mPreference.BitsPerSampleFixType,
                    startPcmData.ValidBitsPerSample,
                    startPcmData.SampleValueRepresentationType);
            for (int i = 0; i < candidateNum; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.CreateSetupSampleFormat(
                        mPreference.WasapiSharedOrExclusive,
                        mPreference.BitsPerSampleFixType,
                        startPcmData.BitsPerSample,
                        startPcmData.ValidBitsPerSample,
                        startPcmData.SampleValueRepresentationType,
                        i);

                if (mDeviceSetupParams.Is(
                        startPcmData.SampleRate,
                        sf.GetSampleFormatType(),
                        PcmChannelsToSetupChannels(startPcmData.NumChannels),
                        latencyMillisec,
                        mPreference.ZeroFlushMillisec,
                        mPreference.WasapiDataFeedMode,
                        mPreference.WasapiSharedOrExclusive,
                        mPreference.RenderThreadTaskType,
                        mPreference.ResamplerConversionQuality,
                        startPcmData.SampleDataType == PcmData.DataType.DoP ? WasapiCS.StreamType.DoP : WasapiCS.StreamType.PCM,
                        mPreference.MMThreadPriority)) {
                    // すでにこのフォーマットでSetup完了している。
                    return true;
                }
            }

            for (int i = 0; i < candidateNum; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.CreateSetupSampleFormat(
                        mPreference.WasapiSharedOrExclusive,
                        mPreference.BitsPerSampleFixType,
                        startPcmData.BitsPerSample,
                        startPcmData.ValidBitsPerSample,
                        startPcmData.SampleValueRepresentationType, i);

                mDeviceSetupParams.Set(
                        startPcmData.SampleRate,
                        sf.GetSampleFormatType(),
                        PcmChannelsToSetupChannels(startPcmData.NumChannels),
                        latencyMillisec,
                        mPreference.ZeroFlushMillisec,
                        mPreference.WasapiDataFeedMode,
                        mPreference.WasapiSharedOrExclusive,
                        mPreference.RenderThreadTaskType,
                        mPreference.ResamplerConversionQuality,
                        startPcmData.SampleDataType == PcmData.DataType.DoP ? WasapiCS.StreamType.DoP : WasapiCS.StreamType.PCM,
                        mPreference.MMThreadPriority);

                int channelMask = WasapiCS.GetTypicalChannelMask(mDeviceSetupParams.NumChannels);

                int hr = mAp.wasapi.Setup(
                        useDeviceId, WasapiCS.DeviceType.Play,
                        mDeviceSetupParams.StreamType, mDeviceSetupParams.SampleRate, mDeviceSetupParams.SampleFormat,
                        mDeviceSetupParams.NumChannels, channelMask,
                        GetMMCSSCallType(), mPreference.MMThreadPriority,
                        PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(mDeviceSetupParams.ThreadTaskType),
                        PreferenceShareModeToWasapiCSShareMode(mDeviceSetupParams.SharedOrExclusive), PreferenceDataFeedModeToWasapiCS(mDeviceSetupParams.DataFeedMode),
                        mDeviceSetupParams.LatencyMillisec, mDeviceSetupParams.ZeroFlushMillisec, mPreference.TimePeriodHundredNanosec,
                        mPreference.IsFormatSupportedCall);
                AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.ReadBegin({0} {1}kHz {2} {3}ch {4} {5} {6} latency={7}ms zeroFlush={8}ms timePeriod={9}ms mmThreadPriority={10}) channelMask=0x{11:X8} {12:X8}{13}",
                        mDeviceSetupParams.StreamType, mDeviceSetupParams.SampleRate * 0.001, mDeviceSetupParams.SampleFormat,
                        mDeviceSetupParams.NumChannels, mDeviceSetupParams.ThreadTaskType, 
                        mDeviceSetupParams.SharedOrExclusive, mDeviceSetupParams.DataFeedMode,
                        mDeviceSetupParams.LatencyMillisec, mDeviceSetupParams.ZeroFlushMillisec, 
                        mPreference.TimePeriodHundredNanosec * 0.0001, mPreference.MMThreadPriority,
                        channelMask, hr, Environment.NewLine));
                if (0 <= hr) {
                    // 成功
                    break;
                }

                // 失敗
                UnsetupDevice();
                if (i == (candidateNum - 1)) {
                    string s = string.Format(CultureInfo.InvariantCulture, "{0}: mAp.wasapi.ReadBegin({1} {2}kHz {3} {4}ch {5} {6}ms {7} {8}) {9} {10:X8} {11}{13}{13}{12}",
                            Properties.Resources.Error,
                            mDeviceSetupParams.StreamType,
                            startPcmData.SampleRate * 0.001,
                            sf.GetSampleFormatType(),
                            PcmChannelsToSetupChannels(startPcmData.NumChannels),
                            Properties.Resources.Latency,
                            latencyMillisec,
                            DfmToStr(mPreference.WasapiDataFeedMode),
                            ShareModeToStr(mPreference.WasapiSharedOrExclusive),
                            Properties.Resources.Failed,
                            hr,
                            WasapiCS.GetErrorMessage(hr),
                            Properties.Resources.SetupFailAdvice,
                            Environment.NewLine);
                    MessageBox.Show(s);
                    return false;
                }
            }

            {
                var stat = mAp.wasapi.GetSessionStatus();
                AddLogText(string.Format(CultureInfo.InvariantCulture, "Endpoint buffer size = {0} frames.{1}",
                        stat.EndpointBufferFrameNum, Environment.NewLine));

                var attr = mAp.wasapi.GetDeviceAttributes(useDeviceId);
            }

            ChangeState(State.デバイスSetup完了);
            UpdateUIStatus();
            return true;
        }

        private void LoadErrorMessageAdd(string s) {
            s = "*" + s.TrimEnd('\r', '\n') + ". ";
            mLoadErrMsg.Append(s);
        }

        private WasapiCS.MMCSSCallType GetMMCSSCallType() {
            if (!mPreference.DwmEnableMmcssCall) {
                return WasapiCS.MMCSSCallType.DoNotCall;
            }
            return mPreference.DwmEnableMmcss ? WasapiCS.MMCSSCallType.Enable : WasapiCS.MMCSSCallType.Disable;
        }

        private void ClearPlayList(PlayListClearMode mode) {
            mAp.ClearPlayList();

            mPlayListItems.Clear();
            PlayListItemInfo.SetNextRowId(1);

            FileDisappearCheck.Clear();

            mGroupIdNextAdd = 0;
            mLoadedGroupId  = -1;
            mLoadingGroupId = -1;

            GC.Collect();

            ChangeState(State.再生リストなし);

            if (mode == PlayListClearMode.ClearWithUpdateUI) {
                //mPlayListView.RefreshCollection();

                progressBar1.Value = 0;
                UpdateUIStatus();

                // 再生リスト列幅を初期値にリセットする。
                {
                    int i=0;
                    foreach (var item in dataGridPlayList.Columns) {
                        item.Width = DataGridLength.SizeToCells;
                        item.Width = mPlaylistColumnDefaults[i].Width;
                        ++i;
                    }
                }
            }
        }

        /// <summary>
        /// PcmDataのヘッダが読み込まれた時。再生リストに追加する。
        /// </summary>
        private void AddPcmDataDelegate(PcmData pcmData, PlaylistTrackInfo plti, bool readSeparatorAfter, bool readFromPpwPlaylist) {
            Dispatcher.BeginInvoke(new Action(delegate() {
                // 描画可能スレッドで実行します。

                if (0 < mAp.PcmDataListForDisp.Count()
                    && !mAp.PcmDataListForDisp.Last().IsSameFormat(pcmData)) {
                    // 1個前のファイルとデータフォーマットが異なる。
                    // Setupのやり直しになるのでファイルグループ番号を変える。
                    ++mGroupIdNextAdd;
                }

                pcmData.Id = mAp.PcmDataListForDisp.Count();
                pcmData.Ordinal = pcmData.Id;
                pcmData.GroupId = mGroupIdNextAdd;

                if (mPreference.BatchReadEndpointToEveryTrack) {
                    // 各々のトラックを個別読込する設定。
                    readSeparatorAfter = true;
                }
                if (plti != null) {
                    if ((plti.indexId == 0 && mPreference.ReplaceGapWithKokomade) || plti.readSeparatorAfter) {
                        // プレイリストのINDEX 00 == gap しかも gapのかわりに[ここまで読みこみ]を追加する の場合
                        readSeparatorAfter = true;
                    }
                }

                if (!readFromPpwPlaylist) {
                    if (pcmData.CueSheetIndex == 0 && mPreference.ReplaceGapWithKokomade) {
                        // PPWプレイリストからの読み出しではない場合で
                        // INDEX 00 == gap しかも gapのかわりに[ここまで読みこみ]を追加する の場合
                        readSeparatorAfter = true;
                    }
                }

                var pli = new PlayListItemInfo(pcmData, new FileDisappearCheck.FileDisappearedEventHandler(FileDisappearedEvent));

                if (readSeparatorAfter) {
                    pli.ReadSeparaterAfter = true;
                    ++mGroupIdNextAdd;
                }

                mAp.PcmDataListForDisp.Add(pcmData);
                mPlayListItems.Add(pli);

                pli.PropertyChanged += new PropertyChangedEventHandler(PlayListItemInfoPropertyChanged);

                if (0 < mPlayListItems.Count) {
                    ChangeState(State.再生リストあり);
                    UpdateUIStatus();
                }
            }));
        }

        /// <summary>
        /// WWSoundFileRW.ErrMsgListを受け取りロードのエラーメッセージにセットします。
        /// </summary>
        private void ProcReadFileHeaderResultMsg(List<WWPcmHeaderReader.ErrInf> ei) {
            foreach (var e in ei) {
                string s = "";

                switch (e.eType) {
                case WWPcmHeaderReader.ErrType.ReadFileFailed:
                    s = string.Format(Properties.Resources.ReadFileFailed + " {1} {2}", e.fileType, e.path, e.ex);
                    break;
                case WWPcmHeaderReader.ErrType.TooManyChannels:
                    s = string.Format(Properties.Resources.TooManyChannels + " {0}", e.path);
                    break;
                case WWPcmHeaderReader.ErrType.CoverArtImgReadFailed:
                    s = string.Format(Properties.Resources.ReadFileFailed + " cover art read failed. {1}", e.fileType, e.path);
                    break;
                case WWPcmHeaderReader.ErrType.NotSupportedBitDepth:
                    s = string.Format(Properties.Resources.NotSupportedQuantizationBitRate + " {0}", e.path);
                    break;
                case WWPcmHeaderReader.ErrType.NotSupportedFileFormat:
                    s = string.Format(Properties.Resources.NotSupportedFileFormat + " {0}", e.path);
                    break;
                }

                mLoadErrMsg.Append(s);
                mLoadErrMsg.Append("\n");
            }
        }

        /// <summary>
        /// ファイルヘッダを読んでメタ情報を抽出する。
        /// </summary>
        /// <returns>エラー発生回数。mode == OnlyConcreteFileの時、0: 成功、1: 失敗。</returns>
        int ReadFileHeader(string path, WWPcmHeaderReader.ReadHeaderMode mode, WWSoundFileRW.PlaylistTrackInfo plti) {
            int nError = 0;

            if (mode != WWPcmHeaderReader.ReadHeaderMode.OnlyConcreteFile && Path.GetExtension(path).ToUpper().Equals(".PPWPL")) {
                // PPW Playlistファイルの読み出し。
                nError = ReadPpwPlaylist(path);
            } else {
                var phr = new WWSoundFileRW.WWPcmHeaderReader(Encoding.GetEncoding(mPreference.CueEncodingCodePage),
                        mPreference.SortDropFolder, AddPcmDataDelegate);
                nError = phr.ReadFileHeader(path, mode, plti);
                ProcReadFileHeaderResultMsg(phr.ErrorMessageList());
            }
            return nError;
        }

        /// <summary>
        /// 再生リストを調べて消えたファイルを消す。
        /// </summary>
        /// <returns>リストから消したファイルの個数。</returns>
        private int RemoveDisappearedFilesFromPlayList(string path) {
            List<int> items = new List<int>();

            for (int i=0; i < mPlayListItems.Count; ++i) {
                var pli = mPlayListItems[i];
                if (!System.IO.File.Exists(pli.Path)) {
                    items.Add(i);
                }
            }

            if (items.Count == 0) {
                // 消すものはない。
                return 0;
            }

            RemovePlaylistItems(items);
            mFileDisappeared = false;

            AddLogText(Properties.Resources.SomeFilesAreDisappeared + "\n");

            return items.Count;
        }

        private bool mFileDisappeared;

        private void FileDisappearedEventProc(string path) {
            Dispatcher.BeginInvoke(new Action(delegate() {
                switch (mState) {
                case State.再生リストあり:
                    RemoveDisappearedFilesFromPlayList(path);
                    break;
                default:
                    mFileDisappeared = true;
                    break;
                }
            }));
        }

        private void FileDisappearedEvent(string path) {
            Console.WriteLine("FileDisappeared {0}", path);

            FileDisappearedEventProc(path);
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        private void MainWindowDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effects = DragDropEffects.Copy;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindowDragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (null == paths) {
                var sb = new StringBuilder(Properties.Resources.DroppedDataIsNotFile);

                var formats = e.Data.GetFormats(false);
                foreach (var format in formats) {
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "{1}    {0}", format, Environment.NewLine));
                }
                MessageBox.Show(sb.ToString());
                return;
            }

            // ドロップされたファイルを後でもよいので再生リストに追加する。
            AddFilesToPlaylist(paths, false);
        }

        private void MenuItemFileSaveCueAs_Click(object sender, RoutedEventArgs e) {
            if (mAp.PcmDataListForDisp.Count() == 0) {
                MessageBox.Show(Properties.Resources.NothingToStore);
                return;
            }

            System.Diagnostics.Debug.Assert(0 < mAp.PcmDataListForDisp.Count());
            var pcmData0 = mAp.PcmDataListForDisp.At(0);

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(pcmData0.FullPath);
            dlg.Filter = Properties.Resources.FilterCueFiles;

            var result = dlg.ShowDialog();
            if (result != true) {
                return;
            }

            var csw = new CueSheetWriter();

            csw.SetAlbumTitle(mPlayListItems[0].AlbumTitle);
            csw.SetAlbumPerformer(mPlayListItems[0].PcmData().ArtistName);

            int i = 0;
            foreach (var pli in mPlayListItems) {
                var pcmData = mAp.PcmDataListForDisp.At(i);

                CueSheetTrackInfo cst = new CueSheetTrackInfo();
                cst.title = pli.Title;
                cst.albumTitle = pli.AlbumTitle;
                cst.indexId = pcmData.CueSheetIndex;
                cst.performer = pli.ArtistName;
                cst.readSeparatorAfter = pli.ReadSeparaterAfter;
                cst.startTick = pcmData.StartTick;
                cst.endTick = pcmData.EndTick;
                cst.path = pcmData.FullPath;
                csw.AddTrackInfo(cst);
                ++i;
            }

            result = false;
            try {
                result = csw.WriteToFile(dlg.FileName);
            } catch (IOException ex) {
                Console.WriteLine("E: MenuItemFileSaveCueAs_Click {0}", ex);
            } catch (ArgumentException ex) {
                Console.WriteLine("E: MenuItemFileSaveCueAs_Click {0}", ex);
            } catch (UnauthorizedAccessException ex) {
                Console.WriteLine("E: MenuItemFileSaveCueAs_Click {0}", ex);
            }

            if (result != true) {
                MessageBox.Show(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", Properties.Resources.SaveFileFailed, dlg.FileName));
            }
        }

        private void MenuItemFileSaveAs_Click(object sender, RoutedEventArgs e) {
            if (mAp.PcmDataListForDisp.Count() == 0) {
                MessageBox.Show(Properties.Resources.NothingToStore);
                return;
            }

            System.Diagnostics.Debug.Assert(0 < mAp.PcmDataListForDisp.Count());
            var pcmData0 = mAp.PcmDataListForDisp.At(0);

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(pcmData0.FullPath);
            dlg.Filter = Properties.Resources.FilterPpwplFiles;

            var result = dlg.ShowDialog();
            if (result == true) {
                if (!SavePpwPlaylist(dlg.FileName)) {
                    MessageBox.Show(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", Properties.Resources.SaveFileFailed, dlg.FileName));
                }
            }
        }
        
        private void MenuItemFileNew_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

        /// <summary>
        /// ファイルを再生リスト画面に追加する。
        /// </summary>
        /// <param name="files">追加するファイル名のリスト。</param>
        /// <param name="bNow">true:再生リストに今すぐ追加。できなければ失敗を戻す。false:後でもよいので追加する。</param>
        /// <returns>再生リストに今すぐ追加する時失敗。</returns>
        private bool AddFilesToPlaylist(string[] files, bool bNow) {
            if (mState < State.デバイスSetup完了) {
                // 即時ファイルを追加。
                // エラーメッセージを貯めて出す。
                mLoadErrMsg = new StringBuilder();

                if (mPreference.SortDroppedFiles) {
                    files = (from s in files orderby s select s).ToArray();
                }

                for (int i = 0; i < files.Length; ++i) {
                    ReadFileHeader(files[i], WWSoundFileRW.WWPcmHeaderReader.ReadHeaderMode.ReadAll, null);
                }

                if (0 < mLoadErrMsg.Length) {
                    AddLogText(mLoadErrMsg.ToString());
                    MessageBox.Show(mLoadErrMsg.ToString(),
                            Properties.Resources.ReadFailedFiles,
                            MessageBoxButton.OK, MessageBoxImage.Information);
                }

                mLoadErrMsg = null;
            } else {
                if (bNow) {
                    return false;
                }

                // 再生中。再生停止後追加。
                mDelayedAddFiles.AddRange(files);
                AddLogText(Properties.Resources.FilesWillBeAddedAfterPlaybackStops + "\n");
            }

            return true;
        }

        private void MenuItemFileOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = Properties.Resources.FilterSupportedFiles;
            dlg.Multiselect = true;

            if (0 <= mPreference.OpenFileDialogFilterIndex) {
                dlg.FilterIndex = mPreference.OpenFileDialogFilterIndex;
            }

            var result = dlg.ShowDialog();
            if (result == true) {
                AddFilesToPlaylist(dlg.FileNames, false);

                // 最後に選択されていたフィルターをデフォルトとする
                mPreference.OpenFileDialogFilterIndex = dlg.FilterIndex;
            }

        }

        private void MenuItemHelpAbout_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(
                string.Format(CultureInfo.InvariantCulture, "PlayPcmWin {0} {1}{3}{3}{2}",
                        Properties.Resources.Version, AssemblyVersion, Properties.Resources.LicenseText, Environment.NewLine));
        }

        private void MenuItemHelpWeb_Click(object sender, RoutedEventArgs e) {
            try {
                System.Diagnostics.Process.Start("https://sourceforge.net/projects/playpcmwin/");
            } catch (System.ComponentModel.Win32Exception) {
            }
        }

        private static string DfmToStr(WasapiDataFeedModeType dfm) {
            switch (dfm) {
            case WasapiDataFeedModeType.EventDriven:
                return Properties.Resources.EventDriven;
            case WasapiDataFeedModeType.TimerDriven:
                return Properties.Resources.TimerDriven;
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private static string ShareModeToStr(WasapiSharedOrExclusiveType t) {
            switch (t) {
            case WasapiSharedOrExclusiveType.Exclusive:
                return "WASAPI " + Properties.Resources.Exclusive;
            case WasapiSharedOrExclusiveType.Shared:
                return "WASAPI " + Properties.Resources.Shared;
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        class ReadFileRunWorkerCompletedArgs {
            public string message;
            public int hr;
            public List<ReadFileResult> individualResultList = new List<ReadFileResult>();

            public ReadFileRunWorkerCompletedArgs Update(string msg, int resultCode) {
                message = msg;
                hr = resultCode;
                return this;
            }
        }

#if false
        private class ReadPcmTask : IDisposable {
            MainWindow mw;
            BackgroundWorker bw;
            PcmDataLib.PcmData pd;
            public long readStartFrame;
            public long readFrames;
            public long writeOffsFrame;
            public ManualResetEvent doneEvent;
            public bool result;
            public string message;

            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    doneEvent.Close();
                }
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public ReadPcmTask(MainWindow mw, BackgroundWorker bw, PcmDataLib.PcmData pd,
                    long readStartFrame, long readFrames, long writeOffsFrame) {
                this.mw = mw;
                this.bw = bw;

                // PcmDataのSampleArrayメンバを各スレッドが物置のように使うので実体をコピーする。
                this.pd = new PcmData();
                this.pd.CopyFrom(pd);

                this.readStartFrame = readStartFrame;
                this.readFrames     = readFrames;
                this.writeOffsFrame = writeOffsFrame;

                this.message = string.Empty;

                doneEvent = new ManualResetEvent(false);
                result = true;
            }

            public void ThreadPoolCallback(Object threadContext) {
                int threadIndex = (int)threadContext;
                var ri = mw.ReadOnePcmFileFragment(bw, pd, readStartFrame, readFrames, writeOffsFrame);
                if (ri.HasMessage) {
                    message += ri.ToString(pd.FileName);
                }
                if (!ri.IsSucceeded) {
                    result = false;
                    message = ri.ToString(pd.FileName);
                }

                doneEvent.Set();
            }

            /// <summary>
            /// このインスタンスの使用を終了する。再利用はできない。
            /// </summary>
            public void End() {
                mw = null;
                bw = null;
                pd = null;
                readStartFrame = 0;
                readFrames = 0;
                writeOffsFrame = 0;
                doneEvent = null;
                result = true;
            }
        };

        /// <summary>
        /// 分割読み込みのそれぞれのスレッドの読み込み開始位置と読み込みバイト数を計算する。
        /// </summary>
        private List<ReadPcmTask> SetupReadPcmTasks(
                BackgroundWorker bw,
                PcmDataLib.PcmData pd,
                long startFrame,
                long endFrame,
                int fragmentCount) {
            var result = new List<ReadPcmTask>();

            long readFrames = (endFrame - startFrame) / fragmentCount;
            // すくなくとも6Mフレームずつ読む。その結果fragmentCountよりも少ない場合がある。
            if (readFrames < WWSoundFileReader.TYPICAL_READ_FRAMES) {
                readFrames = WWSoundFileReader.TYPICAL_READ_FRAMES;
            }

            long readStartFrame = startFrame;
            long writeOffsFrame = 0;
            do {
                if (endFrame < readStartFrame + readFrames) {
                    readFrames = endFrame - readStartFrame;
                }
                var rri = new ReadPcmTask(this, bw, pd, readStartFrame, readFrames, writeOffsFrame);
                result.Add(rri);
                readStartFrame += readFrames;
                writeOffsFrame += readFrames;
            } while (readStartFrame < endFrame);
            return result;
        }
#endif

        /// <summary>
        /// pdの1トラックぶんのPCMを読み出し、再生できる形式に変換しwasapiネイティブ層にセットします。
        /// バックグラウンド読み出しスレッドのReadFileDoWorkから呼び出されます。
        /// </summary>
        private bool ReadOnePcmFile(BackgroundWorker bw, PcmDataLib.PcmData pd,
                long startFrame, long endFrame,
                ref ReadFileRunWorkerCompletedArgs r) {
            if (endFrame < 0) {
                // 負：ファイルを最後まで読むという意味。

                if (0 < pd.NumFrames) {
                    // ファイルの最後が既知。
                    endFrame = pd.NumFrames;
                } else {
                    // endFrameが不明。調べる。
                    // rpi.ReadFramesも確定する。

                    using (var sr = new WWSoundFileRW.WWSoundFileReader()) {
                        // desiredFmtはデバイスが受け付けるPCM形式。
                        // 現状、読み出し処理でdesiredFmtにならないことが多い。
                        // 後続の処理で修正します。
                        var desiredFmt = DeviceSetupParamsToSoundFilePcmFmt(
                                mDeviceSetupParams);
                        int ercd = sr.StreamBegin(
                                new PcmDataLib.PcmData(pd),
                                pd.FullPath, 0, 0,
                                desiredFmt, IntPtr.Zero);
                        sr.StreamEnd();
                        if (ercd < 0) {
                            r.hr = ercd;
                            r.message = string.Format(CultureInfo.InvariantCulture,
                                    "{0}! {1}{5}{2}{5}{3}: {4} (0x{4:X8})",
                                    Properties.Resources.ReadError, pd.FullPath,
                                    FlacDecodeIF.ErrorCodeToStr(ercd),
                                    Properties.Resources.ErrorCode, ercd,
                                    Environment.NewLine);
                            Console.WriteLine("D: ReadFileSingleDoWork() failed");
                            return false;
                        }
                        if (sr.NumFrames < endFrame) {
                            endFrame = sr.NumFrames;
                        }
                    }
                }
            }

            // endFrame値が確定。読み出すフレーム数をPcmDataにセット。
            long wantFramesTotal = endFrame - startFrame;
            pd.SetNumFrames(wantFramesTotal);
            mReadProgressInf.FileReadStart(pd.Id, startFrame, endFrame);
            ReadFileReportProgress(0);

            {
                // このトラックのWasapi PCMデータ領域を確保する。
                long allocBytes = wantFramesTotal * mDeviceSetupParams.UseBytesPerFrame;
                if (!mAp.wasapi.AddPlayPcmDataAllocateMemory(pd.Id, allocBytes)) {
                    //ClearPlayList(PlayListClearMode.ClearWithoutUpdateUI); //< メモリを空ける：効果があるか怪しい
                    r.message = string.Format(CultureInfo.InvariantCulture, Properties.Resources.MemoryExhausted);
                    Console.WriteLine("D: ReadFileSingleDoWork() lowmemory");
                    return false;
                }
            }

            GC.Collect();

            bool result = true;
#if true
            {
#else
            if (mPreference.ParallelRead
                    && WWSoundFileReader.IsTheFormatParallelizable(WWSoundFileReader.GuessFileFormatFromFilePath(pd.FullPath))
                    && ((mPreference.BpsConvNoiseShaping == NoiseShapingType.None)
                         || !mPcmUtil.IsNoiseShapingOrDitherCapable(pd, mDeviceSetupParams.SampleFormat))) {
                // ファイルのstartFrameからendFrameまでを読みだす。(並列化)
                int fragmentCount = Environment.ProcessorCount;
                var rri = SetupReadPcmTasks(bw, pd, startFrame, endFrame, fragmentCount);
                var doneEventArray = new ManualResetEvent[rri.Count];
                for (int i=0; i < rri.Count; ++i) {
                    doneEventArray[i] = rri[i].doneEvent;
                }

                for (int i=0; i < rri.Count; ++i) {
                    ThreadPool.QueueUserWorkItem(rri[i].ThreadPoolCallback, i);
                }
                WaitHandle.WaitAll(doneEventArray);

                for (int i=0; i < rri.Count; ++i) {
                    if (!rri[i].result) {
                        r.message += rri[i].message + Environment.NewLine;
                        result = false;
                    }
                    rri[i].End();
                }
                rri.Clear();
                doneEventArray = null;
            } else {
#endif
                // 書き込み先メモリ領域の先頭。
                var writeBeginPtr = mAp.wasapi.GetPlayPcmDataPtr(pd.Id, 0);

                // ファイルのstartFrameからendFrameまでを読み出す。(1スレッド)
                var ri = ReadOnePcmFileFragment(bw, pd, startFrame, wantFramesTotal, 0, writeBeginPtr);
                if (ri.HasMessage) {
                    r.individualResultList.Add(ri);
                }
                result = ri.IsSucceeded;
                if (!ri.IsSucceeded) {
                    r.message += ri.ToString(pd.FileName);
                }
            }

            return result;
        }

        private ReadFileResult ReadOnePcmFileFragment(
                BackgroundWorker bw,
                PcmDataLib.PcmData pd, long readStartFrame,
                long wantFramesTotal, long writeOffsFrame,
                IntPtr writeBeginPtr) {
            var lowMemoryFailed = new ReadFileResultFailed(pd.Id, "Low memory");
            var ri = new ReadFileResultSuccess(pd.Id) as ReadFileResult;

            var pdCopy = new PcmDataLib.PcmData().CopyFrom(pd);

            using (var sr = new WWSoundFileReader()) {
                int ercd = sr.StreamBegin(pdCopy,
                        pdCopy.FullPath, readStartFrame, wantFramesTotal,
                        DeviceSetupParamsToSoundFilePcmFmt(mDeviceSetupParams),
                        writeBeginPtr);
                if (ercd < 0) {
                    Console.WriteLine("D: ReadOnePcmFileFragment() StreamBegin failed");
                    return new ReadFileResultFailed(pd.Id, FlacDecodeIF.ErrorCodeToStr(ercd));
                }

                long frameCount = 0;
                do {
                    // 読み出したいフレーム数wantFrames。
                    int wantFrames = WWSoundFileReader.TYPICAL_READ_FRAMES;
                    if (wantFramesTotal < frameCount + wantFrames) {
                        wantFrames = (int)(wantFramesTotal - frameCount);
                    }

                    int readFrames = 0;
                    byte[] part = null;
                    int readResult = sr.StreamReadOne(wantFrames, out part, out readFrames);
                    if (part == null || part.Count() == 0) {
                        if (readResult < 0) {
                            // 読み出しエラー。
                            sr.StreamEnd();
                            return new ReadFileResultFailed(
                                    pdCopy.Id, WWFlacRWCS.FlacRW.ErrorCodeToStr(readResult));
                        } else if (readResult == WWSoundFileReader.ERROR_HANDLE_EOF) {
                            // EOFに達した。
                            mAp.wasapi.TrimPlayPcmDataFrameCount(pdCopy.Id, frameCount);
                            break;
                        }
                    }

                    if (part != null && 0 < part.Length) {
                        //Console.WriteLine("part size = {0}", part.Length);

                        pdCopy.SetSampleLargeArray(new LargeArray<byte>(part));
                        part = null;
                        //Console.WriteLine("pd.SetSampleLargeArray {0}", pd.GetSampleLargeArray().LongLength);

                        // 必要に応じてpartの量子化ビット数の変更処理を行い、pdAfterに新しく確保したPCMデータ配列をセット。

                        var bca = new PcmFormatConverter.BitsPerSampleConvArgs(mPreference.BpsConvNoiseShaping);
                        if (mPreference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Exclusive) {
                            pdCopy = mPcmUtil.BitsPerSampleConvWhenNecessary(
                                    pdCopy, mDeviceSetupParams.SampleFormat, bca);
                        }

                        if (pdCopy.GetSampleLargeArray() == null ||
                                0 == pdCopy.GetSampleLargeArray().LongLength) {
                            // サンプルが存在しないのでWasapiにAddしない。
                            break;
                        }

                        //Console.WriteLine("pdCopy.SampleLargeArray {0}", pdCopy.GetSampleLargeArray().LongLength);

                        if (pdCopy.NumChannels == 1) {
                            // モノラル1ch→ステレオ2ch変換。
                            pdCopy = pdCopy.MonoToStereo();
                        }
                        if (mPreference.AddSilentForEvenChannel) {
                            // 偶数チャンネルにするために無音を追加。
                            pdCopy = pdCopy.AddSilentForEvenChannel();
                        }

                        pdCopy = pdCopy.ConvertChannelCount(mDeviceSetupParams.NumChannels);

                        /*
                        // これは駄目だった！もっと手前でDoPマーカーの判定をしてDoPとPCMが混在しないようにする必要がある。
                        // PCMのとき、DoPマーカーが付いていたらDSDフラグを立てる。
                        if (mDeviceSetupParams.SharedOrExclusive == WasapiSharedOrExclusiveType.Exclusive &&
                                pdCopy.ScanDopMarkerAndUpdate()) {
                            mAp.wasapi.UpdateStreamType(pd.Id, WasapiCS.StreamType.DoP);
                        }
                        */

                        //Console.WriteLine("pdCopy.ConvertChannelCount({0}) SampleLargeArray {1}", mDeviceSetupParams.NumChannels, pdCopy.GetSampleLargeArray().LongLength);

                        long posBytes = (writeOffsFrame + frameCount) * pdCopy.BitsPerFrame / 8;

                        bool result = false;
                        lock (pd) {
                            //Console.WriteLine("mAp.wasapi.AddPlayPcmDataSetPcmFragment({0}, {1} {2})", pd.Id, posBytes, pdCopy.GetSampleLargeArray().ToArray().Length);

                            // PCMデータをWasapiネイティブ層にセット。
                            result = mAp.wasapi.AddPlayPcmDataSetPcmFragment(
                                    pdCopy.Id, posBytes, pdCopy.GetSampleLargeArray().ToArray());
                        }
                        System.Diagnostics.Debug.Assert(result);

                        pdCopy.ForgetDataPart();
                    }

                    // frameCountを進める
                    frameCount += readFrames;

                    ReadFileReportProgress(readFrames);

                    if (bw.CancellationPending) {
                        sr.StreamAbort();
                        return new ReadFileResultFailed(pdCopy.Id, string.Empty);
                    }
                } while (frameCount < wantFramesTotal);

                ercd = sr.StreamEnd();

                if (ercd < 0) {
                    return new ReadFileResultFailed(
                            pd.Id, string.Format(CultureInfo.InvariantCulture,
                                "{0}: {1}", FlacDecodeIF.ErrorCodeToStr(ercd), pd.FullPath));
                }

                if (sr.MD5SumOfPcm != null) {
                    ri = new ReadFileResultMD5Sum(pd.Id, sr.MD5SumOfPcm, sr.MD5SumInMetadata);
                }
            }

            return ri;
        }

        private void ReadFileWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            string s = e.UserState as string;
            if (s != null && 0 < s.Length) {
                AddLogText(s);
            }
            progressBar1.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// リピート設定。
        /// </summary>
        private void UpdatePlayRepeat() {
            bool repeat = false;
            // 1曲リピートか、または(全曲リピート再生で、GroupIdが0しかない)場合、WASAPI再生スレッドのリピート設定が可能。
            var playMode = (ComboBoxPlayModeType)comboBoxPlayMode.SelectedIndex;
            if (playMode == ComboBoxPlayModeType.OneTrackRepeat
                    || (playMode == ComboBoxPlayModeType.AllTracksRepeat
                    && 0 == mAp.PcmDataListForPlay.CountPcmDataOnPlayGroup(1))) {
                repeat = true;
            }
            mAp.wasapi.SetPlayRepeat(repeat);
        }

        /// <summary>
        /// バックグラウンドファイル読み込みが完了した。
        /// </summary>
        private void ReadFileRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            if (args.Cancelled) {
                // キャンセル時は何もしないで直ちに終わる。
                return;
            }

            var r = args.Result as ReadFileRunWorkerCompletedArgs;

            AddLogText(r.message);

            if (r.hr < 0) {
                // ファイル読み込みが失敗した。
                Console.WriteLine("ReadFileRunWorkerCompleted with error");
                MessageBox.Show(r.message);
                mTaskAfterStop.Set(NextTaskType.None);
            }

            if (0 < r.individualResultList.Count) {
                foreach (var fileResult in r.individualResultList) {
                    AddLogText(fileResult.ToString(mAp.PcmDataListForPlay.FindById(fileResult.PcmDataId).FileName));
                }
            }

            // WasapiCSのリピート設定。
            UpdatePlayRepeat();

            switch (mTaskAfterStop.Type) {
            case NextTaskType.PlaySpecifiedGroup:
            case NextTaskType.PlayPauseSpecifiedGroup:
                // ファイル読み込み完了後、再生を開始する。
                // 再生するファイルは、タスクで指定されたファイル。
                // このwavDataIdは、再生開始ボタンが押された時点で選択されていたファイル。
                int wavDataId = mTaskAfterStop.PcmDataId;

                if (null != mPliUpdatedByUserWhileLoading) {
                    // (Issue 6)再生リストで選択されている曲が違う曲の場合、
                    // 選択されている曲を再生する。
                    wavDataId = mPliUpdatedByUserWhileLoading.PcmData().Id;

                    // 使い終わったのでクリアーする。
                    mPliUpdatedByUserWhileLoading = null;
                }

                ReadStartPlayByWavDataId(wavDataId);
                break;
            default:
                // 再生断念。
                ChangeState(State.再生リストあり);
                UpdateUIStatus();
                break;
            }
        }

        /// <summary>
        /// デバイスを選択。
        /// 既に使用中の場合、空振りする。
        /// 別のデバイスを使用中の場合、そのデバイスを未使用にして、新しいデバイスを使用状態にする。
        /// </summary>
        private bool UseDevice()
        {
            // 通常使用するデバイスとする。
            var di = listBoxDevices.SelectedItem as DeviceAttributes;
            mUseDevice = di;
            AddLogText(string.Format(CultureInfo.InvariantCulture, "Device name: {0}{1}", di.Name, Environment.NewLine));
            mPreference.PreferredDeviceName     = di.Name;
            mPreference.PreferredDeviceIdString = di.DeviceIdStr;
            return true;
        }

        /// <summary>
        /// loadGroupIdのファイル読み込みを開始する。
        /// 読み込みが完了したらReadFileRunWorkerCompletedが呼ばれる。
        /// </summary>
        private void StartReadFiles(int loadGroupId) {
            //Console.WriteLine("StartReadFiles({0})", loadGroupId);

            progressBar1.Visibility = System.Windows.Visibility.Visible;
            progressBar1.Value = 0;

            mLoadingGroupId = loadGroupId;

            ReadFileWorkerRunAsync(loadGroupId);
        }

        private void ButtonPlayClicked() {
            var di = listBoxDevices.SelectedItem as DeviceAttributes;
            if (!UseDevice()) {
                return;
            }

            if (IsPlayModeShuffle()) {
                // シャッフル再生する
                mAp.CreateShuffledPlayList();
                ReadStartPlayByWavDataId(mAp.PcmDataListForPlay.At(0).Id);
                return;
            }

            // 選択されている曲から順番に再生する。
            // 再生する曲のwavDataIdをdataGridの選択セルから取得する
            int wavDataId = 0;
            var selectedCells = dataGridPlayList.SelectedCells;
            if (0 < selectedCells.Count) {
                var cell = selectedCells[0];
                System.Diagnostics.Debug.Assert(cell != null);
                var pli = cell.Item as PlayListItemInfo;
                System.Diagnostics.Debug.Assert(pli != null);
                var pcmData = pli.PcmData();

                if (null != pcmData) {
                    wavDataId = pcmData.Id;
                } else {
                    // ココまで読んだ的な行は、pcmDataを持っていない
                }
            }

            if (IsPlayModeOneTrack()) {
                // 1曲再生。1曲だけ読み込んで再生する。
                mAp.CreateOneTrackPlayList(wavDataId);
                ReadStartPlayByWavDataId(wavDataId);
                return;
            }

            // 全曲再生
            mAp.CreateAllTracksPlayList();
            ReadStartPlayByWavDataId(wavDataId);
        }

        private void ButtonPauseClicked() {
            int hr = 0;

            switch (mState) {
            case State.再生中:
                hr = mAp.wasapi.Pause();
                AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.Pause() {0:X8}{1}", hr, Environment.NewLine));
                if (0 <= hr) {
                    ChangeState(State.再生一時停止中);
                    UpdateUIStatus();
                    PPWServerSendCommand(new RemoteCommand(
                        RemoteCommandType.Pause, dataGridPlayList.SelectedIndex));
                } else {
                    // Pause失敗＝すでに再生していない または再生一時停止ができない状況。ここで状態遷移する必要はない。
                }
                break;
            case State.再生一時停止中:
                hr = mAp.wasapi.Unpause();
                AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.Unpause() {0:X8}{1}", hr, Environment.NewLine));
                if (0 <= hr) {
                    ChangeState(State.再生中);
                    UpdateUIStatus();
                } else {
                    // Unpause失敗＝すでに再生していない。ここで状態遷移する必要はない。
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e) {
            ButtonPlayClicked();
        }

        private void buttonPause_Click(object sender, RoutedEventArgs e) {
            ButtonPauseClicked();
        }

        /// <summary>
        /// wavDataIdのGroupがロードされていたら直ちに再生開始する。
        /// 読み込まれていない場合、直ちに再生を開始できないので、ロードしてから再生する。
        /// </summary>
        private bool ReadStartPlayByWavDataId(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            NextTaskType nextTask = NextTaskType.PlaySpecifiedGroup;
            if (mTaskAfterStop.Type != NextTaskType.None) {
                nextTask = mTaskAfterStop.Type;
            }

            var pcmData = mAp.PcmDataListForPlay.FindById(wavDataId);
            if (null == pcmData) {
                // 1曲再生モードの時。再生リストを作りなおす。
                mAp.CreateOneTrackPlayList(wavDataId);
                mLoadedGroupId = -1;
                pcmData = mAp.PcmDataListForPlay.FindById(wavDataId);
            }

            if (pcmData.GroupId != mLoadedGroupId) {
                // mLoadedGroupIdと、wr.GroupIdが異なる場合。
                // 再生するためには、ロードする必要がある。
                UnsetupDevice();

                if (!SetupDevice(pcmData.GroupId)) {
                    //dataGridPlayList.SelectedIndex = 0;
                    ChangeState(State.ファイル読み込み完了);

                    DeviceDeselect();
                    UpdateDeviceList();
                    return false;
                }

                mTaskAfterStop.Set(nextTask, pcmData.GroupId, pcmData.Id);
                StartReadPlayGroupOnTask();
                return true;
            }

            // wavDataIdのグループがmLoadedGroupIdである。ロードされている。
            // 連続再生フラグの設定と、現在のグループが最後のグループかどうかによって
            // mLoadedGroupIdの再生が自然に完了したら、行うタスクを決定する。
            UpdateNextTask();

            if (!SetupDevice(pcmData.GroupId) ||
                    !StartPlay(wavDataId)) {
                //dataGridPlayList.SelectedIndex = 0;
                ChangeState(State.ファイル読み込み完了);

                DeviceDeselect();
                UpdateDeviceList();
                return false;
            }

            if (nextTask == NextTaskType.PlayPauseSpecifiedGroup) {
                ButtonPauseClicked();
            }
            return true;
        }

        /// <summary>
        /// 現在のグループの最後のファイルの再生が終わった後に行うタスクを判定し、
        /// mTaskにセットする。
        /// </summary>
        private void UpdateNextTask() {
            if (0 == mAp.PcmDataListForPlay.CountPcmDataOnPlayGroup(1)) {
                // ファイルグループが1個しかない場合、
                // wasapiUserの中で自発的にループ再生する。
                // ファイルの再生が終わった=停止。
                mTaskAfterStop.Set(NextTaskType.None);
                return;
            }

            // 順当に行ったら次に再生するグループ番号は(mLoadedGroupId+1)。
            // ①(mLoadedGroupId+1)の再生グループが存在する場合
            //     (mLoadedGroupId+1)の再生グループを再生開始する。
            // ②(mLoadedGroupId+1)の再生グループが存在しない場合
            //     ②-①連続再生(checkBoxContinuous.IsChecked==true)の場合
            //         GroupId==0、pcmDataId=0を再生開始する。
            //     ②-②連続再生ではない場合
            //         停止する。先頭の曲を選択状態にする。
            int nextGroupId = mLoadedGroupId + 1;

            if (0 < mAp.PcmDataListForPlay.CountPcmDataOnPlayGroup(nextGroupId)) {
                mTaskAfterStop.Set(NextTaskType.PlaySpecifiedGroup, nextGroupId, mAp.PcmDataListForPlay.GetFirstPcmDataIdOnGroup(nextGroupId));
                return;
            }

            if (IsPlayModeRepeat()) {
                mTaskAfterStop.Set(NextTaskType.PlaySpecifiedGroup, 0, 0);
                return;
            }

            mTaskAfterStop.Set(NextTaskType.None);
        }

        /// <summary>
        /// ただちに再生を開始する。
        /// wavDataIdのGroupが、ロードされている必要がある。
        /// </summary>
        /// <returns>false: 再生開始できなかった。</returns>
        private bool StartPlay(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);
            var playPcmData = mAp.PcmDataListForPlay.FindById(wavDataId);
            if (playPcmData.GroupId != mLoadedGroupId) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            ChangeState(State.再生中);
            UpdateUIStatus();

            mSw.Reset();
            mSw.Start();

            // 再生バックグラウンドタスク開始。PlayDoWorkが実行される。
            // 再生バックグラウンドタスクを止めるには、Stop()を呼ぶ。
            // 再生バックグラウンドタスクが止まったらPlayRunWorkerCompletedが呼ばれる。
            int hr = mAp.StartPlayback(wavDataId, new AudioPlayer.PlayEventCallback(PlayEventHandler));
            {
                var stat = mAp.wasapi.GetWorkerThreadSetupResult();

                if (mPreference.RenderThreadTaskType != RenderThreadTaskType.None) {
                    AddLogText(string.Format(CultureInfo.InvariantCulture, "AvSetMMThreadCharacteristics({0}) r={1:X8}{2}",
                        mPreference.RenderThreadTaskType, stat.AvSetMmThreadCharacteristicsResult, Environment.NewLine));
                    

                    if (mPreference.MMThreadPriority != WasapiCS.MMThreadPriorityType.None) {
                        AddLogText(string.Format(CultureInfo.InvariantCulture, "AvSetMMThreadPriority({0}) r={1:X8}{2}",
                            mPreference.MMThreadPriority, stat.AvSetMmThreadPriorityResult, Environment.NewLine));
                    }
                }

                if (mPreference.DwmEnableMmcssCall) {
                    AddLogText(string.Format(CultureInfo.InvariantCulture, "DwmEnableMMCSS({0}) r={1:X8}{2}",
                        mPreference.DwmEnableMmcss, stat.DwmEnableMMCSSResult, Environment.NewLine));
                }
            }

            AddLogText(string.Format(CultureInfo.InvariantCulture,
                    "mAp.wasapi.StartPlayback({0}) {1:X8}{2}", wavDataId, hr, Environment.NewLine));
            if (hr < 0) {
                mTaskAfterStop.Set(NextTaskType.None);

                MessageBox.Show(string.Format(CultureInfo.InvariantCulture,
                        Properties.Resources.PlayStartFailed + "！{0:X8}  {1}", hr, WasapiCS.GetErrorMessage(hr)));
                mAp.PlayStop(false);
                return false;
            }

            return true;
        }

        private void PlayEventHandler(AudioPlayer.PlayEvent ev) {
            switch (ev.eventType) {
            case AudioPlayer.PlayEventType.ProgressChanged:
                PlayProgressChanged(ev);
                break;
            case AudioPlayer.PlayEventType.Finished:
            case AudioPlayer.PlayEventType.Canceled:
                PlayRunWorkerCompleted(ev);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        long mLastSliderPositionUpdateTime = 0;

        /// <summary>
        /// 再生の進行状況をUIに反映する。
        /// </summary>
        private void PlayProgressChanged(AudioPlayer.PlayEvent ev) {
            var bw = ev.bw;

            if (null == mAp.wasapi) {
                return;
            }

            if (bw.CancellationPending) {
                // ワーカースレッドがキャンセルされているので、何もしない。
                return;
            }

            // 再生中PCMデータ(または一時停止再開時再生予定PCMデータ等)の再生位置情報を画面に表示する。
            WasapiCS.PcmDataUsageType usageType = WasapiCS.PcmDataUsageType.NowPlaying;
            int pcmDataId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            if (pcmDataId < 0) {
                pcmDataId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.PauseResumeToPlay);
                usageType = WasapiCS.PcmDataUsageType.PauseResumeToPlay;
            }
            if (pcmDataId < 0) {
                pcmDataId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.SpliceNext);
                usageType = WasapiCS.PcmDataUsageType.SpliceNext;
            }

            string playingTimeString = string.Empty;
            if (pcmDataId < 0) {
                playingTimeString = PLAYING_TIME_UNKNOWN;
            } else {
                if (dataGridPlayList.SelectedIndex != GetPlayListIndexOfPcmDataId(pcmDataId)) {
                    dataGridPlayList.SelectedIndex = GetPlayListIndexOfPcmDataId(pcmDataId);
                    dataGridPlayList.ScrollIntoView(dataGridPlayList.SelectedItem);
                }

                PcmDataLib.PcmData pcmData = mAp.PcmDataListForPlay.FindById(pcmDataId);

                var stat    = mAp.wasapi.GetSessionStatus();
                var playPos = mAp.wasapi.GetPlayCursorPosition(usageType);

                long now = DateTime.Now.Ticks;
                if (now - mLastSliderPositionUpdateTime > SLIDER_UPDATE_TICKS) {
                    // スライダー位置の更新。0.5秒に1回
                    slider1.Maximum = playPos.TotalFrameNum;
                    if (!mSliderSliding || playPos.TotalFrameNum <= slider1.Value) {
                        slider1.Value = playPos.PosFrame;
                    }

                    int posMs = (int)(playPos.PosFrame * 1000 / pcmData.SampleRate);
                    var cmd = new RemoteCommand(RemoteCommandType.PlayPositionUpdate, dataGridPlayList.SelectedIndex, posMs);
                    // 大体の再生状態を送る。
                    switch (mState) {
                    case State.再生中:
                        cmd.state = RemoteCommand.PlaybackState.Playing; break;
                    case State.再生一時停止中:
                        cmd.state = RemoteCommand.PlaybackState.Paused; break;
                    default:
                        cmd.state = RemoteCommand.PlaybackState.Stopped; break;
                    }
                    //Console.WriteLine("playposition track={0} state={1} pos={2}", cmd.trackIdx, cmd.state, cmd.positionMillisec);
                    PPWServerSendCommand(cmd);

                    mLastSliderPositionUpdateTime = now;
                }

                if (pcmData.TrackId != 0) {
                    // CUEシートなのでトラック番号を表示する。
                    if (pcmData.CueSheetIndex == 0) {
                        // INDEX 00区間はマイナス表示。
                        // INDEX 00区間の曲長さ表示は次の曲の長さを表示する。
                        long nextSampleRate = stat.DeviceSampleRate;
                        long nextTotalFrameNum = playPos.TotalFrameNum;
                        var nextPcmData = mAp.PcmDataListForPlay.FindById(pcmDataId+1);
                        if (nextPcmData != null) {
                            nextTotalFrameNum = nextPcmData.NumFrames;
                            nextSampleRate = nextPcmData.SampleRate;
                        } else {
                            // シャッフル再生時に起こるｗｗｗｗ
                        }

                        playingTimeString = string.Format(CultureInfo.InvariantCulture, "Tr.{0:D2} -{1} / {2}",
                                pcmData.TrackId,
                                Util.SecondsToMSString((int)((playPos.TotalFrameNum + stat.DeviceSampleRate - playPos.PosFrame) / stat.DeviceSampleRate)),
                                Util.SecondsToMSString((int)(nextTotalFrameNum / nextSampleRate)));
                    } else {
                        playingTimeString = string.Format(CultureInfo.InvariantCulture, "Tr.{0:D2}  {1} / {2}",
                                pcmData.TrackId,
                                Util.SecondsToMSString((int)(playPos.PosFrame / stat.DeviceSampleRate)),
                                Util.SecondsToMSString((int)(playPos.TotalFrameNum / stat.DeviceSampleRate)));
                    }
                } else {
                    playingTimeString = string.Format(CultureInfo.InvariantCulture, "{0} / {1}",
                            Util.SecondsToMSString((int)(playPos.PosFrame / stat.DeviceSampleRate)),
                            Util.SecondsToMSString((int)(playPos.TotalFrameNum / stat.DeviceSampleRate)));
                }
            }

            // 再生時間表示の再描画をできるだけ抑制する。負荷が減る効果がある
            if (playingTimeString != string.Empty && 0 != string.Compare((string)labelPlayingTime.Content, playingTimeString)) {
                labelPlayingTime.Content = playingTimeString;
            } else {
                //System.Console.WriteLine("time disp update skipped");
            }
        }

        /// <summary>
        /// mTaskに指定されているグループをロードし、ロード完了したら指定ファイルを再生開始する。
        /// ファイル読み込み完了状態にいるときに呼ぶ。
        /// </summary>
        private void StartReadPlayGroupOnTask() {
            mLoadedGroupId = -1;

            switch (mTaskAfterStop.Type) {
            case NextTaskType.PlaySpecifiedGroup:
            case NextTaskType.PlayPauseSpecifiedGroup:
                break;
            default:
                // 想定されていない状況
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            // 再生状態→再生グループ切り替え中状態に遷移。
            ChangeState(State.再生グループ読み込み中);
            UpdateUIStatus();

            StartReadFiles(mTaskAfterStop.GroupId);
        }

        private bool PerformTaskAfterStop() {
            // 再生終了後に行うタスクがある場合、ここで実行する。
            switch (mTaskAfterStop.Type) {
            case NextTaskType.PlaySpecifiedGroup:
            case NextTaskType.PlayPauseSpecifiedGroup:
                UnsetupDevice();

                if (IsPlayModeOneTrack()) {
                    // 1曲再生モードの時、再生リストを作りなおす。
                    mAp.CreateOneTrackPlayList(mTaskAfterStop.PcmDataId);
                }

                if (null == mPliUpdatedByUserWhileLoading) {
                    // 次に再生する曲を選択状態にする。
                    dataGridPlayList.SelectedIndex =
                        GetPlayListIndexOfPcmDataId(mTaskAfterStop.PcmDataId);

                    UpdateUIStatus();
                }

                if (SetupDevice(mTaskAfterStop.GroupId)) {
                    StartReadPlayGroupOnTask();
                    return true;
                }

                // デバイスの設定を試みたら、失敗した。
                // FALL_THROUGHする。
                break;
            default:
                break;
            }

            return false;
        }

        /// <summary>
        /// 再生終了後タスクを実行する。
        /// </summary>
        private void PerformPlayCompletedTask() {
            if (mFileDisappeared && 0 < RemoveDisappearedFilesFromPlayList("")) {
                // 1個以上ファイルが消えた。再生終了後タスクを実行せずに停止する。
                
                mTaskAfterStop.Type = NextTaskType.None;
            } else {
                bool rv = PerformTaskAfterStop();

                if (rv) {
                    // 次の再生が始まる。
                    return;
                }
            }

            // 再生終了後に行うタスクがない。停止する。
            // 再生状態→ファイル読み込み完了状態。

            // 先頭の曲を選択状態にする。
            //dataGridPlayList.SelectedIndex = 0;
            
            ChangeState(State.ファイル読み込み完了);

            DeviceDeselect();

            if (mDeviceListUpdatePending) {
                UpdateDeviceList();
                mDeviceListUpdatePending = false;
            }

            ProcAddFilesMsg();

            GC.Collect();
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(AudioPlayer.PlayEvent ev) {
            mSw.Stop();

            if (ev.eventType == AudioPlayer.PlayEventType.Canceled) {
                // 再生中に×ボタンを押すとここに来る。
                // 再生中に次の曲ボタンを押した場合もここに来る。
                Console.WriteLine("PlayRunWorkerCompleted with cancel");
            }

            if (ev.ercd < 0) {
                AddLogText(string.Format(CultureInfo.InvariantCulture, "Error: play stopped with error {0:X8} {1}{2}",
                    ev.ercd, WasapiCS.GetErrorMessage(ev.ercd), Environment.NewLine));
                return;
            }

            AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.PlayCompletedElapsedTimeIs + " {0}{1}", mSw.Elapsed, Environment.NewLine));
            PerformPlayCompletedTask();
        }

        private void ButtonStopClicked() {
            if (mState == State.再生停止開始) {
                return;
            }

            ChangeState(State.再生停止開始);
            UpdateUIStatus();

            // 停止ボタンで停止した場合は、停止後何もしない。
            Stop(new NextTask(NextTaskType.None), true);
            AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.Stop(){0}", Environment.NewLine));
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            ButtonStopClicked();
        }

        private long mLastSliderValue = 0;

        private void slider1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.Source != slider1) {
                return;
            }

            mLastSliderValue = (long)slider1.Value;
            mSliderSliding = true;
        }

        private void slider1_MouseMove(object sender, MouseEventArgs e) {
            if (e.Source != slider1) {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed) {
                mLastSliderValue = (long)slider1.Value;
                if (!buttonPlay.IsEnabled) {
                    mAp.wasapi.SetPosFrame((long)slider1.Value);
                }
            }
        }
        private void slider1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.Source != slider1) {
                return;
            }

            if (!buttonPlay.IsEnabled &&
                    mLastSliderValue != (long)slider1.Value) {
                mAp.wasapi.SetPosFrame((long)slider1.Value);
            }

            mLastSliderValue = 0;
            mSliderSliding = false;
        }


        /// <summary>
        /// SettingsWindowによって変更された表示情報をUIに反映し、設定を反映する。
        /// </summary>
        void PreferenceUpdated() {
            RenderOptions.ProcessRenderMode =
                    mPreference.GpuRendering ? RenderMode.Default : RenderMode.SoftwareOnly;

            var ffc = new FontFamilyConverter();
            var ff = ffc.ConvertFromString(mPreference.PlayingTimeFontName) as FontFamily;
            if (null != ff) {
                labelPlayingTime.FontFamily = ff;
            }
            labelPlayingTime.FontSize = mPreference.PlayingTimeSize;
            labelPlayingTime.FontWeight = mPreference.PlayingTimeFontBold ? FontWeights.Bold : FontWeights.Normal;

            sliderWindowScaling.Value = mPreference.WindowScale;

            UpdateUIStatus();
        }

        List<string> mLogList = new List<string>();

        /// <summary>
        /// ログを追加する。
        /// </summary>
        /// <param name="s">追加するログ。行末に\r\nを入れる必要あり。</param>
        private void AddLogText(string s) {
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

            textBoxLog.Text = sb.ToString();
            textBoxLog.ScrollToEnd();
        }

        /// <summary>
        /// ロード中に選択曲が変更された場合、ロード後に再生曲変更処理を行う。
        /// ChangePlayWavDataById()でセットし
        /// ReadFileRunWorkerCompleted()で参照する。
        /// </summary>
        private PlayListItemInfo mPliUpdatedByUserWhileLoading = null;

        /// <summary>
        /// 再生中に、再生曲をwavDataIdの曲に切り替える。
        /// wavDataIdの曲がロードされていたら、直ちに再生曲切り替え。
        /// ロードされていなければ、グループをロードしてから再生。
        /// 
        /// 再生中でない場合は、最初に再生する曲をwavDataIdの曲に変更する。
        /// </summary>
        /// <param name="pcmDataId">再生曲</param>
        private void ChangePlayWavDataById(int wavDataId, NextTaskType nextTask) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            var playingId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            var pauseResumeId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.PauseResumeToPlay);
            if (playingId < 0 && pauseResumeId < 0 && 0 <= mLoadingGroupId) {
                // 再生中でなく、ロード中の場合。
                // ロード完了後ReadFileRunWorkerCompleted()で再生する曲を切り替えるための
                // 情報をセットする。
                mPliUpdatedByUserWhileLoading = mPlayListItems[dataGridPlayList.SelectedIndex];
                return;
            }

            if (playingId < 0 && pauseResumeId < 0) {
                // 再生中でなく、再生一時停止中でなく、ロード中でもない場合。
                mAp.wasapi.UpdatePlayPcmDataById(wavDataId);
                return;
            }

            // 再生中か再生一時停止中である。
            var pcmData = mAp.PcmDataListForPlay.FindById(wavDataId);
            if (null == pcmData) {
                // 再生リストの中に次に再生する曲が見つからない。1曲再生の時起きる。
                Stop(new NextTask(nextTask, 0, wavDataId), true);
                return;
            }

            var groupId = pcmData.GroupId;

            var playPcmData = mAp.PcmDataListForPlay.FindById(playingId);
            if (playPcmData == null) {
                playPcmData = mAp.PcmDataListForPlay.FindById(pauseResumeId);
            }
            if (playPcmData.GroupId == groupId) {
                // 同一ファイルグループのファイルの場合、すぐにこの曲が再生可能。
                mAp.wasapi.UpdatePlayPcmDataById(wavDataId);
                AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.UpdatePlayPcmDataById({0}){1}", wavDataId, Environment.NewLine));
            } else {
                // ファイルグループが違う場合、再生を停止し、グループを読み直し、再生を再開する。
                Stop(new NextTask(nextTask, groupId, wavDataId), true);
            }
        }

        /// <summary>
        /// dataGridPlayListの項目(トラック)を削除する。
        /// 項目番号はap.PcmDataListForDispの番号でもある。
        /// </summary>
        private void RemovePlaylistItems(List<int> items) {
            if (0 == items.Count) {
                return;
            }

            if (items.Count == mPlayListItems.Count) {
                // すべて消える。再生開始などが出来なくなるので別処理。
                ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
                return;
            }

            {
                // 再生リストの一部項目が消える。
                // PcmDataのIDが飛び飛びになるので番号を振り直す。
                // PcmDataのGroupIdも飛び飛びになるが、特に問題にならないようなので付け直さない。
                items.Sort();

                for (int i=items.Count - 1; 0 <= i; --i) {
                    int idx = items[i];
                    mAp.PcmDataListForDisp.RemoveAt(idx);
                    mPlayListItems.RemoveAt(idx);
                    // dataGridPlayList.UpdateLayout();
                }

                GC.Collect();

                for (int i = 0; i < mAp.PcmDataListForDisp.Count(); ++i) {
                    mAp.PcmDataListForDisp.At(i).Id = i;
                    mAp.PcmDataListForDisp.At(i).Ordinal = i;
                }

                dataGridPlayList.UpdateLayout();

                UpdateUIStatus();
            }
        }



        /// <summary>
        /// PPW再生リストを読んでmPcmDataListとmPlayListItemsに足す。
        /// UpdateUIは行わない。
        /// </summary>
        /// <param name="path">string.Emptyのとき: IsolatedStorageに保存された再生リストを読む。</param>
        /// <returns>エラーの発生回数。</returns>
        private int ReadPpwPlaylist(string path) {
            int count = 0;

            PlaylistSave3 pl;
            if (path.Length == 0) {
                pl = PpwPlaylistRW.Load();
            } else {
                pl = PpwPlaylistRW.LoadFrom(path);
            }

            var phr = new WWPcmHeaderReader(Encoding.UTF8, false, AddPcmDataDelegate);
            foreach (var p in pl.Items) {

                int rv = phr.ReadFileHeader1(
                        p.PathName,
                        WWPcmHeaderReader.ReadHeaderMode.OnlyConcreteFile,
                        null,
                        PlayListItemSave3ToWWPlaylistItem(p));
                count += rv;
            }

            return count;
        }

#region inter-layer translation

        WWSoundFileReader.SoundFilePcmFmt
        DeviceSetupParamsToSoundFilePcmFmt(DeviceSetupParams a) {
            WWSoundFileReader.SoundFilePcmFmt r = new WWSoundFileReader.SoundFilePcmFmt();
            r.Set(a.SampleRate,
                    a.NumChannels,
                    WasapiCS.SampleFormatTypeToValidBitsPerSample(a.SampleFormat),
                    WasapiCS.SampleFormatTypeToUseBitsPerSample(a.SampleFormat),
                    WasapiCS.SampleFormatTypeIsFloatingPoint(a.SampleFormat),
                    a.StreamType == WasapiCS.StreamType.DoP);
            return r;
        }

        WWPlaylistItem PlayListItemSave3ToWWPlaylistItem(PlaylistItemSave3 pli) {
            var p = new WWPlaylistItem();
            p.AlbumName = pli.AlbumName;
            p.ArtistName = pli.ArtistName;
            p.ComposerName = pli.ComposerName;
            p.CueSheetIndex = pli.CueSheetIndex;
            p.EndTick = pli.EndTick;
            p.LastWriteTime = pli.LastWriteTime;
            p.PathName = pli.PathName;
            p.ReadSeparaterAfter = pli.ReadSeparaterAfter;
            p.StartTick = pli.StartTick;
            p.Title = pli.Title;
            p.TrackId = pli.TrackId;
            return p;
        }

        private static WasapiCS.SchedulerTaskType
        PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(
            RenderThreadTaskType t) {
            switch (t) {
            case RenderThreadTaskType.None:
                return WasapiCS.SchedulerTaskType.None;
            case RenderThreadTaskType.Audio:
                return WasapiCS.SchedulerTaskType.Audio;
            case RenderThreadTaskType.ProAudio:
                return WasapiCS.SchedulerTaskType.ProAudio;
            case RenderThreadTaskType.Playback:
                return WasapiCS.SchedulerTaskType.Playback;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.SchedulerTaskType.None; ;
            }
        }

        private static WasapiCS.ShareMode
        PreferenceShareModeToWasapiCSShareMode(WasapiSharedOrExclusiveType t) {
            switch (t) {
            case WasapiSharedOrExclusiveType.Shared:
                return WasapiCS.ShareMode.Shared;
            case WasapiSharedOrExclusiveType.Exclusive:
                return WasapiCS.ShareMode.Exclusive;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.ShareMode.Exclusive;
            }
        }

        private static WasapiCS.DataFeedMode
        PreferenceDataFeedModeToWasapiCS(WasapiDataFeedModeType t) {
            switch (t) {
            case WasapiDataFeedModeType.EventDriven:
                return WasapiCS.DataFeedMode.EventDriven;
            case WasapiDataFeedModeType.TimerDriven:
                return WasapiCS.DataFeedMode.TimerDriven;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.DataFeedMode.EventDriven;
            }
        }

#endregion

        // イベント処理 /////////////////////////////////////////////////////

        private void buttonSettings_Click(object sender, RoutedEventArgs e) {
            var sw = new SettingsWindow();
            sw.SetPreference(mPreference);
            sw.ShowDialog();

            PreferenceUpdated();
        }

        private void radioButtonExclusive_Checked(object sender, RoutedEventArgs e) {
            mPreference.WasapiSharedOrExclusive = WasapiSharedOrExclusiveType.Exclusive;
        }

        private void radioButtonShared_Checked(object sender, RoutedEventArgs e) {
            mPreference.WasapiSharedOrExclusive = WasapiSharedOrExclusiveType.Shared;
        }

        private void radioButtonEventDriven_Checked(object sender, RoutedEventArgs e) {
            mPreference.WasapiDataFeedMode = WasapiDataFeedModeType.EventDriven;
        }

        private void radioButtonTimerDriven_Checked(object sender, RoutedEventArgs e) {
            mPreference.WasapiDataFeedMode = WasapiDataFeedModeType.TimerDriven;
        }

        private void buttonRemovePlayList_Click(object sender, RoutedEventArgs e) {
            var items = new List<int>();
            items.Add(dataGridPlayList.SelectedIndex);

            RemovePlaylistItems(items);
        }

        private delegate int UpdateOrdinal(int v);

        private void buttonNextOrPrevClickedWhenPlaying(UpdateOrdinal updateOrdinal) {
            NextTaskType nextTask = NextTaskType.PlaySpecifiedGroup;
            var wavDataId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            if (wavDataId < 0) {
                wavDataId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.PauseResumeToPlay);
                nextTask = NextTaskType.PlayPauseSpecifiedGroup;
            } else {
                // 再生リストに登録されている曲数が1曲で、しかも
                // その曲を再生中に、次の曲または前の曲ボタンが押された場合、曲を頭出しする。
                if (1 == mAp.PcmDataListForDisp.Count()) {
                    mAp.wasapi.SetPosFrame(0);
                    return;
                }
            }

            var playingPcmData = mAp.PcmDataListForPlay.FindById(wavDataId);
            if (null == playingPcmData) {
                return;
            }

            var ordinal = playingPcmData.Ordinal;
            int nextPcmDataId = 0;
            for (int i = 0; i < 2; ++i) {
                ordinal = updateOrdinal(ordinal);
                if (ordinal < 0) {
                    ordinal = 0;
                }
                if (mAp.PcmDataListForDisp.Count() <= ordinal) {
                    ordinal = 0;
                }

                int nextCueSheetIndex = -1;
                if (IsPlayModeShuffle()) {
                    // シャッフル再生。
                    nextCueSheetIndex = mAp.PcmDataListForPlay.At(ordinal).CueSheetIndex;
                    nextPcmDataId     = mAp.PcmDataListForPlay.At(ordinal).Id;
                } else {
                    // 全曲再生、1曲再生。1曲再生の時はPlayには1曲だけ入っている。
                    nextCueSheetIndex = mAp.PcmDataListForDisp.At(ordinal).CueSheetIndex;
                    nextPcmDataId     = mAp.PcmDataListForDisp.At(ordinal).Id;
                }

                // 次の曲がIndex0の時、その次の曲にする。
                if (nextCueSheetIndex == 0) {
                    continue;
                } else {
                    break;
                }
            }

            if (ordinal == playingPcmData.Ordinal) {
                // 1曲目再生中に前の曲を押した場合頭出しする。
                mAp.wasapi.SetPosFrame(0);
                return;
            }

            ChangePlayWavDataById(nextPcmDataId, nextTask);
        }

        private void buttonNextOrPrevClickedWhenStop(UpdateOrdinal updateOrdinal) {
            var idx = dataGridPlayList.SelectedIndex;
            idx = updateOrdinal(idx);
            if (idx < 0) {
                idx = 0;
            } else if (dataGridPlayList.Items.Count <= idx) {
                idx = 0;
            }
            dataGridPlayList.SelectedIndex = idx;
            dataGridPlayList.ScrollIntoView(dataGridPlayList.SelectedItem);
        }

        private void buttonNextOrPrevClicked(UpdateOrdinal updateOrdinal) {
            switch (mState) {
            case State.再生一時停止中:
            case State.再生中:
                buttonNextOrPrevClickedWhenPlaying(updateOrdinal);
                break;
            case State.再生リストあり:
                buttonNextOrPrevClickedWhenStop(updateOrdinal);
                break;
            }
        }

        private void buttonNext_Click(object sender, RoutedEventArgs e) {
            buttonNextOrPrevClicked((x) => { return ++x; });
        }

        private void buttonPrev_Click(object sender, RoutedEventArgs e) {
            buttonNextOrPrevClicked((x) => { return --x; });
        }

        private void dataGrid1_LoadingRow(object sender, DataGridRowEventArgs e) {
            e.Row.MouseDoubleClick += new MouseButtonEventHandler(dataGridPlayList_RowMouseDoubleClick);
        }

        private void dataGridPlayList_RowMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (mState == State.再生リストあり && e.ChangedButton == MouseButton.Left && dataGridPlayList.IsReadOnly) {
                // 再生されていない状態で、再生リスト再生モードで項目左ボタンダブルクリックされたら再生開始する
                buttonPlay_Click(sender, e);
            }
        }

        private void dataGridPlayList_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            mPlayListMouseDown = true;

        }

        private void dataGridPlayList_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            mPlayListMouseDown = false;
        }

        private void dataGridPlayList_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e) {
            /*
                if (mState == State.プレイリストあり && 0 <= dataGridPlayList.SelectedIndex) {
                    buttonRemovePlayList.IsEnabled = true;
                } else {
                    buttonRemovePlayList.IsEnabled = false;
                }
            */
        }

        private void dataGridPlayList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateCoverart();

            if (mState == State.再生リストあり && 0 <= dataGridPlayList.SelectedIndex) {
                buttonDelistSelected.IsEnabled = true;
            } else {
                buttonDelistSelected.IsEnabled = false;
            }

            if (null == mAp.wasapi) {
                return;
            }

            if (!mPlayListMouseDown ||
                dataGridPlayList.SelectedIndex < 0 ||
                mPlayListItems.Count() <= dataGridPlayList.SelectedIndex) {
                return;
            }
            mPlayListMouseDown = false;

            var pli = mPlayListItems[dataGridPlayList.SelectedIndex];
            if (pli.PcmData() == null) {
                // 曲じゃない部分を選択したら無視。
                return;
            }

            if (mState != State.再生中) {
                ChangePlayWavDataById(pli.PcmData().Id, NextTaskType.PlaySpecifiedGroup);
                return;
            }

            // 再生中の場合。

            var playingId = mAp.wasapi.GetPcmDataId(WasapiCS.PcmDataUsageType.NowPlaying);
            if (playingId < 0) {
                return;
            }

            // 再生中で、しかも、マウス押下中にこのイベントが来た場合で、
            // しかも、この曲を再生していない場合、この曲を再生する。
            if (null != pli.PcmData() &&
                playingId != pli.PcmData().Id) {
                ChangePlayWavDataById(pli.PcmData().Id, NextTaskType.PlaySpecifiedGroup);
            }
        }

        private bool IsWindowMoveMode(MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) {
                return false;
            }

            foreach (MenuItem mi in menu1.Items) {
                if (mi.IsMouseOver) {
                    return false;
                }
            }
            return true;
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl)) {
                // CTRL + マウスホイールで画面のスケーリング

                var scaling = sliderWindowScaling.Value;
                if (e.Delta < 0) {
                    // 1.25の128乗根 = 1.001744829441175331741294013303
                    scaling /= 1.001744829441175331741294013303;
                } else {
                    scaling *= 1.001744829441175331741294013303;
                }
                sliderWindowScaling.Value = scaling;
                mPreference.WindowScale = scaling;
            }
        }

        /// <summary>
        /// デバイスが突然消えたとか、突然増えたとかのイベント。
        /// </summary>
        private void WasapiStatusChanged(StringBuilder idStr, int dwNewState) {
            //Console.WriteLine("WasapiStatusChanged {0}", idStr);
            Dispatcher.BeginInvoke(new Action(delegate() {
                // AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.DeviceStateChanged + Environment.NewLine, idStr));
                switch (mState)
                {
                    case State.未初期化:
                        return;
                    case State.再生リストなし:
                    case State.再生リスト読み込み中:
                    case State.再生リストあり:
                        // 再生中ではない場合、デバイス一覧を更新する。
                        // DeviceDeselect();
                        UpdateDeviceList();
                        break;
                    case State.デバイスSetup完了:
                    case State.ファイル読み込み完了:
                    case State.再生グループ読み込み中:
                    case State.再生一時停止中:
                    case State.再生中:
                    case State.再生停止開始:
                        if (0 == string.Compare(mUseDevice.DeviceIdStr, idStr.ToString(), StringComparison.Ordinal)) {
                            // 再生に使用しているデバイスの状態が変化した場合、再生停止してデバイス一覧を更新する。
                            AddLogText(string.Format(CultureInfo.InvariantCulture, Properties.Resources.UsingDeviceStateChanged + Environment.NewLine,
                                    mUseDevice.Name, mUseDevice.DeviceIdStr));
                            StopBlocking();
                            DeviceDeselect();
                            UpdateDeviceList();
                        } else {
                            // 次の再生停止時にデバイス一覧を更新する。
                            mDeviceListUpdatePending = true;
                        }
                        break;
                }
            }));
        }
        
#region ドラッグアンドドロップ

        private void dataGridPlayList_CheckDropTarget(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                // ファイルのドラッグアンドドロップ。
                // ここでハンドルせず、MainWindowのMainWindowDragDropに任せる。
                e.Handled = false;
                return;
            }

            e.Handled = true;
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as UIElement);
            if (row == null || !(row.Item is PlayListItemInfo)) {
                // 行がドラッグされていない。
                e.Effects = DragDropEffects.None;
            } else {
                // 行がドラッグされている。
                // Id列を選択している場合のみドラッグアンドドロップ可能。
                //if (0 != "Id".CompareTo(dataGridPlayList.CurrentCell.Column.Header)) {
                //    e.Effects = DragDropEffects.None;
                //}
                // e.Effects = DragDropEffects.Move;
            }
        }

        private void dataGridPlayList_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                // ファイルのドラッグアンドドロップ。
                // ここでハンドルせず、MainWindowのMainWindowDragDropに任せる。
                e.Handled = false;
                return;
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as UIElement);
            if (row == null || !(row.Item is PlayListItemInfo)) {
                // 行がドラッグされていない。(セルがドラッグされている)
            } else {
                // 再生リスト項目のドロップ。
                mDropTargetPlayListItem = row.Item as PlayListItemInfo;
                if (mDropTargetPlayListItem != null) {
                    e.Effects = DragDropEffects.Move;
                }
            }
        }

        private void dataGridPlayList_MouseMove(object sender, MouseEventArgs e) {
            if (mState == State.再生中 ||
                mState == State.再生一時停止中) {
                // 再生中は再生リスト項目入れ替え不可能。
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) {
                // 左マウスボタンが押されていない。
                return;
            }

            var row = FindVisualParent<DataGridRow>(e.OriginalSource as FrameworkElement);
            if ((row == null) || !row.IsSelected) {
                Console.WriteLine("MouseMove row==null || !row.IsSelected");
                return;
            }

            var pli = row.Item as PlayListItemInfo;

            // MainWindow.Drop()イベントを発生させる(ブロック)。
            var finalDropEffect = DragDrop.DoDragDrop(row, pli, DragDropEffects.Move);
            if (finalDropEffect == DragDropEffects.Move && mDropTargetPlayListItem != null) {
                // ドロップ操作実行。
                // Console.WriteLine("MouseMove do move");

                var oldIndex = mPlayListItems.IndexOf(pli);
                var newIndex = mPlayListItems.IndexOf(mDropTargetPlayListItem);
                if (oldIndex != newIndex) {
                    // 項目が挿入された。PcmDataも挿入処理する。
                    mPlayListItems.Move(oldIndex, newIndex);
                    PcmDataListItemsMove(oldIndex, newIndex);
                    // mPlayListView.RefreshCollection();
                    dataGridPlayList.UpdateLayout();
                }
                mDropTargetPlayListItem = null;
            }
        }

        private static T FindVisualParent<T>(UIElement element) where T : UIElement {
            var parent = element;
            while (parent != null) {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null) {
                    return correctlyTyped;
                }

                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private PlayListItemInfo mDropTargetPlayListItem = null;

#endregion

        /// <summary>
        /// mAp.PcmDataListForDispのIdとGroupIdをリナンバーする。
        /// </summary>
        private void PcmDataListForDispItemsRenumber() {
            mGroupIdNextAdd = 0;
            for (int i = 0; i < mAp.PcmDataListForDisp.Count(); ++i) {
                var pcmData = mAp.PcmDataListForDisp.At(i);
                var pli = mPlayListItems[i];

                if (0 < i) {
                    var prevPcmData = mAp.PcmDataListForDisp.At(i - 1);
                    var prevPli = mPlayListItems[i - 1];

                    if (prevPli.ReadSeparaterAfter || !pcmData.IsSameFormat(prevPcmData)) {
                        /* 1つ前の項目にReadSeparatorAfterフラグが立っている、または
                         * 1つ前の項目とPCMフォーマットが異なる。
                         * ファイルグループ番号を更新する。
                         */
                        ++mGroupIdNextAdd;
                    }
                }

                pcmData.Id = i;
                pcmData.Ordinal = i;
                pcmData.GroupId = mGroupIdNextAdd;
            }
        }

        /// <summary>
        /// oldIdxの項目をnewIdxの項目の後に挿入する。
        /// </summary>
        private void PcmDataListItemsMove(int oldIdx, int newIdx) {
            System.Diagnostics.Debug.Assert(oldIdx != newIdx);

            /* oldIdx==0, newIdx==1, Count==2の場合
             * remove(0)
             * insert(1)
             * 
             * oldIdx==1, newIdx==0, Count==2の場合
             * remove(1)
             * insert(0)
             */

            var old = mAp.PcmDataListForDisp.At(oldIdx);
            mAp.PcmDataListForDisp.RemoveAt(oldIdx);
            mAp.PcmDataListForDisp.Insert(newIdx, old);

            // Idをリナンバーする。
            PcmDataListForDispItemsRenumber();
        }

        void PlayListItemInfoPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "ReadSeparaterAfter") {
                // グループ番号をリナンバーする。
                PcmDataListForDispItemsRenumber();
            }
        }

        private void buttonClearPlayList_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

        private void buttonPlayListItemEditMode_Click(object sender, RoutedEventArgs e) {
            dataGridPlayList.IsReadOnly = !dataGridPlayList.IsReadOnly;

            // dataGridPlayList.IsReadOnlyを見て、他の関連メニュー項目状態が更新される
            UpdateUIStatus();
        }

        private InterceptMediaKeys mKListener = null;

        private void AddKeyListener() {
            System.Diagnostics.Debug.Assert(mKListener == null);

            mKListener = new InterceptMediaKeys();
            mKListener.KeyUp += new InterceptMediaKeys.MediaKeyEventHandler(MediaKeyListener_KeyUp);
        }

        private void DeleteKeyListener() {
            if (mKListener != null) {
                mKListener.Dispose();
                mKListener = null;
            }
        }

        private void MediaKeyListener_KeyUp(object sender, InterceptMediaKeys.MediaKeyEventArgs args) {
            if (args == null) {
                return;
            }

            Dispatcher.BeginInvoke(new Action(delegate() {
                switch (args.Key) {
                case Key.MediaPlayPause:
                    if (buttonPlay.IsEnabled) {
                        ButtonPlayClicked();
                    } else if (buttonPause.IsEnabled) {
                        ButtonPauseClicked();
                    }
                    break;
                case Key.MediaStop:
                    if (buttonStop.IsEnabled) {
                        ButtonStopClicked();
                    }
                    break;
                case Key.MediaNextTrack:
                    if (buttonNext.IsEnabled) {
                        buttonNextOrPrevClicked((x) => { return ++x; });
                    }
                    break;
                case Key.MediaPreviousTrack:
                    if (buttonPrev.IsEnabled) {
                        buttonNextOrPrevClicked((x) => { return --x; });
                    }
                    break;
                }
            }));
        }

        private void checkBoxSoundEffects_Checked(object sender, RoutedEventArgs e) {
            mPreference.SoundEffectsEnabled = true;
            buttonSoundEffectsSettings.IsEnabled = true;

            UpdateSoundEffects(true);
        }

        private void checkBoxSoundEffects_Unchecked(object sender, RoutedEventArgs e) {
            mPreference.SoundEffectsEnabled = false;
            buttonSoundEffectsSettings.IsEnabled = false;

            UpdateSoundEffects(false);
        }

        private void UpdatePreferenceAudioFilterListFrom(ObservableCollection<PreferenceAudioFilter> from) {
            mPreferenceAudioFilterList = new List<PreferenceAudioFilter>();
            foreach (var i in from) {
                mPreferenceAudioFilterList.Add(i);
            }
        }

        private void buttonSoundEffectsSettings_Click(object sender, RoutedEventArgs e) {
            var dialog = new SoundEffectsConfiguration();
            dialog.SetAudioFilterList(mPreferenceAudioFilterList);
            var result = dialog.ShowDialog();

            if (true == result) {
                UpdatePreferenceAudioFilterListFrom(dialog.AudioFilterList);

                if (mPreferenceAudioFilterList.Count == 0) {
                    // 音声処理を無効にする。
                    mPreference.SoundEffectsEnabled = false;
                    checkBoxSoundEffects.IsChecked = false;
                    buttonSoundEffectsSettings.IsEnabled = false;
                    UpdateSoundEffects(false);
                } else {
                    UpdateSoundEffects(true);
                }
            }
        }

        private void UpdateSoundEffects(bool bEnable) {
            var sfu = new SoundEffectsUpdater();

            if (bEnable) {
                sfu.Update(mAp.wasapi, mPreferenceAudioFilterList);
            } else {
                sfu.Update(mAp.wasapi, new List<PreferenceAudioFilter>());
            }
        }

        private void MenuItemPPWServerSettings_Click(object sender, RoutedEventArgs e) {
            PPWServerWorker_ShowSettingsWindow();
        }

        private void buttonInspectDevice_Click(object sender, RoutedEventArgs e) {
            InspectSupportedFormats();
        }
    }
}

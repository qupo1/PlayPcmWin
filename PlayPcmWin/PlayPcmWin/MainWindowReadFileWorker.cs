using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using WasapiPcmUtil;

namespace PlayPcmWin {
    public sealed partial class MainWindow : Window {
        private BackgroundWorker mReadFileWorker;

        private void ReadFileWorkerSetup() {
            mReadFileWorker = new BackgroundWorker();
            mReadFileWorker.DoWork += new DoWorkEventHandler(ReadFileDoWork);
            mReadFileWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ReadFileRunWorkerCompleted);
            mReadFileWorker.WorkerReportsProgress = true;
            mReadFileWorker.ProgressChanged += new ProgressChangedEventHandler(ReadFileWorkerProgressChanged);
            mReadFileWorker.WorkerSupportsCancellation = true;

        }

        /// <summary>
        /// 読み出しスレッド開始。
        /// </summary>
        private void ReadFileWorkerRunAsync(int loadGroupId) {
            mReadFileWorker.RunWorkerAsync(loadGroupId);
        }

        private void ReadFileWorkerCancelBlocking() {
            mReadFileWorker.CancelAsync();
            while (mReadFileWorker.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));
                System.Threading.Thread.Sleep(100);
            }
        }

        /// <summary>
        ///  バックグラウンド読み込み。
        ///  mReadFileWorker.RunWorkerAsync(読み込むgroupId)で開始する。
        ///  完了するとReadFileRunWorkerCompletedが呼ばれる。
        /// </summary>
        private void ReadFileDoWork(object o, DoWorkEventArgs args) {
            var bw = o as BackgroundWorker;
            int readGroupId = (int)args.Argument;

            //Console.WriteLine("D: ReadFileDoWork({0}) started", readGroupId);

            WWSoundFileRW.WWSoundFileReader.CalcMD5SumIfAvailable = mPreference.VerifyFlacMD5Sum;

            var r = new ReadFileRunWorkerCompletedArgs();
            try {
                r.hr = -1;
                r.message = string.Empty;

                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();

                mReadProgressInf = new ReadProgressInf(
                        0, 0, 0, 0, mAp.PcmDataListForPlay.CountPcmDataOnPlayGroup(readGroupId));

                mAp.wasapi.ClearPlayList();

                mPcmUtil = new PcmUtil(mAp.PcmDataListForPlay.At(0).NumChannels);

                // ファイルからPCMを読み出す。

                mAp.wasapi.AddPlayPcmDataStart();
                for (int i = 0; i < mAp.PcmDataListForPlay.Count(); ++i) {
                    PcmDataLib.PcmData pd = mAp.PcmDataListForPlay.At(i);
                    if (pd.GroupId != readGroupId) {
                        continue;
                    }

                    // 効果絶大である。(メモリ消費量肥大の抑止)
                    GC.Collect();

                    WasapiPcmUtil.PcmFormatConverter.ClearClippedCounter();

                    long startFrame = (long)(pd.StartTick) * pd.SampleRate / 75;
                    long endFrame = (long)(pd.EndTick) * pd.SampleRate / 75;

                    bool rv = ReadOnePcmFile(bw, pd, startFrame, endFrame, ref r);
                    if (bw.CancellationPending) {
                        r.hr = -1;
                        r.message = string.Empty;
                        args.Result = r;
                        args.Cancel = true;
                        return;
                    }

                    {
                        long clippedCount = WasapiPcmUtil.PcmFormatConverter.ReadClippedCounter();
                        if (0 < clippedCount) {
                            r.individualResultList.Add(new ReadFileResultClipped(pd.Id, clippedCount));
                        }
                    }

                    if (!rv) {
                        args.Result = r;
                        return;
                    }

                    ++mReadProgressInf.trackCount;
                }

                // ダメ押し。
                GC.Collect();

                // サンプルレートの変更。

                if (mPreference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared) {
                    mReadFileWorker.ReportProgress(90, string.Format(CultureInfo.InvariantCulture, "Resampling...{0}", Environment.NewLine));
                }
                r.hr = mAp.wasapi.ResampleIfNeeded(mDeviceSetupParams.ResamplerConversionQuality);
                if (r.hr < 0) {
                    r.message = "Resample({0}) failed! " + string.Format(CultureInfo.InvariantCulture, "0x{1:X8}", mDeviceSetupParams.ResamplerConversionQuality, r.hr);
                    args.Result = r;
                    return;
                }

                mAp.wasapi.ScalePcmAmplitude(1.0);
                if (mPreference.ReduceVolume) {
                    // PCMの音量を6dB下げる。
                    // もしもDSDの時は下げない。
                    double scale = mPreference.ReduceVolumeScale();
                    mAp.wasapi.ScalePcmAmplitude(scale);
                } else if (mPreference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared
                        && mPreference.SootheLimiterApo) {
                    // Limiter APO対策の音量制限。
                    double maxAmplitude = mAp.wasapi.ScanPcmMaxAbsAmplitude();
                    if (SHARED_MAX_AMPLITUDE < maxAmplitude) {
                        mReadFileWorker.ReportProgress(95, string.Format(CultureInfo.InvariantCulture, "Scaling amplitude by {0:0.000}dB ({1:0.000}x) to soothe Limiter APO...{2}",
                                20.0 * Math.Log10(SHARED_MAX_AMPLITUDE / maxAmplitude), SHARED_MAX_AMPLITUDE / maxAmplitude, Environment.NewLine));
                        mAp.wasapi.ScalePcmAmplitude(SHARED_MAX_AMPLITUDE / maxAmplitude);
                    }
                }

                mAp.wasapi.AddPlayPcmDataEnd();

                mPcmUtil = null;

                // 成功。
                sw.Stop();
                r.message = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadPlayGroupNCompleted + Environment.NewLine, readGroupId, sw.ElapsedMilliseconds);
                r.hr = 0;
                args.Result = r;

                mLoadedGroupId = readGroupId;

                // Console.WriteLine("D: ReadFileSingleDoWork({0}) done", readGroupId);
            } catch (IOException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            } catch (ArgumentException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            } catch (UnauthorizedAccessException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            } catch (NullReferenceException ex) {
                args.Result = r.Update(ex.ToString(), -1);
            }
        }

        private void ReadFileReportProgress(long readFrames) {
            lock (mReadFileWorker) {
                mReadProgressInf.readFrames += readFrames;
                var rpi = mReadProgressInf;

                double loadCompletedPercent = 100.0;
                if (mPreference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared) {
                    loadCompletedPercent = 90.0;
                }

                double progressPercentage = loadCompletedPercent * (rpi.trackCount
                        + (double)rpi.readFrames / rpi.WantFramesTotal) / rpi.trackNum;

                // 頻繁に(1Hz以上の頻度で)Log文字列を更新すると描画が止まることがあるのでログの出力を止めた。
                mReadFileWorker.ReportProgress((int)progressPercentage, string.Empty);
            }
        }

    }
}

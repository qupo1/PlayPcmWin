using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using WasapiPcmUtil;

namespace PlayPcmWin {
    public sealed partial class MainWindow : Window {
        // SIMDの都合により48の倍数。
        private const int TYPICAL_READ_FRAMES = 6 * 1024 * 1024;

        private BackgroundWorker m_readFileWorker;

        private void ReadFileWorkerSetup() {
            m_readFileWorker = new BackgroundWorker();
            m_readFileWorker.DoWork += new DoWorkEventHandler(ReadFileDoWork);
            m_readFileWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ReadFileRunWorkerCompleted);
            m_readFileWorker.WorkerReportsProgress = true;
            m_readFileWorker.ProgressChanged += new ProgressChangedEventHandler(ReadFileWorkerProgressChanged);
            m_readFileWorker.WorkerSupportsCancellation = true;

        }

        private void ReadFileWorkerRunAsync(int loadGroupId) {
            m_readFileWorker.RunWorkerAsync(loadGroupId);
        }

        private void ReadFileWorkerCancelBlocking() {
            m_readFileWorker.CancelAsync();
            while (m_readFileWorker.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));
                System.Threading.Thread.Sleep(100);
            }
        }

        /// <summary>
        ///  バックグラウンド読み込み。
        ///  m_readFileWorker.RunWorkerAsync(読み込むgroupId)で開始する。
        ///  完了するとReadFileRunWorkerCompletedが呼ばれる。
        /// </summary>
        private void ReadFileDoWork(object o, DoWorkEventArgs args) {
            var bw = o as BackgroundWorker;
            int readGroupId = (int)args.Argument;

            //Console.WriteLine("D: ReadFileDoWork({0}) started", readGroupId);

            WWSoundFileRW.PcmReader.CalcMD5SumIfAvailable = m_preference.VerifyFlacMD5Sum;

            ReadFileRunWorkerCompletedArgs r = new ReadFileRunWorkerCompletedArgs();
            try {
                r.hr = -1;
                r.message = string.Empty;

                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();

                m_readProgressInfo = new ReadProgressInfo(
                        0, 0, 0, 0, ap.PcmDataListForPlay.CountPcmDataOnPlayGroup(readGroupId));

                ap.wasapi.ClearPlayList();

                mPcmUtil = new PcmUtil(ap.PcmDataListForPlay.At(0).NumChannels);

                // ファイルからPCMを読み出す。

                ap.wasapi.AddPlayPcmDataStart();
                for (int i = 0; i < ap.PcmDataListForPlay.Count(); ++i) {
                    PcmDataLib.PcmData pd = ap.PcmDataListForPlay.At(i);
                    if (pd.GroupId != readGroupId) {
                        continue;
                    }

                    // どーなのよ、という感じがするが。
                    // 効果絶大である。
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

                    ++m_readProgressInfo.trackCount;
                }

                // ダメ押し。
                GC.Collect();

                // サンプルレートの変更。

                if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared) {
                    m_readFileWorker.ReportProgress(90, string.Format(CultureInfo.InvariantCulture, "Resampling...{0}", Environment.NewLine));
                }
                r.hr = ap.wasapi.ResampleIfNeeded(m_deviceSetupParams.ResamplerConversionQuality);
                if (r.hr < 0) {
                    r.message = "Resample({0}) failed! " + string.Format(CultureInfo.InvariantCulture, "0x{1:X8}", m_deviceSetupParams.ResamplerConversionQuality, r.hr);
                    args.Result = r;
                    return;
                }

                ap.wasapi.ScalePcmAmplitude(1.0);
                if (m_preference.ReduceVolume) {
                    // PCMの音量を6dB下げる。
                    // もしもDSDの時は下げない。
                    double scale = m_preference.ReduceVolumeScale();
                    ap.wasapi.ScalePcmAmplitude(scale);
                } else if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared
                        && m_preference.SootheLimiterApo) {
                    // Limiter APO対策の音量制限。
                    double maxAmplitude = ap.wasapi.ScanPcmMaxAbsAmplitude();
                    if (SHARED_MAX_AMPLITUDE < maxAmplitude) {
                        m_readFileWorker.ReportProgress(95, string.Format(CultureInfo.InvariantCulture, "Scaling amplitude by {0:0.000}dB ({1:0.000}x) to soothe Limiter APO...{2}",
                                20.0 * Math.Log10(SHARED_MAX_AMPLITUDE / maxAmplitude), SHARED_MAX_AMPLITUDE / maxAmplitude, Environment.NewLine));
                        ap.wasapi.ScalePcmAmplitude(SHARED_MAX_AMPLITUDE / maxAmplitude);
                    }
                }

                ap.wasapi.AddPlayPcmDataEnd();

                mPcmUtil = null;

                // 成功。
                sw.Stop();
                r.message = string.Format(CultureInfo.InvariantCulture, Properties.Resources.ReadPlayGroupNCompleted + Environment.NewLine, readGroupId, sw.ElapsedMilliseconds);
                r.hr = 0;
                args.Result = r;

                m_loadedGroupId = readGroupId;

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

        private void ReadFileReportProgress(long readFrames, WasapiPcmUtil.PcmFormatConverter.BitsPerSampleConvArgs bpsConvArgs) {
            lock (m_readFileWorker) {
                m_readProgressInfo.readFrames += readFrames;
                var rpi = m_readProgressInfo;

                double loadCompletedPercent = 100.0;
                if (m_preference.WasapiSharedOrExclusive == WasapiSharedOrExclusiveType.Shared) {
                    loadCompletedPercent = 90.0;
                }

                double progressPercentage = loadCompletedPercent * (rpi.trackCount + (double)rpi.readFrames / rpi.WantFramesTotal) / rpi.trackNum;
                m_readFileWorker.ReportProgress((int)progressPercentage, string.Empty);
                /* 頻繁に(1Hz以上の頻度で)Log文字列を更新すると描画が止まることがあるので止めた。
                if (bpsConvArgs != null && bpsConvArgs.noiseShapingOrDitherPerformed) {
                    m_readFileWorker.ReportProgress((int)progressPercentage, string.Format(CultureInfo.InvariantCulture,
                            "{0} {1}/{2} frames done{3}",
                            bpsConvArgs.noiseShaping, rpi.readFrames, rpi.WantFramesTotal, Environment.NewLine));
                }
                */
            }
        }

    }
}

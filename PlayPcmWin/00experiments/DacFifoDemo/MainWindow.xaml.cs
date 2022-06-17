using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DacFifoDemo {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        DispatcherTimer mTimer;

        /// <summary>
        /// DAC sample rate
        /// </summary>
        private const double mPcmSampleFreqHz = 48000.0;

        /// <summary>
        /// USB microframe frequency 48000/3より大きく、48000より小さくする。
        /// </summary>
        //private double mUsbMicroFrameFreqHz = 20000.0;

        /// <summary>
        /// シミュレーションの刻み。(秒)
        /// </summary>
        //private double mSimulationTickSec = 1.0 / (mPcmSampleFreqHz * 2.0 * mUIUpdateFreqHz);

        /// <summary>
        /// UI再描画頻度。 
        /// </summary>
        private const int mUIUpdateFreqHz = 30;

        /// <summary>
        /// シミュレーションの刻み累計回数。
        /// </summary>
        private int mSimulationTickCounter = 0;

        /// <summary>
        /// 出力アナログサイン波の周波数。
        /// </summary>
        private double mSineFreqHz = 48000.0/4.321;

        /// <summary>
        /// DACのクロックカウンター。
        /// </summary>
        private int mDacClockCounter = 0;

        /// <summary>
        /// PCMのサンプルカウンター。
        /// </summary>
        private int mPcmSampleCounter = 0;

        /// <summary>
        /// USBマイクロフレームのカウンタ。
        /// </summary>
        private int mUsbMicroFrameCounter = 0;

        /// <summary>
        /// DACクロックのアニメーションオフセットY。
        /// </summary>
        private double mDacClockDispOffsY = 0;

        /// <summary>
        /// USBクロックのアニメーションオフセットY。
        /// </summary>
        private double mUsbClockDispOffsY = 0;

        /// <summary>
        /// アナログ出力波形のアニメーションオフセットX。
        /// </summary>
        private double mAnalogWaveDispOffsX = 0;

        /// <summary>
        /// HCが送出したマイクロフレームのHC滞在時間(実時間)。
        /// </summary>
        private const double HC_TRANSITION_SEC = 0.2;

        /// <summary>
        /// HCから出てFIFOに届くまでのトランジション・アニメーション時間。
        /// </summary>
        private const double HC_TO_FIFO_TRANSITION_SEC = 0.5;

        /// <summary>
        /// FIFOから出てDACに届くまでのトランジション・アニメーション時間(秒)。
        /// </summary>
        private const double FIFO_TO_DAC_TRANSITION_SEC = 0.5;

        /// <summary>
        /// FIFOサンプルデータの表示Y座標。
        /// </summary>
        private const int Y_FIFO_SAMPLEDATA = 420;

        /// <summary>
        /// HC生成時サンプル表示位置Y。
        /// </summary>
        private const int Y_HC_SAMPLEDATA = 355;

        /// <summary>
        /// HCの出力端X座標。
        /// </summary>
        private const int X_HC_END = 250;

        /// <summary>
        /// サンプルデータ1個の幅。
        /// </summary>
        private const int W_SAMPLEDATA = 40;

        /// <summary>
        /// FIFOの出力単X座標。
        /// </summary>
        private const int X_FIFO_END = 830;

        /// <summary>
        /// 各クロックのスクロール開始Y座標。
        /// </summary>
        private const int Y_CLOCK_START = 100;

        /// <summary>
        /// クロックの波形1周期の高さ(px)。
        /// </summary>
        private const int H_CLOCK_PERIOD = 32;

        /// <summary>
        /// アナログ波形スクロール開始X座標。
        /// </summary>
        private const int X_ANALOG_START = 950;

        /// <summary>
        /// アナログ波形1周期の幅(px)。
        /// </summary>
        private const int W_ANALOG_PERIOD = 128;

        /// <summary>
        /// HCからFIFOまでの移動量(px)。
        /// </summary>
        private const double W_HC_TO_FIFO = 588-160;


        /// <summary>
        /// FIFOからDACまでの移動量(px)。
        /// </summary>
        private const double W_FIFO_TO_DAC = 950-(X_FIFO_END + W_SAMPLEDATA);

        /// <summary>
        /// FIFOに収容できる最大サンプル数。
        /// </summary>
        private const int FIFO_SAMPLE_MAX = 8;

        /// <summary>
        /// フローコントロールフィードバック初期位置X。
        /// </summary>
        private const double X_FB_START = 700;

        /// <summary>
        /// フローコントロールフィードバック最終位置X。
        /// </summary>
        private const double X_FB_END = 200;

        /// <summary>
        /// フローコントロールフィードバック高さY。
        /// </summary>
        private const double Y_FB = 380;

        /// <summary>
        /// フィードバック移動所要時間。
        /// </summary>
        private const double FB_TRANSITION_SEC = 0.5;


        /// <summary>
        /// 経過時間(秒)。DACクロックの時間を基準とする。
        /// </summary>
        private double ElapsedSec() {
            return (double)mDacClockCounter * 1.0 / mPcmSampleFreqHz;
        }

        class HC {
            public enum SampleAmount {
                Less = 1,
                Moderate = 2,
                More = 3,
            }
        }

        private HC mHC = new HC();

        class Fifo {
            public enum BufferState {
                Short,
                OK,
                Long,
            };
            public BufferState mBufferState = BufferState.Short;
        }

        private Fifo mFifo = new Fifo();

        class SampleInf {
            public enum State {
                HC,
                HCtoFifo,
                Fifo,
                FifoToDac,
                Finished,
            };

            public TextBlock mTB;
            public State mState = State.HC;
            public int mFifoPos = -1;
            public int mTransitionCounter = -1;

            public SampleInf(string text, double x, double y, double fontSz) {
                mTB = new TextBlock();
                mTB.Text = text;
                mTB.Background = new SolidColorBrush(Colors.LightYellow);
                mTB.FontFamily = new FontFamily("Consolas");
                mTB.FontSize = fontSz;
                Panel.SetZIndex(mTB, 1);
                Canvas.SetLeft(mTB, x);
                Canvas.SetTop(mTB, y);
            }
        };

        List<SampleInf> mSampleList = new List<SampleInf>();

        class FlowControlFB {
            public enum State {
                FifoToDac,
                DAC,
            };

            public TextBlock mTB;
            public State mState = State.FifoToDac;
            public int mTransitionCounter = -1;

            public HC.SampleAmount mFeedBackMsg = HC.SampleAmount.Moderate;

            public FlowControlFB(string text, double x, double y) {
                mTB = new TextBlock();
                mTB.Text = text;
                mTB.Background = new SolidColorBrush(Colors.LightCyan);
                mTB.FontFamily = new FontFamily("Consolas");
                mTB.FontSize = 16;
                Panel.SetZIndex(mTB, 1);
                Canvas.SetLeft(mTB, x);
                Canvas.SetTop(mTB, y);
            }
        };

        private FlowControlFB mFB = null;

        private string FifoBufferStateToStr() {
            switch (mFifo.mBufferState) {
            case Fifo.BufferState.OK:
            default:
                return "Adequate";
            case Fifo.BufferState.Long:
                return "Long";
            case Fifo.BufferState.Short:
                return "Short";
            }
        }
        private string FifoBufferStateToFBStr() {
            switch (mFifo.mBufferState) {
            case Fifo.BufferState.Long:
                return "Shorter";
            case Fifo.BufferState.OK:
            default:
                return "Moderate";
            case Fifo.BufferState.Short:
                return "Longer";
            }
        }

        private void GenerateFB() {
            mFB = new FlowControlFB("", X_FB_START, Y_FB);
            mCanvas.Children.Add(mFB.mTB);
        }

        private void UpdateFB() {
            if (mFB == null) {
                return;
            }

            ++mFB.mTransitionCounter;
            if (mFB.mTransitionCounter == 0) {
                // フィードバック開始します。
                mFB.mTB.Text = FifoBufferStateToFBStr();
                switch (mFifo.mBufferState) {
                case Fifo.BufferState.OK:
                default:
                    mFB.mFeedBackMsg = HC.SampleAmount.Moderate;
                    break;
                case Fifo.BufferState.Long:
                    mFB.mFeedBackMsg = HC.SampleAmount.Less;
                    break;
                case Fifo.BufferState.Short:
                    mFB.mFeedBackMsg = HC.SampleAmount.More;
                    break;
                }
            } else if (FB_TRANSITION_SEC * mUIUpdateFreqHz < mFB.mTransitionCounter) {
                // トランジション終了。
            } else {
                // トランジション中。
                double progressRatio = (double)mFB.mTransitionCounter / (FB_TRANSITION_SEC * mUIUpdateFreqHz);
                Canvas.SetLeft(mFB.mTB, X_FB_START + (X_FB_END - X_FB_START) * progressRatio);
            }

        }


        private TextBlock NewTB(string text, double x, double y, double fontSz) {
            var si = new SampleInf(text, x, y, fontSz);
            mCanvas.Children.Add(si.mTB);
            mSampleList.Insert(0, si);
            return si.mTB;
        }

        private void HCCreateSendUSBFrame() {
            var sampleCount = HC.SampleAmount.More;
            if (mFB != null) {
                sampleCount = mFB.mFeedBackMsg;
            }

            for (int i = 0; i < (int)sampleCount; ++i) {
                double x = 2.0 * Math.PI * mSineFreqHz * mPcmSampleCounter / mPcmSampleFreqHz;
                //Trace.WriteLine(string.Format("{0} {1}", mPcmSampleCounter, x));

                double y = Math.Sin(x);
                string text = string.Format("{0:X4}", (short)(32767 * y));
                NewTB(text, X_HC_END - W_SAMPLEDATA * i, Y_HC_SAMPLEDATA, 16);

                ++mPcmSampleCounter;
            }

            // フローコントロール フィードバック情報を消します。
            if (mFB != null) {
                mCanvas.Children.Remove(mFB.mTB);
                mFB = null;
            }

        }

        /// <summary>
        /// FIFOに存在するサンプルの総数を数え上げる。
        /// </summary>
        private int FifoSampleCount() {
            int c = 0;

            foreach (var si in mSampleList) {
                if (si.mState == SampleInf.State.Fifo) {
                    ++c;
                }
            }

            return c;
        }

        private void DacPullSample() {
            if (0 < FifoSampleCount()) {
                { 
                    // 最後の要素＝DACに移動。
                    var si = mSampleList[mSampleList.Count - 1];
                    si.mState = SampleInf.State.FifoToDac;
                    si.mTransitionCounter = -1;
                }

                int i = 0;
                foreach (var si in Enumerable.Reverse(mSampleList)) {
                    // FIFOに残った要素を右詰めします。
                    if (si.mState == SampleInf.State.Fifo) {
                        Canvas.SetLeft(si.mTB, X_FIFO_END - i * W_SAMPLEDATA);
                        ++i;
                    }
                }
            }
        }

        private void Update() {
            { 
                // DACクロックをスクロール・アニメーションします。
                mDacClockDispOffsY += H_CLOCK_PERIOD * mSliderSimTick.Value * mPcmSampleFreqHz;
                if (H_CLOCK_PERIOD < mDacClockDispOffsY) {
                    mDacClockDispOffsY -= H_CLOCK_PERIOD;
                    ++mDacClockCounter;
                    DacPullSample();
                }

                Canvas.SetTop(mClockToDac, Y_CLOCK_START + mDacClockDispOffsY);
                Canvas.SetTop(mClockToRcv, Y_CLOCK_START + mDacClockDispOffsY);
            }

            {
                // USBクロックをスクロール・アニメーションします。
                mUsbClockDispOffsY += H_CLOCK_PERIOD * mSliderSimTick.Value * mSliderUsbMFClockHz.Value;
                if (H_CLOCK_PERIOD < mUsbClockDispOffsY) {
                    mUsbClockDispOffsY -= H_CLOCK_PERIOD;
                    ++mUsbMicroFrameCounter;
                    HCCreateSendUSBFrame();
                }

                Canvas.SetTop(mClockToHC, Y_CLOCK_START + mUsbClockDispOffsY);
            }

            {
                // 出力正弦波波形をスクロールします。
                mAnalogWaveDispOffsX += W_ANALOG_PERIOD * mSliderSimTick.Value * mSineFreqHz;
                while (W_ANALOG_PERIOD < mAnalogWaveDispOffsX) {
                    mAnalogWaveDispOffsX -= W_ANALOG_PERIOD;
                }

                //Trace.WriteLine(string.Format("{0}", mAnalogSignalPhase01));

                Canvas.SetLeft(mAnalogSignal, X_ANALOG_START + mAnalogWaveDispOffsX);
            }

            bool sampleArrivedToFifo = false;
            foreach (var si in Enumerable.Reverse(mSampleList)) {
                // サンプルデータ表示位置の更新処理。
                var ev = UpdateSamplePos(si);
                if (ev == SampleEvent.ArrivedToFifo) {
                    sampleArrivedToFifo = true;
                }
            }

            mSampleList.RemoveAll(si => si.mState == SampleInf.State.Finished);

            // FIFOの状態更新。
            if (FifoSampleCount() < FIFO_SAMPLE_MAX / 2) {
                mFifo.mBufferState = Fifo.BufferState.Short;
            } else if (FIFO_SAMPLE_MAX - 2 <= FifoSampleCount()) {
                mFifo.mBufferState = Fifo.BufferState.Long;
            } else {
                mFifo.mBufferState = Fifo.BufferState.OK;
            }
            mQueueStatusTB.Text = FifoBufferStateToStr();

            if (sampleArrivedToFifo) {
                // Fifoのフローコントロール フィードバック送出。
                GenerateFB();
            }
            UpdateFB();

            // 時間表示更新。
            mLabelElapsedTimeSec.Content = string.Format("{0,8:0.0} μs", 1000.0 * 1000.0 * mSimulationTickCounter * mSliderSimTick.Value);
            ++mSimulationTickCounter;
        }

        private enum SampleEvent {
            None,
            ArrivedToFifo,
            ArrivedToDac,
        }

        /// <summary>
        /// サンプルデータの表示位置更新処理。
        /// </summary>
        /// <returns>起きたイベントの種類を戻します。</returns>
        private SampleEvent UpdateSamplePos(SampleInf si) {
            SampleEvent ev = SampleEvent.None;

            ++si.mTransitionCounter;
            double elapsedSec = (double)si.mTransitionCounter / mUIUpdateFreqHz;
            double x;

            switch (si.mState) {
            case SampleInf.State.HC:
                // 静止します。
                if (HC_TRANSITION_SEC <= elapsedSec) {
                    // HC_TO_FIFO状態に遷移。
                    si.mState = SampleInf.State.HCtoFifo;
                    si.mTransitionCounter = -1;
                }
                break;

            case SampleInf.State.HCtoFifo:
                x = Canvas.GetLeft(si.mTB);
                Canvas.SetLeft(si.mTB, x + W_HC_TO_FIFO / (mUIUpdateFreqHz * HC_TO_FIFO_TRANSITION_SEC));
                if (HC_TO_FIFO_TRANSITION_SEC <= elapsedSec) {
                    // FIFO状態に遷移。

                    // FIFO規定位置に移動。
                    int fifoItemCount = FifoSampleCount();
                    Canvas.SetLeft(si.mTB, X_FIFO_END - fifoItemCount * W_SAMPLEDATA);
                    Canvas.SetTop(si.mTB, Y_FIFO_SAMPLEDATA);

                    si.mState = SampleInf.State.Fifo;
                    si.mTransitionCounter = -1;
                    ev = SampleEvent.ArrivedToFifo;
                }
                break;
            case SampleInf.State.Fifo:
                Canvas.SetTop(si.mTB, Y_FIFO_SAMPLEDATA);
                // DACがFIFOからサンプルをPULLすることで遷移します。
                break;
            case SampleInf.State.FifoToDac:
                // FIFOからDACの方向に移動します。
                Canvas.SetLeft(si.mTB, X_FIFO_END + W_SAMPLEDATA + W_FIFO_TO_DAC * elapsedSec / FIFO_TO_DAC_TRANSITION_SEC);
                Canvas.SetTop(si.mTB, Y_FIFO_SAMPLEDATA);

                if (FIFO_TO_DAC_TRANSITION_SEC < elapsedSec) {
                    si.mState = SampleInf.State.Finished;
                    mCanvas.Children.Remove(si.mTB);
                    ev = SampleEvent.ArrivedToDac;
                }
                break;
            case SampleInf.State.Finished:
                break;
            }

            return ev;
        }

        private bool mInitialized = false;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mInitialized = true;
            UpdateLabelSimTick();
            UpdateLabelUsbMFClockHz();

            HCCreateSendUSBFrame();

            mTimer = new DispatcherTimer();
            mTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            mTimer.Interval = new TimeSpan((long)((long)10 * 1000 * 1000 / mUIUpdateFreqHz));
            mTimer.Start();
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e) {
            Update();

            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateLabelSimTick() {
            mLabelSimTick.Content = string.Format("{0:0.####} μs", 1000.0 * 1000.0 * mSliderSimTick.Value);
        }

        private void UpdateLabelUsbMFClockHz() {
            mLabelUsbMFClockHz.Content = string.Format("{0} Hz", (int)mSliderUsbMFClockHz.Value);
        }

        private void mSliderSimTick_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!mInitialized) {
                return;
            }
            UpdateLabelSimTick();
        }

        private void mSliderUsbMFClockHz_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!mInitialized) {
                return;
            }
            UpdateLabelUsbMFClockHz();
        }
    }
}

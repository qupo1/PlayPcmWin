﻿using System;
using System.Collections.Generic;
using WWMath;
using WWAnalogFilterDesign;

namespace WWIIRFilterDesign {
    public class IIRFilterDesign {
        public enum Method {
            ImpulseInvarianceMinimumPhase,
            ImpulseInvarianceMixedPhase,
            Bilinear,
        };

        private Method mMethod;

        private const double CUTOFF_GAIN_DB = -0.1;

        public long SamplingFrequency { get; set; }

        // Impulse invariance methodのとき
        // -10 : 3次 ◎ (poles 対称)(zeroes 虚数1ペア)
        // -20 : 5次 (4次) ×
        // -30 : 5次 ◎ (poles 対称)
        // -40 : 7次 (6次) ×
        // -50 : 7次 ◎ (poles 対称) 
        // -60 : 9次 (8次) ×
        // -70 : 9次 〇 (poles 対称)(zeroesすべて実数)
        // -80 : 11次 (10次) ×
        // -90 : 11次 〇 (poles 対称)(zeroes 虚数1ペア)
        // -100 : 13次 (12次) ×
        // -110 : 13次 〇 (poles 対称)(zeroesすべて実数)
        // -120 : 15次 (14次) ×
        private double StopbandRippleDb() {
            switch (mMethod) {
            case Method.ImpulseInvarianceMinimumPhase:
                return -50; 
            case Method.ImpulseInvarianceMixedPhase:
                return -110;
            case Method.Bilinear:
                return -110;
            default:
                return -110;
            }
        }

        private AnalogFilterDesign mAfd;
        private ImpulseInvarianceMethod mIIRiim;
        private BilinearDesign mIIRBilinear;

        public AnalogFilterDesign Afd() {
            return mAfd;
        }

        public ImpulseInvarianceMethod IIRiim() {
            return mIIRiim;
        }

        public WWMath.Functions.TransferFunctionDelegate TransferFunction() {
            switch (mMethod) {
            case Method.Bilinear:
                return mIIRBilinear.TransferFunction;
            default:
                return mIIRiim.TransferFunction;
            }
        }

        public IIRFilterGraph CreateIIRFilterGraph() {
            IIRFilterGraph iirFilter = null;

            // フィルターを作る。
            // 実数係数版の多項式を使用。
            switch (mMethod) {
            case Method.ImpulseInvarianceMinimumPhase:
                iirFilter = new IIRFilterSerial();
                break;
            case Method.ImpulseInvarianceMixedPhase:
            case Method.Bilinear:
                iirFilter = new IIRFilterParallel();
                break;
            }

            switch (mMethod) {
            case Method.Bilinear:
                for (int i = 0; i < mIIRBilinear.RealHzCount(); ++i) {
                    RealRationalPolynomial p = mIIRBilinear.RealHz(i);
                    Console.WriteLine("{0}", p.ToString("(z)^(-1)"));
                    iirFilter.Add(p);
                }
                break;
            case Method.ImpulseInvarianceMinimumPhase:
            case Method.ImpulseInvarianceMixedPhase:
                for (int i = 0; i < mIIRiim.RealHzCount(); ++i) {
                    RealRationalPolynomial p = mIIRiim.RealHz(i);
                    Console.WriteLine("{0}", p.ToString("(z)^(-1)"));
                    iirFilter.Add(p);
                }
                break;
            }
            return iirFilter;
        }

        public double CutoffFreq { get; set; }
        public double StopbandEdgeFreq { get; set; }

        public bool Design(double fc, double fs, long samplingFreq, Method method,
            AnalogFilterDesign.FilterType filterType = AnalogFilterDesign.FilterType.Cauer) {
            CutoffFreq = fc;
            StopbandEdgeFreq = fs;

            mMethod = method;
            SamplingFrequency = samplingFreq;

            switch (method) {
            case Method.Bilinear:
                return DesignBilinear(fc, fs, samplingFreq, filterType);
            default:
                return DesignImpulseInvariance(fc, fs, samplingFreq, filterType);
            }
        }

        private bool DesignImpulseInvariance(double fc, double fs, long samplingFreq, AnalogFilterDesign.FilterType filterType) {

            mAfd = new AnalogFilterDesign();
            mAfd.DesignLowpass(0, CUTOFF_GAIN_DB, StopbandRippleDb(),
                fc,
                fs,
                filterType,
                ApproximationBase.BetaType.BetaMax);

            var H_s = new List<FirstOrderComplexRationalPolynomial>();
            for (int i = 0; i < mAfd.HPfdCount(); ++i) {
                var p = mAfd.HPfdNth(i);
                H_s.Add(p);
            }

            mIIRiim = new ImpulseInvarianceMethod(H_s, fc * 2.0 * Math.PI, samplingFreq, mMethod == Method.ImpulseInvarianceMinimumPhase);
            return true;
        }

        private bool DesignBilinear(double fc, double fs, long sampleFreq, AnalogFilterDesign.FilterType filterType) {
            double twoπ = 2.0 * Math.PI;

            mIIRBilinear = new BilinearDesign(fc, sampleFreq);

            double fc_pw = mIIRBilinear.PrewarpωtoΩ(twoπ * fc) / twoπ;
            double fs_pw = mIIRBilinear.PrewarpωtoΩ(twoπ * fs) / twoπ;

            mAfd = new AnalogFilterDesign();
            mAfd.DesignLowpass(0,  CUTOFF_GAIN_DB, StopbandRippleDb(),
                fc_pw, fs_pw,
                filterType,
                ApproximationBase.BetaType.BetaMax);

            // 連続時間伝達関数を離散時間伝達関数に変換。
            for (int i = 0; i < mAfd.HPfdCount(); ++i) {
                var s = mAfd.HPfdNth(i);
                mIIRBilinear.Add(s);
            }
            mIIRBilinear.Calc();

            return true;
        }

        public int NumOfPoles() {
            return mAfd.NumOfPoles();
        }

        public WWComplex PoleNth(int nth) {
            switch (mMethod) {
            case Method.Bilinear:
                return mIIRBilinear.StoZ(mAfd.PoleNth(nth)).Reciplocal();
            default:
                return mIIRiim.PoleNth(nth);
            }
        }

        public int NumOfZeroes() {
            switch (mMethod) {
            case Method.Bilinear:
                // バイリニア変換すると、零の数はポールの数と同じになる。
                return mAfd.NumOfPoles();
            default:
                return mIIRiim.NumOfZeroes();
            }
        }

        public WWComplex ZeroNth(int nth) {
            switch (mMethod) {
            case Method.Bilinear:
                if (nth < mAfd.NumOfZeroes()) {
                    return mIIRBilinear.StoZ(mAfd.ZeroNth(nth)).Reciplocal();
                } else {
                    // 零は z==-1にある。
                    return new WWComplex(-1, 0);
                }
            default:
                return mIIRiim.ZeroNth(nth);
            }
        }
    }
}

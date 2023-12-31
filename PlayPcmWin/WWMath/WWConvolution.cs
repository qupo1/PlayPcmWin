﻿// 日本語。
using System;
using System.Threading.Tasks;

namespace WWMath {
    public class WWConvolution {

        /// <summary>
        /// 配列vの要素番号posの要素を戻す。範囲外の時0を戻す。
        /// </summary>
        static WWComplex Get(WWComplex[] v, int pos) {
            if (pos < 0 || v.Length <= pos) {
                return WWComplex.Zero();
            }
            return v[pos];
        }
        
        /// <summary>
        /// Linear Convolution x ** h を計算。
        /// </summary>
        /// <param name="h">コンボリューションカーネル。</param>
        /// <param name="x">入力数列。</param>
        public WWComplex[] ConvolutionBruteForce(WWComplex[] h, WWComplex[] x) {
            var r = new WWComplex[h.Length + x.Length - 1];

            Parallel.For(0, r.Length, i => {
                WWComplex v = WWComplex.Zero();

                for (int j = 0; j < h.Length; ++j) {
                    int hPos = (h.Length - 1) - j; //< ←左右反転されている。
                    int xPos = i + j - (h.Length - 1);
                    v = WWComplex.Add(v, WWComplex.Mul(Get(h, hPos), Get(x, xPos)));
                }
                r[i] = v;
            });

            return r;
        }

        /// <summary>
        /// Linear Convolution x ** h を計算。
        /// </summary>
        /// <param name="h">コンボリューションカーネル。</param>
        /// <param name="x">入力数列。</param>
        public WWComplex[] ConvolutionFft(WWComplex[] h, WWComplex[] x) {
            var r = new WWComplex[h.Length + x.Length - 1];
            int fftSize = Functions.NextPowerOf2(r.Length);
            
            var h2 = new WWComplex[fftSize];
            Array.Copy(h, 0, h2, 0, h.Length);
            for (int i = h.Length; i < h2.Length; ++i) {
                h2[i] = WWComplex.Zero();
            }

            var x2 = new WWComplex[fftSize];
            Array.Copy(x, 0, x2, 0, x.Length);
            for (int i = x.Length; i < x2.Length; ++i) {
                x2[i] = WWComplex.Zero();
            }

            var fft = new WWRadix2Fft(fftSize);
            var H = fft.ForwardFft(h2);
            var X = fft.ForwardFft(x2);

            var Y = WWComplex.Mul(H, X);

            var y = fft.InverseFft(Y);

            Array.Copy(y, 0, r, 0, r.Length);
            return r;
        }

        /// <summary>
        /// 連続FFT オーバーラップアド法でLinear convolution x ** hする。
        /// </summary>
        /// <param name="h">コンボリューションカーネル。</param>
        /// <param name="x">入力数列。</param>
        /// <param name="fragmentSz">個々のFFTに入力するxの断片サイズ。</param>
        public WWComplex[] ConvolutionContinuousFft(WWComplex[] h, WWComplex[] x, int fragmentSz) {
            System.Diagnostics.Debug.Assert(4 <= fragmentSz);

            if (x.Length < h.Length) {
                // swap x and h
                var tmp = h;
                h = x;
                x = tmp;
            }

            // h.Len <= x.Lenである。

            // resultLen: 畳み込み結果の要素数。
            // h: ／＼   x:┌─┐
            //
            //        ↓
            //
            //     ／￣￣＼
            //     h  x   h
            int resultLen = h.Length + x.Length - 1;
            var r = WWComplex.ZeroArray(resultLen);

            // 最小FFTサイズを算出。
            // xをfragment(xf)要素ずつ切り出して畳み込みする。
            // すると、h + xf -1要素の結果が出る。

            int fragConvLen = h.Length + fragmentSz - 1;
            int fftSize = Functions.NextPowerOf2(fragConvLen);

            // hをFFTしてHを得る。
            var h2 = new WWComplex[fftSize];
            Array.Copy(h, 0, h2, 0, h.Length);
            for (int i = h.Length; i < h2.Length; ++i) {
                h2[i] = WWComplex.Zero();
            }
            var fft = new WWRadix2Fft(fftSize);
            var H = fft.ForwardFft(h2);

            for (int offs = 0; offs < x.Length; offs += fragmentSz) {
                // xの断片xFをFFTしてXを得る。
                var xF = WWComplex.ZeroArray(fftSize);
                int copySz = fragmentSz;
                if (x.Length < offs + copySz) {
                    copySz = x.Length - offs;
                }
                Array.Copy(x, offs, xF, 0, copySz);

                var X = fft.ForwardFft(xF);

                // element wise mulする。
                var Y = WWComplex.Mul(H, X);

                // IFFTして時間ドメインに戻す。
                var y = fft.InverseFft(Y);

                // オーバーラップアド法。FFT結果を足し合わせる。
                for (int i = 0; i <fragConvLen; ++i) {
                    if (offs + i < r.Length) {
                        r[offs + i] = WWComplex.Add(r[offs + i], y[i]);
                    }
                }
            }

            return r;
        }
    }
}

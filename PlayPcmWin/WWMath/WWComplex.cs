﻿using System;
using System.Collections;

namespace WWMath {
    public class WWComplex {
        public readonly double real;
        public readonly double imaginary;

        public static string imaginaryUnit = "i";

        public WWComplex(double real, double imaginary) {
            this.real      = real;
            this.imaginary = imaginary;
        }

        public double Magnitude() {
            return Math.Sqrt(real * real + imaginary * imaginary);
        }

        /// <summary>
        /// 大体ゼロのときtrueを戻す。使用する際は大体ゼロの範囲の広さに注意。
        /// </summary>
        public bool AlmostZero() {
            return Math.Abs(real) < double.Epsilon &&
                Math.Abs(imaginary) < double.Epsilon;
        }

        /// <summary>
        /// 内容が全く同じときtrue
        /// </summary>
        public bool EqualValue(WWComplex rhs) {
            return real == rhs.real && imaginary == rhs.imaginary;
        }

        /// <summary>
        /// Phase in radians
        /// </summary>
        /// <returns>radians, -π to +π</returns>
        public double Phase() {
            if (Magnitude() < Double.Epsilon) {
                return 0;
            }

            return Math.Atan2(imaginary, real);
        }

        /// <summary>
        /// create copy and copy := L + R, returns copy.
        /// </summary>
        public static WWComplex Add(WWComplex lhs, WWComplex rhs) {
            return new WWComplex(lhs.real+rhs.real, lhs.imaginary+rhs.imaginary);
        }

        public static WWComplex Add(WWComplex a, WWComplex b, WWComplex c) {
            return new WWComplex(a.real + b.real + c.real, a.imaginary + b.imaginary + c.imaginary);
        }

        public static WWComplex Add(WWComplex a, WWComplex b, WWComplex c, WWComplex d) {
            return new WWComplex(
                a.real + b.real + c.real + d.real,
                a.imaginary + b.imaginary + c.imaginary + d.imaginary);
        }

        public static WWComplex Add(WWComplex a, WWComplex b, WWComplex c, WWComplex d, WWComplex e) {
            return new WWComplex(
                a.real + b.real + c.real + d.real + e.real,
                a.imaginary + b.imaginary + c.imaginary + d.imaginary + e.imaginary);
        }

        /// <summary>
        /// create copy and copy := L - R, returns copy.
        /// </summary>
        public static WWComplex Sub(WWComplex lhs, WWComplex rhs) {
            return new WWComplex(lhs.real - rhs.real, lhs.imaginary - rhs.imaginary);
        }

        /// <summary>
        /// create copy and copy := L * R, returns copy.
        /// </summary>
        public static WWComplex Mul(WWComplex lhs, WWComplex rhs) {
#if false
            // straightforward but slow
            double tR = real * rhs.real      - imaginary * rhs.imaginary;
            double tI = real * rhs.imaginary + imaginary * rhs.real;
            real      = tR;
            imaginary = tI;
#else
            // more efficient way
            double k1 = lhs.real * ( rhs.real + rhs.imaginary );
            double k2 = rhs.imaginary * ( lhs.real + lhs.imaginary );
            double k3 = rhs.real * ( lhs.imaginary - lhs.real );
            double real = k1 - k2;
            double imaginary = k1 + k3;
#endif
            return new WWComplex(real,imaginary);
        }

        public static WWComplex Mul(WWComplex lhs, double v) {
            return new WWComplex(lhs.real * v, lhs.imaginary * v);
        }

        public static WWComplex Reciprocal(WWComplex uni) {
            double sq = uni.real * uni.real + uni.imaginary * uni.imaginary;
            double real = uni.real / sq;
            double imaginary = -uni.imaginary / sq;
            return new WWComplex(real, imaginary);
        }

        /// <summary>
        /// returns reciprocal. this instance is not changed.
        /// </summary>
        public WWComplex Reciplocal() {
            return WWComplex.Reciprocal(this);
        }

        /// <summary>
        /// returns conjugate reciplocal. this instance is not changed.
        /// </summary>
        public WWComplex ConjugateReciprocal() {
            var r = Reciplocal();
            return ComplexConjugate(r);
        }

        /// <summary>
        /// create copy and copy := L / R, returns copy.
        /// </summary>
        public static WWComplex Div(WWComplex lhs, WWComplex rhs) {
            var recip = Reciprocal(rhs);
            return Mul(lhs, recip);
        }

        public static WWComplex Div(WWComplex lhs, double rhs) {
            var recip = 1.0 / rhs;
            return Mul(lhs, recip);
        }

        /// <summary>
        /// create copy and copy := -uni, returns copy.
        /// argument value is not changed.
        /// </summary>
        public static WWComplex Minus(WWComplex uni) {
            return new WWComplex(-uni.real, -uni.imaginary);
        }

        /// <summary>
        /// -1倍したものを戻す。自分自身は変更しない。
        /// </summary>
        public WWComplex Minus() {
            return new WWComplex(-real, -imaginary);
        }

        /// <summary>
        /// create copy and copy := complex conjugate of uni, returns copy.
        /// </summary>
        public static WWComplex ComplexConjugate(WWComplex uni) {
            return new WWComplex(uni.real, -uni.imaginary);
        }

        /// <summary>
        /// scale倍する。自分自身は変更しない。
        /// </summary>
        public WWComplex Scale(double scale) {
            return new WWComplex(scale * real, scale * imaginary);
        }

        /// <summary>
        /// 共役複素数を戻す。 自分自身は変更しない。
        /// </summary>
        /// <returns>共役複素数。</returns>
        public WWComplex ComplexConjugate() {
            return new WWComplex(real, -imaginary);
        }

        /// <summary>
        /// ここで複素数に入れる順序とは、
        /// ①実数成分
        /// ②虚数成分
        /// </summary>
        private class WWComplexComparer : IComparer {
            int IComparer.Compare(Object x, Object y) {
                var cL = x as WWComplex;
                var cR = y as WWComplex;
                if (cL.real != cR.real) {
                    return (cR.real < cL.real) ? 1 : -1;
                }
                if (cL.imaginary != cR.imaginary) {
                    return (cR.imaginary < cL.imaginary) ? 1 : -1;
                }
                return 0;
            }
        }

        /// <summary>
        /// 複素数の配列をWWComplexComparerでソートする。
        /// </summary>
        public static WWComplex[] SortArray(WWComplex[] inp) {
            var outp = new WWComplex[inp.Length];
            Array.Copy(inp, outp, inp.Length);
            Array.Sort(outp, new WWComplexComparer());
            return outp;
        }
        
        public override string ToString() {
            if (Math.Abs(imaginary) < 0.0001) {
                return string.Format("{0:G4}", real);
            }
            if (Math.Abs(real) < 0.0001) {
                return string.Format("{0:G4}{1}", imaginary, imaginaryUnit);
            }

            if (imaginary < 0) {
                // マイナス記号が自動で出る。
                return string.Format("{0:G4}{1:G4}{2}", real, imaginary, imaginaryUnit);
            } else {
                return string.Format("{0:G4}+{1:G4}{2}", real, imaginary, imaginaryUnit);
            }
        }

        public static WWComplex[] Add(WWComplex[] a, WWComplex[] b) {
            if (a.Length != b.Length) {
                throw new ArgumentException("input array length mismatch");
            }

            var c = new WWComplex[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                c[i] = WWComplex.Add(a[i], b[i]);
            }
            return c;
        }

        public static WWComplex[] Mul(WWComplex[] a, WWComplex[] b) {
            if (a.Length != b.Length) {
                throw new ArgumentException("input array length mismatch");
            }

            var c = new WWComplex[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                c[i] = WWComplex.Mul(a[i], b[i]);
            }
            return c;
        }

        public static double AverageDistance(WWComplex[] a, WWComplex[] b) {
            if (a.Length != b.Length) {
                throw new ArgumentException("input array length mismatch");
            }

            double d = 0.0;
            for (int i = 0; i < a.Length; ++i) {
                d += Distance(a[i], b[i]);
            }

            d /= a.Length;
            return d;
        }

        public static double Distance(WWComplex a, WWComplex b) {
            var s = WWComplex.Sub(a, b);
            return s.Magnitude();
        }

        static WWComplex mUnity = new WWComplex(1, 0);
        static WWComplex mZero = new WWComplex(0, 0);
        public static WWComplex Unity() {
            return mUnity;
        }

        public static WWComplex Zero() {
            return mZero;
        }

        /// <summary>
        /// すべての要素値が0の複素数配列をnewして戻す。
        /// </summary>
        /// <param name="count">配列の要素数。</param>
        public static WWComplex[] ZeroArray(int count) {
            var r = new WWComplex[count];
            for (int i = 0; i < r.Length; ++i) {
                r[i] = Zero();
            }
            return r;
        }
        public static WWComplex[] FromRealArray(double[] r) {
            var c = new WWComplex[r.Length];
            for (int i = 0; i < c.Length; ++i) {
                c[i] = new WWComplex(r[i], 0);
            }

            return c;
        }

        public static WWComplex[] FromRealArray(float[] r) {
            var c = new WWComplex[r.Length];
            for (int i = 0; i < c.Length; ++i) {
                c[i] = new WWComplex(r[i], 0);
            }

            return c;
        }

        /// <summary>
        /// 各複素数の実数成分を取り出し実数の配列とする。
        /// </summary>
        public static double[] ToRealArray(WWComplex[] c) {
            var r = new double[c.Length];
            for (int i = 0; i < r.Length; ++i) {
                r[i] = c[i].real;
            }

            return r;
        }

        /// <summary>
        /// 各複素数の大きさを取り実数にし、配列を戻す。
        /// </summary>
        public static double[] ToMagnitudeRealArray(WWComplex[] c) {
            var r = new double[c.Length];
            for (int i = 0; i < r.Length; ++i) {
                r[i] = c[i].Magnitude();
            }

            return r;
        }
    }
}

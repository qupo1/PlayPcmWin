using System.Runtime.InteropServices;
using System;

namespace WWNativeSoundFileReaderCs {
    public class WWNativeSoundFileReader : IDisposable {
        private int mIdx = -1;

        public void Init() {
            mIdx = NativeMethods.WWNativeSoundFileReaderInit();
        }

        public void Term() {
            if (0 <= mIdx) {
                NativeMethods.WWNativeSoundFileReaderTerm(mIdx);
                mIdx = -1;
            }
        }

        public void Dispose() {
            Term();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        public struct WWNativePcmFmt {
            public int sampleRate;
            public int numChannels;
            public int validBitDepth;
            public int containerBitDepth;
            public int isFloat; //< 0: int, 1: float。
            public int isDoP; //< DoPの場合、sampleRate=176400 (16分の1), validBitsPerSample=24、containerBitsPerSample={24|32}になります。
        };

        public int ReadBegin(string path, ref WWNativePcmFmt origPcmFmt, ref WWNativePcmFmt tgtPcmFmt, ref int[] channelMap) {
            System.Diagnostics.Debug.Assert(0 <= mIdx);

            return NativeMethods.WWNativeSoundFileReaderStart(mIdx, path, ref origPcmFmt, ref tgtPcmFmt, ref channelMap);
        }

        /// <summary>
        /// ファイルのfileOffset位置からsampleCount読んでbufTo[bufToPos]に書き込みます。
        /// 読み出しバイト数はsampleCount * origPcmFmt.numChannels * containerBitDepth / 8
        /// 書き込みバイト数はsampleCount * tgtPcmFmt.numChannels * containerBitDepth / 8
        /// </summary>
        public int ReadOne(long fileOffset, long sampleCount, byte[] bufTo, long bufToPos) {
            System.Diagnostics.Debug.Assert(0 <= mIdx);

            return NativeMethods.WWNativeSoundFileReaderReadOne(mIdx, fileOffset, sampleCount, bufTo, bufToPos);
        }

        public void ReadEnd() {
            System.Diagnostics.Debug.Assert(0 <= mIdx);
            NativeMethods.WWNativeSoundFileReaderReadEnd(mIdx);
        }

#region Native Stuff
        internal static class NativeMethods {

            /// @return 0以上: インスタンスId。負: エラー。
            [DllImport("WWNativeSoundFileReaderDLL.dll", CharSet = CharSet.Unicode)]
            internal extern static int
            WWNativeSoundFileReaderInit();

            /// ファイル読み出し開始。
            [DllImport("WWNativeSoundFileReaderDLL.dll", CharSet = CharSet.Unicode)]
            internal extern static int
            WWNativeSoundFileReaderStart(int id, string path, ref WWNativePcmFmt origPcmFmt, ref WWNativePcmFmt tgtPcmFmt, ref int[] channelMap);

            /// 読み終わるまでブロックします。
            [DllImport("WWNativeSoundFileReaderDLL.dll", CharSet = CharSet.Unicode)]
            internal extern static int
            WWNativeSoundFileReaderReadOne(int id, long fileOffset, long sampleCount, byte[] bufTo, long bufToPos);

            /// ファイル読み出し終了。
            [DllImport("WWNativeSoundFileReaderDLL.dll", CharSet = CharSet.Unicode)]
            internal extern static int
            WWNativeSoundFileReaderReadEnd(int id);

            /// 終了処理。
            /// @param id Init()の戻り値のインスタンスID。
            [DllImport("WWNativeSoundFileReaderDLL.dll", CharSet = CharSet.Unicode)]
            internal extern static int
            WWNativeSoundFileReaderTerm(int id);
        };
#endregion

    }
}

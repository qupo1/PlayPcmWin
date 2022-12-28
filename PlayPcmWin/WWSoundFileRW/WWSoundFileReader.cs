using WavRWLib2;
using PcmDataLib;
using System;
using System.IO;
using System.Globalization;
using WWMFReaderCs;
using System.Linq;
using WWNativeSoundFileReaderCs;
using System.Text;

namespace WWSoundFileRW {
    public class WWSoundFileReader : IDisposable {
        // SIMDの都合により48の倍数。
        public const int TYPICAL_READ_FRAMES = 6 * 1024 * 1024;

        private PcmData mPcmData;
        private FlacDecodeIF mFlacR;
        private AiffReader mAiffR;
        private DsfReader mDsfR;
        private DsdiffReader mDsdiffR;
        private WavReader mWaveR;
        private BinaryReader mBr;
        private Mp3Reader mMp3Reader;

        private class NativeReaderInf {
            public WWNativeSoundFileReader mNSFR;
            public bool mUseNativeReader = false;
            public IntPtr mBuf = new IntPtr();
            public long mFileOffset = 0;
            public int mTgtBytesPerFrame = 0;

            public int ReadBegin(string path, long fileOffset, SoundFilePcmFmt origF, SoundFilePcmFmt tgtF) {
                mFileOffset = fileOffset;
                mUseNativeReader = true;
                mTgtBytesPerFrame = tgtF.BytesPerFrame;
                mNSFR = new WWNativeSoundFileReader();
                mNSFR.Init();
                mBuf = mNSFR.AllocNativeBuffer(TYPICAL_READ_FRAMES * mTgtBytesPerFrame);
                return mNSFR.ReadBegin(path,
                    SoundFilePcmFmtToWWNativePcmFmt(origF),
                    SoundFilePcmFmtToWWNativePcmFmt(tgtF), null);
            }

            public byte [] ReadOne(int preferredFrames) {
                if (TYPICAL_READ_FRAMES < preferredFrames) {
                    throw new InternalBufferOverflowException();
                }

                int rv = mNSFR.ReadOne(mFileOffset, preferredFrames, mBuf, 0);
                if (0 <= rv) {
                    // 成功。
                    int bytes = preferredFrames * mTgtBytesPerFrame;
                    
                    var r = new byte[bytes];
                    System.Runtime.InteropServices.Marshal.Copy(mBuf, r, 0, bytes);
                    
                    mFileOffset += bytes;

                    return r;
                } else {
                    // 失敗。
                    return null;
                }
            }

            public void ReadEnd() {
                if (!mUseNativeReader) {
                    return;
                }
                mNSFR.ReadEnd();
                mNSFR.Term();
                mNSFR = null;
                mUseNativeReader = false;
                mBuf = IntPtr.Zero;
            }

            private WWNativeSoundFileReader.WWNativePcmFmt
            SoundFilePcmFmtToWWNativePcmFmt(SoundFilePcmFmt p) {
                var r = new WWNativeSoundFileReader.WWNativePcmFmt();
                r.Set(p.sampleRate, p.numChannels, p.validBitDepth,
                        p.containerBitDepth, p.isFloat != 0, p.isDoP != 0);
                return r;
            }
        };
        private NativeReaderInf mNativeR = new NativeReaderInf();

        private byte[] mMD5SumOfPcm;
        private byte[] mMD5SumInMetadata;

        public byte[] MD5SumOfPcm { get { return mMD5SumOfPcm; } }
        public byte[] MD5SumInMetadata { get { return mMD5SumInMetadata; } }

        public static bool CalcMD5SumIfAvailable { get; set; }

        public long NumFrames { get; set; }

        public enum Format {
            FLAC,
            AIFF,
            WAVE,
            DSF,
            DSDIFF,
            MP3,
            Unknown
        };
        Format m_format;

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                mBr.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        public static bool IsTheFormatParallelizable(Format fmt) {
            switch (fmt) {
            case Format.FLAC:
                return true;
            case Format.MP3:
            case Format.AIFF:
            case Format.WAVE:
            case Format.DSF:
            case Format.DSDIFF:
                return false;
            default:
                System.Diagnostics.Debug.Assert(false);
                return false;
            }
        }

        public static Format GuessFileFormatFromFilePath(string path) {
            string ext = System.IO.Path.GetExtension(path);
            switch (ext.ToUpperInvariant()) {
            case ".FLAC":
                return Format.FLAC;
            case ".AIF":
            case ".AIFF":
            case ".AIFC":
            case ".AIFFC":
                return Format.AIFF;
            case ".WAV":
            case ".WAVE":
                return Format.WAVE;
            case ".DSF":
                return Format.DSF;
            case ".DFF":
                return Format.DSDIFF;
            case ".MP3":
                return Format.MP3;
            default:
                return Format.Unknown;
            }
        }

        public class SoundFilePcmFmt {
            public int sampleRate;
            public int numChannels;
            public int validBitDepth;
            public int containerBitDepth;
            public int isFloat; //< 0: int, 1: float。
            public int isDoP; //< DoPの場合、sampleRate=176400 (16分の1), validBitsPerSample=24、containerBitsPerSample={24|32}になります。
            
            public void Set(int aSampleRate, int aNumChannels, int aValidBitDepth, int aContainerBitDepth, bool aIsFloat, bool aIsDoP) {
                sampleRate = aSampleRate;
                numChannels = aNumChannels;
                validBitDepth = aValidBitDepth;
                containerBitDepth = aContainerBitDepth;
                isFloat = aIsFloat ? 1 : 0;
                isDoP = aIsDoP ? 1 : 0;
            }

            public int BytesPerFrame {
                get { return numChannels * containerBitDepth / 8; }
            }
        };

        /// <summary>
        /// StreamBegin()を呼んだら、成功しても失敗してもStreamEnd()を呼んでください。
        /// </summary>
        /// <param name="path">ファイルパス。</param>
        /// <param name="startFrame">読み出し開始フレーム</param>
        /// <param name="wantFrames">取得したいフレーム数。負の数: 最後まで。0: 取得しない。</param>
        /// <returns>0以上: 成功。負: 失敗。</returns>
        public int StreamBegin(
                PcmDataLib.PcmData pdCopy,
                string path,
                long startFrame, long wantFrames, int typicalReadFrames,
                SoundFilePcmFmt desiredFmt) {

            var fmt = GuessFileFormatFromFilePath(path);
            try {
                switch (fmt) {
                case Format.FLAC:
                    m_format = Format.FLAC;
                    return StreamBeginFlac(path, startFrame);
                case Format.AIFF:
                    m_format = Format.AIFF;
                    return StreamBeginAiff(path, startFrame);
                case Format.WAVE:
                    m_format = Format.WAVE;
                    return ReadBeginWav(pdCopy, path, startFrame, desiredFmt);
                case Format.DSF:
                    m_format = Format.DSF;
                    return StreamBeginDsf(path, startFrame);
                case Format.DSDIFF:
                    m_format = Format.DSDIFF;
                    return StreamBeginDsdiff(path, startFrame);
                case Format.MP3:
                    m_format = Format.MP3;
                    return StreamBeginMp3(path, (int)startFrame);
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return -1;
                }
            } catch (IOException ex) {
                Console.WriteLine("E: StreamBegin {0}" + ex);
                return -1;
            } catch (ArgumentException ex) {
                Console.WriteLine("E: StreamBegin {0}" + ex);
                return -1;
            } catch (UnauthorizedAccessException ex) {
                Console.WriteLine("E: StreamBegin {0}" + ex);
                return -1;
            }
        }

        /// <summary>
        /// PCMデータを読み出す。
        /// </summary>
        /// <param name="preferredFrames">読み込みたいフレーム数(オリジナルSRでフレーム数を数える)。48の倍数が良い。6Mフレームぐらいにすると良い。(このフレーム数のデータが戻るとは限らない)</param>
        /// <returns>PCMデータが詰まったバイト列。0要素の配列の場合、もう終わり。</returns>
        public byte[] StreamReadOne(int preferredFrames, out int ercd) {
            ercd = 0;

            // FLACのデコーダーはエラーコードを戻すことがある。
            // 他のデコーダーは、データ領域に構造がないので読み出しエラーは特にない。System.IOExceptionが起きることはある。

            byte[] result;
            switch (m_format) {
            case Format.FLAC:
                result = mFlacR.ReadStreamReadOne(preferredFrames, out ercd);
                break;
            case Format.AIFF:
                result = mAiffR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.WAVE:
                result = ReadOneWav(preferredFrames);
                break;
            case Format.DSF:
                result = mDsfR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.DSDIFF:
                result = mDsdiffR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.MP3:
                if (int.MaxValue < mMp3Reader.data.LongLength) {
                    result = new byte[0];
                } else {
                    result = mMp3Reader.data.ToArray();
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                result = new byte[0];
                break;
            }
            return result;
        }

        public void StreamAbort() {
            switch (m_format) {
            case Format.FLAC:
                mFlacR.ReadStreamAbort();
                break;
            case Format.AIFF:
                mAiffR.ReadStreamEnd();
                break;
            case Format.WAVE:
                ReadEndWav();
                break;
            case Format.DSF:
                mDsfR.ReadStreamEnd();
                break;
            case Format.DSDIFF:
                mDsdiffR.ReadStreamEnd();
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            if (null != mBr) {
                mBr.Close();
                mBr = null;
            }
            mPcmData = null;
            mFlacR = null;
            mAiffR = null;
            mDsfR = null;
        }

        /// <summary>
        /// 読み込み処理を終了する。
        /// </summary>
        /// <returns>Error code</returns>
        public int StreamEnd() {
            int rv = 0;

            mMD5SumInMetadata = null;
            mMD5SumOfPcm = null;

            switch (m_format) {
            case Format.FLAC:
                rv = mFlacR.ReadEnd();
                mMD5SumInMetadata = mFlacR.MD5SumInMetadata;
                mMD5SumOfPcm = mFlacR.MD5SumOfPcm;
                break;
            case Format.AIFF:
                mAiffR.ReadStreamEnd();
                break;
            case Format.WAVE:
                ReadEndWav();
                break;
            case Format.DSF:
                mDsfR.ReadStreamEnd();
                break;
            case Format.DSDIFF:
                mDsdiffR.ReadStreamEnd();
                break;
            case Format.MP3:
                mMp3Reader.ReadStreamEnd();
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            if (null != mBr) {
                mBr.Close();
                mBr = null;
            }
            mPcmData = null;
            mFlacR = null;
            mAiffR = null;
            mDsfR = null;

            return rv;
        }

        private int StreamBeginFlac(string path, long startFrame)
        {
            mFlacR = new FlacDecodeIF();
            mFlacR.CalcMD5 = CalcMD5SumIfAvailable;
            int ercd = mFlacR.ReadStreamBegin(path, startFrame, out mPcmData);
            if (ercd < 0) {
                return ercd;
            }

            NumFrames = mFlacR.NumFrames;
            return ercd;
        }

        private int StreamBeginAiff(string path, long startFrame)
        {
            int ercd = -1;

            mAiffR = new AiffReader();
            mBr = new BinaryReader(
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

            AiffReader.ResultType result = mAiffR.ReadStreamBegin(mBr, out mPcmData);
            if (result == AiffReader.ResultType.Success) {

                NumFrames = mAiffR.NumFrames;

                mAiffR.ReadStreamSkip(mBr, startFrame);
                ercd = 0;
            }

            return ercd;
        }

        private int StreamBeginDsf(string path, long startFrame) {
            int ercd = -1;

            mDsfR = new DsfReader();
            mBr = new BinaryReader(
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

            DsfReader.ResultType result = mDsfR.ReadStreamBegin(mBr, out mPcmData);
            if (result == DsfReader.ResultType.Success) {

                NumFrames = mDsfR.OutputFrames;

                mDsfR.ReadStreamSkip(mBr, startFrame);
                ercd = 0;
            }

            return ercd;
        }

        private int StreamBeginDsdiff(string path, long startFrame) {
            int ercd = -1;

            mDsdiffR = new DsdiffReader();
            mBr = new BinaryReader(
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

            DsdiffReader.ResultType result = mDsdiffR.ReadStreamBegin(mBr, out mPcmData);
            if (result == DsdiffReader.ResultType.Success) {

                NumFrames = mDsdiffR.OutputFrames;

                mDsdiffR.ReadStreamSkip(mBr, startFrame);
                ercd = 0;
            }

            return ercd;
        }

        private int StreamBeginMp3(string path, int startFrame) {
            mMp3Reader = new Mp3Reader();
            int hr = mMp3Reader.Read(path);
            if (0 <= hr && 0 < startFrame) {
                if (startFrame < mMp3Reader.data.LongLength) {
                    mMp3Reader.data = mMp3Reader.data.Skip(startFrame);
                }
            }

            return hr;
        }

        private int ReadBeginWav(PcmDataLib.PcmData pdCopy, string path, long startFrame, SoundFilePcmFmt tgtF) {
            int ercd = -1;

            var origF = new SoundFilePcmFmt();

            // ヘッダを読んで読み出し開始位置startOffsを調べる。
            // 読み出す方法を、シンプルなWAVファイルと複雑なWAVファイルで切り替えます。
            using (var br = new BinaryReader(
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                var wr = new WavReader();
                if (wr.ReadHeader(br)) {
                    // WAVヘッダ読み込み成功。
                    if (wr.DscList().Count == 1) {
                        // シンプルなWAVファイル。WWNativeSoundFileReaderを使用。
                        var dsc = wr.DscList()[0];

                        origF.Set(
                                wr.SampleRate, wr.NumChannels,
                                wr.ValidBitsPerSample, wr.BitsPerSample,
                                wr.SampleValueRepresentationType == PcmData.ValueRepresentationType.SFloat,
                                wr.SampleDataType == WavReader.DataType.DoP);

                        // チャンネル数をorigとtgtで合わせます。
                        tgtF.numChannels = origF.numChannels;

                        // tgtFへのサンプルフォーマット変更を反映します。
                        pdCopy.BitsPerSample = tgtF.containerBitDepth;
                        pdCopy.ValidBitsPerSample = tgtF.validBitDepth;
                        pdCopy.SampleValueRepresentationType
                                = tgtF.isFloat != 0 ? PcmData.ValueRepresentationType.SFloat : PcmData.ValueRepresentationType.SInt;

                        // WWNativeSoundFileReaderを使用。
                        ercd = mNativeR.ReadBegin(path, dsc.Offset, origF, tgtF);
                        return ercd;
                    }
                }
            }

            {
                // WavReaderを使用。
                mWaveR = new WavReader();
                mBr = new BinaryReader(
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

                bool readSuccess = mWaveR.ReadStreamBegin(mBr, out mPcmData);
                if (readSuccess) {
                    NumFrames = mWaveR.NumFrames;

                    if (mWaveR.ReadStreamSkip(mBr, startFrame)) {
                        ercd = 0;
                    }
                }
            }

            return ercd;
        }

        private byte[] ReadOneWav(int preferredFrames) {
            if (mNativeR.mUseNativeReader) {
                return mNativeR.ReadOne(preferredFrames);
            } else {
                return mWaveR.ReadStreamReadOne(mBr, preferredFrames);
            }
        }

        private void ReadEndWav() {
            mNativeR.ReadEnd();

            if (mWaveR != null) {
                mWaveR.ReadStreamEnd();
                mWaveR = null;
            }
        }
    }
}

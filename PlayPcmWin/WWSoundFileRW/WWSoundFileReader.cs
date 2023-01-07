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

        /// <summary>
        /// EOF : End Of File HRESULTの値です。
        /// </summary>
        public const int ERROR_HANDLE_EOF = 38;

        private PcmDataLib.PcmData mPcmData;
        private FlacDecodeIF mFlacR;
        private AiffReader mAiffR;
        private DsfReader mDsfR;
        private DsdiffReader mDsdiffR;
        private WavReader mWaveR;
        private BinaryReader mBr;
        private Mp3Reader mMp3Reader;
        private IntPtr mWriteBeginPtr;

        private class NativeReaderInf {
            public WWNativeSoundFileReader mNSFR;
            public bool mUseNativeReader = false;
            public long mFileOffs = 0;
            private IntPtr mWriteBeginPtr;
            public long mWritePtrOffs = 0;
            public int mSrcBytesPerFrame = 0;
            public int mTgtBytesPerFrame = 0;

            public int ReadBegin(
                    string path, long fileOffset,
                    SoundFilePcmFmt srcF, SoundFilePcmFmt tgtF,
                    IntPtr writePtr) {
                mFileOffs = fileOffset;
                mWriteBeginPtr = writePtr;
                mWritePtrOffs = 0;
                mUseNativeReader = true;
                mSrcBytesPerFrame = srcF.BytesPerFrame;
                mTgtBytesPerFrame = tgtF.BytesPerFrame;
                mNSFR = new WWNativeSoundFileReader();
                mNSFR.Init();

                return mNSFR.ReadBegin(path,
                    SoundFilePcmFmtToWWNativePcmFmt(srcF),
                    SoundFilePcmFmtToWWNativePcmFmt(tgtF), null);
            }

            public int ReadOne(int readFrames) {
                if (TYPICAL_READ_FRAMES < readFrames) {
                    throw new InternalBufferOverflowException();
                }

                int rv = mNSFR.ReadOne(mFileOffs, readFrames, mWriteBeginPtr, mWritePtrOffs);
                if (0 <= rv) {
                    // 成功。
                    int srcBytes = readFrames * mSrcBytesPerFrame;
                    int tgtBytes = readFrames * mTgtBytesPerFrame;

                    mWritePtrOffs += tgtBytes;
                    mFileOffs += srcBytes;
                } else {
                    // 失敗。
                }

                return rv;
            }

            public void ReadEnd() {
                if (!mUseNativeReader) {
                    return;
                }
                mNSFR.ReadEnd();
                mNSFR.Term();
                mNSFR = null;
                mUseNativeReader = false;
                mWriteBeginPtr = IntPtr.Zero;
                mWritePtrOffs = 0;
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
                if (null != mBr) {
                    mBr.Close();
                    mBr = null;
                }
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
                long startFrame, long wantFrames,
                SoundFilePcmFmt desiredFmt,
                IntPtr writeBeginPtr) {
            mPcmData = pdCopy;
            mWriteBeginPtr = writeBeginPtr;

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
                    return ReadBeginWav(path, startFrame, desiredFmt, writeBeginPtr);
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
        /// <param name="b_out">読み出したPCMデータ断片が戻ることがある。このとき呼び出し側はwasapi再生データとしてセットします。
        ///                     サイズが0の場合は、中で既にwasapiにセットした場合、EOFに達した場合、読み出しエラー発生の場合がある。戻り値によって判断します。</param>
        /// <param name="readFrames">読み込みたいフレーム数(オリジナルSRでフレーム数を数える)。48の倍数が良い。6Mフレームぐらいにすると良い。(このフレーム数のデータが戻るとは限らない)</param>
        /// <returns>0: 成功、負：エラー、ERROR_HANDLE_EOF(38): EOFに達した。</returns>
        public int StreamReadOne(int preferredFrames, out byte[] b_out, out int readFrames) {
            readFrames = 0;
            int ercd = 0;

            // FLACのデコーダーはエラーコードを戻すことがある。
            // 他のデコーダーは、データ領域に構造がないので読み出しエラーは特にない。System.IOExceptionが起きることはある。

            switch (m_format) {
            case Format.FLAC:
                b_out = mFlacR.ReadStreamReadOne(preferredFrames, out ercd);
                break;
            case Format.AIFF:
                b_out = mAiffR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.WAVE:
                // 新型の読み出し処理。
                return ReadOneWav(preferredFrames, out b_out, out readFrames);
            case Format.DSF:
                b_out = mDsfR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.DSDIFF:
                b_out = mDsdiffR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.MP3:
                if (int.MaxValue < mMp3Reader.data.LongLength) {
                    b_out = new byte[0];
                    ercd = ERROR_HANDLE_EOF;
                } else {
                    b_out = mMp3Reader.data.ToArray();
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                b_out = new byte[0];
                break;
            }

            if (b_out == null && 0 <= ercd) {
                // 読み出しエラーにします。
                b_out = new byte[0];
                ercd = -1;
            } else if (b_out != null && b_out.Length == 0) {
                // EOFに達した。
                ercd = ERROR_HANDLE_EOF;
            } else {
                // 実際に読み出されたフレーム数readFrames。
                readFrames = b_out.Length / ( mPcmData.BitsPerFrame / 8 );
            }

            return ercd;
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

        private int ReadBeginWav(
                string path,
                long startFrame,
                SoundFilePcmFmt tgtF,
                IntPtr writeBeginPtr) {
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
                        mPcmData.BitsPerSample      = tgtF.containerBitDepth;
                        mPcmData.ValidBitsPerSample = tgtF.validBitDepth;
                        mPcmData.SampleValueRepresentationType
                                = tgtF.isFloat != 0 
                                    ? PcmData.ValueRepresentationType.SFloat
                                    : PcmData.ValueRepresentationType.SInt;

                        // WWNativeSoundFileReaderを使用。
                        ercd = mNativeR.ReadBegin(path, dsc.Offset, origF, tgtF,
                                writeBeginPtr);
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

        /// <summary>
        /// Wavの読み出し。
        /// </summary>
        /// <returns>0: 成功。負: 失敗。ERROR_HANDLE_EOF: EOFに達した。</returns>
        private int ReadOneWav(
                int preferredFrames, out byte[] b_out, out int readFrames) {
            readFrames = preferredFrames;
            if (mNativeR.mUseNativeReader) {
                // ネイティブWav Readの場合、中でWasapiバッファに直接書き込むのでb_outは空です。
                b_out = new byte[0];
                return mNativeR.ReadOne(preferredFrames);
            } else {
                b_out = mWaveR.ReadStreamReadOne(mBr, preferredFrames);

                // 戻り値を決定します。
                if (b_out == null) {
                    return -1;
                } else if (b_out.Count() == 0) {
                    return ERROR_HANDLE_EOF;
                } else {
                    // 成功。
                    readFrames = b_out.Length / (mPcmData.BitsPerFrame / 8);
                    return 0;
                }
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

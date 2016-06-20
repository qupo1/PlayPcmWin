﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWAudioFilter {
    struct AudioDataPerChannel {
        public PcmDataLib.LargeArray<byte> data;
        public long offsBytes;
        public long totalSamples;
        public int bitsPerSample;
        public bool overflow;
        public double maxMagnitude;

        public void ResetStatistics() {
            overflow = false;
            maxMagnitude = 0.0;
        }

        /// <summary>
        /// double[]を戻すバージョン。
        /// </summary>
        /// <param name="count">取得する要素数。範囲外の領域は0が入る。</param>
        /// <returns></returns>
        public double[] GetPcmInDouble(int count) {
            // 確保するサイズはcount個。
            if (totalSamples <= offsBytes / (bitsPerSample / 8) || count <= 0) {
                return new double[count];
            }

            var result = new double[count];

            // コピーするデータの個数はcount個よりも少ないことがある。
            int copyCount = result.Length;
            if (totalSamples < offsBytes / (bitsPerSample / 8) + copyCount) {
                copyCount = (int)(totalSamples - offsBytes / (bitsPerSample / 8));
            }

            switch (bitsPerSample) {
            case 16:
                for (int i = 0; i < copyCount; ++i) {
                    short v = (short)((data.At(offsBytes)) + (data.At(offsBytes + 1) << 8));
                    result[i] = v * (1.0 / 32768.0);
                    offsBytes += 2;
                }
                break;
            case 24:
                for (int i = 0; i < copyCount; ++i) {
                    int v = (int)((data.At(offsBytes) << 8) + (data.At(offsBytes + 1) << 16)
                        + (data.At(offsBytes + 2) << 24));
                    result[i] = v * (1.0 / 2147483648.0);
                    offsBytes += 3;
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            return result;
        }

        /// <summary>
        /// double型のLargeArrayを戻すバージョン。
        /// </summary>
        /// <param name="longCount">取得する要素数。範囲外の領域は0が入る。</param>
        /// <returns></returns>
        public PcmDataLib.LargeArray<double> GetPcmInDouble(long longCount) {
            // 確保するサイズはlongCount個。
            var result = new PcmDataLib.LargeArray<double>(longCount);

            long fromPos = offsBytes / (bitsPerSample / 8);
            long toPos = 0;
            
            // コピーするデータの個数はcount個よりも少ないことがある。
            long copyCount = result.LongLength;
            if (totalSamples < fromPos + copyCount) {
                copyCount = (int)(totalSamples - fromPos);
            }

            long remain = copyCount;

            for (long i = 0; i < copyCount; i += PcmDataLib.LargeArray<byte>.ARRAY_FRAGMENT_LENGTH_MAX) {
                int fragmentCount = PcmDataLib.LargeArray<byte>.ARRAY_FRAGMENT_LENGTH_MAX;
                if (totalSamples < fromPos + fragmentCount) {
                    fragmentCount = (int)(totalSamples - fromPos);
                }
                if (remain < fragmentCount) {
                    fragmentCount = (int)remain;
                }

                var fragment = GetPcmInDouble(fragmentCount);

                result.CopyFrom(fragment, 0, toPos, fragmentCount);

                toPos += fragmentCount;
                fromPos += fragmentCount;
                remain -= fragmentCount;
            }

            return result;
        }

        /// <param name="pcm">コピー元データ。先頭から全要素コピーする。</param>
        /// <param name="writeOffsCount">コピー先(この配列)先頭要素数。</param>
        public void SetPcmInDouble(double[] pcm, long writeOffsCount) {
            if (bitsPerSample == 1 && 0 != (writeOffsCount & 7)) {
                throw new ArgumentException("writeOffs must be multiple of 8");
            }

            int copyCount = pcm.Length;
            if (totalSamples < writeOffsCount + copyCount) {
                copyCount = (int)(totalSamples - writeOffsCount);
            }

            long writePosBytes;
            switch (bitsPerSample) {
            case 1: {
                    long readPos = 0;

                    // set 1bit data (from LSB to MSB) into 8bit buffer
                    writePosBytes = writeOffsCount / 8;
                    for (long i = 0; i < copyCount / 8; ++i) {
                        byte sampleValue = 0;
                        for (int subPos = 0; subPos < 8; ++subPos) {
                            byte bit = (0 <= pcm[readPos]) ? (byte)(1 << subPos) : (byte)0;
                            sampleValue |= bit;

                            ++readPos;
                        }
                        data.Set(writePosBytes, sampleValue);
                        ++writePosBytes;
                    }
                }
                break;
            case 16:
                writePosBytes = writeOffsCount * 2;
                for (long i = 0; i < copyCount; ++i) {
                    short vS = 0;
                    double vD = pcm[i];
                    if (vD < -1.0f) {
                        vS = -32768;

                        overflow = true;
                        if (maxMagnitude < Math.Abs(vD)) {
                            maxMagnitude = Math.Abs(vD);
                        }
                    } else if (1.0f <= vD) {
                        vS = 32767;

                        overflow = true;
                        if (maxMagnitude < Math.Abs(vD)) {
                            maxMagnitude = Math.Abs(vD);
                        }
                    } else {
                        vS = (short)(32768.0 * vD);
                    }

                    data.Set(writePosBytes + 0, (byte)((vS) & 0xff));
                    data.Set(writePosBytes + 1, (byte)((vS >> 8) & 0xff));

                    writePosBytes += 2;
                }
                break;
            case 24:
                writePosBytes = writeOffsCount * 3;
                for (long i = 0; i < copyCount; ++i) {
                    int vI = 0;
                    double vD = pcm[i];
                    if (vD < -1.0f) {
                        vI = Int32.MinValue;

                        overflow = true;
                        if (maxMagnitude < Math.Abs(vD)) {
                            maxMagnitude = Math.Abs(vD);
                        }
                    } else if (1.0f <= vD) {
                        vI = 0x7fffff00;

                        overflow = true;
                        if (maxMagnitude < Math.Abs(vD)) {
                            maxMagnitude = Math.Abs(vD);
                        }
                    } else {
                        vI = (int)(2147483648.0 * vD);
                    }

                    data.Set(writePosBytes + 0, (byte)((vI >> 8) & 0xff));
                    data.Set(writePosBytes + 1, (byte)((vI >> 16) & 0xff));
                    data.Set(writePosBytes + 2, (byte)((vI >> 24) & 0xff));

                    writePosBytes += 3;
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        /// <summary>
        /// double型のLargeArrayを入力するバージョン。
        /// </summary>
        public void SetPcmInDouble(PcmDataLib.LargeArray<double> pcm, long writeOffsCount) {
            long fromPos = 0;
            long toPos = writeOffsCount;

            for (long i = 0; i < pcm.LongLength; i += PcmDataLib.LargeArray<byte>.ARRAY_FRAGMENT_LENGTH_MAX) {
                int fragmentCount = PcmDataLib.LargeArray<byte>.ARRAY_FRAGMENT_LENGTH_MAX;
                if (pcm.LongLength < fromPos + fragmentCount) {
                    fragmentCount = (int)(pcm.LongLength - fromPos);
                }

                var fragment = new double[fragmentCount];
                pcm.CopyTo(fromPos, fragment, 0, fragmentCount);
                SetPcmInDouble(fragment, toPos);

                fromPos += fragmentCount;
                toPos += fragmentCount;
            }
        }
    };
}
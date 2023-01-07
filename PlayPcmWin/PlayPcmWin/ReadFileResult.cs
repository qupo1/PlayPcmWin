using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace PlayPcmWin {
    abstract class ReadFileResult {
        public bool IsSucceeded { get; set; }
        public bool HasMessage { get; set; }
        public int PcmDataId { get; set; }
        public abstract string ToString(string fileName);
    }

    class ReadFileResultSuccess : ReadFileResult {
        public ReadFileResultSuccess(int pcmDataId) {
            PcmDataId = pcmDataId;
            IsSucceeded = true;
            HasMessage = false;
        }

        public override string ToString(string fileName) {
            return string.Empty;
        }
    };

    class ReadFileResultFailed : ReadFileResult {
        private string message;

        public ReadFileResultFailed(int pcmDataId, string message) {
            PcmDataId = pcmDataId;
            this.message = message;
            IsSucceeded = false;
            HasMessage = !String.IsNullOrEmpty(this.message);
        }

        public override string ToString(string fileName) {
            return message;
        }
    };

    class ReadFileResultClipped : ReadFileResult {
        private long clippedCount;

        public ReadFileResultClipped(int pcmDataId, long clippedCount) {
            PcmDataId = pcmDataId;
            this.clippedCount = clippedCount;
            IsSucceeded = false;
            HasMessage = true;
        }

        public override string ToString(string fileName) {
            return string.Format(CultureInfo.InvariantCulture, Properties.Resources.ClippedSampleDetected,
                    fileName, clippedCount);
        }
    };

    class ReadFileResultMD5Sum : ReadFileResult {
        private byte[] md5SumOfPcm;
        private byte[] md5SumInMetadata;

        public ReadFileResultMD5Sum(int pcmDataId, byte[] md5SumOfPcm, byte[] md5SumInMetadata) {
            PcmDataId = pcmDataId;
            this.md5SumOfPcm = md5SumOfPcm;
            this.md5SumInMetadata = md5SumInMetadata;
            if (null == md5SumInMetadata) {
                // MD5値がメタ情報から取得できなかったので照合は行わずに成功を戻す。
                IsSucceeded = true;
            } else {
                IsSucceeded = md5SumOfPcm.SequenceEqual(md5SumInMetadata);
            }
            HasMessage = true;
        }

        public override string ToString(string fileName) {
            if (null == md5SumInMetadata) {
                return string.Format(CultureInfo.InvariantCulture, Properties.Resources.MD5SumNotAvailable,
                    fileName, MD5SumToStr(md5SumOfPcm)) + Environment.NewLine;
            }

            if (IsSucceeded) {
                return string.Format(CultureInfo.InvariantCulture, Properties.Resources.MD5SumValid,
                    fileName, MD5SumToStr(md5SumInMetadata)) + Environment.NewLine;
            }

            return string.Format(CultureInfo.InvariantCulture, Properties.Resources.MD5SumMismatch,
                    fileName, MD5SumToStr(md5SumInMetadata), MD5SumToStr(md5SumOfPcm)) + Environment.NewLine;

        }

        private static string MD5SumToStr(byte[] a) {
            if (null == a) {
                return "NA";
            }
            return string.Format(CultureInfo.InvariantCulture,
                    "{0:x2}{1:x2}{2:x2}{3:x2}{4:x2}{5:x2}{6:x2}{7:x2}{8:x2}{9:x2}{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}",
                    a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7],
                    a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15]);
        }
    };
}

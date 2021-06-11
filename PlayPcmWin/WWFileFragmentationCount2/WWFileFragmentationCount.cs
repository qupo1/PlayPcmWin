using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace WWFileFragmentationCount2 {
    class WWFileFragmentationCount {
        public class LcnVcn {
            public long startLcn;
            public long nextVcn;
        };

        public class FileFragmentationInfo {
            public long nClusters;
            public int bytesPerSector;
            public int bytesPerCluster;
            public int nFragmentCount;
            public long startVcn;
            public LcnVcn[] lcnVcn;
        };

        // C++層の WW_VCN_LCN_COUNT と同じ数にする。
        public const int TRUNCATE_FRAGMENT_NUM = 256;

        public FileFragmentationInfo Run(string filePath) {
            NativeMethods.WWFileFragmentationInfo nffi = new NativeMethods.WWFileFragmentationInfo();

            int hr = NativeMethods.WWFileFragmentationCount(filePath, ref nffi);
            if (hr != 0) {
                Console.WriteLine("Error: WWFileFragmentationCount failed {0}", hr);
                return null;
            }

            FileFragmentationInfo r = new FileFragmentationInfo();
            r.nClusters = nffi.nClusters;
            r.bytesPerSector = nffi.bytesPerSector;
            r.bytesPerCluster = nffi.bytesPerCluster;
            r.nFragmentCount = nffi.nFragmentCount;
            r.startVcn = nffi.startVcn;

            int n = r.nFragmentCount;
            if (TRUNCATE_FRAGMENT_NUM < n) {
                n = TRUNCATE_FRAGMENT_NUM;
            }

            r.lcnVcn = new LcnVcn[n];
            for (int i = 0; i < n; ++i) {
                r.lcnVcn[i] = new LcnVcn();
                r.lcnVcn[i].startLcn = nffi.lcnVcn[i].startLcn;
                r.lcnVcn[i].nextVcn  = nffi.lcnVcn[i].nextVcn;
            }

            return r;
        }

        internal static class NativeMethods {
            [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
            internal struct NativeLcnVcn {
                public long startLcn;
                public long nextVcn;
            };

            [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
            internal struct WWFileFragmentationInfo {
                public long nClusters;
                public int bytesPerSector;
                public int bytesPerCluster;
                public int nFragmentCount;
                public int reserved0;
                public long startVcn;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
                public NativeLcnVcn [] lcnVcn;
            };

            [DllImport("WWFileFragmentationCountDLL.dll", CharSet = CharSet.Unicode)]
            internal extern static
            int WWFileFragmentationCount(string filePath, ref WWFileFragmentationInfo ffi_return);
        }
    }
}


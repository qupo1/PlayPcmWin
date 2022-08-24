using System.Runtime.InteropServices;
using System.Text;

namespace WWAsmCs {
    public class WWAsm {
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        public struct SimdCapability {
            // x64 CPUには、MMX, SSE, SSE2が必ずある。

            public byte SSE;
            public byte SSE2;
            public byte SSE3;
            public byte SSSE3;

            public byte SSE41;
            public byte SSE42;
            public byte AVX;
            public byte AVX2;

            public byte AVXVNNI; //< Alder-Lake (AVX512-VNNIよりも新しい。)
        };

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        public struct Avx512Capability {
            public byte AVX512F;
            public byte AVX512CD;
            public byte AVX512ER;
            public byte AVX512PF;

            public byte AVX5124FMAPS;
            public byte AVX5124VNNIW;
            public byte AVX512VPOPCNTDQ;
            public byte AVX512VL;

            public byte AVX512DQ;
            public byte AVX512BW;
            public byte AVX512IFMA;
            public byte AVX512VBMI;

            public byte AVX512VBMI2;
            public byte AVX512BITALG;
            public byte AVX512VNNI; //< Ice-Lake
            public byte AVX512BF16;

            public byte AVX512VPCLMULQDQ;
            public byte AVX512GFNI;
            public byte AVX512VAES;
            public byte AVX512VP2INTERSECT;
        };

        public string CpuCapabilityStr() {
            var sb = new StringBuilder();

            var sc = new SimdCapability();
            var ac = new Avx512Capability();
            GetCpuCapability(ref sc, ref ac);

            if (0 != sc.SSE) sb.Append("SSE ");
            if (0 != sc.SSE2) sb.Append("SSE2 ");
            if (0 != sc.SSSE3) sb.Append("SSSE3 ");
            if (0 != sc.AVX) sb.Append("AVX ");
            if (0 != sc.AVX2) sb.Append("AVX2 ");
            if (0 != ac.AVX512F) sb.Append("AVX-512F ");
            if (0 != ac.AVX512BW) sb.Append("AVX-512BW ");

            return sb.ToString();
        }

        public void GetCpuCapability(ref SimdCapability sc, ref Avx512Capability ac) {
            WWGetCpuCapability(ref sc, ref ac);
        }

        [DllImport("WWAsmDLL.dll", CharSet = CharSet.Unicode)]
        internal extern static int
        WWGetCpuCapability(ref SimdCapability sc, ref Avx512Capability ac);

    }
}

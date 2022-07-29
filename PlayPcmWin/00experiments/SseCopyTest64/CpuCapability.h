#pragma once

struct CpuCapability {
    // x64 CPU�ɂ́AMMX, SSE, SSE2���K������B

    bool SSE3;
    bool SSSE3;
    bool SSE41;
    bool SSE42;

    bool AVX;
    bool AVX2;
    bool AVXVNNI; //< Alder-Lake (AVX512-VNNI�����V�����B)
};

struct Avx512Capability {
    bool AVX512F;
    bool AVX512DQ;
    bool AVX512IFMA;
    bool AVX512CD;

    bool AVX512BW;
    bool AVX512VL;
    bool AVX512VBMI;
    bool AVX512VBMI2;

    bool AVX512GFNI;
    bool AVX512VAES;
    bool AVX512VPCLMULQDQ;
    bool AVX512VNNI; //< Ice-Lake

    bool AVX512BITALG;
    bool AVX512VPOPCNTDQ;
    bool AVX512VP2INTERSECT;
    bool AVX512BF16;
};

void GetCpuCapability(CpuCapability * cap_return, Avx512Capability *avx512_return);

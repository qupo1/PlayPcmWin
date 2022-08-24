#include "SimdCapability.h"
#include <intrin.h>
#include <stdint.h>
#include <sstream>

// Intel 64 and IA-32v Architectures Software Developer's Manual Vol.2A 3-214 to 3-218

static const uint32_t R10_ECX_SSE3_BIT  = 0x00000001;
static const uint32_t R10_ECX_SSSE3_BIT = 0x00000200;
static const uint32_t R10_ECX_SSE41_BIT = 0x00080000;
static const uint32_t R10_ECX_SSE42_BIT = 0x00100000;
static const uint32_t R10_ECX_AVX_BIT   = 0x10000000;
static const uint32_t R10_ECX_F16C_BIT  = 0x20000000;

// Vol.2A 3-239 Figure 3-8 
static const uint32_t R10_EDX_SSE_BIT   = 0x20000000;
static const uint32_t R10_EDX_SSE2_BIT  = 0x40000000;

static const uint32_t R70_EBX_AVX2_BIT         = 0x00000020;
static const uint32_t R70_EBX_AVX512f_BIT      = 0x00010000;
static const uint32_t R70_EBX_AVX512dq_BIT     = 0x00020000;
static const uint32_t R70_EBX_AVX512ifma_BIT   = 0x00200000;

static const uint32_t R70_EBX_AVX512pf_BIT     = 0x04000000;
static const uint32_t R70_EBX_AVX512er_BIT     = 0x08000000;
static const uint32_t R70_EBX_AVX512cd_BIT     = 0x10000000;
static const uint32_t R70_EBX_AVX512bw_BIT     = 0x40000000;
static const uint32_t R70_EBX_AVX512vl_BIT     = 0x80000000;

static const uint32_t R70_ECX_AVX512vbmi_BIT   = 0x00000002;
static const uint32_t R70_ECX_AVX512vbmi2_BIT  = 0x00000040;
static const uint32_t R70_ECX_AVX512gfni_BIT   = 0x00000100;
static const uint32_t R70_ECX_AVX512vaes_BIT   = 0x00000200;
static const uint32_t R70_ECX_AVX512vpclmulqdq_BIT = 0x00000400;
static const uint32_t R70_ECX_AVX512vnni_BIT   = 0x00000800;
static const uint32_t R70_ECX_AVX512bitalg_BIT = 0x00001000;
static const uint32_t R70_ECX_AVX512vpopcntdq_BIT = 0x00004000;

static const uint32_t R70_EDX_AVX5124VNNIW_BIT = 0x00000002;
static const uint32_t R70_EDX_AVX5124FMAPS_BIT = 0x00000004;
static const uint32_t R70_EDX_AVX512vp2intersect_BIT = 0x00000100;

static const uint32_t R71_EAX_AVXvnni_BIT = 0x00000010;
static const uint32_t R71_EAX_AVX512bf16_BIT = 0x00000020;

static const uint32_t XGETBV_YMM_SAVE_BITS = 0x6;
static const uint32_t XGETBV_ZMM_SAVE_BITS = 0xe;

struct Regs {
    uint32_t eax;
    uint32_t ebx;
    uint32_t ecx;
    uint32_t edx;
};

void
WWGetSimdCapability(SimdCapability *s)
{
    Regs r10;
    memset(&r10.eax, 0, sizeof r10);

    uint32_t xcr0 = (uint32_t)_xgetbv(0);
    bool OS_SAVE_AVX    = ((xcr0 & XGETBV_YMM_SAVE_BITS) == XGETBV_YMM_SAVE_BITS);

    __cpuidex((int *)&r10.eax, 1, 0);

    s->SSE   = 0 != (r10.edx & R10_EDX_SSE_BIT);
    s->SSE2  = 0 != (r10.edx & R10_EDX_SSE2_BIT);

    s->SSE3  = 0 != (r10.ecx & R10_ECX_SSE3_BIT);
    s->SSSE3 = 0 != (r10.ecx & R10_ECX_SSSE3_BIT);
    s->SSE41 = 0 != (r10.ecx & R10_ECX_SSE41_BIT);
    s->SSE42 = 0 != (r10.ecx & R10_ECX_SSE42_BIT);

    s->AVX   = OS_SAVE_AVX && (0 != (r10.ecx & R10_ECX_AVX_BIT));

    Regs r70;
    memset(&r70.eax, 0, sizeof r70);
    __cpuidex((int *)&r70.eax, 7, 0);
    s->AVX2  = OS_SAVE_AVX && (0 != (r70.ebx & R70_EBX_AVX2_BIT));

    Regs r71;
    memset(&r71.eax, 0, sizeof r71);
    __cpuidex((int *)&r71.eax, 7, 1);
    s->AVXVNNI  = OS_SAVE_AVX && (0 != (r70.ebx & R71_EAX_AVXvnni_BIT));
}

void
WWGetAvx512Capability(Avx512Capability *s)
{
    uint32_t xcr0 = (uint32_t)_xgetbv(0);
    bool OS_SAVE_AVX512 = ((xcr0 & XGETBV_YMM_SAVE_BITS) == XGETBV_YMM_SAVE_BITS);

    Regs r70;
    memset(&r70.eax, 0, sizeof r70);
    __cpuidex((int *)&r70.eax, 7, 0);

    Regs r71;
    memset(&r71.eax, 0, sizeof r71);
    __cpuidex((int *)&r71.eax, 7, 1);

    s->AVX512F     = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512f_BIT));
    s->AVX512DQ    = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512dq_BIT));
    s->AVX512IFMA  = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512ifma_BIT));
    s->AVX512PF    = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512pf_BIT));
    s->AVX512ER    = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512er_BIT));

    s->AVX512CD    = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512cd_BIT));
    s->AVX512BW    = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512bw_BIT));
    s->AVX512VL    = OS_SAVE_AVX512 && (0 != (r70.ebx & R70_EBX_AVX512vl_BIT));
    s->AVX512VBMI  = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512vbmi_BIT));
    s->AVX512VBMI2 = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512vbmi2_BIT));

    s->AVX512GFNI       = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512gfni_BIT));
    s->AVX512VAES       = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512vaes_BIT));
    s->AVX512VPCLMULQDQ = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512vpclmulqdq_BIT));
    s->AVX512VNNI       = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512vnni_BIT));

    s->AVX512BITALG       = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512bitalg_BIT));
    s->AVX512VPOPCNTDQ    = OS_SAVE_AVX512 && (0 != (r70.ecx & R70_ECX_AVX512vpopcntdq_BIT));

    s->AVX5124FMAPS       = OS_SAVE_AVX512 && (0 != (r70.edx & R70_EDX_AVX5124VNNIW_BIT));
    s->AVX5124VNNIW       = OS_SAVE_AVX512 && (0 != (r70.edx & R70_EDX_AVX5124FMAPS_BIT));
    s->AVX512VP2INTERSECT = OS_SAVE_AVX512 && (0 != (r70.edx & R70_EDX_AVX512vp2intersect_BIT));
    s->AVX512BF16         = OS_SAVE_AVX512 && (0 != (r70.edx & R71_EAX_AVX512bf16_BIT));
}

std::string
WWSimdCapabilityToStr(SimdCapability *s)
{
    std::stringstream ss;

    if (s->SSE3) { ss << "SSE3 "; }
    if (s->SSSE3) { ss << "SSSE3 "; }
    if (s->SSE41) { ss << "SSE4.1 "; }
    if (s->SSE42) { ss << "SSE4.2 "; }
    if (s->AVX) { ss << "AVX "; }
    if (s->AVX2) { ss << "AVX2 "; }
    if (s->AVXVNNI) { ss << "AVXVNNI "; } //< AVX-VNNIはAVX512-VNNIとは別の機能で、より新しい。

    return ss.str();
}

std::string
WWAvx512CapabilityToStr(Avx512Capability *s)
{
    std::stringstream ss;

    // https://en.wikipedia.org/wiki/Advanced_Vector_Extensions#AVX-512 の順に表示。

    if (s->AVX512F) {
        ss << "AVX512( F ";
        if (s->AVX512CD) { ss << "CD "; }
        if (s->AVX512ER) { ss << "ER "; }
        if (s->AVX512PF) { ss << "PF "; }
        if (s->AVX5124FMAPS) { ss << "4FMAPS "; }
        if (s->AVX5124VNNIW) { ss << "4VNNIW "; }

        if (s->AVX512VPOPCNTDQ) { ss << "VPOPCNTDQ "; }
        if (s->AVX512VL) { ss << "VL "; }
        if (s->AVX512DQ) { ss << "DQ "; }
        if (s->AVX512BW) { ss << "BW "; }
        if (s->AVX512IFMA) { ss << "IFMA "; }

        if (s->AVX512VBMI) { ss << "VBMI "; }
        if (s->AVX512VBMI2) { ss << "VBMI2 "; }
        if (s->AVX512BITALG) { ss << "BITALG "; }
        if (s->AVX512VNNI) { ss << "AVX512-VNNI "; }
        if (s->AVX512BF16) { ss << "BF16 "; }

        if (s->AVX512VPCLMULQDQ) { ss << "VPCLMULQDQ "; }
        if (s->AVX512GFNI) { ss << "GFNI "; }
        if (s->AVX512VAES) { ss << "VAES "; }
        if (s->AVX512VP2INTERSECT) { ss << "VP2INTERSECT "; }
        ss << ")";
        return ss.str();
    } else {
        return "";
    }
}



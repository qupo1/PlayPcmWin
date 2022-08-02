#include "SimdCapability.h"
#include <intrin.h>
#include <stdint.h>
#include <sstream>

// intel processor manual 3-218

static const uint32_t R10_ECX_SSE3_BIT  = 0x00000001;
static const uint32_t R10_ECX_SSSE3_BIT = 0x00000200;
static const uint32_t R10_ECX_SSE41_BIT = 0x00080000;
static const uint32_t R10_ECX_SSE42_BIT = 0x00100000;
static const uint32_t R10_ECX_AVX_BIT   = 0x10000000;
static const uint32_t R10_ECX_F16C_BIT  = 0x20000000;

static const uint32_t R70_EBX_AVX2_BIT         = 0x00000020;
static const uint32_t R70_EBX_AVX512f_BIT      = 0x00010000;
static const uint32_t R70_EBX_AVX512dq_BIT     = 0x00020000;
static const uint32_t R70_EBX_AVX512ifma_BIT   = 0x00200000;
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

SimdCapability::SimdCapability(void)
{
    Regs r10;

    uint32_t xcr0 = (uint32_t)_xgetbv(0);
    bool OS_SAVE_AVX    = ((xcr0 & XGETBV_YMM_SAVE_BITS) == XGETBV_YMM_SAVE_BITS);

    __cpuidex((int *)&r10.eax, 1, 0);


    SSE3  = 0 != (r10.ecx & R10_ECX_SSE3_BIT);
    SSSE3 = 0 != (r10.ecx & R10_ECX_SSSE3_BIT);
    SSE41 = 0 != (r10.ecx & R10_ECX_SSE41_BIT);
    SSE42 = 0 != (r10.ecx & R10_ECX_SSE42_BIT);

    AVX   = OS_SAVE_AVX && 0 != (r10.ecx & R10_ECX_AVX_BIT);

    Regs r70;
    __cpuidex((int *)&r70.eax, 7, 0);
    AVX2  = OS_SAVE_AVX && 0 != (r70.ebx & R70_EBX_AVX2_BIT);

    Regs r71;
    __cpuidex((int *)&r71.eax, 7, 1);
    AVXVNNI  = OS_SAVE_AVX && 0 != (r70.ebx & R71_EAX_AVXvnni_BIT);
}

std::string
SimdCapability::ToString(void)
{
    std::stringstream ss;

    if (SSE3) { ss << "SSE3 "; }
    if (SSSE3) { ss << "SSSE3 "; }
    if (SSE41) { ss << "SSE4.1 "; }
    if (SSE42) { ss << "SSE4.2 "; }
    if (AVX) { ss << "AVX "; }
    if (AVX2) { ss << "AVX2 "; }
    if (AVXVNNI) { ss << "AVXVNNI "; } //< AVX-VNNIはAVX512-VNNIとは別の機能で、より新しい。

    return ss.str();
}

// ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■


Avx512Capability::Avx512Capability(void)
{
    uint32_t xcr0 = (uint32_t)_xgetbv(0);
    bool OS_SAVE_AVX512 = ((xcr0 & XGETBV_YMM_SAVE_BITS) == XGETBV_YMM_SAVE_BITS);

    Regs r70;
    __cpuidex((int *)&r70.eax, 7, 0);

    Regs r71;
    __cpuidex((int *)&r71.eax, 7, 1);

    AVX512F    = OS_SAVE_AVX512 && 0 != (r70.ebx & R70_EBX_AVX512f_BIT);
    AVX512DQ   = OS_SAVE_AVX512 && 0 != (r70.ebx & R70_EBX_AVX512dq_BIT);
    AVX512IFMA = OS_SAVE_AVX512 && 0 != (r70.ebx & R70_EBX_AVX512ifma_BIT);
    AVX512CD   = OS_SAVE_AVX512 && 0 != (r70.ebx & R70_EBX_AVX512cd_BIT);

    AVX512BW    = OS_SAVE_AVX512 && 0 != (r70.ebx & R70_EBX_AVX512bw_BIT);
    AVX512VL    = OS_SAVE_AVX512 && 0 != (r70.ebx & R70_EBX_AVX512vl_BIT);
    AVX512VBMI  = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512vbmi_BIT);
    AVX512VBMI2 = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512vbmi2_BIT);

    AVX512GFNI       = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512gfni_BIT);
    AVX512VAES       = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512vaes_BIT);
    AVX512VPCLMULQDQ = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512vpclmulqdq_BIT);
    AVX512VNNI       = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512vnni_BIT);

    AVX512BITALG       = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512bitalg_BIT);
    AVX512VPOPCNTDQ    = OS_SAVE_AVX512 && 0 != (r70.ecx & R70_ECX_AVX512vpopcntdq_BIT);
    AVX512VP2INTERSECT = OS_SAVE_AVX512 && 0 != (r70.edx & R70_EDX_AVX512vp2intersect_BIT);
    AVX512BF16         = OS_SAVE_AVX512 && 0 != (r70.edx & R71_EAX_AVX512bf16_BIT);
}

std::string
Avx512Capability::ToString(void)
{
    std::stringstream ss;

    // https://en.wikipedia.org/wiki/Advanced_Vector_Extensions#AVX-512 の順に表示。

    if (AVX512F) {
        ss << "AVX512( F ";
        if (AVX512CD) { ss << "CD "; }
        if (AVX512VPOPCNTDQ) { ss << "VPOPCNTDQ "; }
        if (AVX512VL) { ss << "VL "; }
        if (AVX512DQ) { ss << "DQ "; }
        if (AVX512BW) { ss << "BW "; }
        if (AVX512IFMA) { ss << "IFMA "; }
        if (AVX512VBMI) { ss << "VBMI "; }
        if (AVX512VBMI2) { ss << "VBMI2 "; }
        if (AVX512BITALG) { ss << "BITALG "; }
        if (AVX512VNNI) { ss << "AVX512-VNNI "; }
        if (AVX512BF16) { ss << "BF16 "; }
        if (AVX512VPCLMULQDQ) { ss << "VPCLMULQDQ "; }
        if (AVX512GFNI) { ss << "GFNI "; }
        if (AVX512VAES) { ss << "VAES "; }
        if (AVX512VP2INTERSECT) { ss << "VP2INTERSECT "; }
        ss << ")";
        return ss.str();
    } else {
        return "";
    }
}

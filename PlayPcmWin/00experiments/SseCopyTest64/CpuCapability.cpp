#include "CpuCapability.h"
#include <intrin.h>
#include <stdint.h>

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

struct Regs {
    uint32_t eax;
    uint32_t ebx;
    uint32_t ecx;
    uint32_t edx;
};

void
GetCpuCapability(CpuCapability * cap, Avx512Capability *avx512)
{
    Regs r10;

    if (cap != nullptr) {
        __cpuidex((int *)&r10.eax, 1, 0);
        cap->SSE3  = 0 != (r10.ecx & R10_ECX_SSE3_BIT);
        cap->SSSE3 = 0 != (r10.ecx & R10_ECX_SSSE3_BIT);
        cap->SSE41 = 0 != (r10.ecx & R10_ECX_SSE41_BIT);
        cap->SSE42 = 0 != (r10.ecx & R10_ECX_SSE42_BIT);

        cap->AVX   = 0 != (r10.ecx & R10_ECX_AVX_BIT);

        Regs r70;
        __cpuidex((int *)&r70.eax, 7, 0);
        cap->AVX2  = 0 != (r70.ebx & R70_EBX_AVX2_BIT);

        Regs r71;
        __cpuidex((int *)&r71.eax, 7, 1);
        cap->AVXVNNI  = 0 != (r70.ebx & R71_EAX_AVXvnni_BIT);
    }

    if (avx512 != nullptr) {
        Regs r70;
        __cpuidex((int *)&r70.eax, 7, 0);

        Regs r71;
        __cpuidex((int *)&r71.eax, 7, 1);

        avx512->AVX512F    = 0 != (r70.ebx & R70_EBX_AVX512f_BIT);
        avx512->AVX512DQ   = 0 != (r70.ebx & R70_EBX_AVX512dq_BIT);
        avx512->AVX512IFMA = 0 != (r70.ebx & R70_EBX_AVX512ifma_BIT);
        avx512->AVX512CD   = 0 != (r70.ebx & R70_EBX_AVX512cd_BIT);

        avx512->AVX512BW    = 0 != (r70.ebx & R70_EBX_AVX512bw_BIT);
        avx512->AVX512VL    = 0 != (r70.ebx & R70_EBX_AVX512vl_BIT);
        avx512->AVX512VBMI  = 0 != (r70.ecx & R70_ECX_AVX512vbmi_BIT);
        avx512->AVX512VBMI2 = 0 != (r70.ecx & R70_ECX_AVX512vbmi2_BIT);

        avx512->AVX512GFNI       = 0 != (r70.ecx & R70_ECX_AVX512gfni_BIT);
        avx512->AVX512VAES       = 0 != (r70.ecx & R70_ECX_AVX512vaes_BIT);
        avx512->AVX512VPCLMULQDQ = 0 != (r70.ecx & R70_ECX_AVX512vpclmulqdq_BIT);
        avx512->AVX512VNNI       = 0 != (r70.ecx & R70_ECX_AVX512vnni_BIT);

        avx512->AVX512BITALG       = 0 != (r70.ecx & R70_ECX_AVX512bitalg_BIT);
        avx512->AVX512VPOPCNTDQ    = 0 != (r70.ecx & R70_ECX_AVX512vpopcntdq_BIT);
        avx512->AVX512VP2INTERSECT = 0 != (r70.edx & R70_EDX_AVX512vp2intersect_BIT);
        avx512->AVX512BF16         = 0 != (r70.edx & R71_EAX_AVX512bf16_BIT);
    }

}



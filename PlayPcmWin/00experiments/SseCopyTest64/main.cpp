#include <Windows.h> //< QueryPerformanceCounter()
#include <stdio.h>  //< printf()
#include <string.h> //< memset()
#include <malloc.h> //< _aligned_malloc()
#include <assert.h> //< assert()
#include "MyMemcpy2.h"
#include "PCM16to24.h"
#include "PCM16to32.h"
#include "PCM16toF32.h"
#include "PCM24to32.h"
#include "PCM24toF32.h"
#include "SimdCapability.h"
#include "WWStopwatch.h"

#define SHOW_SIMD_CAP (1)

#define INIT_MEM (1)

#define COMPARE_WITH_CPP (1)

#define FRACTION_TEST (1)

#if FRACTION_TEST
int64_t NUM_OF_ITEMS = 100LL * 1024 * 1024 + 7;
#else
int64_t NUM_OF_ITEMS = 100LL * 1024 * 1024;
#endif


// ASMとの性能比較用C++実装。
static void
Pcm16to32CPP(const int16_t *from, int32_t *to, int64_t n)
{
    for (int64_t i=0; i<n; ++i) {
        to[i] = from[i] << 16;
    }
}

static void
Pcm16toF32CPP(const int16_t *from, float *to, int64_t n)
{
    for (int64_t i=0; i<n; ++i) {
        to[i] = from[i] * (1.0f / 32768.0f);
    }
}

static void
Pcm16to24CPP(const int16_t *src, uint8_t *dst, int64_t n)
{
    for (int i=0; i<n; ++i) {
        uint16_t v = src[i];

        dst[i *3 + 0] = 0;
        dst[i *3 + 1] = v & 0xff;
        dst[i *3 + 2] = (v>>8) & 0xff;
    }
}

static void
Pcm24to32CPP(const uint8_t *src, int32_t *dst, int64_t n)
{
    for (int i=0; i<n; ++i) {
        dst[i] = (src[i*3+2] << 24)
               + (src[i*3+1] << 16)
               + (src[i*3+0] << 8);
    }
}

static void
Pcm24toF32CPP(const uint8_t *src, float *dst, int64_t n)
{
    for (int i=0; i<n; ++i) {
        int32_t v = (src[i*3+2] << 24)
              + (src[i*3+1] << 16)
              + (src[i*3+0] << 8);
        dst[i] = ((float)v) / (32768.0f * 65536.0f);
    }
}

/*
static void *
slowmemcpy1(void * dst,
        const void * src,
        size_t count)
{
    void * ret = dst;

    while (count--) {
        *(char *)dst = *(char *)src;
        dst = (char *)dst + 1;
        src = (char *)src + 1;
    }

    return ret;
}
*/


static void
TestMemcpy(void)
{
    uint8_t* from = (uint8_t*)_aligned_malloc(NUM_OF_ITEMS, 64);
    uint8_t* to   = (uint8_t*)_aligned_malloc(NUM_OF_ITEMS, 64);
    if (from == nullptr || to == nullptr) {
        printf("Error allocating memory\n");
        return;
    }

#if INIT_MEM
    memset(from, 0x69, NUM_OF_ITEMS);
#endif

#if COMPARE_WITH_CPP
    WWStopwatch pc;

    // test MyMemcpy2 performance
    for (int64_t i = 0; i < NUM_OF_ITEMS; ++i) {
        from[i] = (char)i;
    }

    for (int j=0; j<2; ++j) {
        // ASM計測。
        pc.Start();
        MyMemcpy2(to, from, NUM_OF_ITEMS);
        double elapsedSecAsm = pc.ElapsedSeconds();

        printf("ASM 100M bytes copy in %f sec. %f GB/sec\n",
            elapsedSecAsm, 0.1 / elapsedSecAsm);
        // ASMは怪しいのでチェックする。
        for (int64_t i = 0; i < NUM_OF_ITEMS; ++i) {
            if (to[i] != (uint8_t)i) {
                printf("Error: to[%lld](%x) != %x\n", i, (uint32_t)to[i], (uint32_t)i);
            }
        }

        // C++計測。
        pc.Start();
        memcpy(to, from, NUM_OF_ITEMS);
        double elapsedSecCpp = pc.ElapsedSeconds();

        printf("C++ 100M bytes copy in %f sec. %f GB/sec\n",
            elapsedSecCpp, 0.1 / elapsedSecCpp);
    }

#else
    MyMemcpy2(to, from, NUM_OF_ITEMS);
#endif

    _aligned_free(to);
    to = nullptr;
    _aligned_free(from);
    from = nullptr;
}

static void
TestPcmConv16to32(void)
{

    // numOfItems個のshort値PCMをint値PCMに変換します。
    int16_t *from  = (int16_t*)_aligned_malloc(NUM_OF_ITEMS*2, 16);
    int32_t *toAsm = (int32_t*)_aligned_malloc(NUM_OF_ITEMS*4, 64);
    int32_t *toCpp = (int32_t*)_aligned_malloc(NUM_OF_ITEMS*4, 16);
    if (from == nullptr || toAsm == nullptr || toCpp == nullptr) {
        printf("Error allocating memory\n");
        return;
    }

#if INIT_MEM
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        from[i] = (int16_t)(i+1);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }
#endif

#if COMPARE_WITH_CPP
    WWStopwatch pc;
    pc.Start();
    PCM16to32(from, toAsm, NUM_OF_ITEMS);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM16to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm16to32CPP(from, toCpp, NUM_OF_ITEMS);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM16to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        }
    }
#else
    PCM16to32(from, toAsm, NUM_OF_ITEMS);
#endif

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from);
    from = nullptr;
}

static void
TestPcmConv16toF32(void)
{
    // numOfItems個のshort値PCMをint値PCMに変換します。
    int16_t *from  = (int16_t*)_aligned_malloc(NUM_OF_ITEMS*2, 16);
    float   *toAsm = (float*)  _aligned_malloc(NUM_OF_ITEMS*4, 16);
    float   *toCpp = (float*)  _aligned_malloc(NUM_OF_ITEMS*4, 16);
    if (from == nullptr || toAsm == nullptr || toCpp == nullptr) {
        printf("Error allocating memory\n");
        return;
    }

#if INIT_MEM
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        from[i] = (int16_t)(i+1);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }
#endif

#if COMPARE_WITH_CPP
    WWStopwatch pc;
    pc.Start();
    PCM16toF32(from, toAsm, NUM_OF_ITEMS);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM16toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm16toF32CPP(from, toCpp, NUM_OF_ITEMS);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM16toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %f %f\n", i, toAsm[i], toCpp[i]);
        }
    }
#else
    PCM16toF32(from, toAsm, NUM_OF_ITEMS);
#endif

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from);
    from = nullptr;
}

static void
TestPcmConv24to32(void)
{
    // numOfItems個のshort値PCMをint値PCMに変換します。
    uint8_t *from   = (uint8_t*)_aligned_malloc(NUM_OF_ITEMS*3, 16);
    int32_t *toAsm  = (int32_t*)_aligned_malloc(NUM_OF_ITEMS*4, 32);
    int32_t *toCpp  = (int32_t*)_aligned_malloc(NUM_OF_ITEMS*4, 16);
    if (from == nullptr || toAsm == nullptr || toCpp == nullptr) {
        printf("Error allocating memory\n");
        return;
    }

#if INIT_MEM
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        int32_t v = (int32_t)i*3 + 1;
        from[i*3+0] = (uint8_t)(v);
        from[i*3+1] = (uint8_t)(v+1);
        from[i*3+2] = (uint8_t)(v+2);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }
#endif

#if COMPARE_WITH_CPP
    WWStopwatch pc;
    pc.Start();
    PCM24to32(from, toAsm, NUM_OF_ITEMS);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM24to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);


    pc.Start();
    Pcm24to32CPP(from, toCpp, NUM_OF_ITEMS);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM24to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        } else {
            //printf("OK:    %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        }
    }
#else
    PCM24to32(from, toAsm, NUM_OF_ITEMS);
#endif

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from);
    from = nullptr;
}

static void
TestPcmConv24toF32(void)
{
    // numOfItems個のshort値PCMをint値PCMに変換します。
    uint8_t *from   = (uint8_t*)_aligned_malloc(NUM_OF_ITEMS*3, 16);
    float   *toAsm  = (float*)  _aligned_malloc(NUM_OF_ITEMS*4, 16);
    float   *toCpp  = (float*)  _aligned_malloc(NUM_OF_ITEMS*4, 16);
    if (from == nullptr || toAsm == nullptr || toCpp == nullptr) {
        printf("Error allocating memory\n");
        return;
    }

#if INIT_MEM
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        int32_t v = (int32_t)i*3 + 1;
        from[i*3+0] = (uint8_t)(v);
        from[i*3+1] = (uint8_t)(v+1);
        from[i*3+2] = (uint8_t)(v+2);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }
#endif

#if COMPARE_WITH_CPP
    WWStopwatch pc;
    pc.Start();
    PCM24toF32(from, toAsm, NUM_OF_ITEMS);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM24toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);


    pc.Start();
    Pcm24toF32CPP(from, toCpp, NUM_OF_ITEMS);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM24toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %g %g\n", i, toAsm[i], toCpp[i]);
        } else {
            //printf("OK:    %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        }
    }
#else
    PCM24toF32(from, toAsm, NUM_OF_ITEMS);
#endif

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from);
    from = nullptr;
}

static void
TestPcmConv16to24(void)
{
    // numOfItems個のshort値PCMを24bitPCMに変換します。
    int16_t *from  = (int16_t*) _aligned_malloc(NUM_OF_ITEMS*2, 16);
    uint8_t *toAsm = (uint8_t*) _aligned_malloc(NUM_OF_ITEMS*3, 32);
    uint8_t *toCpp = (uint8_t*) _aligned_malloc(NUM_OF_ITEMS*3, 16);
    if (from == nullptr || toAsm == nullptr || toCpp == nullptr) {
        printf("Error allocating memory\n");
        return;
    }

    uint8_t* fromB = (uint8_t*)from;

#if INIT_MEM
    for (int64_t i=0; i<NUM_OF_ITEMS; ++i) {
        int32_t v = (int32_t)(i*2) + 1;
        fromB[i*2+0] = (uint8_t)(v);
        fromB[i*2+1] = (uint8_t)(v+1);
    }
#endif

#if COMPARE_WITH_CPP
    WWStopwatch pc;
    pc.Start();
    PCM16to24(from, toAsm, NUM_OF_ITEMS);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM16to24  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm16to24CPP(from, toCpp, NUM_OF_ITEMS);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM16to24  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<NUM_OF_ITEMS*3; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %02x %02x\n", i, toAsm[i], toCpp[i]);
        } else {
            // printf("OK:    %llx %02x %02x\n", i, toAsm[i], toCpp[i]);
        }
    }
#else
    PCM16to24(from, toAsm, NUM_OF_ITEMS);
#endif

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from);
    from = nullptr;
    fromB = nullptr;
}

static bool
DumpDataToFile(const char *buf, int32_t bytes, const char *path)
{
    bool rv = false;

    FILE* fp = nullptr;
    errno_t e = fopen_s(&fp, path, "wb");
    if (e != 0 || fp == nullptr) {
        printf("E: DumpDataToFile fopen failed %s\n", path);
        return false;
    }

    size_t r = fwrite(buf, 1, bytes, fp);
    if (r != bytes) {
        printf("E: DumpDataToFile fwrite failed %s\n", path);
        fclose(fp);
        goto end;
    }

    rv = true;

end:
    fclose(fp);
    fp = nullptr;

    return rv;
}


int
main(void)
{
    /*
    {
        float v = 1.0f / 32768.0f / 65536.0f;
        DumpDataToFile((const char *)&v, 4, "div_constant.bin");
        // it is 0x30000000
    }
    */

#if SHOW_SIMD_CAP
    {
        SimdCapability cc;
        Avx512Capability ac;

        printf ("%s %s\n", cc.ToString().c_str(), ac.ToString().c_str());
    }
#endif

    TestPcmConv24to32();
    TestPcmConv16to24();
    TestPcmConv16to32();
    TestMemcpy();
    TestPcmConv16toF32();
    TestPcmConv24toF32();

    return 0;
}

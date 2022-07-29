#include <Windows.h> //< QueryPerformanceCounter()
#include <stdio.h>  //< printf()
#include <string.h> //< memset()
#include <malloc.h> //< _aligned_malloc()
#include <assert.h> //< assert()
#include "MyMemcpy64.h"
#include "PCM16to24.h"
#include "PCM16to32.h"
#include "PCM16toF32.h"
#include "PCM24to32.h"
#include "PCM24toF32.h"
#include "CpuCapability.h"

#define BUFFER_SIZE (8192)


// ASMとの性能比較用C++実装。
static void
Pcm16to32CPP(const int16_t *from, int32_t *to, int64_t numOfItems)
{
    for (int64_t i=0; i<numOfItems; ++i) {
        to[i] = from[i] << 16;
    }
}

static void
Pcm16toF32CPP(const int16_t *from, float *to, int64_t numOfItems)
{
    for (int64_t i=0; i<numOfItems; ++i) {
        to[i] = from[i] * (1.0f / 32768.0f);
    }
}

static void
Pcm16to24CPP(const int16_t *src, uint8_t *dst, int64_t numOfItems)
{
    for (int i=0; i<numOfItems; ++i) {
        uint16_t v = src[i];

        dst[i *3 + 0] = 0;
        dst[i *3 + 1] = v & 0xff;
        dst[i *3 + 2] = (v>>8) & 0xff;
    }
}

static void
Pcm24to32CPP(const uint8_t *src, int32_t *dst, int64_t numOfItems)
{
    for (int i=0; i<numOfItems; ++i) {
        dst[i] = (src[i*3+2] << 24)
               + (src[i*3+1] << 16)
               + (src[i*3+0] << 8);
    }
}

static void
Pcm24toF32CPP(const uint8_t *src, float *dst, int64_t numOfItems)
{
    for (int i=0; i<numOfItems; ++i) {
        int32_t v = (src[i*3+2] << 24)
              + (src[i*3+1] << 16)
              + (src[i*3+0] << 8);
        dst[i] = ((float)v) / (32768.0f * 65536.0f);
    }
}

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

class PerfCount
{
public:
    PerfCount(void) {
        QueryPerformanceFrequency(&freq);
        QueryPerformanceCounter(&start);
    }

    void Start(void) {
        QueryPerformanceCounter(&start);
    }

    double ElapsedSeconds(void) {
        QueryPerformanceCounter(&last);

        return (double)(last.QuadPart - start.QuadPart) / freq.QuadPart;
    }

    double ElapsedMillisec(void) {
        QueryPerformanceCounter(&last);

        return (last.QuadPart - start.QuadPart) * 1000.0 / freq.QuadPart;
    }

private:
    LARGE_INTEGER freq;
    LARGE_INTEGER start;
    LARGE_INTEGER last;
};

static void
TestMemcpy(void)
{
    char *from = (char*)_aligned_malloc(BUFFER_SIZE, 16);
    char *to   = (char*)_aligned_malloc(BUFFER_SIZE, 16);

    PerfCount pc;

    for (int j=0; j<10; ++j) {
        // test memcpy performance
        memset(from, 0x69, BUFFER_SIZE);
        pc.Start();
        for (int i=0; i<100; ++i) {
            memcpy(to, from, BUFFER_SIZE);
        }
        printf("memcpy %f\n", pc.ElapsedMillisec());

        // test MyMemcpy64 performance
        for (int i=0; i<BUFFER_SIZE; ++i) {
            from[i] = (char)i;
        }
        pc.Start();
        for (int i=0; i<100; ++i) {
            MyMemcpy64(to, from, BUFFER_SIZE);
        }
        printf("MyMemcpy64 %f\n", pc.ElapsedMillisec());
        for (int i=0; i<BUFFER_SIZE; ++i) {
            if (to[i] != (char)i) {
                printf("to[%d](%x) != %x\n", i, (int)to[i], (char)i);
            }
        }

        // test MyMemcpy64a performance
        for (int i=0; i<BUFFER_SIZE; ++i) {
            from[i] = (char)(i+69);
        }
        pc.Start();
        for (int i=0; i<100; ++i) {
            MyMemcpy64a(to, from, BUFFER_SIZE);
        }
        printf("MyMemcpy64a %f\n", pc.ElapsedMillisec());
        for (int i=0; i<BUFFER_SIZE; ++i) {
            if (to[i] != (char)(i+69)) {
                printf("to[%d](%x) != %x\n", i, (int)to[i], (char)(i+69));
            }
        }

        // test RtlCopyMemory performance
        for (int i=0; i<BUFFER_SIZE; ++i) {
            from[i] = (char)(i+96);
        }
        pc.Start();
        for (int i=0; i<100; ++i) {
            RtlCopyMemory(to, from, BUFFER_SIZE);
        }
        printf("RtlCopyMemory %f\n", pc.ElapsedMillisec());
        for (int i=0; i<BUFFER_SIZE; ++i) {
            if (to[i] != (char)(i+96)) {
                printf("to[%d](%x) != %x\n", i, (int)to[i], (char)(i+69));
            }
        }

        // test slowmemcpy1 performance
        for (int i=0; i<BUFFER_SIZE; ++i) {
            from[i] = (char)(i+1);
        }
        pc.Start();
        for (int i=0; i<100; ++i) {
            slowmemcpy1(to, from, BUFFER_SIZE);
        }
        printf("slowmemcpy1 %f\n", pc.ElapsedMillisec());
        for (int i=0; i<BUFFER_SIZE; ++i) {
            if (to[i] != (char)(i+1)) {
                printf("to[%d](%x) != %x\n", i, (int)to[i], (char)(i+69));
            }
        }
    }

    _aligned_free(to);
    to = nullptr;
    _aligned_free(from);
    from = nullptr;
}

static void
TestPcmConv16to32(void)
{
    PerfCount pc;
    int64_t numOfItems = 100LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMをint値PCMに変換します。
    int16_t *fromS = (int16_t*)_aligned_malloc(numOfItems*2, 16);
    int32_t *toAsm = (int32_t*)_aligned_malloc(numOfItems*4, 16);
    int32_t *toCpp = (int32_t*)_aligned_malloc(numOfItems*4, 16);

    for (int64_t i=0; i<numOfItems; ++i) {
        fromS[i] = (int16_t)(i+1);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }

    pc.Start();
    PCM16to32(fromS, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM16to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm16to32CPP(fromS, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM16to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        }
    }

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(fromS);
    fromS = nullptr;
}

static void
TestPcmConv16toF32(void)
{
    PerfCount pc;
    int64_t numOfItems = 100LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMをint値PCMに変換します。
    int16_t *fromS = (int16_t*)_aligned_malloc(numOfItems*2, 16);
    float   *toAsm = (float*)  _aligned_malloc(numOfItems*4, 16);
    float   *toCpp = (float*)  _aligned_malloc(numOfItems*4, 16);

    for (int64_t i=0; i<numOfItems; ++i) {
        fromS[i] = (int16_t)(i+1);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }

    pc.Start();
    PCM16toF32(fromS, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM16toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm16toF32CPP(fromS, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM16toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %f %f\n", i, toAsm[i], toCpp[i]);
        }
    }

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(fromS);
    fromS = nullptr;
}

static void
TestPcmConv24to32(void)
{
    PerfCount pc;
    int64_t numOfItems = 100LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMをint値PCMに変換します。
    uint8_t *from24 = (uint8_t*)_aligned_malloc(numOfItems*3, 16);
    int32_t *toAsm  = (int32_t*)_aligned_malloc(numOfItems*4, 16);
    int32_t *toCpp  = (int32_t*)_aligned_malloc(numOfItems*4, 16);

    for (int64_t i=0; i<numOfItems; ++i) {
        int32_t v = (int32_t)i*3 + 1;
        from24[i*3+0] = (uint8_t)(v);
        from24[i*3+1] = (uint8_t)(v+1);
        from24[i*3+2] = (uint8_t)(v+2);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }

    pc.Start();
    PCM24to32(from24, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM24to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm24to32CPP(from24, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM24to32  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        } else {
            //printf("OK:    %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        }
    }

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from24);
    from24 = nullptr;
}

static void
TestPcmConv24toF32(void)
{
    PerfCount pc;
    int64_t numOfItems = 100LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMをint値PCMに変換します。
    uint8_t *from24 = (uint8_t*)_aligned_malloc(numOfItems*3, 16);
    float   *toAsm  = (float*)  _aligned_malloc(numOfItems*4, 16);
    float   *toCpp  = (float*)  _aligned_malloc(numOfItems*4, 16);

    for (int64_t i=0; i<numOfItems; ++i) {
        int32_t v = (int32_t)i*3 + 1;
        from24[i*3+0] = (uint8_t)(v);
        from24[i*3+1] = (uint8_t)(v+1);
        from24[i*3+2] = (uint8_t)(v+2);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }

    pc.Start();
    PCM24toF32(from24, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM24toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm24toF32CPP(from24, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM24toF32 100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %g %g\n", i, toAsm[i], toCpp[i]);
        } else {
            //printf("OK:    %llx %08x %08x\n", i, toAsm[i], toCpp[i]);
        }
    }

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(from24);
    from24 = nullptr;
}

static void
TestPcmConv16to24(void)
{
    PerfCount pc;
    int64_t numOfItems = 100LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMを24bitPCMに変換します。
    int16_t *from  = (int16_t*) _aligned_malloc(numOfItems*2, 16);
    uint8_t *toAsm = (uint8_t*) _aligned_malloc(numOfItems*3, 16);
    uint8_t *toCpp = (uint8_t*) _aligned_malloc(numOfItems*3, 16);

    uint8_t *fromB = (uint8_t*)from;

    for (int64_t i=0; i<numOfItems; ++i) {
        int32_t v = (int32_t)(i*2) + 1;
        fromB[i*2+0] = (uint8_t)(v);
        fromB[i*2+1] = (uint8_t)(v+1);
    }

    pc.Start();
    PCM16to24(from, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM16to24  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 0.1 / elapsedSecAsm);

    pc.Start();
    Pcm16to24CPP(from, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM16to24  100M sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 0.1 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems*3; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %llx %02x %02x\n", i, toAsm[i], toCpp[i]);
        } else {
            // printf("OK:    %llx %02x %02x\n", i, toAsm[i], toCpp[i]);
        }
    }

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

    {
        CpuCapability cc;
        Avx512Capability ac;

        GetCpuCapability(&cc, &ac);

        // https://en.wikipedia.org/wiki/Advanced_Vector_Extensions#AVX-512 の順に表示。
        if (cc.SSE3) { printf("SSE3 "); }
        if (cc.SSSE3) { printf("SSSE3 "); }
        if (cc.SSE41) { printf("SSE4.1 "); }
        if (cc.SSE42) { printf("SSE4.2 "); }
        if (cc.AVX) { printf("AVX "); }
        if (cc.AVX2) { printf("AVX2 "); }
        if (cc.AVXVNNI) { printf("AVXVNNI "); } //< AVX-VNNIはAVX512-VNNIとは別の機能で、より新しい。

        if (ac.AVX512F) {
            printf("AVX512( F ");
            if (ac.AVX512CD) { printf("CD "); }
            if (ac.AVX512VPOPCNTDQ) { printf("VPOPCNTDQ "); }
            if (ac.AVX512VL) { printf("VL "); }
            if (ac.AVX512DQ) { printf("DQ "); }
            if (ac.AVX512BW) { printf("BW "); }
            if (ac.AVX512IFMA) { printf("IFMA "); }
            if (ac.AVX512VBMI) { printf("VBMI "); }
            if (ac.AVX512VBMI2) { printf("VBMI2 "); }
            if (ac.AVX512BITALG) { printf("BITALG "); }
            if (ac.AVX512VNNI) { printf("AVX512-VNNI "); }
            if (ac.AVX512BF16) { printf("BF16 "); }
            if (ac.AVX512VPCLMULQDQ) { printf("VPCLMULQDQ "); }
            if (ac.AVX512GFNI) { printf("GFNI "); }
            if (ac.AVX512VAES) { printf("VAES "); }
            if (ac.AVX512VP2INTERSECT) { printf("VP2INTERSECT "); }
            printf(")");
        }

        printf ("\n");
    }

    //TestMemcpy();
    TestPcmConv16toF32();
    TestPcmConv16to24();
    TestPcmConv24toF32();
    TestPcmConv24to32();
    TestPcmConv16to32();

    return 0;
}

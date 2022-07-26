#include <Windows.h> //< QueryPerformanceCounter()
#include <stdio.h>  //< printf()
#include <string.h> //< memset()
#include <malloc.h> //< _aligned_malloc()
#include <assert.h> //< assert()
#include "MyMemcpy64.h"
#include "PCM16to32.h"
#include "PCM16toF32.h"

#define BUFFER_SIZE (8192)

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

/// ASMとの性能比較用C++実装。
static void
Pcm16to32CPP(const short *from, int *to, int64_t numOfItems)
{
    for (int64_t i=0; i<numOfItems; ++i) {
        to[i] = from[i] << 16;
    }
}

static void
Pcm16toF32CPP(const short *from, float *to, int64_t numOfItems)
{
    for (int64_t i=0; i<numOfItems; ++i) {
        to[i] = from[i] * (1.0f / 32768.0f);
    }
}


static void
TestPcmConv16to32(void)
{
    PerfCount pc;
    int64_t numOfItems = 1000LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMをint値PCMに変換します。
    short *fromS = (short*)_aligned_malloc(numOfItems*2, 16);
    int   *toAsm   = (int*)_aligned_malloc(numOfItems*4, 16);
    int   *toCpp   = (int*)_aligned_malloc(numOfItems*4, 16);

    for (int64_t i=0; i<numOfItems; ++i) {
        fromS[i] = (short)(i+1);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }

    pc.Start();
    PCM16to32(fromS, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM 1G sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 1.0 / elapsedSecAsm);

    pc.Start();
    Pcm16to32CPP(fromS, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM 1G sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 1.0 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %04x %08x %08x\n", i, toAsm[i], toCpp[i]);
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
    int64_t numOfItems = 1000LL * 1000 * 1000 + 7;

    // numOfItems個のshort値PCMをint値PCMに変換します。
    short *fromS = (short*)_aligned_malloc(numOfItems*2, 16);
    float   *toAsm   = (float*)_aligned_malloc(numOfItems*4, 16);
    float   *toCpp   = (float*)_aligned_malloc(numOfItems*4, 16);

    for (int64_t i=0; i<numOfItems; ++i) {
        fromS[i] = (short)(i+1);
        toAsm[i] = 0;
        toCpp[i] = 0;
    }

    pc.Start();
    PCM16toF32(fromS, toAsm, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM 1G sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 1.0 / elapsedSecAsm);

    pc.Start();
    Pcm16toF32CPP(fromS, toCpp, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM 1G sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 1.0 / elapsedSecCpp);

    // Compare two results.
    for (int64_t i=0; i<numOfItems; ++i) {
        if (toAsm[i] != toCpp[i]) {
            printf("Error: %04x %f %f\n", i, toAsm[i], toCpp[i]);
        }
    }

    _aligned_free(toCpp);
    toCpp = nullptr;
    _aligned_free(toAsm);
    toAsm = nullptr;
    _aligned_free(fromS);
    fromS = nullptr;
}

static bool
DumpDataToFile(const char *buf, int bytes, const char *path)
{
    bool rv = false;

    FILE* fp = nullptr;
    errno_t e = fopen_s(&fp, path, "wb");
    if (e != 0 || fp == nullptr) {
        printf("E: DumpDataToFile fopen failed %s\n", path);
        return false;
    }

    int r = fwrite(buf, 1, bytes, fp);
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

    //TestMemcpy();
    //TestPcmConv16to32();
    TestPcmConv16toF32();

    return 0;
}

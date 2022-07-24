#include <Windows.h> //< QueryPerformanceCounter()
#include <stdio.h>  //< printf()
#include <string.h> //< memset()
#include <malloc.h> //< _aligned_malloc()
#include <assert.h> //< assert()
#include "MyMemcpy64.h"
#include "PCM16to32.h"
#include <stdint.h>

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

static void
Pcm16to32CPP(const short *from, int *to, int64_t numOfItems)
{
    for (int64_t i=0; i<numOfItems; ++i) {
        to[i] = from[i] << 16;
    }
}


static void
TestPcmConv(void)
{
    PerfCount pc;
    int64_t numOfItems = 1000LL * 1000 * 1000;

    // numOfItemsŒÂ‚Ìshort’lPCM‚ðint’lPCM‚É•ÏŠ·‚µ‚Ü‚·B
    short *fromS = (short*)_aligned_malloc(numOfItems*2, 16);
    int   *toD   = (int*)_aligned_malloc(numOfItems*4, 16);

    for (int i=0; i<numOfItems; ++i) {
        fromS[i] = i;
        toD[i] = 0;
    }

    pc.Start();
    PCM16to32(fromS, toD, numOfItems);
    double elapsedSecAsm = pc.ElapsedSeconds();

    printf("ASM PCM 1G sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecAsm, 1.0 / elapsedSecAsm);

    /*
    int printCounts = 16;
    for (int i=0; i<printCounts; ++i) {
        printf("%04x %08x\n", i, toD[i]);
    }
    */

    pc.Start();
    Pcm16to32CPP(fromS, toD, numOfItems);
    double elapsedSecCpp = pc.ElapsedSeconds();

    printf("C++ PCM 1G sample conversion in %f sec. %f Gsamples/sec\n",
        elapsedSecCpp, 1.0 / elapsedSecCpp);

    _aligned_free(toD);
    toD = nullptr;
    _aligned_free(fromS);
    fromS = nullptr;
}

int
main(void)
{
    //TestMemcpy();
    TestPcmConv();

    return 0;
}

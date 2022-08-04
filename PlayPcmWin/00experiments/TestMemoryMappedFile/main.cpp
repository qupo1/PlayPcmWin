#include <Windows.h>
#include <stdio.h>
#include <stdint.h>
#include "MMapReadFile.h"
#include "WWStopwatch.h"
#include "MyMemcpy2.h"

int
wmain(int argc, wchar_t* argv[])
{
    const wchar_t *path = L"C:/audio/03-JAPRS-HiRes-192kHz24bit.wav";
    MMapReadFile mrf;
    WWStopwatch sw;

    HRESULT hr = mrf.Open(path);
    if (FAILED(hr)) {
        printf("Error: mrf.Open(%S) failed %x\n", path, hr);
        return 1;
    }

    // コピー先メモリを割り当てます。64バイトアラインで確保。
    uint8_t *to = (uint8_t*) _aligned_malloc(mrf.FileSize(), 64);
    
    // memset(to, 0, mrf.FileSize());

    for (int i=0;i<2; ++i) {
        sw.Start();

#if 1
        MyMemcpy2(to, mrf.BaseAddr(), mrf.FileSize());
#else
        memcpy(to, mrf.BaseAddr(), mrf.FileSize());
#endif

        double es = sw.ElapsedSeconds();

        printf("%f MB copied in %f seconds. %f MB/sec\n",
            mrf.FileSize()*0.001*0.001,
            es,
            mrf.FileSize()*0.001*0.001 / es);
    }

    _aligned_free(to);
    to = nullptr;
    return 0;
}


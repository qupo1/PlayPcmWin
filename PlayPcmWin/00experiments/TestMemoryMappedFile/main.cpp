// 結論：キャッシュされてない場合freadの方がmmapより速い。

#include <Windows.h>
#include <stdio.h>
#include <stdint.h>
#include "MMapReadFile.h"
#include "WWStopwatch.h"
#include "MyMemcpy2.h"
#include <vector>
#include <assert.h>

static const int NUM_THREADS = 4;
static const int ALIGN = 256;

enum CopyType {
    CT_Mmap,
    CT_Fread,
};

static const char *
CopyTypeToStr(CopyType t)
{
    switch (t) {
    case CT_Mmap: return "MMAP";
    case CT_Fread: return "fread";
    default: return "Unknown";
    }
}


static HRESULT
ParallelCopy(const wchar_t *path, CopyType ct)
{
    WWStopwatch sw;
    MMapReadFile mrf;    
    FILE *fp = nullptr;
    uint8_t *to = nullptr;
    int64_t bytes = 0;
    uint8_t *buf = nullptr;
    HRESULT hr = E_FAIL;

    switch (ct) {
    case CT_Mmap: {
            HRESULT hr = mrf.Open(path);
            if (FAILED(hr)) {
                printf("Error: mrf.Open(%S) failed %x\n", path, hr);
                return E_FAIL;
            }

            bytes = mrf.FileSize();
        }
        break;
    case CT_Fread: {
            errno_t ercd = _wfopen_s(&fp, path, L"rb");
            _fseeki64(fp, 0, SEEK_END);
            bytes = _ftelli64(fp);
            _fseeki64(fp, 0, SEEK_SET);
        }
        break;
    default:
        assert(0);
        break;
    }

    // コピー先メモリを割り当てます。アライン。
    assert(ALIGN * NUM_THREADS < bytes);
    to = (uint8_t*) _aligned_malloc(bytes, 256);
    memset(to, 0, bytes);

    // 分割読み込みします。
    std::vector<int64_t> fragments;
    for (int64_t i=0; i<NUM_THREADS; ++i) {
        int64_t pos = i * bytes / NUM_THREADS;
        pos = pos & ~((uint64_t)ALIGN-1);
        fragments.push_back(pos);
    }
    fragments.push_back(bytes);

    int64_t bufBytes = fragments[NUM_THREADS] - fragments[NUM_THREADS-1];
    buf = new uint8_t[bufBytes];
    if (nullptr == buf) {
        printf("Error: new memory failed %x\n", path, hr);
        hr = E_OUTOFMEMORY;
        goto Cleanup;
    }

    sw.Start();

    #pragma omp parallel for
    for (int i=0; i<NUM_THREADS; ++i) {
        int64_t pos   = fragments[i];
        int64_t posTo = fragments[i+1];
        int64_t fragBytes = posTo - pos;

        switch (ct) {
        case CT_Mmap: {
                MyMemcpy2(to+pos, ((const uint8_t*)mrf.BaseAddr()) +pos, fragBytes);
            }
            break;
        case CT_Fread: {
                fread(buf, 1, fragBytes, fp);
                MyMemcpy2(to+pos, buf, fragBytes);
            }
            break;
        }
    }

    double es = sw.ElapsedSeconds();

    printf("%s %f MB copied in %f seconds. %f MB/sec\n",
        CopyTypeToStr(ct),
        bytes*0.001*0.001,
        es,
        bytes*0.001*0.001 / es);

    hr = S_OK;

Cleanup:

    if (fp != nullptr) {
        fclose(fp);
        fp = nullptr;
    }

    delete [] buf;
    buf = nullptr;

    _aligned_free(to);
    to = nullptr;

    return hr;
}

int
wmain(int argc, wchar_t* argv[])
{
    const wchar_t *path = L"D:/audio/03-JAPRS-HiRes-192kHz24bit.wav";

    for (int i=0;i<2; ++i) {
        ParallelCopy(path, CT_Mmap);
        ParallelCopy(path, CT_Fread);
    }

    return 0;
}


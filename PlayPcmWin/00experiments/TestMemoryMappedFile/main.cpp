// 結論：
// IO Control portsを使用した読み出しが速い。
// キャッシュされてない場合freadの方がmmapより速い。

#include <Windows.h>
#include <stdio.h>
#include <stdint.h>
#include "WWMMapReadFile.h"
#include "WWStopwatch.h"
#include "MyMemcpy2.h"
#include <vector>
#include <assert.h>
#include "WWFileReader.h"

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
    WWMMapReadFile mrf;
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

static void
ReadCompleted(uint64_t pos, uint8_t *buf, int bytes)
{
    // printf("%lld %d %02x%02x%02x%02x\n", pos, bytes, buf[0], buf[1], buf[2], buf[3]);
}

static HRESULT
ReadWithFileReader(const wchar_t *path)
{
    HRESULT hr = E_FAIL;
    WWStopwatch sw;

    WWFileReader fr;
    hr = fr.Init();
    if (FAILED(hr)) {
        printf("ReadWithFileReader Init failed %x\n", hr);
        return hr;
    }

    hr = fr.Open(path);
    if (FAILED(hr)) {
        printf("ReadWithFileReader Open failed %x\n", hr);
        return hr;
    }

    sw.Start();

    hr = fr.Read(0, fr.FileSz(), ReadCompleted);
    if (FAILED(hr)) {
        printf("ReadWithFileReader Read failed %x\n", hr);
        return hr;
    }

    double es = sw.ElapsedSeconds();

    printf("%f MB read in %f seconds. %f MB/sec\n",
        fr.FileSz()*0.001*0.001,
        es,
        fr.FileSz()*0.001*0.001 / es);

    fr.Close();
    fr.Term();

    return S_OK;
}

int
wmain(int argc, wchar_t* argv[])
{
    const wchar_t *path = L"D:/audio/03-JAPRS-HiRes-192kHz24bit.wav";

#if 1
    for (int i=0;i<1; ++i) {
        ReadWithFileReader(path);
    }
#else
    for (int i=0;i<2; ++i) {
        ParallelCopy(path, CT_Mmap);
        ParallelCopy(path, CT_Fread);
    }
#endif

    return 0;
}


#include <Windows.h>
#include <stdio.h>
#include <stdint.h>

static void
PrintUsage(void)
{
    printf("Usage: ReadOneFileWithoutCache filename\n");
}

static HRESULT
ReadOneFile(const wchar_t *path,
        uint8_t *buf, const DWORD bufBytes,
        int64_t &fileBytes_return)
{
    HRESULT hr        = S_OK;
    HANDLE  fh        = INVALID_HANDLE_VALUE;
    DWORD   readBytes = 0;
    int     count     = 0;

    fileBytes_return = 0;

    fh = CreateFile(path,
            GENERIC_READ,     //< dwDesiredAccess
            FILE_SHARE_READ,  //< dwShareMode
            nullptr,          //< lpSecurityAttributes
            OPEN_EXISTING,    //< dwCreationDisposition 
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_NO_BUFFERING, //< dwFlagsAndAttributes
            nullptr           //< hTemplateFile
            );

    if (INVALID_HANDLE_VALUE == fh) {
        hr = GetLastError();
        printf("Error: ReadOneFile file open failed %d\n", hr);
        goto end;
    }

    while (true) {
        BOOL b = ReadFile(fh,
                buf,
                bufBytes,
                &readBytes,
                nullptr);

        if (b == 0) {
            // ReadFile failed.
            hr = GetLastError();
        }
        if (b == 0 || readBytes == 0) {
            // ReadFile failed or EOF reached.
            goto end;
        }

        fileBytes_return += readBytes;

        // printf("%d\n", count++);
    }

end:
    if (fh != INVALID_HANDLE_VALUE) {
        CloseHandle(fh);
        fh = INVALID_HANDLE_VALUE;
    }

    return hr;
}

static HRESULT
Run(const wchar_t *path)
{
    HRESULT       hr         = S_OK;
    uint8_t       *buf       = nullptr;
    const DWORD   bufBytes   = 1024 * 1024;
    int64_t       fileBytes  = 0;
    LARGE_INTEGER freqTick   = {};
    LARGE_INTEGER startTick  = {};
    LARGE_INTEGER endTick    = {};
    double        recipFreq  = 0;
    double        elapsedSec = 0;


    buf = (uint8_t*)malloc(bufBytes);
    if (buf == nullptr) {
        printf("Error: memory exhausted\n");
        hr = E_OUTOFMEMORY;
        goto end;
    }
    memset(buf, 0, bufBytes);

    // パフォーマンスカウンタ。
    QueryPerformanceFrequency(&freqTick);
    recipFreq = 1.0 / freqTick.QuadPart;

    // 計測開始。
    QueryPerformanceCounter(&startTick);

    hr = ReadOneFile(path, buf, bufBytes, fileBytes);

    // 計測終了。
    QueryPerformanceCounter(&endTick);

    if (FAILED(hr)) {
        printf("ReadOneFile Failed: %d %S\n", hr, path);
        goto end;
    }

    // 経過時間表示。
    elapsedSec = ((double)(endTick.QuadPart - startTick.QuadPart)) * recipFreq;
    printf("%f seconds. %f MB/s\n", elapsedSec, fileBytes / elapsedSec / 1000.0 / 1000.0);

end:
    free(buf);
    buf = nullptr;

    return hr;
}


int
wmain(int argc, wchar_t *argv[])
{
    if (argc < 2) {
        PrintUsage();
        return 1;
    }

    const wchar_t *path = argv[1];

    HRESULT hr = Run(path);

    return SUCCEEDED(hr) ? 0 : 1;
}


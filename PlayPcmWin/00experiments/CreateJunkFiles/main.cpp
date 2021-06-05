#include <stdio.h>
#include <stdlib.h>
#include <string>
#include <stdint.h>

static void
PrintUsage(void)
{
    printf("Usage: CreateJunkFiles dirPath fileNamePrefix [filesize in bytes < 2GB]\n");
}

static bool
CreateJunkFile(const wchar_t *dirPath, const wchar_t *fileNamePrefix, int idx, int fileSz)
{
    bool br = false;
    FILE *fp = nullptr;
    uint8_t *p = nullptr;
    wchar_t s[256] = {};
    size_t sz = 0;
    
    swprintf_s(s, L"%s/%s%08d.bin", dirPath, fileNamePrefix, idx);

    auto ercd = _wfopen_s(&fp, s, L"wb");
    if (ercd != 0) {
        printf("Error: file open for write failed\n");
        return false;
    }

    printf("Created %S\n", s);

    p = (uint8_t*)malloc(fileSz);
    if (p == nullptr) {
        printf("Error: malloc failed\n");
        goto end;
    }
    memset(p, 0x55, fileSz);

    sz = fwrite(p, 1, fileSz, fp);
    printf("  fwrite %d bytes\n", sz);
    if (sz != fileSz) {
        goto end;
    }

    br = true;

end:
    free(p);
    p = nullptr;

    if (fp != nullptr) {
        fclose(fp);
        fp = nullptr;
    }

    return br;
}

int wmain(int argc, wchar_t *argv[])
{
    if (argc < 3) {
        PrintUsage();
        return 1;
    }

    int fileSz = 1024 * 1024;
    if (argc == 4) {
        fileSz = wcstol(argv[3], nullptr, 10);
    }

    const wchar_t *dirPath = argv[1];
    const wchar_t *fileNamePrefix = argv[2];

    for (int i=0; ; ++i) {
        bool br = CreateJunkFile(dirPath, fileNamePrefix, i, fileSz);
        if (!br) {
            break;
        }
    }

    return 0;
}


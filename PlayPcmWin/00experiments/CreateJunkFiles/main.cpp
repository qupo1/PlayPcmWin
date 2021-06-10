#include <stdio.h>
#include <stdlib.h>
#include <string>
#include <stdint.h>
#include <Windows.h>


static const int FILES_PER_FOLDER = 1000;


static void
PrintUsage(void)
{
    printf("Usage: CreateJunkFiles dirPath [filesize in bytes]\n");
}


static bool
CreateJunkFile1(const wchar_t *dirPath, int idx, uint64_t fileSz, const uint8_t *buf)
{
    bool br = false;
    FILE *fp = nullptr;
    wchar_t s[MAX_PATH] = {};
    size_t sz = 0;
    
    swprintf_s(s, L"%s/%d.bin", dirPath, idx);

    auto ercd = _wfopen_s(&fp, s, L"wb");
    if (ercd != 0) {
        printf("Error: file open for write failed %S\n", s);
        return false;
    }

    printf("%S        \n", s);

    sz = fwrite(buf, 1, fileSz, fp);
    //printf("  fwrite %d bytes\n", sz);
    if (sz != fileSz) {
        goto end;
    }

    br = true;

end:
    if (fp != nullptr) {
        fclose(fp);
        fp = nullptr;
    }

    return br;
}


static void
CreateJunkFiles(const wchar_t *parentDirPath, uint64_t fileSz, const uint8_t *buf)
{
    wchar_t s[MAX_PATH] = {};

    for (int dir1Idx = 0;; ++dir1Idx) {
        // 1層目のフォルダー作成。
        swprintf_s(s, L"%s/%d", parentDirPath, dir1Idx);
        CreateDirectoryW(s, nullptr);

        for (int dir2Idx = 0; dir2Idx < FILES_PER_FOLDER; ++dir2Idx) {
            // 2層目のフォルダー作成。
            swprintf_s(s, L"%s/%d/%d", parentDirPath, dir1Idx, dir2Idx);
            CreateDirectoryW(s, nullptr);

            for (int i = 0; i < FILES_PER_FOLDER; ++i) {

                bool b = CreateJunkFile1(s, i, fileSz, buf);
                if (!b) {
                    return;
                }
            }
        }
    }
}


int wmain(int argc, wchar_t *argv[])
{
    uint8_t *buf = nullptr;
    uint64_t fileSz = 1024 * 1024;

    if (argc < 3) {
        PrintUsage();
        return 1;
    }

    const wchar_t *dirPath = argv[1];

    fileSz = _wcstoui64(argv[2], nullptr, 10);
    buf = (uint8_t*)malloc(fileSz);
    if (buf == nullptr) {
        printf("Error: malloc failed\n");
        goto end;
    }
    memset(buf, 0x55, fileSz);

    CreateJunkFiles(dirPath, fileSz, buf);

end:
    free(buf);
    buf = nullptr;

    return 0;
}


#include "WWFileFragmentationCount.h"
#include <stdio.h>

static void
PrintUsage(void)
{
    printf("Usage: WWFileFragmentationCount filePath\n");
}

int
wmain(int argc, wchar_t *argv[])
{
    WWFileFragmentationInfo ffi = {};

    if (argc != 2) {
        PrintUsage();
        return 1;
    }

    int hr = WWFileFragmentationCount(argv[1], ffi);
    if (hr < 0) {
        printf("Error: WWFileFragmentationCount failed %08x\n", hr);
        return 1;
    }

    return 0;
}

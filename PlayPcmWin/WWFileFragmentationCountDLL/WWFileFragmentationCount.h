#pragma once

#include <stdint.h>

#ifdef WWFILEFRAGMENTATIONCOUNTDLL_EXPORTS
#define WWFILEFRAGMENTATIONCOUNT_API __declspec(dllexport)
#else
#define WWFILEFRAGMENTATIONCOUNT_API __declspec(dllimport)
#endif

#define WW_VCN_LCN_COUNT (256)

#pragma pack(push, 4)
struct LcnVcn {
    int64_t lcn;
    int64_t nextVcn;
};

struct WWFileFragmentationInfo {
    int64_t nClusters;
    int bytesPerSector;
    int bytesPerCluster;
    int nFragmentCount;
    int reserved0;
    int64_t startVcn;
    LcnVcn lcnVcn[WW_VCN_LCN_COUNT];
};
#pragma pack(pop)

extern "C" WWFILEFRAGMENTATIONCOUNT_API
int __stdcall
WWFileFragmentationCount(const wchar_t *filePath, WWFileFragmentationInfo &ffi_return);



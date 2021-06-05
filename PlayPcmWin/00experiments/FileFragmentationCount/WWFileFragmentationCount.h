#pragma once

#include <stdint.h>

extern "C" {

struct LcnVcn {
    int64_t lcn;
    int64_t nextVcn;
};

#define WW_VCN_LCN_COUNT (256)

struct WWFileFragmentationInfo {
    int64_t nClusters;
    int bytesPerSector;
    int bytesPerCluster;
    int nFragmentCount;
    int reserved0;
    int64_t startVcn;
    LcnVcn lcnVcn[WW_VCN_LCN_COUNT];
};

int
WWFileFragmentationCount(const wchar_t *filePath, WWFileFragmentationInfo &ffi_return);

}; //< extern "C"


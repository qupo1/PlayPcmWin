#include "WWAsmDLL.h"
#include "SimdCapability.h"

extern "C" {

WWASMDLL_API int __stdcall
WWGetCpuCapability(SimdCapability &sc, Avx512Capability &ac)
{
    WWGetSimdCapability(&sc);
    WWGetAvx512Capability(&ac);

    return 0;
}


};

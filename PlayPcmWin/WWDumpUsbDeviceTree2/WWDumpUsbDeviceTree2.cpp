#include "WWUsbDeviceTreeDLL.h"
#include <iostream>

int main(void)
{
    WWUsbDeviceTreeDLL_Init();
    WWUsbDeviceTreeDLL_Refresh();
    WWUsbDeviceTreeDLL_Term();

    return 0;
}

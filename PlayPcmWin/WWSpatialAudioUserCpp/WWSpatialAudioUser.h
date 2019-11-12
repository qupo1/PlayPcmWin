﻿// 日本語
#pragma once
#include "WWSpatialAudioUserTemplate.h"
#include "WWDynAudioObject.h"
#include <SpatialAudioClient.h>

class WWSpatialAudioUser :
    public WWSpatialAudioUserTemplate<
        ISpatialAudioObjectRenderStream,
        WWDynAudioObject>
{
public:
    HRESULT Init(void) override;

    /// @param staticObjectTypeMask 1つもスタティックなオブジェクトが無いときはNone。Dynamicにするとエラーが起きた。
    HRESULT ActivateAudioStream(int maxDynObjectCount, int staticObjectTypeMask) override;

private:
    static DWORD RenderEntry(LPVOID lpThreadParameter);
    HRESULT RenderMain(void);
    HRESULT Render1(void);
};

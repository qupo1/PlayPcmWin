#pragma once

#include <Windows.h>
#include <stdint.h>

struct WWNativePcmFmt;

/// @channelMap toの各チャンネルに入れる入力fromチャンネル番号の表。toFmt.numChannels要素ある。無音を入れる時-1。
/// 例: toのch2に対応する入力チャンネル番号はchannelMap[2]に入っている。
/// 例: int chFrom = channelMap[chTo]
HRESULT WWPcmFmtConverter(
        const uint8_t *pcmFrom, const WWNativePcmFmt &fromFmt,
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt, const int *channelMap, const int64_t frameCount);


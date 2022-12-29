﻿#pragma once

#include <Windows.h>
#include <stdint.h>

#include "WWNativePcmFmt.h"
#include "WWFileReaderMT.h"
#include <vector>


class WWNativeWavReader {
public:
    HRESULT Init(void);
    void Term(void);

    /// @ channelMap tgtPcmFmt.numChannels要素の配列。tgtにmapするorigのチャンネル番号が入っている。無音は-1。
    HRESULT PcmReadStart(const wchar_t *path, const WWNativePcmFmt & origPcmFmt, const WWNativePcmFmt & tgtPcmFmt, const int *channelMap);

    /// 読み終わるまでブロックします。
    HRESULT PcmReadOne(const int64_t fileOffset, const int64_t readFrames, uint8_t *bufTo);

    void PcmReadEnd(void);

    void ReadCompleted(const uint64_t fileOffset, const uint64_t readOffset, const uint8_t *bufFrom, const int readBytes, uint8_t *bufTo);

private:
    WWNativePcmFmt mOrigPcmFmt;
    WWNativePcmFmt mTgtPcmFmt;
    WWFileReaderMT mFileReader;

    std::vector<int> mChannelMap;
};


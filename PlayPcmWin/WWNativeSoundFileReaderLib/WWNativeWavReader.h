#pragma once

#include <SDKDDKVer.h>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>

#include "WWNativePcmFmt.h"
#include "WWFileReaderMT.h"
#include <vector>


class WWNativeWavReader {
public:
    HRESULT Init(void);
    void Term(void);

    /// @ channelMap tgtPcmFmt.numChannels要素の配列。tgtにmapするorigのチャンネル番号が入っている。無音は-1。
    HRESULT PcmReadStart(const wchar_t *path, WWNativePcmFmt & origPcmFmt, WWNativePcmFmt & tgtPcmFmt, const int *channelMap);

    /// 読み終わるまでブロックします。
    HRESULT PcmReadOne(int64_t fileOffset, int64_t sampleCount, uint8_t *bufTo);

    void PcmReadEnd(void);

    void ReadCompleted(uint64_t fileOffset, uint8_t *bufFrom, int bytes, uint8_t *bufTo);

private:
    WWNativePcmFmt mOrigPcmFmt;
    WWNativePcmFmt mTgtPcmFmt;
    WWFileReaderMT mFileReader;
    std::vector<int> mChannelMap;
};

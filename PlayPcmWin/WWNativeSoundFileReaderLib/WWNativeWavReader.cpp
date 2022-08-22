#include "WWNativeWavReader.h"
#include "WWPcmFmtConverter.h"


HRESULT
WWNativeWavReader::Init(void)
{
    HRESULT hr = E_FAIL;
    
    hr = mFileReader.Init();
    if (FAILED(hr)) {
        return hr;
    }

    return S_OK;
}

void WWNativeWavReader::Term(void)
{
    mFileReader.Term();
}

HRESULT
WWNativeWavReader::PcmReadStart(const wchar_t *path, WWNativePcmFmt & origPcmFmt, WWNativePcmFmt & tgtPcmFmt, const int *channelMap)
{
    HRESULT hr = E_FAIL;

    hr = mFileReader.Open(path);
    if (FAILED(hr)) {
        return hr;
    }

    mOrigPcmFmt = origPcmFmt;
    mTgtPcmFmt  = tgtPcmFmt;

    if (nullptr == channelMap) {
        for (int ch=0;ch<mTgtPcmFmt.numChannels; ++ch) {
            mChannelMap.push_back(ch);
        }
    } else {
        for (int ch=0;ch<mTgtPcmFmt.numChannels; ++ch) {
            mChannelMap.push_back(channelMap[ch]);
        }
    }

    return S_OK;
}

struct ReadTag {
    WWNativeWavReader *self;
    uint8_t *bufTo;
   
    ReadTag(WWNativeWavReader *aSelf, uint8_t *aBufTo) {
        self = aSelf;
        bufTo = aBufTo;
    }
};

static void
gReadCompleted(uint64_t fileOffset, uint8_t *buf, int bytes, void *tag)
{
    ReadTag *rt = (ReadTag*)tag;

    WWNativeWavReader *self = rt->self;
    self->ReadCompleted(fileOffset, buf, bytes, rt->bufTo);
}

HRESULT
WWNativeWavReader::PcmReadOne(int64_t fileOffset, int64_t sampleCount, uint8_t *bufTo)
{
    HRESULT hr = E_FAIL;

    ReadTag tag(this, bufTo);

    int64_t origReadBytes = sampleCount * mOrigPcmFmt.ContainerBytesPerFrame();
    hr = mFileReader.Read(fileOffset, origReadBytes, gReadCompleted, (void*)&tag);
    if (FAILED(hr)) {
        return hr;
    }

    return hr;
}

void
WWNativeWavReader::PcmReadEnd(void)
{
    mFileReader.Close();
}

void
WWNativeWavReader::ReadCompleted(uint64_t fileOffset, uint8_t *bufFrom, int bytes, uint8_t *bufTo)
{
    // 読み出し完了時処理。

    const int64_t numFrames = bytes / mOrigPcmFmt.ContainerBytesPerFrame();

    WWPcmFmtConverter(bufFrom, mOrigPcmFmt, bufTo, mTgtPcmFmt, &mChannelMap[0], numFrames);
}

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
    mChannelMap.clear();
    mFileReader.Term();
}

HRESULT
WWNativeWavReader::PcmReadStart(
        const wchar_t *path,
        const WWNativePcmFmt & origPcmFmt,
        const WWNativePcmFmt & tgtPcmFmt,
        const int *channelMap)
{
    HRESULT hr = E_FAIL;

    hr = mFileReader.Open(path);
    if (FAILED(hr)) {
        return hr;
    }

    mOrigPcmFmt = origPcmFmt;
    mTgtPcmFmt  = tgtPcmFmt;

    mChannelMap.clear();
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
    int64_t readFrames;
   
    ReadTag(WWNativeWavReader *aSelf, uint8_t *aBufTo, int64_t aReadFrames) {
        self = aSelf;
        bufTo = aBufTo;
        readFrames = aReadFrames;
    }
};

static void
gReadCompleted(
        const uint64_t fileOffs, const uint64_t readOffs,
        const uint8_t *buf, const int readBytes, void *tag)
{
    ReadTag *rt = (ReadTag*)tag;

    WWNativeWavReader *self = rt->self;
    self->ReadCompleted(fileOffs, readOffs, buf, readBytes, rt->bufTo);
}

void
WWNativeWavReader::ReadCompleted(
        const uint64_t fileOffs, const uint64_t readOffs,
        const uint8_t *bufFrom, const int readBytes, uint8_t *bufTo)
{
    // 読み出し完了時処理。

    const int64_t offsFrames = readOffs   / mOrigPcmFmt.ContainerBytesPerFrame();
    const int64_t readFrames = readBytes  / mOrigPcmFmt.ContainerBytesPerFrame();
    const int64_t writeOffs  = offsFrames * mTgtPcmFmt.ContainerBytesPerFrame();

    // printf("WWNativeWavReader::ReadCompleted fileOffs=%llx readOffs=%llx writeOffs=%llx readFrames=%llx bufFrom=%p bufTo=%p\n",
    //     fileOffs, readOffs, writeOffs, readFrames, bufFrom, &bufTo[writeOffs]);

    WWPcmFmtConverter(bufFrom, mOrigPcmFmt, &bufTo[writeOffs], mTgtPcmFmt, &mChannelMap[0], readFrames);
}

HRESULT
WWNativeWavReader::PcmReadOne(
        const int64_t fileOffs, const int64_t readFrames, uint8_t *bufTo)
{
    HRESULT hr = E_FAIL;

    ReadTag tag(this, bufTo, readFrames);

    const int64_t readBytes = readFrames * mOrigPcmFmt.ContainerBytesPerFrame();
    hr = mFileReader.Read(fileOffs, readBytes, gReadCompleted, (void*)&tag);
    if (FAILED(hr)) {
        return hr;
    }

    return hr;
}

void
WWNativeWavReader::PcmReadEnd(void)
{
    mChannelMap.clear();
    mFileReader.Close();
}


#include "WWPcmFmtConverter.h"
#include "PCM16to24.h"
#include "PCM16to32.h"
#include "PCM16toF32.h"
#include "PCM24to32.h"
#include "PCM24toF32.h"
#include <assert.h>


enum SampleFmt {
    SF_PCM_Unsupported = -1,
    SF_PCM_i16,
    SF_PCM_i24,
    SF_PCM_i32,
    SF_PCM_f32,
};

SampleFmt
BitDepthAndIntFloatToSampleFmt(int bitDepth, bool isFloat)
{
    if (isFloat) {
        if (bitDepth == 32) {
            return SF_PCM_f32;
        }
        return SF_PCM_Unsupported;
    }

    switch (bitDepth) {
    case 16:
        return SF_PCM_i16;
    case 24:
        return SF_PCM_i24;
    case 32:
        return SF_PCM_i32;
    default:
        return SF_PCM_Unsupported;
    }
}

/// 32bit integer PCMサンプル値を戻します。32bit float値が入っている場合、そのビット列がそのまま32bit入ります。
static int32_t
GetSampleValueI32(const uint8_t *buf, int64_t frameNr, int ch, const WWNativePcmFmt &fmt)
{
    // FLOATの場合、32bitのみ。
    assert(!fmt.isFloat || fmt.validBitDepth == 32);

    if (fmt.numChannels <= ch) {
        // チャンネル番号が範囲外の場合、0を戻します。
        return 0;
    }

    int64_t sampleNr = (frameNr * fmt.numChannels + ch);
    int64_t bytePos  = sampleNr * fmt.containerBitDepth / 8;

    switch (fmt.validBitDepth) {
    case 16:
        {
            uint16_t v = (buf[bytePos]<<16) + (buf[bytePos+1]<<24);
            return v;
        }
    case 24:
        {
            uint32_t v = (buf[bytePos]<<8) + (buf[bytePos+1]<<16) + (buf[bytePos+1]<<24);
            return v;
        }
    case 32:
        {
            uint32_t v = buf[bytePos] + (buf[bytePos+1]<<8) + (buf[bytePos+1]<<16) + (buf[bytePos+1]<<24);
            return v;
        }
    default:
        assert(0);
        return 0;
    }
}

/// 32bit integer PCMサンプル値を書き込みます。
static void
SetSampleValueI32(const int32_t v, uint8_t *buf, int64_t frameNr, int ch, const WWNativePcmFmt &fmt)
{
    // FLOATの場合、32bitのみ。
    assert(!fmt.isFloat || fmt.validBitDepth == 32);

    // 書き込みチャンネル数は範囲内。
    assert(0 <= ch && ch < fmt.numChannels);

    int64_t sampleNr = (frameNr * fmt.numChannels + ch);
    int64_t bytePos  = sampleNr * fmt.containerBitDepth / 8;

    switch (fmt.containerBitDepth) {
    case 16:
        buf[bytePos + 0] = (v>>16) & 0xff;
        buf[bytePos + 1] = (v>>24) & 0xff;
        break;
    case 24:
        buf[bytePos + 0] = (v>>8) & 0xff;
        buf[bytePos + 1] = (v>>16) & 0xff;
        buf[bytePos + 2] = (v>>24) & 0xff;
        break;
    case 32:
        switch (fmt.validBitDepth) {
        case 24:
            buf[bytePos + 0] = 0;
            buf[bytePos + 1] = (v>>8) & 0xff;
            buf[bytePos + 2] = (v>>16) & 0xff;
            buf[bytePos + 3] = (v>>24) & 0xff;
            break;
        case 32:
            buf[bytePos + 0] = v & 0xff;
            buf[bytePos + 1] = (v>>8) & 0xff;
            buf[bytePos + 2] = (v>>16) & 0xff;
            buf[bytePos + 3] = (v>>24) & 0xff;
            break;
        default:
            assert(0);
            break;
        }
    default:
        assert(0);
        break;
    }
}

static HRESULT
BitDepthConvertCpp(
        const uint8_t *pcmFrom, const WWNativePcmFmt &fromFmt,
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt, const int64_t frameCount)
{
    SampleFmt fromSF = BitDepthAndIntFloatToSampleFmt(fromFmt.containerBitDepth, fromFmt.isFloat!=0);
    SampleFmt toSF   = BitDepthAndIntFloatToSampleFmt(toFmt.containerBitDepth, toFmt.isFloat!=0);

    if (fromSF == SF_PCM_Unsupported) {
        return E_INVALIDARG;
    }
    if (toSF == SF_PCM_Unsupported) {
        return E_INVALIDARG;
    }

    for (int64_t i=0; i<frameCount; ++i) {
        for (int ch=0; ch<toFmt.numChannels; ++ch) {
            int32_t v = GetSampleValueI32(pcmFrom, i, ch, fromFmt);
            SetSampleValueI32(v, pcmTo, i, ch, toFmt);
        }
    }

    return S_OK;
}


HRESULT
WWPcmFmtConverter::BitDepthConverter(
        const uint8_t *pcmFrom, const WWNativePcmFmt &fromFmt,
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt, const int64_t frameCount)
{
    SampleFmt fromSF = BitDepthAndIntFloatToSampleFmt(fromFmt.containerBitDepth, fromFmt.isFloat!=0);
    SampleFmt toSF   = BitDepthAndIntFloatToSampleFmt(toFmt.containerBitDepth,   toFmt.isFloat!=0);

    if (fromSF == SF_PCM_Unsupported) {
        return E_INVALIDARG;
    }
    if (toSF == SF_PCM_Unsupported) {
        return E_INVALIDARG;
    }

    if (fromFmt.numChannels != toFmt.numChannels) {
        // 入出力チャンネル数が異なる。
        return BitDepthConvertCpp(
                pcmFrom, fromFmt,
                pcmTo,   toFmt, frameCount);
    }

    // fromNumChannels == toNumChannelsなので、入出力サンプルカウントは同じ(sampleCount)。
    int64_t sampleCount = frameCount * fromFmt.numChannels;

    switch (fromSF) {
    case SF_PCM_i16:
        switch (toSF) {
        case SF_PCM_i16:
            memcpy(pcmTo, pcmFrom, sampleCount * fromFmt.containerBitDepth / 8);
            return S_OK;
        case SF_PCM_i24:
            PCM16to24((const int16_t*)pcmFrom, pcmTo, sampleCount);
            return S_OK;
        case SF_PCM_i32:
            PCM16to32((const int16_t*)pcmFrom, (int32_t*)pcmTo, sampleCount);
            return S_OK;
        case SF_PCM_f32:
            PCM16toF32((const int16_t*)pcmFrom, (float*)pcmTo, sampleCount);
            return S_OK;
        default:
            return E_INVALIDARG;
        }
    case SF_PCM_i24:
        switch (toSF) {
        case SF_PCM_i16:
            return BitDepthConvertCpp(
                    pcmFrom, fromFmt,
                    pcmTo,   toFmt, frameCount);
        case SF_PCM_i24:
            memcpy(pcmTo, pcmFrom, sampleCount * fromFmt.containerBitDepth / 8);
            return S_OK;
        case SF_PCM_i32:
            PCM24to32(pcmFrom, (int32_t*)pcmTo, sampleCount);
            return S_OK;
        case SF_PCM_f32:
            PCM24toF32(pcmFrom, (float*)pcmTo, sampleCount);
            return S_OK;
        default:
            return E_INVALIDARG;
        }
    case SF_PCM_i32:
    case SF_PCM_f32:
        return BitDepthConvertCpp(
                pcmFrom, fromFmt,
                pcmTo,   toFmt, frameCount);
    default:
        return E_INVALIDARG;
    }
    
}

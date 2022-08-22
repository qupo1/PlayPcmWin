#include "WWPcmFmtConverter.h"
#include "WWNativePcmFmt.h"
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

/// 32bit integer PCM�T���v���l��߂��܂��B32bit float�l�������Ă���ꍇ�A���̃r�b�g�񂪂��̂܂�32bit����܂��B
static int32_t
GetSampleValueI32(const uint8_t *buf, int64_t frameNr, int ch, const WWNativePcmFmt &fmt)
{
    // FLOAT�̏ꍇ�A32bit�̂݁B
    assert(!fmt.isFloat || fmt.validBitDepth == 32);

    if (fmt.numChannels <= ch) {
        // �`�����l���ԍ����͈͊O�̏ꍇ�A0��߂��܂��B
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

/// 32bit integer PCM�T���v���l���������݂܂��B
static void
SetSampleValueI32(const int32_t v, uint8_t *buf, int64_t frameNr, int ch, const WWNativePcmFmt &fmt)
{
    // FLOAT�̏ꍇ�A32bit�̂݁B
    assert(!fmt.isFloat || fmt.validBitDepth == 32);

    // �������݃`�����l�����͔͈͓��B
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
ConvertCpp(
        const uint8_t *pcmFrom, const WWNativePcmFmt &fromFmt,
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt, const int *channelMap, const int64_t frameCount)
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
        for (int chTo=0; chTo<toFmt.numChannels; ++chTo) {
            // PCM�����ŏ������B
            int32_t v = 0;

            if (toFmt.isDoP) {
                // DoP�����B
                if (i%2==0) {
                    v = 0x05696900;
                } else {
                    v = 0xfa696900;
                }
            }

            int chFrom = channelMap[chTo];
            if (0 <= chFrom) {
                v = GetSampleValueI32(pcmFrom, i, chTo, fromFmt);
            }

            SetSampleValueI32(v, pcmTo, i, chTo, toFmt);
        }
    }

    return S_OK;
}

/// true: 0��0, 1��1�A... k��k �S�`�����l������̃`�����l�����Ή�����}�b�v�B
static bool
IsIdenticalMap(int nCh, const int *channelMap)
{
    for (int ch=0; ch<nCh; ++ch) {
        if (channelMap[ch] != ch) {
            return false;
        }
    }

    return true;
}


HRESULT
WWPcmFmtConverter(
        const uint8_t *pcmFrom, const WWNativePcmFmt &fromFmt,
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt, const int *channelMap, const int64_t frameCount)
{
    SampleFmt fromSF = BitDepthAndIntFloatToSampleFmt(fromFmt.containerBitDepth, fromFmt.isFloat!=0);
    SampleFmt toSF   = BitDepthAndIntFloatToSampleFmt(toFmt.containerBitDepth,   toFmt.isFloat!=0);

    if (fromSF == SF_PCM_Unsupported) {
        return E_INVALIDARG;
    }
    if (toSF == SF_PCM_Unsupported) {
        return E_INVALIDARG;
    }

    if (fromFmt.numChannels != toFmt.numChannels
            || !IsIdenticalMap(toFmt.numChannels, channelMap)) {
        // ���o�̓`�����l�������قȂ�B�܂��́A�`�����l���Ή������ւ��鏈���B
        return ConvertCpp(
                pcmFrom, fromFmt,
                pcmTo,   toFmt, channelMap, frameCount);
    }

    // ���o�̓T���v���J�E���g�͓���(sampleCount)�B
    int64_t sampleCount = frameCount * fromFmt.numChannels;

    switch (fromSF) {
    case SF_PCM_i16:
        switch (toSF) {
        case SF_PCM_i16:
            memcpy(pcmTo, pcmFrom, (size_t)(sampleCount * fromFmt.containerBitDepth / 8));
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
            return ConvertCpp(
                    pcmFrom, fromFmt,
                    pcmTo,   toFmt, channelMap, frameCount);
        case SF_PCM_i24:
            memcpy(pcmTo, pcmFrom, (size_t)(sampleCount * fromFmt.containerBitDepth / 8));
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
        return ConvertCpp(
                pcmFrom, fromFmt,
                pcmTo,   toFmt, channelMap, frameCount);
    default:
        return E_INVALIDARG;
    }
    
}

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

/// PCMサンプル値をdouble型で戻します。整数値が入っている場合(-1.0 ≤ v < +1.0)に正規化。
static double
GetSampleValueAsDouble(
        const uint8_t *buf,
        int64_t frameNr,
        int ch,
        const WWNativePcmFmt &fmt)
{
    double d = 0.0;

    // FLOATの場合、32bitのみ対応。
    assert(!fmt.isFloat || (fmt.containerBitDepth == 32 && fmt.validBitDepth == 32));

    if (fmt.numChannels <= ch) {
        // チャンネル番号が範囲外の場合、0を戻します。DoPの場合は問題あり。
        return 0.0;
    }

    const int64_t sampleNr = frameNr  * fmt.numChannels + ch;
    const int64_t bytePos  = sampleNr * fmt.containerBitDepth / 8;

    switch (fmt.validBitDepth) {
    case 16:
        {
            int16_t v
                = (buf[bytePos  ])
                + (buf[bytePos+1]<<8);
            d = v / (INT16_MAX + 1.0);
            break;
        }
    case 24:
        {
            int32_t v
                = (buf[bytePos  ]<<8)
                + (buf[bytePos+1]<<16)
                + (buf[bytePos+2]<<24);
            d = v / (INT32_MAX + 1.0);
            break;
        }
    case 32:
        if (fmt.isFloat) {
            // 32bit float
            uint32_t v
                = (buf[bytePos  ])
                + (buf[bytePos+1]<<8)
                + (buf[bytePos+2]<<16)
                + (buf[bytePos+3]<<24);
            float vf = *((float*)&v);
            d = vf;
            break;
        } else {
            // 32bit Int
            int32_t v
                = (buf[bytePos  ])
                + (buf[bytePos+1]<<8)
                + (buf[bytePos+2]<<16)
                + (buf[bytePos+3]<<24);
            d = v / (INT32_MAX + 1.0);
            break;
        }
    default:
        assert(0);
        break;
    }

    return d;
}

/// PCMサンプル値dを書き込みます。
static void
SetSampleValue(
        const double d,
        uint8_t *buf,
        int64_t frameNr,
        int ch,
        const WWNativePcmFmt &fmt)
{
    // FLOATの場合、32bit。
    assert(!fmt.isFloat || (fmt.containerBitDepth == 32 && fmt.validBitDepth == 32));

    // 書き込みチャンネル数は範囲内。
    assert(0 <= ch && ch < fmt.numChannels);

    const int64_t sampleNr = frameNr  * fmt.numChannels + ch;
    const int64_t bytePos  = sampleNr * fmt.containerBitDepth / 8;

    switch (fmt.containerBitDepth) {
    case 16:
        {
            int16_t v = 0;
            if ((double)INT16_MAX/(INT16_MAX+1) < d) {
                v = INT16_MAX;
            } else if (d < -1.0) {
                v = INT16_MIN;
            } else {
                v = (int16_t)(d * (INT16_MAX+1));
            }

            buf[bytePos + 0] = (v   ) & 0xff;
            buf[bytePos + 1] = (v>>8) & 0xff;
        }
        break;
    case 24:
        {
            int32_t v = 0;
            if (8388607.0/8388608.0 < d) {
                v = INT32_MAX;
            } else if (d < -1.0) {
                v = INT32_MIN;
            } else {
                v = (int32_t)(d * 0x80000000LL);
            }

            buf[bytePos + 0] = (v>>8)  & 0xff;
            buf[bytePos + 1] = (v>>16) & 0xff;
            buf[bytePos + 2] = (v>>24) & 0xff;
        }
        break;
    case 32:
        if (fmt.isFloat) {
            // 32bit float
            float f = (float)d;
            uint32_t v = *((uint32_t*)&f);

            buf[bytePos + 0] =  v      & 0xff;
            buf[bytePos + 1] = (v>>8)  & 0xff;
            buf[bytePos + 2] = (v>>16) & 0xff;
            buf[bytePos + 3] = (v>>24) & 0xff;
        } else {
            // 32bit int
            switch (fmt.validBitDepth) {
            case 24:
                {
                    int32_t v = 0;
                    if (8388607.0/8388608.0 < d) {
                        v = INT32_MAX;
                    } else if (d < -1.0) {
                        v = INT32_MIN;
                    } else {
                        v = (int32_t)(d * 0x80000000LL);
                    }

                    buf[bytePos + 0] = 0;
                    buf[bytePos + 1] = (v>>8)  & 0xff;
                    buf[bytePos + 2] = (v>>16) & 0xff;
                    buf[bytePos + 3] = (v>>24) & 0xff;
                }
                break;
            case 32:
                {
                    int32_t v = 0;
                    if ((double)INT32_MAX/(INT32_MAX+1.0) < d) {
                        v = INT32_MAX;
                    } else if (d < -1.0) {
                        v = INT32_MIN;
                    } else {
                        v = (int32_t)(d * 0x80000000LL);
                    }

                    buf[bytePos + 0] =  v      & 0xff;
                    buf[bytePos + 1] = (v>>8)  & 0xff;
                    buf[bytePos + 2] = (v>>16) & 0xff;
                    buf[bytePos + 3] = (v>>24) & 0xff;
                }
                break;
            default:
                assert(0);
                break;
            }
        }
        break;
    default:
        assert(0);
        break;
    }
}

/// PCMフォーマット変換処理。チャンネル数が異なる場合や、ビットフォーマットが異なる場合にも対応。
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
            // PCM無音で初期化。
            double v = 0;

            if (toFmt.isDoP) {
                // DoP無音。(1bit audioデータ列は0x69を並べます。)
                if (i%2==0) {
                    v = 0x05696900 / (INT32_MAX + 1.0);
                } else {
                    v = 0xfa696900 / (INT32_MAX + 1.0);
                }
            }

            int chFrom = channelMap[chTo];
            if (0 <= chFrom) {
                v = GetSampleValueAsDouble(pcmFrom, i, chFrom, fromFmt);
            }

            SetSampleValue(v, pcmTo, i, chTo, toFmt);
        }
    }

    return S_OK;
}

/// true: 0→0, 1→1、... k→k 全チャンネル同一のチャンネルが対応するマップ。
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
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt,
        const int *channelMap, const int64_t frameCount)
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
        // 入出力チャンネル数が異なる。または、チャンネル対応を入れ替える処理。
        return ConvertCpp(
                pcmFrom, fromFmt,
                pcmTo,   toFmt, channelMap, frameCount);
    }

    // 入出力サンプルカウントは同じ値＝＝sampleCount。
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

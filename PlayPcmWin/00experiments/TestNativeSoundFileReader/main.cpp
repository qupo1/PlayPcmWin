#include "WWNativeWavReader.h"
#include "WWNativePcmFmt.h"
#include <stdio.h>
#include <Windows.h>
#include <stdint.h>
#include <MMReg.h>
#include "WWStopwatch.h"


struct RiffHeader {
    char riff[4];
    uint32_t fileBytes;
    char wave[4];
};

static void
PrintUsage(void)
{
    printf("Usage: TestNativeSoundFileReader wavFilePath\n");
}

struct WavFileInf {
    WWNativePcmFmt pcmFmt;
    int64_t numFrames;

    /// ファイル先頭からPCMデータの先頭までのオフセット。
    int64_t pcmDataOffset;
};

struct UnknownHeader {
    char fourcc[4];
    uint32_t bytes;
};

static HRESULT
SkipToSpecifiedHeader(FILE *fp, const char *fourcc, uint32_t *payloadBytes_out)
{
    HRESULT hr = E_FAIL;
    UnknownHeader uh;

    do {
        int bytes = (int)fread(&uh, 1, sizeof uh, fp);
        if (bytes != sizeof uh) {
            printf("Error: find data header failed\n");
            return E_FAIL;
        }

        if (0 == memcmp(uh.fourcc, fourcc, 4)) {
            // 発見。
            *payloadBytes_out = uh.bytes;
            return S_OK;
        }

        // 次のヘッダに移動。
        int r = fseek(fp, uh.bytes, SEEK_CUR);
        if (0 != r) {
            printf("Error: find data header failed\n");
            return E_FAIL;
        }
    } while (true);
}

static HRESULT
ReadWavHeader(const wchar_t *path, WavFileInf *wfi)
{
    HRESULT hr = E_FAIL;
    RiffHeader rh;
    WAVEFORMATEXTENSIBLE wfex;
    uint32_t dataBytes = 0;
    UnknownHeader uh;

    FILE *fp = nullptr;
    errno_t ercd = _wfopen_s(&fp, path, L"rb");
    if (ercd != 0 || fp == nullptr) {
        printf("Error: file open failed.\n");
        return E_FAIL;
    }

    int bytes = (int)fread(&rh, 1, 12, fp);
    if (bytes != 12) {
        printf("Error: RIFF header read failed.\n");
        hr = E_FAIL;
        goto end;
    }

    if (0 != memcmp(rh.riff, "RIFF", 4) ||
        0 != memcmp(rh.wave, "WAVE", 4)) {
        printf("Error: this is not RIFF WAVE.\n");
        hr = E_FAIL;
        goto end;
    }

    hr = SkipToSpecifiedHeader(fp, "fmt ", &uh.bytes);
    if (FAILED(hr)) {
        goto end;
    }

    bytes = (int)fread(&wfex, 1, uh.bytes, fp);

    switch (wfex.Format.wFormatTag) {
    case WAVE_FORMAT_PCM:
        if (uh.bytes < 14) {
            printf("Error: WAVEFORMAT header size too small\n");
            hr = E_FAIL;
            goto end;
        }
        if (18 <= uh.bytes) {
            wfi->pcmFmt.containerBitDepth = wfex.Format.wBitsPerSample;
            wfi->pcmFmt.validBitDepth = wfex.Format.wBitsPerSample;
        } else {
            wfi->pcmFmt.containerBitDepth = 16;
            wfi->pcmFmt.validBitDepth     = 16;
        }
        wfi->pcmFmt.isDoP = false;
        wfi->pcmFmt.isFloat = false;
        wfi->pcmFmt.numChannels = wfex.Format.nChannels;
        wfi->pcmFmt.sampleRate = wfex.Format.nSamplesPerSec;
        break;
    case WAVE_FORMAT_EXTENSIBLE:
        if (uh.bytes < 40) {
            printf("Error: WAVEFORMAT header size too small\n");
            hr = E_FAIL;
            goto end;
        }
        wfi->pcmFmt.validBitDepth = wfex.Samples.wValidBitsPerSample;
        wfi->pcmFmt.containerBitDepth = wfex.Format.wBitsPerSample;
        wfi->pcmFmt.isDoP = false;
        wfi->pcmFmt.numChannels = wfex.Format.nChannels;
        wfi->pcmFmt.sampleRate = wfex.Format.nSamplesPerSec;
        
        wfi->pcmFmt.isFloat = false;
        if (0 == memcmp(&wfex.SubFormat, &KSDATAFORMAT_SUBTYPE_IEEE_FLOAT, 16)) {
            wfi->pcmFmt.isFloat = true;
        }
        break;
    default:
        printf("Error: Unsupported wFormatTag type %d\n", wfex.Format.wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    hr = SkipToSpecifiedHeader(fp, "data", &dataBytes);
    if (FAILED(hr)) {
        goto end;
    }

    wfi->pcmDataOffset = ftell(fp);
    wfi->numFrames = dataBytes / wfi->pcmFmt.ContainerBytesPerFrame();

end:
    fclose(fp);
    fp = nullptr;

    return hr;
}


static HRESULT
Run(const wchar_t * path)
{
    HRESULT hr = E_FAIL;
    WWNativeWavReader nwr;
    WavFileInf wfi;
    WWNativePcmFmt tgtFmt;

    uint8_t *wavBuf = nullptr;
    WWStopwatch sw;
    double elapsed = 0;

    hr = nwr.Init();
    if (FAILED(hr)) {
        return hr;
    }

    // WAVファイルヘッダを読みフォーマットを調べます。
    hr = ReadWavHeader(path, &wfi);
    if (FAILED(hr)) {
        return hr;
    }

    // Target Formatを決めます。
    tgtFmt = wfi.pcmFmt;
    tgtFmt.containerBitDepth = 32;
    tgtFmt.validBitDepth = 24;

    wavBuf = (uint8_t*)_aligned_malloc(tgtFmt.ContainerBytesPerFrame() * wfi.numFrames, 64);
    if (nullptr == wavBuf) {
        printf("Error: Memory exhausted\n");
        hr = E_OUTOFMEMORY;
        goto end;
    }

    hr = nwr.PcmReadStart(path, wfi.pcmFmt, tgtFmt, nullptr);
    if (FAILED(hr)) {
        return hr;
    }

    sw.Start();

    hr = nwr.PcmReadOne(wfi.pcmDataOffset, wfi.numFrames, &wavBuf[0]);
    if (FAILED(hr)) {
        goto end;
    }

    elapsed = sw.ElapsedSeconds();

    printf("%f sec. %.1f Mbytes/sec\n", elapsed, (double)(wfi.numFrames * wfi.pcmFmt.ContainerBytesPerFrame()) *0.001 * 0.001 / elapsed);

end:
    nwr.PcmReadEnd();
    nwr.Term();

    _aligned_free(wavBuf);
    wavBuf = nullptr;

    return hr;
}

int
wmain(int argc, wchar_t *argv[])
{
    if (argc < 2) {
        PrintUsage();
        return 1;
    }

    const wchar_t *path = argv[1];

    if (FAILED(Run(path))) {
        return 1;
    }

    return 0;
}

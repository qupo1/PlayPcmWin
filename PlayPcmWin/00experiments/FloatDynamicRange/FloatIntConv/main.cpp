#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <math.h>
#include <assert.h>
#include <stdint.h>

static void
PrintUsage(void)
{
    printf("FloatIntConv -IntToFloat|-FloatToInt bitDepth inFile.bin outFile.bin\n");
    printf("  bitDepth: 16 or 24\n");
}

enum Mode {
    M_IntToFloat,
    M_FloatToInt
};

static int
Conv(Mode mode, int bitDepth, const char *inFile, const char *outFile)
{
    FILE *fpr = nullptr;
    FILE *fpw = nullptr;

    int ercd = fopen_s(&fpr, inFile, "rb");
    if (ercd != 0 || fpr == nullptr) {
        printf("E: fopen failed %s\n", inFile);
        return -1;
    }

    ercd = fopen_s(&fpw, outFile, "wb");
    if (ercd != 0 || fpw == nullptr) {
        printf("E: fopen failed %s\n", outFile);
        return -1;
    }

    const int bytesPerSample = (bitDepth+7) / 8;
    const int nSamples = (int)pow(2.0, (double)bitDepth);
    const int nMultiply = (int)pow(2.0, (double)(bitDepth-1));

    switch (mode) {
    case M_IntToFloat:
        for (int i=0; i<nSamples; ++i) {
            int32_t v = 0;
            int r = fread(&v, 1, bytesPerSample, fpr);
            if (r != bytesPerSample) {
                printf("E: file read error\n");
                return -1;
            }

            // リトルエンディアンの計算機で実行することを想定。
            // freadで下の桁から数字が埋まる。
                
            // マイナスの数値の符号ビットを算術シフトで引き伸ばします。
            // v == 0xffff, bitDepth=16のときv=-1になる。
            v <<= (32 - bitDepth);
            v >>= (32 - bitDepth);

            // Int to Float conversion.
            float vF = v/nMultiply;

            r = fwrite(&vF, 1, 4, fpw);
            if (r != 4) {
                printf("E: file write error %d expected 4\n", r);
                return -1;
            }
        }
        break;
    case M_FloatToInt:
        for (int i=0; i<nSamples; ++i) {
            int32_t v = 0;
            float vF = 0;

            int r = fread(&vF, 1, 4, fpr);
            if (r != 4) {
                printf("E: file read error\n");
                return -1;
            }

            // float to int conversion.
            v = (int32_t)(vF * nMultiply);

            r = fwrite(&v, 1, bytesPerSample, fpw);
            if (r != bytesPerSample) {
                printf("E: file write error %d expected %d\n", r, bytesPerSample);
                return -1;
            }
        }
        break;
    default:
        assert(0);
        break;
    }

    fclose(fpw);
    fpw = nullptr;

    fclose(fpr);
    fpr = nullptr;

    return 0;
}


int
main(int argc, char *argv[])
{
    int bitDepth = 0;
    const char *inFile = nullptr;
    const char *outFile = nullptr;
    Mode mode = M_IntToFloat;

    if (argc != 5) {
        PrintUsage();
        return 1;
    }

    if (0 == strcmp("-IntToFloat", argv[1])) {
        mode = M_IntToFloat;
    } else if (0 == strcmp("-FloatToInt", argv[1])) {
        mode = M_FloatToInt;
    } else {
        printf("E: Unknown param %s\n", argv[1]);
        PrintUsage();
        return 1;
    }

    bitDepth = atoi(argv[2]);
    if (bitDepth != 16 && bitDepth != 24) {
        printf("E: bitdepth not supported\n");
        PrintUsage();
        return 1;
    }

    inFile = argv[3];
    outFile = argv[4];

    int hr = Conv(mode, bitDepth, inFile, outFile);
    if (hr < 0) {
        PrintUsage();
        return 1;
    }

    return 0;
}

#include "WWNativeSoundFileReaderDLL.h"
#include "WWNativeWavReader.h"
#include "WWInstanceMgr.h"

static WWInstanceMgr<WWNativeWavReader> gNWRMgr;

/// 初期化。スレッドプールの作成、読み出しバッファ確保、スレッド処理完了待ち合わせイベント作成等。
/// @return 0以上: インスタンスId。負: エラー。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderInit(void)
{
    int id = 0;
    auto self = gNWRMgr.New(&id);
    HRESULT hr = self->Init();
    if (FAILED(hr)) {
        gNWRMgr.Delete(id);
        self = nullptr;
        return hr;
    }

    return id;
}

extern "C" WWNATIVESOUNDFILEREADERDLL_API
uint8_t * __stdcall
WWNativeSoundFileReaderAllocNativeBuffer(int64_t bytes)
{
    uint8_t *p = (uint8_t*)malloc(bytes);
    memset(p, 0, bytes);

    printf("Allocated                     %p bytes=%llx\n", p, bytes);
    return p;
}

extern "C" WWNATIVESOUNDFILEREADERDLL_API
void __stdcall
WWNativeSoundFileReaderReleaseNativeBuffer(uint8_t *ptr)
{
    printf("freed %p\n", ptr);
    return free(ptr);
}


/// 終了処理。
/// @param id Init()の戻り値のインスタンスID。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderTerm(int id)
{
    auto self = gNWRMgr.Find(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    self->Term();
    gNWRMgr.Delete(id);
    self = nullptr;

    return S_OK;
}


/// ファイル読み出し開始。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderStart(int id, const wchar_t *path, const WWNativePcmFmt & origPcmFmt, const WWNativePcmFmt & tgtPcmFmt, const int *channelMap)
{
    auto self = gNWRMgr.Find(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    for (int ch=0; ch<tgtPcmFmt.numChannels; ++ch) {
        int cm = channelMap[ch];

        // cm==-1の時無音のチャンネルにする。
        if (cm < -1 || tgtPcmFmt.numChannels <= cm) {
            printf ("E: WWNativeSoundFileReaderStart ChannelMap invalid\n");
            return E_INVALIDARG;
        }
    }

    return self->PcmReadStart(path, origPcmFmt, tgtPcmFmt, channelMap);
}

/// 読み終わるまでブロックします。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderReadOne(int id, const int64_t fileOffset, const int64_t sampleCount, uint8_t *bufTo, const int64_t bufToPos)
{
    auto self = gNWRMgr.Find(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    return self->PcmReadOne(fileOffset, sampleCount, &bufTo[bufToPos]);
}


/// ファイル読み出し終了。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderReadEnd(int id)
{
    auto self = gNWRMgr.Find(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    self->PcmReadEnd();
    return S_OK;
}


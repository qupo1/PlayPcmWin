#include "WWNativeSoundFileReaderDLL.h"
#include "WWNativeWavReader.h"
#include <Windows.h>
#include <map>
#include <assert.h>

static HANDLE gMutex = nullptr;

class StaticInitializer {
public:
    StaticInitializer(void) {
        assert(gMutex == nullptr);
        gMutex = CreateMutex(nullptr, FALSE, nullptr);
    }

    ~StaticInitializer(void) {
        assert(gMutex);
        ReleaseMutex(gMutex);
        gMutex = nullptr;
    }
};

static StaticInitializer gStaticInitializer;

/////////////////////////////////////////////////////////////////////////////////////////////

volatile int gNextId = 1;

/// 物置の実体。グローバル変数。
static std::map<int, WWNativeWavReader*> gInstanceMap;

/////////////////////////////////////////////////////////////////////////////////////////////

static WWNativeWavReader *
NewInstance(int *id_return)
{
    WWNativeWavReader * self = new WWNativeWavReader();
    if (nullptr == self) {
        printf("E: NewInstance failed\n");
        return nullptr;
    }

    assert(gMutex);
    WaitForSingleObject(gMutex, INFINITE);

    gInstanceMap.insert(std::make_pair(gNextId, self));
    *id_return = gNextId;

    ++gNextId;

    ReleaseMutex(gMutex);
    return self;
}

static void
DeleteInstance(int id)
{
    auto ite = gInstanceMap.find(id);
    if (ite == gInstanceMap.end()) {
        // mapに登録されていない場合。
        return;
    }

    assert(gMutex);
    WaitForSingleObject(gMutex, INFINITE);

    auto self = gInstanceMap[id];
    gInstanceMap.erase(id);
    delete self;
    self = nullptr;

    ReleaseMutex(gMutex);
}

static WWNativeWavReader *
FindInstanceById(int id)
{
    assert(gMutex);
    WaitForSingleObject(gMutex, INFINITE);

    std::map<int, WWNativeWavReader*>::iterator ite
        = gInstanceMap.find(id);
    if (ite == gInstanceMap.end()) {
        ReleaseMutex(gMutex);
        printf("E: FindInstanceById not found %d\n", id);
        return nullptr;
    }

    // 発見。
    ReleaseMutex(gMutex);
    return ite->second;
}

// ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

/// 初期化。スレッドプールの作成、読み出しバッファ確保、スレッド処理完了待ち合わせイベント作成等。
/// @return 0以上: インスタンスId。負: エラー。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderInit(void)
{
    int id = 0;
    auto self = NewInstance(&id);
    HRESULT hr = self->Init();
    if (FAILED(hr)) {
        DeleteInstance(id);
        self = nullptr;
        return hr;
    }

    return id;
}

/// 終了処理。
/// @param id Init()の戻り値のインスタンスID。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderTerm(int id)
{
    auto self = FindInstanceById(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    self->Term();
    DeleteInstance(id);
    self = nullptr;

    return S_OK;
}


/// ファイル読み出し開始。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderStart(int id, const wchar_t *path, const WWNativePcmFmt & origPcmFmt, const WWNativePcmFmt & tgtPcmFmt, const int *channelMap)
{
    auto self = FindInstanceById(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    return self->PcmReadStart(path, origPcmFmt, tgtPcmFmt, channelMap);
}

/// 読み終わるまでブロックします。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderReadOne(int id, const int64_t fileOffset, const int64_t sampleCount, uint8_t *bufTo)
{
    auto self = FindInstanceById(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    return self->PcmReadOne(fileOffset, sampleCount, bufTo);
}


/// ファイル読み出し終了。
extern "C" WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderReadEnd(int id)
{
    auto self = FindInstanceById(id);
    if (self == nullptr) {
        // 見つからない。
        return E_INVALIDARG;
    }

    self->PcmReadEnd();
    return S_OK;
}


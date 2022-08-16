#pragma once

#include <Windows.h>
#include <stdint.h>
#include <vector>

/// Multiple-queue IOCP and multi-threaded with Threadpool
class WWFileReaderMT {
public:
    typedef void ReadCompletedCB(uint64_t pos, uint8_t *buf, int bytes);

    WWFileReaderMT(void) :
            mhFile(INVALID_HANDLE_VALUE),
            mhIocp(INVALID_HANDLE_VALUE),
            mCompletionKey(0),
            mNumOfQueues(8),
            mFileSz(0),
            mTp(nullptr),
            mTpWork(nullptr),
            mReadCompletedCB(nullptr) {
    }

    ~WWFileReaderMT(void) {
        Close();
        Term();
    }

    /// バッファメモリの確保等の準備。
    HRESULT Init(int nQueues = 8);

    /// メモリの解放等。
    void Term(void);

    /// Open成功すると、ファイルサイズがFileSz()で取得できます。
    HRESULT Open(const wchar_t *path);

    // ファイルサイズ。
    int64_t FileSz(void) const { return mFileSz; }

    // 位置posからbytesバイト読み出します。24MiB(3MiB x 8 queues)以上の3MiBの倍数にすると効率的。
    HRESULT Read(int64_t pos, int64_t bytes, ReadCompletedCB cb);

    // ファイルを閉じます。
    void Close(void);

    /// ファイル読み出し完了CB。内部で使用する物です。
    void ioCallback(void);

private:
    HANDLE mhFile;
    HANDLE mhIocp;
    UINT mCompletionKey;
    int mNumOfQueues;
    int64_t mFileSz;
    static const int mReadFragmentSz = 1024 * 1024 * 3;
    PTP_POOL mTp;
    PTP_WORK mTpWork;
    ReadCompletedCB *mReadCompletedCB;
    TP_CALLBACK_ENVIRON mCe;

    /// WaitForMultipleObjects待ち合わせ用の、Eventのハンドルの配列。参照
    std::vector<HANDLE> mWaitEventAry;

    struct ReadCtx {
        ReadCtx(void) :
                buf(nullptr),
                isUsed(false),
                pos(0),
                readBytes(0),
                idx(-1),
                waitEvent(INVALID_HANDLE_VALUE) {
            memset(&overlapped,0,sizeof overlapped);
        }

        HRESULT Init(int aIdx) {
            HRESULT hr = E_FAIL;
            idx = aIdx;

            if (buf != nullptr) {
                printf("Error: ReadCtx::Init buf is not null\n");
                return E_FAIL;
            }

            buf = (uint8_t *)VirtualAlloc(nullptr, mReadFragmentSz, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (buf == nullptr) {
                hr = GetLastError();
                printf("Error: ReadCtx::Init VirtualAlloc failed. %x\n", hr);
                return hr;
            }

            waitEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
            if (waitEvent == nullptr) {
                hr = GetLastError();
                waitEvent = INVALID_HANDLE_VALUE;
                printf("Error: ReadCtx::Init CreateEvent failed. %x\n", hr);
                return hr;
            }
            return S_OK;
        }

        void Term(void) {
            if (waitEvent != INVALID_HANDLE_VALUE) {
                CloseHandle(waitEvent);
                waitEvent = INVALID_HANDLE_VALUE;
            }

            if (buf != nullptr) {
                VirtualFree(buf, 0, MEM_RELEASE);
                buf = nullptr;
            }
        }

        ~ReadCtx(void) {
            Term();
        }

        // 最初のメンバーがOVERLAPPEDになるようにします。これによりOVERLAPPEDをReadCtxにキャストすることが出来る。
        OVERLAPPED overlapped;

        /// VirtualAllocによってページアライン確保します。
        uint8_t *buf;

        bool isUsed;

        int64_t pos;

        int readBytes;

        int idx;
        HANDLE waitEvent;
    };

    std::vector<ReadCtx> mReadCtx;

    /// 未使用のReadCtxを探します。無いときnullptr。
    ReadCtx *FindAvailableReadCtx(void);

    // 1個スレッド処理完了を待つ。
    HRESULT WaitAnyThreadCompletion(void);

    // 利用可能なReadCtx数。
    int CountAvailableReadCtx(void);
};

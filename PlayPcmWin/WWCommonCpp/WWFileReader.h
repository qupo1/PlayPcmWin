﻿#pragma once

#include <Windows.h>
#include <stdint.h>
#include <vector>

/// Multiple-queue with IO Completion ports and single threaded version.
class WWFileReader {
public:
    typedef void ReadCompletedCB(uint64_t pos, uint8_t *buf, int bytes);

    WWFileReader(void) :
            mhFile(INVALID_HANDLE_VALUE),
            mhIocp(INVALID_HANDLE_VALUE),
            mCompletionKey(0),
            mNumOfQueues(8),
            mFileSz(0) {
    }

    ~WWFileReader(void) {
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

private:
    HANDLE mhFile;
    HANDLE mhIocp;
    UINT mCompletionKey;
    int mNumOfQueues;
    int64_t mFileSz;
    static const int mReadFragmentSz = 1024 * 1024 * 3;

    struct ReadCtx {
        ReadCtx(void) :
                buf(nullptr),
                isUsed(false),
                pos(0),
                readBytes(0),
                idx(-1) {
            memset(&overlapped,0,sizeof overlapped);
        }

        bool Init(int aIdx) {
            idx = aIdx;

            if (buf != nullptr) {
                printf("Error: ReadCtx::Init buf is not null\n");
                return false;
            }

            buf = (uint8_t *)VirtualAlloc(nullptr, mReadFragmentSz, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (buf == nullptr) {
                printf("Error: ReadCtx::Init VirtualAlloc failed.\n");
                return false;
            }

            return true;
        }

        void Term(void) {
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

    };

    std::vector<ReadCtx> mReadCtx;

    /// 未使用のReadCtxを探します。無いときnullptr。
    ReadCtx *FindAvailableReadCtx(void);

    // 1個IO完了を待つ。
    HRESULT WaitAnyIOCompletion(ReadCompletedCB cb);

    // 利用可能なReadCtx数。
    int CountAvailableReadCtx(void);
};

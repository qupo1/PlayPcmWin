﻿#include "WWFileReaderMT.h"
#include <stdio.h>
#include <assert.h>

// IO Completion portsを使用して効率的にファイルを読みます。
// 参考：https://docs.microsoft.com/en-us/windows/win32/fileio/i-o-completion-ports#:~:text=I%2FO%20completion%20ports%20provide%20an%20efficient%20threading%20model,whose%20sole%20purpose%20is%20to%20service%20these%20requests.


// ThreadPoolを使用し、複数スレッドで結果を受け取って処理します。
// Thread Pools https://docs.microsoft.com/en-us/windows/win32/procthread/thread-pools
// CreateThreadpoolWork https://docs.microsoft.com/en-us/windows/win32/api/threadpoolapiset/nf-threadpoolapiset-createthreadpoolwork
// CreateThreadpoolWorkのコールバック https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms687396(v=vs.85)
// IoCompletionCallback https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms684124(v=vs.85)
// GetQueuedCompletionStatus https://docs.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-getqueuedcompletionstatus

static void CALLBACK
sIoCallback(
        PTP_CALLBACK_INSTANCE instance,
        PVOID                 parameter,
        PTP_WORK              work)
{
    WWFileReaderMT *self = (WWFileReaderMT*)parameter;
    self->ioCallback();
}

HRESULT
WWFileReaderMT::Init(int nQueues)
{
    HRESULT hr = E_FAIL;

    mNumOfQueues  = nQueues;

    // ReadCtxを作成します。
    mReadCtx.resize(mNumOfQueues);
    int idx=0;
    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++idx, ++it) {
        ReadCtx &rc = *it;
        hr = rc.Init(idx);
        if (FAILED(hr)) {
            printf("Init ReadCtx failed %x\n", hr);
            return hr;
        }
    }

    mWaitEventAry.resize(mReadCtx.size());
    for (int i=0; i<(int)mWaitEventAry.size(); ++i) {
        mWaitEventAry[i] = mReadCtx[i].waitEvent;
    }

    // Threadpool、ThreadpoolWorkは何度も使いまわせるので、最初に作成します。
    mTp = CreateThreadpool(nullptr);
    if (nullptr == mTp) {
        hr = GetLastError();
        printf("Init CreateThreadpool failed %x\n", hr);
        return hr;
    }

    InitializeThreadpoolEnvironment(&mCe);
    SetThreadpoolCallbackPool(&mCe, mTp);
    mTpWork = CreateThreadpoolWork(sIoCallback, this, &mCe);
    if (nullptr == mTpWork) {
        hr = GetLastError();
        printf("Init CreateThreadpoolWork failed %x\n", hr);
        return hr;
    }

    return S_OK;
}

void
WWFileReaderMT::Term(void)
{
    if (mhIocp != INVALID_HANDLE_VALUE) {
        CloseHandle(mhIocp);
        mhIocp = 0;
    }

    if (mTp != nullptr) {
        CloseThreadpool(mTp);
        mTp = nullptr;

        DestroyThreadpoolEnvironment(&mCe);
    }
    if (mTpWork != nullptr) {
        CloseThreadpoolWork(mTpWork);
        mTpWork = nullptr;
    }

    // ReadCtxを削除します。
    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++it) {
        ReadCtx &rc = *it;
        rc.Term();
    }
    mReadCtx.clear();

    // mReadCtxが保持するWaitEventのハンドルをクローズし無効化したのでmWaitEventAryが持っている参照を消します。
    mWaitEventAry.clear();
}

HRESULT
WWFileReaderMT::Open(const wchar_t *path)
{
    HRESULT hr = E_FAIL;
    BOOL br = FALSE;

    if (mhFile != INVALID_HANDLE_VALUE) {
        printf("Error: Open with not closed state %x\n", hr);
        goto end;
    }

    // CreateFileW https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew

    mhFile = CreateFile(path,
            GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN | FILE_FLAG_OVERLAPPED, 0);
    if (mhFile == INVALID_HANDLE_VALUE) {
        // ファイルが開けない。
        hr =  GetLastError();
        printf("Error: CreateFile failed %x\n", hr);
        goto end;
    }

    // ファイルハンドルとIOCPとを関連付け。
    assert(mhIocp == INVALID_HANDLE_VALUE);
    mhIocp = CreateIoCompletionPort(mhFile, nullptr, mCompletionKey, mNumOfQueues);
    if (mhIocp == INVALID_HANDLE_VALUE) {
        // IOCP関連付け失敗。
        hr =  GetLastError();
        printf("Error: CreateIoCompletionPort failed %x\n", hr);
        goto end;
    }

    // ファイルサイズを調べます。
    br = GetFileSizeEx(mhFile, (PLARGE_INTEGER)&mFileSz);
    if (!br) {
        hr = GetLastError();
        printf("Error: CreateIoCompletionPort failed %x\n", hr);
        goto end;
    }

    // 成功。
    hr = S_OK;

end:
    if (FAILED(hr)) {
        Close();
    }

    return hr;
}

void
WWFileReaderMT::Close(void)
{
    if (mhFile != INVALID_HANDLE_VALUE) {
        CloseHandle(mhFile);
        mhFile = INVALID_HANDLE_VALUE;

        // ファイルハンドルとIOCPの関連付けが無くなる。IOCPは他の用途に使用できないので、CloseHandleします。
        CloseHandle(mhIocp);
        mhIocp = INVALID_HANDLE_VALUE;
    }
    
    mFileSz = 0;
}

static void
SetReadOffsetToOverlappedMember(int64_t offs, OVERLAPPED &ol)
{
    ULARGE_INTEGER posLH;
    posLH.QuadPart = offs;

    ol.Offset     = posLH.LowPart;
    ol.OffsetHigh = posLH.HighPart;
}

// 未使用のReadCtxを探します。無いときnullptr。
WWFileReaderMT::ReadCtx *
WWFileReaderMT::FindAvailableReadCtx(void)
{
    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++it) {
        ReadCtx &rc = *it;

        if (!rc.isUsed) {
            return &rc;
        }
    }

    return nullptr;
}

int
WWFileReaderMT::CountAvailableReadCtx(void)
{
    int cnt = 0;

    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++it) {
        ReadCtx &rc = *it;

        if (!rc.isUsed) {
            ++cnt;
        }
    }

    return cnt;
}


HRESULT
WWFileReaderMT::WaitAnyThreadCompletion(int *idx_return)
{
    HRESULT hr = E_FAIL;

    *idx_return = -1;

    // どれかのスレッドが終わるまで待つ。
    DWORD r = WaitForMultipleObjects((DWORD)mWaitEventAry.size(), &mWaitEventAry[0], FALSE, INFINITE);
    if (WAIT_FAILED == r) {
        hr = GetLastError();
        printf("Error: WaitAnyThreadCompletion WaitForMultipleObjects failed, error %x\n", hr);
        return hr;
    }

    *idx_return = r - WAIT_OBJECT_0;

    return S_OK;
}

HRESULT
WWFileReaderMT::Read(int64_t fileOffs, int64_t bytes, ReadCompletedCB cb, void *tag)
{
    HRESULT hr = E_FAIL;
    BOOL    br = FALSE;
    int idx = -1;

    mReadCompletedCB = cb;
    mTag = tag;

    if (mFileSz < fileOffs + bytes) {
        printf("WWFileReaderMT::Read bytes too large\n");
        return E_INVALIDARG;
    }

    for (int64_t cnt=0; cnt<bytes; cnt+= mReadFragmentSz, fileOffs += mReadFragmentSz) {
        ReadCtx  *rc = FindAvailableReadCtx();
        if (nullptr == rc) {
            // 1個IOが終わるまで待ちます。
            hr = WaitAnyThreadCompletion(&idx);
            if (FAILED(hr)) {
                printf("Read WaitIOCompletion failed %x\n", hr);
                return E_FAIL;
            }
            // 成功。
            assert(0 <= idx && idx < (int)mReadCtx.size());
            rc = &mReadCtx[idx];

            while(rc->isUsed) {
                // これは、たまに起こる。
                // printf("%s:%d waiting thread %d completion\n", __FILE__, __LINE__, idx);
                Sleep(1);
            }
        }

        rc->isUsed = true;

        // 読み出し開始位置のセット。
        SetReadOffsetToOverlappedMember(fileOffs, rc->overlapped);

        int readBytes = mReadFragmentSz;
        if (bytes < cnt + readBytes) {
            readBytes = (int)(bytes - cnt);
        }

        rc->fileOffs = fileOffs;
        rc->bytesFromReadStart = cnt;
        rc->readBytes = readBytes;

        // printf("%s:%d ReadFile fileOffs=%llx %p %x\n", __FILE__, __LINE__, fileOffs, rc->buf, readBytes);

        // 読み出し開始。
        br = ReadFile(mhFile, rc->buf, readBytes, nullptr, &rc->overlapped);
        if (!br) {
            // PENDING(正常)の場合も有り得る。
            hr = GetLastError();
            if (hr != ERROR_IO_PENDING) {
                printf("Error: FileReader::Read ReadFile failed %d\n", hr);
                return hr;
            }
        }

        // スレッドを起動し読み出し完了を待ちます。
        SubmitThreadpoolWork(mTpWork);
    }

    while (CountAvailableReadCtx() != mReadCtx.size()) {
        hr = WaitAnyThreadCompletion(&idx);
        if (FAILED(hr)) {
            printf("Read WaitIOCompletion failed %x\n", hr);
            return E_FAIL;
        }
    }

    return S_OK;
}

// IO読み出し完了後処理のスレッド。
void
WWFileReaderMT::ioCallback(void)
{
    DWORD bytesXfer = 0;
    ULONG_PTR ulKey = 0;
    LPOVERLAPPED overlapped = nullptr;
    BOOL br = FALSE;
    HRESULT hr = E_FAIL;

    // 何れかの読み出しが完了するまで待ちます。
    br = GetQueuedCompletionStatus(mhIocp, &bytesXfer, &ulKey, &overlapped, INFINITE);
    if (!br) {
        hr = GetLastError();
        printf("Error: ioCallback GetQueuedCompletionStatus failed %d\n", hr);

        if (overlapped == nullptr) {
            printf("Error: not recoverable error\n");
            return;
        }
    }

    // IO完了コールバック。
    ReadCtx *pRC = (ReadCtx*)overlapped;

    // キューに入るときはFIFO順だが、
    // 出てくる順番は不定！
    // printf("%d ", pRC->idx);

    // 読み出し完了したのでコールバックを呼びます。
    mReadCompletedCB(pRC->fileOffs, pRC->bytesFromReadStart, pRC->buf, pRC->readBytes, mTag);

    pRC->isUsed = false;

    SetEvent(pRC->waitEvent);
}

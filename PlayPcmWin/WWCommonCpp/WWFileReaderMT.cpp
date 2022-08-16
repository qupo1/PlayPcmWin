#include "WWFileReaderMT.h"
#include <stdio.h>
#include <assert.h>

// IO Completion ports���g�p���Č����I�Ƀt�@�C����ǂ݂܂��B
// �Q�l�Fhttps://docs.microsoft.com/en-us/windows/win32/fileio/i-o-completion-ports#:~:text=I%2FO%20completion%20ports%20provide%20an%20efficient%20threading%20model,whose%20sole%20purpose%20is%20to%20service%20these%20requests.


// ThreadPool���g�p���A�����X���b�h�Ō��ʂ��󂯎���ď������܂��B
// Thread Pools https://docs.microsoft.com/en-us/windows/win32/procthread/thread-pools
// CreateThreadpoolWork https://docs.microsoft.com/en-us/windows/win32/api/threadpoolapiset/nf-threadpoolapiset-createthreadpoolwork
// CreateThreadpoolWork�̃R�[���o�b�N https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms687396(v=vs.85)
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

    // ReadCtx���쐬���܂��B
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
    for (int i=0; i<mWaitEventAry.size(); ++i) {
        mWaitEventAry[i] = mReadCtx[i].waitEvent;
    }

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

    // ReadCtx���폜���܂��B
    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++it) {
        ReadCtx &rc = *it;
        rc.Term();
    }
    mReadCtx.clear();

    // mReadCtx���ێ�����WaitEvent�̃n���h�����N���[�Y�������������̂�mWaitEventAry�������Ă���Q�Ƃ������܂��B
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
        // �t�@�C�����J���Ȃ��B
        hr =  GetLastError();
        printf("Error: CreateFile failed %x\n", hr);
        goto end;
    }

    // �t�@�C���n���h����IOCP�Ƃ��֘A�t���B
    assert(mhIocp == INVALID_HANDLE_VALUE);
    mhIocp = CreateIoCompletionPort(mhFile, nullptr, mCompletionKey, mNumOfQueues);
    if (mhIocp == INVALID_HANDLE_VALUE) {
        // IOCP�֘A�t�����s�B
        hr =  GetLastError();
        printf("Error: CreateIoCompletionPort failed %x\n", hr);
        goto end;
    }

    // �t�@�C���T�C�Y�𒲂ׂ܂��B
    br = GetFileSizeEx(mhFile, (PLARGE_INTEGER)&mFileSz);
    if (!br) {
        hr = GetLastError();
        printf("Error: CreateIoCompletionPort failed %x\n", hr);
        goto end;
    }

    // �����B
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

        // �t�@�C���n���h����IOCP�̊֘A�t���������Ȃ�BIOCP�͑��̗p�r�Ɏg�p�ł��Ȃ��̂ŁACloseHandle���܂��B
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

// ���g�p��ReadCtx��T���܂��B�����Ƃ�nullptr�B
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
WWFileReaderMT::WaitAnyThreadCompletion(void)
{
    HRESULT hr = E_FAIL;

    // �ǂꂩ�̃X���b�h���I���܂ő҂B
    DWORD r = WaitForMultipleObjects((DWORD)mWaitEventAry.size(), &mWaitEventAry[0], FALSE, INFINITE);
    if (WAIT_FAILED == r) {
        hr = GetLastError();
        printf("Error: WaitAnyThreadCompletion WaitForMultipleObjects failed, error %x\n", hr);
        return hr;
    }

    return S_OK;
}

HRESULT
WWFileReaderMT::Read(int64_t pos, int64_t bytes, ReadCompletedCB cb)
{
    HRESULT hr = E_FAIL;
    BOOL    br = FALSE;

    mReadCompletedCB = cb;
    
    for (int64_t cnt=0; cnt<bytes; cnt+= mReadFragmentSz, pos += mReadFragmentSz) {
        ReadCtx  *rc = FindAvailableReadCtx();
        if (nullptr == rc) {
            // 1��IO���I���܂ő҂��܂��B
            hr = WaitAnyThreadCompletion();
            if (FAILED(hr)) {
                printf("Read WaitIOCompletion failed %x\n", hr);
                return E_FAIL;
            }
            // �����B
            rc = FindAvailableReadCtx();
            assert(rc != nullptr);
        }

        rc->isUsed = true;

        // �ǂݏo���J�n�ʒu�̃Z�b�g�B
        SetReadOffsetToOverlappedMember(pos, rc->overlapped);

        int wantBytes = mReadFragmentSz;
        if (bytes < pos + wantBytes) {
            wantBytes = (int)(bytes - pos);
        }

        rc->pos = pos;
        rc->readBytes = wantBytes;

        // �ǂݏo���J�n�B
        br = ReadFile(mhFile, rc->buf, wantBytes, nullptr, &rc->overlapped);
        if (!br) {
            hr = GetLastError();
            if (hr != ERROR_IO_PENDING) {
                printf("Error: FileReader::Read ReadFile failed %d\n", hr);
                return hr;
            }
        }

        // �X���b�h���N�����ǂݏo��������҂��܂��B
        SubmitThreadpoolWork(mTpWork);
    }

    while (CountAvailableReadCtx() != mReadCtx.size()) {
        hr = WaitAnyThreadCompletion();
        if (FAILED(hr)) {
            printf("Read WaitIOCompletion failed %x\n", hr);
            return E_FAIL;
        }
    }

    return S_OK;
}

// IO�ǂݏo�������㏈���̃X���b�h�B
void
WWFileReaderMT::ioCallback(void)
{
    DWORD bytesXfer = 0;
    ULONG_PTR ulKey = 0;
    LPOVERLAPPED overlapped = nullptr;
    BOOL br = FALSE;
    HRESULT hr = E_FAIL;

    br = GetQueuedCompletionStatus(mhIocp, &bytesXfer, &ulKey, &overlapped, INFINITE);
    if (!br) {
        hr = GetLastError();
        printf("Error: ioCallback GetQueuedCompletionStatus failed %d\n", hr);

        if (overlapped == nullptr) {
            printf("Error: not recoverable error\n");
            return;
        }
    }

    // IO�����R�[���o�b�N�B
    ReadCtx *pRC = (ReadCtx*)overlapped;

    // �L���[�ɓ���Ƃ���FIFO�������A
    // �o�Ă��鏇�Ԃ͕s��I
    // printf("%d ", pRC->idx);

    // �ǂݏo�����������̂ŃR�[���o�b�N���Ăт܂��B
    mReadCompletedCB(pRC->pos, pRC->buf, pRC->readBytes);

    pRC->isUsed = false;

    SetEvent(pRC->waitEvent);
}

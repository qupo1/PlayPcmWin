#include "WWFileReader.h"
#include <stdio.h>
#include <assert.h>

// IO Completion ports���g�p���Č����I�Ƀt�@�C����ǂ݂܂��B
// �Q�l�Fhttps://docs.microsoft.com/en-us/windows/win32/fileio/i-o-completion-ports#:~:text=I%2FO%20completion%20ports%20provide%20an%20efficient%20threading%20model,whose%20sole%20purpose%20is%20to%20service%20these%20requests.

HRESULT
WWFileReader::Init(int nQueues, int nThreads)
{
    HRESULT hr = E_FAIL;

    assert(nThreads == 1);

    mNumOfQueues  = nQueues;
    mNumOfThreads = nThreads;

    // ReadCtx���쐬���܂��B
    mReadCtx.resize(mNumOfQueues);
    int idx=0;
    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++idx, ++it) {
        ReadCtx &rc = *it;
        if (!rc.Init(idx)) {
            printf("Init ReadCtx failed\n");
            return E_FAIL;
        }
    }


    return S_OK;
}

void
WWFileReader::Term(void)
{
    if (mhIocp != INVALID_HANDLE_VALUE) {
        CloseHandle(mhIocp);
        mhIocp = 0;
    }

    // ReadCtx���폜���܂��B
    for (auto & it=mReadCtx.begin(); it!=mReadCtx.end(); ++it) {
        ReadCtx &rc = *it;
        rc.Term();
    }
    mReadCtx.clear();
}

HRESULT
WWFileReader::Open(const wchar_t *path)
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
WWFileReader::Close(void)
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
WWFileReader::ReadCtx *
WWFileReader::FindAvailableReadCtx(void)
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
WWFileReader::CountAvailableReadCtx(void)
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
WWFileReader::WaitAnyIOCompletion(ReadCompletedCB cb)
{
    HRESULT hr = E_FAIL;
    BOOL br = false;
    DWORD nBytesXfer = 0;
    DWORD_PTR key = 0;
    LPOVERLAPPED completedOverlapped = nullptr;
    ReadCtx *pRC = nullptr;

    br = GetQueuedCompletionStatus(mhIocp,
            &nBytesXfer,
            &key,
            &completedOverlapped,
            INFINITE);
    if (!br) {
        hr = GetLastError();

        // Either of the two
        // �E the function failed to dequeue a completion packet (CompletedOverlapped is not NULL)
        // �E it dequeued a completion packet of a failed I/O operation (CompletedOverlapped is NULL)
        printf("Error: GetQueuedCompletionStatus on the IoPort failed, error %x\n", hr);
        return hr;
    }

    // �����B
    pRC = (ReadCtx*)completedOverlapped;

    // �ǂݏo�����������̂ŃR�[���o�b�N���Ăт܂��B
    cb(pRC->pos, pRC->buf, pRC->readBytes);

    pRC->isUsed = false;

    // �L���[�ɓ���Ƃ���FIFO�������A
    // �o�Ă��鏇�Ԃ͕s��I
    // printf("%d ", pRC->idx);

    return S_OK;
}

HRESULT
WWFileReader::Read(int64_t pos, int64_t bytes, ReadCompletedCB cb)
{
    HRESULT hr = E_FAIL;
    BOOL br = FALSE;
    
    for (int64_t cnt=0; cnt<bytes; cnt+= mReadFragmentSz, pos += mReadFragmentSz) {
        ReadCtx  *rc = FindAvailableReadCtx();
        if (nullptr == rc) {
            // 1��IO���I���܂ő҂��܂��B
            hr = WaitAnyIOCompletion(cb);
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

        br = ReadFile(mhFile, rc->buf, wantBytes, nullptr, &rc->overlapped);
        if (!br) {
            hr = GetLastError();
            if (hr != ERROR_IO_PENDING) {
                printf("Error: FileReader::Read ReadFile failed %d\n", hr);
                return hr;
            }
        }
    }

    while (CountAvailableReadCtx() != mReadCtx.size()) {
        hr = WaitAnyIOCompletion(cb);
        if (FAILED(hr)) {
            printf("Read WaitIOCompletion failed %x\n", hr);
            return E_FAIL;
        }
    }

    return S_OK;
}


#pragma once

#include <Windows.h>
#include <stdint.h>

class MMapReadFile {
public:
    MMapReadFile(void) {
        m_hFile = 0;
        m_hMap = 0;
        m_basePtr = nullptr;
        m_fileSize.QuadPart = 0;
    }

    ~MMapReadFile(void) {
        Close();
    }

    HRESULT Open(const wchar_t *path);
    void Close(void);

    /// ファイルサイズを戻します。(バイト)
    int64_t FileSize(void) const { return m_fileSize.QuadPart; }

    /// マップされたメモリの先頭アドレスを戻します。
    void * BaseAddr(void) const { return m_basePtr; }

private:
    HANDLE m_hFile;
    HANDLE m_hMap;
    void * m_basePtr;
    LARGE_INTEGER m_fileSize;
};


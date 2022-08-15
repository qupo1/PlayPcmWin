#pragma once

#include <Windows.h>
#include <stdint.h>

class WWMMapReadFile {
public:
    WWMMapReadFile(void) {
        m_hFile = 0;
        m_hMap = 0;
        m_basePtr = nullptr;
        m_fileSize.QuadPart = 0;
    }

    ~WWMMapReadFile(void) {
        Close();
    }

    HRESULT Open(const wchar_t *path);

    /// ファイルサイズを戻します。(バイト)。Openで調べます。
    int64_t FileSize(void) const { return m_fileSize.QuadPart; }

    /// マップされたメモリの先頭アドレスを戻します。Openすると使用できます。
    const void * BaseAddr(void) const { return m_basePtr; }

    void Close(void);

private:
    HANDLE m_hFile;
    HANDLE m_hMap;
    void * m_basePtr;
    LARGE_INTEGER m_fileSize;
};


#include "WWMMapReadFile.h"
#include <assert.h>
#include <stdio.h>

void
WWMMapReadFile::Close(void)
{
    if (m_basePtr != nullptr) {
        UnmapViewOfFile(m_basePtr);
        m_basePtr = nullptr;
    }

    if (m_hMap != 0) {
        CloseHandle(m_hMap);
        m_hMap = 0;
    }

    if (m_hFile) {
        CloseHandle(m_hFile);
        m_hFile = 0;
    }
}

HRESULT
WWMMapReadFile::Open(const wchar_t *path)
{
    HRESULT hr = S_OK;

    assert(m_hFile == 0);
    assert(m_hMap == 0);
    assert(m_basePtr == nullptr);

    // shareMode should be exclusive. https://docs.microsoft.com/en-us/windows/win32/memory/creating-a-file-mapping-object

    // CreateFileW https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew
    m_hFile = CreateFile(path, 
        GENERIC_READ, 0 /* exclusive access */ , nullptr, OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL, 0);
    if (m_hFile == INVALID_HANDLE_VALUE) {
        // ファイルが開けない。
        printf("Error: CreateFile failed %d\n", GetLastError());
        hr = E_FAIL;
        goto Failed;
    }

    if (!GetFileSizeEx(m_hFile, &m_fileSize)) {
        printf("Error: GetFileSize failed %d\n", GetLastError());
        hr = E_FAIL;
        goto Failed;
    }

    if (m_fileSize.QuadPart == 0) {
        printf("Error: File is empty\n");
        hr = E_FAIL;
        goto Failed;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfilemappinga
    m_hMap = CreateFileMapping(
        m_hFile, nullptr /* mapping attr */, PAGE_READONLY,
        0 /* maxSizeHigh */, 0 /* maxSizeLow */,
        nullptr /* name */);
    if (m_hMap == 0) {
        printf("Error: CreateFileMapping failed %d\n", GetLastError());
        hr = E_FAIL;
        goto Failed;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-mapviewoffile
    m_basePtr = MapViewOfFile(
        m_hMap, FILE_MAP_READ, 0 /* fileOffsetHigh */, 0 /* fileOffsetLow */,
        0 /* all the file is mapped */);
    if (m_basePtr == nullptr) {
        printf("Error: MapViewOfFile failed %d\n", GetLastError());
        hr = E_FAIL;
        goto Failed;
    }

    // 成功。
    return S_OK;

Failed:
    if (m_basePtr != nullptr) {
        UnmapViewOfFile(m_basePtr);
        m_basePtr = nullptr;
    }

    if (m_hMap != 0) {
        CloseHandle(m_hMap);
        m_hMap = 0;
    }

    if (m_hFile) {
        CloseHandle(m_hFile);
        m_hFile = 0;
    }

    return hr;
}


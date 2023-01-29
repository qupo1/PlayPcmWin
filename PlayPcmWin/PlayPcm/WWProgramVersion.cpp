#include "WWProgramVersion.h"
#include <stdint.h>
#include <stdio.h>

HRESULT
WWProgramVersion(uint32_t* majorVersion, uint32_t* minorVersion, uint32_t* minorVersion2, uint32_t* minorVersion3)
{
	wchar_t fileName[MAX_PATH + 1] = {};
	DWORD sz  = 0;
	DWORD handle = 0;
	LPVOID pBuf = nullptr;
	UINT uLen = sizeof(VS_FIXEDFILEINFO);
	BOOL b = FALSE;
	char *s = nullptr;
	DWORD dw = 0;
	VS_FIXEDFILEINFO* vf = nullptr;
	
	if (majorVersion == nullptr ||
			minorVersion == nullptr) {
		return E_INVALIDARG;
	}

	dw = GetModuleFileNameW(nullptr, fileName, MAX_PATH);
	if (dw == 0) {
		return E_FAIL;
	}

	sz = GetFileVersionInfoSizeW(fileName, &handle);
	if (sz == 0) {
		return E_FAIL;
	}

	s = new char[sz];
	b = GetFileVersionInfoW(fileName, 0, sz, (LPVOID)s);
	if (b == 0) {
		delete [] s;
		return E_FAIL;
	}

	b = VerQueryValueW(s, L"\\", &pBuf, &uLen);
	if (b == 0) {
		delete[] s;
		return E_FAIL;
	}

	vf = (VS_FIXEDFILEINFO*)pBuf;

	*majorVersion = 0xffff & (vf->dwFileVersionMS >> 16);
	*minorVersion = 0xffff & (vf->dwFileVersionMS >> 0);

	if (minorVersion2 != nullptr) {
		*minorVersion2 = 0xffff & (vf->dwFileVersionLS >> 16);
	}
	if (minorVersion3 != nullptr) {
		*minorVersion3 = 0xffff & (vf->dwFileVersionLS >> 0);
	}

	delete[] s;
	return S_OK;
}

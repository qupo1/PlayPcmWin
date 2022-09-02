#pragma once

// C#層とC++層の糊のコードです。

#ifdef WWNATIVESOUNDFILEREADERDLL_EXPORTS
#define WWNATIVESOUNDFILEREADERDLL_API __declspec(dllexport)
#else
#define WWNATIVESOUNDFILEREADERDLL_API __declspec(dllimport)
#endif

#include <SDKDDKVer.h>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include "WWNativePcmFmt.h"

extern "C" {

/// 初期化。スレッドプールの作成、読み出しバッファ確保、スレッド処理完了待ち合わせイベント作成等。
/// @return 0以上: インスタンスId。負: エラー。
WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderInit(void);

WWNATIVESOUNDFILEREADERDLL_API
uint8_t * __stdcall
WWNativeSoundFileReaderAllocNativeBuffer(int64_t bytes);

WWNATIVESOUNDFILEREADERDLL_API
void __stdcall
WWNativeSoundFileReaderReleaseNativeBuffer(uint8_t *ptr);



/// ファイル読み出し開始。
WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderStart(int id, const wchar_t *path, const WWNativePcmFmt & origPcmFmt, const WWNativePcmFmt & tgtPcmFmt, const int *channelMap);

/// 読み終わるまでブロックします。
WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderReadOne(int id, const int64_t fileOffset, const int64_t sampleCount, uint8_t *bufTo, const int64_t bufToPos);

/// ファイル読み出し終了。
WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderReadEnd(int id);

/// 終了処理。
/// @param id Init()の戻り値のインスタンスID。
WWNATIVESOUNDFILEREADERDLL_API
int __stdcall
WWNativeSoundFileReaderTerm(int id);

}; // extern "C"

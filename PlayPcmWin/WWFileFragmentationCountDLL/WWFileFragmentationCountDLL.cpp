#include "WWFileFragmentationCount.h"

#include <string>
#include <Windows.h>


static void
PrintRetrievalPointersBuffer(const RETRIEVAL_POINTERS_BUFFER *p)
{
    printf("\nRetrievalPointersBuffer\n");
    printf("  ExtentCount=%d\n", p->ExtentCount);
    printf("  StartingVcn=%lld\n", p->StartingVcn.QuadPart);
    printf("    (VCN: Virtual Cluster Number, sequential number from zero: VCN 0 points the start of the file data)\n");
    printf("    (LCN: Logical Cluster Number, sequential number from zero: LCN 0 points the boot sector)\n");

    int64_t startVcn = p->StartingVcn.QuadPart;
    for (size_t i=0; i<p->ExtentCount; ++i) {
        printf("  Extent[%d] VCN from %lld to %lld is stored from LCN %lld\n",
            i, startVcn, p->Extents[i].NextVcn.QuadPart-1, p->Extents[i].Lcn.QuadPart);
        startVcn = p->Extents[i].NextVcn.QuadPart;
    }
}

static void
PrintNtfsVolumeData(const NTFS_VOLUME_DATA_BUFFER *p)
{
    printf("\nNtfsVolumeData\n");
    printf("  VolumeSerialNumber = %016llx\n", p->VolumeSerialNumber.QuadPart);
    printf("  NumberSectors      = %lld\n", p->NumberSectors.QuadPart);
    printf("  TotalClusters      = %lld\n", p->TotalClusters.QuadPart);
    printf("  FreeClusters       = %lld (%d %%)\n", p->FreeClusters.QuadPart, 100LL * p->FreeClusters.QuadPart / p->TotalClusters.QuadPart);
    printf("  TotalReserved      = %lld\n", p->TotalReserved.QuadPart);

    printf("  BytesPerSector               = %d\n", p->BytesPerSector);
    printf("  BytesPerCluster              = %d\n", p->BytesPerCluster);
    printf("  BytesPerFileRecordSegment    = %d\n", p->BytesPerFileRecordSegment);
    printf("  ClustersPerFileRecordSegment = %d\n", p->ClustersPerFileRecordSegment);

    printf("  MftValidDataLength = %lld\n", p->MftValidDataLength.QuadPart);
    printf("  MftStartLcn        = %lld\n", p->MftStartLcn.QuadPart);
    printf("  Mft2StartLcn       = %lld\n", p->Mft2StartLcn.QuadPart);
    printf("  MftZoneStart       = %lld\n", p->MftZoneStart.QuadPart);
    printf("  MftZoneEnd         = %lld\n", p->MftZoneEnd.QuadPart);
}

static void
PrintFileStandardInfo(const FILE_STANDARD_INFO *p)
{
    printf("\nFileStandardInfo\n");
    printf("  AllocationSize = %lld\n", p->AllocationSize.QuadPart);
    printf("  EndOfFile = %lld\n", p->EndOfFile.QuadPart);
    printf("  NumberOfLinks = %d\n", p->NumberOfLinks);
    printf("  DeletePending = %d\n", (int)p->DeletePending);
    printf("  Directory = %d\n", (int)p->Directory);
}

static HRESULT
GetFileStandardInfo(HANDLE fh, FILE_STANDARD_INFO &si_return)
{
    HRESULT hr = E_FAIL;
    BOOL br = FALSE;

    br = GetFileInformationByHandleEx(fh,
            FileStandardInfo,
            &si_return,
            sizeof si_return);
    if (!br) {
        hr = GetLastError();
        printf("Error: GetFileInformationByHandleEx: %d\n", hr);
        return hr;
    }

    return S_OK;
}

static HRESULT
GetNtfsVolumeData(const wchar_t *filePath, NTFS_VOLUME_DATA_BUFFER &nvdb_return)
{
    HRESULT hr = E_FAIL;
    BOOL br = FALSE;
    HANDLE fh = INVALID_HANDLE_VALUE;
    wchar_t volumePath[256];
    DWORD retBytes = 0;

    std::wstring s(filePath);

    std::size_t pos = s.find_first_of(L':');
    if (pos == std::wstring::npos) {
        printf("Error: file name should be full path containing colon.\n");
        return E_FAIL;
    }

    std::wstring drvLetter = s.substr(pos-1, 1);

    // \\.\C:   C: volume.
    swprintf_s(volumePath, L"\\\\.\\%s:", drvLetter.c_str());

    // ボリュームを読み込みモードで開く。
    fh = CreateFile(volumePath,
            GENERIC_READ, //< dwDesiredAccess
            FILE_SHARE_READ | FILE_SHARE_WRITE, //< dwShareMode
            nullptr, //< security attributes
            OPEN_EXISTING, //< dwCreationDisposition
            FILE_FLAG_NO_BUFFERING | FILE_FLAG_SEQUENTIAL_SCAN, //< Attr and flags
            nullptr); //< hTemplate
    if (INVALID_HANDLE_VALUE == fh) {
        hr = GetLastError();
        printf("Error: open volume failed %d\n", hr);
        goto end;
    }

    br = DeviceIoControl(fh,
            FSCTL_GET_NTFS_VOLUME_DATA,
            nullptr,
            0,
            &nvdb_return,
            sizeof nvdb_return,
            &retBytes,
            nullptr);
    if (br) {
        // 成功。
        //printf("D: DeviceIoControl FSCTL_GET_NTFS_VOLUME_DATA %d bytes\n", retBytes);
        PrintNtfsVolumeData(&nvdb_return);
        hr = S_OK;
    } else {
        // エラー。
        hr = GetLastError();
        printf("Error: DeviceIoControl FSCTL_GET_NTFS_VOLUME_DATA %d\n", hr);
        goto end;
    }

end:
    if (INVALID_HANDLE_VALUE != fh) {
        CloseHandle(fh);
        fh = INVALID_HANDLE_VALUE;
    }

    return hr;
}

static HRESULT
GetFragmentationCount(const wchar_t *filePath, WWFileFragmentationInfo &fi_r)
{
    HRESULT hr = E_FAIL;
    BOOL br = FALSE;
    HANDLE fh = INVALID_HANDLE_VALUE;
    PRETRIEVAL_POINTERS_BUFFER pr = nullptr;
    DWORD sz = sizeof(RETRIEVAL_POINTERS_BUFFER) + 4096;
    STARTING_VCN_INPUT_BUFFER svi = {};
    FILE_STANDARD_INFO fsi = {};

    // 既存ファイルを読み込みモードで開く。
    fh = CreateFile(filePath,
            GENERIC_READ,
            FILE_SHARE_READ,
            nullptr, //< security attributes
            OPEN_EXISTING,
            FILE_READ_ATTRIBUTES, //< Attr and flags
            nullptr); //< hTemplate

    if (INVALID_HANDLE_VALUE == fh) {
        goto end;
    }

    hr = GetFileStandardInfo(fh, fsi);
    if (FAILED(hr)) {
        printf("Error: GetFileStandardInfo failed %d\n", hr);
        goto end;
    }
    PrintFileStandardInfo(&fsi);

    svi.StartingVcn.QuadPart = 0;

    do {
        pr = (PRETRIEVAL_POINTERS_BUFFER)malloc(sz);
        DWORD retBytes = 0;

        br = DeviceIoControl(fh,
                FSCTL_GET_RETRIEVAL_POINTERS,
                &svi,
                sizeof svi,
                pr,
                sz,
                &retBytes,
                nullptr);
        if (br) {
            // 成功。
            //printf("D: DeviceIoControl FSCTL_GET_RETRIEVAL_POINTERS %d bytes\n", retBytes);
            hr = S_OK;

            PrintRetrievalPointersBuffer(pr);

            // fi_rにコピーします。
            fi_r.nFragmentCount = pr->ExtentCount;
            fi_r.startVcn = pr->StartingVcn.QuadPart;
            int copyCount = pr->ExtentCount;
            if (WW_VCN_LCN_COUNT < copyCount) {
                copyCount = WW_VCN_LCN_COUNT;
            }
            for (int i=0; i<copyCount; ++i) {
                fi_r.lcnVcn[i].lcn = pr->Extents[i].Lcn.QuadPart;
                fi_r.lcnVcn[i].nextVcn = pr->Extents[i].NextVcn.QuadPart;
            }
            fi_r.nClusters = fsi.AllocationSize.QuadPart / fi_r.bytesPerCluster;

            printf("   total Clusters = %lld\n", fi_r.nClusters);

            hr = S_OK;
            break;
        }

        if (!br) {
            hr = GetLastError();

            if (hr == ERROR_MORE_DATA) {
                // バッファーサイズが小さすぎるので増やしてリトライする。

                free(pr);
                pr = nullptr;

                sz *= 2;
                continue;
            }

            // エラー。
            printf("Error: DeviceIoControl FSCTL_GET_RETRIEVAL_POINTERS %d\n", hr);
            goto end;
        }
    } while (true);

    // ERROR_INSUFFICIENT_BUFFER

end:
    if (INVALID_HANDLE_VALUE != fh) {
        CloseHandle(fh);
        fh = INVALID_HANDLE_VALUE;
    }
    
    free(pr);
    pr = nullptr;

    return hr;
}


extern "C" __declspec(dllexport)
int __stdcall
WWFileFragmentationCount(const wchar_t *filePath, WWFileFragmentationInfo &ffi_return)
{
    HRESULT hr = E_FAIL;
    NTFS_VOLUME_DATA_BUFFER nvdb = {};

    hr = GetNtfsVolumeData(filePath, nvdb);
    if (FAILED(hr)) {
        return hr;
    }

    ffi_return.bytesPerSector = nvdb.BytesPerSector;
    ffi_return.bytesPerCluster = nvdb.BytesPerCluster;

    hr = GetFragmentationCount(filePath, ffi_return);
    if (FAILED(hr)) {
        return hr;
    }

    return hr;
}

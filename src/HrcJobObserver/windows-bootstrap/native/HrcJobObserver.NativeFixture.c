/*
 * Synthetic offline loader/containment fixture only.
 *
 * This source deliberately has no C runtime, heap, file, registry, network,
 * COM, environment, or standard-I/O dependency. Its complete dynamic import
 * surface is audited after linking.
 */

typedef unsigned long DWORD;
typedef unsigned short WCHAR;
typedef const WCHAR *LPCWSTR;
typedef unsigned long long ULONGLONG;

#define NATIVE_IMPORT __declspec(dllimport)
#define NATIVE_NORETURN __declspec(noreturn)
#define NATIVE_STDCALL __stdcall
#define NATIVE_INFINITE ((DWORD)0xffffffffUL)
#define NATIVE_INVALID_ARGUMENT ((DWORD)87UL)

NATIVE_IMPORT LPCWSTR NATIVE_STDCALL GetCommandLineW(void);
NATIVE_IMPORT void NATIVE_STDCALL ExitProcess(DWORD exitCode);
NATIVE_IMPORT void NATIVE_STDCALL Sleep(DWORD milliseconds);

/*
 * /DEPENDENTLOADFLAG requires the image to own a load-config directory. Keep
 * this definition at the complete IMAGE_LOAD_CONFIG_DIRECTORY64 layout from
 * the pinned Windows 10.0.26100 SDK, through UmaFunctionPointers. The pinned
 * linker emits its supported 0x148-byte prefix through GuardMemcpy, patches
 * DependentLoadFlags, and omits the final UMA field. This fixture has no
 * source-level function pointer or computed indirect control flow; its calls
 * use fixed static-import IAT slots. It is deliberately not CFG-instrumented,
 * and the managed PE audit requires every emitted guard field to remain zero.
 */
typedef struct NativeCodeIntegrity {
    unsigned short Flags;
    unsigned short Catalog;
    DWORD CatalogOffset;
    DWORD Reserved;
} NativeCodeIntegrity;

typedef struct NativeLoadConfigDirectory64 {
    DWORD Size;
    DWORD TimeDateStamp;
    unsigned short MajorVersion;
    unsigned short MinorVersion;
    DWORD GlobalFlagsClear;
    DWORD GlobalFlagsSet;
    DWORD CriticalSectionDefaultTimeout;
    ULONGLONG DeCommitFreeBlockThreshold;
    ULONGLONG DeCommitTotalFreeThreshold;
    ULONGLONG LockPrefixTable;
    ULONGLONG MaximumAllocationSize;
    ULONGLONG VirtualMemoryThreshold;
    ULONGLONG ProcessAffinityMask;
    DWORD ProcessHeapFlags;
    unsigned short CsdVersion;
    unsigned short DependentLoadFlags;
    ULONGLONG EditList;
    ULONGLONG SecurityCookie;
    ULONGLONG SeHandlerTable;
    ULONGLONG SeHandlerCount;
    ULONGLONG GuardCfCheckFunctionPointer;
    ULONGLONG GuardCfDispatchFunctionPointer;
    ULONGLONG GuardCfFunctionTable;
    ULONGLONG GuardCfFunctionCount;
    DWORD GuardFlags;
    NativeCodeIntegrity CodeIntegrity;
    ULONGLONG GuardAddressTakenIatEntryTable;
    ULONGLONG GuardAddressTakenIatEntryCount;
    ULONGLONG GuardLongJumpTargetTable;
    ULONGLONG GuardLongJumpTargetCount;
    ULONGLONG DynamicValueRelocTable;
    ULONGLONG ChpeMetadataPointer;
    ULONGLONG GuardRfFailureRoutine;
    ULONGLONG GuardRfFailureRoutineFunctionPointer;
    DWORD DynamicValueRelocTableOffset;
    unsigned short DynamicValueRelocTableSection;
    unsigned short Reserved2;
    ULONGLONG GuardRfVerifyStackPointerFunctionPointer;
    DWORD HotPatchTableOffset;
    DWORD Reserved3;
    ULONGLONG EnclaveConfigurationPointer;
    ULONGLONG VolatileMetadataPointer;
    ULONGLONG GuardEhContinuationTable;
    ULONGLONG GuardEhContinuationCount;
    ULONGLONG GuardXfgCheckFunctionPointer;
    ULONGLONG GuardXfgDispatchFunctionPointer;
    ULONGLONG GuardXfgTableDispatchFunctionPointer;
    ULONGLONG CastGuardOsDeterminedFailureMode;
    ULONGLONG GuardMemcpyFunctionPointer;
    ULONGLONG UmaFunctionPointers;
} NativeLoadConfigDirectory64;

__declspec(selectany) const NativeLoadConfigDirectory64 _load_config_used = {
    sizeof(NativeLoadConfigDirectory64),
    0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0,
    0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0,
    0, { 0, 0, 0, 0 },
    0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0
};

static int IsExactArgument(const WCHAR *actual, const WCHAR *expected)
{
    while (*expected != 0) {
        if (*actual != *expected) {
            return 0;
        }

        ++actual;
        ++expected;
    }

    return *actual == 0;
}

static const WCHAR *FindArgument(LPCWSTR commandLine)
{
    const WCHAR *cursor = commandLine;

    if (cursor == 0 || *cursor != (WCHAR)'"') {
        return 0;
    }

    ++cursor;
    if (*cursor == 0 || *cursor == (WCHAR)'"') {
        return 0;
    }

    while (*cursor != 0 && *cursor != (WCHAR)'"') {
        ++cursor;
    }

    if (*cursor != (WCHAR)'"' || cursor[1] != (WCHAR)' ') {
        return 0;
    }

    return cursor + 2;
}

NATIVE_NORETURN void NATIVE_STDCALL NativeRoleEntry(void)
{
    static const WCHAR exitArgument[] = {
        '-', '-', 'n', 'a', 't', 'i', 'v', 'e', '-',
        'e', 'x', 'i', 't', 0
    };
    static const WCHAR blockArgument[] = {
        '-', '-', 'n', 'a', 't', 'i', 'v', 'e', '-',
        'b', 'l', 'o', 'c', 'k', 0
    };
    const WCHAR *argument = FindArgument(GetCommandLineW());

    if (argument != 0 && IsExactArgument(argument, exitArgument)) {
        ExitProcess(0);
    }

    if (argument != 0 && IsExactArgument(argument, blockArgument)) {
        for (;;) {
            Sleep(NATIVE_INFINITE);
        }
    }

    ExitProcess(NATIVE_INVALID_ARGUMENT);
}

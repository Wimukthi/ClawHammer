#pragma once

#include <cstdint>

#if defined(CLAWHAMMER_NATIVECORE_EXPORTS)
#define CLAWHAMMER_API extern "C" __declspec(dllexport)
#else
#define CLAWHAMMER_API extern "C" __declspec(dllimport)
#endif

using CH_ShouldCancelCallback = int(__stdcall *)(void* userData);
using CH_ReportProgressCallback = void(__stdcall *)(void* userData, int operations);
using CH_ReportMessageCallback = void(__stdcall *)(void* userData, const wchar_t* message);

enum CH_ValidationMode : int
{
    CH_ValidationOff = 0,
    CH_ValidationLight = 1,
    CH_ValidationFull = 2
};

struct CH_WorkerConfig
{
    int workerId;
    std::uint64_t seed;
    int validationMode;
    int validationIntervalMs;
    int batchSize;
    std::int64_t primeRangeMin;
    std::int64_t primeRangeMax;
    int memoryBufferBytes;
};

struct CH_WorkerCallbacks
{
    void* userData;
    CH_ShouldCancelCallback shouldCancel;
    CH_ReportProgressCallback reportProgress;
    CH_ReportMessageCallback reportError;
    CH_ReportMessageCallback reportStatus;
};

CLAWHAMMER_API int __stdcall CH_IsAvailable();
CLAWHAMMER_API int __stdcall CH_RunFloatingPoint(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks);
CLAWHAMMER_API int __stdcall CH_RunAvx(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks);
CLAWHAMMER_API int __stdcall CH_RunIntegerPrimes(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks);
CLAWHAMMER_API int __stdcall CH_RunIntegerHeavy(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks);
CLAWHAMMER_API int __stdcall CH_RunMemoryBandwidth(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks);

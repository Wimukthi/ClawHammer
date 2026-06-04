#include "ClawHammer.NativeCore.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iomanip>
#include <iterator>
#include <limits>
#include <sstream>
#include <string>
#include <vector>
#include <windows.h>

namespace
{
    constexpr int DefaultFloatBatchSize = 4096;
    constexpr int DefaultPrimeBatchSize = 2048;
    constexpr int DefaultIntegerBatchSize = 8192;
    constexpr std::uint64_t DefaultSeed = 0x9E3779B97F4A7C15ull;

    constexpr std::uint32_t IntegerModulus = 0xFFFFFFFBu;
    constexpr std::uint32_t IntegerMultiplier = 1664525u;
    constexpr std::uint32_t IntegerIncrement = 1013904223u;
    constexpr std::uint32_t IntegerLightExpected = 0x15FE3041u;
    constexpr std::uint32_t IntegerFullExpected = 0x7A2EB3C2u;

    constexpr int MemoryLightSampleCount = 256;
    constexpr int MemoryFullSampleCount = 1024;
    constexpr int PatternShift1 = 11;
    constexpr int PatternShift2 = 27;
    constexpr int PatternShift3 = 43;

    struct PrimeValidationRange
    {
        int maxValue;
        int expectedCount;
    };

    constexpr PrimeValidationRange PrimeLightRanges[] = {
        {1000, 168},
        {5000, 669}
    };

    constexpr PrimeValidationRange PrimeFullRanges[] = {
        {1000, 168},
        {5000, 669},
        {10000, 1229},
        {20000, 2262}
    };

    struct XorShift64Star
    {
        std::uint64_t state;

        explicit XorShift64Star(std::uint64_t seed) : state(seed == 0 ? DefaultSeed : seed)
        {
        }

        std::uint64_t NextULong()
        {
            std::uint64_t x = state;
            x ^= (x << 13);
            x ^= (x >> 7);
            x ^= (x << 17);
            state = x;
            return x;
        }

        double NextDouble()
        {
            constexpr double scale = 1.0 / 9007199254740992.0;
            return static_cast<double>(NextULong() >> 11) * scale;
        }

        float NextSingle()
        {
            return static_cast<float>(NextDouble());
        }
    };

    template <typename T>
    struct MathState
    {
        std::vector<T> v1;
        std::vector<T> v2;
        std::vector<T> v3;
        std::vector<T> v4;
    };

    struct NumericValidationResult
    {
        bool passed = true;
        double expected = 0.0;
        double actual = 0.0;
        double difference = 0.0;
        double tolerance = 0.0;
        int iterations = 0;
    };

    struct IntegerValidationResult
    {
        bool passed = true;
        std::uint32_t expected = 0;
        std::uint32_t actual = 0;
        int iterations = 0;
    };

    struct MemoryValidationResult
    {
        bool passed = true;
        int index = -1;
        std::uint64_t expected = 0;
        std::uint64_t actual = 0;
        int patternId = 0;
        int checkedCount = 0;
        bool fullPass = false;
    };

    bool ShouldCancel(const CH_WorkerCallbacks* callbacks)
    {
        return callbacks != nullptr &&
            callbacks->shouldCancel != nullptr &&
            callbacks->shouldCancel(callbacks->userData) != 0;
    }

    void ReportProgress(const CH_WorkerCallbacks* callbacks, int operations)
    {
        if (callbacks != nullptr && callbacks->reportProgress != nullptr && operations > 0)
        {
            callbacks->reportProgress(callbacks->userData, operations);
        }
    }

    void ReportError(const CH_WorkerCallbacks* callbacks, const std::wstring& message)
    {
        if (callbacks != nullptr && callbacks->reportError != nullptr)
        {
            callbacks->reportError(callbacks->userData, message.c_str());
        }
    }

    void ReportStatus(const CH_WorkerCallbacks* callbacks, const std::wstring& message)
    {
        if (callbacks != nullptr && callbacks->reportStatus != nullptr)
        {
            callbacks->reportStatus(callbacks->userData, message.c_str());
        }
    }

    int ClampBatchSize(int batchSize, int defaultBatchSize)
    {
        return batchSize > 0 ? batchSize : defaultBatchSize;
    }

    int ClampValidationInterval(int intervalMs)
    {
        return intervalMs >= 250 ? intervalMs : 250;
    }

    std::wstring BuildStatusMessage(int workerId, const wchar_t* kernelName, const std::wstring& detail)
    {
        std::wstringstream builder;
        builder << L"STATUS|" << workerId << L"|" << kernelName << L"|" << detail;
        return builder.str();
    }

    std::wstring BuildStatusMessage(int workerId, const wchar_t* kernelName, const wchar_t* prefix, int value)
    {
        std::wstringstream builder;
        builder << L"STATUS|" << workerId << L"|" << kernelName << L"|" << prefix << value << L")";
        return builder.str();
    }

    std::wstring FormatDouble(double value)
    {
        std::wstringstream builder;
        builder << std::setprecision(17) << value;
        return builder.str();
    }

    std::wstring FormatHex32(std::uint32_t value)
    {
        std::wstringstream builder;
        builder << L"0x" << std::uppercase << std::hex << std::setw(8) << std::setfill(L'0') << value;
        return builder.str();
    }

    std::wstring FormatHex64(std::uint64_t value)
    {
        std::wstringstream builder;
        builder << L"0x" << std::uppercase << std::hex << std::setw(16) << std::setfill(L'0') << value;
        return builder.str();
    }

    bool IsFaultInjectionEnabled(const wchar_t* target)
    {
        wchar_t value[128] = {};
        const DWORD length = GetEnvironmentVariableW(L"CLAWHAMMER_NATIVE_FAULT", value, static_cast<DWORD>(std::size(value)));
        if (length == 0 || length >= std::size(value))
        {
            return false;
        }

        const std::wstring selected(value, length);
        return selected == L"all" || selected == target;
    }

    std::wstring BuildNumericValidationError(int workerId, const wchar_t* kernelName, const wchar_t* checkName, const NumericValidationResult& result)
    {
        std::wstringstream builder;
        builder << L"Validation failed: kernel=" << kernelName
            << L", worker=" << workerId
            << L", check=" << checkName
            << L", iterations=" << result.iterations
            << L", expected=" << FormatDouble(result.expected)
            << L", actual=" << FormatDouble(result.actual)
            << L", diff=" << FormatDouble(result.difference)
            << L", tolerance=" << FormatDouble(result.tolerance);
        return builder.str();
    }

    std::wstring BuildIntegerValidationError(int workerId, const wchar_t* kernelName, const wchar_t* checkName, const IntegerValidationResult& result)
    {
        std::wstringstream builder;
        builder << L"Validation failed: kernel=" << kernelName
            << L", worker=" << workerId
            << L", check=" << checkName
            << L", iterations=" << result.iterations
            << L", expected=" << FormatHex32(result.expected)
            << L", actual=" << FormatHex32(result.actual);
        return builder.str();
    }

    std::wstring BuildMemoryValidationError(int workerId, const wchar_t* kernelName, const wchar_t* checkName, const MemoryValidationResult& result)
    {
        std::wstringstream builder;
        builder << L"Validation failed: kernel=" << kernelName
            << L", worker=" << workerId
            << L", check=" << checkName
            << L", mode=" << (result.fullPass ? L"full-pass" : L"sample")
            << L", pattern=" << result.patternId
            << L", checked=" << result.checkedCount
            << L", index=" << result.index
            << L", expected=" << FormatHex64(result.expected)
            << L", actual=" << FormatHex64(result.actual);
        return builder.str();
    }

    template <typename T>
    MathState<T> CreateMathState(int lanes, std::uint64_t seed, std::uint64_t mix, T min1, T scale1, T min2, T scale2, T min3, T scale3, T min4, T scale4)
    {
        MathState<T> state;
        state.v1.resize(lanes);
        state.v2.resize(lanes);
        state.v3.resize(lanes);
        state.v4.resize(lanes);

        XorShift64Star rng(seed ^ mix);
        for (int i = 0; i < lanes; ++i)
        {
            state.v1[i] = min1 + static_cast<T>(rng.NextDouble()) * scale1;
            state.v2[i] = min2 + static_cast<T>(rng.NextDouble()) * scale2;
            state.v3[i] = min3 + static_cast<T>(rng.NextDouble()) * scale3;
            state.v4[i] = min4 + static_cast<T>(rng.NextDouble()) * scale4;
        }

        return state;
    }

    void StepFloatingPoint(MathState<float>& state, int iterations)
    {
        constexpr float eps = 0.001f;
        constexpr float scale = 0.0001f;
        const int lanes = static_cast<int>(state.v1.size());

        for (int iter = 0; iter < iterations; ++iter)
        {
            for (int i = 0; i < lanes; ++i)
            {
                const float next1 = (state.v1[i] * state.v2[i] + state.v3[i]) * scale;
                const float next2 = (state.v2[i] * state.v4[i] + next1) * scale;
                const float next3 = state.v3[i] * next1 + next2;
                const float sqrtIn = std::fabs(next1) + std::fabs(next2) + std::fabs(next3) + eps;
                state.v1[i] = next1;
                state.v2[i] = next2;
                state.v3[i] = next3;
                state.v4[i] = std::sqrt(sqrtIn);
            }
        }
    }

    void StepAvx(MathState<double>& state, int iterations)
    {
        constexpr double eps = 0.000001;
        constexpr double scale = 0.000001;
        const int lanes = static_cast<int>(state.v1.size());

        for (int iter = 0; iter < iterations; ++iter)
        {
            for (int i = 0; i < lanes; ++i)
            {
                const double next1 = (state.v1[i] * state.v2[i] + state.v3[i]) * scale;
                const double next2 = (state.v2[i] * state.v4[i] + next1) * scale;
                const double next3 = state.v3[i] * next1 + next2;
                const double sqrtIn = std::fabs(next1) + std::fabs(next2) + std::fabs(next3) + eps;
                state.v1[i] = next1;
                state.v2[i] = next2;
                state.v3[i] = next3;
                state.v4[i] = std::sqrt(sqrtIn);
            }
        }
    }

    double RunFloatingPointReference(const MathState<float>& initial, int iterations)
    {
        constexpr double eps = 0.001;
        constexpr double scale = 0.0001;
        const int lanes = static_cast<int>(initial.v1.size());
        std::vector<double> v1(initial.v1.begin(), initial.v1.end());
        std::vector<double> v2(initial.v2.begin(), initial.v2.end());
        std::vector<double> v3(initial.v3.begin(), initial.v3.end());
        std::vector<double> v4(initial.v4.begin(), initial.v4.end());

        for (int iter = 0; iter < iterations; ++iter)
        {
            for (int i = 0; i < lanes; ++i)
            {
                const double next1 = (v1[i] * v2[i] + v3[i]) * scale;
                const double next2 = (v2[i] * v4[i] + next1) * scale;
                const double next3 = v3[i] * next1 + next2;
                const double sqrtIn = std::fabs(next1) + std::fabs(next2) + std::fabs(next3) + eps;
                v1[i] = next1;
                v2[i] = next2;
                v3[i] = next3;
                v4[i] = std::sqrt(sqrtIn);
            }
        }

        double sum = 0.0;
        for (int i = 0; i < lanes; ++i)
        {
            sum += v1[i] + v2[i] + v3[i] + v4[i];
        }
        return sum;
    }

    double RunFloatingPointKernelSum(const MathState<float>& initial, int iterations)
    {
        MathState<float> state = initial;
        StepFloatingPoint(state, iterations);
        double sum = 0.0;
        const int lanes = static_cast<int>(state.v1.size());
        for (int i = 0; i < lanes; ++i)
        {
            sum += state.v1[i] + state.v2[i] + state.v3[i] + state.v4[i];
        }
        return sum;
    }

    double RunAvxReference(const MathState<double>& initial, int iterations)
    {
        constexpr long double eps = 0.000001L;
        constexpr long double scale = 0.000001L;
        const int lanes = static_cast<int>(initial.v1.size());
        std::vector<long double> v1(initial.v1.begin(), initial.v1.end());
        std::vector<long double> v2(initial.v2.begin(), initial.v2.end());
        std::vector<long double> v3(initial.v3.begin(), initial.v3.end());
        std::vector<long double> v4(initial.v4.begin(), initial.v4.end());

        for (int iter = 0; iter < iterations; ++iter)
        {
            for (int i = 0; i < lanes; ++i)
            {
                const long double next1 = (v1[i] * v2[i] + v3[i]) * scale;
                const long double next2 = (v2[i] * v4[i] + next1) * scale;
                const long double next3 = v3[i] * next1 + next2;
                const long double sqrtIn = std::fabs(next1) + std::fabs(next2) + std::fabs(next3) + eps;
                v1[i] = next1;
                v2[i] = next2;
                v3[i] = next3;
                v4[i] = std::sqrt(sqrtIn);
            }
        }

        long double sum = 0.0L;
        for (int i = 0; i < lanes; ++i)
        {
            sum += v1[i] + v2[i] + v3[i] + v4[i];
        }
        return static_cast<double>(sum);
    }

    double RunAvxKernelSum(const MathState<double>& initial, int iterations)
    {
        MathState<double> state = initial;
        StepAvx(state, iterations);
        double sum = 0.0;
        const int lanes = static_cast<int>(state.v1.size());
        for (int i = 0; i < lanes; ++i)
        {
            sum += state.v1[i] + state.v2[i] + state.v3[i] + state.v4[i];
        }
        return sum;
    }

    template <typename T>
    T SumMathState(const MathState<T>& state)
    {
        T sum = static_cast<T>(0);
        const int lanes = static_cast<int>(state.v1.size());
        for (int i = 0; i < lanes; ++i)
        {
            sum += state.v1[i] + state.v2[i] + state.v3[i] + state.v4[i];
        }
        return sum;
    }

    int GetFloatingValidationIterations(int validationMode)
    {
        return validationMode == CH_ValidationFull ? 128 : 64;
    }

    int GetAvxValidationIterations(int validationMode)
    {
        return validationMode == CH_ValidationFull ? 96 : 48;
    }

    NumericValidationResult ValidateFloatingPointPath(int validationMode, int workerId)
    {
        NumericValidationResult result;
        result.iterations = GetFloatingValidationIterations(validationMode);
        result.tolerance = validationMode == CH_ValidationFull ? 0.05 : 0.1;

        const MathState<float> state = CreateMathState<float>(16, 0x12345678ABCDEF01ull ^ static_cast<std::uint64_t>(workerId),
            0x83A9B6C9D3F2A7C5ull, 0.1f, 4.0f, 0.1f, 3.0f, 0.1f, 2.0f, 0.1f, 5.0f);
        result.expected = RunFloatingPointReference(state, result.iterations);
        result.actual = RunFloatingPointKernelSum(state, result.iterations);
        if (IsFaultInjectionEnabled(L"fp-validation"))
        {
            result.actual += result.tolerance + 1.0;
        }
        result.difference = std::fabs(result.expected - result.actual);
        result.passed = std::isfinite(result.expected) &&
            std::isfinite(result.actual) &&
            result.difference <= result.tolerance;
        return result;
    }

    NumericValidationResult ValidateAvxPath(int validationMode)
    {
        NumericValidationResult result;
        result.iterations = GetAvxValidationIterations(validationMode);
        result.tolerance = validationMode == CH_ValidationFull ? 0.000001 : 0.00001;

        const MathState<double> state = CreateMathState<double>(8, 0x1B2A3C4D5E6F7788ull,
            0x4C957F2D3A1B0E67ull, 0.01, 30.0, 0.01, 20.0, 0.01, 15.0, 0.01, 25.0);
        result.expected = RunAvxReference(state, result.iterations);
        result.actual = RunAvxKernelSum(state, result.iterations);
        if (IsFaultInjectionEnabled(L"avx-validation"))
        {
            result.actual += result.tolerance + 1.0;
        }
        result.difference = std::fabs(result.expected - result.actual);
        result.passed = std::isfinite(result.expected) &&
            std::isfinite(result.actual) &&
            result.difference <= result.tolerance;
        return result;
    }

    bool IsPrime(std::int64_t value, const CH_WorkerCallbacks* callbacks)
    {
        if (value <= 1)
        {
            return false;
        }
        if (value == 2)
        {
            return true;
        }
        if ((value % 2) == 0)
        {
            return false;
        }
        if (ShouldCancel(callbacks))
        {
            return false;
        }

        const std::int64_t maxCheck = static_cast<std::int64_t>(std::sqrt(static_cast<double>(value)));
        for (std::int64_t i = 3; i <= maxCheck; i += 2)
        {
            if (ShouldCancel(callbacks))
            {
                return false;
            }
            if ((value % i) == 0)
            {
                return false;
            }
        }

        return true;
    }

    int CountPrimesUpTo(int maxValue, const CH_WorkerCallbacks* callbacks)
    {
        int count = 0;
        for (int i = 2; i <= maxValue; ++i)
        {
            if (ShouldCancel(callbacks))
            {
                return count;
            }
            if (IsPrime(i, callbacks))
            {
                ++count;
            }
        }
        return count;
    }

    std::wstring GetPrimeRangeDetail(int validationMode)
    {
        const PrimeValidationRange* ranges = validationMode == CH_ValidationFull ? PrimeFullRanges : PrimeLightRanges;
        const int count = validationMode == CH_ValidationFull ? static_cast<int>(std::size(PrimeFullRanges)) : static_cast<int>(std::size(PrimeLightRanges));

        std::wstringstream builder;
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                builder << L",";
            }
            builder << L"primes<=" << ranges[i].maxValue;
        }
        return builder.str();
    }

    bool ValidatePrimeRanges(int validationMode, const CH_WorkerCallbacks* callbacks)
    {
        const PrimeValidationRange* ranges = validationMode == CH_ValidationFull ? PrimeFullRanges : PrimeLightRanges;
        const int count = validationMode == CH_ValidationFull ? static_cast<int>(std::size(PrimeFullRanges)) : static_cast<int>(std::size(PrimeLightRanges));
        for (int i = 0; i < count; ++i)
        {
            if (ShouldCancel(callbacks))
            {
                return true;
            }
            if (CountPrimesUpTo(ranges[i].maxValue, callbacks) != ranges[i].expectedCount)
            {
                return false;
            }
        }
        return true;
    }

    std::uint32_t StepIntegerState(std::uint32_t state)
    {
        const std::uint64_t product = static_cast<std::uint64_t>(state) * IntegerMultiplier + IntegerIncrement;
        state = static_cast<std::uint32_t>(product % IntegerModulus);
        state ^= (state << 13);
        state ^= (state >> 7);
        state ^= (state << 17);
        return state;
    }

    std::uint32_t ComputeIntegerState(std::uint32_t state, int iterations)
    {
        for (int i = 0; i < iterations; ++i)
        {
            state = StepIntegerState(state);
        }
        return state;
    }

    int GetIntegerValidationIterations(int validationMode)
    {
        return validationMode == CH_ValidationFull ? 20000 : 5000;
    }

    IntegerValidationResult ValidateIntegerChain(int validationMode)
    {
        IntegerValidationResult result;
        const int iterations = GetIntegerValidationIterations(validationMode);
        const std::uint32_t expected = validationMode == CH_ValidationFull ? IntegerFullExpected : IntegerLightExpected;
        std::uint32_t actual = ComputeIntegerState(1u, iterations);
        if (IsFaultInjectionEnabled(L"integer-validation"))
        {
            actual ^= 1u;
        }
        result.iterations = iterations;
        result.expected = expected;
        result.actual = actual;
        result.passed = actual == expected;
        return result;
    }

    IntegerValidationResult ValidateIntegerBatch(std::uint32_t stateBeforeBatch, std::uint32_t stateAfterBatch, int batchSize)
    {
        IntegerValidationResult result;
        std::uint32_t actual = stateAfterBatch;
        if (IsFaultInjectionEnabled(L"integer-live"))
        {
            actual ^= 1u;
        }
        result.iterations = batchSize;
        result.expected = ComputeIntegerState(stateBeforeBatch, batchSize);
        result.actual = actual;
        result.passed = result.actual == result.expected;
        return result;
    }

    int GetMemorySampleCount(int validationMode)
    {
        return validationMode == CH_ValidationFull ? MemoryFullSampleCount : MemoryLightSampleCount;
    }

    std::uint64_t RotateLeft64(std::uint64_t value, int shift)
    {
        const int normalized = shift & 63;
        if (normalized == 0)
        {
            return value;
        }
        return (value << normalized) | (value >> (64 - normalized));
    }

    int GetMemoryPatternId(std::uint64_t patternCounter)
    {
        return static_cast<int>(patternCounter & 3ull);
    }

    std::uint64_t ExpectedMemoryValue(std::uint64_t seed, std::uint64_t patternCounter, int index)
    {
        const std::uint64_t baseValue = seed ^ patternCounter;
        const std::uint64_t idx = static_cast<std::uint64_t>(index);
        const std::uint64_t addressPattern = baseValue ^ (idx << PatternShift1) ^ (idx << PatternShift2) ^ (idx << PatternShift3);
        switch (GetMemoryPatternId(patternCounter))
        {
        case 1:
            return ~addressPattern;
        case 2:
            return RotateLeft64(baseValue + idx * 0x9E3779B97F4A7C15ull, static_cast<int>((idx + patternCounter) & 63ull));
        case 3:
            return baseValue ^ (1ull << ((idx + patternCounter) & 63ull)) ^ (idx * 0xD6E8FEB86659FD93ull);
        default:
            return addressPattern;
        }
    }

    void FillMemoryPattern(std::vector<std::uint64_t>& buffer, std::uint64_t seed, std::uint64_t patternCounter)
    {
        for (int i = 0; i < static_cast<int>(buffer.size()); ++i)
        {
            buffer[i] = ExpectedMemoryValue(seed, patternCounter, i);
        }
    }

    MemoryValidationResult ValidateMemoryPattern(const std::vector<std::uint64_t>& buffer,
        std::uint64_t seed,
        std::uint64_t patternCounter,
        int sampleCount,
        bool fullPass,
        const CH_WorkerCallbacks* callbacks)
    {
        MemoryValidationResult result;
        result.patternId = GetMemoryPatternId(patternCounter);
        result.fullPass = fullPass;

        if (IsFaultInjectionEnabled(L"memory-validation"))
        {
            result.passed = false;
            result.index = 0;
            result.expected = ExpectedMemoryValue(seed, patternCounter, 0);
            result.actual = result.expected ^ 1ull;
            result.checkedCount = 1;
            return result;
        }

        const std::uint64_t length = static_cast<std::uint64_t>(buffer.size());
        if (fullPass)
        {
            for (int i = 0; i < static_cast<int>(buffer.size()); ++i)
            {
                if ((i & 4095) == 0 && ShouldCancel(callbacks))
                {
                    return result;
                }
                ++result.checkedCount;
                const std::uint64_t expected = ExpectedMemoryValue(seed, patternCounter, i);
                if (buffer[static_cast<std::size_t>(i)] != expected)
                {
                    result.passed = false;
                    result.index = i;
                    result.expected = expected;
                    result.actual = buffer[static_cast<std::size_t>(i)];
                    return result;
                }
            }
            return result;
        }

        XorShift64Star rng(seed ^ patternCounter ^ 0xC2B2AE3D27D4EB4Full);
        const int checkedLimit = (std::max)(1, sampleCount);
        for (int i = 0; i < checkedLimit; ++i)
        {
            if ((i & 63) == 0 && ShouldCancel(callbacks))
            {
                return result;
            }
            const int idx = static_cast<int>(rng.NextULong() % length);
            ++result.checkedCount;
            const std::uint64_t expected = ExpectedMemoryValue(seed, patternCounter, idx);
            if (buffer[static_cast<std::size_t>(idx)] != expected)
            {
                result.passed = false;
                result.index = idx;
                result.expected = expected;
                result.actual = buffer[static_cast<std::size_t>(idx)];
                return result;
            }
        }

        return result;
    }

    int RunNativeMathFloat(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
    {
        const int workerId = config->workerId;
        const int validationMode = config->validationMode;
        const int batchSize = ClampBatchSize(config->batchSize, DefaultFloatBatchSize);
        const int validationIntervalMs = ClampValidationInterval(config->validationIntervalMs);

        MathState<float> state = CreateMathState<float>(16, config->seed, 0x83A9B6C9D3F2A7C5ull,
            0.01f, 10.0f, 0.01f, 5.0f, 0.01f, 3.0f, 0.01f, 7.0f);
        ULONGLONG lastValidationMs = GetTickCount64();

        if (validationMode != CH_ValidationOff)
        {
            const NumericValidationResult validationResult = ValidateFloatingPointPath(validationMode, workerId);
            if (!validationResult.passed)
            {
                ReportError(callbacks, BuildNumericValidationError(workerId, L"Native Vector FP", L"self-test", validationResult));
                return -2;
            }
            ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Vector FP", L"Self-test OK (compare iters=", validationResult.iterations));
        }

        while (!ShouldCancel(callbacks))
        {
            StepFloatingPoint(state, batchSize);
            ReportProgress(callbacks, batchSize);

            if (!std::isfinite(SumMathState(state)))
            {
                ReportError(callbacks, L"Validation failed: native floating-point produced NaN/Infinity.");
                return -3;
            }

            if (validationMode != CH_ValidationOff)
            {
                const ULONGLONG nowMs = GetTickCount64();
                if (nowMs - lastValidationMs >= static_cast<ULONGLONG>(validationIntervalMs))
                {
                    lastValidationMs = nowMs;
                    const NumericValidationResult validationResult = ValidateFloatingPointPath(validationMode, workerId);
                    if (!validationResult.passed)
                    {
                        ReportError(callbacks, BuildNumericValidationError(workerId, L"Native Vector FP", L"periodic-compare", validationResult));
                        return -4;
                    }
                    ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Vector FP", L"Tick OK (compare iters=", validationResult.iterations));
                }
            }
        }

        return 0;
    }

    int RunNativeMathDouble(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
    {
        const int workerId = config->workerId;
        const int validationMode = config->validationMode;
        const int batchSize = ClampBatchSize(config->batchSize, DefaultFloatBatchSize);
        const int validationIntervalMs = ClampValidationInterval(config->validationIntervalMs);

        MathState<double> state = CreateMathState<double>(8, config->seed, 0x4C957F2D3A1B0E67ull,
            0.01, 100.0, 0.01, 50.0, 0.01, 20.0, 0.01, 80.0);
        ULONGLONG lastValidationMs = GetTickCount64();

        if (validationMode != CH_ValidationOff)
        {
            const NumericValidationResult validationResult = ValidateAvxPath(validationMode);
            if (!validationResult.passed)
            {
                ReportError(callbacks, BuildNumericValidationError(workerId, L"Native AVX", L"self-test", validationResult));
                return -2;
            }
            ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native AVX", L"Self-test OK (compare iters=", validationResult.iterations));
        }

        while (!ShouldCancel(callbacks))
        {
            StepAvx(state, batchSize);
            ReportProgress(callbacks, batchSize);

            if (!std::isfinite(SumMathState(state)))
            {
                ReportError(callbacks, L"Validation failed: native AVX produced NaN/Infinity.");
                return -3;
            }

            if (validationMode != CH_ValidationOff)
            {
                const ULONGLONG nowMs = GetTickCount64();
                if (nowMs - lastValidationMs >= static_cast<ULONGLONG>(validationIntervalMs))
                {
                    lastValidationMs = nowMs;
                    const NumericValidationResult validationResult = ValidateAvxPath(validationMode);
                    if (!validationResult.passed)
                    {
                        ReportError(callbacks, BuildNumericValidationError(workerId, L"Native AVX", L"periodic-compare", validationResult));
                        return -4;
                    }
                    ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native AVX", L"Tick OK (compare iters=", validationResult.iterations));
                }
            }
        }

        return 0;
    }
}

CLAWHAMMER_API int __stdcall CH_IsAvailable()
{
    return 1;
}

CLAWHAMMER_API int __stdcall CH_RunFloatingPoint(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
{
    if (config == nullptr || callbacks == nullptr)
    {
        return -1;
    }
    return RunNativeMathFloat(config, callbacks);
}

CLAWHAMMER_API int __stdcall CH_RunAvx(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
{
    if (config == nullptr || callbacks == nullptr)
    {
        return -1;
    }
    return RunNativeMathDouble(config, callbacks);
}

CLAWHAMMER_API int __stdcall CH_RunIntegerPrimes(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
{
    if (config == nullptr || callbacks == nullptr)
    {
        return -1;
    }

    const int workerId = config->workerId;
    const int validationMode = config->validationMode;
    const int batchSize = ClampBatchSize(config->batchSize, DefaultPrimeBatchSize);
    const int validationIntervalMs = ClampValidationInterval(config->validationIntervalMs);
    const std::int64_t minValue = std::max<std::int64_t>(2, config->primeRangeMin);
    const std::int64_t maxValue = std::max<std::int64_t>(minValue, config->primeRangeMax);
    std::int64_t current = minValue + static_cast<std::int64_t>(workerId) * 2;
    if ((current % 2) == 0)
    {
        ++current;
    }
    ULONGLONG lastValidationMs = GetTickCount64();

    if (validationMode != CH_ValidationOff)
    {
        if (!ValidatePrimeRanges(validationMode, callbacks))
        {
            ReportError(callbacks, L"Validation failed: native prime self-test mismatch.");
            return -2;
        }
        ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Integer Primes", L"Self-test OK (" + GetPrimeRangeDetail(validationMode) + L")"));
    }

    while (!ShouldCancel(callbacks))
    {
        int ops = 0;
        for (int i = 0; i < batchSize; ++i)
        {
            if (ShouldCancel(callbacks))
            {
                break;
            }
            (void)IsPrime(current, callbacks);
            ++ops;
            current += 2;
            if (current > maxValue)
            {
                current = (minValue % 2) == 0 ? minValue + 1 : minValue;
            }
        }

        ReportProgress(callbacks, ops);

        if (validationMode != CH_ValidationOff)
        {
            const ULONGLONG nowMs = GetTickCount64();
            if (nowMs - lastValidationMs >= static_cast<ULONGLONG>(validationIntervalMs))
            {
                lastValidationMs = nowMs;
                if (!ValidatePrimeRanges(validationMode, callbacks))
                {
                    ReportError(callbacks, L"Validation failed: native prime count mismatch.");
                    return -3;
                }
                ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Integer Primes", L"Tick OK (" + GetPrimeRangeDetail(validationMode) + L")"));
            }
        }
    }

    return 0;
}

CLAWHAMMER_API int __stdcall CH_RunIntegerHeavy(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
{
    if (config == nullptr || callbacks == nullptr)
    {
        return -1;
    }

    const int workerId = config->workerId;
    const int validationMode = config->validationMode;
    const int batchSize = ClampBatchSize(config->batchSize, DefaultIntegerBatchSize);
    const int validationIntervalMs = ClampValidationInterval(config->validationIntervalMs);

    std::uint64_t workerMix = static_cast<std::uint64_t>(workerId);
    workerMix = workerMix ^ (workerMix << 11) ^ (workerMix << 21) ^ (workerMix << 32);
    std::uint32_t state = static_cast<std::uint32_t>((config->seed & 0xffffffffull) ^ workerMix);
    if (state == 0)
    {
        state = 1;
    }

    ULONGLONG lastValidationMs = GetTickCount64();

    if (validationMode != CH_ValidationOff)
    {
        const IntegerValidationResult chainResult = ValidateIntegerChain(validationMode);
        if (!chainResult.passed)
        {
            ReportError(callbacks, BuildIntegerValidationError(workerId, L"Native Integer Heavy", L"self-test", chainResult));
            return -2;
        }
        ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Integer Heavy", L"Self-test OK (iters=", chainResult.iterations));
    }

    while (!ShouldCancel(callbacks))
    {
        std::uint32_t checksum = 0;
        const std::uint32_t stateBeforeBatch = state;
        for (int i = 0; i < batchSize; ++i)
        {
            state = StepIntegerState(state);
            checksum ^= state;
        }

        ReportProgress(callbacks, batchSize);

        if (checksum == 0)
        {
            ReportError(callbacks, L"Validation failed: native integer checksum zero.");
            return -3;
        }

        if (validationMode != CH_ValidationOff)
        {
            const ULONGLONG nowMs = GetTickCount64();
            if (nowMs - lastValidationMs >= static_cast<ULONGLONG>(validationIntervalMs))
            {
                lastValidationMs = nowMs;
                const IntegerValidationResult liveResult = ValidateIntegerBatch(stateBeforeBatch, state, batchSize);
                if (!liveResult.passed)
                {
                    ReportError(callbacks, BuildIntegerValidationError(workerId, L"Native Integer Heavy", L"live-batch", liveResult));
                    return -4;
                }
                const IntegerValidationResult chainResult = ValidateIntegerChain(validationMode);
                if (!chainResult.passed)
                {
                    ReportError(callbacks, BuildIntegerValidationError(workerId, L"Native Integer Heavy", L"periodic-self-test", chainResult));
                    return -5;
                }
                ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Integer Heavy", L"Tick OK (live batch=", liveResult.iterations));
            }
        }
    }

    return 0;
}

CLAWHAMMER_API int __stdcall CH_RunMemoryBandwidth(const CH_WorkerConfig* config, const CH_WorkerCallbacks* callbacks)
{
    if (config == nullptr || callbacks == nullptr)
    {
        return -1;
    }

    const int workerId = config->workerId;
    const int validationMode = config->validationMode;
    const int validationIntervalMs = ClampValidationInterval(config->validationIntervalMs);
    const int clampedBytes = (std::max)(256 * 1024, config->memoryBufferBytes);
    const int length = (std::max)(1, clampedBytes / static_cast<int>(sizeof(std::uint64_t)));
    std::vector<std::uint64_t> buffer(static_cast<std::size_t>(length));
    std::uint64_t seed = config->seed ^ (static_cast<std::uint64_t>(workerId) * 0x85EBCA6Bull);
    std::uint64_t batchCounter = 0;
    ULONGLONG lastValidationMs = GetTickCount64();

    if (validationMode != CH_ValidationOff)
    {
        const int sampleCount = GetMemorySampleCount(validationMode);
        const bool fullPass = validationMode == CH_ValidationFull;
        FillMemoryPattern(buffer, seed, batchCounter);
        const MemoryValidationResult validationResult = ValidateMemoryPattern(buffer, seed, batchCounter, sampleCount, fullPass, callbacks);
        if (!validationResult.passed)
        {
            ReportError(callbacks, BuildMemoryValidationError(workerId, L"Native Memory Bandwidth", L"self-test", validationResult));
            return -2;
        }
        if (ShouldCancel(callbacks))
        {
            return 0;
        }
        std::wstringstream detail;
        detail << L"Self-test OK (" << (validationResult.fullPass ? L"full checked=" : L"samples=") << validationResult.checkedCount
            << L",pattern=" << validationResult.patternId << L")";
        ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Memory Bandwidth", detail.str()));
    }

    while (!ShouldCancel(callbacks))
    {
        const std::uint64_t patternCounter = batchCounter;
        FillMemoryPattern(buffer, seed, patternCounter);

        std::uint64_t checksum = 0;
        for (int i = 0; i < length; i += 4)
        {
            checksum ^= buffer[static_cast<std::size_t>(i)];
            if (i + 1 < length) checksum ^= buffer[static_cast<std::size_t>(i + 1)];
            if (i + 2 < length) checksum ^= buffer[static_cast<std::size_t>(i + 2)];
            if (i + 3 < length) checksum ^= buffer[static_cast<std::size_t>(i + 3)];
        }
        (void)checksum;
        ReportProgress(callbacks, length);

        if (validationMode != CH_ValidationOff)
        {
            const ULONGLONG nowMs = GetTickCount64();
            if (nowMs - lastValidationMs >= static_cast<ULONGLONG>(validationIntervalMs))
            {
                lastValidationMs = nowMs;
                const int sampleCount = GetMemorySampleCount(validationMode);
                const bool fullPass = validationMode == CH_ValidationFull;
                const MemoryValidationResult validationResult = ValidateMemoryPattern(buffer, seed, patternCounter, sampleCount, fullPass, callbacks);
                if (!validationResult.passed)
                {
                    ReportError(callbacks, BuildMemoryValidationError(workerId, L"Native Memory Bandwidth", L"periodic-readback", validationResult));
                    return -3;
                }
                if (ShouldCancel(callbacks))
                {
                    return 0;
                }
                std::wstringstream detail;
                detail << L"Tick OK (" << (validationResult.fullPass ? L"full checked=" : L"samples=") << validationResult.checkedCount
                    << L",pattern=" << validationResult.patternId << L")";
                ReportStatus(callbacks, BuildStatusMessage(workerId, L"Native Memory Bandwidth", detail.str()));
            }
        }

        ++batchCounter;
    }

    return 0;
}

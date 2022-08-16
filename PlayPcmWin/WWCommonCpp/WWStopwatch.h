#pragma once

#include <Windows.h> //< QueryPerformanceCounter()

class WWStopwatch
{
public:
    WWStopwatch(void) {
        mLast.QuadPart = 0;
        QueryPerformanceFrequency(&mFreq);
        QueryPerformanceCounter(&mStart);
    }

    void Start(void) {
        QueryPerformanceCounter(&mStart);
    }

    double ElapsedSeconds(void) {
        QueryPerformanceCounter(&mLast);

        return (double)(mLast.QuadPart - mStart.QuadPart) / mFreq.QuadPart;
    }

    int ElapsedMillisec(void) {
        QueryPerformanceCounter(&mLast);

        return (int)((mLast.QuadPart - mStart.QuadPart) * 1000 / mFreq.QuadPart);
    }

private:
    LARGE_INTEGER mFreq;
    LARGE_INTEGER mStart;
    LARGE_INTEGER mLast;
};


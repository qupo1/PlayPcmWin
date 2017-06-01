#pragma once

#include "WWIIRFilterBlock.h"
#include <assert.h>

class WWIIRFilterGraph
{
protected:
    WWIIRFilterBlock *mFilterBlockArray;
    int mCount;
    int mCapacity;
    int mOsr;

public:
    WWIIRFilterGraph(int nBlocks);

    virtual ~WWIIRFilterGraph(void);

    /// <summary>
    /// 多項式pは直列接続される。(p同士を掛けていく感じになる)
    /// </summary>
    virtual void Add(int aCount, const double *a, int bCount, const double *b);

    virtual void Filter(int n, const double *buffIn, double *buffOut) = 0;

    void SetOSR(int osr) { mOsr = osr; }
};

#pragma once

#include <Windows.h>
#include <assert.h>
#include <map>
#include <stdio.h>

/// クラスTの実体を保持、idで管理、出し入れするクラス。
template<typename T>
class WWInstanceMgr {

private:
    HANDLE mMutex;
    int mNextId;
    std::map<int, T*> mMap;

public:
    WWInstanceMgr(void) {
        mMutex = CreateMutex(nullptr, FALSE, nullptr);
        assert(mMutex);
        mNextId = 1;
    }

    ~WWInstanceMgr(void) {
        assert(mMutex);
        CloseHandle(mMutex);
        mMutex = nullptr;

        // 管理している実体を全部消します。この時点でmMapは空であることが理想。
        for (auto ite=mMap.begin(); ite != mMap.end(); ++ite) {
            T* self = ite->second;
            delete self;
            self = nullptr;
        }
        mMap.clear();
    }

    /// @param id_return 割り振られたid番号が戻ります。
    /// @return newされた実体が戻ります。
    /// Newで作成したインスタンスは、使用終了時にDeleteで消して下さい。
    T *
    New(int *id_return) {
        T * self = new T();
        if (nullptr == self) {
            printf("E: NewInstance failed\n");
            return nullptr;
        }

        assert(mMutex);
        WaitForSingleObject(mMutex, INFINITE);

        mMap.insert(std::make_pair(mNextId, self));
        *id_return = mNextId;

        ++mNextId;

        ReleaseMutex(mMutex);
        return self;
    }

    /// Newで作成した実体を消します。
    void
    Delete(int id) {
        auto ite = mMap.find(id);
        if (ite == mMap.end()) {
            // mapに登録されていない場合。
            return;
        }

        assert(mMutex);
        WaitForSingleObject(mMutex, INFINITE);

        auto self = mMap[id];
        mMap.erase(id);
        delete self;
        self = nullptr;

        ReleaseMutex(mMutex);
    }

    /// 番号がidの実体取得。
    T *
    Find(int id) {
        assert(mMutex);
        WaitForSingleObject(mMutex, INFINITE);

        auto ite = mMap.find(id);
        if (ite == mMap.end()) {
            ReleaseMutex(mMutex);
            printf("E: FindInstanceById not found %d\n", id);
            return nullptr;
        }

        // 発見。
        ReleaseMutex(mMutex);
        return ite->second;
    }
};


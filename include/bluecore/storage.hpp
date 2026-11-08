#pragma once

#include "bluecore/types.hpp"

// Ölçüm kaydı.
//
// Görev: yalnızca savunulabilir canlı örnekleri saklamak.
// İlişkili dosyalar: src/storage/storage.cpp, policy.
// Donanım: bu sürüm flash, NVS veya SD kullanmaz.
// Etki: sanitize kuralı değişirse geçmiş ekranı ve dışa aktarım aynı kaydı görür.

namespace bluecore {

struct LogRecord {
    uint32_t id;
    uint32_t monotonicMs;
    TimeQuality timeQuality;
    uint16_t year;
    uint8_t month;
    uint8_t day;
    uint8_t hour;
    uint8_t minute;
    uint8_t second;
    uint8_t stripped;
    Sample samples[kSensorCount];
};

class ILogStore {
 public:
    virtual ~ILogStore() = default;
    virtual bool append(LogRecord record) = 0;
    virtual uint32_t count() const = 0;
    virtual bool get(uint32_t index, LogRecord& out) const = 0;
};

class UnavailableLogStore : public ILogStore {
 public:
    bool append(LogRecord record) override;
    uint32_t count() const override;
    bool get(uint32_t index, LogRecord& out) const override;
};

class MemoryLogStore : public ILogStore {
 public:
    static constexpr int kCapacity = 16;

    bool append(LogRecord record) override;
    uint32_t count() const override;
    bool get(uint32_t index, LogRecord& out) const override;

 private:
    LogRecord records_[kCapacity] = {};
    uint32_t count_ = 0;
    uint32_t nextId_ = 1;
};

}  // namespace bluecore

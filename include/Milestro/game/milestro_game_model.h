#ifndef MILESTRO_GAME_MODEL_H
#define MILESTRO_GAME_MODEL_H

#include <string>
#include <utility>
#include <vector>

namespace milestro::game::model {
enum DataType : int32_t {
    DataTypeNumber = 1000,
    DataTypeBytes = 2000,
};

class DataEnvelop {
public:
    virtual DataType GetType() = 0;

    DataEnvelop() {}

    virtual ~DataEnvelop() = default;
};

class BytesWrapper : public DataEnvelop {
private:
    std::vector<uint8_t> store;

public:
    explicit BytesWrapper(std::vector<uint8_t> value)
            : store(std::move(value)) {

    };

    explicit BytesWrapper(std::string value)
            : store(value.data(), value.data() + value.size()) {

    };

    explicit BytesWrapper(uint8_t *ptr, size_t size)
            : store(ptr, ptr + size) {

    };

    virtual ~BytesWrapper() = default;

    [[nodiscard]] size_t GetSize() {
        return store.size();
    }

    [[nodiscard]] uint8_t *GetPtr() {
        return store.data();
    }

    std::vector<uint8_t> GetData() {
        return store;
    }

    DataType GetType() override {
        return DataTypeBytes;
    }
};

class NumberWrapper : public DataEnvelop {
private:
    double store;

public:
    explicit NumberWrapper(double value)
            : store(value) {

    };

    virtual ~NumberWrapper() = default;

    double GetValue() {
        return store;
    }

    DataType GetType() override {
        return DataTypeNumber;
    }
};

#define MILIZE_GAMEPROP_STR_WRAPPER(propName, breadcrumb)       \
                                                                \
    [[nodiscard]] const size_t Get##breadcrumb##Size() {        \
        return propName.size();                                 \
    }                                                           \
                                                                \
    [[nodiscard]] char* Get##breadcrumb##Ptr() {                \
        return propName.data();                                 \
    }                                                           \
                                                                \
    [[nodiscard]] void Set##breadcrumb(char* c) {               \
        propName = std::string(c);                              \
    }

#define MILESTRO_GAMEPROP_GENERIC_WRAPPER(propName, type, breadcrumb)     \
                                                                        \
    [[nodiscard]] type Get##breadcrumb() {                              \
        return propName;                                                \
    }                                                                   \
                                                                        \
    [[nodiscard]] void Set##breadcrumb(type c) {                        \
        propName = c;                                                   \
    }

#define MILESTRO_GAMEPROP_INT64_WRAPPER(propName, breadcrumb) MILESTRO_GAMEPROP_GENERIC_WRAPPER(propName, int64_t, breadcrumb)

} // namespace milestro::game::model

#endif

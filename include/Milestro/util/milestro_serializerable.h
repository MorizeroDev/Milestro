#ifndef MILESTRO_SERIALIZERABLE_H
#define MILESTRO_SERIALIZERABLE_H

#include <nlohmann/json.hpp>

namespace milestro::util::serialization {

class serializable {
public:
    virtual nlohmann::json toJson() = 0;
};

template <typename T>
concept IsSerializable = std::is_assignable_v<serializable, T>;

template <IsSerializable T>
inline nlohmann::json vectorToJson(std::vector<T> arr) {
    nlohmann::json ret;
    for (auto item: arr) {
        ret.push_back(item.toJson());
    }
    return ret;
}

inline nlohmann::json vectorToJson(std::vector<int> arr) {
    nlohmann::json ret;
    for (auto item: arr) {
        ret.push_back(item);
    }
    return ret;
}

inline nlohmann::json vectorToJson(std::vector<double> arr) {
    nlohmann::json ret;
    for (auto item: arr) {
        ret.push_back(item);
    }
    return ret;
}

inline nlohmann::json vectorToJson(std::vector<std::string> arr) {
    nlohmann::json ret;
    for (auto item: arr) {
        ret.push_back(item);
    }
    return ret;
}

} // namespace milestro::util::serialization

#endif

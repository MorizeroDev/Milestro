#ifndef MILESTRO_GAME_RETCODE_H
#define MILESTRO_GAME_RETCODE_H

#include <cstdint>

constexpr int64_t MILESTRO_API_RET_OK = 0;
constexpr int64_t MILESTRO_API_RET_UNEXPECTED_EXCEPTION = -11709394;
constexpr int64_t MILESTRO_API_RET_FAILED = -1;

// Structured statuses keep the historical sign convention: negative values
// are failures, while non-negative values are successful results or warnings.
// The high bits carry lifecycle information so cleanup callers can decide
// whether an object is safe to recycle without parsing an opaque error code.
constexpr uint64_t MILESTRO_API_STATUS_SEVERITY_SHIFT = 62;
constexpr uint64_t MILESTRO_API_STATUS_FLAGS_SHIFT = 59;
constexpr uint64_t MILESTRO_API_STATUS_MAGIC_SHIFT = 51;
constexpr uint64_t MILESTRO_API_STATUS_DOMAIN_SHIFT = 43;
constexpr uint64_t MILESTRO_API_STATUS_OPERATION_SHIFT = 32;

constexpr uint64_t MILESTRO_API_STATUS_SEVERITY_MASK = 0x3ULL;
constexpr uint64_t MILESTRO_API_STATUS_FLAGS_MASK = 0x7ULL;
constexpr uint64_t MILESTRO_API_STATUS_MAGIC_MASK = 0xFFULL;
constexpr uint64_t MILESTRO_API_STATUS_DOMAIN_MASK = 0xFFULL;
constexpr uint64_t MILESTRO_API_STATUS_OPERATION_MASK = 0x7FFULL;
constexpr uint64_t MILESTRO_API_STATUS_DETAIL_MASK = 0xFFFFFFFFULL;

constexpr uint64_t MILESTRO_API_STATUS_MAGIC = 0x5AULL;

enum : uint64_t {
    MILESTRO_API_STATUS_SEVERITY_SUCCESS = 0,
    MILESTRO_API_STATUS_SEVERITY_WARNING = 1,
    MILESTRO_API_STATUS_SEVERITY_FAILURE = 2,
    MILESTRO_API_STATUS_SEVERITY_FATAL = 3,
};

enum : uint64_t {
    MILESTRO_API_STATUS_FLAG_DEFERRED = 1,
    MILESTRO_API_STATUS_FLAG_RETRYABLE = 2,
    MILESTRO_API_STATUS_FLAG_CONSUMED = 4,
};

constexpr uint64_t MILESTRO_API_STATUS_DOMAIN_NATIVE = 1;
constexpr uint64_t MILESTRO_API_STATUS_DOMAIN_SKIA = MILESTRO_API_STATUS_DOMAIN_NATIVE;
constexpr uint64_t MILESTRO_API_STATUS_OPERATION_DESTROY = 1;
constexpr uint64_t MILESTRO_API_STATUS_OPERATION_ENQUEUE = 2;
constexpr uint64_t MILESTRO_API_STATUS_DETAIL_UNEXPECTED_EXCEPTION = 1;
constexpr uint64_t MILESTRO_API_STATUS_DETAIL_QUEUE_FULL = 2;

constexpr int64_t MILESTRO_API_STATUS_MAKE(uint64_t severity,
                                            uint64_t flags,
                                            uint64_t domain,
                                            uint64_t operation,
                                            uint64_t detail) {
    const uint64_t raw = ((severity & MILESTRO_API_STATUS_SEVERITY_MASK)
                          << MILESTRO_API_STATUS_SEVERITY_SHIFT) |
                         ((flags & MILESTRO_API_STATUS_FLAGS_MASK)
                          << MILESTRO_API_STATUS_FLAGS_SHIFT) |
                         ((MILESTRO_API_STATUS_MAGIC & MILESTRO_API_STATUS_MAGIC_MASK)
                          << MILESTRO_API_STATUS_MAGIC_SHIFT) |
                         ((domain & MILESTRO_API_STATUS_DOMAIN_MASK)
                          << MILESTRO_API_STATUS_DOMAIN_SHIFT) |
                         ((operation & MILESTRO_API_STATUS_OPERATION_MASK)
                          << MILESTRO_API_STATUS_OPERATION_SHIFT) |
                         (detail & MILESTRO_API_STATUS_DETAIL_MASK);
    return static_cast<int64_t>(raw);
}


constexpr int64_t MILESTRO_API_RET_BEATMAP_FAILED = MILESTRO_API_RET_FAILED;
constexpr int64_t MILESTRO_API_RET_BEATMAP_UNEXPECTED = MILESTRO_API_RET_UNEXPECTED_EXCEPTION;

constexpr int64_t MILESTRO_API_RET_COMPRESSING_FAILED = MILESTRO_API_RET_FAILED;
constexpr int64_t MILESTRO_API_RET_COMPRESSING_UNEXPECTED = MILESTRO_API_RET_UNEXPECTED_EXCEPTION;

constexpr int64_t MILESTRO_API_RET_FS_FAILED = MILESTRO_API_RET_FAILED;

constexpr int64_t MILESTRO_API_RET_IMAGE_UNPACK_ERROR = -1;
constexpr int64_t MILESTRO_API_RET_IMAGE_DECODE_ERROR = -2;

constexpr int64_t MILESTRO_API_RET_AUDIO_FAILED = -1;

constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_PREVIEW_RANGE_ERROR = -1; // 预览时间段设定错误
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_ENCODER_ERROR = -2;       // OPUS编码器报错
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_DECODER_ERROR = -3;       // OPUS解码器报错
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_CRC_ERROR = -4;           // CRC校验错误
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_OPUS_ENCODE_ERROR = -5;   // OPUS编码错误
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_PAD_SN_ERROR = -6;        // 填充长度错误
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_OFFSET_TOO_BIG = -7;      // 编码偏移量过大无法还原
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_TOOL_ERROR = -8;          // 外部工具错误
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_PARA_ERR = -9;            // 传的参数有问题
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_INVALID_HANDLE = -10;     // 传入的HANDLE错误

constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_PLAYER_IRRFILTER_COEFFICIENTS_ARGS_COUNT = -11;
constexpr int64_t MILESTRO_API_RET_AUDIO_MUA_PLAYER_AUDIO_SOURCE_NOT_READY = -12;

constexpr int64_t MILESTRO_API_RET_WINDOW_FAILED = -1;

#endif //MILESTRO_MILESTRO_GAME_RETCODE_H

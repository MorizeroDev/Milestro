cmake_minimum_required(VERSION 3.25)

if (NOT TARGET skia OR NOT TARGET skia_shaper OR NOT TARGET icu)
    message(FATAL_ERROR
            "skmodules requires the skia, skia_shaper, and icu targets; "
            "add ext/icu-cmake and ext/skia-cmake first."
    )
endif ()

set(MILESTRO_SKMODULES_ROOT "${CMAKE_CURRENT_LIST_DIR}")
set(MILESTRO_SKMODULES_METADATA "${MILESTRO_SKMODULES_ROOT}/vendor-meta")
set(MILESTRO_SKMODULES_SKIA_SOURCE "${MILESTRO_SKMODULES_ROOT}/../skia")

function(milestro_skmodules_read_metadata key out_var)
    file(STRINGS "${MILESTRO_SKMODULES_METADATA}" MILESTRO_SKMODULES_METADATA_LINE
            REGEX "^${key}: [0-9a-f]+$"
    )
    list(LENGTH MILESTRO_SKMODULES_METADATA_LINE MILESTRO_SKMODULES_METADATA_LINE_COUNT)
    if (NOT MILESTRO_SKMODULES_METADATA_LINE_COUNT EQUAL 1)
        message(FATAL_ERROR "Missing or invalid '${key}' in ${MILESTRO_SKMODULES_METADATA}")
    endif ()
    string(REGEX REPLACE "^${key}: " "" MILESTRO_SKMODULES_METADATA_VALUE
            "${MILESTRO_SKMODULES_METADATA_LINE}"
    )
    set(${out_var} "${MILESTRO_SKMODULES_METADATA_VALUE}" PARENT_SCOPE)
endfunction()

milestro_skmodules_read_metadata("skia-base" MILESTRO_SKMODULES_SKIA_BASE)
milestro_skmodules_read_metadata("source-commit" MILESTRO_SKMODULES_SOURCE_COMMIT)

find_package(Git REQUIRED)
execute_process(
        COMMAND "${GIT_EXECUTABLE}" -C "${MILESTRO_SKMODULES_SKIA_SOURCE}" rev-parse HEAD
        RESULT_VARIABLE MILESTRO_SKMODULES_SKIA_REVISION_RESULT
        OUTPUT_VARIABLE MILESTRO_SKMODULES_SKIA_REVISION
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
)
if (NOT MILESTRO_SKMODULES_SKIA_REVISION_RESULT EQUAL 0)
    message(FATAL_ERROR "Failed to read the Skia revision from ${MILESTRO_SKMODULES_SKIA_SOURCE}")
endif ()
if (NOT MILESTRO_SKMODULES_SKIA_REVISION STREQUAL MILESTRO_SKMODULES_SKIA_BASE
        AND NOT MILESTRO_SKMODULES_SKIA_REVISION STREQUAL MILESTRO_SKMODULES_SOURCE_COMMIT)
    message(FATAL_ERROR
            "Skia revision ${MILESTRO_SKMODULES_SKIA_REVISION} is incompatible with skmodules. "
            "Expected base ${MILESTRO_SKMODULES_SKIA_BASE} or source ${MILESTRO_SKMODULES_SOURCE_COMMIT}."
    )
endif ()

set(MILESTRO_SKUNICODE_SOURCES
        modules/skunicode/src/SkUnicode.cpp
        modules/skunicode/src/SkUnicode_icu.cpp
        modules/skunicode/src/SkUnicode_icu_bidi.cpp
        modules/skunicode/src/SkBidiFactory_icu_full.cpp
        modules/skunicode/src/SkUnicode_icu_builtin.cpp
)

add_library(milestro_skunicode STATIC ${MILESTRO_SKUNICODE_SOURCES})
add_library(Milestro::SkUnicode ALIAS milestro_skunicode)
target_compile_features(milestro_skunicode PUBLIC cxx_std_20)
target_compile_definitions(milestro_skunicode
        PUBLIC
        SK_UNICODE_AVAILABLE
        SK_UNICODE_ICU_IMPLEMENTATION
        PRIVATE
        SKUNICODE_IMPLEMENTATION=1
        U_USING_ICU_NAMESPACE=0
        U_DISABLE_RENAMING=1
)
target_include_directories(milestro_skunicode PRIVATE "${MILESTRO_SKMODULES_ROOT}")
target_link_libraries(milestro_skunicode
        PUBLIC skia icu
)

set(MILESTRO_SKPARAGRAPH_SOURCES
        modules/skparagraph/src/Decorations.cpp
        modules/skparagraph/src/FontArguments.cpp
        modules/skparagraph/src/FontCollection.cpp
        modules/skparagraph/src/OneLineShaper.cpp
        modules/skparagraph/src/ParagraphBuilderImpl.cpp
        modules/skparagraph/src/ParagraphCache.cpp
        modules/skparagraph/src/ParagraphImpl.cpp
        modules/skparagraph/src/ParagraphPainterImpl.cpp
        modules/skparagraph/src/ParagraphStyle.cpp
        modules/skparagraph/src/Run.cpp
        modules/skparagraph/src/TextLine.cpp
        modules/skparagraph/src/TextShadow.cpp
        modules/skparagraph/src/TextStyle.cpp
        modules/skparagraph/src/TextWrapper.cpp
        modules/skparagraph/src/TypefaceFontProvider.cpp
)

add_library(milestro_skparagraph STATIC ${MILESTRO_SKPARAGRAPH_SOURCES})
add_library(Milestro::SkParagraph ALIAS milestro_skparagraph)
target_compile_features(milestro_skparagraph PUBLIC cxx_std_20)
target_compile_definitions(milestro_skparagraph PUBLIC SK_ENABLE_PARAGRAPH)
target_include_directories(milestro_skparagraph BEFORE PUBLIC "${MILESTRO_SKMODULES_ROOT}")
target_link_libraries(milestro_skparagraph
        PRIVATE skia_shaper
        PUBLIC Milestro::SkUnicode skia
)

message(STATUS
        "Skia modules: base ${MILESTRO_SKMODULES_SKIA_BASE}, "
        "source ${MILESTRO_SKMODULES_SOURCE_COMMIT}"
)

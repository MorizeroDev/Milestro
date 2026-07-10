set(MILESTRO_TEST_NAME "MilestroTest_SkiaUnicodeFallback")
set(MILESTRO_TEST_SOURCE
        src/test_font_collection.cpp
        src/test_paragraph_style.cpp
        src/test_unicode_fallback.cpp
)

add_executable(${MILESTRO_TEST_NAME} ${MILESTRO_TEST_SOURCE})
target_link_libraries(${MILESTRO_TEST_NAME} PRIVATE
        GTest::gtest GTest::gtest_main GTest::gmock GTest::gmock_main
        Milestro
        ${PROJECT_PLATFORM_LIBRARIES}
)
target_compile_definitions(${MILESTRO_TEST_NAME} PRIVATE
        MILESTRO_TEST_ICUDTL_PATH="${PROJECT_SOURCE_DIR}/ext/icu-cmake/common/icudtl.dat"
        MILESTRO_TEST_FONT_PATH="${PROJECT_SOURCE_DIR}/tests/data/font/SourceHanSans-VF.otf.woff2.bytes"
        MILESTRO_TEST_EMOJI_FONT_PATH="${PROJECT_SOURCE_DIR}/tests/data/font/NotoColorEmoji.ttf.bytes"
)

if (NOT ANDROID)
    set(MILESTRO_TEST_NO_ICU_WORKDIR "${CMAKE_CURRENT_BINARY_DIR}/no-icu-workdir")
    file(MAKE_DIRECTORY "${MILESTRO_TEST_NO_ICU_WORKDIR}")

    add_test(
            NAME ${MILESTRO_TEST_NAME}
            WORKING_DIRECTORY "${MILESTRO_TEST_NO_ICU_WORKDIR}"
            COMMAND ${MILESTRO_TEST_NAME}
    )
    set_tests_properties(${MILESTRO_TEST_NAME} PROPERTIES ENVIRONMENT "LD_LIBRARY_PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
    set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "DYLD_LIBRARY_PATH=${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")
endif ()

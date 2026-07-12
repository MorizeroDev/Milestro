set(MILESTRO_TEST_NAME MilestroImeTest)

add_executable(${MILESTRO_TEST_NAME}
        src/test_ime_session_windows.cpp
        ${PROJECT_SOURCE_DIR}/src/ime/ImeSession_windows.cpp)
target_include_directories(${MILESTRO_TEST_NAME}
        PRIVATE
        ${PROJECT_SOURCE_DIR}/include
        ${PROJECT_SOURCE_DIR}/src)
target_link_libraries(${MILESTRO_TEST_NAME}
        PRIVATE
        GTest::gtest
        GTest::gtest_main
        imm32
)

add_test(NAME ${MILESTRO_TEST_NAME} COMMAND ${MILESTRO_TEST_NAME})

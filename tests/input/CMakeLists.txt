set(MILESTRO_TEST_NAME MilestroScrollPhaseTest)

add_executable(${MILESTRO_TEST_NAME}
        src/test_scroll_phase.cpp
        ${PROJECT_SOURCE_DIR}/src/input/ScrollPhaseGestureTracker.cpp
        ${PROJECT_SOURCE_DIR}/src/input/ScrollPhaseMinimalGestureTracker.cpp
        ${PROJECT_SOURCE_DIR}/src/input/ScrollPhaseLease.cpp
        ${PROJECT_SOURCE_DIR}/src/input/ScrollPhaseUnloadPolicy.cpp)
target_include_directories(${MILESTRO_TEST_NAME}
        PRIVATE
        ${PROJECT_SOURCE_DIR}/include
        ${PROJECT_SOURCE_DIR}/src/input)
target_link_libraries(${MILESTRO_TEST_NAME}
        PRIVATE
        GTest::gtest
        GTest::gtest_main)

add_test(NAME ${MILESTRO_TEST_NAME} COMMAND ${MILESTRO_TEST_NAME})

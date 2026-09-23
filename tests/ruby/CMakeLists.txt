add_executable(MilestroTest_Ruby src/test_ruby.cpp)
target_link_libraries(MilestroTest_Ruby PRIVATE
        GTest::gtest GTest::gtest_main Milestro::SkParagraph ${PROJECT_PLATFORM_LIBRARIES})
target_compile_definitions(MilestroTest_Ruby PRIVATE RUBY_TEST_ROOT="${PROJECT_SOURCE_DIR}")
if (MSVC)
    target_compile_options(MilestroTest_Ruby PRIVATE /utf-8)
endif ()
if (NOT ANDROID)
    file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/results")
    add_test(NAME MilestroTest_Ruby COMMAND MilestroTest_Ruby)
    set_tests_properties(MilestroTest_Ruby PROPERTIES
            WORKING_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/results")
endif ()

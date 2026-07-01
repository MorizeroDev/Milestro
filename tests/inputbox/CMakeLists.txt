set(MILESTRO_TEST_NAME "MilestroTest_InputBoxApiSpike")
set(MILESTRO_TEST_SOURCE
        src/test_inputbox_api_spike.cpp
)

add_executable(${MILESTRO_TEST_NAME} ${MILESTRO_TEST_SOURCE})
target_link_libraries(${MILESTRO_TEST_NAME} PRIVATE
        GTest::gtest GTest::gtest_main GTest::gmock GTest::gmock_main
        Milestro
)

if (ANDROID)
    # Android execution is still handled manually for Milestro native tests.
else ()
    add_test(
            NAME ${MILESTRO_TEST_NAME}
            WORKING_DIRECTORY ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}
            COMMAND ${MILESTRO_TEST_NAME}
    )
    set_tests_properties(${MILESTRO_TEST_NAME} PROPERTIES ENVIRONMENT "LD_LIBRARY_PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
    set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "MILESTRO_TEST_DATA_PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/data")
    add_custom_command(
            TARGET ${MILESTRO_TEST_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_CURRENT_SOURCE_DIR}/../data" "${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/data"
    )
endif ()

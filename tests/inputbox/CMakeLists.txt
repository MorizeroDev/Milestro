function(add_milestro_inputbox_test MILESTRO_TEST_NAME MILESTRO_TEST_SOURCE)
    add_executable(${MILESTRO_TEST_NAME} ${MILESTRO_TEST_SOURCE})
    target_compile_definitions(${MILESTRO_TEST_NAME} PRIVATE
            MILESTRO_TEST_ICUDTL_PATH="${PROJECT_SOURCE_DIR}/ext/icu-cmake/common/icudtl.dat"
    )
    target_link_libraries(${MILESTRO_TEST_NAME} PRIVATE
            GTest::gtest GTest::gtest_main GTest::gmock GTest::gmock_main
            Milestro
            ${PROJECT_PLATFORM_LIBRARIES}
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
        set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "DYLD_LIBRARY_PATH=${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")
        set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "MILESTRO_TEST_DATA_PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/data")
        set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "MILESTRO_TEST_ICUDTL_PATH=${PROJECT_SOURCE_DIR}/ext/icu-cmake/common/icudtl.dat")
        add_custom_command(
                TARGET ${MILESTRO_TEST_NAME} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_CURRENT_SOURCE_DIR}/../data" "${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/data"
        )
    endif ()
endfunction()

add_milestro_inputbox_test("MilestroTest_InputBoxApiSpike" src/test_inputbox_api_spike.cpp)
add_milestro_inputbox_test("MilestroTest_InputBox" src/test_inputbox.cpp)

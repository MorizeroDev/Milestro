set(MILESTRO_TEST_NAME "MilestroTest_ReadFont")
set(MILESTRO_TEST_SOURCE
        src/test_readfont.cpp

)

add_executable(${MILESTRO_TEST_NAME} ${MILESTRO_TEST_SOURCE})
target_link_libraries(${MILESTRO_TEST_NAME} PRIVATE
        GTest::gtest GTest::gtest_main GTest::gmock GTest::gmock_main
        Milestro
)

if (ANDROID)
    #    add_test(
    #            NAME ${MILESTRO_TEST_NAME}
    #            WORKING_DIRECTORY ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}
    #            COMMAND ${CMAKE_COMMAND} -DANDROID_NDK=${ANDROID_NDK} "-DTEST_FILES=${CMAKE_CURRENT_SOURCE_DIR}/data;${CMAKE_BINARY_DIR}/bin/TestApp;${CMAKE_BINARY_DIR}/bin/TestLibrary.so" -DLIBRARY_DIR=${CMAKE_BINARY_DIR}/lib -DUNITTEST=${CMAKE_BINARY_DIR}/bin/Foundation-testrunner -DTEST_PARAMETER=-all -P ${CMAKE_SOURCE_DIR}/cmake/ExecuteOnAndroid.cmake
    #    )
else ()
    add_test(
            NAME ${MILESTRO_TEST_NAME}
            WORKING_DIRECTORY ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}
            COMMAND ${MILESTRO_TEST_NAME}
    )
    set_tests_properties(${MILESTRO_TEST_NAME} PROPERTIES ENVIRONMENT "LD_LIBRARY_PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
    #    set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}:$ENV{PATH}")
    set_property(TEST ${MILESTRO_TEST_NAME} APPEND PROPERTY ENVIRONMENT "MILESTRO_TEST_DATA_PATH=${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/data")
    # The test is run in the runtime directory. So the test data is copied there too
    add_custom_command(
            TARGET ${MILESTRO_TEST_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_CURRENT_SOURCE_DIR}/../data" "${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/data"
    )
endif ()

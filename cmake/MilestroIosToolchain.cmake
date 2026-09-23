set(MILESTRO_IOS_DEPLOYMENT_TARGET "15.0")

set(DEPLOYMENT_TARGET
        "${MILESTRO_IOS_DEPLOYMENT_TARGET}"
        CACHE STRING
        "Minimum iOS deployment target."
        FORCE
)
set(CMAKE_OSX_DEPLOYMENT_TARGET
        "${MILESTRO_IOS_DEPLOYMENT_TARGET}"
        CACHE STRING
        "Minimum iOS deployment target."
        FORCE
)

include("${CMAKE_CURRENT_LIST_DIR}/../ext/ios-cmake/ios.toolchain.cmake")

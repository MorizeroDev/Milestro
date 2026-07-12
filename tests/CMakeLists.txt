add_subdirectory(readfont)
add_subdirectory(image)
add_subdirectory(icu)
add_subdirectory(skia)
add_subdirectory(inputbox)

if (WIN32)
    add_subdirectory(ime)
endif ()

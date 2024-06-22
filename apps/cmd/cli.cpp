#include "CLI/App.hpp"
#include "Milestro/skia/textlayout/TextStyle.h"

int main(int argc, char **argv) {
    CLI::App app{"Milestro CLI"};
    app.fallthrough(false);
    app.set_version_flag("-v,--version", std::string(MILESTRO_VERSION));

    if (argc == 1) {
        std::cout << app.help() << std::endl;
        return 0;
    }

    // windows默认的控制台非utf8，cli11的main分支提供了转utf8的功能，但是不在发布版本中
    // 如有需要，可关注https://github.com/CLIUtils/CLI11/pull/974
    argv = app.ensure_utf8(argv);
    CLI11_PARSE(app, argc, argv);
    return 0;
}

#!/usr/bin/env python3
import argparse
import re
import subprocess


class GlibcCompatibilityError(ValueError):
    pass


def check_glibc(version_info, maximum):
    requirements = {
        match.group(1)
        for match in re.finditer(r"\bName: GLIBC_(\S+)", version_info)
    }
    if not requirements:
        raise ValueError("No GLIBC version requirements found in ELF version information")
    maximum_version = tuple(int(part) for part in maximum.split("."))
    for requirement in requirements:
        if not re.fullmatch(r"\d+(?:\.\d+)+", requirement):
            raise GlibcCompatibilityError(f"Unsupported GLIBC requirement: {requirement}")
    required = max(requirements, key=lambda value: tuple(int(part) for part in value.split(".")))
    if tuple(int(part) for part in required.split(".")) > maximum_version:
        raise GlibcCompatibilityError(f"Requires GLIBC_{required}; maximum supported is GLIBC_{maximum}")
    return required


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary")
    parser.add_argument("--max-glibc", required=True)
    args = parser.parse_args()
    result = subprocess.run(
        ["readelf", "--version-info", "--wide", args.binary],
        check=True,
        text=True,
        stdout=subprocess.PIPE,
    )
    try:
        required = check_glibc(result.stdout, args.max_glibc)
    except GlibcCompatibilityError as error:
        print(f"::error::{error}")
        return
    print(f"{args.binary}: requires GLIBC_{required}, within GLIBC_{args.max_glibc} limit")


if __name__ == "__main__":
    main()

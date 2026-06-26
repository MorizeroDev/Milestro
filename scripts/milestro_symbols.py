#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path


FORBIDDEN_RUNTIME_PATHS = [
    "/home/runner",
    "/Users/runner",
    "D:\\a\\",
    "D:/a/",
]

ELF_DEBUG_SECTIONS = [
    ".debug_addr",
    ".debug_info",
    ".debug_line",
    ".debug_line_str",
    ".debug_loc",
    ".debug_loclists",
    ".debug_abbrev",
    ".debug_ranges",
    ".debug_rnglists",
    ".debug_str",
    ".debug_str_offsets",
]

PE_DEBUG_SECTIONS = [
    ".debug_addr",
    ".debug_info",
    ".debug_line",
    ".debug_line_str",
    ".debug_loc",
    ".debug_loclists",
    ".debug_abbrev",
    ".debug_ranges",
    ".debug_rnglists",
    ".debug_str",
    ".debug_str_offsets",
]


def run(args, *, check=True):
    print("+ " + " ".join(str(arg) for arg in args))
    result = subprocess.run(args, check=False, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if check and result.returncode != 0:
        if result.stdout:
            print(result.stdout, file=sys.stderr)
        raise subprocess.CalledProcessError(result.returncode, args, output=result.stdout)
    return result


def find_tool(*names, env_var=None):
    if env_var:
        configured = os.environ.get(env_var)
        if configured:
            path = Path(configured)
            if path.exists() and path.is_file():
                return str(path)
            raise RuntimeError(f"{env_var} is set but does not point to a file: {configured}")
    for name in names:
        found = shutil.which(name)
        if found:
            return found
    raise RuntimeError(f"none of these tools were found: {', '.join(names)}")


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_tree(path):
    if path.is_file():
        return sha256_file(path)

    digest = hashlib.sha256()
    for child in sorted(p for p in path.rglob("*") if p.is_file()):
        digest.update(child.relative_to(path).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(sha256_file(child).encode("ascii"))
        digest.update(b"\0")
    return digest.hexdigest()


def fail(message):
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


def normalize_forbidden_patterns(patterns):
    values = set(FORBIDDEN_RUNTIME_PATHS)
    for pattern in patterns:
        if not pattern:
            continue
        values.add(pattern)
        values.add(pattern.replace("\\", "/"))
        values.add(pattern.replace("/", "\\"))
    return sorted(values)


def verify_no_forbidden_paths(binary, patterns):
    data = binary.read_bytes()
    offenders = []
    for pattern in normalize_forbidden_patterns(patterns):
        encoded_utf8 = pattern.encode("utf-8", errors="ignore")
        encoded_utf16 = pattern.encode("utf-16-le", errors="ignore")
        if encoded_utf8 and encoded_utf8 in data:
            offenders.append(f"{pattern} (utf-8)")
        if encoded_utf16 and encoded_utf16 in data:
            offenders.append(f"{pattern} (utf-16-le)")
    if offenders:
        fail(f"{binary} still contains forbidden path fragments: {', '.join(offenders)}")


def readobj_output(binary, *args):
    readobj = find_tool("llvm-readobj", env_var="MILESTRO_READOBJ")
    return run([readobj, *args, str(binary)]).stdout


def readelf_output(binary, *args):
    readelf = find_tool("llvm-readelf", "readelf", env_var="MILESTRO_READELF")
    return run([readelf, *args, str(binary)]).stdout


def elf_build_id(binary):
    output = readelf_output(binary, "-n")
    match = re.search(r"Build ID:\s*([0-9a-fA-F]+)", output)
    if not match:
        fail(f"could not read ELF Build ID from {binary}")
    return match.group(1).lower()


def verify_debug_symbol(path, output, sections, kind):
    present = [section for section in sections if section in output]
    if not present:
        fail(f"{path} does not contain {kind} debug sections")


def verify_elf_runtime(binary):
    output = readelf_output(binary, "-S")
    present = [section for section in ELF_DEBUG_SECTIONS if section in output]
    if present:
        fail(f"{binary} still contains debug sections after strip: {', '.join(present)}")
    if ".gnu_debuglink" not in output:
        fail(f"{binary} does not contain .gnu_debuglink")


def split_elf(binary, symbol_dir, symbol_name):
    objcopy = find_tool("llvm-objcopy", "objcopy", env_var="MILESTRO_OBJCOPY")
    symbol_path = symbol_dir / symbol_name
    symbol_dir.mkdir(parents=True, exist_ok=True)

    run([objcopy, "--only-keep-debug", str(binary), str(symbol_path)])
    verify_debug_symbol(symbol_path, readelf_output(symbol_path, "-S"), ELF_DEBUG_SECTIONS, "ELF")
    run([objcopy, "--strip-debug", "--strip-unneeded", str(binary)])
    run([objcopy, f"--add-gnu-debuglink={symbol_path}", str(binary)])
    verify_elf_runtime(binary)
    return symbol_path


def readobj_field(output, field, binary):
    match = re.search(rf"^\s*{re.escape(field)}:\s*(.+)$", output, re.MULTILINE)
    if not match:
        fail(f"could not read PE {field} from {binary}")
    return match.group(1).strip()


def normalize_numeric_field(line, field, binary):
    hex_tokens = re.findall(r"0x[0-9A-Fa-f]+", line)
    if hex_tokens:
        return hex(int(hex_tokens[-1], 16))

    decimal_tokens = re.findall(r"\b[0-9]+\b", line)
    if decimal_tokens:
        return hex(int(decimal_tokens[-1], 10))

    fail(f"could not parse PE {field} value from {binary}: {line}")


def pe_debug_id(binary):
    output = readobj_output(binary, "--file-headers")
    timestamp = readobj_field(output, "TimeDateStamp", binary)
    image_size = readobj_field(output, "SizeOfImage", binary)
    machine = readobj_field(output, "Machine", binary)
    return {
        "time_date_stamp": normalize_numeric_field(timestamp, "TimeDateStamp", binary),
        "size_of_image": normalize_numeric_field(image_size, "SizeOfImage", binary),
        "machine": machine,
    }


def verify_pe_runtime(binary):
    output = readobj_output(binary, "--sections")
    present = [section for section in PE_DEBUG_SECTIONS if section in output]
    if present:
        fail(f"{binary} still contains debug sections after strip: {', '.join(present)}")
    if ".gnu_debuglink" not in output:
        fail(f"{binary} does not contain .gnu_debuglink")


def split_pe_coff(binary, symbol_dir, symbol_name):
    objcopy = find_tool("llvm-objcopy", env_var="MILESTRO_OBJCOPY")
    symbol_path = symbol_dir / symbol_name
    symbol_dir.mkdir(parents=True, exist_ok=True)

    run([objcopy, "--only-keep-debug", str(binary), str(symbol_path)])
    verify_debug_symbol(symbol_path, readobj_output(symbol_path, "--sections"), PE_DEBUG_SECTIONS, "PE/COFF")
    run([objcopy, "--strip-debug", "--strip-unneeded", str(binary)])
    run([objcopy, f"--add-gnu-debuglink={symbol_path}", str(binary)])
    verify_pe_runtime(binary)
    return symbol_path


def apple_uuid(path):
    dwarfdump = find_tool("dwarfdump")
    output = run([dwarfdump, "--uuid", str(path)]).stdout
    return sorted(set(uuid.lower() for uuid in re.findall(r"UUID:\s*([0-9A-Fa-f-]+)", output)))


def verify_apple_uuid_match(binary, dsym):
    binary_uuids = apple_uuid(binary)
    dsym_uuids = apple_uuid(dsym)
    if not binary_uuids:
        fail(f"could not read Mach-O UUID from {binary}")
    if not dsym_uuids:
        fail(f"could not read dSYM UUID from {dsym}")
    if binary_uuids != dsym_uuids:
        fail(f"dSYM UUID mismatch for {binary}: binary={binary_uuids}, dsym={dsym_uuids}")
    return binary_uuids, dsym_uuids


def split_apple(binary, symbol_dir, symbol_name):
    dsymutil = find_tool("dsymutil")
    strip = find_tool("strip")
    symbol_path = symbol_dir / symbol_name
    symbol_dir.mkdir(parents=True, exist_ok=True)

    run([dsymutil, "-o", str(symbol_path), str(binary)])
    run([strip, "-x", "-S", str(binary)])
    return symbol_path


def append_manifest(manifest_path, entry):
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    if manifest_path.exists():
        data = json.loads(manifest_path.read_text(encoding="utf-8"))
    else:
        data = {"schema": 1, "entries": []}
    data["entries"].append(entry)
    manifest_path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def relative_to_manifest(manifest_path, path):
    try:
        return path.relative_to(manifest_path.parent).as_posix()
    except ValueError:
        return path.as_posix()


def prepare(args):
    binary = Path(args.binary).resolve()
    if not binary.exists():
        fail(f"binary was not found: {binary}")

    symbol_dir = (Path(args.symbols_root) / args.platform / f"{args.arch}-{args.variant}").resolve()
    manifest_path = Path(args.manifest).resolve()
    forbidden_patterns = list(args.forbid)
    if args.source_root:
        forbidden_patterns.append(str(Path(args.source_root).resolve()))

    debug_id = {}
    if args.kind == "elf":
        symbol_name = args.symbol_name or f"{binary.name}.debug"
        symbol_path = split_elf(binary, symbol_dir, symbol_name)
        debug_id["build_id"] = elf_build_id(binary)
    elif args.kind == "pe-coff":
        symbol_name = args.symbol_name or f"{binary.name}.debug"
        symbol_path = split_pe_coff(binary, symbol_dir, symbol_name)
        debug_id["pe"] = pe_debug_id(binary)
    elif args.kind == "apple":
        symbol_name = args.symbol_name or f"{binary.name}.dSYM"
        symbol_path = split_apple(binary, symbol_dir, symbol_name)
        uuid, dsym_uuid = verify_apple_uuid_match(binary, symbol_path)
        debug_id["uuid"] = uuid
        debug_id["dsym_uuid"] = dsym_uuid
    else:
        fail(f"unsupported kind: {args.kind}")

    verify_no_forbidden_paths(binary, forbidden_patterns)

    entry = {
        "repo": args.repo,
        "commit": args.commit,
        "tag": args.tag,
        "platform": args.platform,
        "arch": args.arch,
        "variant": args.variant,
        "binary": args.binary.replace("\\", "/"),
        "binary_sha256": sha256_file(binary),
        "symbol": relative_to_manifest(manifest_path, symbol_path),
        "symbol_sha256": sha256_tree(symbol_path),
        "debug_id": debug_id,
    }
    append_manifest(manifest_path, entry)


def main():
    parser = argparse.ArgumentParser(description="Split Milestro release symbols and write symbol manifests.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    prepare_parser = subparsers.add_parser("prepare")
    prepare_parser.add_argument("--kind", choices=["elf", "apple", "pe-coff"], required=True)
    prepare_parser.add_argument("--repo", default="Milestro")
    prepare_parser.add_argument("--commit", required=True)
    prepare_parser.add_argument("--tag", default="")
    prepare_parser.add_argument("--platform", required=True)
    prepare_parser.add_argument("--arch", required=True)
    prepare_parser.add_argument("--variant", required=True)
    prepare_parser.add_argument("--binary", required=True)
    prepare_parser.add_argument("--symbol-name")
    prepare_parser.add_argument("--symbols-root", required=True)
    prepare_parser.add_argument("--manifest", required=True)
    prepare_parser.add_argument("--source-root", default="")
    prepare_parser.add_argument("--forbid", action="append", default=[])
    prepare_parser.set_defaults(func=prepare)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()

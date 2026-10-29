#!/usr/bin/env python3
"""Validate documentation languages and links, then compile/run marked C# examples."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = {
    "ru": ("README.ru.md", "docs"),
    "en": ("README.md", "docs/en"),
    "de": ("README.de.md", "docs/de"),
}
EXAMPLE = re.compile(
    r"^[ \t]*<!--\s*verify:\s*([a-z0-9-]+)\s*-->\s*```csharp\r?\n(.*?)^```[ \t]*$",
    re.MULTILINE | re.DOTALL,
)
MARKER = re.compile(r"^[ \t]*<!--\s*verify:\s*([a-z0-9-]+)\s*-->", re.MULTILINE)
FENCE = re.compile(r"^```[^\n]*\n.*?^```[ \t]*$", re.MULTILINE | re.DOTALL)
LINK = re.compile(r"!?\[[^\]\n]*\]\(([^\n)]+)\)")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def documentation_paths() -> list[Path]:
    errors: list[str] = []
    originals = {path.name for path in (ROOT / LANGUAGES["ru"][1]).glob("*.md")}
    paths: list[Path] = []
    for language, (readme, directory) in LANGUAGES.items():
        root_page = ROOT / readme
        if not root_page.is_file():
            errors.append(f"{language}: missing project page {readme}")
        else:
            paths.append(root_page)
        translated = {path.name for path in (ROOT / directory).glob("*.md")}
        for name in sorted(originals - translated):
            errors.append(f"{language}: missing documentation page {directory}/{name}")
        for name in sorted(translated - originals):
            errors.append(f"{language}: page has no Russian original: {directory}/{name}")
    if not originals:
        errors.append("No Russian documentation pages found.")
    if errors:
        raise ValueError("\n".join(errors))
    paths.append(ROOT / "README.en.md")  # Compatibility page for existing links.
    paths.extend(sorted((ROOT / "docs").rglob("*.md")))
    print(f"Validated documentation languages: {', '.join(LANGUAGES)}.", flush=True)
    return paths


def anchors(markdown: str) -> set[str]:
    found: set[str] = set()
    counts: dict[str, int] = {}
    for title in re.findall(r"^#{1,6}\s+(.+?)\s*#*\s*$", FENCE.sub("", markdown), re.MULTILINE):
        slug = re.sub(r"[^\w\- ]", "", title.lower()).replace(" ", "-")
        number = counts.get(slug, 0)
        counts[slug] = number + 1
        found.add(slug if number == 0 else f"{slug}-{number}")
    return found


def inspect_documents(paths: list[Path]) -> list[tuple[str, str]]:
    errors: list[str] = []
    examples: list[tuple[str, str]] = []
    seen: set[str] = set()
    links = 0
    for path in paths:
        content = read(path)
        location = path.relative_to(ROOT)
        if re.search(r"\{\{(?:example|fence):", content):
            errors.append(f"{location}: unresolved documentation placeholder")
        if len(re.findall(r"^```", content, re.MULTILINE)) % 2:
            errors.append(f"{location}: unclosed Markdown code fence")
        blocks = EXAMPLE.findall(content)
        if MARKER.findall(content) != [name for name, _ in blocks]:
            errors.append(f"{location}: verify marker must precede a complete csharp fence")
        for name, code in blocks:
            if name in seen:
                errors.append(f"{location}: duplicate example name {name}")
            seen.add(name)
            examples.append((name, code))
        for raw_target in LINK.findall(FENCE.sub("", content)):
            target = raw_target.strip().split(' "', 1)[0].strip("<>")
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc:
                continue
            destination = (path.parent / unquote(parsed.path)).resolve() if parsed.path else path
            links += 1
            if not destination.exists():
                errors.append(f"{location}: missing link target {target}")
            elif parsed.fragment and destination.suffix.lower() == ".md":
                if unquote(parsed.fragment) not in anchors(read(destination)):
                    errors.append(f"{location}: missing anchor {target}")
    if errors:
        raise ValueError("\n".join(errors))
    print(f"Validated {len(paths)} Markdown files, {links} local links, {len(examples)} C# examples.", flush=True)
    return examples


def run(command: list[str], cwd: Path, timeout: int = 180) -> None:
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True,
                            encoding="utf-8", errors="replace", timeout=timeout)
    if result.returncode:
        raise RuntimeError(f"Command failed: {' '.join(command)}\n{result.stdout}\n{result.stderr}")


def verify_examples(examples: list[tuple[str, str]]) -> None:
    if not examples:
        raise ValueError("No verifiable C# examples found.")
    with tempfile.TemporaryDirectory(prefix="trexon-docs-") as temporary:
        directory = Path(temporary)
        (directory / "global.json").write_text(json.dumps(json.loads(read(ROOT / "global.json"))), encoding="utf-8")
        solution = ET.Element("Solution")
        projects: list[tuple[str, Path, str]] = []
        for index, (name, code) in enumerate(examples, start=1):
            assembly = f"DocumentationExample{index:03d}"
            project_directory = directory / assembly
            project_directory.mkdir()
            project = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
            properties = ET.SubElement(project, "PropertyGroup")
            for key, value in {
                "OutputType": "Exe",
                "TargetFramework": "net8.0",
                "ImplicitUsings": "enable",
                "Nullable": "enable",
                "IsPackable": "false",
            }.items():
                ET.SubElement(properties, key).text = value
            references = ET.SubElement(project, "ItemGroup")
            ET.SubElement(references, "ProjectReference", {"Include": str(ROOT / "Trexon.Logging/Trexon.Logging.csproj")})
            project_path = project_directory / f"{assembly}.csproj"
            ET.ElementTree(project).write(project_path, encoding="utf-8", xml_declaration=True)
            (project_directory / "Program.cs").write_text(code, encoding="utf-8")
            ET.SubElement(solution, "Project", {"Path": f"{assembly}/{assembly}.csproj"})
            projects.append((name, project_directory, assembly))
        solution_path = directory / "Documentation.slnx"
        ET.ElementTree(solution).write(solution_path, encoding="utf-8", xml_declaration=True)
        run(["dotnet", "build", str(solution_path), "--configuration", "Release", "--nologo", "--verbosity", "quiet", "-warnaserror"], directory)
        for name, project_directory, assembly in projects:
            binary = project_directory / "bin/Release/net8.0" / f"{assembly}.dll"
            run(["dotnet", str(binary)], project_directory, timeout=30)
            print(f"Passed: {name}", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--links-only", action="store_true", help="Skip compiling and running C# examples")
    options = parser.parse_args()
    try:
        paths = documentation_paths()
        examples = inspect_documents(paths)
        if not options.links_only:
            verify_examples(examples)
    except (ValueError, RuntimeError, subprocess.TimeoutExpired, OSError) as error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

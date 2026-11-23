#!/usr/bin/env python3
"""Gate de licencias: falla si un paquete del cierre transitivo de un proyecto no tiene licencia OSI permitida.

Uso: python3 eng/license-gate.py <proyecto.csproj> [<proyecto.csproj> ...]
Requiere `dotnet restore` previo de cada proyecto (lee obj/project.assets.json).
"""

import json
import os
import re
import sys
import xml.etree.ElementTree as ET

ALLOWLIST_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "allowed-licenses.json")


def load_allowlist():
    with open(ALLOWLIST_PATH, encoding="utf-8") as f:
        data = json.load(f)
    return set(data["licenses"]), {k.lower(): v for k, v in data.get("exceptions", {}).items()}


def read_license(nuspec_path):
    """Devuelve (tipo, valor) de la licencia declarada en el .nuspec."""
    root = ET.parse(nuspec_path).getroot()
    for el in root.iter():
        tag = el.tag.rsplit("}", 1)[-1]
        if tag == "license":
            return el.attrib.get("type", ""), (el.text or "").strip()
    for el in root.iter():
        if el.tag.rsplit("}", 1)[-1] == "licenseUrl":
            return "url", (el.text or "").strip()
    return "none", ""


def expression_allowed(expression, allowed):
    """Evalúa una expresión SPDX: OR alcanza con una permitida; AND exige todas."""
    expression = expression.replace("(", " ").replace(")", " ")
    alternatives = re.split(r"\s+OR\s+", expression)
    for alternative in alternatives:
        ids = [t.strip() for t in re.split(r"\s+AND\s+", alternative) if t.strip()]
        if ids and all(i in allowed for i in ids):
            return True
    return False


def closure(assets):
    """Cierre transitivo de paquetes que viajan al consumidor (sin PrivateAssets=all)."""
    target = next(iter(assets["targets"].values()))
    resolved = {}
    for key, value in target.items():
        name, version = key.split("/", 1)
        resolved[name.lower()] = (name, version, value)

    roots = []
    for framework in assets["project"]["frameworks"].values():
        for name, dep in framework.get("dependencies", {}).items():
            if dep.get("suppressParent", "").replace(" ", "").lower() == "all":
                continue
            roots.append(name.lower())
    for key, value in target.items():
        if value.get("type") == "project":
            roots.extend(d.lower() for d in value.get("dependencies", {}))

    seen = {}
    stack = list(roots)
    while stack:
        name = stack.pop()
        if name in seen or name not in resolved:
            continue
        real, version, value = resolved[name]
        seen[name] = (real, version, value.get("type"))
        stack.extend(d.lower() for d in value.get("dependencies", {}))
    return [v for v in seen.values() if v[2] == "package"]


def check_project(csproj, allowed, exceptions):
    assets_path = os.path.join(os.path.dirname(os.path.abspath(csproj)), "obj", "project.assets.json")
    if not os.path.isfile(assets_path):
        print(f"ERROR {csproj}: falta {assets_path}; correr dotnet restore antes.")
        return 1
    with open(assets_path, encoding="utf-8") as f:
        assets = json.load(f)
    folders = list(assets.get("packageFolders", {}))
    libraries = assets["libraries"]
    failures = []
    for name, version, _ in sorted(closure(assets)):
        key = f"{name}@{version}".lower()
        lib = libraries.get(f"{name}/{version}", {})
        nuspec = None
        for folder in folders:
            candidate = os.path.join(folder, lib.get("path", f"{name.lower()}/{version.lower()}"), f"{name.lower()}.nuspec")
            if os.path.isfile(candidate):
                nuspec = candidate
                break
        if nuspec is None:
            failures.append(f"{name}@{version}: no se encontró el .nuspec")
            continue
        kind, value = read_license(nuspec)
        if kind == "expression" and expression_allowed(value, allowed):
            continue
        if key in exceptions:
            continue
        failures.append(f"{name}@{version}: licencia {kind} '{value}' fuera de la allowlist")
    if failures:
        print(f"FALLA {csproj}:")
        for line in failures:
            print(f"  - {line}")
        return 1
    print(f"OK {csproj}")
    return 0


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    allowed, exceptions = load_allowlist()
    return 1 if any([check_project(p, allowed, exceptions) for p in argv[1:]]) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

#!/usr/bin/env python3
"""Verifica los paquetes de un `dotnet pack`: uno por PackageId esperado, con símbolos, licencia, readme y repositorio.

Uso: python3 eng/verify-pack.py <carpeta-nupkg> (--all | <PackageId> [<PackageId> ...])
--all espera un paquete por cada src/<PackageId>/<PackageId>.csproj.
"""

import glob
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPOSITORY_URL = "https://github.com/ElRoso/Commons"


def all_package_ids():
    return sorted(
        os.path.basename(os.path.dirname(p))
        for p in glob.glob(os.path.join(ROOT, "src", "*", "*.csproj"))
        if os.path.basename(p) == os.path.basename(os.path.dirname(p)) + ".csproj"
    )


def csproj_version(package_id):
    path = os.path.join(ROOT, "src", package_id, f"{package_id}.csproj")
    if not os.path.isfile(path):
        return None
    for el in ET.parse(path).getroot().iter("Version"):
        return (el.text or "").strip()
    return None


def local(tag):
    return tag.rsplit("}", 1)[-1]


def read_nuspec(archive):
    """Devuelve (metadata como dict de elementos, lista de archivos) del paquete."""
    names = archive.namelist()
    nuspec = next(n for n in names if "/" not in n and n.endswith(".nuspec"))
    root = ET.fromstring(archive.read(nuspec))
    metadata = next(el for el in root if local(el.tag) == "metadata")
    return {local(el.tag): el for el in metadata}, names


def check_package(path):
    """Devuelve (id, version, errores) de un .nupkg."""
    errors = []
    with zipfile.ZipFile(path) as archive:
        meta, names = read_nuspec(archive)
    package_id = (meta["id"].text or "").strip()
    version = (meta["version"].text or "").strip()

    license_el = meta.get("license")
    if license_el is None or license_el.attrib.get("type") != "expression" or (license_el.text or "").strip() != "MIT":
        errors.append("license no es la expresión MIT")

    readme = meta.get("readme")
    if readme is None or not (readme.text or "").strip():
        errors.append("falta <readme>")
    elif (readme.text or "").strip() not in names:
        errors.append(f"el readme '{readme.text.strip()}' no está dentro del paquete")

    repo = meta.get("repository")
    if repo is None:
        errors.append("falta <repository>")
    else:
        if repo.attrib.get("type") != "git" or repo.attrib.get("url") != REPOSITORY_URL:
            errors.append(f"repository no apunta a {REPOSITORY_URL}: {repo.attrib}")
        if not re.fullmatch(r"[0-9a-f]{40}", repo.attrib.get("commit", "")):
            errors.append("repository sin commit")

    expected_version = csproj_version(package_id)
    if expected_version is not None and version != expected_version:
        errors.append(f"versión {version} distinta de la del csproj ({expected_version})")

    symbols = os.path.join(os.path.dirname(path), f"{package_id}.{version}.snupkg")
    if not os.path.isfile(symbols):
        errors.append(f"falta {os.path.basename(symbols)}")

    return package_id, version, errors


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    folder = argv[1]
    expected = all_package_ids() if argv[2:] == ["--all"] else sorted(argv[2:])

    found = {}
    failures = []
    for path in sorted(glob.glob(os.path.join(folder, "*.nupkg"))):
        package_id, version, errors = check_package(path)
        if package_id in found:
            failures.append(f"{package_id}: más de un .nupkg en la carpeta")
        found[package_id] = version
        failures.extend(f"{package_id} {version}: {e}" for e in errors)

    for missing in sorted(set(expected) - set(found)):
        failures.append(f"{missing}: no se empaquetó")
    for extra in sorted(set(found) - set(expected)):
        failures.append(f"{extra}: empaquetado pero no esperado")
    snupkgs = glob.glob(os.path.join(folder, "*.snupkg"))
    if len(snupkgs) != len(found):
        failures.append(f"{len(found)} .nupkg y {len(snupkgs)} .snupkg")

    if failures:
        print(f"FALLA {folder}:")
        for line in failures:
            print(f"  - {line}")
        return 1
    print(f"OK {folder}: {len(found)} .nupkg y {len(snupkgs)} .snupkg")
    for package_id in sorted(found):
        print(f"  {package_id} {found[package_id]}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

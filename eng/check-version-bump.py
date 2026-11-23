#!/usr/bin/env python3
"""Exige subir el <Version> de cada paquete cuyo src/<PackageId>/ cambió respecto de la base del PR.

Uso: python3 eng/check-version-bump.py <ref-base> [<ref-cabeza>]
Un cambio en src/Directory.Build.props exige subir todos los paquetes. En Directory.Packages.props, un
PackageVersion agregado, borrado o con otra versión exige subir los paquetes que lo referencian, directo o
por ProjectReference. Si ningún csproj del repo lo referencia (un pin), exige subir todos, igual que
cualquier otro cambio en ese archivo.
Un paquete que no existe en la base no necesita bump. Corre sobre el repo git del directorio actual.
"""

import re
import subprocess
import sys
import xml.etree.ElementTree as ET

BUILD_PROPS = "src/Directory.Build.props"
PACKAGES_PROPS = "Directory.Packages.props"


def git(*args):
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def file_at(ref, path):
    """Devuelve el contenido del archivo en ese ref, o None si no existe."""
    result = subprocess.run(["git", "show", f"{ref}:{path}"], capture_output=True, text=True)
    return result.stdout if result.returncode == 0 else None


def package_ids(ref):
    paths = git("ls-tree", "-r", "--name-only", ref, "--", "src").splitlines()
    return sorted(
        parts[1]
        for parts in (p.split("/") for p in paths)
        if len(parts) == 3 and parts[2] == f"{parts[1]}.csproj"
    )


def csproj_version(ref, package_id):
    content = file_at(ref, f"src/{package_id}/{package_id}.csproj")
    if content is None:
        return None
    for el in ET.fromstring(content).iter("Version"):
        return (el.text or "").strip()
    return None


def parse_xml(content):
    return ET.fromstring(content) if content is not None else ET.Element("Project")


def split_packages_props(content):
    """Devuelve ({PackageVersion: versión}, resto del archivo serializado sin los PackageVersion)."""
    root = parse_xml(content)
    versions = {}
    for group in root.iter("ItemGroup"):
        for item in [el for el in group if el.tag == "PackageVersion"]:
            versions[item.get("Include")] = item.get("Version")
            group.remove(item)
    for el in root.iter():
        el.text = (el.text or "").strip()
        el.tail = (el.tail or "").strip()
    return versions, ET.tostring(root)


def changed_package_versions(base, head):
    """Devuelve los PackageVersion que cambiaron, o None si cambió algo fuera de ellos."""
    old, old_rest = split_packages_props(file_at(base, PACKAGES_PROPS))
    new, new_rest = split_packages_props(file_at(head, PACKAGES_PROPS))
    if old_rest != new_rest:
        return None
    return {name for name in old.keys() | new.keys() if old.get(name) != new.get(name)}


def package_references(root):
    return {el.get("Include") for el in root.iter("PackageReference")}


def referenced_anywhere(ref):
    """PackageReference de todos los csproj y Directory.Build.props del repo en ese ref."""
    paths = git("ls-tree", "-r", "--name-only", ref).splitlines()
    names = set()
    for path in paths:
        if path.endswith(".csproj") or path.split("/")[-1] == "Directory.Build.props":
            names |= package_references(parse_xml(file_at(ref, path)))
    return names


def dependents_of(ref, package_ids_set, changed_versions):
    """Paquetes que referencian algún PackageVersion cambiado, directo o por ProjectReference."""
    shared = package_references(parse_xml(file_at(ref, BUILD_PROPS)))
    direct = {}
    projects = {}
    for package_id in package_ids_set:
        root = parse_xml(file_at(ref, f"src/{package_id}/{package_id}.csproj"))
        direct[package_id] = shared | package_references(root)
        projects[package_id] = {
            re.split(r"[\\/]", el.get("Include", ""))[-1].removesuffix(".csproj")
            for el in root.iter("ProjectReference")
        }
    affected = {p for p in package_ids_set if direct[p] & changed_versions}
    while True:
        more = {p for p in package_ids_set if p not in affected and projects[p] & affected}
        if not more:
            return affected
        affected |= more


def semver_key(version):
    """Clave ordenable según SemVer 2.0: núcleo numérico y prerelease menor que la release."""
    match = re.fullmatch(r"(\d+(?:\.\d+)*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?", version or "")
    if not match:
        raise ValueError(f"versión ilegible: '{version}'")
    core = [int(n) for n in match.group(1).split(".")]
    core += [0] * (4 - len(core))
    if match.group(2) is None:
        return (core, 1, [])
    pre = [(0, int(p), "") if p.isdigit() else (1, 0, p) for p in match.group(2).split(".")]
    return (core, 0, pre)


def main(argv):
    if len(argv) not in (2, 3):
        print(__doc__)
        return 2
    base = argv[1]
    head = argv[2] if len(argv) == 3 else "HEAD"

    changed = git("diff", "--name-only", base, head).splitlines()
    head_ids = package_ids(head)
    touched = {p.split("/")[1] for p in changed if p.startswith("src/") and p.count("/") >= 2}
    if BUILD_PROPS in changed:
        touched = set(head_ids)
    elif PACKAGES_PROPS in changed:
        changed_versions = changed_package_versions(base, head)
        if changed_versions is None or changed_versions - referenced_anywhere(base) - referenced_anywhere(head):
            touched = set(head_ids)
        else:
            touched |= dependents_of(head, set(head_ids), changed_versions)
    touched &= set(head_ids)

    failures = []
    for package_id in sorted(touched):
        old = csproj_version(base, package_id)
        if old is None:
            continue
        new = csproj_version(head, package_id)
        try:
            bumped = semver_key(new) > semver_key(old)
        except ValueError as error:
            failures.append(f"{package_id}: {error}")
            continue
        if not bumped:
            failures.append(f"{package_id}: cambió y su versión {new} no es mayor que la de la base ({old})")

    if failures:
        print("FALLA: paquetes modificados sin subir <Version>:")
        for line in failures:
            print(f"  - {line}")
        return 1
    print(f"OK: {len(touched)} paquete(s) modificado(s), todos con versión mayor o nuevos")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

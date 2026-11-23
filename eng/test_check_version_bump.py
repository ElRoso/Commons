#!/usr/bin/env python3
"""Casos de eng/check-version-bump.py sobre repos git temporales.

Uso: python3 -m unittest discover -s eng -p 'test_*.py'
"""

import os
import subprocess
import sys
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "check-version-bump.py")

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
\t<PropertyGroup>
\t\t<PackageId>{id}</PackageId>
\t\t<Version>{version}</Version>
\t</PropertyGroup>
\t<ItemGroup>
{refs}
\t</ItemGroup>
</Project>
"""

# Referencias de cada paquete del fixture: (PackageReference, ProjectReference).
REFS = {
    "Lib.Alpha": ([], []),
    "Lib.Beta": ([], []),
    "Lib.Mongo": (["Mongo.Driver"], ["Lib.Alpha"]),
    "Lib.Meta": ([], ["Lib.Alpha", "Lib.Beta", "Lib.Mongo"]),
}

VERSIONS = {"Mongo.Driver": "3.0.0", "Test.Runner": "2.0.0", "Shared.Tool": "1.0.0", "Old.Unused": "1.0.0", "Sample.Dep": "1.0.0", "Security.Pin": "1.0.0"}


def packages_props(versions, extra=""):
    items = "".join(f'\t\t<PackageVersion Include="{k}" Version="{v}" />\n' for k, v in versions.items())
    return f"<Project>\n{extra}\t<ItemGroup>\n{items}\t</ItemGroup>\n</Project>\n"


class VersionBumpTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.repo = self.tmp.name
        self.git("init", "-q", "-b", "mainline")
        self.git("config", "user.email", "ci@example.com")
        self.git("config", "user.name", "ci")
        self.write(
            "src/Directory.Build.props",
            '<Project><ItemGroup><PackageReference Include="Shared.Tool" /></ItemGroup></Project>\n',
        )
        self.write("Directory.Packages.props", packages_props(VERSIONS))
        self.write("Tests/Tests.csproj", '<Project><ItemGroup><PackageReference Include="Test.Runner" /></ItemGroup></Project>\n')
        self.write(
            "samples/Minimal/Minimal.csproj",
            '<Project><ItemGroup><PackageReference Include="Sample.Dep" /></ItemGroup></Project>\n',
        )
        self.write_package("Lib.Alpha", "1.0.0")
        self.write_package("Lib.Beta", "2.1.0")
        self.write_package("Lib.Mongo", "1.0.0")
        self.write_package("Lib.Meta", "2027.0.0")
        self.base = self.commit("base")

    def tearDown(self):
        self.tmp.cleanup()

    def git(self, *args):
        return subprocess.run(
            ["git", *args], cwd=self.repo, check=True, capture_output=True, text=True
        ).stdout.strip()

    def write(self, path, content):
        full = os.path.join(self.repo, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8") as f:
            f.write(content)

    def write_package(self, package_id, version, code="class A {}\n", refs=None):
        packages, projects = refs or REFS.get(package_id, ([], []))
        items = [f'\t\t<PackageReference Include="{p}" />' for p in packages]
        items += [f'\t\t<ProjectReference Include="..\\{p}\\{p}.csproj" />' for p in projects]
        self.write(
            f"src/{package_id}/{package_id}.csproj",
            CSPROJ.format(id=package_id, version=version, refs="\n".join(items)),
        )
        self.write(f"src/{package_id}/A.cs", code)

    def commit(self, message):
        self.git("add", "-A")
        self.git("commit", "-q", "-m", message)
        return self.git("rev-parse", "HEAD")

    def run_guard(self):
        return subprocess.run(
            [sys.executable, SCRIPT, self.base], cwd=self.repo, capture_output=True, text=True
        )

    def test_code_change_without_bump_fails_naming_the_package(self):
        self.write("src/Lib.Alpha/A.cs", "class A { int x; }\n")
        self.commit("cambio")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Lib.Alpha", result.stdout)
        self.assertNotIn("Lib.Beta", result.stdout)

    def test_code_change_with_bump_passes(self):
        self.write_package("Lib.Alpha", "1.0.1", code="class A { int x; }\n")
        self.commit("cambio con bump")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_version_lowered_fails(self):
        self.write_package("Lib.Beta", "2.0.9", code="class A { int x; }\n")
        self.commit("baja")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Lib.Beta", result.stdout)

    def test_prerelease_of_same_version_is_not_greater(self):
        self.write_package("Lib.Alpha", "1.0.0-beta.1", code="class A { int x; }\n")
        self.commit("pre")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)

    def test_numeric_comparison_not_lexical(self):
        self.write_package("Lib.Alpha", "1.0.10", code="class A { int x; }\n")
        self.commit("bump")
        self.base = self.git("rev-parse", "HEAD")
        self.write_package("Lib.Alpha", "1.0.9", code="class A { int y; }\n")
        self.commit("baja lexica")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)

    def test_only_tests_samples_eng_github_docs_passes(self):
        self.write("Tests/Tests.csproj", "<Project><!-- x --></Project>\n")
        self.write("samples/Minimal/Program.cs", "// x\n")
        self.write("eng/tool.py", "# x\n")
        self.write(".github/workflows/ci.yml", "name: x\n")
        self.write("docs/guide.md", "# x\n")
        self.write("README.md", "# x\n")
        self.commit("sin paquetes")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_new_package_passes(self):
        self.write_package("Lib.Gamma", "0.1.0")
        self.commit("nuevo")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_shared_build_props_requires_bump_of_every_package(self):
        self.write("src/Directory.Build.props", "<Project><!-- x --></Project>\n")
        self.write_package("Lib.Alpha", "1.1.0")
        self.commit("props")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Lib.Beta", result.stdout)
        self.assertNotIn("Lib.Alpha", result.stdout)

    def test_shared_build_props_with_every_package_bumped_passes(self):
        self.write("src/Directory.Build.props", "<Project><!-- x --></Project>\n")
        self.write_package("Lib.Alpha", "1.0.1")
        self.write_package("Lib.Beta", "3.0.0")
        self.write_package("Lib.Mongo", "1.1.0")
        self.write_package("Lib.Meta", "2027.0.1")
        self.commit("props con bump")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def change_versions(self, **changes):
        versions = dict(VERSIONS)
        for key, value in changes.items():
            name = key.replace("_", ".")
            if value is None:
                versions.pop(name)
            else:
                versions[name] = value
        self.write("Directory.Packages.props", packages_props(versions))

    def test_packages_props_version_used_only_by_tests_passes(self):
        self.change_versions(Test_Runner="2.1.0")
        self.commit("runner")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_packages_props_version_requires_bump_of_users_and_their_dependents(self):
        self.change_versions(Mongo_Driver="3.1.0")
        self.commit("mongo")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Lib.Mongo:", result.stdout)
        self.assertIn("Lib.Meta:", result.stdout)
        self.assertNotIn("Lib.Alpha:", result.stdout)
        self.assertNotIn("Lib.Beta:", result.stdout)

    def test_packages_props_version_with_users_bumped_passes(self):
        self.change_versions(Mongo_Driver="3.1.0")
        self.write_package("Lib.Mongo", "1.0.1")
        self.write_package("Lib.Meta", "2027.0.1")
        self.commit("mongo con bump")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_packages_props_version_referenced_from_build_props_requires_every_package(self):
        self.change_versions(Shared_Tool="1.1.0")
        self.commit("tool")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        for package_id in REFS:
            self.assertIn(f"{package_id}:", result.stdout)

    def assert_requires_every_package(self, result):
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        for package_id in REFS:
            self.assertIn(f"{package_id}:", result.stdout)

    def bump_every_package(self):
        self.write_package("Lib.Alpha", "1.0.1")
        self.write_package("Lib.Beta", "2.1.1")
        self.write_package("Lib.Mongo", "1.0.1")
        self.write_package("Lib.Meta", "2027.0.1")

    def test_packages_props_pin_change_requires_every_package(self):
        self.change_versions(Security_Pin="1.0.1")
        self.commit("pin")
        self.assert_requires_every_package(self.run_guard())

    def test_packages_props_pin_change_with_every_package_bumped_passes(self):
        self.change_versions(Security_Pin="1.0.1")
        self.bump_every_package()
        self.commit("pin con bump")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_packages_props_added_pin_requires_every_package(self):
        self.change_versions(New_Pin="1.0.0")
        self.commit("agrega pin")
        self.assert_requires_every_package(self.run_guard())

    def test_packages_props_removed_pin_requires_every_package(self):
        self.change_versions(Old_Unused=None)
        self.commit("borra pin")
        self.assert_requires_every_package(self.run_guard())

    def test_packages_props_version_used_only_by_samples_passes(self):
        self.change_versions(Sample_Dep="1.1.0")
        self.commit("sample")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_packages_props_removed_test_dependency_passes(self):
        self.change_versions(Test_Runner=None)
        self.write("Tests/Tests.csproj", "<Project />\n")
        self.commit("saca runner")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_packages_props_added_version_used_by_package_requires_its_bump(self):
        self.change_versions(New_Lib="1.0.0")
        self.write_package("Lib.Beta", "2.1.0", refs=(["New.Lib"], []))
        self.commit("agrega y usa")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Lib.Beta:", result.stdout)
        self.assertIn("Lib.Meta:", result.stdout)
        self.assertNotIn("Lib.Mongo:", result.stdout)

    def test_packages_props_comment_only_passes(self):
        self.write("Directory.Packages.props", packages_props(VERSIONS).replace("<Project>", "<Project><!-- x -->"))
        self.commit("comentario")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_packages_props_change_outside_package_versions_requires_every_package(self):
        extra = "\t<PropertyGroup><CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled></PropertyGroup>\n"
        self.write("Directory.Packages.props", packages_props(VERSIONS, extra))
        self.commit("propiedad")
        result = self.run_guard()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        for package_id in REFS:
            self.assertIn(f"{package_id}:", result.stdout)

    def test_deleted_package_passes(self):
        self.git("rm", "-r", "-q", "src/Lib.Beta")
        self.commit("borra")
        result = self.run_guard()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()

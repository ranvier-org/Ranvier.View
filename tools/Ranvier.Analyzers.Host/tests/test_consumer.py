"""Portable external-consumer proof for the packaged Ranvier analyzer host.

Unit mode is Python-only. Remote CI can invoke `--run-external` with the
freshly packed host/analyzer feed to run the real net10.0 consumer scenarios.
"""

from pathlib import Path
import argparse
import ctypes
from dataclasses import dataclass
import hashlib
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
import zipfile
import xml.etree.ElementTree as ET
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_MARKER = 'test "fixed buffers preserve calls and diagnostics across their whole scope"'
PUBLIC_RANVIER_VERSION = "0.1.1-preview.1"
HOST_PACKAGE_ID = "Ranvier.Analyzers.Host"
ANALYZERS_PACKAGE_ID = "Ranvier.Analyzers"
NUGET_SOURCE = "https://api.nuget.org/v3/index.json"
DEFAULT_TIMEOUT_SECONDS = 540
DEFAULT_SDK_VERSION = "10.0.403"


@dataclass(frozen=True)
class Package:
    package_id: str
    version: str
    path: Path
    nuspec_name: str


@dataclass(frozen=True)
class PrivatePackagePlan:
    analyzers: Package
    host: Package
    host_dependency_version: str
    manifest: dict


def _local_name(tag):
    return tag.rsplit("}", 1)[-1]


def _child_text(element, name):
    child = next((node for node in element if _local_name(node.tag) == name), None)
    if child is None or child.text is None:
        raise ValueError(f"NuGet nuspec is missing {name}")
    return child.text.strip()


def _nuspec_from_package(path):
    with zipfile.ZipFile(path) as archive:
        names = [name for name in archive.namelist()
                 if name.lower().endswith(".nuspec") and "/" not in name]
        if len(names) != 1:
            raise ValueError(f"Expected one root nuspec in {path.name}, found {len(names)}")
        name = names[0]
        root = ET.fromstring(archive.read(name))
        metadata = next((node for node in root if _local_name(node.tag) == "metadata"), None)
        if metadata is None:
            raise ValueError(f"NuGet nuspec has no metadata: {path.name}")
        package = Package(_child_text(metadata, "id"), _child_text(metadata, "version"), path, name)
        return package, metadata


def discover_private_packages(feed):
    feed = Path(feed).resolve()
    if not feed.is_dir():
        raise ValueError(f"Private analyzer package feed does not exist: {feed}")
    packages = {ANALYZERS_PACKAGE_ID.casefold(): [], HOST_PACKAGE_ID.casefold(): []}
    nuspecs = {}
    for path in sorted(feed.glob("*.nupkg"), key=lambda item: item.name.casefold()):
        package, metadata = _nuspec_from_package(path)
        key = package.package_id.casefold()
        if key in packages:
            packages[key].append(package)
            nuspecs[key] = metadata
    for key, values in packages.items():
        if len(values) != 1:
            raise ValueError(f"Expected exactly one {key} package in private feed; found {len(values)}")
    analyzers = packages[ANALYZERS_PACKAGE_ID.casefold()][0]
    host = packages[HOST_PACKAGE_ID.casefold()][0]
    analyzer_metadata = nuspecs[ANALYZERS_PACKAGE_ID.casefold()]
    dependencies = [node for node in analyzer_metadata.iter()
                    if _local_name(node.tag) == "dependency"
                    and node.attrib.get("id", "").casefold() == HOST_PACKAGE_ID.casefold()]
    if len(dependencies) != 1:
        raise ValueError(f"Analyzer package must declare exactly one {HOST_PACKAGE_ID} dependency")
    dependency_version = dependencies[0].attrib.get("version", "").strip()
    normalized_version = dependency_version.strip("[]")
    if normalized_version != host.version:
        raise ValueError(f"Analyzer nuspec requests host {dependency_version}; feed contains {host.version}")

    manifest_name = "tools/net10.0/any/vendor-manifest.json"
    compiler_name = "tools/net10.0/any/FSharp.Compiler.Service.dll"
    with zipfile.ZipFile(host.path) as archive:
        names = archive.namelist()
        if manifest_name not in names or compiler_name not in names:
            raise ValueError("Host package is missing the compiler manifest or compiler DLL")
        manifest_bytes = archive.read(manifest_name)
        manifest = json.loads(manifest_bytes)
        compiler_bytes = archive.read(compiler_name)
    compiler_hash = hashlib.sha256(compiler_bytes).hexdigest()
    if compiler_hash != manifest.get("compilerSha256"):
        raise ValueError("Host package compiler bytes do not match its embedded vendor manifest")
    if manifest.get("compilerVersion") != "43.12.402.0" or manifest.get("compilerPublicKeyToken") != "":
        raise ValueError("Host manifest has an unexpected compiler assembly version or public key token")
    return PrivatePackagePlan(analyzers, host, normalized_version, manifest)


def write_nuget_config(feed, destination):
    feed = Path(feed).resolve()
    destination = Path(destination)
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="ranvier-analyzer-feed", value=str(feed))
    ET.SubElement(sources, "add", key="nuget.org", value=NUGET_SOURCE)
    mapping = ET.SubElement(config, "packageSourceMapping")
    private = ET.SubElement(mapping, "packageSource", key="ranvier-analyzer-feed")
    ET.SubElement(private, "package", pattern=ANALYZERS_PACKAGE_ID)
    ET.SubElement(private, "package", pattern=HOST_PACKAGE_ID)
    public = ET.SubElement(mapping, "packageSource", key="nuget.org")
    ET.SubElement(public, "package", pattern="*")
    destination.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(config).write(destination, encoding="utf-8", xml_declaration=True)
    return destination


def extract_fixed_buffer_fixture(repo_root):
    source_path = Path(repo_root) / "tools/Ranvier.Analyzers.Tests/IndexTests.fs"
    source = source_path.read_text(encoding="utf-8")
    if source.count(FIXTURE_MARKER) != 1:
        raise ValueError(f"Expected one proven fixture marker in {source_path}")
    after_case = source.split(FIXTURE_MARKER, 1)[1]
    parts = after_case.split('"""', 2)
    if len(parts) != 3:
        raise ValueError(f"Could not extract the proven triple-quoted fixture from {source_path}")
    return parts[1]


def external_fixtures(repo_root):
    negative = extract_fixed_buffer_fixture(repo_root)
    marker = "createEffect ignore; "
    if negative.count(marker) != 1:
        raise ValueError("The proven negative fixture no longer has its single owned-effect diagnostic")
    positive = negative.replace(marker, "", 1) + "\nlet convert (value: int64) = nativeint value\n"
    return negative, positive


def feed_fingerprint(feed):
    digest = hashlib.sha256()
    for path in sorted(Path(feed).glob("*.nupkg"), key=lambda item: item.name.casefold()):
        digest.update(path.name.casefold().encode("utf-8"))
        digest.update(b"\0")
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def write_global_json(directory, sdk_version):
    path = Path(directory) / "global.json"
    path.write_text(json.dumps({"sdk": {"version": sdk_version, "rollForward": "disable",
                                       "allowPrerelease": False}}, indent=2) + "\n", encoding="utf-8")
    return path


def project_text(analyzers_version):
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(properties, "TargetFramework").text = "net10.0"
    ET.SubElement(properties, "GenerateDocumentationFile").text = "false"
    references = ET.SubElement(project, "ItemGroup")
    ET.SubElement(references, "PackageReference", Include="Ranvier", Version=PUBLIC_RANVIER_VERSION)
    ET.SubElement(references, "PackageReference", Include=ANALYZERS_PACKAGE_ID,
                  Version=analyzers_version, PrivateAssets="all")
    return ET.tostring(project, encoding="unicode")


def build_command(dotnet, project, heap_bytes=None):
    command = [dotnet, "build", str(project), "-c", "Release", "--no-restore", "--nologo",
               "-v", "normal", "-m:1", "-nodeReuse:false", "-p:RanvierAnalyze=true"]
    if heap_bytes is not None:
        command.append(f"-p:RanvierAnalyzerHeapHardLimitBytes={heap_bytes}")
    return command


def child_environment(cache, provenance):
    env = os.environ.copy()
    for name in ("DOTNET_GCHeapHardLimit", "COMPlus_GCHeapHardLimit",
                 "DOTNET_GCHeapHardLimitPercent", "COMPlus_GCHeapHardLimitPercent",
                 "RanvierAnalyzerHeapHardLimitBytes", "RANVIER_ANALYZER_HOST_PROVENANCE_DIR",
                 "RestoreConfigFile", "RestorePackagesPath", "NuGetPackageRoot",
                 "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "UseLocalRanvier", "RanvierRoot",
                 "RanvierNativeApiContractRoot"):
        env.pop(name, None)
    cache = Path(cache).resolve()
    env["NUGET_PACKAGES"] = str(cache)
    env["NUGET_HTTP_CACHE_PATH"] = str(cache / "http-cache")
    env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    env["MSBUILDDISABLENODEREUSE"] = "1"
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env["DOTNET_NOLOGO"] = "1"
    env["RANVIER_ANALYZER_HOST_PROVENANCE_DIR"] = str(Path(provenance).resolve())
    return env


def _write_fake_package(path, package_id, version, files, dependency=None):
    dependency_xml = ""
    if dependency:
        dependency_xml = f'<dependencies><group targetFramework="net10.0"><dependency id="{dependency[0]}" version="{dependency[1]}" /></group></dependencies>'
    nuspec = (f'<?xml version="1.0"?><package><metadata><id>{package_id}</id><version>{version}</version>'
              f'{dependency_xml}</metadata></package>')
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        for name, data in files.items():
            archive.writestr(name, data)


def create_fake_feed(feed):
    feed = Path(feed)
    compiler = b"synthetic compiler bytes for metadata-only feed fixture"
    compiler_hash = hashlib.sha256(compiler).hexdigest()
    manifest = json.dumps({"compilerSha256": compiler_hash, "compilerVersion": "43.12.402.0",
                           "compilerPublicKeyToken": "", "compilerSourceCommit": "fixture",
                           "sourceGuardSha256": "fixture", "cliSourceCommit": "fixture"})
    _write_fake_package(feed / "Ranvier.Analyzers.Host.0.1.0-preview.1.nupkg",
                        HOST_PACKAGE_ID, "0.1.0-preview.1",
                        {"tools/net10.0/any/vendor-manifest.json": manifest,
                         "tools/net10.0/any/FSharp.Compiler.Service.dll": compiler})
    _write_fake_package(feed / "Ranvier.Analyzers.0.1.1-preview.2.nupkg",
                        ANALYZERS_PACKAGE_ID, "0.1.1-preview.2", {},
                        dependency=(HOST_PACKAGE_ID, "0.1.0-preview.1"))


def ensure_outside_checkout(path, repo_root):
    path = Path(path).resolve()
    checkout = Path(repo_root).resolve()
    try:
        path.relative_to(checkout)
    except ValueError:
        return path
    raise ValueError(f"External-consumer evidence and temp paths must remain outside the checkout: {path}")


def run_command(command, cwd, env, log_path, timeout_seconds):
    log_path = Path(log_path)
    log_path.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    creationflags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
    log_stream = log_path.open("wb")
    try:
        process = subprocess.Popen(command, cwd=cwd, env=env, stdout=log_stream,
                                   stderr=subprocess.STDOUT, shell=False, start_new_session=os.name != "nt",
                                   creationflags=creationflags)
    except BaseException:
        log_stream.close()
        raise
    timed_out = False
    tree_kill = None
    try:
        try:
            exit_code = process.wait(timeout=timeout_seconds)
        except subprocess.TimeoutExpired:
            timed_out = True
            tree_kill = _terminate_process_tree(process)
            exit_code = process.wait(timeout=10)
        log_stream.flush()
        return {"command": [str(item) for item in command], "cwd": str(Path(cwd).resolve()),
                "exitCode": exit_code, "timedOut": timed_out,
                "timeoutSeconds": timeout_seconds if timed_out else None,
                "timeoutReason": f"child process exceeded {timeout_seconds:g}s deadline" if timed_out else None,
                "durationSeconds": round(time.monotonic() - started, 3),
                "logPath": str(log_path.resolve()),
                "logSha256": hashlib.sha256(log_path.read_bytes()).hexdigest(),
                "processId": process.pid, "treeKill": tree_kill}
    except BaseException:
        if process.poll() is None:
            _terminate_process_tree(process)
        raise
    finally:
        log_stream.close()


def _terminate_process_tree(process):
    if os.name == "nt":
        command = ["taskkill.exe", "/PID", str(process.pid), "/T", "/F"]
        try:
            result = subprocess.run(command, capture_output=True, text=True, timeout=10, check=False)
            result_data = {"command": command, "exitCode": result.returncode,
                           "verified": result.returncode == 0}
        except (OSError, subprocess.TimeoutExpired) as error:
            result_data = {"command": command, "verified": False, "error": str(error)}
        if process.poll() is None:
            process.kill()
    else:
        result_data = {"processGroup": process.pid, "signal": "TERM then KILL", "verified": True}
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=1)
        except subprocess.TimeoutExpired:
            pass
        try:
            # Catch descendants that ignored TERM even if the group leader has exited.
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=10)
        result_data["verified"] = False
        result_data["error"] = "owned process did not exit after tree termination"
    return result_data


def _write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _read_log(path):
    return Path(path).read_text(encoding="utf-8", errors="replace")


def _asset_libraries(project):
    assets_path = Path(project).parent / "obj/project.assets.json"
    if not assets_path.is_file():
        raise ValueError(f"Restore did not produce {assets_path}")
    assets = json.loads(assets_path.read_text(encoding="utf-8"))
    libraries = {key.casefold(): value for key, value in assets.get("libraries", {}).items()}
    package_folders = [Path(folder).resolve() for folder in assets.get("packageFolders", {})]
    return assets, libraries, package_folders


def _verify_restore_graph(project, plan, cache, evidence_path):
    assets, libraries, package_folders = _asset_libraries(project)
    expected = {
        f"{ANALYZERS_PACKAGE_ID}/{plan.analyzers.version}".casefold(),
        f"{HOST_PACKAGE_ID}/{plan.host.version}".casefold(),
        f"Ranvier/{PUBLIC_RANVIER_VERSION}".casefold(),
    }
    missing = sorted(key for key in expected if key not in libraries)
    if missing:
        raise ValueError(f"Restore graph is missing required public/private packages: {missing}")
    cache = Path(cache).resolve()
    if not package_folders or any(folder != cache for folder in package_folders):
        raise ValueError(f"Restore used package folders outside the fresh cache: {package_folders}")
    graph = {"assetsPath": str((Path(project).parent / "obj/project.assets.json").resolve()),
             "packageFolders": [str(folder) for folder in package_folders],
             "libraries": sorted(libraries),
             "required": sorted(expected)}
    _write_json(evidence_path, graph)
    return graph


def _installed_host_assets(cache, plan):
    package_root = Path(cache) / HOST_PACKAGE_ID.casefold() / plan.host.version
    payload = package_root / "tools/net10.0/any"
    manifest_path = payload / "vendor-manifest.json"
    compiler_path = payload / "FSharp.Compiler.Service.dll"
    host_path = payload / "Ranvier.Analyzers.Host.dll"
    if not manifest_path.is_file() or not compiler_path.is_file() or not host_path.is_file():
        raise ValueError(f"Restored host payload is incomplete under {payload}")
    manifest_bytes = manifest_path.read_bytes()
    manifest = json.loads(manifest_bytes)
    compiler_hash = hashlib.sha256(compiler_path.read_bytes()).hexdigest()
    if compiler_hash != manifest.get("compilerSha256") or compiler_hash != plan.manifest.get("compilerSha256"):
        raise ValueError("Restored host compiler bytes do not match the package's actual manifest")
    return {"payloadDirectory": str(payload.resolve()), "manifestPath": str(manifest_path.resolve()),
            "manifestSha256": hashlib.sha256(manifest_bytes).hexdigest(), "manifest": manifest,
            "compilerPath": str(compiler_path.resolve()), "compilerSha256": compiler_hash,
            "hostPath": str(host_path.resolve())}


def _verify_host_receipts(provenance_dir, payload, expected_heap, evidence_dir):
    receipt_paths = sorted(Path(provenance_dir).glob("host-*.json"))
    if not receipt_paths:
        raise ValueError(f"The analyzer host did not write a provenance receipt under {provenance_dir}")
    manifest = payload["manifest"]
    receipts = []
    for path in receipt_paths:
        receipt = json.loads(path.read_text(encoding="utf-8"))
        if not receipt.get("matched"):
            raise ValueError(f"Host reported a compiler identity mismatch: {path}")
        if Path(receipt["compilerAssemblyPath"]).resolve() != Path(payload["compilerPath"]):
            raise ValueError(f"Host loaded compiler from an unexpected path: {receipt['compilerAssemblyPath']}")
        if Path(receipt["hostAssemblyPath"]).resolve() != Path(payload["hostPath"]):
            raise ValueError(f"Host assembly was loaded from an unexpected package path: {receipt['hostAssemblyPath']}")
        expected_fields = {
            "compilerVersion": manifest["compilerVersion"],
            "compilerPublicKeyToken": manifest["compilerPublicKeyToken"],
            "compilerSha256": manifest["compilerSha256"],
            "compilerSourceCommit": manifest["compilerSourceCommit"],
            "sourceGuardSha256": manifest["sourceGuardSha256"],
            "cliSourceCommit": manifest["cliSourceCommit"],
            "gcTotalAvailableMemoryBytes": expected_heap,
            "gcIsServer": False,
        }
        for key, value in expected_fields.items():
            if receipt.get(key) != value:
                raise ValueError(f"Host receipt {path.name} has {key}={receipt.get(key)!r}; expected {value!r}")
        if receipt.get("gcHeapHardLimitEnvironment") not in (None, ""):
            raise ValueError("Host process inherited a manual heap-limit environment override")
        copied = Path(evidence_dir) / path.name
        copied.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, copied)
        receipts.append({"path": str(copied.resolve()), "sha256": hashlib.sha256(copied.read_bytes()).hexdigest(),
                         "processId": receipt.get("processId"), "compilerAssemblyPath": receipt.get("compilerAssemblyPath"),
                         "compilerSha256": receipt.get("compilerSha256"),
                         "gcTotalAvailableMemoryBytes": receipt.get("gcTotalAvailableMemoryBytes")})
    return receipts


def _scenario_specs():
    return [
        {"name": "positive-default-4g", "fixture": "positive", "heapProperty": None,
         "expectedHeap": 4 * 1024 ** 3, "expectedExit": 0, "diagnostic": "absent"},
        {"name": "positive-override-2g", "fixture": "positive", "heapProperty": str(2 * 1024 ** 3),
         "expectedHeap": 2 * 1024 ** 3, "expectedExit": 0, "diagnostic": "absent"},
        {"name": "invalid-heap-zero", "fixture": "positive", "heapProperty": "0",
         "expectedHeap": None, "expectedExit": "nonzero", "diagnostic": "invalid-heap"},
        {"name": "negative-ran010", "fixture": "negative", "heapProperty": None,
         "expectedHeap": 4 * 1024 ** 3, "expectedExit": "nonzero", "diagnostic": "present"},
    ]


def run_external_consumer(args):
    repo_root = Path(args.repo_root).resolve()
    evidence = ensure_outside_checkout(args.evidence_dir, repo_root)
    evidence.mkdir(parents=True, exist_ok=True)
    feed = Path(args.host_feed).resolve()
    plan = discover_private_packages(feed)
    fingerprint = feed_fingerprint(feed)
    config_path = write_nuget_config(feed, evidence / "NuGet.Config")
    negative_fixture, positive_fixture = external_fixtures(repo_root)
    record = {"feedFingerprintSha256": fingerprint,
              "sdkVersion": args.sdk_version,
              "privatePackages": [{"id": plan.analyzers.package_id, "version": plan.analyzers.version,
                                   "file": plan.analyzers.path.name, "sha256": hashlib.sha256(plan.analyzers.path.read_bytes()).hexdigest()},
                                  {"id": plan.host.package_id, "version": plan.host.version,
                                   "file": plan.host.path.name, "sha256": hashlib.sha256(plan.host.path.read_bytes()).hexdigest()}],
              "publicCoreReference": {"id": "Ranvier", "version": PUBLIC_RANVIER_VERSION},
              "sourceMapping": {"privateFeedIds": [ANALYZERS_PACKAGE_ID, HOST_PACKAGE_ID], "publicFeed": NUGET_SOURCE},
              "fixtureHashes": {"negative": hashlib.sha256(negative_fixture.encode()).hexdigest(),
                                "positive": hashlib.sha256(positive_fixture.encode()).hexdigest()},
              "nugetConfigPath": str(config_path.resolve()), "scenarios": []}
    _write_json(evidence / "private-feed-manifest.json", record["privatePackages"])
    host_manifest_path = evidence / "host-vendor-manifest.json"
    with zipfile.ZipFile(plan.host.path) as package:
        host_manifest_bytes = package.read("tools/net10.0/any/vendor-manifest.json")
    host_manifest_path.write_bytes(host_manifest_bytes)
    record["hostVendorManifest"] = {"path": str(host_manifest_path.resolve()),
                                    "sha256": hashlib.sha256(host_manifest_bytes).hexdigest(),
                                    "manifest": json.loads(host_manifest_bytes)}
    temp_parent = Path(tempfile.gettempdir()).resolve()
    ensure_outside_checkout(temp_parent, repo_root)
    all_passed = True
    with tempfile.TemporaryDirectory(prefix="ranvier-analyzer-external-") as temporary:
        temp_root = ensure_outside_checkout(temporary, repo_root)
        global_json = write_global_json(temp_root, args.sdk_version)
        record["temporarySdkSelector"] = {"path": str(global_json),
                                          "sha256": hashlib.sha256(global_json.read_bytes()).hexdigest()}
        cache = temp_root / f"nuget-cache-{fingerprint[:20]}"
        cache.mkdir(parents=True, exist_ok=True)
        payload = None
        for spec in _scenario_specs():
            case_evidence = evidence / "scenarios" / spec["name"]
            case_evidence.mkdir(parents=True, exist_ok=True)
            work = temp_root / "consumers" / spec["name"]
            work.mkdir(parents=True, exist_ok=True)
            fixture = positive_fixture if spec["fixture"] == "positive" else negative_fixture
            (work / "Fixture.fs").write_text(fixture, encoding="utf-8")
            project_path = work / "Fixture.fsproj"
            project_path.write_text(project_text(plan.analyzers.version), encoding="utf-8")
            shutil.copy2(work / "Fixture.fs", case_evidence / "Fixture.fs")
            (case_evidence / "Fixture.fsproj").write_text(project_text(plan.analyzers.version), encoding="utf-8")
            provenance = case_evidence / "provenance"
            provenance.mkdir(parents=True, exist_ok=True)
            env = child_environment(cache, provenance)
            dotnet_path = shutil.which(args.dotnet) or args.dotnet
            env["DOTNET_HOST_PATH"] = str(Path(dotnet_path).resolve()) if Path(dotnet_path).exists() else str(dotnet_path)
            restore_command = [args.dotnet, "restore", str(project_path), "--configfile", str(config_path),
                               "--packages", str(cache), "--no-cache", "--force", "--verbosity", "normal"]
            restore = run_command(restore_command, work, env, case_evidence / "restore.log", args.timeout_seconds)
            scenario = {"name": spec["name"], "restore": restore, "build": None, "passed": False,
                        "heapProperty": spec["heapProperty"], "expectedHeapBytes": spec["expectedHeap"]}
            try:
                if restore["exitCode"] != 0 or restore["timedOut"]:
                    raise ValueError("External consumer package restore failed or timed out")
                graph = _verify_restore_graph(project_path, plan, cache, case_evidence / "restore-graph.json")
                scenario["restoreGraph"] = graph
                if payload is None:
                    payload = _installed_host_assets(cache, plan)
                    installed_manifest_path = evidence / "restored-host-vendor-manifest.json"
                    shutil.copy2(payload["manifestPath"], installed_manifest_path)
                    payload["installedManifestPath"] = str(installed_manifest_path.resolve())
                    payload["installedManifestSha256"] = hashlib.sha256(installed_manifest_path.read_bytes()).hexdigest()
                    record["restoredHostPayload"] = payload
                command = build_command(args.dotnet, project_path, spec["heapProperty"])
                build = run_command(command, work, env, case_evidence / "build.log", args.timeout_seconds)
                scenario["build"] = build
                build_text = _read_log(case_evidence / "build.log")
                if spec["expectedExit"] == 0:
                    if build["exitCode"] != 0 or build["timedOut"]:
                        raise ValueError(f"Expected successful build, got exit {build['exitCode']}")
                elif spec["expectedExit"] == "nonzero":
                    if build["exitCode"] == 0 or build["timedOut"]:
                        raise ValueError(f"Expected a bounded validation/diagnostic failure, got exit {build['exitCode']}")
                if spec["diagnostic"] == "present" and not re.search(r"error\s+RAN010:", build_text, re.IGNORECASE):
                    raise ValueError("The negative external consumer did not report error RAN010")
                if spec["diagnostic"] == "absent" and re.search(r"RAN010", build_text, re.IGNORECASE):
                    raise ValueError("The positive external consumer unexpectedly reported RAN010")
                if spec["diagnostic"] == "invalid-heap":
                    if "RanvierAnalyzerHeapHardLimitBytes must be a positive decimal byte count" not in build_text:
                        raise ValueError("Heap value 0 did not fail the host property validation")
                if spec["expectedHeap"] is None:
                    if list(provenance.glob("host-*.json")):
                        raise ValueError("Invalid heap setting reached the host process before validation failed")
                    scenario["hostReceipts"] = []
                else:
                    scenario["hostReceipts"] = _verify_host_receipts(
                        provenance, payload, spec["expectedHeap"], case_evidence / "provenance-retained")
                scenario["passed"] = True
            except Exception as error:
                scenario["error"] = str(error)
                all_passed = False
            _write_json(case_evidence / "scenario.json", scenario)
            record["scenarios"].append({"name": spec["name"], "passed": scenario["passed"],
                                         "error": scenario.get("error"),
                                         "restoreExit": restore["exitCode"],
                                         "buildExit": scenario["build"]["exitCode"] if scenario["build"] else None,
                                         "timedOut": bool(restore["timedOut"] or (scenario["build"] or {}).get("timedOut"))})
    record["passed"] = all_passed and len(record["scenarios"]) == len(_scenario_specs())
    record["temporaryWorkspaceRemoved"] = True
    _write_json(evidence / "external-consumer-proof.json", record)
    print(json.dumps({"passed": record["passed"], "evidenceDirectory": str(evidence),
                      "scenarios": record["scenarios"],
                      "compilerSha256": record.get("restoredHostPayload", {}).get("compilerSha256")}, indent=2))
    return 0 if record["passed"] else 1


class ConsumerHarnessContract(unittest.TestCase):
    def test_fixtures_preserve_the_existing_positive_and_negative_cases(self):
        original = extract_fixed_buffer_fixture(REPO_ROOT)
        negative, positive = external_fixtures(REPO_ROOT)
        self.assertEqual(negative, original)
        self.assertEqual(original.count("createEffect ignore; "), 1)
        self.assertEqual(positive, original.replace("createEffect ignore; ", "", 1)
                         + "\nlet convert (value: int64) = nativeint value\n")

    def test_nuget_config_maps_only_analyzer_packages_to_the_private_feed(self):
        with tempfile.TemporaryDirectory(prefix="consumer-config-test-") as temporary:
            feed = Path(temporary) / "host-feed"
            feed.mkdir()
            config_path = Path(temporary) / "NuGet.Config"
            write_nuget_config(feed, config_path)
            config = ET.parse(config_path).getroot()
        sources = {item.attrib["key"]: item.attrib["value"]
                   for item in config.findall("./packageSources/add")}
        mapping = {node.attrib["key"]: [item.attrib["pattern"] for item in node.findall("package")]
                   for node in config.findall("./packageSourceMapping/packageSource")}
        self.assertEqual(set(sources), {"ranvier-analyzer-feed", "nuget.org"})
        self.assertEqual(mapping["ranvier-analyzer-feed"], ["Ranvier.Analyzers", "Ranvier.Analyzers.Host"])
        self.assertEqual(mapping["nuget.org"], ["*"])
        self.assertTrue(sources["ranvier-analyzer-feed"].endswith("host-feed"))

    def test_feed_pair_checks_nuspec_dependency_version_and_payload(self):
        with tempfile.TemporaryDirectory(prefix="consumer-feed-test-") as temporary:
            feed = Path(temporary)
            create_fake_feed(feed)
            plan = discover_private_packages(feed)
        self.assertEqual(plan.analyzers.version, "0.1.1-preview.2")
        self.assertEqual(plan.host.version, "0.1.0-preview.1")
        self.assertEqual(plan.host.version, plan.host_dependency_version)

    def test_fixture_project_uses_public_core_and_private_analyzer_package_ids(self):
        project = ET.fromstring(project_text("0.1.1-preview.2"))
        references = {node.attrib["Include"]: node.attrib["Version"]
                      for node in project.findall("./ItemGroup/PackageReference")}
        self.assertEqual(references, {"Ranvier": PUBLIC_RANVIER_VERSION,
                                      ANALYZERS_PACKAGE_ID: "0.1.1-preview.2"})
        self.assertNotIn(HOST_PACKAGE_ID, references)

    def test_default_build_does_not_pass_a_heap_override(self):
        default = build_command("dotnet", Path("Fixture.fsproj"))
        overridden = build_command("dotnet", Path("Fixture.fsproj"), str(2 * 1024 ** 3))
        invalid = build_command("dotnet", Path("Fixture.fsproj"), "0")
        self.assertFalse(any(item.startswith("-p:RanvierAnalyzerHeapHardLimitBytes=") for item in default))
        self.assertIn("-p:RanvierAnalyze=true", default)
        self.assertIn("-p:RanvierAnalyzerHeapHardLimitBytes=2147483648", overridden)
        self.assertIn("-p:RanvierAnalyzerHeapHardLimitBytes=0", invalid)

    def test_sdk_selector_is_exact_and_disables_roll_forward(self):
        with tempfile.TemporaryDirectory(prefix="consumer-sdk-selector-") as temporary:
            selector = json.loads(write_global_json(temporary, "10.0.403").read_text(encoding="utf-8"))
        self.assertEqual(selector, {"sdk": {"version": "10.0.403", "rollForward": "disable",
                                             "allowPrerelease": False}})

    def test_child_environment_isolated_from_parent_gc_and_restore_settings(self):
        inherited = {"DOTNET_GCHeapHardLimit": "0x40000000", "COMPlus_GCHeapHardLimit": "40000000",
                     "DOTNET_GCHeapHardLimitPercent": "50", "COMPlus_GCHeapHardLimitPercent": "50",
                     "RanvierAnalyzerHeapHardLimitBytes": "1", "NUGET_PACKAGES": "wrong-cache",
                     "RestoreConfigFile": "wrong-config"}
        original_heap = os.environ.get("DOTNET_GCHeapHardLimit")
        with mock.patch.dict(os.environ, inherited):
            env = child_environment(Path("fresh-packages"), Path("provenance"))
            self.assertNotIn("DOTNET_GCHeapHardLimit", env)
            self.assertNotIn("COMPlus_GCHeapHardLimit", env)
            self.assertNotIn("DOTNET_GCHeapHardLimitPercent", env)
            self.assertNotIn("COMPlus_GCHeapHardLimitPercent", env)
            self.assertNotIn("RanvierAnalyzerHeapHardLimitBytes", env)
            self.assertNotEqual(env["NUGET_PACKAGES"], "wrong-cache")
            self.assertNotIn("RestoreConfigFile", env)
        self.assertEqual(os.environ.get("DOTNET_GCHeapHardLimit"), original_heap)

    def test_feed_fingerprint_is_stable_and_tracks_private_package_bytes(self):
        with (tempfile.TemporaryDirectory(prefix="consumer-feed-hash-test-") as first,
              tempfile.TemporaryDirectory(prefix="consumer-feed-hash-test-") as second):
            create_fake_feed(Path(first))
            create_fake_feed(Path(second))
            self.assertEqual(feed_fingerprint(first), feed_fingerprint(second))
            package = next(Path(second).glob("Ranvier.Analyzers.Host.*.nupkg"))
            package.write_bytes(package.read_bytes() + b"changed")
            self.assertNotEqual(feed_fingerprint(first), feed_fingerprint(second))

    def test_evidence_path_cannot_be_inside_source_checkout(self):
        with tempfile.TemporaryDirectory(prefix="consumer-evidence-path-") as temporary:
            with self.assertRaises(ValueError):
                ensure_outside_checkout(Path(REPO_ROOT) / "artifacts", REPO_ROOT)
            self.assertEqual(ensure_outside_checkout(temporary, REPO_ROOT), Path(temporary).resolve())

    def test_bounded_runner_preserves_output_exit_and_cleans_timed_out_process_tree(self):
        with tempfile.TemporaryDirectory(prefix="consumer-process-test-") as temporary:
            root = Path(temporary)
            env = os.environ.copy()
            result = run_command([sys.executable, "-c", "print('partial output', flush=True); raise SystemExit(7)"],
                                 root, env, root / "exit.log", 5)
            self.assertEqual(result["exitCode"], 7)
            self.assertIn("partial output", (root / "exit.log").read_text(encoding="utf-8"))

            child_pid_path = root / "child.pid"
            code = ("import pathlib,subprocess,sys,time; "
                    "child=subprocess.Popen([sys.executable,'-c','import time;time.sleep(30)']); "
                    "pathlib.Path(sys.argv[1]).write_text(str(child.pid)); "
                    "print('child-started',flush=True); time.sleep(30)")
            timeout = run_command([sys.executable, "-c", code, str(child_pid_path)], root, env,
                                  root / "timeout.log", 0.75)
            self.assertTrue(timeout["timedOut"])
            self.assertNotEqual(timeout["exitCode"], 0)
            self.assertTrue(child_pid_path.is_file())
            child_pid = int(child_pid_path.read_text())
            deadline = time.monotonic() + 3
            while _pid_is_running(child_pid) and time.monotonic() < deadline:
                time.sleep(0.05)
            self.assertFalse(_pid_is_running(child_pid), f"owned child process {child_pid} survived timeout cleanup")


def _pid_is_running(pid):
    if os.name == "nt":
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel32.OpenProcess.restype = ctypes.c_void_p
        kernel32.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong)]
        kernel32.GetExitCodeProcess.restype = ctypes.c_int
        kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
        kernel32.CloseHandle.restype = ctypes.c_int
        handle = kernel32.OpenProcess(0x1000, 0, pid)
        if not handle:
            return False
        try:
            code = ctypes.c_ulong()
            return bool(kernel32.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == 259
        finally:
            kernel32.CloseHandle(handle)
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    stat = Path(f"/proc/{pid}/stat")
    if stat.exists():
        try:
            return stat.read_text().rsplit(") ", 1)[1][0] != "Z"
        except (OSError, IndexError):
            return True
    return True


if __name__ == "__main__":
    if "--run-external" not in sys.argv[1:]:
        unittest.main(verbosity=2)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-external", action="store_true")
    parser.add_argument("--host-feed", type=Path, required=True,
                        help="Directory containing exactly one Ranvier.Analyzers and Host nupkg")
    parser.add_argument("--evidence-dir", type=Path, required=True,
                        help="Persistent evidence directory outside the source checkout")
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--sdk-version", default=DEFAULT_SDK_VERSION)
    parser.add_argument("--timeout-seconds", type=float, default=DEFAULT_TIMEOUT_SECONDS)
    arguments = parser.parse_args()
    if arguments.timeout_seconds <= 0:
        parser.error("--timeout-seconds must be positive")
    sys.exit(run_external_consumer(arguments))

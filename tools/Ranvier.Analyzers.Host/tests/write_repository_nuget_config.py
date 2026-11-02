#!/usr/bin/env python3
"""Create a temporary repository qualification NuGet config without dropping feeds.

The ordinary external-consumer harness intentionally uses a minimal NuGet-only graph.
Repository-wide qualification also needs the checked-in Cloudsmith feeds, so this helper
copies the repository configuration and adds explicit exact-ID mappings for private feeds.
"""

from __future__ import annotations

import argparse
from copy import deepcopy
import json
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile


PACKAGE_ID = re.compile(r"^[A-Za-z0-9_.-]+$")
REQUIRED_PRIVATE_IDS = {"ranvier.analyzers", "ranvier.analyzers.host"}


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _tag_like(parent: ET.Element, name: str) -> str:
    if parent.tag.startswith("{"):
        namespace = parent.tag.split("}", 1)[0] + "}"
        return namespace + name
    return name


def _children(parent: ET.Element, name: str) -> list[ET.Element]:
    return [child for child in parent if _local_name(child.tag) == name]


def _package_ids_in_feed(feed: Path) -> set[str]:
    if not feed.is_dir():
        raise ValueError(f"Private package feed is not a directory: {feed}")
    packages = sorted(feed.glob("*.nupkg"))
    if not packages:
        raise ValueError(f"Private package feed contains no .nupkg files: {feed}")
    ids: set[str] = set()
    for package in packages:
        with zipfile.ZipFile(package) as archive:
            nuspecs = [name for name in archive.namelist()
                       if name.lower().endswith(".nuspec") and "/" not in name]
            if len(nuspecs) != 1:
                raise ValueError(f"Expected one root nuspec in {package.name}, found {len(nuspecs)}")
            root = ET.fromstring(archive.read(nuspecs[0]))
            metadata = next((node for node in root if _local_name(node.tag) == "metadata"), None)
            if metadata is None:
                raise ValueError(f"Package has no nuspec metadata: {package.name}")
            package_id = next((node.text.strip() for node in metadata
                               if _local_name(node.tag) == "id" and node.text), None)
            if not package_id:
                raise ValueError(f"Package has no nuspec ID: {package.name}")
            ids.add(package_id.casefold())
    return ids


def _validate_private_feed_specs(specs: list[dict]) -> list[dict]:
    if not isinstance(specs, list) or not specs:
        raise ValueError("privateFeeds must be a nonempty array")
    result = []
    seen_keys: set[str] = set()
    owners: dict[str, str] = {}
    mapped_ids: set[str] = set()
    for spec in specs:
        if not isinstance(spec, dict):
            raise ValueError("Each private feed entry must be an object")
        key, path, package_ids = spec.get("key"), spec.get("path"), spec.get("packageIds")
        if not isinstance(key, str) or not key.strip() or not isinstance(path, str) or not path.strip():
            raise ValueError("Each private feed needs nonempty key and path strings")
        folded_key = key.casefold()
        if folded_key in seen_keys:
            raise ValueError(f"Duplicate private feed key: {key}")
        seen_keys.add(folded_key)
        if not isinstance(package_ids, list) or not package_ids:
            raise ValueError(f"Private feed {key} needs at least one exact package ID")
        folded_ids = []
        for package_id in package_ids:
            if not isinstance(package_id, str) or not PACKAGE_ID.fullmatch(package_id):
                raise ValueError(f"Private package IDs must be exact IDs, not patterns: {package_id!r}")
            folded = package_id.casefold()
            if folded in owners:
                raise ValueError(f"Package {package_id} is assigned to both {owners[folded]} and {key}")
            owners[folded] = key
            folded_ids.append((package_id, folded))
        feed = Path(path).expanduser().resolve()
        cache_candidates = [Path.home() / ".nuget" / "packages"]
        if os.environ.get("NUGET_PACKAGES"):
            cache_candidates.append(Path(os.environ["NUGET_PACKAGES"]).expanduser().resolve())
        for cache in cache_candidates:
            try:
                feed.relative_to(cache.resolve())
            except ValueError:
                continue
            raise ValueError(f"Private source cannot point into a NuGet machine/package cache: {feed}")
        actual_ids = _package_ids_in_feed(feed)
        missing = [package_id for package_id, folded in folded_ids if folded not in actual_ids]
        if missing:
            raise ValueError(f"Feed {key} does not contain the explicitly mapped packages: {missing}")
        result.append({"key": key, "path": feed, "packageIds": [item[0] for item in folded_ids]})
        mapped_ids.update(folded for _, folded in folded_ids)
    missing_required = REQUIRED_PRIVATE_IDS - mapped_ids
    if missing_required:
        raise ValueError(f"Private feeds must map the exact analyzer package IDs: {sorted(missing_required)}")
    return result


def build_repository_config(root_config: Path, destination: Path, private_feeds: list[dict]) -> dict:
    source_path = root_config.resolve()
    destination_path = destination.resolve()
    if not source_path.is_file():
        raise FileNotFoundError(f"Repository NuGet config not found: {source_path}")
    if source_path == destination_path:
        raise ValueError("Destination must not overwrite the checked-in repository config")
    feeds = _validate_private_feed_specs(private_feeds)
    try:
        root = ET.parse(source_path).getroot()
    except ET.ParseError as error:
        raise ValueError(f"Invalid repository NuGet config XML: {error}") from error
    if _local_name(root.tag) != "configuration":
        raise ValueError("Root NuGet config element must be <configuration>")
    source_nodes = _children(root, "packageSources")
    mapping_nodes = _children(root, "packageSourceMapping")
    if len(source_nodes) != 1 or len(mapping_nodes) != 1:
        raise ValueError("Expected exactly one packageSources and one packageSourceMapping section")
    sources, mapping = source_nodes[0], mapping_nodes[0]
    existing_sources = {node.get("key", "").casefold(): node.get("value", "")
                        for node in _children(sources, "add")}
    mapping_by_key = {node.get("key", "").casefold(): node
                      for node in _children(mapping, "packageSource")}
    if len(mapping_by_key) != len(_children(mapping, "packageSource")):
        raise ValueError("Duplicate packageSourceMapping source keys are ambiguous")

    exact_owner: dict[str, str] = {}
    for source in _children(mapping, "packageSource"):
        key = source.get("key", "").casefold()
        for package in _children(source, "package"):
            pattern = package.get("pattern", "")
            if not pattern or any(c in pattern for c in "*?"):
                continue
            folded = pattern.casefold()
            prior = exact_owner.get(folded)
            if prior is not None and prior != key:
                raise ValueError(f"Exact package ID is already mapped to multiple sources: {pattern}")
            exact_owner[folded] = key

    for feed in feeds:
        key = feed["key"]
        folded_key = key.casefold()
        old_value = existing_sources.get(folded_key)
        if old_value is not None and Path(old_value).expanduser().resolve() != feed["path"]:
            raise ValueError(f"NuGet source key {key!r} already names a different source")
        if old_value is None:
            ET.SubElement(sources, _tag_like(sources, "add"), key=key, value=str(feed["path"]))
        source_mapping = mapping_by_key.get(folded_key)
        if source_mapping is None:
            source_mapping = ET.SubElement(mapping, _tag_like(mapping, "packageSource"), key=key)
            mapping_by_key[folded_key] = source_mapping
        existing_patterns = {node.get("pattern", "").casefold()
                             for node in _children(source_mapping, "package")}
        for package_id in feed["packageIds"]:
            folded_id = package_id.casefold()
            owner = exact_owner.get(folded_id)
            if owner is not None and owner != folded_key:
                raise ValueError(f"Exact package ID {package_id} is already mapped to another source")
            if folded_id not in existing_patterns:
                ET.SubElement(source_mapping, _tag_like(source_mapping, "package"), pattern=package_id)
                existing_patterns.add(folded_id)
            exact_owner[folded_id] = folded_key

    # Exact ID mappings take precedence over preserved NuGet '*' fallback mappings.
    # Reject any duplicate exact route; retain all original feed and mapping entries.
    for feed in feeds:
        for package_id in feed["packageIds"]:
            owners_now = [source.get("key", "").casefold()
                          for source in _children(mapping, "packageSource")
                          if any(node.get("pattern", "").casefold() == package_id.casefold()
                                 for node in _children(source, "package"))]
            if owners_now != [feed["key"].casefold()]:
                raise ValueError(f"Private package {package_id} is not mapped to exactly its private source: {owners_now}")

    destination_path.parent.mkdir(parents=True, exist_ok=True)
    tree = ET.ElementTree(deepcopy(root))
    if destination_path.exists():
        raise FileExistsError(f"Refusing to overwrite existing config: {destination_path}")
    tree.write(destination_path, encoding="utf-8", xml_declaration=True)
    return {"path": str(destination_path), "privatePackages": {
        package_id: {"source": feed["key"], "path": str(feed["path"])}
        for feed in feeds for package_id in feed["packageIds"]}}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root-config", required=True, type=Path)
    parser.add_argument("--private-feeds", required=True, type=Path,
                        help="JSON array of {key,path,packageIds}; IDs are exact package IDs")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)
    try:
        specs = json.loads(args.private_feeds.read_text(encoding="utf-8"))
        result = build_repository_config(args.root_config, args.output, specs)
        print(json.dumps(result, indent=2, sort_keys=True))
        return 0
    except Exception as error:
        print(f"NuGet config generation failed: {type(error).__name__}: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

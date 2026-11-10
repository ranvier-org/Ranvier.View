"""Select the Ranvier.View.Anime publication pair: both packages, at the project's version, and nothing else."""

import argparse
import importlib.util
from pathlib import Path
import re
import sys

IDS = ('Ranvier.View.Anime', 'Ranvier.View.Anime.Traced')


def load_selector():
    spec = importlib.util.spec_from_file_location('native_delivery_packages', Path(__file__).with_name('native_delivery_packages.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def project_version(project):
    versions = re.findall(r'<Version>([^<]+)</Version>', project.read_text())
    if len(versions) != 1:
        raise ValueError(f'Expected one <Version> in {project}')
    return versions[0].strip()


def select(directory, project):
    identity = load_selector().package_identity
    version = project_version(project)
    rows = [(path, identity(path)) for path in sorted(directory.glob('*.nupkg'))]
    by_id = {}
    for path, row in rows:
        key = row['id'].casefold()
        if key in by_id:
            raise ValueError(f'Duplicate package id: {row["id"]}')
        by_id[key] = (path, row)
    expected = [name.casefold() for name in IDS]
    if sorted(by_id) != sorted(expected):
        raise ValueError(f'Expected exactly {", ".join(IDS)}; found {", ".join(row["id"] for _, row in rows) or "nothing"}')
    for path, row in by_id.values():
        if row['version'] != version:
            raise ValueError(f'{row["id"]} is {row["version"]}; the project declares {version}')
    return [by_id[key][0] for key in expected]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args()
    try:
        paths = select(args.directory, args.project)
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1
    for path in paths:
        print(path)
    return 0


if __name__ == '__main__':
    sys.exit(main())

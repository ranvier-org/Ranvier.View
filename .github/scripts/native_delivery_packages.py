"""Select native-delivery artifacts by declared NuGet identity before publication."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
import zipfile


def package_identity(path):
    if path.is_symlink():
        raise ValueError(f'Package must be a regular artifact: {path}')
    with zipfile.ZipFile(path) as archive:
        manifests = [name for name in archive.namelist() if name.casefold().endswith('.nuspec')]
        if len(manifests) != 1:
            raise ValueError(f'Expected one nuspec: {path}')
        metadata = ET.fromstring(archive.read(manifests[0])).find('{*}metadata')
        if metadata is None:
            raise ValueError(f'Missing package metadata: {path}')
        ids = metadata.findall('{*}id')
        versions = metadata.findall('{*}version')
        if len(ids) != 1 or len(versions) != 1:
            raise ValueError(f'Ambiguous package identity: {path}')
        identity = (ids[0].text or '').strip()
        version = (versions[0].text or '').strip()
        if not re.fullmatch(r'[A-Za-z0-9_.-]+', identity) or not version:
            raise ValueError(f'Invalid package identity: {path}')
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return {'id': identity, 'version': version, 'file': path.name, 'sha256': digest.hexdigest()}


def select(directory, excluded_directory, require_view_cohort=False):
    policy = json.loads(Path(__file__).with_name('native-delivery-packages.json').read_text())
    required = policy['required_view_package_ids']
    external = policy['external_required_package_ids']
    excluded = {name.casefold() for name in policy['excluded_package_ids']}
    required_keys = {name.casefold() for name in required}
    acceptance_ids = external + required
    if policy['schema'] != 1 or len(set(name.casefold() for name in acceptance_ids)) != len(acceptance_ids):
        raise ValueError('Invalid native-delivery package policy')
    if excluded & {name.casefold() for name in acceptance_ids}:
        raise ValueError('Required packages cannot be excluded')
    directory = directory.resolve(strict=True)
    excluded_directory = excluded_directory.resolve()
    if directory == excluded_directory:
        raise ValueError('Excluded artifacts must leave the publication directory')
    packages = [(path, package_identity(path)) for path in sorted(directory.glob('*.nupkg'))]
    found = [row['id'].casefold() for _, row in packages]
    if len(found) != len(set(found)):
        raise ValueError('Duplicate package identity in CI artifacts')
    missing = required_keys - set(found)
    if missing and require_view_cohort:
        raise ValueError('Missing required native View packages: ' + ', '.join(sorted(missing)))
    removed = [(path, row) for path, row in packages if row['id'].casefold() in excluded]
    for path, _ in removed:
        if (excluded_directory / path.name).exists():
            raise ValueError(f'Excluded artifact destination already exists: {path.name}')
    report = {
        'selected': [row for _, row in packages if row['id'].casefold() not in excluded],
        'excluded': [row for _, row in removed],
        'required_acceptance_package_ids': acceptance_ids,
        'view_cohort_complete': not missing,
        'missing_required_view_package_ids': [name for name in required if name.casefold() in missing],
    }
    if removed:
        excluded_directory.mkdir(parents=True, exist_ok=True)
        for path, _ in removed:
            shutil.move(str(path), str(excluded_directory / path.name))
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--excluded-directory', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--require-view-cohort', action='store_true')
    args = parser.parse_args()
    report = select(args.directory, args.excluded_directory, args.require_view_cohort)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'selected': len(report['selected']), 'excluded': len(report['excluded']),
                      'requiredAcceptancePackages': len(report['required_acceptance_package_ids']),
                      'completeViewCohort': report['view_cohort_complete']}))


if __name__ == '__main__':
    main()

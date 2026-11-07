"""Print the ranvier-org/Ranvier commit CI builds the core source from: the later of the commits that built the
Ranvier.Dom package named by RanvierDomVersion and the Ranvier package named by RanvierVersion.

Each commit comes from the `repository` element nuget.org serves in the package's nuspec. The later commit contains
the source of both releases; commits on diverged histories raise.
"""
import argparse
import json
import os
from pathlib import Path
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'ranvier.dom'
CORE_PACKAGE = 'ranvier'
REPOSITORY = 'https://github.com/ranvier-org/Ranvier'
API = 'https://api.github.com/repos/ranvier-org/Ranvier'


def props_version(name: str, props: Path = ROOT / 'Directory.Build.props') -> str:
    found = re.search(rf'<{name}>([^<]+)</{name}>', props.read_text(encoding='utf-8'))
    if not found:
        raise ValueError(f'{props}: no {name}')
    return found.group(1).strip()


def dom_version(props: Path = ROOT / 'Directory.Build.props') -> str:
    return props_version('RanvierDomVersion', props)


def fetch(url: str) -> bytes:
    headers = {}
    if url.startswith(API):
        headers['Accept'] = 'application/vnd.github+json'
        token = os.environ.get('GH_TOKEN') or os.environ.get('GITHUB_TOKEN')
        if token:
            headers['Authorization'] = f'Bearer {token}'
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=60) as response:
        return response.read()


def source_commit(version: str, get=fetch, package: str = PACKAGE) -> str:
    nuspec = get(f'https://api.nuget.org/v3-flatcontainer/{package}/{version.lower()}/{package}.nuspec')
    repository = next((node for node in ET.fromstring(nuspec).iter() if node.tag.rsplit('}', 1)[-1] == 'repository'), None)
    if repository is None or repository.get('url', '').rstrip('/').removesuffix('.git') != REPOSITORY:
        raise ValueError(f'{package} {version}: the nuspec does not name {REPOSITORY}')
    commit = repository.get('commit', '')
    if not re.fullmatch('[0-9a-f]{40}', commit):
        raise ValueError(f'{package} {version}: the nuspec carries no full source commit')
    return commit


def later(first: str, second: str, get=fetch) -> str:
    """The commit of the two that contains the other; raises for diverged histories."""
    if first == second:
        return first
    status = json.loads(get(f'{API}/compare/{first}...{second}'))['status']
    if status in ('ahead', 'identical'):
        return second
    if status == 'behind':
        return first
    raise ValueError(f'{first} and {second} are on diverged histories ({status})')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', help='Ranvier.Dom version; defaults to RanvierDomVersion in Directory.Build.props.')
    parser.add_argument('--core-version', help='Ranvier version; defaults to RanvierVersion in Directory.Build.props.')
    args = parser.parse_args()
    dom = source_commit(args.version or dom_version())
    core = source_commit(args.core_version or props_version('RanvierVersion'), package=CORE_PACKAGE)
    print(later(dom, core))


if __name__ == '__main__':
    try:
        main()
    except ValueError as error:
        sys.exit(str(error))

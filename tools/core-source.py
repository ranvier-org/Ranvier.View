"""Print the ranvier-org/Ranvier commit that built the Ranvier.Dom package named by RanvierDomVersion.

The commit comes from the `repository` element nuget.org serves in the package's nuspec, so CI checks out the
Core source of exactly the DOM release the View builds against.
"""
import argparse
from pathlib import Path
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'ranvier.dom'
REPOSITORY = 'https://github.com/ranvier-org/Ranvier'


def dom_version(props: Path = ROOT / 'Directory.Build.props') -> str:
    found = re.search(r'<RanvierDomVersion>([^<]+)</RanvierDomVersion>', props.read_text(encoding='utf-8'))
    if not found:
        raise ValueError(f'{props}: no RanvierDomVersion')
    return found.group(1).strip()


def fetch(url: str) -> bytes:
    with urllib.request.urlopen(url, timeout=60) as response:
        return response.read()


def source_commit(version: str, get=fetch) -> str:
    nuspec = get(f'https://api.nuget.org/v3-flatcontainer/{PACKAGE}/{version.lower()}/{PACKAGE}.nuspec')
    repository = next((node for node in ET.fromstring(nuspec).iter() if node.tag.rsplit('}', 1)[-1] == 'repository'), None)
    if repository is None or repository.get('url', '').rstrip('/').removesuffix('.git') != REPOSITORY:
        raise ValueError(f'Ranvier.Dom {version}: the nuspec does not name {REPOSITORY}')
    commit = repository.get('commit', '')
    if not re.fullmatch('[0-9a-f]{40}', commit):
        raise ValueError(f'Ranvier.Dom {version}: the nuspec carries no full source commit')
    return commit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', help='Ranvier.Dom version; defaults to RanvierDomVersion in Directory.Build.props.')
    args = parser.parse_args()
    print(source_commit(args.version or dom_version()))


if __name__ == '__main__':
    try:
        main()
    except ValueError as error:
        sys.exit(str(error))

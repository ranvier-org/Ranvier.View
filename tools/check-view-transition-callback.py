"""Check that generated transition options marshal an application closure as a rooted callback."""
import argparse
from pathlib import Path
import subprocess
import tempfile


def check(generator, model, contracts, interop):
    with tempfile.TemporaryDirectory(prefix='ranvier-view-transition-callback-') as temporary:
        subprocess.run(['dotnet', str(generator), str(model), str(contracts), str(interop), temporary], check=True)
        source = (Path(temporary) / 'Objects.fs').read_text()
        mappings = [line for line in source.splitlines() if 'result.update <-' in line]
        assert mappings, 'Document.startViewTransition callback transport was not generated'
        assert all('scope.ViewTransitionCallback(this.Session, item)' in line for line in mappings), (
            'A ViewTransitionUpdateCallback is an application closure; generated options must root a native callback, '
            'rather than pass the closure to Interop.handle', mappings)
        assert all('result.update <- (match (item).update with Some item when not (isNull (box item)) -> Interop.handle' not in line
                   for line in mappings), 'Generated options treated a closure as a native object'
        print('PASS view-transition generated callback transport')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('generator', type=Path)
    parser.add_argument('model', type=Path)
    parser.add_argument('contracts', type=Path)
    parser.add_argument('interop', type=Path)
    arguments = parser.parse_args()
    check(arguments.generator, arguments.model, arguments.contracts, arguments.interop)

"""Admit the completed package-only JIT builds without rebuilding them."""
import hashlib
import json
from pathlib import Path
import zipfile

from support import sha


def admit_reuse(directory, config):
    pin = config['reuseJit']
    receipt, evidence = directory / 'receipt.json', directory / 'evidence.json'
    if sha(receipt) != pin['receiptSha256'] or sha(evidence) != pin['evidenceSha256']:
        raise ValueError('Original JIT receipt/evidence changed')
    record = json.loads(receipt.read_text())
    if (record['controllerCommit'] != pin['controllerCommit'] or record['publicViewSource'] != config['publicViewSource']
            or not all(record.get(n) for n in ['sourceUnchanged', 'inputsUnchanged', 'allChildrenReaped', 'noSourceProviderBuild', 'noEngineBuild'])):
        raise ValueError('Original JIT source/build scope is invalid')
    files = json.loads(evidence.read_text())['files']
    inputs = {str(receipt): sha(receipt), str(evidence): sha(evidence)}
    for name, row in files.items():
        path = (directory / name).resolve(strict=True)
        if not path.is_relative_to(directory.resolve()) or sha(path) != row['sha256']:
            raise ValueError('Original JIT artifact byte mismatch: ' + name)
        inputs[str(path)] = row['sha256']
    builds = {row['name']: row for row in record['commands']}
    for mode in ['normal', 'traced']:
        if builds['build-jit-' + mode]['exit'] != 0:
            raise ValueError('Original JIT build did not succeed')
        output = directory / ('run-jit-' + mode)
        actual = {p.relative_to(directory).as_posix() for p in output.rglob('*') if p.is_file()}
        bound = {n for n in files if n.startswith('run-jit-' + mode + '/')}
        if actual != bound or not (output / 'Ranvier.View.WebCore.Tests.dll').is_file():
            raise ValueError('Original JIT output closure missing/extra files')
    with zipfile.ZipFile(directory / 'controller-source.zip') as zipped:
        for name, digest in config['fixtureHashes'].items():
            if hashlib.sha256(zipped.read('tests/mac-public/' + name)).hexdigest() != digest:
                raise ValueError('Original JIT fixture differs')
    return {'origin': pin, 'verifiedFiles': len(inputs), 'jitCompilationRequested': False}, inputs

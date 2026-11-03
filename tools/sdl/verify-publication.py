import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import subprocess
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('sdl_candidate', ROOT/'tools/sdl/validate-candidate.py')
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)
spec = importlib.util.spec_from_file_location('sdl_qualification', ROOT/'tools/sdl/verify-qualification.py')
qualification = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qualification)


def validate_pins(pins):
    if pins.get('schemaVersion') != 1 or pins.get('id') != 'Ranvier.View.WebCore.Sdl.Ibus' or pins.get('version') != candidate.VERSION:
        raise ValueError('Unexpected publication identity')
    for key in ('candidateRun','qualificationRun','qualificationArtifact'):
        if type(pins.get(key)) is not int or pins[key] <= 0:
            raise ValueError('A reviewed run or artifact is required: '+key)
    for key, size in [('candidateHead',40),('packageSha256',64),('candidateReceiptSha256',64),('qualificationHead',40),('qualificationArtifactSha256',64),('qualificationReceiptSha256',64),('qualificationArchiveSha256',64)]:
        if not isinstance(pins.get(key),str) or not re.fullmatch('[0-9a-f]{'+str(size)+'}',pins[key]):
            raise ValueError('A reviewed exact pin is required: '+key)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def absent():
    url='https://api.nuget.org/v3-flatcontainer/ranvier.view.webcore.sdl.ibus/'+candidate.VERSION+'/ranvier.view.webcore.sdl.ibus.'+candidate.VERSION+'.nupkg'
    try:
        with urllib.request.urlopen(url,timeout=30):
            raise ValueError('Public version already exists')
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise


def validate_relay(run, artifacts, pins):
    if any((run.get('head_sha')!=pins['qualificationHead'],run.get('event')!='workflow_dispatch',run.get('path')!='.github/workflows/sdl-ibus-qualification-relay.yml',run.get('conclusion')!='success',run.get('head_branch')!='main')):
        raise ValueError('Qualification relay workflow identity or result differs')
    selected=[a for a in artifacts if a.get('name')=='sdl-ibus-local-qualified']
    if len(selected)!=1 or selected[0].get('id')!=pins['qualificationArtifact'] or selected[0].get('digest')!='sha256:'+pins['qualificationArtifactSha256'] or selected[0].get('expired') is not False:
        raise ValueError('Qualification relay artifact differs')


def validate_qualification_binding(data, checked, pins, receipt):
    if any(data.get(k)!=v for k,v in checked.items()) or data.get('relayRun')!=pins['qualificationRun'] or data.get('relayHead')!=pins['qualificationHead'] or data.get('relayWorkflow')!='ranvier-org/Ranvier.View/.github/workflows/sdl-ibus-qualification-relay.yml@refs/heads/main' or checked['packageSha256']!=pins['packageSha256'] or checked['candidateRun']!=pins['candidateRun'] or checked['candidateHead']!=pins['candidateHead'] or checked['sdlSha256']!=receipt['sdlSha256']:
        raise ValueError('Qualification does not bind the candidate')


def verify(work):
    pins=json.loads((ROOT/'tools/sdl/publication-pins.json').read_text())
    validate_pins(pins)
    run=json.loads(subprocess.check_output(['gh','api',f"repos/ranvier-org/Ranvier.View/actions/runs/{pins['candidateRun']}"],text=True))
    if any((run.get('head_sha') != pins['candidateHead'], run.get('event') != 'workflow_dispatch',
            run.get('path') != '.github/workflows/sdl-ibus-candidate.yml',run.get('conclusion') != 'success',
            run.get('head_branch') != 'main')):
        raise ValueError('Candidate workflow identity or result differs')
    bundle=work/'candidate'
    subprocess.run(['gh','run','download',str(pins['candidateRun']),'--repo','ranvier-org/Ranvier.View','--name','sdl-ibus-candidate','--dir',str(bundle)],check=True)
    package=bundle/candidate.PACKAGE
    receipt_path=bundle/'candidate.json'
    if sha(package)!=pins['packageSha256'] or sha(receipt_path)!=pins['candidateReceiptSha256']:
        raise ValueError('Candidate package or receipt differs from reviewed pins')
    receipt=json.loads(receipt_path.read_text())
    if receipt.get('viewSource')!=pins['candidateHead'] or receipt.get('sdlRevision')!=candidate.SDL_REVISION or receipt.get('sourceGitBlobs')!=candidate.SOURCE_BLOBS:
        raise ValueError('Candidate source contract differs')
    for name, expected in candidate.SOURCE_BLOBS.items():
        blob=subprocess.check_output(['git','hash-object',str(ROOT/name)],text=True).strip()
        if blob!=expected:
            raise ValueError('Current SDL source differs: '+name)
    binary=bundle/'libSDL3.so'
    canonical=work/'canonical-source'
    canonical.mkdir()
    for name in ('Ranvier.View.WebCore.Sdl.Ibus.targets','SDL-LICENSE.txt'):
        (canonical/name).write_bytes(subprocess.check_output(['git','show','HEAD:tools/sdl/package/'+name],cwd=ROOT))
    checked=candidate.validate(package,binary,canonical/'Ranvier.View.WebCore.Sdl.Ibus.targets',canonical/'SDL-LICENSE.txt')
    if any(receipt.get(k)!=v for k,v in checked.items()):
        raise ValueError('Candidate payload receipt differs')
    header,cache=bundle/'SDL_build_config.h',bundle/'CMakeCache.txt'
    candidate.validate_config(header.read_text(),cache.read_text())
    if sha(header)!=receipt.get('configSha256') or sha(cache)!=receipt.get('cacheSha256'):
        raise ValueError('Candidate generated configuration differs')
    relay=json.loads(subprocess.check_output(['gh','api',f"repos/ranvier-org/Ranvier.View/actions/runs/{pins['qualificationRun']}"],text=True))
    artifacts=json.loads(subprocess.check_output(['gh','api',f"repos/ranvier-org/Ranvier.View/actions/runs/{pins['qualificationRun']}/artifacts"],text=True))['artifacts']
    validate_relay(relay,artifacts,pins)
    relayed=work/'qualification'
    subprocess.run(['gh','run','download',str(pins['qualificationRun']),'--repo','ranvier-org/Ranvier.View','--name','sdl-ibus-local-qualified','--dir',str(relayed)],check=True)
    proof=relayed/'candidate-mozc-qualification.zip'
    relay_receipt=relayed/'relay-receipt.json'
    if sha(proof)!=pins['qualificationArchiveSha256'] or sha(relay_receipt)!=pins['qualificationReceiptSha256']:
        raise ValueError('Qualification proof or relay receipt differs')
    checked_proof=qualification.verify(proof)
    relay_data=json.loads(relay_receipt.read_text())
    validate_qualification_binding(relay_data,checked_proof,pins,receipt)
    absent()
    export=work/'export'
    export.mkdir(parents=True,exist_ok=False)
    shutil.copy2(package,export/package.name)
    (work/'verification.json').write_text(json.dumps({'candidateRun':pins['candidateRun'],'candidateHead':pins['candidateHead'],'packageSha256':sha(package),'candidateReceiptSha256':sha(receipt_path),'qualificationRun':pins['qualificationRun'],'qualificationHead':pins['qualificationHead'],'qualificationArchiveSha256':sha(proof),'qualificationReceiptSha256':sha(relay_receipt),'qualificationProofKind':checked_proof['proofKind'],'testsExecutedByRelay':False},indent=2)+'\n')
    print('Verified unchanged SDL candidate before authentication')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--work',type=Path,required=True)
    verify(parser.parse_args().work)

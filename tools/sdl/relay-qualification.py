import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('sdl_qualification',ROOT/'tools/sdl/verify-qualification.py')
qualification=importlib.util.module_from_spec(spec);spec.loader.exec_module(qualification)

def validate_release(release,pins):
    if release.get('id')!=pins['draftRelease'] or release.get('draft') is not True or release.get('tag_name')!=pins['tag'] or release.get('target_commitish')!=pins['targetCommit'] or release.get('author',{}).get('login')!=pins['author']:
        raise ValueError('Scratch draft identity differs')
    assets=release.get('assets',[])
    if len(assets)!=1: raise ValueError('Scratch draft asset set differs')
    asset=assets[0]
    if asset.get('id')!=pins['asset'] or asset.get('name')!=pins['assetName'] or asset.get('size')!=pins['assetBytes'] or asset.get('digest')!='sha256:'+pins['assetSha256'] or asset.get('uploader',{}).get('login')!=pins['uploader'] or asset.get('state')!='uploaded':
        raise ValueError('Scratch draft asset identity differs')

def receive(work):
    pins=json.loads((ROOT/'tools/sdl/qualification-relay-pins.json').read_text())
    if pins.get('schemaVersion')!=1 or pins.get('assetSha256')!=qualification.ARCHIVE_SHA256:
        raise ValueError('Relay pins differ')
    if os.environ.get('GITHUB_EVENT_NAME')!='workflow_dispatch' or os.environ.get('GITHUB_REF')!='refs/heads/main' or os.environ.get('GITHUB_WORKFLOW_REF')!='ranvier-org/Ranvier.View/.github/workflows/sdl-ibus-qualification-relay.yml@refs/heads/main':
        raise ValueError('Relay must be the reviewed main-only manual workflow')
    release=json.loads(subprocess.check_output(['gh','api',f"repos/ranvier-org/Ranvier.View/releases/{pins['draftRelease']}"],text=True))
    validate_release(release,pins)
    work.mkdir(parents=True,exist_ok=True)
    archive=work/pins['assetName']
    with archive.open('wb') as out:
        subprocess.run(['gh','api','-H','Accept: application/octet-stream',f"repos/ranvier-org/Ranvier.View/releases/assets/{pins['asset']}"],stdout=out,check=True)
    if archive.stat().st_size!=pins['assetBytes']:
        raise ValueError('Qualification archive size differs')
    result=qualification.verify(archive)
    result.update(relayRun=int(os.environ['GITHUB_RUN_ID']),relayHead=os.environ['GITHUB_SHA'],relayWorkflow=os.environ['GITHUB_WORKFLOW_REF'],draftRelease=pins['draftRelease'],asset=pins['asset'])
    (work/'relay-receipt.json').write_text(json.dumps(result,indent=2)+'\n')
    print('Relayed verified local qualification; no tests executed')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--work',type=Path,required=True);receive(parser.parse_args().work)

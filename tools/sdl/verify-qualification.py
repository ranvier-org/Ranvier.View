import hashlib
import json
from pathlib import Path
import zipfile

ARCHIVE_SHA256='3dedd15d84b545ac2126f753121c202f7b681f0a34cfc3dbd0ca23dbb1497960'
PACKAGE_SHA256='7923c9f66b6a0e65991e44ac353ec61cb39f03139e0adc69f79b6e5059d41de6'
SDL_SHA256='107928bd9421b71a2d5560a11b891c448f791a2ca6dd3a92144ec8dea2715482'
HOST_SHA256='b5e96388c503c78096f1ad1172dda44aa0cc7817000f5524ca30af752bd15059'
LAUNCHER_SHA256='ce685e74e03abb6102db38cf156fc3a50dd83f29dbbc825cce332398ce440f89'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def verify(path):
    if sha(path.read_bytes()) != ARCHIVE_SHA256:
        raise ValueError('Qualification archive differs from independently reviewed bytes')
    with zipfile.ZipFile(path) as z:
        names=z.namelist()
        if len(names)!=len(set(names)):
            raise ValueError('Duplicate qualification members')
        manifest=json.loads(z.read('manifest.json'))
        if manifest.get('candidatePackageSha256')!=PACKAGE_SHA256 or manifest.get('sdlSha256')!=SDL_SHA256:
            raise ValueError('Qualification candidate identity differs')
        if manifest.get('cases')!=['normal-commit','normal-cancel','traced-commit','traced-cancel']:
            raise ValueError('Qualification cases differ')
        rows=manifest['files']
        if len(rows)!=67 or set(names)!={x['member'] for x in rows}|{'manifest.json'}:
            raise ValueError('Qualification member set differs')
        by_source={}
        for row in rows:
            key=row['source'].replace('\\','/')
            if key in by_source or sha(z.read(row['member']))!=row['sha256']:
                raise ValueError('Qualification member changed or source duplicated')
            by_source[key]=row['member']
        def read(source):
            return z.read(by_source[source])
        if sha(read('ci-candidate/Ranvier.View.WebCore.Sdl.Ibus.0.1.0-alpha.1.nupkg'))!=PACKAGE_SHA256:
            raise ValueError('Qualified package differs')
        producer=json.loads(read('candidate-download.json'))
        candidate=json.loads(read('ci-candidate/candidate.json'))
        if producer.get('run')!=41679434452 or producer.get('head')!='7dab7468a042a7a809270b808e29f4683a02dff8' or producer.get('packageSha256')!=PACKAGE_SHA256 or producer.get('binarySha256')!=SDL_SHA256:
            raise ValueError('Qualification producer identity differs')
        if sha(read('ci-candidate/candidate.json'))!=producer.get('candidateReceiptSha256') or candidate.get('viewSource')!=producer['head'] or candidate.get('packageSha256')!=PACKAGE_SHA256 or candidate.get('sdlSha256')!=SDL_SHA256:
            raise ValueError('Qualification candidate receipt differs')
        if sha(read('launcher.py'))!=LAUNCHER_SHA256:
            raise ValueError('Qualified launcher differs')
        for mode in ('normal','traced'):
            for case in ('commit','cancel'):
                prefix=f'consumer-{mode}-{case}'
                log=read(prefix+'.log').decode('utf8')
                receipt=json.loads(read(prefix+'-receipt.json'))
                markers=['MOZC_POPUP_IDENTIFIED','MOZC_POPUP_PIXELS before','MOZC_POPUP_PIXELS after','MOZC_SELECTION_VISIBLE','SCROLL_CLICK','SCROLLED_CARET_MATCH','SURROUNDING_AVAILABLE true','SURROUNDING_AVAILABLE false']
                markers += ['MOZC_SELECTED_COMMIT' if case=='commit' else 'MOZC_CANCELLATION_RESTORED']
                if not all(x in log for x in markers):
                    raise ValueError('Missing real Mozc evidence: '+prefix)
                if receipt.get('mode')!='consumer-'+mode or receipt.get('case')!=case or receipt.get('exit')!=0 or receipt.get('stageUnchanged') is not True or receipt.get('reapedAfterLauncher')!=[]:
                    raise ValueError('Qualification outcome differs: '+prefix)
                if receipt.get('hostSha256')!=HOST_SHA256 or receipt.get('sdlSha256')!=SDL_SHA256 or receipt.get('launcherSha256')!=LAUNCHER_SHA256 or receipt.get('logSha256')!=sha(log.encode('utf8')):
                    raise ValueError('Qualification runtime identity differs: '+prefix)
                session=f'final-sessions/{mode}-{case}/'
                monitor=read(session+'monitor.log').decode('utf8',errors='replace')
                if 'member=SetSurroundingText' not in monitor or 'private-secret-171' in monitor or (case=='commit' and 'member=CommitText' not in monitor):
                    raise ValueError('Surrounding, privacy or commit evidence differs: '+prefix)
                before,after=read(session+'candidate-before.ppm'),read(session+'candidate-after.ppm')
                if not before.startswith(b'P6\n') or not after.startswith(b'P6\n') or before==after:
                    raise ValueError('Candidate popup pixel evidence differs: '+prefix)
                for kind in ('host','sdl'):
                    if f'consumer-{mode}/bin/Release/net10.0/linux-x64/' not in read(session+kind+'-maps.log').decode('utf8'):
                        raise ValueError('Runtime mapped path differs: '+prefix)
    return {'proofKind':'local-qualified-relayed','testsExecutedByRelay':False,'qualificationArchiveSha256':ARCHIVE_SHA256,'candidateRun':41679434452,'candidateHead':'7dab7468a042a7a809270b808e29f4683a02dff8','packageSha256':PACKAGE_SHA256,'sdlSha256':SDL_SHA256,'hostSha256':HOST_SHA256,'cases':['normal-commit','normal-cancel','traced-commit','traced-cancel'],'scope':'locally executed Mozc evidence; relay does not execute tests or authenticate source provenance'}

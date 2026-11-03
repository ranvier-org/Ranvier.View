import hashlib,importlib.util,json,tempfile,unittest,zipfile
from pathlib import Path
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('qualification',ROOT/'tools/sdl/verify-qualification.py')
q=importlib.util.module_from_spec(spec);spec.loader.exec_module(q)

class QualificationTests(unittest.TestCase):
    def fixture(self, path, failure=None):
        package=b'candidate';launcher=b'launcher';package_sha=q.sha(package);launcher_sha=q.sha(launcher)
        candidate={'viewSource':'7dab7468a042a7a809270b808e29f4683a02dff8','packageSha256':package_sha,'sdlSha256':q.SDL_SHA256}
        candidate_bytes=json.dumps(candidate).encode()
        producer={'run':41679434452,'head':candidate['viewSource'],'packageSha256':package_sha,'binarySha256':q.SDL_SHA256,'candidateReceiptSha256':q.sha(candidate_bytes)}
        if failure=='head': producer['head']='0'*40
        sources={'launcher.py':launcher,'candidate-download.json':json.dumps(producer).encode(),'ci-candidate/candidate.json':candidate_bytes,'ci-candidate/Ranvier.View.WebCore.Sdl.Ibus.0.1.0-alpha.1.nupkg':package}
        for mode in ('normal','traced'):
            for case in ('commit','cancel'):
                prefix=f'consumer-{mode}-{case}'
                markers=['MOZC_POPUP_IDENTIFIED','MOZC_POPUP_PIXELS before','MOZC_POPUP_PIXELS after','MOZC_SELECTION_VISIBLE','SCROLL_CLICK','SCROLLED_CARET_MATCH','SURROUNDING_AVAILABLE true','SURROUNDING_AVAILABLE false','MOZC_SELECTED_COMMIT' if case=='commit' else 'MOZC_CANCELLATION_RESTORED']
                if failure=='popup' and mode=='normal' and case=='commit': markers.remove('MOZC_POPUP_IDENTIFIED')
                log='\n'.join(markers).encode()
                receipt={'mode':'consumer-'+mode,'case':case,'exit':1 if failure=='exit' and mode=='normal' and case=='commit' else 0,'stageUnchanged':True,'reapedAfterLauncher':[],'hostSha256':q.HOST_SHA256,'sdlSha256':q.SDL_SHA256,'launcherSha256':launcher_sha,'logSha256':q.sha(log)}
                sources[prefix+'.log']=log;sources[prefix+'-receipt.json']=json.dumps(receipt).encode()
                base=f'final-sessions/{mode}-{case}/'
                sources[base+'monitor.log']=b'member=SetSurroundingText member=CommitText'
                sources[base+'candidate-before.ppm']=b'P6\nbefore';sources[base+'candidate-after.ppm']=b'P6\nafter'
                for kind in ('host','sdl'): sources[base+kind+'-maps.log']=f'consumer-{mode}/bin/Release/net10.0/linux-x64/'.encode()
        while len(sources)<67: sources[f'filler/{len(sources)}']=b'filler'
        rows=[]
        with zipfile.ZipFile(path,'w') as z:
            for i,(source,data) in enumerate(sources.items()):
                name=f'{i:03d}-data';z.writestr(name,data);rows.append({'source':source,'member':name,'sha256':q.sha(data)})
            if failure=='duplicate-source': rows[-1]['source']=rows[0]['source']
            manifest={'candidatePackageSha256':package_sha,'sdlSha256':q.SDL_SHA256,'cases':['normal-commit','normal-cancel','traced-commit','traced-cancel'],'files':rows}
            z.writestr('manifest.json',json.dumps(manifest))
            if failure=='duplicate-member': z.writestr(rows[0]['member'],b'duplicate')
        return package_sha,launcher_sha
    def test_valid_and_rehashed_internal_negatives(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'proof.zip'
            for failure in (None,'popup','exit','head','duplicate-source','duplicate-member'):
                package,launcher=self.fixture(p,failure)
                with patch.object(q,'ARCHIVE_SHA256',hashlib.sha256(p.read_bytes()).hexdigest()),patch.object(q,'PACKAGE_SHA256',package),patch.object(q,'LAUNCHER_SHA256',launcher):
                    if failure is None: self.assertFalse(q.verify(p)['testsExecutedByRelay'])
                    else:
                        with self.subTest(failure=failure),self.assertRaises(ValueError): q.verify(p)

if __name__=='__main__': unittest.main()

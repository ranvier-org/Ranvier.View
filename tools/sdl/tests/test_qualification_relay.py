import importlib.util,json,os,unittest
from pathlib import Path
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('relay',ROOT/'tools/sdl/relay-qualification.py')
relay=importlib.util.module_from_spec(spec);spec.loader.exec_module(relay)

class RelayTests(unittest.TestCase):
    def test_exact_draft_and_asset(self):
        p=json.loads((ROOT/'tools/sdl/qualification-relay-pins.json').read_text())
        a={'id':p['asset'],'name':p['assetName'],'size':p['assetBytes'],'digest':'sha256:'+p['assetSha256'],'uploader':{'login':p['uploader']},'state':'uploaded'}
        r={'id':p['draftRelease'],'draft':True,'tag_name':p['tag'],'target_commitish':p['targetCommit'],'author':{'login':p['author']},'assets':[a]}
        relay.validate_release(r,p)
        for key,value in [('draft',False),('target_commitish','wrong'),('assets',[])]:
            with self.subTest(key=key),self.assertRaises(ValueError): relay.validate_release(dict(r,**{key:value}),p)
        for key,value in [('digest','sha256:'+'0'*64),('uploader',{'login':'other'}),('size',1),('state','new')]:
            with self.subTest(key=key),self.assertRaises(ValueError): relay.validate_release(dict(r,assets=[dict(a,**{key:value})]),p)

    def test_manual_main_workflow_only(self):
        text=(ROOT/'.github/workflows/sdl-ibus-qualification-relay.yml').read_text()
        self.assertNotIn('if: ${{ false &&',text)
        self.assertIn("github.event_name == 'workflow_dispatch'",text)
        self.assertIn("github.ref == 'refs/heads/main'",text)
        self.assertNotIn('id-token:',text.lower())
        self.assertNotIn('nuget push',text.lower())
        self.assertNotIn('cmake',text.lower())

if __name__=='__main__':unittest.main()

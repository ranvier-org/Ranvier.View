import importlib.util
import json
from pathlib import Path
import re
import unittest
import urllib.error
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('publication',ROOT/'tools/sdl/verify-publication.py')
publication=importlib.util.module_from_spec(spec)
spec.loader.exec_module(publication)


class PublicationTests(unittest.TestCase):
    def test_incomplete_pins_fail_and_exact_pins_validate(self):
        pins=json.loads((ROOT/'tools/sdl/publication-pins.json').read_text())
        publication.validate_pins(pins)
        with self.assertRaises(ValueError): publication.validate_pins(dict(pins,qualificationRun=None))
        valid=dict(pins,candidateRun=1,candidateHead='a'*40,packageSha256='b'*64,candidateReceiptSha256='c'*64)
        publication.validate_pins(valid)
        for field,value in [('candidateRun',True),('candidateHead','A'*40),('packageSha256',None),('version','0.1.0-alpha.2'),('qualificationRun',None),('qualificationHead','A'*40),('qualificationArtifact',True),('qualificationArtifactSha256',None),('qualificationReceiptSha256',None),('qualificationArchiveSha256',None)]:
            with self.subTest(field=field),self.assertRaises(ValueError): publication.validate_pins(dict(valid,**{field:value}))

    def test_public_absence_requires_exact_404(self):
        error=urllib.error.HTTPError('test',404,'absent',{},None)
        with patch.object(publication.urllib.request,'urlopen',side_effect=error) as request:
            publication.absent()
            self.assertIn('/0.1.0-alpha.1/',request.call_args.args[0])
        with patch.object(publication.urllib.request,'urlopen'),self.assertRaises(ValueError): publication.absent()
        with patch.object(publication.urllib.request,'urlopen',side_effect=urllib.error.HTTPError('test',500,'error',{},None)),self.assertRaises(urllib.error.HTTPError): publication.absent()

    def test_relay_and_candidate_binding_reject_changes(self):
        p=json.loads((ROOT/'tools/sdl/publication-pins.json').read_text())
        run={'head_sha':p['qualificationHead'],'event':'workflow_dispatch','path':'.github/workflows/sdl-ibus-qualification-relay.yml','conclusion':'success','head_branch':'main'}
        artifact={'name':'sdl-ibus-local-qualified','id':p['qualificationArtifact'],'digest':'sha256:'+p['qualificationArtifactSha256'],'expired':False}
        publication.validate_relay(run,[artifact],p)
        for field,value in [('head_sha','0'*40),('event','push'),('path','wrong'),('conclusion','failure'),('head_branch','other')]:
            with self.subTest(field=field),self.assertRaises(ValueError): publication.validate_relay(dict(run,**{field:value}),[artifact],p)
        for changed in ([],[artifact,artifact],[dict(artifact,id=1)],[dict(artifact,digest='sha256:'+'0'*64)],[dict(artifact,expired=True)]):
            with self.subTest(artifacts=changed),self.assertRaises(ValueError): publication.validate_relay(run,changed,p)
        checked={'proofKind':'local-qualified-relayed','testsExecutedByRelay':False,'packageSha256':p['packageSha256'],'candidateRun':p['candidateRun'],'candidateHead':p['candidateHead'],'sdlSha256':'a'*64}
        data=dict(checked,relayRun=p['qualificationRun'],relayHead=p['qualificationHead'],relayWorkflow='ranvier-org/Ranvier.View/.github/workflows/sdl-ibus-qualification-relay.yml@refs/heads/main')
        receipt={'sdlSha256':'a'*64}
        publication.validate_qualification_binding(data,checked,p,receipt)
        for field,value in [('testsExecutedByRelay',True),('relayRun',1),('relayHead','wrong'),('relayWorkflow','wrong')]:
            with self.subTest(field=field),self.assertRaises(ValueError): publication.validate_qualification_binding(dict(data,**{field:value}),checked,p,receipt)
        for field,value in [('packageSha256','0'*64),('candidateRun',1),('candidateHead','wrong'),('sdlSha256','0'*64)]:
            changed=dict(checked,**{field:value})
            with self.subTest(field=field),self.assertRaises(ValueError): publication.validate_qualification_binding(dict(data,**{field:value}),changed,p,receipt)

    def test_main_manual_optional_route_verifies_before_login(self):
        workflow=(ROOT/'.github/workflows/publish.yml').read_text()
        job=workflow.split('  publish-sdl-ibus:',1)[1].split('\n  publish:',1)[0]
        self.assertNotIn('if: ${{ false &&',job)
        self.assertIn("github.event_name == 'workflow_dispatch'",job)
        self.assertIn("github.ref == 'refs/heads/main'",job)
        self.assertIn("inputs.sdl_ibus == 'publish'",job)
        self.assertLess(job.index('verify-publication.py'),job.index('NuGet/login@v1'))
        self.assertLess(job.index('NuGet/login@v1'),job.index('dotnet nuget push'))
        self.assertNotIn('--skip-duplicate',job)
        self.assertNotIn('--require-view-cohort',job)

    def test_manual_sdl_choice_cannot_enter_existing_publication_jobs(self):
        workflow=(ROOT/'.github/workflows/publish.yml').read_text()
        jobs=workflow.split('jobs:',1)[1]
        names=re.findall(r'^  ([a-z][a-z0-9-]*):$',jobs,re.M)
        self.assertEqual(set(names),{'publish-sdl-ibus','publish','publish-analyzers','qualify-public-analyzers'})
        for name, required in [('publish',"github.event.workflow_run.name == 'CI'"),('publish-analyzers',"github.event.workflow_run.name == 'Analyzer host qualification'")]:
            block=re.split(r'\n  [a-z][a-z0-9-]*:\s*\n',jobs.split('\n  '+name+':',1)[1],maxsplit=1)[0]
            self.assertIn(required,block)
            self.assertIn("github.event.workflow_run.event == 'push'",block)
        dependent=jobs.split('\n  qualify-public-analyzers:',1)[1]
        self.assertIn('needs: publish-analyzers',dependent)


if __name__=='__main__': unittest.main()

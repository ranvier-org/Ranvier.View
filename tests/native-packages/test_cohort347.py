"""Hermetic policy/restore fixtures; these tests do not execute a renderer."""
from copy import deepcopy
import hashlib
import importlib.util
import inspect
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'tools/native-cohort347/verify.py'
if SCRIPT.exists():
    spec = importlib.util.spec_from_file_location('cohort347', SCRIPT)
    gate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gate)
else:
    gate = None


def cohort():
    return dict(schemaVersion=1, armed=True, managedSource='1'*40, nativeSource='2'*40,
                providerSource='3'*40, webkitSource='4'*40,
                versions=dict(view='0.1.99', backend='0.1.0-alpha.99', native='0.1.1-preview.99', provider='0.1.1-preview.99',
                              core='0.1.1-preview.1', dom='0.1.0-alpha.56', css='0.1.1-alpha.2', cssAddons='0.1.1-alpha.1'),
                rids={'win-x64':dict(hostSha256='a'*64), 'linux-x64':dict(hostSha256='b'*64,providerSha256='c'*64),
                      'linux-arm64':dict(hostSha256='d'*64,providerSha256='e'*64)})


class CohortPolicyTests(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(gate, 'The cohort verifier is not implemented.')

    def test_unarmed_checked_in_inputs_fail_before_restore(self):
        value=json.loads((ROOT/'tools/native-cohort347/inputs/unarmed.json').read_text())
        with self.assertRaisesRegex(ValueError,'unarmed'):
            gate.load_cohort(value)

    def test_exact_public_inputs_are_accepted_without_allocating_them(self):
        self.assertEqual(gate.load_cohort(cohort()),cohort())

    def test_partial_or_private_inputs_are_rejected(self):
        for field,bad in [('nativeSource','da11'),('managedSource',None),('providerSource','f'*39),('webkitSource','HEAD')]:
            value=cohort(); value[field]=bad
            with self.subTest(field=field),self.assertRaises(ValueError): gate.load_cohort(value)
        for bad in [None,'','0.0.0-private347.1','0.1.0-local.5','0.0.0-ci.123','1.2','1.2.3;echo']:
            value=cohort(); value['versions']['native']=bad
            with self.subTest(version=bad),self.assertRaises(ValueError): gate.load_cohort(value)

    def test_provider_version_and_public_dependency_minimums_cannot_drift(self):
        for key,bad in [('provider','0.1.1-preview.98'),('core','0.1.1-preview.2'),('dom','0.1.0-alpha.55'),('css','0.1.1-alpha.1'),('cssAddons','0.1.1-alpha.2')]:
            value=cohort(); value['versions'][key]=bad
            with self.subTest(key=key),self.assertRaises(ValueError): gate.load_cohort(value)

    def test_arm_requires_its_own_hashes_and_rid_entry(self):
        for field in ['hostSha256','providerSha256']:
            value=cohort(); value['rids']['linux-arm64'][field]=None
            with self.subTest(field=field),self.assertRaises(ValueError): gate.load_cohort(value)
        value=cohort(); del value['rids']['linux-arm64']
        with self.assertRaises(ValueError): gate.load_cohort(value)

    def test_binary_architecture_is_checked_independently_of_manifest_rid(self):
        elf=bytearray(64); elf[:6]=b'\x7fELF\x02\x01'; elf[18:20]=(183).to_bytes(2,'little')
        gate.verify_architecture(bytes(elf),'linux-arm64')
        with self.assertRaises(ValueError): gate.verify_architecture(bytes(elf),'linux-x64')
        pe=bytearray(128); pe[:2]=b'MZ'; pe[60:64]=(64).to_bytes(4,'little'); pe[64:68]=b'PE\0\0'; pe[68:70]=(0x8664).to_bytes(2,'little')
        gate.verify_architecture(bytes(pe),'win-x64')
        with self.assertRaises(ValueError): gate.verify_architecture(bytes(pe),'linux-x64')
        with self.assertRaises(ValueError): gate.verify_architecture(b'MZ','win-x64')

    def test_any_altered_full_signature_header_fails(self):
        with tempfile.TemporaryDirectory() as temporary:
            path=Path(temporary)/'header.h'; path.write_text('RV_EXPORT int rv_host_ime_surrounding(void);')
            with self.assertRaisesRegex(ValueError,'signature'): gate.verify_header(path)

    def test_workflow_cannot_arm_incomplete_or_arm_inputs_by_dispatch(self):
        path=ROOT/'.github/workflows/native-cohort347.yml'
        self.assertTrue(path.exists(),'The statically unarmed consumer workflow is missing.')
        text=path.read_text()
        self.assertIn('if: ${{ false }}',text)
        self.assertIn('linux-arm64',text)
        self.assertIn('mode: [normal, traced]',text)
        self.assertIn('rid: [win-x64, linux-x64]',text)
        self.assertIn('rid: linux-arm64',text)
        self.assertEqual(text.count('if: ${{ false }}'),2)
        self.assertNotIn('NuGet/login',text)
        self.assertNotIn('dotnet nuget push',text)
        self.assertIn('test_cohort347.py',text)

    def test_arm_gap_does_not_arm_arm_or_block_independent_x64_guards(self):
        self.assertIn('rid',inspect.signature(gate.load_cohort).parameters,'Selected-RID validation is missing.')
        value=cohort(); value['rids']['linux-arm64']['providerSha256']=None
        self.assertEqual(gate.load_cohort(value,'win-x64'),value)
        self.assertEqual(gate.load_cohort(value,'linux-x64'),value)
        with self.assertRaises(ValueError): gate.load_cohort(value,'linux-arm64')
        value['versions']['provider']=None
        self.assertEqual(gate.load_cohort(value,'win-x64'),value)
        with self.assertRaises(ValueError): gate.load_cohort(value,'linux-x64')

    def test_consumer_uses_public_sdl_without_direct_higher_core_or_interop_override(self):
        path=ROOT/'tests/native-cohort347/Consumer.fsproj'
        self.assertTrue(path.exists(),'The isolated public consumer is missing.')
        tree=ET.parse(path).getroot()
        self.assertEqual(tree.findall('.//ProjectReference'),[])
        packages=[node.get('Include') for node in tree.findall('.//PackageReference') if node.get('Include')]
        self.assertEqual(set(packages),{'Ranvier.View.WebCore.Sdl$(CohortSuffix)',gate.PROVIDER})
        props=(path.parent/'Directory.Build.props').read_text()
        self.assertIn('<TreatWarningsAsErrors>true</TreatWarningsAsErrors>',props)
        self.assertIn('IL3050',props)
        self.assertNotIn('NoWarn',props)

    def test_runtime_summary_requires_the_exact_loaded_linux_paths(self):
        self.assertTrue(callable(getattr(gate,'verify_runtime_result',None)), 'Runtime result checks are missing.')
        text=json.dumps(dict(windows=3,directHosts=3,expectedAttached=True,dynamicCodeSupported=False))
        with self.assertRaisesRegex(ValueError,'loaded'):
            gate.verify_runtime_result(text,Path('/owned/publish'),True,True,True,True)
        maps='\nnative-map 00-01 r-xp 00000000 00:00 1 /owned/publish/libRanvierWebCoreHost.so\nnative-map 00-01 r-xp 00000000 00:00 2 /owned/publish/libRanvierWebCoreAtspi.so'
        self.assertEqual(gate.verify_runtime_result(text+maps,Path('/owned/publish'),True,True,True,True)['windows'],3)
        with self.assertRaises(ValueError): gate.verify_runtime_result(text+maps,Path('/other/publish'),True,True,True,True)

    def test_control_cleanup_attempts_every_owned_child_after_one_failure(self):
        path=ROOT/'tools/native-cohort347/linux_control.py'
        spec=importlib.util.spec_from_file_location('linux_control_test',path)
        module=importlib.util.module_from_spec(spec)
        with patch.dict(sys.modules,{'verify':gate}): spec.loader.exec_module(module)
        self.assertTrue(callable(getattr(module,'cleanup_children',None)),'Complete child cleanup is missing.')
        class Child:
            def __init__(self,pid): self.pid=pid
            def poll(self): return None
        first,second=Child(1),Child(2)
        with patch.object(module.supervision,'terminate_owned_process_tree',side_effect=[RuntimeError('first cleanup failed'),None]) as terminate:
            failures=module.cleanup_children([first,second])
        self.assertEqual(terminate.call_count,2)
        self.assertEqual(failures,[dict(pid=2,error='first cleanup failed')])


class PublicGraphTests(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(gate,'The cohort verifier is not implemented.')
        self.temporary=tempfile.TemporaryDirectory(); self.addCleanup(self.temporary.cleanup)
        self.cache=Path(self.temporary.name)

    def graph(self,traced=False,rid='linux-x64'):
        value=cohort(); versions=value['versions']; suffix='.Traced' if traced else ''
        selected={}; libraries={}
        def package(name,version,source='https://api.nuget.org/v3/index.json',commit=None,dependencies=None):
            key=name+'/'+version; relative=name.lower()+'/'+version
            selected[key]={'dependencies':dependencies or {}}; libraries[key]={'type':'package','path':relative}
            directory=self.cache/relative; directory.mkdir(parents=True,exist_ok=True)
            (directory/'.nupkg.metadata').write_text(json.dumps(dict(source=source)))
            (directory/(name.lower()+'.nuspec')).write_text('<package><metadata><repository commit="'+(commit or value['managedSource'])+'" /></metadata></package>')
        for name in ['Ranvier.View','Ranvier.View.Dom','Ranvier.View.WebCore','Ranvier.View.WebCore.Sdl']:
            deps={}
            if name=='Ranvier.View.Dom': deps['Ranvier.Dom'+suffix]=versions['dom']
            if name=='Ranvier.View.WebCore': deps['Ranvier.WebCore']=versions['native']
            package(name+suffix,versions['backend'] if '.WebCore' in name else versions['view'],dependencies=deps)
        package('Ranvier'+suffix,versions['core'])
        package('Ranvier.Dom'+suffix,versions['dom'])
        package('Ranvier.WebCore',versions['native'],commit=value['nativeSource'])
        package('Ranvier.WebCore.Native',versions['native'],dependencies={'Ranvier.WebCore.Native.'+r:versions['native'] for r in gate.ALL_NATIVE_RIDS})
        for r in gate.ALL_NATIVE_RIDS: package('Ranvier.WebCore.Native.'+r,versions['native'])
        return dict(libraries=libraries,packageFolders={str(self.cache):{}},targets={'net10.0/'+rid:selected}),value,selected

    def test_both_flavors_keep_exact_public_core_and_native_graph(self):
        for traced in [False,True]:
            assets,value,_=self.graph(traced)
            self.assertTrue(gate.verify_graph(assets,value,'linux-x64',traced))

    def test_source_project_private_feed_and_mixed_trace_are_rejected(self):
        for mode in ['project','private','mixed','source','minimum']:
            assets,value,selected=self.graph()
            if mode=='project': assets['libraries']['Source/1']={'type':'project'}
            if mode=='private':
                p=self.cache/'ranvier.webcore'/value['versions']['native']/'.nupkg.metadata'
                p.write_text(json.dumps(dict(source='C:/private-feed')))
            if mode=='mixed': selected['Ranvier.View.Traced/'+value['versions']['view']]={}
            if mode=='source':
                p=self.cache/'ranvier.view'/value['versions']['view']/'ranvier.view.nuspec'
                p.write_text('<package><metadata><repository commit="'+'9'*40+'" /></metadata></package>')
            if mode=='minimum': selected['Ranvier.View.Dom/'+value['versions']['view']]['dependencies']['Ranvier.Dom']='0.1.0-alpha.55'
            with self.subTest(mode=mode),self.assertRaises(ValueError): gate.verify_graph(assets,value,'linux-x64',False)

    def test_missing_fifth_rid_and_native_version_override_are_rejected(self):
        for mode in ['missing','version','rid']:
            assets,value,selected=self.graph()
            if mode=='missing': del selected['Ranvier.WebCore.Native.osx-arm64/'+value['versions']['native']]
            if mode=='version':
                entry=selected.pop('Ranvier.WebCore/'+value['versions']['native']); selected['Ranvier.WebCore/0.1.1-preview.7']=entry
            if mode=='rid': assets['targets']={'net10.0/win-x64':selected}
            with self.subTest(mode=mode),self.assertRaises(ValueError): gate.verify_graph(assets,value,'linux-x64',False)

    def output_fixture(self):
        self.assertTrue(callable(getattr(gate,'verify_output',None)), 'Output verification is not implemented.')
        assets,value,selected=self.graph()
        version=value['versions']['native']; output=self.cache/'output'; output.mkdir()
        host=bytearray(64); host[:6]=b'\x7fELF\x02\x01'; host[18:20]=(62).to_bytes(2,'little')
        host=bytes(host); module=host+b'provider-metadata-fixture'
        value['rids']['linux-x64'].update(hostSha256=hashlib.sha256(host).hexdigest(),providerSha256=hashlib.sha256(module).hexdigest())
        name='libRanvierWebCoreHost.so'; package='Ranvier.WebCore.Native.linux-x64'; directory=self.cache/package.lower()/version
        native=directory/'runtimes/linux-x64/native'; native.mkdir(parents=True)
        (native/name).write_bytes(host); (output/name).write_bytes(host)
        record=dict(name=name,bytes=len(host),sha256=hashlib.sha256(host).hexdigest())
        manifest=dict(version=version,rid='linux-x64',flavor='slim',abi=63,source=dict(commit=value['nativeSource'],dirty=False),webkit=value['webkitSource'],files=[record],total_bytes=len(host))
        (directory/'manifest.json').write_text(json.dumps(manifest))
        selected[package+'/'+version]['native']={'runtimes/linux-x64/native/'+name:{}}
        provider=self.cache/gate.PROVIDER.lower()/version; provider.mkdir(parents=True)
        (provider/'.nupkg.metadata').write_text(json.dumps(dict(source='https://api.nuget.org/v3/index.json')))
        (provider/'provider.nuspec').write_text('<package><metadata><repository commit="'+value['providerSource']+'" /></metadata></package>')
        assets['libraries'][gate.PROVIDER+'/'+version]=dict(type='package',path=gate.PROVIDER.lower()+'/'+version)
        selected[gate.PROVIDER+'/'+version]={'native':{'runtimes/linux-x64/native/libRanvierWebCoreAtspi.so':{}}}
        provider_manifest=dict(id=gate.PROVIDER,version=version,rid='linux-x64',abi=63,
                               callerClaimedProviderSourceRevision=value['providerSource'],hostSha256=value['rids']['linux-x64']['hostSha256'],providerSha256=value['rids']['linux-x64']['providerSha256'])
        (provider/'provider-manifest.json').write_text(json.dumps(provider_manifest))
        module_path=provider/'runtimes/linux-x64/native/libRanvierWebCoreAtspi.so'; module_path.parent.mkdir(parents=True); module_path.write_bytes(module)
        (output/module_path.name).write_bytes(module)
        return assets,value,output,directory,provider

    def test_output_checks_real_file_bytes_but_does_not_execute_fixture_binaries(self):
        assets,value,output,_,_=self.output_fixture()
        with patch.object(gate,'verify_header',return_value=['fixture']*344+gate.TAIL):
            result=gate.verify_output(assets,output,value,'linux-x64',False)
        self.assertEqual(result['abi'],63)
        self.assertIn('execution is separate',result['scope'])

    def test_stale_abi_source_rid_and_mutated_output_fail(self):
        assets,value,output,directory,_=self.output_fixture()
        original=json.loads((directory/'manifest.json').read_text())
        for mode in ['abi','source','rid','dirty','bytes','pin','duplicate']:
            manifest=deepcopy(original); expected=deepcopy(value)
            if mode=='abi': manifest['abi']=62
            if mode=='source': manifest['source']['commit']='9'*40
            if mode=='rid': manifest['rid']='linux-arm64'
            if mode=='dirty': manifest['source']['dirty']=True
            if mode=='bytes': (output/'libRanvierWebCoreHost.so').write_bytes(b'changed')
            if mode=='pin': expected['rids']['linux-x64']['hostSha256']='9'*64
            if mode=='duplicate': manifest['files'].append(manifest['files'][0])
            (directory/'manifest.json').write_text(json.dumps(manifest))
            with self.subTest(mode=mode),patch.object(gate,'verify_header',return_value=[]),self.assertRaises(ValueError):
                gate.verify_output(assets,output,expected,'linux-x64',False)
            (output/'libRanvierWebCoreHost.so').write_bytes((directory/'runtimes/linux-x64/native/libRanvierWebCoreHost.so').read_bytes())

    def test_provider_mixed_host_and_wrong_architecture_fail(self):
        assets,value,output,_,provider=self.output_fixture()
        original=json.loads((provider/'provider-manifest.json').read_text())
        for mode in ['host','source','abi','module','architecture']:
            manifest=deepcopy(original)
            if mode=='host': manifest['hostSha256']='9'*64
            if mode=='source': manifest['callerClaimedProviderSourceRevision']='9'*40
            if mode=='abi': manifest['abi']=62
            if mode=='module': (output/'libRanvierWebCoreAtspi.so').write_bytes(b'changed')
            if mode=='architecture':
                value['rids']['linux-x64']['providerSha256']=hashlib.sha256(b'wrong arch').hexdigest()
                manifest['providerSha256']=value['rids']['linux-x64']['providerSha256']
                (output/'libRanvierWebCoreAtspi.so').write_bytes(b'wrong arch')
            (provider/'provider-manifest.json').write_text(json.dumps(manifest))
            with self.subTest(mode=mode),patch.object(gate,'verify_header',return_value=[]),self.assertRaises(ValueError):
                gate.verify_output(assets,output,value,'linux-x64',False)
            (output/'libRanvierWebCoreAtspi.so').write_bytes((provider/'runtimes/linux-x64/native/libRanvierWebCoreAtspi.so').read_bytes())


if __name__=='__main__': unittest.main()

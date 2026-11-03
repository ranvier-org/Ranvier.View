"""Compare public package graphs and binary pins; build provenance remains a separate receipt."""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('published_native', ROOT/'tools/verify-published-native-runtime.py')
published = importlib.util.module_from_spec(spec)
spec.loader.exec_module(published)
RIDS = {'win-x64','linux-x64','linux-arm64'}
ALL_NATIVE_RIDS = published.RIDS
PROVIDER = 'Ranvier.WebCore.Accessibility.Atspi'
HEADER_SHA256 = 'a991d384426af8d8c295fb83fb797f50480f93056bf28d940c412ab3fbaa52b7'
TAIL = ['rv_host_ime_surrounding','rv_host_ime_delete_surrounding','rv_ax_text_style_run']


def full_hash(value,length,label):
    if not isinstance(value,str) or not re.fullmatch('[0-9a-f]{'+str(length)+'}',value):
        raise ValueError(label+' requires a full lowercase hash.')


def load_cohort(value,rid=None):
    if value.get('schemaVersion')!=1 or value.get('armed') is not True:
        raise ValueError('The final cohort is unarmed.')
    for key in ['managedSource','nativeSource','providerSource','webkitSource']:
        full_hash(value.get(key),40,key)
    versions=value['versions']
    if rid is not None and rid not in RIDS: raise ValueError('Unsupported consumer RID.')
    for key in ['view','backend','native','provider']:
        version=versions.get(key)
        if key=='provider' and version is None and rid=='win-x64': continue
        if (not isinstance(version,str) or not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*)?',version)
                or re.search(r'(?:^|[.-])(?:private\w*|local\w*|ci)(?:[.-]|$)',version,re.I)):
            raise ValueError(key+' requires an exact allocated public version.')
    if versions.get('provider') is not None and versions['provider']!=versions['native']:
        raise ValueError('Provider and Native versions must match their coordinated cohort.')
    expected=dict(core='0.1.1-preview.1',dom='0.1.0-alpha.57',css='0.1.1-alpha.2',cssAddons='0.1.1-alpha.1')
    if any(versions.get(key)!=value for key,value in expected.items()):
        raise ValueError('Keep the reviewed public Core/DOM/CSS versions.')
    if set(value['rids'])!=RIDS:
        raise ValueError('Supply all three consumer RIDs; ARM is not inferred from x64.')
    for item_rid,record in value['rids'].items():
        if rid is None or item_rid==rid:
            full_hash(record.get('hostSha256'),64,item_rid+' host')
            if item_rid.startswith('linux-'): full_hash(record.get('providerSha256'),64,item_rid+' provider')
    return value


def restored_package(assets,name,version):
    _,library=published.matching_entry(assets['libraries'],name,version)
    relative=published.relative_name(library['path'])
    roots=[Path(folder)/relative for folder in assets['packageFolders'] if (Path(folder)/relative).is_dir()]
    if len(roots)!=1: raise ValueError('Expected one isolated cache entry for '+name)
    root=roots[0]
    metadata=json.loads(published.contained_file(root,'.nupkg.metadata').read_text(encoding='utf-8'))
    if metadata.get('source','').rstrip('/').casefold()!=published.OFFICIAL_SOURCE:
        raise ValueError(name+' must be restored from official NuGet.')
    return root


def repository_claim(root,expected):
    nuspecs=list(root.glob('*.nuspec'))
    if len(nuspecs)!=1: raise ValueError('Expected one package nuspec.')
    repositories=[node for node in ET.parse(nuspecs[0]).getroot().iter() if node.tag.rsplit('}',1)[-1]=='repository']
    if len(repositories)!=1 or repositories[0].get('commit')!=expected:
        raise ValueError('Package repository source claim differs from the expected pin.')


def verify_graph(assets,cohort,rid,traced):
    load_cohort(cohort,rid)
    if rid not in RIDS: raise ValueError('Unsupported consumer RID.')
    if any(value.get('type')=='project' for value in assets['libraries'].values()):
        raise ValueError('Public acceptance cannot use source project references.')
    if any(key.casefold().startswith(('ranvier.private.','xantham.','fable.ranvier')) for key in assets['libraries']):
        raise ValueError('Private/browser package in native consumer graph.')
    targets=[value for name,value in assets['targets'].items() if name.endswith('/'+rid)]
    if len(targets)!=1: raise ValueError('Restore exactly one selected RID target.')
    selected=targets[0]; suffix='.Traced' if traced else ''; versions=cohort['versions']
    for name in ['Ranvier.View','Ranvier.View.Dom','Ranvier.View.WebCore','Ranvier.View.WebCore.Sdl','Ranvier','Ranvier.Dom']:
        version=versions['core'] if name=='Ranvier' else versions['dom'] if name=='Ranvier.Dom' else versions['backend'] if '.WebCore' in name else versions['view']
        published.matching_entry(selected,name+suffix,version)
        if any(key.rsplit('/',1)[0].casefold()==(name+('' if traced else '.Traced')).casefold() for key in selected):
            raise ValueError('Mixed normal/traced graph.')
        if name.startswith('Ranvier.View'):
            repository_claim(restored_package(assets,name+suffix,version),cohort['managedSource'])
    for owner,dependency,version in [('Ranvier.View.Dom','Ranvier.Dom'+suffix,versions['dom']),('Ranvier.View.WebCore','Ranvier.WebCore',versions['native'])]:
        _,entry=published.matching_entry(selected,owner+suffix,versions['backend'] if '.WebCore' in owner else versions['view'])
        if entry.get('dependencies',{}).get(dependency)!=version:
            raise ValueError('The package minimum must be correct without a higher direct override.')
    published.matching_entry(selected,'Ranvier.WebCore',versions['native'])
    repository_claim(restored_package(assets,'Ranvier.WebCore',versions['native']),cohort['nativeSource'])
    _,meta=published.matching_entry(selected,'Ranvier.WebCore.Native',versions['native'])
    if not {'ranvier.webcore.native.'+item for item in ALL_NATIVE_RIDS}<={name.casefold() for name in meta.get('dependencies',{})}:
        raise ValueError('The complete five-RID native meta-package graph is required.')
    for item in ALL_NATIVE_RIDS: published.matching_entry(selected,'Ranvier.WebCore.Native.'+item,versions['native'])
    if any(key.casefold().startswith('ranvier.webcore.native.gpu.') and published.active_assets(entry) for key,entry in selected.items()):
        raise ValueError('This Slim consumer must not select GPU runtime assets.')
    for key,entry in assets['libraries'].items():
        name,version=key.rsplit('/',1)
        if entry.get('type')=='package' and name.casefold().startswith('ranvier'):
            restored_package(assets,name,version)
    return selected


def verify_architecture(data,rid):
    if rid=='win-x64':
        offset=int.from_bytes(data[60:64],'little')
        valid=len(data)>=70 and data[:2]==b'MZ' and data[offset:offset+4]==b'PE\0\0' and int.from_bytes(data[offset+4:offset+6],'little')==0x8664
    else:
        valid=len(data)>=20 and data[:6]==b'\x7fELF\x02\x01' and int.from_bytes(data[18:20],'little')=={'linux-x64':62,'linux-arm64':183}.get(rid)
    if not valid: raise ValueError('Binary architecture does not match '+rid)


def verify_header(path):
    data=Path(path).read_bytes().replace(b'\r\n',b'\n')
    if hashlib.sha256(data).hexdigest()!=HEADER_SHA256:
        raise ValueError('The reviewed full signature header differs; repeat source/generator review.')
    names=re.findall(r'RV_EXPORT\s+[^;{}]+?\b(rv_[A-Za-z0-9_]+)\s*\([^;{}]*\)\s*;',data.decode())
    if len(names)!=347 or names[-3:]!=TAIL:
        raise ValueError('Expected the reviewed 344-prefix and three-tail ABI63 signatures.')
    return names


def verify_output(assets,output,cohort,rid,traced):
    selected=verify_graph(assets,cohort,rid,traced)
    version=cohort['versions']['native']; output=Path(output)
    package='Ranvier.WebCore.Native.'+rid
    cache=restored_package(assets,package,version)
    manifest=json.loads(published.contained_file(cache,'manifest.json').read_text(encoding='utf-8'))
    if type(manifest.get('abi')) is not int or (manifest.get('version'),manifest.get('rid'),manifest.get('flavor'),manifest['abi'])!=(version,rid,'slim',63):
        raise ValueError('Native manifest version/RID/flavor/ABI differs.')
    source=manifest.get('source',{})
    if source.get('commit')!=cohort['nativeSource'] or source.get('dirty') is not False or manifest.get('webkit')!=cohort['webkitSource']:
        raise ValueError('Native source claims differ from the expected pins.')
    records=manifest.get('files')
    if not isinstance(records,list) or not records: raise ValueError('Native manifest has no payload records.')
    names=[published.relative_name(record['name']) for record in records]
    if len({name.casefold() for name in names})!=len(names): raise ValueError('Duplicate native manifest payload.')
    for record in records:
        if type(record.get('bytes')) is not int or record['bytes']<0: raise ValueError('Invalid native payload size.')
        full_hash(record.get('sha256'),64,'Native payload')
    if manifest.get('total_bytes')!=sum(record['bytes'] for record in records): raise ValueError('Native manifest total differs.')
    native=cache/('runtimes/'+rid+'/native')
    if set(names)!={path.relative_to(native).as_posix() for path in native.rglob('*') if path.is_file()}:
        raise ValueError('The native package must match its complete manifest.')
    _,entry=published.matching_entry(selected,package,version)
    prefix='runtimes/'+rid+'/native/'
    if {name[len(prefix):] for name in published.active_assets(entry) if name.startswith(prefix)}!=set(names):
        raise ValueError('Selected runtime assets differ from the complete manifest.')
    for record in records:
        published.check_bytes(published.contained_file(native,record['name']),record)
        published.check_bytes(published.contained_file(output,record['name']),record)
    host_name='RanvierWebCoreHost.dll' if rid.startswith('win-') else 'libRanvierWebCoreHost.so'
    host=published.contained_file(output,host_name)
    if hashlib.sha256(host.read_bytes()).hexdigest()!=cohort['rids'][rid]['hostSha256']:
        raise ValueError('Host bytes differ from the independently pinned cohort.')
    verify_architecture(host.read_bytes(),rid)
    interop=restored_package(assets,'Ranvier.WebCore',version)
    declarations=verify_header(interop/'include/RanvierWebCoreAbi.h')
    if rid.startswith('linux-'):
        provider_version=cohort['versions']['provider']
        _,entry=published.matching_entry(selected,PROVIDER,provider_version)
        asset='runtimes/'+rid+'/native/libRanvierWebCoreAtspi.so'
        if asset not in published.active_assets(entry): raise ValueError('Provider is not selected for this RID.')
        cache=restored_package(assets,PROVIDER,provider_version)
        provider=json.loads(published.contained_file(cache,'provider-manifest.json').read_text(encoding='utf-8'))
        if provider.get('schemaVersion')==2:
            if (type(provider.get('abi')) is not int or provider['abi']!=63 or provider.get('nativeVersion')!=version or provider.get('callerClaimedNativeSourceRevision')!=cohort['nativeSource']
                    or provider.get('callerClaimedProviderSourceRevision')!=cohort['providerSource']
                    or provider.get('sourceProvenance')!='unverified-caller-claim'
                    or provider.get('requiredCapability')!='accessibility-text-style' or provider.get('hostBundled') is not False):
                raise ValueError('Aggregate provider cohort/source-claim contracts differ.')
            runtimes=provider.get('runtimes')
            if not isinstance(runtimes,dict) or rid not in runtimes or not set(runtimes)<={'linux-x64','linux-arm64'}:
                raise ValueError('The aggregate has no selected supported RID record.')
            for item_rid,record in runtimes.items():
                if type(record.get('abi')) is not int or (record.get('rid'),record['abi'],record.get('callerClaimedProviderSourceRevision'))!=(item_rid,63,cohort['providerSource']):
                    raise ValueError('Aggregate RID/source/ABI contracts differ.')
                for field in ['hostSha256','providerSha256']: full_hash(record.get(field),64,'Aggregate '+field)
            payloads=provider.get('payloads')
            if not isinstance(payloads,dict) or not payloads: raise ValueError('Aggregate payload hashes are missing.')
            for name,expected in payloads.items():
                full_hash(expected,64,'Aggregate payload')
                if hashlib.sha256(published.contained_file(cache,name).read_bytes()).hexdigest()!=expected:
                    raise ValueError('Aggregate SDK/native payload bytes differ.')
            provider={**provider,**runtimes[rid]}
        if type(provider.get('abi')) is not int or (provider.get('id'),provider.get('version'),provider.get('rid'),provider['abi'])!=(PROVIDER,provider_version,rid,63):
            raise ValueError('Provider identity/RID/ABI differs.')
        if provider.get('callerClaimedProviderSourceRevision')!=cohort['providerSource'] or provider.get('hostSha256')!=cohort['rids'][rid]['hostSha256']:
            raise ValueError('Provider source claim or selected host differs.')
        expected=cohort['rids'][rid]['providerSha256']
        module=published.contained_file(output,'libRanvierWebCoreAtspi.so')
        if provider.get('providerSha256')!=expected or any(hashlib.sha256(path.read_bytes()).hexdigest()!=expected for path in [module,published.contained_file(cache,asset)]):
            raise ValueError('Provider module bytes differ.')
        verify_architecture(module.read_bytes(),rid)
    elif any(key.rsplit('/',1)[0].casefold()==PROVIDER.casefold() for key in selected):
        raise ValueError('The Linux provider must not become a Windows dependency.')
    return dict(scope='public package graph and payload/source-claim comparison only; execution is separate',rid=rid,traced=traced,abi=63,declarations=len(declarations),host=str(host))


def verify_runtime_result(text,output,aot,attached,linux,provider_loaded):
    results=[json.loads(line) for line in text.splitlines() if line.startswith('{')]
    expected=dict(windows=3,directHosts=3,expectedAttached=attached,dynamicCodeSupported=not aot)
    if results!=[expected]: raise ValueError('The real consumer did not complete its exact Window/Host profile.')
    if linux:
        loaded={line[len('native-map '):].split(None,5)[-1] for line in text.splitlines() if line.startswith('native-map ')}
        names=['libRanvierWebCoreHost.so']+(['libRanvierWebCoreAtspi.so'] if provider_loaded else [])
        if loaded!={Path(output).as_posix().rstrip('/')+'/'+name for name in names}:
            raise ValueError('The loaded native paths differ from the verified consumer output.')
    return expected

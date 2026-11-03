"""Restore a pinned public consumer into a new cache and retain real JIT/AOT results."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys
import verify

spec=importlib.util.spec_from_file_location('published_runner',verify.ROOT/'tools/check-published-shared-api.py')
supervision=importlib.util.module_from_spec(spec)
spec.loader.exec_module(supervision)


def run(cohort_path,rid,mode,output,dotnet='dotnet'):
    cohort=verify.load_cohort(json.loads(Path(cohort_path).read_text(encoding='utf-8')),rid)
    output=Path(output).resolve()
    if output.exists(): raise ValueError('Use a new owned output/cache directory.')
    output.mkdir(parents=True)
    (output/'global.json').write_text(json.dumps(dict(sdk=dict(version='10.0.403',rollForward='disable',allowPrerelease=False))))
    config=output/'NuGet.Config'
    config.write_text('<configuration><packageSources><clear/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
    env=supervision.checker_environment(output)
    for name in ['LD_LIBRARY_PATH','DYLD_LIBRARY_PATH','DOTNET_ADDITIONAL_DEPS','DOTNET_SHARED_STORE']: env.pop(name,None)
    env['DOTNET_PROCESSOR_COUNT']='2'
    steps=[]; receipt=dict(scope='exact public consumer trial; source fields are compared claims, independent producer/OS acceptance remains separate',rid=rid,mode=mode,cohort=cohort,steps=steps)
    def command(label,argv,environment=None,timeout=600):
        step=dict(label=label); steps.append(step)
        try:
            return supervision.run_logged_command(argv,cwd=output,environment=environment or env,timeout_seconds=timeout,log_path=output/(label+'.log'),step=step)
        finally:
            (output/'receipt.json').write_text(json.dumps(receipt,indent=2))
    try:
        if command('sdk',[dotnet,'--version']).strip()!='10.0.403': raise ValueError('Use the reviewed .NET SDK10.0.403.')
        for execution,aot in [('jit',False),('aot',True)]:
            publish=output/execution
            artifacts=output/('build-'+execution)
            argv=[dotnet,'publish',str(verify.ROOT/'tests/native-cohort347/Consumer.fsproj'),'-c','Release','-r',rid,'-m:1','/nr:false',
                  '-p:PublishAot='+str(aot).lower(),'-p:CohortBackendVersion='+cohort['versions']['backend'],
                  '-p:CohortSuffix='+('.Traced' if mode=='traced' else ''),'-p:CohortLinux='+str(rid.startswith('linux-')).lower(),
                  '-p:CohortProviderVersion='+(cohort['versions'].get('provider') or ''),'-p:RestorePackagesPath='+str(output/'packages'),
                  '-p:RestoreConfigFile='+str(config),'--artifacts-path',str(artifacts),'-o',str(publish)]
            command('publish-'+execution,argv)
            matches=list(artifacts.rglob('project.assets.json'))
            assets=[path for path in matches if Path(json.loads(path.read_text())['project']['restore']['projectPath']).resolve()==(verify.ROOT/'tests/native-cohort347/Consumer.fsproj').resolve()]
            if len(assets)!=1: raise ValueError('Expected the one isolated public consumer restore graph.')
            value=json.loads(assets[0].read_text())
            receipt[execution+'Graph']=verify.verify_output(value,publish,cohort,rid,mode=='traced')
            interop=verify.restored_package(value,'Ranvier.WebCore',cohort['versions']['native'])
            declarations=verify.verify_header(interop/'include/RanvierWebCoreAbi.h')
            command('exports-'+execution,[sys.executable,str(verify.ROOT/'tools/native-cohort347/exports.py'),'--host',receipt[execution+'Graph']['host'],'--header',str(interop/'include/RanvierWebCoreAbi.h')],timeout=30)
            if len(declarations)!=347: raise ValueError('Expected347 declarations.')
            binary=publish/('AccessibilityAot.exe' if rid.startswith('win-') else 'AccessibilityAot')
            app=[str(binary)] if aot else [dotnet,str(publish/'AccessibilityAot.dll')]
            if rid.startswith('linux-'):
                for profile in ['present','absent','no-bus']:
                    command(execution+'-'+profile,[sys.executable,str(verify.ROOT/'tools/native-cohort347/linux_control.py'),'--output',str(publish),'--profile',profile,'--aot',str(aot).lower(),'--dotnet',dotnet],timeout=60)
            else:
                text=command(execution+'-uia',app+['--expect-attached'],timeout=60)
                verify.verify_runtime_result(text,publish,aot,True,False,False)
        receipt['status']='passed'
    except BaseException as error:
        receipt.update(status='failed',failure=str(error)); raise
    finally:
        (output/'receipt.json').write_text(json.dumps(receipt,indent=2))
    return receipt


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cohort',type=Path,required=True)
    parser.add_argument('--rid',choices=sorted(verify.RIDS),required=True)
    parser.add_argument('--mode',choices=['normal','traced'],required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--dotnet',default='dotnet')
    args=parser.parse_args()
    print(json.dumps(run(args.cohort,args.rid,args.mode,args.output,args.dotnet)))

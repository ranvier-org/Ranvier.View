"""Exercise an actual Linux consumer using an owned bus and inherited owned display."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import select
import shutil
import subprocess
import time
import verify

spec=importlib.util.spec_from_file_location('published_runner',verify.ROOT/'tools/check-published-shared-api.py')
supervision=importlib.util.module_from_spec(spec)
spec.loader.exec_module(supervision)


def cleanup_children(children):
    failures=[]
    for child in reversed(children):
        if child.poll() is None:
            try: supervision.terminate_owned_process_tree(child)
            except Exception as error: failures.append(dict(pid=child.pid,error=str(error)))
    return failures


def execute(output,profile,aot,dotnet):
    output=Path(output).resolve()
    selected=output
    if profile=='absent':
        selected=output.with_name(output.name+'-absent')
        shutil.copytree(output,selected,ignore=shutil.ignore_patterns('libRanvierWebCoreAtspi.so'))
        if list(selected.rglob('libRanvierWebCoreAtspi.so')): raise ValueError('The absent control retained a provider module.')
    names=['libRanvierWebCoreHost.so','AccessibilityAot' if aot else 'AccessibilityAot.dll']
    if profile!='absent': names.append('libRanvierWebCoreAtspi.so')
    digest=lambda name:hashlib.sha256((selected/name).read_bytes()).hexdigest()
    before={name:digest(name) for name in names}
    env=dict(os.environ,SDL_VIDEODRIVER='x11',EGL_PLATFORM='surfaceless',LIBGL_ALWAYS_SOFTWARE='1')
    env.pop('LD_LIBRARY_PATH',None)
    children=[]; streams=[]
    receipt=dict(profile=profile,aot=aot,before=before,steps=[])
    directory=output.parent/(output.name+'-'+profile+'-control'); directory.mkdir()
    def spawn(argv,name,pipe=False):
        log=(directory/(name+'.log')).open('wb'); streams.append(log)
        child=subprocess.Popen(argv,env=env,stdout=subprocess.PIPE if pipe else log,stderr=log,start_new_session=True)
        children.append(child)
        receipt['steps'].append(dict(command=argv,pid=child.pid,startTicks=Path('/proc/'+str(child.pid)+'/stat').read_text().split()[21]))
        return child
    try:
        if not env.get('DISPLAY'): raise ValueError('Supply an owned Xvfb display or the approved local test display.')
        bus=spawn(['dbus-daemon','--session','--nofork','--print-address=1'],'bus',True)
        if not select.select([bus.stdout],[],[],5)[0]: raise TimeoutError('Owned accessibility bus did not start.')
        address=bus.stdout.readline().decode().strip()
        if not address: raise ValueError('Owned bus returned no address.')
        env.update(DBUS_SESSION_BUS_ADDRESS=address,AT_SPI_BUS_ADDRESS=address)
        registry=spawn(['/usr/libexec/at-spi2-registryd'],'registry')
        deadline=time.monotonic()+5
        while True:
            probe=subprocess.run(['gdbus','call','--address',address,'--dest','org.freedesktop.DBus','--object-path','/org/freedesktop/DBus','--method','org.freedesktop.DBus.NameHasOwner','org.a11y.atspi.Registry'],env=env,capture_output=True,text=True,timeout=2)
            if probe.returncode==0 and probe.stdout.strip()=='(true,)': break
            if registry.poll() is not None or time.monotonic()>=deadline: raise TimeoutError('Owned AT-SPI registry did not start.')
            time.sleep(0.02)
        if profile=='no-bus':
            unavailable=directory/'unavailable-bus'
            env.update(DBUS_SESSION_BUS_ADDRESS='unix:path='+str(unavailable),AT_SPI_BUS_ADDRESS='unix:path='+str(unavailable))
        command=[str(selected/'AccessibilityAot')] if aot else [dotnet,str(selected/'AccessibilityAot.dll')]
        attached=profile=='present'
        if attached: command.append('--expect-attached')
        step={}; receipt['consumer']=step
        text=supervision.run_logged_command(command,cwd=selected,environment=env,timeout_seconds=45,log_path=directory/'consumer.log',step=step)
        receipt['result']=verify.verify_runtime_result(text,selected,aot,attached,True,profile!='absent')
    finally:
        receipt['cleanupErrors']=cleanup_children(children)
        for child in children:
            if child.stdout is not None: child.stdout.close()
        for stream in streams: stream.close()
        receipt['after']={name:digest(name) for name in names}
        receipt['allOwnedProcessesExited']=all(child.poll() is not None for child in children)
        (directory/'receipt.json').write_text(json.dumps(receipt,indent=2))
        if receipt['cleanupErrors']: raise RuntimeError('Owned control cleanup failed: '+str(receipt['cleanupErrors']))
        if receipt['before']!=receipt['after']: raise ValueError('Consumer/native bytes changed during the control.')
    print(json.dumps(receipt))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--profile',choices=['present','absent','no-bus'],required=True)
    parser.add_argument('--aot',choices=['true','false'],required=True)
    parser.add_argument('--dotnet',default='dotnet')
    args=parser.parse_args()
    execute(args.output,args.profile,args.aot=='true',args.dotnet)

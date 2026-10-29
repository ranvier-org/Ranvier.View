"""Run a release's shared API fixtures using only its official NuGet packages."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

REPOSITORY = Path(__file__).resolve().parents[1]
OFFICIAL = 'https://api.nuget.org/v3/index.json'
FIXTURES = 'tests/NativeApi.Conformance/'


def validate_profile(profile):
    if not re.fullmatch('[0-9a-f]{40}', profile.get('source_commit', '')):
        raise ValueError('Fixture source_commit must be an immutable full commit.')
    for key in ['view_version', 'backend_version', 'dom_version', 'native_version',
                'css_dom_version', 'css_bindings_version', 'fsharp_core_version', 'fable_core_version']:
        if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-[a-zA-Z0-9.]+)?', profile.get(key, '')):
            raise ValueError(f'Invalid exact package version: {key}')
    for key in ['native_abi', 'shared_assertions', 'native_lifetime_assertions',
                'browser_context_assertions', 'css_escape_oracle']:
        if type(profile.get(key)) is not int or profile[key] <= 0:
            raise ValueError(f'Expected positive integer: {key}')


def compile_names(project):
    names = [item.attrib['Include'] for item in ET.fromstring(project).iter('Compile')]
    if not names or len(names) != len(set(names)):
        raise ValueError('Fixture compile list must be nonempty and unique.')
    if any(not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*\.fs', name) for name in names):
        raise ValueError('Fixture compile files must stay in their source directory.')
    return names


def verify_nuspec(source, commit):
    repository = ET.fromstring(source).find('.//{*}repository')
    if (repository is None or repository.get('commit') != commit
            or repository.get('url', '').removesuffix('.git').rstrip('/').casefold()
            != 'https://github.com/ranvier-org/ranvier.view'):
        raise ValueError('Published View package repository commit must match fixture source commit.')


def verify_variant(identities, variant):
    traced = variant == 'traced'
    for identity in identities:
        name = identity.rsplit('/', 1)[0]
        if name.casefold().startswith(('ranvier', 'fable.ranvier')) and not name.startswith('Ranvier.WebCore'):
            if name.endswith('.Traced') != traced:
                raise ValueError(f'Mixed trace package graph: {identity}')


def verify_browser_assets(targets, suffix):
    native = 'Ranvier.Dom' + suffix + '/'
    browser = 'Fable.Ranvier.Dom' + suffix + '/'
    for target in targets.values():
        native_assets = [path for name, entry in target.items() if name.startswith(native)
                         for path in entry.get('compile', {}) if not path.endswith('/_._')]
        browser_assets = [path for name, entry in target.items() if name.startswith(browser)
                          for path in entry.get('compile', {}) if path.endswith('/Ranvier.Dom.dll')]
        if native_assets or len(browser_assets) != 1:
            raise ValueError('Fable must select browser DOM and exclude transitive native DOM assets.')


def verify_assertions(output, target, variant, profile):
    kind = 'NATIVE' if target == 'native' else 'BROWSER'
    counts = re.findall(kind + r'_API (.+?) PASS assertions=(\d+)', output)
    total = sum(int(value) for name, value in counts if 'LIFETIME' not in name and 'CONTEXT' not in name)
    if total != profile['shared_assertions'] or ('D11_VIEW_HYPERLINKS', '50') not in counts:
        raise ValueError(f'Incomplete shared assertions: {target}/{variant}: {total}')
    if kind + '_API D09 TAB PASS' not in output:
        raise ValueError('Tab traversal did not run.')
    if target == 'native':
        lifetime = sum(int(value) for name, value in counts if 'LIFETIME' in name)
        marker = f"OFFICIAL_TRACE_METADATA PASS expected={variant == 'traced'} assemblies=5"
        if lifetime != profile['native_lifetime_assertions'] or 'NATIVE_API TEARDOWN PASS' not in output or marker not in output:
            raise ValueError('Incomplete native lifetime, teardown or trace checks.')
    else:
        contexts = sum(int(value) for name, value in counts if 'CONTEXT' in name)
        if contexts != profile['browser_context_assertions'] or f"oracle={profile['css_escape_oracle']}" not in output:
            raise ValueError('Incomplete browser context/oracle checks.')


def package_references(profile, target, suffix):
    packages = [('Ranvier.View' + suffix, 'view_version'), ('Ranvier.View.Dom' + suffix, 'view_version'),
                ('Ranvier.Css.Bindings' + suffix, 'css_bindings_version')]
    if target == 'native':
        packages += [('Ranvier.View.WebCore' + suffix, 'backend_version'), ('Ranvier.Dom' + suffix, 'dom_version'),
                     ('Ranvier.WebCore', 'native_version'), ('Ranvier.WebCore.Native', 'native_version'),
                     ('Ranvier.Css.Dom' + suffix, 'css_dom_version')]
    else:
        packages += [('Fable.Ranvier.Dom' + suffix, 'dom_version'),
                     ('Fable.Ranvier.Css.Dom' + suffix, 'css_dom_version'), ('Fable.Core', 'fable_core_version')]
    references = [f'    <PackageReference Include="{name}" Version="[{profile[key]}]" />' for name, key in packages]
    if target == 'browser':
        references += [f'    <PackageReference Include="Ranvier.Dom{suffix}" Version="[{profile["dom_version"]}]" ExcludeAssets="all" />']
    references += [f'    <PackageReference Update="FSharp.Core" Version="{profile["fsharp_core_version"]}" />']
    return '\n'.join(references)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='New directory; existing directories are refused.')
    parser.add_argument('--variant', choices=['normal', 'traced'], required=True)
    parser.add_argument('--target', choices=['native', 'browser'], required=True)
    args = parser.parse_args()
    profile = json.loads(args.profile.read_text(encoding='utf-8'))
    validate_profile(profile)
    if args.target == 'native' and os.name != 'nt':
        raise ValueError('This native profile requires Windows x64.')
    output = args.output.resolve()
    if output.is_relative_to(REPOSITORY):
        raise ValueError('Consumer output must be outside the source checkout to avoid inherited build settings.')
    output.mkdir(parents=True, exist_ok=False)
    environment = dict(os.environ, NUGET_PACKAGES=str(output / 'packages'),
                       NUGET_HTTP_CACHE_PATH=str(output / 'http-cache'),
                       NATIVE_API_BROWSER_PACKAGE=str(output / 'package.json'), FICKL_SERVER='0',
                       COHORT_EXPECT_TRACE=str(args.variant == 'traced').lower())
    if os.name == 'nt':
        environment['OS'] = 'Windows_NT'
    suffix = '.Traced' if args.variant == 'traced' else ''
    evidence = {'profile': profile, 'variant': args.variant, 'target': args.target, 'sources': {}}

    def source(path):
        data = subprocess.check_output(['git', 'show', profile['source_commit'] + ':' + path], cwd=REPOSITORY)
        evidence['sources'][path] = hashlib.sha256(data).hexdigest()
        return data

    def run(label, *command):
        result = subprocess.run(list(map(str, command)), cwd=output, env=environment,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, encoding='utf-8', errors='replace')
        (output / (label + '.log')).write_text(result.stdout, encoding='utf-8')
        print(result.stdout, flush=True)
        result.check_returncode()
        return result.stdout

    try:
        config = output / 'NuGet.Config'
        config.write_text('<configuration><packageSources><clear/><add key="official" value="' + OFFICIAL
                          + '"/></packageSources><packageSourceMapping><clear/><packageSource key="official">'
                          '<package pattern="*"/></packageSource></packageSourceMapping></configuration>')
        (output / 'global.json').write_bytes(source('global.json'))
        name = 'Native' if args.target == 'native' else 'Browser'
        names = compile_names(source(FIXTURES + name + '.fsproj'))
        for filename in names:
            (output / filename).write_bytes(source(FIXTURES + filename))
        properties = '<OutputType>Exe</OutputType><RuntimeIdentifier>win-x64</RuntimeIdentifier><PublishAot>true</PublishAot>' if args.target == 'native' else ''
        if args.target == 'native':
            trace = REPOSITORY / 'tests/native-packages/SharedTraceProbe.fs'
            (output / trace.name).write_bytes(trace.read_bytes())
            runner = output / 'Native.fs'
            text = runner.read_text(encoding='utf-8')
            entry = 'let main _ =\n'
            if text.count(entry) != 1:
                raise ValueError('Expected one known native runner entry point for the trace precheck.')
            runner.write_text(text.replace(entry, entry + '    OfficialTraceProbe.verify ()\n'), encoding='utf-8')
            evidence['native_runner_precheck'] = 'OfficialTraceProbe.verify () before original entry point'
            names.insert(0, trace.name)
        items = '\n'.join(f'    <Compile Include="{filename}" />' for filename in names)
        project = output / 'Consumer.fsproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
                           '<AssemblyName>' + ('OfficialViewConsumer' if args.target == 'native' else 'Browser')
                           + '</AssemblyName><GenerateDocumentationFile>false</GenerateDocumentationFile>'
                           + '<DefineConstants>' + ('RANVIER_TRACE' if suffix else '') + '</DefineConstants>'
                           + properties + '</PropertyGroup><ItemGroup>' + items + '\n'
                           + package_references(profile, args.target, suffix) + '</ItemGroup></Project>')
        run('restore', 'dotnet', 'restore', project, '--configfile', config)
        assets_path = output / 'obj/project.assets.json'
        assets = json.loads(assets_path.read_text(encoding='utf-8'))
        verify_variant(assets['libraries'], args.variant)
        for identity, library in assets['libraries'].items():
            if library.get('type') != 'package':
                raise ValueError('Published consumers require packages only: ' + identity)
            package = output / 'packages' / library['path']
            metadata = json.loads((package / '.nupkg.metadata').read_text(encoding='utf-8'))
            if metadata.get('source', '').rstrip('/').casefold() != OFFICIAL:
                raise ValueError('Non-official package source: ' + identity)
            if identity.startswith(('Ranvier.View/', 'Ranvier.View.Traced/', 'Ranvier.View.Dom/',
                                    'Ranvier.View.Dom.Traced/', 'Ranvier.View.WebCore/', 'Ranvier.View.WebCore.Traced/')):
                verify_nuspec(next(package.glob('*.nuspec')).read_text(encoding='utf-8-sig'), profile['source_commit'])
        if args.target == 'native':
            def verify_payload(directory):
                run('payload-' + directory.name, sys.executable, REPOSITORY / 'tools/verify-published-native-runtime.py',
                    '--assets', assets_path, '--output', directory, '--native-version', profile['native_version'],
                    '--view-package', 'Ranvier.View.WebCore' + suffix, '--view-version', profile['backend_version'],
                    '--flavor', 'slim', '--abi', str(profile['native_abi']))
            result = run('jit', 'dotnet', 'run', '--project', project, '-c', 'Release', '--no-restore')
            verify_assertions(result, args.target, args.variant, profile)
            verify_payload(output / 'bin/Release/net10.0/win-x64')
            published = output / 'published-aot'
            run('publish', 'dotnet', 'publish', project, '-c', 'Release', '--no-restore', '-o', published)
            result = run('aot', published / 'OfficialViewConsumer.exe')
            verify_assertions(result, args.target, args.variant, profile)
            verify_payload(published)
        else:
            verify_browser_assets(assets['targets'], suffix)
            tools = json.loads(source('.config/dotnet-tools.json'))
            tools['tools'] = {'fickl': tools['tools']['fickl']}
            (output / '.config').mkdir()
            (output / '.config/dotnet-tools.json').write_text(json.dumps(tools))
            run('tools', 'dotnet', 'tool', 'restore', '--configfile', config)
            for filename in ['package.json', 'package-lock.json', 'browser.mjs']:
                (output / filename).write_bytes(source(FIXTURES + filename))
            run('npm', 'npm.cmd' if os.name == 'nt' else 'npm', 'ci')
            run('chromium-install', 'npx.cmd' if os.name == 'nt' else 'npx', 'playwright', 'install', 'chromium')
            command = ['dotnet', 'fickl', project, '--outDir', output / 'generated', '--configuration', 'Release',
                       '--extension', '.js', '--noRestore', '--noPackageCache']
            if suffix:
                command += ['--define', 'RANVIER_TRACE']
            run('fable', *command)
            result = run('browser', 'node', output / 'browser.mjs')
            verify_assertions(result, args.target, args.variant, profile)
        evidence['passed'] = True
        print(f'OFFICIAL_SHARED_API PASS {args.variant} {args.target} assertions={profile["shared_assertions"]}', flush=True)
    finally:
        (output / 'evidence.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()

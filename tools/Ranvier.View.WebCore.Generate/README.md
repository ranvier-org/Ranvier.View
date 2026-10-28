# Native DOM wrappers

The generator combines the canonical `Ranvier.WebCore` package's `model/dom-api.json` and managed interop
assembly with the shared `Ranvier.Dom` contract assembly. It writes static F# wrapper implementations and
their mapping report; it does not execute native code.

Build and invoke it with these arguments:

```text
dotnet build tools/Ranvier.View.WebCore.Generate -c Release
dotnet tools/Ranvier.View.WebCore.Generate/bin/Release/net10.0/Ranvier.View.WebCore.Generate.dll <dom-api.json> <Ranvier.Dom.dll> <Ranvier.WebCore.dll> [output-directory]
```

The default output is `src/Ranvier.View.WebCore/Generated`. After restoring and building the backend from
published packages, `python tools/check-webcore-generated.py` regenerates into a temporary directory and
checks both committed outputs. CI runs this check on Windows x64. Local interop/runtime overrides are
rejected by that check.

Native roots and transport mappings come from the canonical model. Derived wrapper classes come from the
contract assembly's interface ancestry, so a compatible contract marker can use an existing native transport
without requiring a new native binary. For example, `CSSStyleProperties` inherits `CSSStyleDeclaration` and
uses its handle and release functions. Dictionary handles remain direct pointers; optional scalar and
container payloads stay pinned through their native call. Optional arguments use their IDL defaults.

The report lists unmapped public members and native exports without generated mappings. The latter includes
intentional event adapters and redundant inherited ABI exports, as well as any missing contract coverage.

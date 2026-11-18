# Toolchain

Platform: WSL, Ubuntu 24.04.4 LTS (distribution `Ubuntu`), opam 2.3.0 switch `ranvier-proofs`.
`build.sh` and `check.sh` re-execute themselves in that distribution when started from Git Bash;
set `RANVIER_WSL_DISTRO` to use another distribution name.

| Component | Version |
|---|---|
| OCaml | 4.14.2 (`ocaml-base-compiler`) |
| Rocq (`rocq-core`, `rocq-runtime`) | 9.1.0 |
| `rocq-stdlib` | 9.2.0 |
| stdpp (`rocq-stdpp`) | 1.13.0 |
| zarith | 1.14 |

`Ranvier.View` models pure state machines and tree/sibling transformations above the reactive core
(which already verifies graph and heap invariants in `ranvier-org/Ranvier`), so Iris and HeapLang
are intentionally omitted from this repository's proof dependencies.

## Reproducing the switch

```sh
opam init --bare --disable-sandboxing --no-setup
opam switch create ranvier-proofs ocaml-base-compiler.4.14.2
eval "$(opam env --switch=ranvier-proofs)"
opam repo add rocq-released https://rocq-prover.org/opam/released
opam update
opam install -j16 rocq-core.9.1.0 rocq-stdlib.9.2.0 rocq-stdpp.1.13.0 zarith.1.14
```

## Differential check

`differential/run.sh 400` compiles the committed `differential/engine.ml` with `ocamlopt` alone (no
Rocq or stdpp needed) and compares it against the compiled F# assemblies (`Ranvier.View`,
`Ranvier.View.Dom`, `Ranvier.View.Twin`, `Ranvier.View.WebCore`, `Ranvier.View.WebCore.Sdl`).

## Commands

- Build: `bash proofs/build.sh`
- Audit: `bash proofs/check.sh`
- Differential check: `bash proofs/differential/run.sh [count] [seed]`
- Run a command in the toolchain environment: `bash proofs/build.sh --run CMD ARGS`

# Correspondence

Maps each symbol in `proofs/theories/` to the production F# definitions across `src/Ranvier.View/`,
`src/Ranvier.View.Dom/`, `src/Ranvier.View.WebCore/`, and `src/Ranvier.View.WebCore.Sdl/`, and records
what is inherited from `ranvier-org/Ranvier` core proofs versus proved here.

## Boundary with `ranvier-org/Ranvier` Core Proofs

Per `.claude/rules/view-uses-core.md`, reactive graph propagation, glitch freedom, memo/effect cutoff
soundness, `Owner` scope disposal order, `KeyedProjection` / `IndexProjection` row reuse, and
`Positional.diff` edit scripts belong to `ranvier-org/Ranvier` (`proofs/theories/{Reference,Pure,Heap}`).
`Ranvier.View` proofs begin at that boundary and model the view-owned state machines above the reactive
core. HTML/SVG DSL generation and external browser/WebCore/SDL/GPU FFI calls stay covered by Twin,
Playwright, and NativeAOT suites rather than Rocq axioms.

## 1. `theories/Pure/Layout.v` — DOM Sibling Layout & Departures

| Rocq symbol | F# symbol | Notes & simplifications |
|---|---|---|
| `move_before`, `insert_at_anchor`, `remove_node` | `Mounting.insert` (`Mounting.fs:34-38`), `Row.Place` (`Mounting.fs:1438-1443`) | Models sibling list under one parent `Node`; each row is represented by a unique `nat` root id (multi-node row spans move together in `Row.MoveBefore`). |
| `place_rtl_step`, `place_rtl` | Backward placement loop in `Row.Layout` (`Mounting.fs:1533-1544`) | Structural recursion on `(row, keep)` pairs computes the exact `offset = count - 1 .. 0` traversal with running `before` anchor. |
| `lower_bound_tails`, `lis_scan`, `mark_chain`, `increasing_run` | `Mounting.increasingRun` (`Mounting.fs:626-665`) | Patience-sorting tails + predecessor links producing `bool` keep flags over non-negative source positions. |
| `common_prefix_len`, `common_suffix_len`, `is_outer_swap`, `row_layout` | `Row.Layout` (`Mounting.fs:1450-1544`) | Covers prefix/suffix window framing, pure insertion (`aStart = aEnd`), outer-pair swap (`swapped`), and LIS right-to-left placement. Staging-fragment bulk insertion (`Mounting.fs:1494-1495`) produces the identical sibling list to `move_many_before`. |
| `dep_state`, `filter_live`, `next_live_after`, `holds_between`, `retire_or_remove`, `finish_exit` | `Departures` (`Departures.fs:267-313, 414-467`) | Models `exits`, `leaving`, and `pending` weak sets over sibling ids. Safety timer (`10000 ms`) triggers `finish_exit` identically to an explicit `finish` callback. |
| `clear_between`, `release_rows`, `flush_released` | `Row.ClearBetween` (`Mounting.fs:1358-1373`), `RegionSite.FlushReleased` (`Mounting.fs:1880-1915`) | Proves `ClearBetween` fast path equals row-by-row `release_rows` when `holds_between` is false (`Departures.Holds` guard at `Mounting.fs:1895`). |
| `reflow_state`, `reflow_before`, `reflow_run_post`, `reflow_abort_post` | `ReflowEntry`, `Departures.before`, `MountState.Post` (`Departures.fs:276-283, 472-514`, `Mounting.fs:3632-3665`) | `rf_posts` models `entry.Posts`; proves `Before` runs once per open measurement, only the latest posted callback closes `Open` and runs `After`, and aborting the latest post (`FailFastCancel`, teardown, or unwinding pass) closes `Open` without running `After`. |

## 2. `theories/Pure/Hydration.v` — SSR Markers, HydrationCursor, & ViewBoundary

| Rocq symbol | F# symbol | Notes & simplifications |
|---|---|---|
| `ssr_token`, `scan_matching_end`, `logical_next` | `TemplateDom.isStartMarker`, `TemplateDom.matchingEnd`, `TemplateDom.logicalNext` (`TemplateDom.fs:207-246`) | Models `<!--[...-->` (`TokStart`), `<!--]-->` (`TokEnd`), and element/text siblings (`TokNode`). Matches `logicalNext` returning `matchingEnd` (`TokEnd :: rest`) on start markers. |
| `cursor_entry`, `restore_entries`, `held_entries`, `commit_entries` | `TemplateDom.HydrationCursor` (`Undo`, `Nest`, `Restore`, `Commit`, `HydrationCounters.held`) (`TemplateDom.fs:101-170`) | Models arbitrary state `St` (server DOM + `DocumentHead` snapshot) with nested child cursors restored in LIFO order. |
| `bstate`, `bs_expect`, `binding_step`, `binding_dispose` | `BindingState` (`started`, `rejected`, `report`, `recovered`, `released`, `ready`) (`Scope.fs:24-80`) | Models the 2-bit state word (`ReadFailed = 1`, `Waiting = 2`). |
| `boundary_model`, `bm_wait`, `bm_ready`, `bm_fail`, `bm_retry` | `ViewBoundary` (`ViewScope` implementation & retry generation fence) (`Mounting.fs:2083-2101, 2482-2565`) | Models `waiting`, `settingUp`, `revealed`, `failed`, `pending`, `wake`, and `generation`. |

## 3. `theories/Pure/Lifetime.v` — Placement & MountLifetime

| Rocq symbol | F# symbol | Notes & simplifications |
|---|---|---|
| `placement_model`, `pl_enter`, `pl_leave`, `pl_enqueue`, `pl_add`, `pl_place`, `pl_discard`, `sort_groups` | `Placement` (`Placement.fs:33-174`) | Models instance group ordering (`ordered` fast path vs stable sort by group index when `group < lastGroup`), own-before-forwarded release, and `Discard`. |
| `pass_model`, `ps_begin`, `ps_place` | `PlacementPass` (`Placement.fs:180-220`) | Models `Begun`, `Inserted` (`false` on first pass, `true` on subsequent passes), and `placed`. |
| `lifetime_tree`, `collect_teardown_sub`, `dispose_lifetime_tree` | `MountLifetime` (`Finish`, `Complete`, `RunRelease`, `Dispose`) (`Lifetime.fs:90-409`) | Captures reverse-acquisition child teardown (`Owner.DisposeScope`) and deferral of descendant `RunRelease` onto `outer.queued` so all `Owner` cleanups execute while DOM nodes remain attached. |
| `teardown_window_indices`, `disjoint_intervals` | `TeardownWindow` (`Exclude`, `Errors`) (`Lifetime.fs:34-70`) | Models root error index slicing `[start, start + len)` with nested `[first, finish)` exclusions. |

## 4. `theories/Pure/Scheduling.v` — Queued Commits, OwnerWorkQueue, & CompletionFence

| Rocq symbol | F# symbol | Notes & simplifications |
|---|---|---|
| `slot_model`, `queue_model`, `q_reserve`, `q_enqueue`, `q_cancel`, `q_flush`, `q_flush_reenq`, `q_run_posted_ticket`, `q_dispose` | `Scheduling.Slot`, `Scheduling.Queue` (`Scheduling.fs:32-268`) | Models `Slot.Version`, `Listed` pending deduplication, `generation` ticket invalidation across `Flush`/`Dispose` (`Reserve` guarded when disposed), `CollectAndContinue` vs `FailFastCancel`, and `Slot.Dropped` firing on both unreplaced snapshot commits and in-flush replacement commits. |
| `report_by_scope` | `Queue.Report` (`Scheduling.fs:219-250`) | Groups failing slots by non-null `ViewScope` in first-failure order. |
| `owq_model`, `owq_try_enqueue`, `owq_try_dequeue`, `owq_set_wake`, `owq_stop` | `OwnerWorkQueue` (`OwnerWorkQueue.fs:7-111`) | Models capacity admission (`Accepted`/`Full`/`Closed`), item conservation, and no-lost-wakeup across `TryEnqueue` and `SetWakeCallback`. |
| `fence_model`, `cf_try_complete`, `cf_reset` | `CompletionFence`, `GpuCompletionFailure` (`CompletionFence.fs:4-53`) | Models owner-thread GPU marker allocation, single release on completion or `GpuCompletionFailure`, and `Reset` guard when `pending <> 0n`. |

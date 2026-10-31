# I07/T09/T04 DataTransfer, clipboard files and range replacement (native provider)

## Core and view state

- **Core primitives used:** none. The provider and the fixture create no owners, memos, effects or projections. A view
  that handles drops or pastes reads the event's `dataTransfer` or `clipboardData` inside its listener, under the
  listener's owner, and registers `DataTransferService.Dispose` with `onCleanup`.
- **View-owned state:** the clipboard delegates of `DataTransfers.setClipboard`, kept per host in a
  `ConditionalWeakTable` so the native callbacks stay alive while the host is. They are native callback registrations,
  which the core has no equivalent for, and hold no DOM state. A DataTransfer's data lives in the engine; the wrappers
  keep only their native handle and the document they read in.

## Design

- Core `Ranvier.Dom/DataTransfers.fs`: `DataTransferService.Create` constructs a read/write DataTransfer. The shared
  interfaces (`Generated/Dom.fs`) are the Ranvier.WebCore generator's DataTransfer, DataTransferItemList,
  DataTransferItem, `DragEvent.dataTransfer`, `ClipboardEvent.clipboardData` and `InputEvent.dataTransfer`.
  `BrowserDataTransfers` constructs the realm's DataTransfer.
- `Ranvier.View.WebCore/DataTransfers.fs`: `forHost` over `rv_dom_data_transfer_create`; `drop` (`rv_drag_data_add`,
  `rv_drag_data_add_file`, `rv_host_drag`); `setClipboard`, `copy` and `paste` (`rv_clipboard_offer`,
  `rv_clipboard_offer_file`, `rv_clipboard_deny`, `rv_host_set_clipboard_file_write`); `replaceRange`
  (`rv_host_replace_range`). The generator binds a DataTransfer, item list or item to its owner's document, or to the
  host's document for one a host event carries (`Interop.transferDocument`). Placeholder kinds 160-162 are renumbered
  at batch integration.
- Drags out of the page (`rv_drag_file`, `rv_drag_image` with `setDragImage`) have no Views API yet; the native probe
  `tests/abi/data_transfer.py` covers them.

## Proof

`DataTransferChecks` runs on JIT, NativeAOT and Chromium (synthetic `DragEvent` and `ClipboardEvent` with a
constructed DataTransfer in Chromium); the native runner adds store-mode lifetime, permission, copy, reserved-type and
range-replacement checks.

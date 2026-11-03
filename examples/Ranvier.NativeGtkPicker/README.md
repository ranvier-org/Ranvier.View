# Linux GTK file picker example

This example connects a file input to an application-owned GTK 3 native chooser through
`FilePickers.install`. GTK is optional and is loaded only by this example; the renderer and its
packages do not depend on GTK. Install GTK 3 and run this example on a Linux desktop with an
available display. If GTK or the native file-picker capability is unavailable, the window says so.

The provider handles one file at a time and applies the input's extension and MIME filters.
Directory and multiple-file requests are cancelled. Its `Pump` method runs bounded, nonblocking
GLib iterations on the host thread. Install, pump and dispose the provider on that thread. Disposing
the provider or the host closes an open chooser and
invalidates its request. Applications can adapt the provider to their own event loop and dialog
parenting needs.

From the repository root, run `dotnet run --project examples/Ranvier.NativeGtkPicker` with a
compatible native WebCore package installed. The example uses the normal native host and SDL
packages configured by the repository.

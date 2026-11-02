import ctypes as C
import json
import os
from pathlib import Path
import sys
import threading
import time

runtime = Path(os.environ['IME_SDL_DLL']).resolve()
os.add_dll_directory(str(runtime.parent))
sdl = C.CDLL(str(runtime))
user = C.WinDLL('user32', use_last_error=True)
imm = C.WinDLL('imm32', use_last_error=True)

def bind(lib, name, result, arguments):
    fn = getattr(lib, name)
    fn.restype, fn.argtypes = result, arguments
    return fn

P, U, I = C.c_void_p, C.c_uint32, C.c_int
bind(sdl, 'SDL_Init', C.c_bool, [U])
bind(sdl, 'SDL_CreateWindow', P, [C.c_char_p, I, I, C.c_uint64])
bind(sdl, 'SDL_GetWindowProperties', U, [P])
bind(sdl, 'SDL_GetPointerProperty', P, [U, C.c_char_p, P])
bind(sdl, 'SDL_StartTextInput', C.c_bool, [P])
bind(sdl, 'SDL_StopTextInput', C.c_bool, [P])
bind(sdl, 'SDL_PollEvent', C.c_bool, [P])
bind(sdl, 'SDL_DestroyWindow', None, [P])
bind(sdl, 'SDL_Quit', None, [])
bind(sdl, 'SDL_GetError', C.c_char_p, [])
bind(user, 'LoadKeyboardLayoutW', P, [C.c_wchar_p, U])
bind(user, 'ActivateKeyboardLayout', P, [P, U])
bind(user, 'SetForegroundWindow', C.c_bool, [P])
bind(user, 'GetForegroundWindow', P, [])
bind(user, 'keybd_event', None, [C.c_ubyte, C.c_ubyte, U, C.c_size_t])
bind(imm, 'ImmGetContext', P, [P])
bind(imm, 'ImmSetOpenStatus', C.c_bool, [P, C.c_bool])
bind(imm, 'ImmGetOpenStatus', C.c_bool, [P])
bind(imm, 'ImmSetConversionStatus', C.c_bool, [P, U, U])
bind(imm, 'ImmReleaseContext', C.c_bool, [P, P])

class TextEvent(C.Structure):
    _fields_ = [('type', U), ('reserved', U), ('timestamp', C.c_uint64), ('window', U),
                ('text', C.c_char_p), ('start', I), ('length', I)]

result = {'runtime': str(runtime), 'events': [], 'errors': []}
output = Path(os.environ.get('IME_RESULT', 'ime171-result.json'))
window = None
try:
    if not sdl.SDL_Init(0x20):
        raise RuntimeError('SDL_Init: ' + sdl.SDL_GetError().decode(errors='replace'))
    layout = user.LoadKeyboardLayoutW('00000411', 1)
    if not layout:
        raise OSError(C.get_last_error(), 'LoadKeyboardLayoutW')
    result['layout'] = hex(layout)
    user.ActivateKeyboardLayout(layout, 0)
    window = sdl.SDL_CreateWindow(b'Ranvier IME171 runner probe', 500, 200, 0)
    if not window:
        raise RuntimeError('SDL_CreateWindow: ' + sdl.SDL_GetError().decode(errors='replace'))
    hwnd = sdl.SDL_GetPointerProperty(sdl.SDL_GetWindowProperties(window), b'SDL.window.win32.hwnd', None)
    if not hwnd:
        raise RuntimeError('SDL window HWND unavailable')
    result['foregroundGranted'] = bool(user.SetForegroundWindow(hwnd))
    if not sdl.SDL_StartTextInput(window):
        raise RuntimeError('SDL_StartTextInput: ' + sdl.SDL_GetError().decode(errors='replace'))
    context = imm.ImmGetContext(hwnd)
    result['imeContext'] = bool(context)
    if not context:
        raise RuntimeError('IME context unavailable')
    result['imeOpenSet'] = bool(imm.ImmSetOpenStatus(context, True))
    result['conversionSet'] = bool(imm.ImmSetConversionStatus(context, 9, 0))
    result['imeOpen'] = bool(imm.ImmGetOpenStatus(context))
    imm.ImmReleaseContext(hwnd, context)
    def type_keys():
        time.sleep(2)
        for key in [0x4b, 0x41, 0x4e, 0x41, 0x20, 0x20, 0x0d]:
            user.keybd_event(key, 0, 0, 0)
            user.keybd_event(key, 0, 2, 0)
            time.sleep(.25)
    sender = threading.Thread(target=type_keys)
    sender.start()
    event = C.create_string_buffer(128)
    deadline = time.monotonic() + 12
    while time.monotonic() < deadline:
        while sdl.SDL_PollEvent(event):
            entry = TextEvent.from_buffer(event)
            if entry.type in (0x302, 0x303):
                result['events'].append({'type': hex(entry.type), 'text': entry.text.decode('utf-8', errors='replace') if entry.text else '',
                                         'start': entry.start if entry.type == 0x302 else None,
                                         'length': entry.length if entry.type == 0x302 else None})
        time.sleep(.01)
    sender.join()
    result['foregroundStillOwned'] = user.GetForegroundWindow() == hwnd
    result['nonAsciiPreedit'] = any(e['type'] == '0x302' and any(ord(c) > 127 for c in e['text']) for e in result['events'])
    result['nonAsciiCommit'] = any(e['type'] == '0x303' and any(ord(c) > 127 for c in e['text']) for e in result['events'])
    result['qualified'] = result['nonAsciiPreedit'] and result['nonAsciiCommit']
except Exception as error:
    result['errors'].append(repr(error))
finally:
    if window:
        sdl.SDL_StopTextInput(window)
        sdl.SDL_DestroyWindow(window)
    sdl.SDL_Quit()
    output.write_text(json.dumps(result, indent=2, ensure_ascii=True))
    print(json.dumps(result, indent=2, ensure_ascii=True))
sys.exit(0 if result.get('qualified') else 1)

import ctypes as C
import json
import os
from pathlib import Path
import sys
import threading
import time
import uuid

runtime = Path(os.environ['IME_SDL_DLL']).resolve()
os.add_dll_directory(str(runtime.parent))
sdl = C.CDLL(str(runtime))
user = C.WinDLL('user32', use_last_error=True)
imm = C.WinDLL('imm32', use_last_error=True)
ole = C.WinDLL('ole32', use_last_error=True)

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

class Guid(C.Structure):
    _fields_ = [('a', U), ('b', C.c_uint16), ('c', C.c_uint16), ('d', C.c_ubyte * 8)]
    @classmethod
    def parse(cls, value):
        return cls.from_buffer_copy(uuid.UUID(value).bytes_le)
    def __str__(self):
        return str(uuid.UUID(bytes_le=bytes(self)))

class Profile(C.Structure):
    _fields_ = [('kind', U), ('lang', C.c_uint16), ('clsid', Guid), ('profile', Guid),
                ('category', Guid), ('substitute', P), ('caps', U), ('keyboard', P), ('flags', U)]

def com_method(obj, index, result, *arguments):
    table = C.cast(obj, C.POINTER(C.POINTER(P))).contents
    return C.WINFUNCTYPE(result, P, *arguments)(table[index])

bind(ole, 'CoInitializeEx', C.c_long, [P, U])
bind(ole, 'CoCreateInstance', C.c_long, [C.POINTER(Guid), P, U, C.POINTER(Guid), C.POINTER(P)])
bind(ole, 'CoUninitialize', None, [])

class TextEvent(C.Structure):
    _fields_ = [('type', U), ('reserved', U), ('timestamp', C.c_uint64), ('window', U),
                ('text', C.c_char_p), ('start', I), ('length', I)]

result = {'runtime': str(runtime), 'events': [], 'errors': []}
output = Path(os.environ.get('IME_RESULT', 'ime171-result.json'))
window = None
manager = P()
com_initialized = False
try:
    com_status = ole.CoInitializeEx(None, 2)
    result['comInitialize'] = hex(com_status & 0xffffffff)
    com_initialized = com_status >= 0
    if not com_initialized:
        raise RuntimeError('STA COM initialization failed')
    if not sdl.SDL_Init(0x20):
        raise RuntimeError('SDL_Init: ' + sdl.SDL_GetError().decode(errors='replace'))
    layout = user.LoadKeyboardLayoutW('00000411', 1)
    if not layout:
        raise OSError(C.get_last_error(), 'LoadKeyboardLayoutW')
    result['layout'] = hex(layout)
    user.ActivateKeyboardLayout(layout, 0)
    clsid = Guid.parse('33c53a50-f456-4884-b049-85fd643ecfed')
    iid = Guid.parse('71c6e74c-0f28-11d8-a82a-00065b84435c')
    japanese = Guid.parse('03b5835f-f03c-411b-9ce2-aa23e1171e36')
    profile_id = Guid.parse('a76c93d9-5523-4e90-aafa-4db112f9ac76')
    category = Guid.parse('34745c63-b2f0-4784-8b67-5e12c8701a31')
    status = ole.CoCreateInstance(C.byref(clsid), None, 1, C.byref(iid), C.byref(manager))
    result['profileManager'] = hex(status & 0xffffffff)
    if status < 0:
        raise RuntimeError('TSF profile manager unavailable')
    activate = com_method(manager, 3, C.c_long, U, C.c_uint16, C.POINTER(Guid), C.POINTER(Guid), P, U)
    status = activate(manager, 1, 0x411, C.byref(japanese), C.byref(profile_id), None, 0x10000005)
    result['activateProfile'] = hex(status & 0xffffffff)
    get_active = com_method(manager, 10, C.c_long, C.POINTER(Guid), C.POINTER(Profile))
    active = Profile()
    active_status = get_active(manager, C.byref(category), C.byref(active))
    result['activeProfile'] = {'status': hex(active_status & 0xffffffff), 'language': hex(active.lang),
                               'clsid': str(active.clsid), 'profile': str(active.profile), 'flags': active.flags}
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
    result['qualified'] = (active_status == 0 and str(active.clsid) == str(japanese)
                           and str(active.profile) == str(profile_id)
                           and result['nonAsciiPreedit'] and result['nonAsciiCommit'])
except Exception as error:
    result['errors'].append(repr(error))
finally:
    if window:
        sdl.SDL_StopTextInput(window)
        sdl.SDL_DestroyWindow(window)
    sdl.SDL_Quit()
    if manager:
        com_method(manager, 2, C.c_ulong)(manager)
    if com_initialized:
        ole.CoUninitialize()
    output.write_text(json.dumps(result, indent=2, ensure_ascii=True))
    print(json.dumps(result, indent=2, ensure_ascii=True))
sys.exit(0 if result.get('qualified') else 1)

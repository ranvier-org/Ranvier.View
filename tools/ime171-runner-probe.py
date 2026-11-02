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
bind(sdl, 'SDL_SetHint', C.c_bool, [C.c_char_p, C.c_char_p])
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
bind(user, 'GetKeyboardLayout', P, [U])
bind(user, 'SetForegroundWindow', C.c_bool, [P])
bind(user, 'GetForegroundWindow', P, [])
bind(user, 'keybd_event', None, [C.c_ubyte, C.c_ubyte, U, C.c_size_t])
bind(user, 'MapVirtualKeyExW', U, [U, U, P])
bind(user, 'CreateWindowExW', P, [U, C.c_wchar_p, C.c_wchar_p, U, I, I, I, I, P, P, P, P])
bind(user, 'DestroyWindow', C.c_bool, [P])
bind(user, 'SetFocus', P, [P])
bind(user, 'GetFocus', P, [])
bind(user, 'GetWindowTextLengthW', I, [P])
bind(user, 'GetWindowTextW', I, [P, C.c_wchar_p, I])
bind(user, 'SetWindowLongPtrW', C.c_ssize_t, [P, I, C.c_ssize_t])
bind(user, 'CallWindowProcW', C.c_ssize_t, [P, P, U, C.c_size_t, C.c_ssize_t])
bind(user, 'PeekMessageW', C.c_bool, [P, P, U, U, U])
bind(user, 'TranslateMessage', C.c_bool, [P])
bind(user, 'DispatchMessageW', C.c_ssize_t, [P])
bind(imm, 'ImmGetContext', P, [P])
bind(imm, 'ImmSetOpenStatus', C.c_bool, [P, C.c_bool])
bind(imm, 'ImmGetOpenStatus', C.c_bool, [P])
bind(imm, 'ImmSetConversionStatus', C.c_bool, [P, U, U])
bind(imm, 'ImmGetConversionStatus', C.c_bool, [P, C.POINTER(U), C.POINTER(U)])
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

class CandidateEvent(C.Structure):
    _fields_ = [('type', U), ('reserved', U), ('timestamp', C.c_uint64), ('window', U),
                ('candidates', C.POINTER(C.c_char_p)), ('count', I), ('selected', I), ('horizontal', C.c_bool)]

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
    result['compositionHint'] = bool(sdl.SDL_SetHint(b'SDL_IME_IMPLEMENTED_UI', b'composition'))
    if not result['compositionHint']:
        raise RuntimeError('SDL rejected the Window composition hint')
    if not sdl.SDL_Init(0x20):
        raise RuntimeError('SDL_Init: ' + sdl.SDL_GetError().decode(errors='replace'))
    window = sdl.SDL_CreateWindow(b'Ranvier IME171 runner probe', 500, 200, 0)
    if not window:
        raise RuntimeError('SDL_CreateWindow: ' + sdl.SDL_GetError().decode(errors='replace'))
    hwnd = sdl.SDL_GetPointerProperty(sdl.SDL_GetWindowProperties(window), b'SDL.window.win32.hwnd', None)
    if not hwnd:
        raise RuntimeError('SDL window HWND unavailable')
    result['foregroundGranted'] = bool(user.SetForegroundWindow(hwnd))
    user.SetFocus(hwnd)
    result['windowFocused'] = user.GetFocus() == hwnd and user.GetForegroundWindow() == hwnd
    if not result['windowFocused']:
        raise RuntimeError('SDL window did not obtain keyboard focus')
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
    status = activate(manager, 1, 0x411, C.byref(japanese), C.byref(profile_id), None, 0x10000001)
    result['activateProfile'] = hex(status & 0xffffffff)
    get_active = com_method(manager, 10, C.c_long, C.POINTER(Guid), C.POINTER(Profile))
    active = Profile()
    active_status = get_active(manager, C.byref(category), C.byref(active))
    result['activeProfile'] = {'status': hex(active_status & 0xffffffff), 'language': hex(active.lang),
                               'clsid': str(active.clsid), 'profile': str(active.profile), 'flags': active.flags}
    current_layout = user.GetKeyboardLayout(0) or 0
    result['activeHkl'] = hex(current_layout)
    result['profileVerified'] = (status == 0 and active_status == 0 and active.lang == 0x411
                                 and str(active.clsid) == str(japanese) and str(active.profile) == str(profile_id)
                                 and (active.flags & 3) == 3 and (current_layout & 0xffff) == 0x411)
    if not result['profileVerified']:
        raise RuntimeError('Japanese TIP and Japanese keyboard layout are not both active')
    layout = current_layout
    def verify_profile(stage, expected_hwnd):
        current = Profile()
        profile_status = get_active(manager, C.byref(category), C.byref(current))
        hkl = user.GetKeyboardLayout(0) or 0
        observed = {'status': hex(profile_status & 0xffffffff), 'language': hex(current.lang),
                    'clsid': str(current.clsid), 'profile': str(current.profile), 'flags': current.flags,
                    'hkl': hex(hkl), 'focused': user.GetFocus() == expected_hwnd,
                    'foreground': user.GetForegroundWindow() == expected_hwnd}
        result[stage] = observed
        if not (profile_status == 0 and current.lang == 0x411 and str(current.clsid) == str(japanese)
                and str(current.profile) == str(profile_id) and (current.flags & 3) == 3
                and (hkl & 0xffff) == 0x411 and observed['focused'] and observed['foreground']):
            raise RuntimeError(stage + ' lost the Japanese profile, layout or keyboard focus')
    if not sdl.SDL_StartTextInput(window):
        raise RuntimeError('SDL_StartTextInput: ' + sdl.SDL_GetError().decode(errors='replace'))
    context = imm.ImmGetContext(hwnd)
    result['imeContext'] = bool(context)
    if not context:
        raise RuntimeError('IME context unavailable')
    result['imeOpenSet'] = bool(imm.ImmSetOpenStatus(context, True))
    result['conversionSet'] = bool(imm.ImmSetConversionStatus(context, 9, 0))
    result['imeOpen'] = bool(imm.ImmGetOpenStatus(context))
    conversion, sentence = U(), U()
    result['conversionRead'] = bool(imm.ImmGetConversionStatus(context, C.byref(conversion), C.byref(sentence)))
    result['conversionMode'] = hex(conversion.value)
    result['sentenceMode'] = hex(sentence.value)
    imm.ImmReleaseContext(hwnd, context)
    if not (result['imeOpen'] and result['conversionRead'] and (conversion.value & 9) == 9):
        raise RuntimeError('SDL IME did not enter native Hiragana conversion mode')
    keys = [0x4b, 0x41, 0x4e, 0x41, 0x20, 0x20, 0x0d]
    result['keyScancodes'] = [{'virtualKey': hex(key), 'scanCode': hex(user.MapVirtualKeyExW(key, 0, layout))} for key in keys]
    def type_keys():
        time.sleep(2)
        for key in keys:
            scan = user.MapVirtualKeyExW(key, 0, layout)
            user.keybd_event(key, scan, 0, 0)
            user.keybd_event(key, scan, 2, 0)
            time.sleep(.25)
    verify_profile('sdlBeforeKeys', hwnd)
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
            elif entry.type == 0x307:
                candidates = CandidateEvent.from_buffer(event)
                count = max(0, min(candidates.count, 64))
                values = [candidates.candidates[i].decode('utf-8', errors='replace') if candidates.candidates[i] else ''
                          for i in range(count)] if candidates.candidates else []
                result['events'].append({'type': hex(entry.type), 'candidates': values,
                                         'selected': candidates.selected, 'horizontal': bool(candidates.horizontal)})
        time.sleep(.01)
    sender.join()
    result['foregroundStillOwned'] = user.GetForegroundWindow() == hwnd
    sdl.SDL_StopTextInput(window)
    edit = user.CreateWindowExW(0, 'EDIT', '', 0x10cf0080, 100, 100, 500, 100, None, None, None, None)
    if not edit:
        raise OSError(C.get_last_error(), 'CreateWindowExW EDIT')
    try:
        result['editImeMessages'] = []
        callback_type = C.WINFUNCTYPE(C.c_ssize_t, P, U, C.c_size_t, C.c_ssize_t)
        previous_proc = P()
        @callback_type
        def edit_proc(target, message, wparam, lparam):
            if message in (0x010D, 0x010E, 0x010F, 0x0282):
                result['editImeMessages'].append({'message': hex(message), 'wparam': hex(wparam), 'lparam': hex(lparam & 0xffffffffffffffff)})
            return user.CallWindowProcW(previous_proc, target, message, wparam, lparam)
        previous_proc = P(user.SetWindowLongPtrW(edit, -4, C.cast(edit_proc, P).value))
        if not previous_proc:
            raise OSError(C.get_last_error(), 'subclass native EDIT')
        result['editForegroundGranted'] = bool(user.SetForegroundWindow(edit))
        user.SetFocus(edit)
        result['editFocused'] = user.GetFocus() == edit
        edit_context = imm.ImmGetContext(edit)
        if not edit_context:
            raise RuntimeError('EDIT IME context unavailable')
        imm.ImmSetOpenStatus(edit_context, True)
        imm.ImmSetConversionStatus(edit_context, 9, 0)
        edit_conversion, edit_sentence = U(), U()
        result['editOpen'] = bool(imm.ImmGetOpenStatus(edit_context))
        result['editConversionRead'] = bool(imm.ImmGetConversionStatus(edit_context, C.byref(edit_conversion), C.byref(edit_sentence)))
        result['editConversionMode'] = hex(edit_conversion.value)
        result['editSentenceMode'] = hex(edit_sentence.value)
        imm.ImmReleaseContext(edit, edit_context)
        if not (result['editFocused'] and result['editOpen'] and result['editConversionRead'] and (edit_conversion.value & 9) == 9):
            raise RuntimeError('EDIT did not obtain focus and native Hiragana conversion mode')
        verify_profile('editBeforeKeys', edit)
        sender = threading.Thread(target=type_keys)
        sender.start()
        message = C.create_string_buffer(128)
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            while user.PeekMessageW(message, None, 0, 0, 1):
                user.TranslateMessage(message)
                user.DispatchMessageW(message)
            time.sleep(.01)
        sender.join()
        length = user.GetWindowTextLengthW(edit)
        text = C.create_unicode_buffer(length + 1)
        user.GetWindowTextW(edit, text, length + 1)
        result['editText'] = text.value
        result['editNonAscii'] = any(ord(c) > 127 for c in text.value)
        result['editPreedit'] = any(m['message'] == '0x10f' and (int(m['lparam'], 16) & 8) for m in result['editImeMessages'])
        result['editForegroundStillOwned'] = user.GetForegroundWindow() == edit
    finally:
        user.DestroyWindow(edit)
    result['nonAsciiPreedit'] = any(e['type'] == '0x302' and any(ord(c) > 127 for c in e['text']) for e in result['events'])
    result['nonAsciiCommit'] = any(e['type'] == '0x303' and any(ord(c) > 127 for c in e['text']) for e in result['events'])
    result['qualified'] = (result['profileVerified'] and result['editNonAscii'] and result['editPreedit'] and result['editForegroundStillOwned']
                           and result['foregroundStillOwned']
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

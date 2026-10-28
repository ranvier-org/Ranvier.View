/// SDL's scancodes, keycodes and modifiers as the DOM's KeyboardEvent code, key and modifier bits.
module internal Ranvier.View.WebCore.Sdl.Keys

open System.Text
open SDL

// RV_MODIFIER_* of the ABI header.
[<Literal>]
let private Shift = 1

[<Literal>]
let private Control = 2

[<Literal>]
let private Alt = 4

[<Literal>]
let private Meta = 8

/// The RV_MODIFIER_* bits of SDL's modifier state.
let modifiers (state: SDL_Keymod) =
    let has (flag: SDL_Keymod) =
        state &&& flag <> SDL_Keymod.SDL_KMOD_NONE

    (if has SDL_Keymod.SDL_KMOD_SHIFT then Shift else 0)
    ||| (if has SDL_Keymod.SDL_KMOD_CTRL then Control else 0)
    ||| (if has SDL_Keymod.SDL_KMOD_ALT then Alt else 0)
    ||| (if has SDL_Keymod.SDL_KMOD_GUI then Meta else 0)

let private between (scancode: SDL_Scancode) (first: SDL_Scancode) (last: SDL_Scancode) =
    int scancode >= int first
    && int scancode <= int last

let private offset (scancode: SDL_Scancode) (first: SDL_Scancode) =
    int scancode - int first

/// The KeyboardEvent.code of a physical key: where it sits on a US keyboard, whatever the layout.
let code (scancode: SDL_Scancode) : string =
    if between scancode SDL_Scancode.SDL_SCANCODE_A SDL_Scancode.SDL_SCANCODE_Z then
        "Key"
        + string (
            char (
                int 'A'
                + offset scancode SDL_Scancode.SDL_SCANCODE_A
            )
        )
    elif between scancode SDL_Scancode.SDL_SCANCODE_1 SDL_Scancode.SDL_SCANCODE_9 then
        "Digit"
        + string (1 + offset scancode SDL_Scancode.SDL_SCANCODE_1)
    elif between scancode SDL_Scancode.SDL_SCANCODE_F1 SDL_Scancode.SDL_SCANCODE_F12 then
        "F"
        + string (1 + offset scancode SDL_Scancode.SDL_SCANCODE_F1)
    elif between scancode SDL_Scancode.SDL_SCANCODE_F13 SDL_Scancode.SDL_SCANCODE_F24 then
        "F"
        + string (13 + offset scancode SDL_Scancode.SDL_SCANCODE_F13)
    elif between scancode SDL_Scancode.SDL_SCANCODE_KP_1 SDL_Scancode.SDL_SCANCODE_KP_9 then
        "Numpad"
        + string (1 + offset scancode SDL_Scancode.SDL_SCANCODE_KP_1)
    else
        match scancode with
        | SDL_Scancode.SDL_SCANCODE_0 -> "Digit0"
        | SDL_Scancode.SDL_SCANCODE_RETURN -> "Enter"
        | SDL_Scancode.SDL_SCANCODE_ESCAPE -> "Escape"
        | SDL_Scancode.SDL_SCANCODE_BACKSPACE -> "Backspace"
        | SDL_Scancode.SDL_SCANCODE_TAB -> "Tab"
        | SDL_Scancode.SDL_SCANCODE_SPACE -> "Space"
        | SDL_Scancode.SDL_SCANCODE_MINUS -> "Minus"
        | SDL_Scancode.SDL_SCANCODE_EQUALS -> "Equal"
        | SDL_Scancode.SDL_SCANCODE_LEFTBRACKET -> "BracketLeft"
        | SDL_Scancode.SDL_SCANCODE_RIGHTBRACKET -> "BracketRight"
        | SDL_Scancode.SDL_SCANCODE_BACKSLASH
        | SDL_Scancode.SDL_SCANCODE_NONUSHASH -> "Backslash"
        | SDL_Scancode.SDL_SCANCODE_SEMICOLON -> "Semicolon"
        | SDL_Scancode.SDL_SCANCODE_APOSTROPHE -> "Quote"
        | SDL_Scancode.SDL_SCANCODE_GRAVE -> "Backquote"
        | SDL_Scancode.SDL_SCANCODE_COMMA -> "Comma"
        | SDL_Scancode.SDL_SCANCODE_PERIOD -> "Period"
        | SDL_Scancode.SDL_SCANCODE_SLASH -> "Slash"
        | SDL_Scancode.SDL_SCANCODE_NONUSBACKSLASH -> "IntlBackslash"
        | SDL_Scancode.SDL_SCANCODE_CAPSLOCK -> "CapsLock"
        | SDL_Scancode.SDL_SCANCODE_PRINTSCREEN -> "PrintScreen"
        | SDL_Scancode.SDL_SCANCODE_SCROLLLOCK -> "ScrollLock"
        | SDL_Scancode.SDL_SCANCODE_PAUSE -> "Pause"
        | SDL_Scancode.SDL_SCANCODE_INSERT -> "Insert"
        | SDL_Scancode.SDL_SCANCODE_HOME -> "Home"
        | SDL_Scancode.SDL_SCANCODE_PAGEUP -> "PageUp"
        | SDL_Scancode.SDL_SCANCODE_DELETE -> "Delete"
        | SDL_Scancode.SDL_SCANCODE_END -> "End"
        | SDL_Scancode.SDL_SCANCODE_PAGEDOWN -> "PageDown"
        | SDL_Scancode.SDL_SCANCODE_RIGHT -> "ArrowRight"
        | SDL_Scancode.SDL_SCANCODE_LEFT -> "ArrowLeft"
        | SDL_Scancode.SDL_SCANCODE_DOWN -> "ArrowDown"
        | SDL_Scancode.SDL_SCANCODE_UP -> "ArrowUp"
        | SDL_Scancode.SDL_SCANCODE_NUMLOCKCLEAR -> "NumLock"
        | SDL_Scancode.SDL_SCANCODE_KP_DIVIDE -> "NumpadDivide"
        | SDL_Scancode.SDL_SCANCODE_KP_MULTIPLY -> "NumpadMultiply"
        | SDL_Scancode.SDL_SCANCODE_KP_MINUS -> "NumpadSubtract"
        | SDL_Scancode.SDL_SCANCODE_KP_PLUS -> "NumpadAdd"
        | SDL_Scancode.SDL_SCANCODE_KP_ENTER -> "NumpadEnter"
        | SDL_Scancode.SDL_SCANCODE_KP_0 -> "Numpad0"
        | SDL_Scancode.SDL_SCANCODE_KP_PERIOD -> "NumpadDecimal"
        | SDL_Scancode.SDL_SCANCODE_KP_EQUALS -> "NumpadEqual"
        | SDL_Scancode.SDL_SCANCODE_APPLICATION -> "ContextMenu"
        | SDL_Scancode.SDL_SCANCODE_LCTRL -> "ControlLeft"
        | SDL_Scancode.SDL_SCANCODE_LSHIFT -> "ShiftLeft"
        | SDL_Scancode.SDL_SCANCODE_LALT -> "AltLeft"
        | SDL_Scancode.SDL_SCANCODE_LGUI -> "MetaLeft"
        | SDL_Scancode.SDL_SCANCODE_RCTRL -> "ControlRight"
        | SDL_Scancode.SDL_SCANCODE_RSHIFT -> "ShiftRight"
        | SDL_Scancode.SDL_SCANCODE_RALT -> "AltRight"
        | SDL_Scancode.SDL_SCANCODE_RGUI -> "MetaRight"
        | _ -> "Unidentified"

// Keys whose KeyboardEvent.key is a name rather than the character they type.
let private named (scancode: SDL_Scancode) : string =
    if
        between scancode SDL_Scancode.SDL_SCANCODE_F1 SDL_Scancode.SDL_SCANCODE_F12
        || between scancode SDL_Scancode.SDL_SCANCODE_F13 SDL_Scancode.SDL_SCANCODE_F24
    then
        code scancode
    else
        match scancode with
        | SDL_Scancode.SDL_SCANCODE_RETURN
        | SDL_Scancode.SDL_SCANCODE_KP_ENTER -> "Enter"
        | SDL_Scancode.SDL_SCANCODE_ESCAPE -> "Escape"
        | SDL_Scancode.SDL_SCANCODE_BACKSPACE -> "Backspace"
        | SDL_Scancode.SDL_SCANCODE_TAB -> "Tab"
        | SDL_Scancode.SDL_SCANCODE_CAPSLOCK -> "CapsLock"
        | SDL_Scancode.SDL_SCANCODE_PRINTSCREEN -> "PrintScreen"
        | SDL_Scancode.SDL_SCANCODE_SCROLLLOCK -> "ScrollLock"
        | SDL_Scancode.SDL_SCANCODE_PAUSE -> "Pause"
        | SDL_Scancode.SDL_SCANCODE_INSERT -> "Insert"
        | SDL_Scancode.SDL_SCANCODE_HOME -> "Home"
        | SDL_Scancode.SDL_SCANCODE_PAGEUP -> "PageUp"
        | SDL_Scancode.SDL_SCANCODE_DELETE -> "Delete"
        | SDL_Scancode.SDL_SCANCODE_END -> "End"
        | SDL_Scancode.SDL_SCANCODE_PAGEDOWN -> "PageDown"
        | SDL_Scancode.SDL_SCANCODE_RIGHT -> "ArrowRight"
        | SDL_Scancode.SDL_SCANCODE_LEFT -> "ArrowLeft"
        | SDL_Scancode.SDL_SCANCODE_DOWN -> "ArrowDown"
        | SDL_Scancode.SDL_SCANCODE_UP -> "ArrowUp"
        | SDL_Scancode.SDL_SCANCODE_NUMLOCKCLEAR -> "NumLock"
        | SDL_Scancode.SDL_SCANCODE_APPLICATION -> "ContextMenu"
        | SDL_Scancode.SDL_SCANCODE_LCTRL
        | SDL_Scancode.SDL_SCANCODE_RCTRL -> "Control"
        | SDL_Scancode.SDL_SCANCODE_LSHIFT
        | SDL_Scancode.SDL_SCANCODE_RSHIFT -> "Shift"
        | SDL_Scancode.SDL_SCANCODE_LALT
        | SDL_Scancode.SDL_SCANCODE_RALT -> "Alt"
        | SDL_Scancode.SDL_SCANCODE_LGUI
        | SDL_Scancode.SDL_SCANCODE_RGUI -> "Meta"
        | _ -> null

// SDLK_SCANCODE_MASK: keycodes at or above it have no character.
[<Literal>]
let private NoCharacter = 0x40000000u

/// The KeyboardEvent.key of a key press: its name, or the character it types in the current layout with the modifiers
/// held, or "Unidentified".
let key (scancode: SDL_Scancode) (state: SDL_Keymod) : string =
    match named scancode with
    | null ->
        let keycode = uint32 (SDL3.SDL_GetKeyFromScancode (scancode, state, true))

        if
            keycode >= 0x20u
            && keycode <> 0x7Fu
            && keycode < NoCharacter
            && Rune.IsValid keycode
        then
            (Rune keycode).ToString()
        else
            "Unidentified"
    | name -> name

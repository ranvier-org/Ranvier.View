import ctypes
import os
from pathlib import Path
import sys


def inotify_descriptors():
    result = []
    for path in Path('/proc/self/fd').iterdir():
        try:
            if os.readlink(path) == 'anon_inode:inotify':
                result.append(int(path.name))
        except FileNotFoundError:
            pass
    return sorted(result)


library = ctypes.CDLL(sys.argv[1])
library.SDL_Init.argtypes = [ctypes.c_uint32]
library.SDL_Init.restype = ctypes.c_bool
library.SDL_Quit.argtypes = []
baseline = inotify_descriptors()
for cycle in range(2):
    if not library.SDL_Init(0x20):
        raise RuntimeError('SDL_Init failed')
    during = inotify_descriptors()
    library.SDL_Quit()
    after = inotify_descriptors()
    print('INOTIFY', cycle, baseline, during, after, flush=True)
    if after != baseline:
        raise AssertionError('SDL left an IBus inotify descriptor open after quit')

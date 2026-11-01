"""Shows whether a reload of a released image URL is a WebCore memory-cache hit (no handler request) or a miss."""
import ctypes
import json
import os
import struct
import sys
import zlib
from pathlib import Path

directory = Path(sys.argv[1]).resolve()
rounds = int(sys.argv[2]) if len(sys.argv) > 2 else 10
os.add_dll_directory(str(directory))
library = ctypes.CDLL(str(directory / 'RanvierWebCoreHost.dll'))
pointer, integer, u64 = ctypes.c_void_p, ctypes.c_int32, ctypes.c_uint64


def bind(name, result, arguments):
    function = getattr(library, name)
    function.restype, function.argtypes = result, arguments
    return function


def png(width, height):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    rows = b''.join(b'\x00' + b'\xff\x00\x00\xff' * width for _ in range(height))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b'')


create = bind('rv_host_create', pointer, [integer, integer])
destroy = bind('rv_host_destroy', None, [pointer])
load_html = bind('rv_host_load_html', integer, [pointer, ctypes.c_char_p])
create_element = bind('rv_document_create_element', pointer, [pointer, ctypes.c_char_p])
set_attribute = bind('rv_element_set_attribute', integer, [pointer, ctypes.c_char_p, ctypes.c_char_p])
release_node = bind('rv_node_release', None, [pointer])
cycle = bind('rv_run_loop_cycle', integer, [integer])
collect = bind('rv_collect_garbage', None, [])
release_memory = bind('rv_release_memory', None, [])
Handler = ctypes.CFUNCTYPE(None, pointer, pointer, ctypes.c_char_p)
set_handler = bind('rv_host_set_resource_handler', integer, [pointer, Handler, pointer])
finish = bind('rv_resource_load_finish', integer, [pointer, ctypes.c_char_p, integer, ctypes.c_char_p])
Completion = ctypes.CFUNCTYPE(None, pointer, integer, integer, integer)
decode = bind('rv_image_decode', integer, [pointer, pointer, Completion, pointer, ctypes.POINTER(u64)])
release = bind('rv_resource_release', integer, [u64])
image = png(3, 2)
requests = []


def serve(context, load, url):
    requests.append(url.decode())
    finish(load, image, len(image), b'image/png')


handler = Handler(serve)
outcomes = {}


def complete(context, outcome, width, height):
    outcomes[context] = (outcome, width, height)


completion = Completion(complete)
host = create(100, 100)
set_handler(host, handler, None)
load_html(host, b'<!DOCTYPE html><html><body></body></html>')
cycle(5)
tag = [0]


def decode_url(url):
    node = create_element(host, b'img')
    set_attribute(node, b'src', url)
    tag[0] += 1
    operation = u64(0)
    decode(host, node, completion, tag[0], ctypes.byref(operation))
    for _ in range(200):
        if tag[0] in outcomes:
            break
        cycle(5)
    release(operation.value)
    return node, outcomes.get(tag[0])


results = {}
for mode in ['retained', 'released', 'purged', 'evicted', 'navigated']:
    rows = []
    for n in range(rounds):
        url = f'https://cache-probe.invalid/{mode}/{n}.png'.encode()
        node, first = decode_url(url)
        before = len([r for r in requests if r == url.decode()])
        if mode != 'retained':
            release_node(node)
            node = None
        if mode == 'purged':
            collect()
            cycle(5)
            release_memory()
        if mode == 'evicted':
            for _ in range(3):
                cycle(50)
                collect()
            release_memory()
            cycle(5)
        if mode == 'navigated':
            load_html(host, b'<!DOCTYPE html><html><body></body></html>')
            cycle(5)
        again, second = decode_url(url)
        after = len([r for r in requests if r == url.decode()])
        rows.append(dict(firstRequests=before, reloadRequests=after - before, reload=second))
        release_node(again)
        if node:
            release_node(node)
    results[mode] = dict(reloadRequests=[r['reloadRequests'] for r in rows], reloadOutcomes=sorted({str(r['reload']) for r in rows}))
destroy(host)
print(json.dumps(results, indent=2))

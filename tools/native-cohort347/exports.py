"""Check actual exports in a separate process before running the real consumer."""
import argparse
import ctypes
import json
from pathlib import Path
import verify

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',type=Path,required=True)
parser.add_argument('--header',type=Path,required=True)
args=parser.parse_args()
names=verify.verify_header(args.header)
host=ctypes.CDLL(str(args.host.resolve()))
host.rv_abi_version.restype=ctypes.c_int32
if host.rv_abi_version()!=63: raise ValueError('The actual loaded host does not report ABI63.')
for name in names:
    if not hasattr(host,name): raise ValueError('The actual host is missing '+name)
print(json.dumps(dict(scope='actual ABI/export check only; Window/Host execution is separate',abi=63,exports=len(names))))

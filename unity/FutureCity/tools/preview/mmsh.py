"""MMSH: ミライフのメッシュ形式（Blender の書き出し・Unity の読み込み・プレビュー・Mono の書き出しで共通）

  char[4]  "MMSH"
  uint32   version (=1)
  uint32   flags   bit0=normals bit1=uv0 bit2=colors(RGBA8) bit3=uv1(float2) bit4=32bit index
  uint32   vertexCount
  uint32   indexCount
  uint32   submeshCount
  float32  boundsMin[3], boundsMax[3]
  float32  positions[3*vc]   （Unity 座標：左手系・Y上）
  float32  normals[3*vc]
  float32  uv0[2*vc]
  uint8    colors[4*vc]
  float32  uv1[2*vc]
  uint16/32 indices[indexCount]   （Unity の表面＝時計回り）
  submesh × n: uint32 start, uint32 count, uint8 nameLen, utf8 name
"""
import struct
import numpy as np

F_NORMALS, F_UV0, F_COLORS, F_UV1, F_IDX32 = 1, 2, 4, 8, 16


class MeshData:
    def __init__(self):
        self.pos = np.zeros((0, 3), np.float32)
        self.nrm = None
        self.uv0 = None
        self.col = None     # uint8 (n,4)
        self.uv1 = None
        self.idx = np.zeros((0,), np.uint32)
        self.submeshes = []  # (start, count, name)

    @property
    def vcount(self):
        return len(self.pos)


def write(path, m: MeshData):
    vc = len(m.pos)
    flags = 0
    if m.nrm is not None: flags |= F_NORMALS
    if m.uv0 is not None: flags |= F_UV0
    if m.col is not None: flags |= F_COLORS
    if m.uv1 is not None: flags |= F_UV1
    idx32 = vc > 65535
    if idx32: flags |= F_IDX32
    bmin = m.pos.min(axis=0) if vc else np.zeros(3)
    bmax = m.pos.max(axis=0) if vc else np.zeros(3)
    with open(path, "wb") as f:
        f.write(b"MMSH")
        f.write(struct.pack("<5I", 1, flags, vc, len(m.idx), len(m.submeshes)))
        f.write(struct.pack("<6f", *bmin, *bmax))
        f.write(np.ascontiguousarray(m.pos, np.float32).tobytes())
        if m.nrm is not None: f.write(np.ascontiguousarray(m.nrm, np.float32).tobytes())
        if m.uv0 is not None: f.write(np.ascontiguousarray(m.uv0, np.float32).tobytes())
        if m.col is not None: f.write(np.ascontiguousarray(m.col, np.uint8).tobytes())
        if m.uv1 is not None: f.write(np.ascontiguousarray(m.uv1, np.float32).tobytes())
        f.write(np.ascontiguousarray(m.idx, np.uint32 if idx32 else np.uint16).tobytes())
        for (s, c, name) in m.submeshes:
            nb = name.encode("utf-8")[:255]
            f.write(struct.pack("<2IB", s, c, len(nb)))
            f.write(nb)


def read(path) -> MeshData:
    data = open(path, "rb").read()
    assert data[:4] == b"MMSH", path
    ver, flags, vc, ic, sc = struct.unpack_from("<5I", data, 4)
    o = 4 + 20 + 24
    m = MeshData()

    def take(dtype, count):
        nonlocal o
        n = np.dtype(dtype).itemsize * count
        a = np.frombuffer(data, dtype, count, o).copy()
        o += n
        return a

    m.pos = take(np.float32, vc * 3).reshape(vc, 3)
    if flags & F_NORMALS: m.nrm = take(np.float32, vc * 3).reshape(vc, 3)
    if flags & F_UV0: m.uv0 = take(np.float32, vc * 2).reshape(vc, 2)
    if flags & F_COLORS: m.col = take(np.uint8, vc * 4).reshape(vc, 4)
    if flags & F_UV1: m.uv1 = take(np.float32, vc * 2).reshape(vc, 2)
    m.idx = take(np.uint32 if flags & F_IDX32 else np.uint16, ic).astype(np.uint32)
    for _ in range(sc):
        s, c, nl = struct.unpack_from("<2IB", data, o)
        o += 9
        name = data[o:o + nl].decode("utf-8")
        o += nl
        m.submeshes.append((s, c, name))
    return m

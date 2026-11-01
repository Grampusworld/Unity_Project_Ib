"""给 Girl 模型按部位分材质槽（由 Tools/export_girl.py 在导出前调用）。

Girl.blend 里只有一整块焊死的网格、一个空白材质、没有贴图，骨骼权重也不按部位分。
这里在绑定姿势下按几何位置把每个三角面归到 7 个部位，导出后 Unity 里是 7 个子网格，
每个子网格对应 Assets/Art/Models/Girl/Materials/ 下的一个材质（Girl.fbx.meta 里做了重映射）：

  M_Girl_Frame   相框（含背板）
  M_Girl_Void    相框里面：画布 + 从画里鼓出来的那团布，纯黑
  M_Girl_Skin    脸、脖子、手臂、手
  M_Girl_Hair    头发
  M_Girl_Dress   衣服
  M_Girl_EyeWhite / M_Girl_Iris  眼白 / 瞳孔

所有阈值都是针对 Girl.blend 当前这份网格量出来的（绑定姿势、Blender 坐标、Z 朝上、
相框在 +Y、人朝 -Y 爬出）。换了模型就要重新量。
"""
import math

import bmesh
import bpy
import mathutils
import numpy as np
from mathutils.bvhtree import BVHTree

SLOTS = ["M_Girl_Frame", "M_Girl_Void", "M_Girl_Skin", "M_Girl_Hair",
         "M_Girl_Dress", "M_Girl_EyeWhite", "M_Girl_Iris"]
# Blender 里的预览色（Unity 里用的是重映射后的材质）
PREVIEW = [(0.42, 0.30, 0.14), (0.0, 0.0, 0.0), (0.85, 0.62, 0.50), (0.03, 0.025, 0.025),
           (0.12, 0.30, 0.20), (0.85, 0.82, 0.78), (0.05, 0.02, 0.02)]
FRAME, VOID, SKIN, HAIR, DRESS, EYEW, IRIS = range(7)

# 相框平面：中心、法线(朝 +Y)、面内两轴；面内范围
FRAME_C = np.array([0.027804, 0.737897, 1.259515])
FRAME_N = np.array([0.002885, 0.999996, -0.000058])
FRAME_U = np.array([-0.034057, 0.000156, 0.99942])
FRAME_V = np.array([0.999416, -0.002881, 0.034057])
U0, U1, V0, V1 = -0.553, 0.642, -0.554, 0.464
FRAME_FRONT = -0.052   # 相框正面深度（再往前就是画里鼓出来的东西）
FRAME_BORDER = 0.068   # 相框边宽
FRAME_BACK = 0.022     # 比这更靠后的都是背板

VOID_Y = 0.40          # 鼓包从这里开始算"画里面"（加一点起伏，边缘不死板）

# 脸：从前下方 35° 的正交视角（1000px，ortho 0.8）上圈出来的脸+脖子轮廓
FACE_CAM_LOC = (0.04, -2.0, 1.45 - 2.0 * math.tan(math.radians(35)))
FACE_CAM_ROT = (math.radians(125), 0.0, 0.0)
FACE_CAM_SCALE = 0.8
FACE_POLY = [(240, 170), (330, 120), (450, 130), (560, 180), (600, 300), (600, 450),
             (620, 600), (600, 680), (540, 740), (450, 780), (380, 760), (320, 720),
             (300, 620), (280, 520), (250, 420), (230, 300)]
EYES = [(292, 264, 33, 18), (503, 283, 33, 18)]   # 像素中心 x, y, 眼白半径, 瞳孔半径

YA, YB, ZB = -0.27, -0.08, 1.33   # 头发：脸前垂下的 / 后脑勺垂下的
R_ARM = 0.09


def _inpoly(x, y, poly):
    ins = np.zeros(len(x), bool)
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        m = ((yi > y) != (yj > y)) & (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi)
        ins ^= m
        j = i
    return ins


def _segdist(P, a, b):
    ab = b - a
    t = np.clip(((P - a) @ ab) / (ab @ ab), 0, 1)
    return np.linalg.norm(P - (a + np.outer(t, ab)), axis=1)


def classify(o, arm):
    me = o.data
    nf = len(me.polygons)
    C = np.zeros((nf, 3))
    Nn = np.zeros((nf, 3))
    me.polygons.foreach_get("center", C.ravel())
    me.polygons.foreach_get("normal", Nn.ravel())
    cls = np.full(nf, DRESS)

    # 相框
    Q = C - FRAME_C
    d = Q @ FRAME_N
    pu = Q @ FRAME_U
    pv = Q @ FRAME_V
    e = np.minimum.reduce([pu - U0, U1 - pu, pv - V0, V1 - pv])
    isframe = ((d > FRAME_FRONT) & (e < FRAME_BORDER)) | (d > FRAME_BACK)

    # 画里面（鼓包）
    wob = 0.025 * np.sin(C[:, 0] * 23 + 1.3) * np.cos(C[:, 2] * 17) + 0.015 * np.sin(C[:, 0] * 61 + C[:, 2] * 47)
    isvoid = (C[:, 1] > VOID_Y + wob) & ~isframe

    bvh = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [p.vertices[:] for p in me.polygons])

    # 厚度：沿法线往里打射线，用来排除贴在脸前的发丝
    thick = np.full(nf, 1.0)
    for i in np.where(C[:, 1] < 0.12)[0]:
        p = mathutils.Vector(C[i])
        nn = mathutils.Vector(Nn[i])
        hit = bvh.ray_cast(p - nn * 0.0015, -nn, 0.2)
        thick[i] = hit[3] + 0.0015 if hit[0] is not None else 0.2

    # 脸：投影到前下方视角，在轮廓里、且能被看到
    rot = mathutils.Euler(FACE_CAM_ROT, "XYZ").to_matrix()
    R = np.array(rot)
    loc = np.array(FACE_CAM_LOC)
    head = np.where((C[:, 2] > 1.0) & (C[:, 1] < 0.05))[0]
    local = (C[head] - loc) @ R          # = R^T (p - loc)
    px = (local[:, 0] / FACE_CAM_SCALE + 0.5) * 1000
    py = (0.5 - local[:, 1] / FACE_CAM_SCALE) * 1000
    inface = np.zeros(nf, bool)
    inface[head] = _inpoly(px, py, FACE_POLY)
    tocam = R @ np.array([0, 0, 1.0])
    vis = np.zeros(nf, bool)
    tc = mathutils.Vector(tocam)
    for i in np.where(inface)[0]:
        if Nn[i] @ tocam <= 0.05:
            continue
        hit = bvh.ray_cast(mathutils.Vector(C[i]) + mathutils.Vector(Nn[i]) * 0.002, tc, 3.0)
        vis[i] = hit[0] is None
    faceskin = inface & vis & (thick > 0.02) & ~isframe & ~isvoid
    hpx = np.full(nf, -1e9)
    hpy = np.full(nf, -1e9)
    hpx[head] = px
    hpy[head] = py
    iseyew = np.zeros(nf, bool)
    isiris = np.zeros(nf, bool)
    for ex, ey, rw, ri in EYES:
        dd = np.hypot(hpx - ex, hpy - ey)
        iseyew |= (dd < rw) & vis
        isiris |= (dd < ri) & vis

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    adj = [[] for _ in range(nf)]
    for ed in bm.edges:
        lf = [f.index for f in ed.link_faces]
        if len(lf) == 2:
            a, b = lf
            adj[a].append(b)
            adj[b].append(a)
    for _ in range(2):   # 往被遮住的邻面扩两圈，边缘干净
        add = [j for i in np.where(faceskin)[0] for j in adj[i]
               if not faceskin[j] and inface[j] and thick[j] > 0.02 and Nn[j] @ tocam > -0.2]
        faceskin[add] = True

    # 手臂：到手臂骨骼的距离（这套骨骼里 *Leg/*Foot 其实也摆在手臂里）
    armd = np.full(nf, 9.0)
    upd = np.full(nf, 9.0)
    legd = np.full(nf, 9.0)
    for b in arm.data.bones:
        nm = b.name
        if not nm.startswith(("Left", "Right")) or "Shoulder" in nm or "UpLeg" in nm:
            continue
        h = np.array(arm.matrix_world @ b.head_local)
        t = np.array(arm.matrix_world @ b.tail_local)
        dd = _segdist(C, h, t)
        if "Leg" in nm or "Foot" in nm or "Toe" in nm:
            legd = np.minimum(legd, dd)
        elif nm in ("LeftArm", "RightArm"):
            upd = np.minimum(upd, dd)
        else:
            armd = np.minimum(armd, dd)
    armskin = (((armd < R_ARM) & (C[:, 2] < 1.06)) | ((upd < 0.05) & (C[:, 2] < 1.05))
               | (C[:, 2] < 0.55) | ((legd < 0.09) & (C[:, 2] < 0.74))) & ~isframe & ~isvoid

    # 头发
    headreg = (C[:, 2] > 1.22) & (C[:, 1] < -0.22)
    sleeve = ((armd < 0.145) | (upd < 0.145)) & (C[:, 2] < 1.25)
    hang = ((C[:, 1] < YA) & (C[:, 2] > 0.85) & ~sleeve) | ((C[:, 1] < YB) & (C[:, 2] > ZB))
    ishair = (headreg | hang) & ~faceskin & ~isframe & ~isvoid & ~armskin

    cls[ishair] = HAIR
    cls[faceskin | armskin] = SKIN
    cls[isvoid] = VOID
    cls[isframe] = FRAME

    # 两侧发梢顺着发丝往下长
    growable = (cls == DRESS) & (np.abs(C[:, 0]) > 0.2) & (armd > 0.11) & (upd > 0.11) & (C[:, 2] > 0.95) & (C[:, 1] < -0.21)
    front = set(np.where(cls == HAIR)[0])
    for _ in range(20):
        new = [j for i in front for j in adj[i] if growable[j] and cls[j] == DRESS]
        if not new:
            break
        cls[new] = HAIR
        front = set(new)

    # 去掉孤立的小碎面
    for _ in range(3):
        nc = cls.copy()
        for i in range(nf):
            if cls[i] in (FRAME, VOID):
                continue
            nb = [cls[j] for j in adj[i] if cls[j] in (SKIN, HAIR, DRESS)]
            if len(nb) >= 2:
                cnt = np.bincount(nb, minlength=5)
                k = cnt.argmax()
                if cnt[k] >= len(nb) and k != cls[i]:
                    nc[i] = k
        cls = nc

    cls[iseyew & (cls == SKIN)] = EYEW
    cls[isiris & (cls == EYEW)] = IRIS
    bm.free()
    return cls


def apply(o, arm):
    """在绑定姿势下分类，给网格建 7 个材质槽并分配。"""
    old = arm.data.pose_position
    arm.data.pose_position = "REST"
    bpy.context.view_layer.update()
    cls = classify(o, arm)
    arm.data.pose_position = old
    bpy.context.view_layer.update()

    me = o.data
    me.materials.clear()
    for name, col in zip(SLOTS, PREVIEW):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = (*col, 1.0)
        me.materials.append(m)
    me.polygons.foreach_set("material_index", cls.astype(np.int32))
    me.update()
    print("[girl_colors] faces per slot:", dict(zip(SLOTS, np.bincount(cls, minlength=7).tolist())))

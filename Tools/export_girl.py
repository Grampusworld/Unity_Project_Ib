"""把 Blender/Unity_Project_Ib/Girl.blend 导出成 Unity 用的 Assets/Art/Models/Girl/Girl.fbx。

用法（不会改动 .blend 本身，只在内存里处理后导出）：
  /Applications/Blender.app/Contents/MacOS/Blender -b <Girl.blend> --python Tools/export_girl.py -- <out.fbx>

处理：
- 只导出骨骼和网格：.blend 里自带的 Camera / Light 导进 Unity 会抢走玩家画面。
- 按部位分 7 个材质槽（Tools/girl_colors.py），Unity 里重映射到 Assets/Art/Models/Girl/Materials/。
- 爬行动作原地化：原动作一圈会整体往前爬 ~2.5 m，循环时会"瞬移"回原点；
  这里把 Hips 在水平面上的线性位移从 Armature 物体上抵消掉，保留身体的左右/上下起伏。
  移动交给 NavMeshAgent。
"""
import sys

import bpy

out_path = sys.argv[sys.argv.index("--") + 1]
scene = bpy.context.scene

for o in list(bpy.data.objects):
    if o.type not in {"ARMATURE", "MESH"}:
        bpy.data.objects.remove(o, do_unlink=True)

arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")

# 按部位分材质槽（相框/画里黑/皮肤/头发/衣服/眼睛），见 Tools/girl_colors.py
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import girl_colors  # noqa: E402

model = next(o for o in bpy.data.objects if o.type == "MESH")
girl_colors.apply(model, arm)
action = arm.animation_data.action
f0, f1 = int(action.frame_range[0]), int(action.frame_range[1])
scene.frame_start, scene.frame_end = f0, f1


def hips_xy(frame):
    scene.frame_set(frame)
    p = arm.matrix_world @ arm.pose.bones["Hips"].head
    return p.x, p.y


x0, y0 = hips_xy(f0)
x1, y1 = hips_xy(f1)
print("[export_girl] Hips drift over clip: dx=%.3f dy=%.3f" % (x1 - x0, y1 - y0))

base = arm.location.copy()
for f in range(f0, f1 + 1):
    t = (f - f0) / float(f1 - f0)
    arm.location = (base.x - (x1 - x0) * t, base.y - (y1 - y0) * t, base.z)
    arm.keyframe_insert("location", frame=f)

xa, ya = hips_xy(f0)
xb, yb = hips_xy(f1)
print("[export_girl] after: dx=%.3f dy=%.3f" % (xb - xa, yb - ya))

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=False,
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    bake_anim=True,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    path_mode="COPY",
    embed_textures=True,
    add_leaf_bones=False,
)
print("[export_girl] wrote", out_path)

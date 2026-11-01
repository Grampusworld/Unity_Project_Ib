"""第一人称手电筒（Ib 美术 2026-11-01）→ Assets/Art/Models/Props/Flashlight_Viewmodel.fbx
用法：/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Tools/build_flashlight_viewmodel.py -- <out.fbx>
Blender 里灯头朝 +Y，原点在握持处（手心中心）；总长 22 cm（尾盖 y=-0.075 → 镜片 y=+0.145）。
物体/材质槽：FL_Body(Metal) FL_Grip(Rubber) FL_Lens(Lens) FL_Reflector(Chrome) FL_Button(Rubber)
只做手电本体，不含手（老大 2026-11-01 要求第一人称只显示手电）。
Unity 侧由 Assets/Editor/FlashlightViewmodelSetup.cs 做成 prefab（统一把灯头转到 +Z、加 LightMount）。
"""
import sys, math
import bpy, bmesh
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
mats = {}


def mat(name, rgb, metal=0.0, rough=0.5, emit=None):
    if name in mats:
        return mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    mats[name] = m
    return m


def lathe(name, profile, material, segs=32):
    """profile: [(r, y)]，绕 Y 轴旋转成实体。"""
    bm = bmesh.new()
    rings = []
    for r, y in profile:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring.append(bm.verts.new((r * math.cos(a), y, r * math.sin(a))))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    for ring, rev in ((rings[0], False), (rings[-1], True)):
        if ring[0].co.x != 0 or ring[0].co.z != 0:
            bm.faces.new(ring[::-1] if not rev else ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(material)
    return ob


M_metal = mat("FL_Metal", (0.08, 0.085, 0.09), 0.85, 0.38)
M_rubber = mat("FL_Rubber", (0.025, 0.025, 0.025), 0.0, 0.85)
M_chrome = mat("FL_Chrome", (0.9, 0.9, 0.9), 1.0, 0.08)
M_lens = mat("FL_Lens", (1.0, 0.93, 0.78), 0.0, 0.05)

R = 0.0155   # 筒身半径 → 直径 3.1 cm
body = lathe("FL_Body", [
    (0.0, -0.075), (0.012, -0.075), (0.0165, -0.073), (0.0168, -0.062),   # 尾盖
    (R, -0.060), (R, 0.075),                                               # 筒身
    (0.0175, 0.085), (0.0225, 0.112), (0.0235, 0.118), (0.0235, 0.141),    # 喇叭口灯头
    (0.0215, 0.145), (0.019, 0.143),                                       # 前沿卷边
], M_metal)
grip = lathe("FL_Grip", [(R + 0.0012 * (k % 2), -0.045 + 0.0055 * k) for k in range(19)], M_rubber)
refl = lathe("FL_Reflector", [(0.004, 0.122), (0.0105, 0.128), (0.0175, 0.138), (0.019, 0.1425)], M_chrome)
lens = lathe("FL_Lens", [(0.0, 0.1415), (0.019, 0.1415)], M_lens)
# 开关按钮
bpy.ops.mesh.primitive_cylinder_add(radius=0.0045, depth=0.006, vertices=16, location=(0, 0.062, R + 0.002))
btn = bpy.context.object
btn.name = "FL_Button"
btn.data.materials.append(M_rubber)


# 灯头位置标记（Unity 侧以此生成 LightMount）
lm = bpy.data.objects.new("LightMount", None)
lm.location = (0, 0.146, 0)
bpy.context.collection.objects.link(lm)

root = bpy.data.objects.new("Flashlight", None)
bpy.context.collection.objects.link(root)
for o in list(bpy.context.collection.objects):
    if o is not root:
        o.parent = root

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         bake_space_transform=True, axis_forward="-Z", axis_up="Y", object_types={"EMPTY", "MESH"},
                         mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False)
# 预览图
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
bpy.context.collection.objects.link(cam)
cam.location = (0.28, -0.12, 0.16)
cam.rotation_euler = (Vector((0, 0.035, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 50
bpy.context.scene.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 4
sun.rotation_euler = (0.6, 0.3, 0.8)
bpy.context.collection.objects.link(sun)
w = bpy.data.worlds.new("w"); w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.35, 0.38, 1)
bpy.context.scene.world = w
s = bpy.context.scene
s.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
s.render.resolution_x, s.render.resolution_y = 800, 600
s.render.filepath = "/tmp/Flashlight_Viewmodel_preview.png"   # 不往 Assets 里丢图
bpy.ops.render.render(write_still=True)
print("EXPORTED", out)

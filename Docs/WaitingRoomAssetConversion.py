"""Convert an AssetHub GLB to a textured Unity FBX.

Usage: blender -b --python WaitingRoomAssetConversion.py -- input.glb output-directory asset-name
"""

from pathlib import Path
import bpy
import sys


args = sys.argv[sys.argv.index("--") + 1:]
source, output, name = Path(args[0]), Path(args[1]), args[2]
output.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError(f"No mesh in {source}")

# Bake the GLB hierarchy into one mesh. Unity can then size and place this prop
# without depending on importer-created root rotations or empty parents.
bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.convert(target="MESH")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if len(meshes) > 1:
    bpy.ops.object.join()
mesh = bpy.context.view_layer.objects.active
mesh.name = name
mesh.data.name = name
meshes = [mesh]

corners = [vertex.co for vertex in mesh.data.vertices]
min_x, max_x = min(v.x for v in corners), max(v.x for v in corners)
min_y, max_y = min(v.y for v in corners), max(v.y for v in corners)
min_z = min(v.z for v in corners)
centre_x, centre_y = (min_x + max_x) / 2, (min_y + max_y) / 2
for vertex in mesh.data.vertices:
    vertex.co.x -= centre_x
    vertex.co.y -= centre_y
    vertex.co.z -= min_z

faces = sum(len(obj.data.polygons) for obj in meshes)
if faces > 6000:
    ratio = 5900 / faces
    for obj in meshes:
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("Game mesh reduction", "DECIMATE")
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)

colors = [image for image in bpy.data.images if image.name.lower().startswith("color_")]
if not colors:
    raise RuntimeError(f"No base-color texture in {source}")
color = colors[0]
if color.packed_file:
    (output / "Color.png").write_bytes(bytes(color.packed_file.data))
else:
    color.filepath_raw = str(output / "Color.png")
    color.file_format = "PNG"
    color.save()

bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.export_scene.fbx(
    filepath=str(output / f"{name}.fbx"),
    use_selection=True,
    object_types={"MESH"},
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    bake_space_transform=True,
    mesh_smooth_type="FACE",
    path_mode="STRIP",
)
print("EXPORTED", output / f"{name}.fbx", "faces", sum(len(obj.data.polygons) for obj in meshes))

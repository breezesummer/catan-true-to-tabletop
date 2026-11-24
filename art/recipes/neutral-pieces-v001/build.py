"""通过 Blender MCP 重建正式中立棋子。单件入口与合件场景共用两份现行配方。"""
from pathlib import Path
from tempfile import TemporaryDirectory
import runpy
import bpy

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
SELECTION=(
    ("robber-band","roadside-bandits",not globals().get("merchant_only",False)),
    ("peddler-stall","travelling-merchant",not globals().get("robber_only",False)),
)


def build_neutral():
    selected=[(name,recipe) for name,recipe,enabled in SELECTION if enabled]
    report={}
    (ROOT/".local").mkdir(exist_ok=True)
    with TemporaryDirectory(dir=ROOT/".local") as directory:
        stage=Path(directory)
        for name,recipe in selected:
            runpy.run_path(str(HERE.parent/recipe/"build.py"))
            obj=bpy.context.view_layer.objects.active
            assert obj.name==name and obj.type=="MESH"
            obj.data.calc_loop_triangles()
            report[name]={"triangles":len(obj.data.loop_triangles),"metres":list(obj.dimensions)}
            if len(selected)==2:
                bpy.data.libraries.write(str(stage/(name+".blend")),{obj},compress=True)
        if len(selected)==2:
            scene=bpy.data.scenes.new("商路中立棋子")
            bpy.context.window.scene=scene
            for old in list(bpy.data.scenes):
                if old!=scene:bpy.data.scenes.remove(old)
            bpy.data.orphans_purge(do_recursive=True)
            for index,(name,_) in enumerate(selected):
                with bpy.data.libraries.load(str(stage/(name+".blend"))) as (_,loaded):
                    loaded.objects=[name]
                obj=loaded.objects[0]
                scene.collection.objects.link(obj)
                obj.location.x=(-.016,.016)[index]
            bpy.data.libraries.write(str(HERE/"商路棋子.blend"),{scene},compress=True)
    return report


result=build_neutral()

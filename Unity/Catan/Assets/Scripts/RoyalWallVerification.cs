using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Catan.Core;
using UnityEngine;
using M5=Catan.Core.M5;

public sealed partial class TabletopUiVerification
{
    // 同一盘面取两帧，城址及相连道路来自存档。四种建筑只排入供绘制的公开棋子列表。
    IEnumerator VerifyRoyalWall()
    {
        var game=GetComponent<M5App>();
        var board=game.VerificationBoard;
        var view=game.M6View;
        var state=view.Board;
        Require(app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster && app.Fishing &&
            app.ScenarioId=="through-the-desert","城墙对照的开局配置不符");
        var cities=state.Cities.GroupBy(c=>c.PlayerId).OrderBy(group=>group.Key).Select(group=>group.First()).ToArray();
        Require(cities.Length==4,"对照存档需要四席各一座城市");
        var metros=cities.Skip(1).Select((c,i)=>new M5.Metropolis{LocationId=c.LocationId,PlayerId=c.PlayerId,Track=(M5.ImprovementTrack)i}).ToArray();
        string[] names={"哥特王城","哥特商会","冠冕议政宫","观星学宫"};
        void Project(PiecePlacement[] walls) => board.RefreshProjectedPieces(state.Settlements,state.Roads,state.Cities,
            state.RobberTileId,walls,state.Knights,metros,view.MerchantTileId,state.Ships,state.PirateTileId,
            view.BarbarianPosition,view.ThreeKingdoms?.BoardAssets);
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        SetTop(false);
        Table.Frame(BoardCamera,false,TabletopCameraModel.DefaultZoom);
        Table.SetPitch((int)TabletopCameraModel.DefaultAngle,false);
        Table.ResetPan();
        Project(System.Array.Empty<PiecePlacement>());
        yield return new WaitForSeconds(1);
        yield return new WaitForEndOfFrame();
        DetailFrame(Vector3.zero,"默认视距-筑墙前",full:true,matchCameraMsaa:true);
        Project(cities);
        yield return null;
        yield return new WaitForEndOfFrame();
        Require(Table.ZoomMultiplier==TabletopCameraModel.DefaultZoom &&
            Table.Pitch==(int)TabletopCameraModel.DefaultAngle,"拍摄未使用默认镜头");
        DetailFrame(Vector3.zero,"默认视距-筑墙后",full:true,matchCameraMsaa:true);

        Mesh shared=null;
        Material sharedStone=null;
        var rows=new StringBuilder("建筑,玩家,屏幕中心X,屏幕中心Y,缩放,俯角,城墙三角面各档\n");
        for(int i=0;i<cities.Length;i++)
        {
            var city=cities[i];
            var root=board.CommittedPieces.Cast<Transform>().Single(t=>t.gameObject.activeInHierarchy &&
                (t.name.StartsWith("City with wall ")||t.name.StartsWith("Metropolis ")) &&
                t.name.Contains(city.LocationId+" ["));
            var wall=PlayerComponentAssets.Model(PlayerComponentAssets.BuildingResource("city-wall",city.PlayerId));
            var mesh=wall.GetComponentInChildren<MeshFilter>().sharedMesh;
            var filters=root.GetComponentsInChildren<MeshFilter>();
            var wallFilter=filters.Single(f=>f.sharedMesh==mesh);
            var stone=wallFilter.GetComponent<MeshRenderer>();
            var materials=stone.sharedMaterials;
            if(i==0){shared=mesh;sharedStone=materials[0];}
            Require(filters.Length==2 && mesh==shared && mesh.lodCount>1 && mesh.subMeshCount==2 &&
                mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1),"城墙缺少共享网格、天光或远景细节");
            Require(stone.enabled && !stone.forceRenderingOff && materials[0]==sharedStone &&
                !materials[0].IsKeywordEnabled("_CITY_TINTED_SLATE") && materials[1].IsKeywordEnabled("_CITY_TINTED_SLATE") &&
                materials[1].GetColor("_BaseColor")==BoardRenderer.SeatColor(city.PlayerId),"石料或旗面归属色错误");
            Require(materials.All(m=>m.shader.isSupported && m.mainTexture.name=="wall-surface" &&
                m.GetTexture("_BumpMap")?.name=="wall-normal" && m.GetTexture("_MetallicGlossMap")?.name=="wall-finish"),
                "城墙缺少固有色、法线或表面材质");
            var insert=PlayerComponentAssets.Model(PlayerComponentAssets.BuildingResource(i==0?"city":"metropolis",city.PlayerId,i-1));
            var bodyMesh=insert.GetComponentInChildren<MeshFilter>().sharedMesh;
            Require(filters.Any(f=>f.sharedMesh==bodyMesh),"墙内建筑不符");
            Require(root.GetComponentsInChildren<Collider>().Length==0,"外墙遮挡棋盘交互");
            if(i>0)CheckRoofLight(root.gameObject,(M5.ImprovementTrack)(i-1));
            var road=state.Roads.FirstOrDefault(r=>r.PlayerId==city.PlayerId && Edges.Any(e=>e.Id==r.LocationId && e.Vertices.Contains(city.LocationId)));
            var facing=Vector3.back;
            if(road!=null)
            {
                var far=Edges.Single(e=>e.Id==road.LocationId).Vertices.Single(id=>id!=city.LocationId);
                facing=board.Position(far)-board.Position(city.LocationId);
            }
            Require(Vector3.Angle(root.forward,facing)<.2f,"城门与路口朝向不符");
            var pixel=BoardCamera.WorldToScreenPoint(root.position+Vector3.up*.012f);
            var lods=Enumerable.Range(0,mesh.lodCount).Select(level=>Enumerable.Range(0,mesh.subMeshCount)
                .Sum(slot=>(long)mesh.GetIndexCount(slot,level))/3);
            rows.AppendLine(string.Join(",",names[i],city.PlayerId,Mathf.RoundToInt(1920*pixel.x/Screen.width),
                Mathf.RoundToInt(1200*(1-pixel.y/Screen.height)),Table.ZoomMultiplier,Table.Pitch,string.Join("/",lods)));
            var eye=(root.forward+.47f*root.right+Vector3.up*.78f).normalized*.17f;
            DetailFrame(root.position+Vector3.up*.010f,names[i]+"-门前",detailSize:.0225f,eyeOffset:eye,matchCameraMsaa:true);
            var preview=TabletopPointerPreview.ForCommand("BuildWall");
            if(i>0)preview.EncloseMetropolis((int)metros[i-1].Track);
            board.PreviewPiece(city.LocationId,preview,city.PlayerId,true);
            var ghost=board.VerificationPointerPreview;
            var previewMeshes=ghost.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh);
            Require(previewMeshes.Contains(mesh) && previewMeshes.Contains(bodyMesh) &&
                Vector3.Angle(ghost.transform.forward,root.forward)<.2f,"筑墙预览的城墙、城内建筑或朝向与落子不符");
            board.ClearPreview();
        }
        File.WriteAllText(Path.Combine(output,"默认镜头与模型.csv"),rows.ToString());
        board.Refresh(view);
    }
}

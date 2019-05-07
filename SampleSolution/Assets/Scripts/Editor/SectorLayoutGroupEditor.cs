using Btf.View;
using UnityEditor;
using UnityEngine;

public class SectorLayoutGroupEditor
{
	[DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
	private static void Draw(SectorLayoutGroup view, GizmoType type)
	{
		if (!view.debugConfig.enable)
		{
			return;
		}

		DrawSectorGizmos(view, 10, Color.grey);
		DrawSphereGizmos(view.centerMarker.transform, Color.red);
		DrawSphereGizmos(view.startMarker.transform, Color.blue);
		DrawSphereGizmos(view.endMarker.transform, Color.green);
	}

	private static void DrawSphereGizmos(Transform target, Color color)
	{
		Gizmos.color = color;
		Gizmos.DrawSphere(target.position, 1f);
	}

	private static void DrawSectorGizmos(SectorLayoutGroup view, int divideCount, Color color)
	{
		var centerV = view.centerMarker.transform.localPosition;
		var startV = view.startMarker.transform.localPosition - centerV;

		//始点へのベクトルを極座標変換する
		var startR = startV.magnitude;
		var startTheta = Mathf.Acos(startV.z / startR);
		var startPhi = Mathf.Atan2(startV.y, startV.x);

		var endV = (view.endMarker.transform.localPosition - centerV).normalized * startR;

		//終点へのベクトルを極座標変換する
		var endR = endV.magnitude;
		var endTheta = Mathf.Acos(endV.z / endR);
		var endPhi = Mathf.Atan2(endV.y, endV.x);

		//始点と終点の角度を求め、子供一人あたりの差分を決める
		//3次元なので角度も2つ
		var thetaDelta = (endTheta - startTheta) / divideCount;
		var phiDelta = (endPhi - startPhi) / divideCount;

		//左詰め配置ではなく均一配置がしたいので、初期値には delta / 2を足す
		var thetaCursor = startTheta + thetaDelta / 2;
		var phiCursor = startPhi + phiDelta / 2;

		//SectorLayoutGroupの仕様上、三角形の数は分割数 + 1 になる
		var triangleCount = divideCount + 1;

		//三角形の線分の数なので + 2 （両端以外の線分は重なる）
		var verticesCount = triangleCount + 2;

		var vertices = new Vector3[verticesCount];
		var indices = new int[triangleCount * 6];

		//扇形の中心
		vertices[0] = centerV;

		//扇形の始まりの線分
		vertices[1] = startV + centerV;

		//極座標に従って線分をずらしていく
		for (var i = 0; i < triangleCount; i++)
		{
			//扇形に均一に配置するよう位置を作成する
			var position = new Vector3(
				startR * Mathf.Sin(thetaCursor) * Mathf.Cos(phiCursor)
			  , startR * Mathf.Sin(thetaCursor) * Mathf.Sin(phiCursor)
			  , startR * Mathf.Cos(thetaCursor)
			);

			vertices[i + 2] = position + centerV;

			thetaCursor += thetaDelta;
			phiCursor += phiDelta;
		}

		vertices[verticesCount - 1] = endV + centerV;

		for (var i = 0; i < triangleCount; i++)
		{
			var offset = 3 * i;
			indices[offset + 1] = i + 1;
			indices[offset + 2] = i + 2;

			indices[3 * triangleCount + offset + 1] = i + 2;
			indices[3 * triangleCount + offset + 2] = i + 1;
		}

		var mesh = new Mesh { vertices = vertices, triangles = indices };

		Gizmos.color = color;
		mesh.RecalculateNormals();
		Gizmos.DrawMesh(mesh, view.transform.position, view.transform.rotation);
	}
}
using System;
using System.Collections.Generic;
using UniRx;
using UnityEditor;
using UnityEngine;

namespace Niwatly
{
	/**
	 * 子供を扇形に沿って並べるLayoutGroup
	 *
	 * 扇形の定義は、Start, End, Centerの3つのGameObjectの位置を変更することで行う
	 *
	 * 子供となるGameObjectの位置は、扇形の円周に対して均一に並ぶように調整される
	 */
	public sealed partial class SectorLayoutGroup : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("扇形の中心点となるGameObject\n扇形の位置を決めるために使用される")]
		public GameObject centerMarker;

		[SerializeField]
		[Tooltip("扇形の始点となるGameObject\n扇形の半径を決めるために使用される")]
		public GameObject startMarker;

		[SerializeField]
		[Tooltip("扇形の終点となるGameObject\n扇形の角度を決めるために使用される")]
		public GameObject endMarker;

		[SerializeField]
		[Tooltip("子供の回転方向\n指定された方向に子供の頭が向く用に回転ルートが決まる\n\n子供の向きや角度が思ったように行かないときに変更すると良い")]
		public HeadDirection childHeadDirection = HeadDirection.Up;

		[SerializeField]
		[Range(0, 360)]
		[Tooltip("子供の回転方向のオフセット（x軸）\n\n子供の向きや角度が思ったように行かないときに変更すると良い")]
		public int childXOffset;

		[SerializeField]
		[Range(0, 360)]
		[Tooltip("子供の回転方向のオフセット（y軸）\n\n子供の向きや角度が思ったように行かないときに変更すると良い")]
		public int childYOffset;

		[SerializeField]
		[Range(0, 360)]
		[Tooltip("子供の回転方向のオフセット（z軸）\n\n子供の向きや角度が思ったように行かないときに変更すると良い")]
		public int childZOffset;

		[SerializeField]
		public AnimationConfig animationConfig;

		[SerializeField]
		public DebugConfig debugConfig;

		//アニメーション管理用のストリーム
		//再生中に次のアニメーションが要求された時、古いほうをキャンセルさせたい
		private readonly Dictionary<int, IDisposable> _interpolatorDisposables = new Dictionary<int, IDisposable>();

		private void Awake()
		{
			//子供の数が変化したらレイアウトし直す
			transform.ObserveEveryValueChanged(x => x.childCount)
			   .Subscribe(x =>
				{
					AlignChildren();
				})
			   .AddTo(this)
				;
		}

		private void OnDestroy()
		{
			foreach (var disposable in _interpolatorDisposables.Values)
			{
				disposable.Dispose();
			}
		}

		/**
		 * 対象となる子供を取得する
		 *
		 * 子供には位置取り用の Start, End, Center が含まれているのでそれらを除外したい
		 */
		public IEnumerable<Transform> GetTargetChildren()
		{
			for (var i = 0; i < transform.childCount; i++)
			{
				var child = transform.GetChild(i);

				if (!child.gameObject.activeSelf)
				{
					continue;
				}

				var childName = child.name;

				//center, start, end のGameObjectはレイアウトの対象外
				if (childName == centerMarker.name || childName == startMarker.name || childName == endMarker.name)
				{
					continue;
				}

				yield return child;
			}
		}

		private void AlignChildren()
		{
			var children = GetTargetChildren();

			//ここに来るときは center, start, end のGameObjectが必ず含まれていると仮定して -3 する
			//childrenのEnumerableを回すのは1回にとどめたい
			var childrenCount = gameObject.transform.childCount - 3;

			if (childrenCount == 0)
			{
				//対象となる子供はいないので終了する
				return;
			}

			if (debugConfig.freeze)
			{
				//フリーズが指定されているので、回転情報を初期化して終了する
				//位置情報も初期化したい？
				foreach (var child in children)
				{
					child.localRotation = Quaternion.identity;
				}

				return;
			}

			//始点へのベクトルと終点へのベクトルを作る
			//
			//Note: 角度算出のための極座標変換に向けて、中心点からのベクトルとする
			var centerV = centerMarker.transform.localPosition;
			var startV = startMarker.transform.localPosition - centerV;

			//始点へのベクトルを極座標変換する
			var startR = startV.magnitude;
			//xy平面上の角度をx、yz平面上の角度をyする2次元ベクトルを作成する
			var startA = new Vector2(Mathf.Acos(startV.z / startR), Mathf.Atan2(startV.y, startV.x));

			var endV = (endMarker.transform.localPosition - centerV).normalized * startR;

			//終点へのベクトルを極座標変換する
			var endR = endV.magnitude;
			//xy平面上の角度をx、yz平面上の角度をyする2次元ベクトルを作成する
			var endA = new Vector2(Mathf.Acos(endV.z / endR), Mathf.Atan2(endV.y, endV.x));

			//始点と終点の角度を求め、子供一人あたりの差分を決める
			var angleDelta = (endA - startA) / childrenCount;

			//左詰め配置ではなく均一配置がしたいので、初期値には delta / 2を足す
			var angleCursor = startA + angleDelta / 2;

			foreach (var child in children)
			{
				//扇形に均一に配置するよう位置を作成する
				var position = new Vector3(
						startR * Mathf.Sin(angleCursor.x) * Mathf.Cos(angleCursor.y)
					  , startR * Mathf.Sin(angleCursor.x) * Mathf.Sin(angleCursor.y)
					  , startR * Mathf.Cos(angleCursor.x)
					)
					;

				//計算開始時にCenterからのベクトルに変換しているので、ここで戻す
				var newPosition = position + centerV;

				//Centerを向くよう回転を作成する
				//他の点も向きたい？
				var lookAtPosition = centerV;

				var newRotation = Quaternion.LookRotation(lookAtPosition - newPosition, childHeadDirection.ToVector());

				//Inspector上で指定されたOffsetを適用する
				newRotation *= Quaternion.Euler(childXOffset, childYOffset, childZOffset);

				//位置情報と回転情報を更新
				SetPositionAndRotation(new LayoutData(child, newPosition, newRotation));

				//角度カーソルを次に進める
				angleCursor += angleDelta;
			}
		}

		private void SetPositionAndRotation(LayoutData layoutData)
		{
			if (_interpolatorDisposables.TryGetValue(layoutData.id, out var disposable))
			{
				//再生中のアニメーションがあればキャンセルする
				disposable.Dispose();
			}

			//1. 指定されたcountとintervalで、位置の変更を線形補間したい
			var startAnimation = Observable.IntervalFrame(animationConfig.frameInterval)
					//2. 流れてくる値を指定された最大個数で正規化する
				   .Select(x => (float) x / animationConfig.frameCount)
					//3. 値の総数が最大個数を超えたら完了する（アニメーション終了）
				   .TakeWhile(x => x <= animationConfig.frameCount)
				;

			//アニメーションが行えない場合は位置の変更を即座に行いたい
			var skipAnimation = Observable.Create<float>(observer =>
					{
						if (!animationConfig.IsValid)
						{
							//「アニメーションを利用しない」がONになっているので即座に移動する
							observer.OnNext(1f);
						}

						#if UNITY_EDITOR
						if (!EditorApplication.isPlayingOrWillChangePlaymode)
						{
							//Editor上で編集中なので即座に移動する
							//扇形の形状を変更中などの値の更新頻度が高いケースにおいて変な感じになるのを避けたい
							observer.OnNext(1f);
						}
						#endif

						return Disposable.Empty;
					})
				   .First()
				;

			var newDisposable = Observable.Return(layoutData)
			   .CombineLatest(
					Observable.Amb(startAnimation, skipAnimation)
				  , (data, cursor) => (data, cursor)
				)
				//対称のGameObjectが破棄されたら完了する（アニメーション終了）
			   .TakeUntilDestroy(layoutData.target)
			   .Subscribe(x =>
				{
					//子供の位置と回転を更新する
					x.data.Interpolate(x.cursor, animationConfig.useSlerp);
				});

			_interpolatorDisposables[layoutData.id] = newDisposable;
		}

		/**
		 * 位置と回転の更新に必要なデータのデータクラス
		 */
		private struct LayoutData
		{
			public readonly int id;
			public readonly Transform target;
			private readonly Vector3 _oldPosition;
			private readonly Vector3 _newPosition;
			private readonly Quaternion _oldRotation;
			private readonly Quaternion _newRotation;

			public LayoutData(Transform target, Vector3 newPosition, Quaternion newRotation)
			{
				id = target.gameObject.GetInstanceID();
				_oldPosition = target.localPosition;
				_oldRotation = target.localRotation;
				_newPosition = newPosition;
				_newRotation = newRotation;
				this.target = target;
			}

			public void Interpolate(float cursor, bool useSlerp)
			{
				target.localPosition = useSlerp
						? Vector3.Slerp(_oldPosition, _newPosition, cursor)
						: Vector3.Lerp(_oldPosition, _newPosition, cursor)
					;

				target.localRotation = useSlerp
						? Quaternion.Slerp(_oldRotation, _newRotation, cursor)
						: Quaternion.Lerp(_oldRotation, _newRotation, cursor)
					;
			}
		}

		/**
		 * アニメーションに関する設定値のデータクラス
		 */
		[Serializable]
		public class AnimationConfig
		{
			[Tooltip("アニメーションを利用しない")]
			public bool disable;

			[Tooltip("アニメーションの実行に使用するフレーム数")]
			public int frameCount = 10;

			[Tooltip("アニメーションを実行するフレーム間隔")]
			public int frameInterval = 1;

			[Tooltip("アニメーションにVector3.Slerpを使うかどうか\nfalseの場合、Vector3.Lerpを使用する")]
			public bool useSlerp;

			public bool IsValid => !disable && frameCount > 0 && frameInterval > 0;
		}

		/**
		 * デバッグ設定に関する設定値のデータクラス
		 */
		[Serializable]
		public class DebugConfig
		{
			[Tooltip("Gizmosによるデバッグ描画を行う")]
			public bool enable;

			[Tooltip("整列時、子供の回転情報をデフォルトに戻す")]
			public bool freeze;
		}

		public enum HeadDirection
		{
			Right
		  , Up
		  , Forward
		  , Left
		  , Down
		  , Back
		}
	}

	public static class SectorLayoutGroupExtends
	{
		public static Vector3 ToVector(this SectorLayoutGroup.HeadDirection source)
		{
			switch (source)
			{
				case SectorLayoutGroup.HeadDirection.Right:
					return Vector3.right;
				case SectorLayoutGroup.HeadDirection.Up:
					return Vector3.up;
				case SectorLayoutGroup.HeadDirection.Forward:
					return Vector3.forward;
				case SectorLayoutGroup.HeadDirection.Left:
					return Vector3.left;
				case SectorLayoutGroup.HeadDirection.Down:
					return Vector3.down;
				case SectorLayoutGroup.HeadDirection.Back:
					return Vector3.back;
				default:
					throw new ArgumentOutOfRangeException(nameof(source), source, null);
			}
		}
	}

	#if UNITY_EDITOR
	/**
	 * Editor編集時、パラメータの変更を検知してレイアウトを命令する
	 */
	public partial class SectorLayoutGroup
	{
		private IDisposable _disposable;

		private void OnValidate()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				return;
			}

			if (centerMarker == null)
			{
				Debug.LogError($"{gameObject.name}(SectorLayoutGroup) requires `Center` GameObject.");
				return;
			}

			if (startMarker == null)
			{
				Debug.LogError($"{gameObject.name}(SectorLayoutGroup) requires `Start` GameObject.");
				return;
			}

			if (endMarker == null)
			{
				Debug.LogError($"{gameObject.name}(SectorLayoutGroup) requires `End` GameObject.");
				return;
			}

			//直前に行っていたパラメータの変更をキャンセルする
			_disposable?.Dispose();
			var centerPositionChanged = centerMarker
				   .ObserveEveryValueChanged(x => x.transform.position)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;
			var startPositionChanged = startMarker
				   .ObserveEveryValueChanged(x => x.transform.position)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;
			var endPositionChanged = endMarker
				   .ObserveEveryValueChanged(x => x.transform.position)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;
			var childrenCountChanged = transform.ObserveEveryValueChanged(x => x.childCount)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;

			//背景: 位置取り用GameObjectのpositionが変更されたらレイアウトし直したい
			//問題: Editor拡張で実装する方法がわからない
			//対応: UniRxで値の変更を監視する
			//FIXME: コスパ悪い

			//子供の数が変わったらレイアウトし直したい。LayoutGroupを継承すれば実装は可能だが
			//どうせUniRxを使ってるなら、継承せずにchildCountの変更を監視する
			_disposable = Observable.Merge(
						centerPositionChanged
					  , startPositionChanged
					  , endPositionChanged
					  , childrenCountChanged
					)
				   .Subscribe(x =>
					{
						AlignChildren();
					})
				;

			//現在のパラメータでレイアウトを命令する
			AlignChildren();
		}
	}

	public class SectorLayoutGroupEditor
	{
		[DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
		private static void DrawGizmos(SectorLayoutGroup view, GizmoType type)
		{
			if (!view.debugConfig.enable)
			{
				return;
			}

			const int divideCount = 10;

			//始点へのベクトルと終点へのベクトルを作る
			//
			//Note: 角度算出のための極座標変換に向けて、中心点からのベクトルとする
			var centerV = view.centerMarker.transform.localPosition;
			var startV = view.startMarker.transform.localPosition - centerV;

			//始点へのベクトルを極座標変換する
			var startR = startV.magnitude;
			//xy平面上の角度をx、yz平面上の角度をyする2次元ベクトルを作成する
			var startA = new Vector2(Mathf.Acos(startV.z / startR), Mathf.Atan2(startV.y, startV.x));

			var endV = (view.endMarker.transform.localPosition - centerV).normalized * startR;

			//終点へのベクトルを極座標変換する
			var endR = endV.magnitude;
			//xy平面上の角度をx、yz平面上の角度をyする2次元ベクトルを作成する
			var endA = new Vector2(Mathf.Acos(endV.z / endR), Mathf.Atan2(endV.y, endV.x));

			//始点と終点の角度を求め、子供一人あたりの差分を決める
			var angleDelta = (endA - startA) / divideCount;

			//左詰め配置ではなく均一配置がしたいので、初期値には delta / 2を足す
			var angleCursor = startA + angleDelta / 2;

			//SectorLayoutGroupの仕様上、三角形の数は分割数 + 1 になる
			const int triangleCount = divideCount + 1;

			//三角形の頂点数なので + 2
			const int verticesCount = triangleCount + 2;
			var vertices = new Vector3[verticesCount];

			//扇形の中心の頂点
			vertices[0] = centerV;

			//扇形の始点の頂点
			vertices[1] = startV + centerV;

			//極座標に従って線分をずらしていく
			for (var i = 0; i < triangleCount; i++)
			{
				//扇形に均一に配置するよう位置を作成する
				var position = new Vector3(
						startR * Mathf.Sin(angleCursor.x) * Mathf.Cos(angleCursor.y)
					  , startR * Mathf.Sin(angleCursor.x) * Mathf.Sin(angleCursor.y)
					  , startR * Mathf.Cos(angleCursor.x)
					)
					;

				//扇形の途中の頂点
				vertices[i + 2] = position + centerV;
				angleCursor += angleDelta;
			}

			//扇形の終点の頂点
			vertices[verticesCount - 1] = endV + centerV;

			//裏面も描画したいので三角形の数の2倍のインデックスを計算する
			var indices = new int[triangleCount * 6];
			for (var i = 0; i < triangleCount; i++)
			{
				var offset = 3 * i;

				//表面
				indices[offset + 1] = i + 1;
				indices[offset + 2] = i + 2;

				//裏面
				indices[3 * triangleCount + offset + 1] = i + 2;
				indices[3 * triangleCount + offset + 2] = i + 1;
			}

			var mesh = new Mesh { vertices = vertices, triangles = indices };
			Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
			mesh.RecalculateNormals();

			//これまでの計算はすべてLocalSpaceで行われているので、親を起点として描画する
			var parentTransform = view.transform;
			Gizmos.DrawMesh(mesh, parentTransform.position, parentTransform.rotation);
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(view.centerMarker.transform.position, startR / 10);
			Gizmos.color = Color.blue;
			Gizmos.DrawSphere(view.startMarker.transform.position, startR / 10);
			Gizmos.color = Color.green;
			Gizmos.DrawSphere(view.endMarker.transform.position, startR / 10);
		}
	}

	#endif
}
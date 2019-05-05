using System;
using System.Collections.Generic;
using UniRx;
using UniRx.Async;
using UnityEditor;
using UnityEngine;

namespace Btf.View
{
	/**
	 * 子供GameObjectを扇形に沿って並べるLayoutGroup
	 *
	 * 扇形の定義は、Start, End, Centerの3つのGameObjectの位置を変更することで行う
	 *
	 * 子供となるGameObjectの位置は、扇形の円周に対して均一に並ぶように調整される
	 */
	public class SectorLayoutGroup : MonoBehaviour
	{
		#region 子供を円弧状に整列させる

		//扇形の中心。位置を定めるために必要
		[SerializeField]
		public GameObject center;

		//扇形の始点。半径を定めるために必要
		[SerializeField]
		public GameObject start;

		//扇形の終点。角度を定めるために必要
		//Note: 扇形の半径は始点から中心への距離にのみ依存する
		[SerializeField]
		public GameObject end;

		//デバッグ用の処理をONにする
		// - Debug.Logによるロギング
		// - 位置取り用GameObjectの可視化
		[SerializeField]
		public bool enableDebug;

		//子供の整列が終わった後、どの位置に向けて角度を調整するか
		[SerializeField]
		public LookAtKind lookAt;

		//位置と角度の計算を行わない
		[SerializeField]
		public bool freezing;

		private void Awake()
		{
			transform.ObserveEveryValueChanged(x => x.childCount)
			   .Subscribe(x =>
				{
					AlignChildren();
				})
			   .AddTo(this)
				;
		}

		private IEnumerable<Transform> GetTargetChildren()
		{
			for (var i = 0; i < transform.childCount; i++)
			{
				var child = transform.GetChild(i);

				var childName = child.name;

				//center, start, end のGameObjectは整列の対象外
				if (childName == center.name || childName == start.name || childName == end.name)
				{
					continue;
				}

				yield return child.transform;
			}
		}

		private void AlignChildren()
		{
			var children = GetTargetChildren();
			//ここに来るときは center, start, end のGameObjectが必ず含まれていると仮定して -3 する
			var childrenCount = gameObject.transform.childCount - 3;

			if (enableDebug)
			{
				Debug.Log($"children are {childrenCount}");
			}

			if (childrenCount == 0)
			{
				return;
			}

			if (freezing)
			{
				foreach (var child in children)
				{
					child.rotation = Quaternion.identity;
				}

				return;
			}

			//始点へのベクトルと終点へのベクトルを作る
			//Note: 計算の都合上、中心からのベクトルとして作成している
			var worldCenterV = center.transform.position;
			var startV = start.transform.position - worldCenterV;
			var endV = end.transform.position - worldCenterV;

			if (enableDebug)
			{
				Debug.Log($"start = {startV}");
				Debug.Log($"end = {endV}");
			}

			//始点へのベクトルを極座標変換する
			var startR = startV.magnitude;
			var startTheta = Mathf.Acos(startV.y / startR);
			var startPhi = Mathf.Atan2(startV.z, startV.x);

			//終点へのベクトルを曲座標変換する
			var endR = endV.magnitude;
			var endTheta = Mathf.Acos(endV.y / endR);
			var endPhi = Mathf.Atan2(endV.z, endV.x);

			//始点と終点の間の角度を求める
			//3次元なので角度も2つ
			var thetaDelta = (endTheta - startTheta) / childrenCount;
			var phiDelta = (endPhi - startPhi) / childrenCount;

			if (enableDebug)
			{
				Debug.Log($"theta: start = {startTheta * Mathf.Rad2Deg}, end = {endTheta * Mathf.Rad2Deg}, delta = {thetaDelta * Mathf.Rad2Deg}");
				Debug.Log($"phi: start = {startPhi * Mathf.Rad2Deg}, end = {endPhi * Mathf.Rad2Deg}, delta = {phiDelta * Mathf.Rad2Deg}");
			}

			//2つの角度それぞれに対してカーソルを作成する
			//左詰め配置ではなく均一配置がしたいので、初期値には delta / 2を足す
			var thetaCursor = startTheta + thetaDelta / 2;
			var phiCursor = startPhi + phiDelta / 2;

			foreach (var child in children)
			{
				//角度のカーソルを元に新しいベクトルを作成
				var position = new Vector3(
						startR * Mathf.Sin(thetaCursor) * Mathf.Cos(phiCursor)
					  , startR * Mathf.Cos(thetaCursor)
					  , startR * Mathf.Sin(thetaCursor) * Mathf.Sin(phiCursor)
					)
					;

				//計算開始時に中心からのベクトルに変換しているので、ここで戻す
				var worldPosition = position + worldCenterV;

				Vector3 at;
				switch (lookAt)
				{
					case LookAtKind.Center:
						at = worldCenterV;
						break;
					case LookAtKind.CrossProduct:
						at = Vector3.Cross(endV, startV);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}


				if (enableDebug)
				{
					Debug.Log($"{child.name}: on {worldPosition}, to {at}");
				}

				var rotation = Quaternion.FromToRotation(worldPosition, at);

				if (enableDebug)
				{
					//Debug.DrawLine(rawCenterP, worldPosition, Color.red, 2f, false);
				}

				child.SetPositionAndRotation(worldPosition, rotation);

				_laidOutGameObjectSub?.OnNext(child.name);

				//カーソルを進める
				thetaCursor += thetaDelta;
				phiCursor += phiDelta;
			}
		}

		#endregion

		#region SectorLayoutGroupを使いやすくする

		private ISubject<string> _laidOutGameObjectSub;

		/**
		 * 指定した名前のGameObjectの整列が完了するまで待つ
		 */
		public UniTask<string> WaitUntilLaidOut(string gameObjectName)
		{
			return (_laidOutGameObjectSub ?? (_laidOutGameObjectSub = new Subject<string>()))
			   .AsObservable()
			   .Where(x => x == gameObjectName)
			   .ToUniTask(useFirstValue: true)
				;
		}

		#endregion

		#if UNITY_EDITOR

		#region Editor編集時、パラメータの変更を検知して整列命令を行う

		private IDisposable _disposable;

		private void OnValidate()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				return;
			}

			Debug.Log($"on validate");

			if (center == null)
			{
				Debug.LogError($"center game object missing! skip aligning.");
				return;
			}

			center.gameObject.SetActive(enableDebug);

			if (start == null)
			{
				Debug.LogError($"start game object missing! skip aligning.");
				return;
			}

			start.gameObject.SetActive(enableDebug);

			if (end == null)
			{
				Debug.LogError($"end game object missing! skip aligning.");
				return;
			}

			end.gameObject.SetActive(enableDebug);

			//直前に行っていたパラメータの変更をキャンセルする
			_disposable?.Dispose();

			//各種パラメータの変更を購読し、整列命令を行う
			var centerPositionChanged = center
				   .ObserveEveryValueChanged(x => x.transform.position, fastDestroyCheck: true)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;

			var startPositionChanged = start
				   .ObserveEveryValueChanged(x => x.transform.position, fastDestroyCheck: true)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;

			var endPositionChanged = end
				   .ObserveEveryValueChanged(x => x.transform.position, fastDestroyCheck: true)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;

			var childrenCountChanged = transform.ObserveEveryValueChanged(x => x.childCount, fastDestroyCheck: true)
					//購読直後のパラメータは捨てる（購読後にまとめてハンドルしたい）
				   .Skip(1)
				   .AsUnitObservable()
				;

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

			//現在のパラメータで整列を命令する
			AlignChildren();
		}

		#endregion

		#endif

		public enum LookAtKind
		{
			CrossProduct
		  , Center
		}
	}
}
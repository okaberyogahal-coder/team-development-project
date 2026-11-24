using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// ベンチなどに座れる場所。位置と向きだけを持ち、座る処理本体は PlayerWalker / MiraiWorld が担う。
    /// モーションは後から差し替えられるよう、ここでは姿勢の状態遷移に必要な情報だけを保持する。
    /// </summary>
    public class BenchSeat : MonoBehaviour
    {
        [Tooltip("座る基準位置（ワールド座標）。通常は座面の位置")]
        public Vector3 seatPoint;

        [Tooltip("座ったときに向く方向（Y軸回転・度）")]
        public float yaw;

        [Tooltip("現在この席に座っているか（プレイヤー用の簡易管理）")]
        public bool occupied;

        /// <summary>ワールド座標と向きから席を初期化する。</summary>
        public void Init(Vector3 point, float facingYaw)
        {
            seatPoint = point;
            yaw = facingYaw;
            transform.position = point;
            transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            var p = Application.isPlaying ? seatPoint : transform.position;
            Gizmos.DrawWireSphere(p, 0.18f);
            var rot = Quaternion.Euler(0f, Application.isPlaying ? yaw : transform.eulerAngles.y, 0f);
            Gizmos.DrawRay(p, rot * Vector3.forward * 0.5f);
        }
    }
}

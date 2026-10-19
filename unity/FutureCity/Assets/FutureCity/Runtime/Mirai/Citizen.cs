using UnityEngine;

namespace FutureCity
{
    /// <summary>広場の住民。近づくとプレイヤーの方を向き、頭上の輪がゆっくり浮かぶ。</summary>
    public class Citizen : MonoBehaviour
    {
        public MiraiProfile profile;
        public Transform halo;
        public Transform player;
        public float homeYaw;

        float seed, pulse;
        Vector3 haloBase;

        void Start()
        {
            seed = Random.value * 10f;
            if (halo != null) haloBase = halo.localPosition;
            transform.rotation = Quaternion.Euler(0f, homeYaw, 0f);
        }

        /// <summary>教え合ったときなどに頭上の輪を光らせる。</summary>
        public void Pulse() => pulse = 1f;

        public Vector3 HeadPosition => transform.position + Vector3.up * 2.1f * transform.localScale.y;

        void Update()
        {
            float dt = Time.deltaTime;
            float targetYaw = homeYaw;
            if (player != null)
            {
                Vector3 d = player.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < 25f) targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, 120f * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            pulse = Mathf.MoveTowards(pulse, 0f, dt * 0.8f);
            if (halo != null)
            {
                halo.localPosition = haloBase + Vector3.up * (Mathf.Sin(Time.time * 1.6f + seed) * 0.04f + pulse * 0.25f);
                halo.localScale = Vector3.one * (1f + pulse * 0.8f);
                halo.Rotate(Vector3.up, 40f * dt, Space.Self);
            }
        }
    }
}

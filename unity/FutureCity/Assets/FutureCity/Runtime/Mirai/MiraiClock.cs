using System;
using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// ミライフ内の時計。太陽の位置と、講座の開始時刻・予約締切・受講完了日時を同じ時計で扱う。
    /// （PCの時計をそのまま使うと、夜にプレイしたとき「昼の空なのに22時の講座」のようにずれるため）
    ///
    /// 開始時に「今日の startHour 時」から始まり、現実と同じ速さで進む。
    /// 本番では予約締切などの判定はサーバー時刻で行う（仕様 FR-103）。これは試作用の表示・判定の時計。
    /// </summary>
    public static class MiraiClock
    {
        static DateTime origin;
        static float originRealtime;

        public static bool Started { get; private set; }

        public static void Start(float startHour)
        {
            origin = DateTime.Today.AddHours(startHour);
            originRealtime = Time.realtimeSinceStartup;
            Started = true;
        }

        public static DateTime Now => Started ? origin.AddSeconds(Time.realtimeSinceStartup - originRealtime) : DateTime.Now;

        /// <summary>0〜24の時刻（太陽の計算用）</summary>
        public static float HourOfDay => (float)Now.TimeOfDay.TotalHours;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Backend.Bootstrap;
using Backend.Models;
using PlayFab;
using PlayFab.ClientModels;

namespace Game.Match
{
    /// <summary>
    /// Thu thap so lieu NGUOI CHOI CUC BO trong 1 tran roi gui len PlayFab khi tran ket thuc:
    /// gop thong ke (K/D/Assist/HS/Matches/EXP/Level) + kill-theo-sung + thuong GC (CloudScript).
    ///
    /// [Aemy 2026-09-17] Mo rong tu ban lead: Assist, EXP (cong don -> tu len Level qua LevelCurve), thuong GC.
    ///
    /// Cach dung tu gameplay:
    ///   - Dau tran:  MatchStatsReporter.Instance.ResetMatch();
    ///   - (tuy chon per-event) RegisterKill(sung, headshot) / RegisterDeath() / RegisterAssist() / RegisterExp(amount);
    ///   - Ket thuc (uu tien - truyen thang so tu NetworkGameState):
    ///        MatchStatsReporter.Instance.ReportMatchEnd(kills, deaths, assists, headshots, expGained, won, onDone, onError);
    ///   - Ket thuc (dung so da Register*):
    ///        MatchStatsReporter.Instance.ReportMatchEnd(expGained, won, onDone, onError);
    /// </summary>
    public class MatchStatsReporter : MonoBehaviour
    {
        private static MatchStatsReporter _instance;

        public static MatchStatsReporter Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MatchStatsReporter");
                    _instance = go.AddComponent<MatchStatsReporter>();
                }
                return _instance;
            }
        }

        private int _kills, _deaths, _headshots, _assists, _exp;
        private readonly Dictionary<string, int> _weaponKills = new Dictionary<string, int>();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        /// <summary>Xoa so lieu - goi khi bat dau 1 tran moi.</summary>
        public void ResetMatch()
        {
            _kills = 0; _deaths = 0; _headshots = 0; _assists = 0; _exp = 0;
            _weaponKills.Clear();
        }

        public void RegisterKill(string weaponName, bool headshot = false)
        {
            _kills++;
            if (headshot) _headshots++;
            if (!string.IsNullOrEmpty(weaponName))
            {
                int cur; _weaponKills.TryGetValue(weaponName, out cur);
                _weaponKills[weaponName] = cur + 1;
            }
        }

        public void RegisterDeath()  { _deaths++; }
        public void RegisterAssist() { _assists++; }                      // [Aemy]
        public void RegisterExp(int amount) { if (amount > 0) _exp += amount; } // [Aemy]

        /// <summary>[Aemy] Ket thuc tran - dung so lieu da Register* (kem _exp da cong don).</summary>
        public void ReportMatchEnd(bool won,
            Action<CombatStats> onDone = null, Action<string> onError = null)
        {
            ReportMatchEnd(_kills, _deaths, _assists, _headshots, _exp, won, onDone, onError);
        }

        /// <summary>[Aemy] Ket thuc tran - truyen thang so lieu (uu tien, lay tu NetworkGameState).</summary>
        public void ReportMatchEnd(int kills, int deaths, int assists, int headshots, int expGained, bool won,
            Action<CombatStats> onDone = null, Action<string> onError = null)
        {
            var stats = BackendBootstrap.PlayerStatsService;
            if (stats == null)
            {
                if (onError != null) onError("Chua co PlayerStatsService (chua dang nhap / chua bootstrap).");
                return;
            }

            // 1) Cong don thong ke + Assist + Experience (Level tu suy trong service).
            stats.ReportMatchResult(kills, deaths, headshots, assists, expGained,
                total =>
                {
                    // 2) Cong don kill-theo-sung (neu co ghi nhan per-event).
                    if (_weaponKills.Count > 0)
                        stats.AddWeaponKills(new Dictionary<string, int>(_weaponKills), null, _ => { });

                    // 3) Thuong GC qua CloudScript (client KHONG tu cap currency duoc).
                    AwardMatchGC(won);

                    ResetMatch();
                    if (onDone != null) onDone(total);
                },
                e => { if (onError != null) onError(e.ErrorMessage); });
        }

        /// <summary>
        /// [Aemy] Goi CloudScript "AwardMatchGC" de SERVER cong GC (thang/thua).
        /// Handler duoc tao ben PlayFab dashboard (Automation/CloudScript). Client chi kich hoat.
        /// </summary>
        private void AwardMatchGC(bool won)
        {
            var req = new ExecuteCloudScriptRequest
            {
                FunctionName = "AwardMatchGC",
                FunctionParameter = new { won = won },
                GeneratePlayStreamEvent = false
            };
            PlayFabClientAPI.ExecuteCloudScript(req,
                res =>
                {
                    if (res.Error != null)
                        Debug.LogWarning("[MatchStatsReporter] CloudScript AwardMatchGC loi: " + res.Error.Message);
                    else
                        Debug.Log("[MatchStatsReporter] Thuong GC OK.");
                },
                err => Debug.LogWarning("[MatchStatsReporter] Goi CloudScript loi: " + err.GenerateErrorReport()));
        }
    }
}

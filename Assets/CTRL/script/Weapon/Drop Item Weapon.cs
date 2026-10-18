using Fusion;
using Game.UI;
using UnityEngine;

public class DropItemWeapon : NetworkBehaviour
{
    [Networked] public int CurrentBullet { get; private set; }
    [Networked] public int MaxBullet { get; private set; }
    // [NET-OPT 2026-10-18 Aemy] (bao cao 2.2) NetworkString<_16> -> byte chi so trong GameManager._weapon_Prefabs.
    [Networked] private byte _weaponIdx { get; set; } = 255;
    public string WeaponID { get { var gm = GameManager.Instance; return gm != null ? (gm.GetWeaponIdByIndex(_weaponIdx) ?? "") : ""; } }
    // [NET-OPT 2026-10-18 Aemy] (bao cao 3.2) chi host dung; DropItemWeapon khong nam trong vong resume migration -> field thuong.
    private TickTimer DespawnTimer;

    private GameHudControllerBase _gameHudController;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 10f);
        }

        // ✅ Client tự lấy HUD của mình sau khi spawn
        // Thay bằng cách lấy HUD phù hợp với project của bạn
        if (NPlayer.Local != null)
        {
            _gameHudController = NPlayer.Local
                .GetComponentInChildren<GameHudControllerBase>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _gameHudController?.SetDragItemVisible(false, null);
    }

    private void Update()
    {
        // ✅ Guard cả hai null
        if (_gameHudController == null || NPlayer.Local == null) return;

        float dis = Vector3.Distance(Object.transform.position, NPlayer.Local.transform.position);
        if (dis < 1.5f)
            _gameHudController.SetDragItemVisible(true, this);
        else
            _gameHudController.SetDragItemVisible(false, null);
    }

    public void SetBullet(int current, int max, string weaponID, GameHudControllerBase hud)
    {
        CurrentBullet = current;
        MaxBullet = max;
        var gm = GameManager.Instance;
        int idx = gm != null ? gm.GetWeaponIndexById(weaponID) : -1;
        if (idx < 0 || idx > 254) Debug.LogWarning("[DropItemWeapon] Khong tim thay vu khi '" + weaponID + "' trong GameManager -> khong nhat duoc.");
        _weaponIdx = (byte)(idx >= 0 && idx <= 254 ? idx : 255);
        _gameHudController = hud; // Vẫn giữ để host set luôn
    }

    public void Despawn()
    {
        if (Object.HasStateAuthority)
            Runner.Despawn(Object);
        else
            RPC_RequestDespawn();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDespawn()
    {
        Runner.Despawn(Object);
    }
}
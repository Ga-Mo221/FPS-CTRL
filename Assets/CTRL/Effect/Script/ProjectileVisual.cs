using System.Collections;
using UnityEngine;

public class ProjectileVisual : MonoBehaviour
{
	[Header("Projectile Setup")]
	public float Speed = 80f;
	public float MaxDistance = 100f;
	public float LifeTimeAfterHit = 2f;

	[Header("Impact Setup")]
	public GameObject ProjectileObject;
	public GameObject HitEffectPrefab;

	private Vector3 _startPosition;
	private Vector3 _targetPosition;
	private Vector3 _hitNormal;

	private bool _showHitEffect;
	private GameObject _hitEffectInstance; // Cache instance, tái sử dụng thay vì Instantiate mỗi lần

	private float _startTime;
	private float _duration;

	private bool _canShoot;

	/// <summary>
	/// Set where the projectile visual should land.
	/// </summary>
	/// <param name="hitPosition">Position where projectile hit geometry</param>
	/// <param name="hitNormal">Normal of the hit surface</param>
	/// <param name="showHitEffect">Whether projectile impact should be displayed
	/// (e.g. we don't want static impact effect displayed on other player's body)</param>
	public void SetHit(Vector3 hitPosition, Vector3 hitNormal, bool showHitEffect)
	{
		// Hủy coroutine cũ nếu object được tái sử dụng trước khi HideHitEffect() kết thúc
		StopAllCoroutines();

		// Reset state
		enabled = true;
		_canShoot = true;
		_hitNormal = hitNormal;
		_showHitEffect = showHitEffect;

		// Reset projectile object
		ProjectileObject?.SetActive(true);

		// Tắt hit effect từ lần bắn trước (nếu có)
		_hitEffectInstance?.SetActive(false);

		// Tính toán đường bay
		_startPosition = transform.position;
		_targetPosition = hitPosition;
		_duration = Vector3.Distance(_startPosition, _targetPosition) / Speed;
		_startTime = Time.timeSinceLevelLoad;
	}

	private void Start()
	{
		_startPosition = transform.position;

		if (_targetPosition == Vector3.zero)
		{
			_targetPosition = _startPosition + transform.forward * MaxDistance;
		}

		_duration = Vector3.Distance(_startPosition, _targetPosition) / Speed;
		_startTime = Time.timeSinceLevelLoad;
	}

	private void Update()
	{
		if (!_canShoot) return;
		
		float time = Time.timeSinceLevelLoad - _startTime;

		if (time < _duration)
		{
			transform.position = Vector3.Lerp(_startPosition, _targetPosition, time / _duration);
		}
		else
		{
			transform.position = _targetPosition;
			FinishProjectile();
		}
	}

	private void FinishProjectile()
	{
		if (_showHitEffect == false)
		{
			// No hit effect, destroy immediately.
			gameObject.SetActive(false);
			return;
		}

		// Stop updating projectile visual.
		enabled = false;

		// FIX 2: Tắt _canShoot ngay tại đây trước khi chạy coroutine
		// để tránh race condition khi object được pool tái sử dụng
		_canShoot = false;

		if (ProjectileObject != null)
		{
			ProjectileObject.SetActive(false);
		}

		if (HitEffectPrefab != null)
		{
			if (_hitEffectInstance == null)
				_hitEffectInstance = Instantiate(HitEffectPrefab, _targetPosition, Quaternion.LookRotation(_hitNormal), transform);
			else
			{
				_hitEffectInstance.transform.SetPositionAndRotation(_targetPosition, Quaternion.LookRotation(_hitNormal));
				_hitEffectInstance.SetActive(true);
			}
		}

		StartCoroutine(HideHitEffect());
	}

	IEnumerator HideHitEffect()
	{
		yield return new WaitForSeconds(LifeTimeAfterHit);
		_hitEffectInstance?.SetActive(false);
		gameObject.SetActive(false);
	}
}
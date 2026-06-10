using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal static class StatusBarBillboard
{
	private static int cachedCameraFrame = -1;

	private static Camera? cachedCamera;

	internal static void ApplyBillboardRotation(Transform target, Camera? viewCamera)
	{
		if (!TryResolveBillboardRotation(viewCamera, out Quaternion rotation))
		{
			return;
		}

		target.rotation = rotation;
	}

	internal static bool TryResolveBillboardRotation(Camera? viewCamera, out Quaternion rotation)
	{
		rotation = Quaternion.identity;
		if (viewCamera == null)
		{
			return false;
		}

		Vector3 cameraForward = viewCamera.transform.forward;
		Vector3 cameraUp = viewCamera.transform.up;
		if (cameraForward == Vector3.zero || cameraUp == Vector3.zero)
		{
			return false;
		}

		rotation = Quaternion.LookRotation(cameraForward, cameraUp);
		return true;
	}

	internal static Camera? ResolveViewCamera()
	{
		if (cachedCameraFrame == Time.frameCount)
		{
			return cachedCamera;
		}

		cachedCameraFrame = Time.frameCount;
		cachedCamera = ResolveViewCameraUncached();
		return cachedCamera;
	}

	private static Camera? ResolveViewCameraUncached()
	{
		StartOfRound? startOfRound = StartOfRound.Instance;
		if (startOfRound != null)
		{
			if (startOfRound.activeCamera != null && startOfRound.activeCamera.enabled)
			{
				return startOfRound.activeCamera;
			}

			PlayerControllerB? localPlayer = startOfRound.localPlayerController;
			if (localPlayer != null && !localPlayer.isPlayerDead && localPlayer.gameplayCamera != null && localPlayer.gameplayCamera.enabled)
			{
				return localPlayer.gameplayCamera;
			}

			if (startOfRound.spectateCamera != null && startOfRound.spectateCamera.enabled)
			{
				return startOfRound.spectateCamera;
			}
		}

		return Camera.main != null ? Camera.main : Camera.current;
	}
}

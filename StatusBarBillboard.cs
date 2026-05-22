using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class StatusBarBillboard : MonoBehaviour
{
	private static int cachedCameraFrame = -1;

	private static Camera? cachedCamera;

	private void LateUpdate()
	{
		Camera? viewCamera = ResolveViewCamera();
		if (viewCamera == null)
		{
			return;
		}

		Vector3 cameraForward = viewCamera.transform.forward;
		Vector3 cameraUp = viewCamera.transform.up;
		if (cameraForward == Vector3.zero || cameraUp == Vector3.zero)
		{
			return;
		}

		transform.rotation = Quaternion.LookRotation(cameraForward, cameraUp);
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

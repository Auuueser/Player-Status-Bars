using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class StatusBarBillboard : MonoBehaviour
{
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

	private static Camera? ResolveViewCamera()
	{
		StartOfRound? startOfRound = StartOfRound.Instance;
		if (startOfRound == null)
		{
			return Camera.current;
		}

		PlayerControllerB? localPlayer = startOfRound.localPlayerController;
		if (localPlayer != null && !localPlayer.isPlayerDead)
		{
			return localPlayer.gameplayCamera;
		}

		return startOfRound.spectateCamera != null ? startOfRound.spectateCamera : Camera.current;
	}
}

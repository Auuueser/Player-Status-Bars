using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal readonly struct PlayerStatusFrameContext
{
	public PlayerStatusFrameContext(
		StatusBarConfig settings,
		PlayerControllerB localPlayer,
		Camera? viewCamera,
		bool hasBillboardRotation,
		Quaternion billboardRotation,
		bool canShowGroup,
		Vector3 observerPosition)
	{
		Settings = settings;
		LocalPlayer = localPlayer;
		ViewCamera = viewCamera;
		HasBillboardRotation = hasBillboardRotation;
		BillboardRotation = billboardRotation;
		CanShowGroup = canShowGroup;
		ObserverPosition = observerPosition;
		MaxDistanceSqr = settings.MaxDistanceSqr;
		AnchorYOffset = settings.AnchorYOffset;
		UiScale = settings.UiScale;
	}

	public readonly StatusBarConfig Settings;

	public readonly PlayerControllerB LocalPlayer;

	public readonly Camera? ViewCamera;

	public readonly bool HasBillboardRotation;

	public readonly Quaternion BillboardRotation;

	public readonly bool CanShowGroup;

	public readonly Vector3 ObserverPosition;

	public readonly float MaxDistanceSqr;

	public readonly float AnchorYOffset;

	public readonly float UiScale;
}

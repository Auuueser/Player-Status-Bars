using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Hides health a player inherited from whoever used their slot before. The game keeps one player object
/// per slot and does not clear it when someone leaves, even at the end of a round, so a player joining
/// later starts with the previous occupant's values on every other client while really being at full
/// health. Inherited health is ignored until the game shows something new about the new occupant.
/// </summary>
internal sealed class SlotInheritanceTracker
{
	public const int FreshPlayerHealth = 100;

	private SlotState[] slots = Array.Empty<SlotState>();

	/// <summary>Records the slot's current state and returns true when a new player took over the slot.</summary>
	public bool Observe(int slot, ulong occupantId, int health, bool isCritical, float damageTimestamp)
	{
		if (slot < 0)
		{
			return false;
		}

		if (slot >= slots.Length)
		{
			Array.Resize(ref slots, Math.Max(slot + 1, Math.Max(16, slots.Length * 2)));
		}

		ref SlotState state = ref slots[slot];
		if (!state.HasOccupant)
		{
			state.HasOccupant = true;
			state.OccupantId = occupantId;
			return false;
		}

		if (state.OccupantId != occupantId)
		{
			state.OccupantId = occupantId;
			state.IsHealthInherited = true;
			state.InheritedHealth = health;
			state.InheritedDamageTimestamp = damageTimestamp;
			return true;
		}

		if (state.IsHealthInherited
			&& (health != state.InheritedHealth || damageTimestamp != state.InheritedDamageTimestamp || isCritical))
		{
			state.IsHealthInherited = false;
		}

		return false;
	}

	public int ResolveHealth(int slot, int health)
	{
		return (uint)slot < (uint)slots.Length && slots[slot].IsHealthInherited ? FreshPlayerHealth : health;
	}

	public void Reset()
	{
		Array.Clear(slots, 0, slots.Length);
	}

	private struct SlotState
	{
		public bool HasOccupant;

		public ulong OccupantId;

		public bool IsHealthInherited;

		public int InheritedHealth;

		public float InheritedDamageTimestamp;
	}
}

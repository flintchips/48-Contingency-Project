using GameNetcodeStuff;
using UnityEngine;

namespace OpaliteMoonMod;

// copy of QuicksandTrigger for water but it still causes movement hindrance if not only touching terrain
public class ContingencyWaterTrigger : MonoBehaviour
{
	public bool isWater = true;

	public bool isInsideWater = false;

	public int audioClipIndex;

	[Space(5f)]
	public bool sinkingLocalPlayer;

	public float movementHinderance = 1.6f;

	public float sinkingSpeedMultiplier = 0.15f;

	private void OnTriggerStay(Collider other)
	{
		if (isWater)
		{
			if (!other.gameObject.CompareTag("Player"))
			{
				return;
			}
			PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
			if (component != GameNetworkManager.Instance.localPlayerController && component != null && component.underwaterCollider != this)
			{
				component.underwaterCollider = base.gameObject.GetComponent<Collider>();
				return;
			}
		}
		if (!isWater && !other.gameObject.CompareTag("Player"))
		{
			return;
		}
		PlayerControllerB component2 = other.gameObject.GetComponent<PlayerControllerB>();
		if (component2 != GameNetworkManager.Instance.localPlayerController)
		{
			return;
		}
		if ((isWater && component2.isInsideFactory != isInsideWater) || component2.isInElevator)
		{
			if (sinkingLocalPlayer)
			{
				StopSinkingLocalPlayer(component2);
			}
			return;
		}
		if (isWater && !component2.isUnderwater)
		{
			component2.underwaterCollider = base.gameObject.GetComponent<Collider>();
			component2.isUnderwater = true;
			if (!isInsideWater && GameNetworkManager.Instance.localPlayerController == component2 && (component2.isFallingFromJump || component2.isFallingNoJump) && component2.fallValue < -4f)
			{
				TimeOfDay.Instance.WaterSplashEffect(component2.transform.position, component2.fallValue > -17f, syncToServer: true);
			}
		}
		component2.statusEffectAudioIndex = audioClipIndex;
		if (component2.isSinking)
		{
			return;
		}
		if (sinkingLocalPlayer)
		{
			if (!component2.CheckConditionsForSinkingInQuicksand())
			{
				StopSinkingLocalPlayer(component2);
			}
		}
		else if (component2.CheckConditionsForSinkingInQuicksand())
		{
			sinkingLocalPlayer = true;
			component2.sourcesCausingSinking++;
			component2.isMovementHindered++;
			component2.hinderedMultiplier *= movementHinderance;
			if (isWater)
			{
				component2.sinkingSpeedMultiplier = 0f;
			}
			else
			{
				component2.sinkingSpeedMultiplier = sinkingSpeedMultiplier;
			}
		}
	}
	
	private bool CheckConditionsForSinkingInWater(PlayerControllerB player)
	{
		if (!player.thisController.isGrounded)
		{
			return false;
		}
		if (player.inSpecialInteractAnimation || (bool)player.inAnimationWithEnemy || player.isClimbingLadder)
		{
			return false;
		}
		if (player.physicsParent != null)
		{
			return false;
		}
		if (player.isInHangarShipRoom)
		{
			return false;
		}
		if (player.isInElevator)
		{
			return false;
		}
		return true;
	}

	private void OnTriggerExit(Collider other)
	{
		OnExit(other);
	}

	public void OnExit(Collider other)
	{
		PlayerControllerB playerControllerB = null;
		if (!sinkingLocalPlayer)
		{
			if (isWater && other.CompareTag("Player"))
			{
				playerControllerB = other.gameObject.GetComponent<PlayerControllerB>();
				if (!(playerControllerB != null) || !(playerControllerB == GameNetworkManager.Instance.localPlayerController))
				{
					playerControllerB.isUnderwater = false;
				}
			}
		}
		else if (other.CompareTag("Player"))
		{
			if (playerControllerB == null)
			{
				playerControllerB = other.gameObject.GetComponent<PlayerControllerB>();
			}
			if (!(playerControllerB != GameNetworkManager.Instance.localPlayerController))
			{
				StopSinkingLocalPlayer(playerControllerB);
			}
		}
	}

	public void StopSinkingLocalPlayer(PlayerControllerB playerScript)
	{
		if (sinkingLocalPlayer)
		{
			sinkingLocalPlayer = false;
			playerScript.sourcesCausingSinking = Mathf.Clamp(playerScript.sourcesCausingSinking - 1, 0, 100);
			playerScript.isMovementHindered = Mathf.Clamp(playerScript.isMovementHindered - 1, 0, 100);
			playerScript.hinderedMultiplier = Mathf.Clamp(playerScript.hinderedMultiplier / movementHinderance, 1f, 100f);
			if (playerScript.isMovementHindered == 0 && isWater)
			{
				playerScript.isUnderwater = false;
			}
		}
	}
}
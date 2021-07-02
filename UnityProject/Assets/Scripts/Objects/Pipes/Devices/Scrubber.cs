using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ScriptableObjects.Atmospherics;
using Systems.Atmospherics;
using Systems.Electricity;
using Objects.Atmospherics;
using UnityEngine;


namespace Pipes
{
	/// <summary>
	/// <para>Scrubbers are, in normal operation, responsible for removing unwanted gases
	/// from the local atmosphere for collection by the atmospherics waste loop.</para>
	/// <remarks>Typically controlled by an <see cref="AirController"/>.</remarks>
	/// </summary>
	public class Scrubber : MonoPipe, IServerSpawn, IAPCPowerable, IACUControllable
	{
		public enum Mode
		{
			Scrubbing = 0,
			Siphoning = 1,
		}

		/// <summary>Maps to <c>SpriteHandler</c>'s sprite SO catalogue.</summary>
		private enum Sprite
		{
			Off = 0,
			Scrubbing = 1,
			Siphoning = 2,
			WideRange = 3,
			Welded = 4,
		}

		[SerializeField]
		[Tooltip("Sound to play when the welding task is complete.")]
		private AddressableReferences.AddressableAudioSource weldFinishSfx = default;

		[SerializeField]
		[Tooltip("If enabled, allows the scrubber to operate without being connected to a pipenet (magic). Usage is discouraged.")]
		private bool selfSufficient = false;

		private readonly List<GasSO> defaultFilteredGases = new List<GasSO>() { Gas.CarbonDioxide };
		private List<GasSO> defaultContaminatedGases = new List<GasSO>();

		private MetaDataNode metaNode;
		private MetaDataLayer metaDataLayer;

		#region Lifecycle

		public override void Awake()
		{
			base.Awake();

			if (CustomNetworkManager.IsServer)
			{
				powerable = GetComponent<APCPoweredDevice>();
				FilteredGases.CollectionChanged += OnFilteredGasesChanged;
			}
		}

		public override void OnSpawnServer(SpawnInfo info)
		{
			defaultContaminatedGases = new List<Gas>(Gas.All);
			defaultContaminatedGases.Remove(Gas.Oxygen);
			defaultContaminatedGases.Remove(Gas.Nitrogen);

			metaDataLayer = MatrixManager.AtPoint(registerTile.WorldPositionServer, true).MetaDataLayer;
			metaNode = metaDataLayer.Get(registerTile.LocalPositionServer, false);
			pipeMix = selfSufficient ? GasMix.NewGasMix(GasMixes.Empty) : pipeData.GetMixAndVolume.GetGasMix();

			if (TryGetComponent<ACUDevice>(out var device) && device.Controller != null)
			{
				SetOperatingMode(device.Controller.DesiredMode);
			}

			base.OnSpawnServer(info);
		}

		#endregion

		public override void TickUpdate()
		{
			base.TickUpdate();
			pipeData.mixAndVolume.EqualiseWithOutputs(pipeData.Outputs);

			if (IsOperating == false || isWelded) return;
			if (CanTransfer() == false) return;

			switch (OperatingMode)
			{
				case Mode.Scrubbing:
					ModeScrub();
					break;
				case Mode.Siphoning:
					ModeSiphon();
					break;
			}

			metaDataLayer.UpdateSystemsAt(registerTile.LocalPositionServer, SystemType.AtmosSystem);

			if (selfSufficient)
			{
				pipeMix.Copy(GasMixes.Empty);
			}
		}

		#region Operation

		public bool IsTurnedOn { get; private set; } = false;
		public bool IsOperating { get; private set; } = false;
		public Mode OperatingMode { get; private set; } = Mode.Scrubbing;

		public bool IsExpandedRange { get; private set; } = false;
		/// <summary>Updates the scrubber's power consumption when the collection is modified.</summary>
		public ObservableCollection<Gas> FilteredGases { get; private set; } = new ObservableCollection<Gas>() { Gas.CarbonDioxide };

		private float Effectiveness => voltageMultiplier;
		private readonly float nominalMolesTransferCap = 10;

		private GasMix pipeMix;

		private bool CanTransfer()
		{
			// No external gas to take
			if (metaNode.GasMix.Pressure.Approx(0)) return false;
			if (selfSufficient == false)
			{
				// No room in internal pipe to push to
				if (pipeData.mixAndVolume.Density().y > MaxInternalPressure) return false;
			}

			return true;
		}

		private void ModeScrub()
		{
			// Scrub out a portion of each specified gas.
			// If all these gases exceed transfer amount, reduce each gas scrub mole count proportionally.

			float scrubbableMolesAvailable = 0;
			float[] gasMoles = new float[Gas.All.Length];
			foreach (var gas in FilteredGases)
			{
				// Only scrub at most some fraction of what is on the tile. TODO: tweak
				gasMoles[gas] = metaNode.GasMix.GetMoles(gas) * (IsExpandedRange ? 0.05f : 0.20f) * Effectiveness;
				scrubbableMolesAvailable += gasMoles[gas]; 
			}

			if (scrubbableMolesAvailable.Approx(0)) return; // No viable gases found

			float molesToTransfer = scrubbableMolesAvailable.Clamp(0, nominalMolesTransferCap * Effectiveness);
			float ratio = molesToTransfer / scrubbableMolesAvailable;
			ratio = ratio.Clamp(0, 1);

			// actual scrubbing
			foreach (var gas in FilteredGases)
			{
				float transferAmount = gasMoles[gas] * ratio;
				metaNode.GasMix.RemoveGas(gas, transferAmount); // TODO: works, but gets voided
				if (selfSufficient == false)
				{
					pipeMix.AddGas(gas, transferAmount); // TODO: fix something?
				}
			}
		}

		private void ModeSiphon()
		{
			float moles = metaNode.GasMix.Moles * (IsExpandedRange ? 0.40f : 0.05f) * Effectiveness; // siphon a portion
			moles = moles.Clamp(0, nominalMolesTransferCap);

			if (moles.Approx(0)) return;
			
			GasMix.TransferGas(pipeMix, metaNode.GasMix, moles);
		}

		#endregion

		#region Interaction

		private bool isWelded = false;

		public override void Interaction(HandApply interaction)
		{
			if (Validations.HasUsedActiveWelder(interaction))
			{
				ToolUtils.ServerUseToolWithActionMessages(interaction, 3,
					$"You begin {(isWelded ? "unwelding" : "welding over")} the scrubber...",
					"",
					$"{interaction.PerformerPlayerScript.visibleName} begins {(isWelded ? "unwelding" : "welding")} the scrubber...",
					$"{interaction.PerformerPlayerScript.visibleName} {(isWelded ? "unwelds" : "welds")} the scrubber!",
					() =>
					{
						isWelded = !isWelded;
						UpdateSprite();
						SoundManager.PlayNetworkedAtPos(weldFinishSfx, registerTile.WorldPositionServer, sourceObj: gameObject);
					});
			}
		}

		#endregion

		private void UpdateSprite()
		{
			Sprite sprite = Sprite.Off;

			if (IsOperating)
			{
				switch (OperatingMode)
				{
					case Mode.Scrubbing:
						sprite = IsExpandedRange ? Sprite.WideRange : Sprite.Scrubbing;
						break;
					case Mode.Siphoning:
						sprite = Sprite.Siphoning;
						break;
				}
			}

			if (isWelded)
			{
				sprite = Sprite.Welded;
			}

			if ((int)sprite == spritehandler.CataloguePage) return;
			spritehandler.ChangeSprite((int)sprite);
		}

		#region IAPCPowerable

		private readonly float siphoningPowerConsumption = 60; // Watts
		private readonly float scrubbingPowerConsumption = 10; // Per enabled filter

		APCPoweredDevice powerable;

		private PowerState powerState = PowerState.Off;
		private float voltageMultiplier = 1;

		public void PowerNetworkUpdate(float voltage)
		{
			voltageMultiplier = voltage / 240;
		}

		public void StateUpdate(PowerState state)
		{
			if (state == powerState) return;
			powerState = state;

			IsOperating = state == PowerState.Off ? false : IsTurnedOn;

			UpdateSprite();
		}

		private void UpdatePowerUsage()
		{
			var basePower = siphoningPowerConsumption;
			if (OperatingMode == Mode.Scrubbing)
			{
				basePower = scrubbingPowerConsumption * FilteredGases.Count;
			}

			if (IsExpandedRange)
			{
				basePower *= 8;
			}
			
			powerable.Wattusage = basePower;
		}

		#endregion

		#region IACUControllable

		private static readonly List<ACUMode> acuSiphonModes = new List<ACUMode>()
		{
			ACUMode.Cycle, ACUMode.Draught, ACUMode.Siphon, ACUMode.PanicSiphon
		};

		public GasMix AmbientGasMix => metaNode.GasMix;

		public void SetOperatingMode(ACUMode mode)
		{
			// Override all custom changes if the operating mode changes.

			OperatingMode = acuSiphonModes.Contains(mode) ? Mode.Siphoning : Mode.Scrubbing;
			// Create a list copy; this list can be further modified via device settings.
			FilteredGases = new ObservableCollection<Gas>(mode == ACUMode.Contaminated ? defaultContaminatedGases : defaultFilteredGases);
			FilteredGases.CollectionChanged += OnFilteredGasesChanged;
			IsExpandedRange = mode == ACUMode.Contaminated || mode == ACUMode.Cycle || mode == ACUMode.PanicSiphon;

			SetTurnedOn(mode != ACUMode.Off);
		}

		#endregion

		#region ACU-GUI

		public void SetTurnedOn(bool isTurnedOn)
		{
			IsTurnedOn = isTurnedOn;

			if (powerState != PowerState.Off)
			{
				IsOperating = IsTurnedOn;
			}

			UpdateSprite();
		}

		public void SetOperatingMode(Mode mode)
		{
			OperatingMode = mode;
			UpdatePowerUsage();
			UpdateSprite();
		}

		public void SetExpandedRange(bool isExpanded)
		{
			IsExpandedRange = isExpanded;
			UpdatePowerUsage();
			UpdateSprite();
		}

		private void OnFilteredGasesChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdatePowerUsage();
		}

		#endregion
	}
}

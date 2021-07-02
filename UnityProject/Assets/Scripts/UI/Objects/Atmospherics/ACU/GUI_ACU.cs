using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Objects.Atmospherics;

namespace UI.Objects.Atmospherics.ACU
{
	/// <summary>
	/// Main class for the <see cref="AirController"/>'s GUI.
	/// </summary>
	public class GUI_ACU : NetTab
	{
		[SerializeField, BoxGroup("Paging")]
		private NetPageSwitcher pageSwitcher = default;
		[SerializeField, BoxGroup("Paging")]
		private GUI_ACULockedPage lockedMessagePage = default;
		[SerializeField, BoxGroup("Paging")]
		private GUI_ACUNoPowerPage noPowerPage = default;

		[SerializeField, BoxGroup("Status")]
		private NetColorChanger statusIndicator = default;
		[SerializeField, BoxGroup("Status")]
		private NetLabel statusLabel = default;

		[SerializeField, BoxGroup("Element References")]
		private GameObject lockIcon = default;
		[SerializeField, BoxGroup("Element References")]
		private GameObject powerIcon = default;
		[SerializeField, BoxGroup("Element References")]
		private GameObject connectionIcon = default;

		[SerializeField]
		private NetLabel acuLabel = default;

		[SerializeField]
		private GUI_ACUValueModal editValueModal = default;

		[SerializeField, BoxGroup("Colors")]
		private Color colorOff = Color.grey;
		[SerializeField, BoxGroup("Colors")]
		private Color colorNominal = Color.green;
		[SerializeField, BoxGroup("Colors")]
		private Color colorCaution = Color.yellow;
		[SerializeField, BoxGroup("Colors")]
		private Color colorAlert = Color.red;

		public GUI_ACUValueModal EditValueModal => editValueModal;

		private GUI_ACUPage page;

		private NetSpriteImage lockIconSprite;
		private NetColorChanger lockIconColor;
		private NetColorChanger powerIconColor;
		private NetColorChanger connectionIconColor;

		private static Dictionary<ACUStatus, Color> statusColors;

		public AirController Acu { get; private set; }

		#region Initialisation

		private void Awake()
		{
			if (statusColors == null)
			{
				statusColors = new Dictionary<ACUStatus, Color>()
				{
					{ ACUStatus.Off, colorOff },
					{ ACUStatus.Nominal, colorNominal },
					{ ACUStatus.Caution, colorCaution },
					{ ACUStatus.Alert, colorAlert },
				};
			}

			lockIconSprite = lockIcon.GetComponent<NetSpriteImage>();
			lockIconColor = lockIcon.GetComponent<NetColorChanger>();
			powerIconColor = powerIcon.GetComponent<NetColorChanger>();
			connectionIconColor = connectionIcon.GetComponent<NetColorChanger>();
		}

		protected override void InitServer()
		{
			page = pageSwitcher.DefaultPage as GUI_ACUPage;
			StartCoroutine(WaitForProvider());
		}

		private IEnumerator WaitForProvider()
		{
			while (Provider == null)
			{
				yield return WaitFor.EndOfFrame;
			}

			Acu = Provider.GetComponent<AirController>();
			acuLabel.SetValueServer(Acu.name);

			foreach (var netPage in pageSwitcher.Pages)
			{
				var page = netPage as GUI_ACUPage;
				page.Acu = Acu;
				page.AcuUi = this;
			}

			editValueModal.Acu = Acu;
			editValueModal.AcuUi = this;

			(pageSwitcher.CurrentPage as GUI_ACUPage).OnPageActivated();
			OnTabOpened.AddListener(TabOpened);
			OnTabClosed.AddListener(TabClosed);

			if (IsUnobserved == false)
			{
				// Call manually; OnTabOpened is invoked before the Provider is set,
				// so the initial invoke was missed.
				TabOpened();
			}
		}

		private void TabOpened(ConnectedPlayer newPeeper = default)
		{
			UpdateManager.Add(Acu.RequestImmediateUpdate, 0.5f); // ACU quicker updates if we have peepers
			Acu.OnStateChanged += OnAcuStateChanged;
		}

		private void TabClosed(ConnectedPlayer oldPeeper = default)
		{
			// Remove listeners when unobserved (old peeper has not yet been removed).
			if (Peepers.Count < 2)
			{
				UpdateManager.Remove(CallbackType.PERIODIC_UPDATE, Acu.RequestImmediateUpdate);
				Acu.OnStateChanged -= OnAcuStateChanged;
			}
		}

		#endregion

		private void OnAcuStateChanged()
		{
			statusIndicator.SetValueServer(statusColors[Acu.OverallStatus]);
			UpdateDisplayTray();
			SetPage(ValidatePage(page));
		}

		private void UpdateDisplayTray()
		{
			statusLabel.SetValueServer(Acu.IsPowered
					? ColorStringByStatus(Acu.OverallStatus.ToString(), Acu.OverallStatus)
					: string.Empty);

			lockIconSprite.SetSprite(Acu.IsLocked ? 0 : 1);
			lockIconColor.SetValueServer(Acu.IsLocked ? colorNominal : colorCaution);
			powerIconColor.SetValueServer(Acu.IsPowered ? colorNominal : colorAlert);
			connectionIconColor.SetValueServer(Acu.ConnectedDevices.Count > 0 ? colorNominal : colorAlert);
		}

		private GUI_ACUPage ValidatePage(GUI_ACUPage requestedPage)
		{
			if (Acu.IsPowered == false) return noPowerPage;
			if (requestedPage.IsProtected && Acu.IsLocked) return lockedMessagePage;

			return requestedPage;
		}

		private void SetPage(GUI_ACUPage page)
		{
			var currentPage = pageSwitcher.CurrentPage as GUI_ACUPage;
			if (page != currentPage)
			{
				EditValueModal.Close();
				currentPage.OnPageDeactivated();
				pageSwitcher.SetActivePage(page);
				page.OnPageActivated();
			}

			page.OnPeriodicUpdate();
		}

		#region Buttons

		public void BtnRequestPage(int pageIndex)
		{
			PlayClick();
			page = pageSwitcher.Pages[pageIndex] as GUI_ACUPage;

			SetPage(ValidatePage(page));
		}

		#endregion

		#region Helpers

		/// <summary>Get the color code associated with the given <c>ACU</c> status.</summary>
		/// <returns>HTML color code as a hexadecimal string</returns>
		public static string GetHtmlColorByStatus(ACUStatus status)
		{
			if (statusColors.ContainsKey(status))
			{
				return ColorUtility.ToHtmlStringRGB(statusColors[status]);
			}

			// What, write some kind of color code here? Pfft!
			return ColorUtility.ToHtmlStringRGB(Color.white);
		}

		/// <summary>Color the given string with the associated color of the given <c>ACU</c> status.</summary>
		public static string ColorStringByStatus(string text, ACUStatus status)
		{
			return $"<color=#{GetHtmlColorByStatus(status)}>{text}</color>";
		}

		public void PlayClick()
		{
			PlaySound(SingletonSOSounds.Instance.Click01);
		}

		public void PlayTap()
		{
			PlaySound(SingletonSOSounds.Instance.Tap);
		}

		#endregion
	}
}

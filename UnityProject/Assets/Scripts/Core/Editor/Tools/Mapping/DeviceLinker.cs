using System.Collections;
using UnityEngine;
using UnityEditor;
using Objects.Atmospherics;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Core.Editor.Tools.Mapping
{
	/// <summary>
	/// An editor window to assist in quickly connecting slave devices to their masters while scene editing.
	/// </summary>
	public class DeviceLinker : EditorWindow
	{
		/// <summary>
		/// <para>The maximum distance between a slave and its master that is allowed.</para>
		/// <remarks>We limit the distance for gameplay reasons and to ensure reasonable distribution of master controllers.</remarks>
		/// </summary>
		public static readonly int MaxDistance = 30;

		private int activeWindowTab = 0;

		private void OnEnable()
		{
			titleContent = new GUIContent("Device Linker");
			var windowSize = minSize;
			windowSize.x = 250;
			minSize = windowSize;

			relinkedAcuDevices = -1;
			InitAcuLists();
		}

		private void OnGUI()
		{
			EditorGUILayout.Space();
			activeWindowTab = GUILayout.Toolbar(activeWindowTab, new string[] { "ACU Devices" });
			EditorGUILayout.Space();
			switch (activeWindowTab)
			{
				case 0:
					SectionACU();
					break;
			}
		}

		[MenuItem("Tools/Mapping/Device Linker", priority = 120)]
		public static void ShowWindow()
		{
			GetWindow<DeviceLinker>().Show();
		}

		#region ACU

		public static List<AirController> AcuControllers;
		public static List<ACUDevice> AcuDevices;
		private static List<ACUDevice> distantAcuDevices = new List<ACUDevice>();

		private static bool relinkConnected = false;
		private static int relinkedAcuDevices = -1;
		private static int reviewDistantAcuIndex = -1;

		private void SectionACU()
		{
			EditorGUILayout.LabelField("Link Devices", EditorStyles.boldLabel);

			relinkConnected = GUILayout.Toggle(relinkConnected, "Relink connected devices");

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Link"))
			{
				relinkedAcuDevices = LinkAcuDevices(relinkConnected);
			}
			GUILayout.Label($"<b>{GetLinkedCount()}</b> / <b>{AcuDevices.Count}</b> connected", EditorUIUtils.LabelStyle, GUILayout.Width(120));
			GUILayout.EndHorizontal();
			if (relinkedAcuDevices > -1)
			{
				GUILayout.Label($"Linked <b>{relinkedAcuDevices}</b> / <b>{AcuDevices.Count}</b> devices.", EditorUIUtils.LabelStyle);
			}

			EditorGUILayout.Space();
			if (distantAcuDevices.Count > 0)
			{
				SectionAcuReviewDistants();
			}
		}

		private void SectionAcuReviewDistants()
		{
			EditorGUILayout.LabelField("Distant Devices", EditorStyles.boldLabel);
			GUILayout.Label($"<b>{distantAcuDevices.Count}</b> {(distantAcuDevices.Count == 1 ? "device was" : "devices were")} "
					+ "too far from a master device for connection.", EditorUIUtils.LabelStyle);
			GUILayout.BeginHorizontal();
			bool btnPrevious = GUILayout.Button("Previous");
			bool btnNext = GUILayout.Button("Next");
			GUILayout.EndHorizontal();

			if (btnPrevious)
			{
				reviewDistantAcuIndex--;
			}
			if (btnNext)
			{
				reviewDistantAcuIndex++;
			}

			if (btnPrevious || btnNext)
			{
				reviewDistantAcuIndex = Mathf.Clamp(reviewDistantAcuIndex, 0, distantAcuDevices.Count);

				Selection.activeGameObject = distantAcuDevices[reviewDistantAcuIndex].gameObject;
				SceneView.FrameLastActiveSceneView();
			}

			if (reviewDistantAcuIndex > -1)
			{
				var deviceUnderReview = distantAcuDevices[reviewDistantAcuIndex];
				float distance = LinkAcuDevice(deviceUnderReview);
				if (distance < MaxDistance)
				{
					GUILayout.Label($"<b>{deviceUnderReview.name}</b> has been relinked.</b>");
				}

				GUILayout.Label($"<b>{deviceUnderReview.name}</b>: maximum distance of <b>{MaxDistance}</b> tiles exceeded; " +
						$"distance to nearest ACU found to be <b>{distance, 0:N}</b> tiles.", EditorUIUtils.LabelStyle);
			}
		}

		public static void InitAcuLists()
		{
			AcuControllers = new List<AirController>(FindObjectsOfType<AirController>());
			AcuDevices = new List<ACUDevice>(FindObjectsOfType<ACUDevice>());
		}

		public static void SortAcus(Vector3 position)
		{
			AcuControllers.Sort((a, b) =>
			{
				var aDistance = (a.transform.position - position).sqrMagnitude;
				var bDistance = (b.transform.position - position).sqrMagnitude;

				return aDistance.CompareTo(bDistance); // Ascending
			});
		}

		private static float LinkAcuDevice(ACUDevice device)
		{
			SortAcus(device.transform.position);
			float distance = Vector3.Distance(device.transform.position, AcuControllers[0].transform.position);
			if (distance > MaxDistance)
			{
				device.Controller = null;
				if (distantAcuDevices.Contains(device) == false)
				{
					distantAcuDevices.Add(device);
				}
			}
			else
			{
				device.Controller = AcuControllers[0];
			}

			EditorUtility.SetDirty(device);
			return distance;
		}

		private static int LinkAcuDevices(bool relinkConnected)
		{
			int count = 0;
			
			foreach (var device in AcuDevices)
			{
				if (relinkConnected || device.Controller == null)
				{
					if (LinkAcuDevice(device) <= MaxDistance)
					{
						count++;
					}
				}
			}

			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

			return count;
		}

		private static int GetLinkedCount()
		{
			int count = 0;
			foreach (var device in AcuDevices)
			{
				if (device.Controller != null)
				{
					count++;
				}
			}

			return count;
		}

		#endregion ACU
	}
}

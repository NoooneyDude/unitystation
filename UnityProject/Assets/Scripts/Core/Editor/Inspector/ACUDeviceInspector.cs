using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Objects.Atmospherics;
using Core.Editor;
using Core.Editor.Tools.Mapping;

namespace CustomInspectors
{
	/// <summary>
	/// <para>Allows the mapper to conveniently link a device to an <see cref="AirController"/>, and find disconnected devices.</para>
	/// <remarks>See also <seealso cref="ACUDevice"/>.</remarks>
	/// </summary>
	[CustomEditor(typeof(ACUDevice))]
	public class ACUDeviceInspector : Editor
	{
		private ACUDevice thisDevice;
		private SerializedProperty controller;
		
		private float closestAcuDistance;

		private void OnEnable()
		{
			thisDevice = target as ACUDevice;
			controller = serializedObject.FindProperty(nameof(thisDevice.Controller));

			DeviceLinker.InitAcuLists();
			DeviceLinker.SortAcus(thisDevice.transform.position);
		}

		[DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
		private static void DrawGizmoConnection(ACUDevice device, GizmoType type)
		{
			if (type.HasFlag(GizmoType.Selected) || type.HasFlag(GizmoType.Active))
			{
				if (device.Controller == null) return;

				Gizmos.color = Color.cyan;
				Gizmos.DrawLine(device.Controller.transform.position, device.transform.position);
				Gizmos.DrawSphere(device.Controller.transform.position, 0.15f);
			}
			else if (type.HasFlag(GizmoType.NonSelected))
			{
				if (device.Controller != null) return;

				Gizmos.DrawIcon(device.transform.position, "disconnected");
			}
		}

		public override void OnInspectorGUI()
		{
			if (Application.isPlaying) return;
			if (SceneManager.GetActiveScene() == null) return; // TODO: validate this wrokws

			EditorGUILayout.HelpBox("You can connect all ACU devices at once via\n`Tools/Mapping/Device Linker`.", MessageType.Info);

			if (thisDevice.Controller == null)
			{
				EditorGUILayout.HelpBox("Not connected to any ACU!", MessageType.Warning);
			}

			GUILayout.Label("Connect to an ACU:");

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Closest"))
			{
				closestAcuDistance = TryLinkToClosestController();
				Save();
			}
			if (thisDevice.Controller != null)
			{
				if (GUILayout.Button("Closer"))
				{
					LinkToNextController(-1);
					Save();
				}
				else if (GUILayout.Button("Further"))
				{
					LinkToNextController(1);
					Save();
				}
			}
			GUILayout.EndHorizontal();

			if (closestAcuDistance == -1)
			{
				GUILayout.Label("No ACUs found!");
			}
			else if (closestAcuDistance != default && closestAcuDistance > DeviceLinker.MaxDistance)
			{
				GUILayout.Label($"Closest ACU is <b>{closestAcuDistance,0:N}</b> tiles away, " +
						$"but this exceeds the maximum distance of <b>{DeviceLinker.MaxDistance}</b>!", EditorUIUtils.LabelStyle);
			}

			serializedObject.Update();
			EditorGUILayout.PropertyField(controller);
			serializedObject.ApplyModifiedProperties();

			if (thisDevice.Controller != null)
			{
				GUILayout.BeginHorizontal();
				var distance = Vector3.Distance(thisDevice.transform.position, thisDevice.Controller.transform.position);
				GUILayout.Label($"Connected to <b>{thisDevice.Controller.gameObject.name}</b> " +
						$"(distance of <b>{distance, 0:N}</b> tiles).", EditorUIUtils.LabelStyle);
				if (GUILayout.Button("Clear", GUILayout.Width(EditorGUIUtility.currentViewWidth / 4)))
				{
					ClearController();
					Save();
				}
				GUILayout.EndHorizontal();
			}
		}

		public float TryLinkToClosestController()
		{
			if (DeviceLinker.AcuControllers.Count < 1) return -1;

			DeviceLinker.SortAcus(thisDevice.transform.position);
			AirController closestController = DeviceLinker.AcuControllers[0];
			float distance = Vector3.Distance(thisDevice.transform.position, closestController.transform.position);
			thisDevice.Controller = distance <= DeviceLinker.MaxDistance ? closestController : null;

			return distance;
		}

		private void LinkToNextController(int direction)
		{
			var index = DeviceLinker.AcuControllers.IndexOf(thisDevice.Controller);
			index = Mathf.Clamp(index + direction, 0, DeviceLinker.AcuControllers.Count - 1);
			var device = DeviceLinker.AcuControllers[index];
			var distance = Vector3.Distance(thisDevice.transform.position, device.transform.position);
			if (distance > DeviceLinker.MaxDistance) return;

			thisDevice.Controller = device;
		}

		private void ClearController()
		{
			thisDevice.Controller = default;
		}

		private void Save()
		{
			EditorUtility.SetDirty(thisDevice);
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
		}
	}
}

using System.Collections;
using UnityEditor;
using UnityEngine;
using Core.Editor.Tools.Mapping;
using Objects.Atmospherics;

namespace CustomInspectors
{
	/// <summary>
	/// Simply draws a line between the controller and its linked devices for assisted mapping.
	/// </summary>
	[CustomEditor(typeof(AirController))]
	public class ACUInspector : Editor
	{
		private void OnEnable()
		{
			DeviceLinker.InitAcuLists();
		}

		[DrawGizmo(GizmoType.Selected | GizmoType.Active)]
		private static void DrawGizmoConnection(AirController controller, GizmoType type)
		{
			if (DeviceLinker.AcuDevices == null) return;

			foreach (var device in DeviceLinker.AcuDevices)
			{
				if (device.Controller != controller) continue;

				Gizmos.color = Color.cyan;
				Gizmos.DrawLine(device.transform.position, controller.transform.position);
				Gizmos.DrawSphere(device.transform.position, 0.15f);
			}
		}
	}
}

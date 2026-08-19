using Dynamite3D.RealIvy;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dynamite3D.RealIvy
{
	[CustomEditor(typeof(IvyInfo))]
	public class IvyInfoCustomEditor : Editor
	{
		private IvyInfo ivyInfo;
		private Texture2D logo;
		private void Awake()
		{
			logo = (Texture2D)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath("3cd66423e57d18044b92ac1fe5cd66cc"), typeof(Texture2D));
		}

		public override void OnInspectorGUI()
		{
			GUILayout.Space(12f);
			GUILayoutUtility.GetRect(0f, 38f);
			Rect texRect = new Rect(GUILayoutUtility.GetLastRect().width / 2 - 70f, GUILayoutUtility.GetLastRect().y, 151f, 38f);
			GUI.DrawTexture(texRect, logo);
			GUILayout.Space(10f);
			EditorGUILayout.HelpBox("This component holds the information for further editability of the Ivy.\r\n\r\nThe performance cost of this component is minimum, " +
				"but you can remove it if you want and the mesh will be preserved in the current state.", MessageType.Info);
			GUILayout.Space(5f);
			if (GUILayout.Button("Edit in Real Ivy Editor"))
			{
				IvyInfo ivyInfo = (IvyInfo)target;
				RealIvyWindow.Init();
				RealIvyWindow.controller.ModifyIvy(ivyInfo);
			}
			GUILayout.Space(5f);
		}

		private void OnSceneGUI()
		{
			Color[] branchesColor = new Color[] { Color.yellow, Color.red, Color.blue, Color.green,
			Color.magenta, Color.black, Color.cyan};

			if (Application.isPlaying)
			{
				//DrawBranchPoints(Color.red);
				//DrawBranchVertices(Color.yellow);
				//DrawBuildingMesh(Color.red);
				//DrawBuildingTriangle(Color.red);
				//DrawLastLeaf(Color.red);
				//DrawProcessedMeshVertices(branchesColor);
			}
		}

		/*private void DrawBranchPoints(Color color)
		{
			ivyInfo = (IvyInfo)target;
			Handles.color = color;

			RTBakedIvy bakedIvy = ivyInfo.GetComponent<RTBakedIvy>();

			BranchContainer branch = bakedIvy.ivyContainer.branches[1];

			//BranchContainer currentBranch = ivyInfo.infoPool.ivyContainer.branches[i];
			Handles.color = Color.blue;
			Handles.CubeHandleCap(0, branch.branchPoints[0].point, Quaternion.identity, 0.0025f, EventType.Repaint);
			for (int j = 1; j < branch.branchPoints.Count; j++)
			{
				Handles.color = color;
				Handles.CubeHandleCap(0, branch.branchPoints[j].point, Quaternion.identity, 0.0025f, EventType.Repaint);
			}
			
		}*/

		private void DrawBuildingMesh(Color color)
		{
			ivyInfo = (IvyInfo)target;
			Handles.color = color;


			RuntimeBakedIvy bakedIvy = ivyInfo.GetComponent<RuntimeBakedIvy>();



			if(bakedIvy != null && bakedIvy.meshBuilder != null)
			{
				RTMeshData meshData = bakedIvy.meshBuilder.buildingMeshData;
				int maxVertices = meshData.VertexCount();
				int initIndex = meshData.VertexCount() - 200;
				initIndex = Mathf.Clamp(initIndex, 0, int.MaxValue);

				maxVertices = Mathf.Clamp(maxVertices, 0, 100);

				for (int i = 0; i < meshData.VertexCount(); i++)
				{
					Vector3 pos = ivyInfo.transform.TransformPoint(meshData.vertices[i]);

					Handles.CubeHandleCap(0, pos, Quaternion.identity, 0.0025f, EventType.Repaint);

					
					Handles.Label(pos, "" + i);
				}
			}


			//ivyMesh.vertices.Length - 25

		}

		private void DrawProcessedMeshVertices(Color[] colorPerBranch)
		{
			ivyInfo = (IvyInfo)target;
			


			RuntimeBakedIvy bakedIvy = ivyInfo.GetComponent<RuntimeBakedIvy>();

			RTMeshData processedMeshData = bakedIvy.meshBuilder.processedMeshData;
			List<List<int>> verticesIndicesPerBranch = bakedIvy.meshBuilder.processedVerticesIndicesPerBranch;
			int verticesPerLoop = bakedIvy.meshBuilder.ivyParameters.sides + 1;

			int initIndex = verticesIndicesPerBranch.Count - (verticesPerLoop * 4);
			initIndex = Mathf.Clamp(initIndex, 0, int.MaxValue);

			Handles.color = Color.yellow;
			for (int j = 0; j < bakedIvy.meshBuilder.processedMeshData.VertexCount(); j++)
			{
				Vector3 vertex = bakedIvy.meshBuilder.processedMeshData.vertices[j];
				Vector3 pos = ivyInfo.transform.TransformPoint(vertex);
				Handles.CubeHandleCap(0, pos, Quaternion.identity, 0.01f, EventType.Repaint);
			}



			for (int j = 0; j < verticesIndicesPerBranch[verticesIndicesPerBranch.Count - 1].Count; j++)
			{
				int index = verticesIndicesPerBranch[verticesIndicesPerBranch.Count - 1][j];
				Vector3 pos = ivyInfo.transform.TransformPoint(processedMeshData.vertices[index]);
				Handles.CubeHandleCap(0, pos, Quaternion.identity, 0.01f, EventType.Repaint);
			}
		}

		private void DrawBranchVertices(Color color)
		{
			ivyInfo = (IvyInfo)target;
			Handles.color = color;


			RuntimeBakedIvy bakedIvy = ivyInfo.GetComponent<RuntimeBakedIvy>();
			//Mesh ivyMesh = bakedIvy.meshFilter.mesh;


			//ivyMesh.vertices.Length - 25
			for (int i = 0; i < bakedIvy.meshBuilder.buildingMeshData.VertexCount(); i++)
			{
				Vector3 pos = ivyInfo.transform.TransformPoint(bakedIvy.meshBuilder.buildingMeshData.vertices[i]);

				Handles.CubeHandleCap(0, pos, Quaternion.identity, 0.01f, EventType.Repaint);
			}
		}
	}
}
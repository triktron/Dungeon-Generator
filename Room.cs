using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Triktron.DungeonGenerator {
  [CreateAssetMenu(fileName = "Room_", menuName = "Dungeon/Room")]
  public class Room : ScriptableObject
  {
      public GameObject Prefab = null;
      public Vector2Int RoomSize = new Vector2Int(3,3);
      public List<Vector2Int> Connections = new List<Vector2Int>();
  }

  #region Editor Scripts
  #if UNITY_EDITOR
  [CustomEditor(typeof(Room))]
  public class RoomEditor : Editor
  {
      SerializedProperty Prefab;
      SerializedProperty RoomSize;

      Room Room;

      void OnEnable()
      {
          Prefab = serializedObject.FindProperty("Prefab");
          RoomSize = serializedObject.FindProperty("RoomSize");
          Room = (Room)target;
      }

      public override void OnInspectorGUI()
      {
          serializedObject.Update();
          EditorGUILayout.PropertyField(Prefab);
          EditorGUILayout.PropertyField(RoomSize);
          serializedObject.ApplyModifiedProperties();

          GUILayout.Space(5);

          GUILayout.Label("Connections");


          for (int y = 0; y < Room.RoomSize.y; y++)
          {
              GUILayout.BeginHorizontal();
              GUILayout.FlexibleSpace();
              for (int x = 0; x < Room.RoomSize.x; x++)
              {
                  var enabled = (x != 0 && x != Room.RoomSize.x - 1 && y != 0 && y != Room.RoomSize.y - 1);
                  EditorGUI.BeginDisabledGroup(enabled);

                  var hadConnection = Room.Connections.Contains(new Vector2Int(x, y));
                  var wantsConnection = GUILayout.Toggle(hadConnection, GUIContent.none);

                  if (hadConnection && !wantsConnection) Room.Connections.RemoveAll(c => c.x == x && c.y == y);
                  if (!hadConnection && wantsConnection) Room.Connections.Add(new Vector2Int(x,y));

                  EditorGUI.EndDisabledGroup();
              }
              GUILayout.EndHorizontal();
          }
      }
  }
  #endif
  #endregion
}

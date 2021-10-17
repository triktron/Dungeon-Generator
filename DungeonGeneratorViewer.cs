#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

namespace Triktron.DungeonGenerator
{
    public class DungeonGeneratorViewer : EditorWindow
    {
        private Vector2Int Size = new Vector2Int(51, 21);
        private int RoomTries = 1000;
        private int ExtraConnectorChance = 20;
        [SerializeField]
        private List<Room> Rooms = new List<Room>();
        private int WindingPercent = 0;
        private int SizeMultiplier = 1;
        private int Seed;

        private DungeonGenerator DG = new DungeonGenerator();
        private Texture2D dungeon;
        private Rect _rect;

        private const float PAD_SIZE = 4f;

        private Dictionary<DungeonGenerator.TileTypes, Color> TileColors = new Dictionary<DungeonGenerator.TileTypes, Color>() {
          { DungeonGenerator.TileTypes.Floor, Color.white },
          { DungeonGenerator.TileTypes.Wall, Color.black },
          { DungeonGenerator.TileTypes.Doorway, Color.yellow }
      };

        [MenuItem("Tools/Dungeon Viewer")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            DungeonGeneratorViewer window = (DungeonGeneratorViewer)EditorWindow.GetWindow(typeof(DungeonGeneratorViewer));
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("Settings");
            EditorGUIUtility.wideMode = true;
            Size = EditorGUILayout.Vector2IntField("Size", Size);
            RoomTries = EditorGUILayout.IntField("Room Tries", RoomTries);
            ExtraConnectorChance = EditorGUILayout.IntSlider("Extra Connector Chance", ExtraConnectorChance, 0, 100);

            ScriptableObject target = this;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty stringsProperty = so.FindProperty("Rooms");

            EditorGUILayout.PropertyField(stringsProperty, true);
            so.ApplyModifiedProperties();

            WindingPercent = EditorGUILayout.IntSlider("Winding Percent", WindingPercent, 0, 100);
            SizeMultiplier = EditorGUILayout.IntSlider("Tile Size Multipier", SizeMultiplier, 0, 5);


            EditorGUILayout.BeginHorizontal();
            Seed = EditorGUILayout.IntField("Seed", Seed);
            if (GUILayout.Button("Randomize")) Seed = Random.Range(0, 999999);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generate");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Init")) { DG.Init(Size.x, Size.y, Seed, RoomTries, Rooms, ExtraConnectorChance, WindingPercent); render(); }
            if (GUILayout.Button("Generate Rooms")) { DG.GenerateRooms(); render(); }
            if (GUILayout.Button("Add Mazes")) { DG.AddMazes(); render(); }
            if (GUILayout.Button("Connect Regions")) { DG.ConnectRegions(); render(); }
            if (GUILayout.Button("Remove Dead Ends")) { DG.RemoveDeadEnds(); render(); }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("All"))
            {
                DG.Generate(Size.x, Size.y, Seed, RoomTries, Rooms, ExtraConnectorChance, WindingPercent);
                render();
            }

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
                _rect = GUILayoutUtility.GetLastRect();

            if (dungeon != null) EditorGUI.DrawPreviewTexture(new Rect(PAD_SIZE, _rect.height + PAD_SIZE, position.width - PAD_SIZE * 2, position.width / Size.x * Size.y), dungeon);
        }

        void render()
        {
            List<DungeonGenerator.TileTypes> Tiles = DG.Dungeon;

            dungeon = new Texture2D(Size.x, Size.y);
            dungeon.filterMode = FilterMode.Point;

            for (int x = 0; x < Size.x; x++)
            {
                for (int y = 0; y < Size.y; y++)
                {
                    var tile = Tiles[x + y * Size.x];
                    var color = TileColors[tile];

                    dungeon.SetPixel(x, y, color);
                }
            }

            dungeon.Apply();
        }
    }

#endif
}

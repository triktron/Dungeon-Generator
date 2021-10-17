using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Triktron.DungeonGenerator {
  public class DungeonGenerator
  {
      public enum TileTypes
      {
          Wall,
          Floor,
          Doorway
      }

      #region Variables
      // amount of tries should be taken to place rooms
      private int _roomTries;
      private List<Room> _roomPrefabs;

      /// The inverse chance of adding a connector between two regions that have
      /// already been joined. Increasing this leads to more loosely connected
      /// dungeons.
      private float _extraConnectorChance = .2f;

      // percentage how straight the mazes should be
      private float _windingPercent = 0;

      private Vector2Int _size;

      private List<RectInt> _rooms = new List<RectInt>();

      /// For each open position in the dungeon, the index of the connected region
      /// that that position is a part of.
      private List<int> _regions = new List<int>();

      private List<TileTypes> _dungeon = new List<TileTypes>();


      /// The index of the current region being carved.
      private int _currentRegion = -1;

      public List<TileTypes> Dungeon => _dungeon;
      #endregion

      #region Phases
      /// <summary>
      /// Goes thru every phase of the generator and returns the dungeon
      /// </summary>
      /// <param name="sizeX">The horizontal size of the dungeon.</param>
      /// <param name="sizeY">The vertical size of the dungeon.</param>
      /// <param name="seed">The seed used for randomnes. when -1 it generates a random seed.</param>
      /// <param name="roomTries">How many tries should be done placing rooms.</param>
      /// <param name="rooms">A list of all room types.</param>
      /// <param name="connectorChance">The procentage chance of a conection being placed. 0 being none and 1 everywhere</param>
      /// <param name="windingPercentage">The procentage chance of a maze path winding. 0 being straight and 1 turn every step.</param>
      public List<TileTypes> Generate(int sizeX, int sizeY, int seed = -1, int roomTries = 500, List<Room> rooms = null, float connectorChance = .2f, float windingPercentage = 0)
      {
          Init(sizeX, sizeY, seed, roomTries, rooms, connectorChance, windingPercentage);
          GenerateRooms();
          AddMazes();
          ConnectRegions();
          RemoveDeadEnds();

          return _dungeon;
      }

      /// <summary>
      /// Phase one
      /// Initialize the generator
      /// </summary>
      /// <param name="sizeX">The horizontal size of the dungeon.</param>
      /// <param name="sizeY">The vertical size of the dungeon.</param>
      /// <param name="seed">The seed used for randomnes. when -1 it generates a random seed.</param>
      /// <param name="roomTries">How many tries should be done placing rooms.</param>
      /// <param name="rooms">A list of all room types.</param>
      /// <param name="connectorChance">The procentage chance of a conection being placed. 0 being none and 1 everywhere</param>
      /// <param name="windingPercentage">The procentage chance of a maze path winding. 0 being straight and 1 turn every step.</param>
      public void Init(int sizeX, int sizeY, int seed = -1, int roomTries = 500, List<Room> rooms = null, float connectorChance = .2f, float windingPercentage = 0)
      {
          if (seed == -1) seed = UnityEngine.Random.Range(0,99999);
          if (rooms == null) rooms = new List<Room>();

          UnityEngine.Random.InitState(seed);

          //reset variables
          _size = new Vector2Int(sizeX, sizeY);
          _rooms.Clear();
          _regions.Clear();
          _currentRegion = -1;
          _dungeon = new List<TileTypes>(sizeY * sizeX);
          _roomTries = roomTries;
          _roomPrefabs = rooms;
          _extraConnectorChance = connectorChance;
          _windingPercent = windingPercentage;

          // fill dungeon with walls
          for (int i = 0; i < sizeY * sizeX; i++)
          {
              _dungeon.Add(TileTypes.Wall);
              _regions.Add(-1);
          }
      }

      /// <summary>
      /// Phase two
      /// Places Rooms of random sizes at random positions n times.
      /// </summary>
      public void GenerateRooms()
      {
            Debug.Assert(_roomPrefabs.Count != 0, "No rooms were given!");

          for (var i = 0; i < _roomTries; i++)
          {
              var roomPrefab = _roomPrefabs[UnityEngine.Random.Range(0, _roomPrefabs.Count)];

              var width = roomPrefab.RoomSize.x;
              var height = roomPrefab.RoomSize.y;

              var x = UnityEngine.Random.Range(0, Mathf.FloorToInt((_size.x - width) / 2)) * 2 + 1;
              var y = UnityEngine.Random.Range(0, Mathf.FloorToInt((_size.y - height) / 2)) * 2 + 1;

              var room = new RectInt(x, y, width, height);
              var roomBorder = new RectInt(x-1, y-1, width+2, height+2);

              var overlaps = false;
              foreach (var other in _rooms)
              {
                  if (other.Overlaps(roomBorder))
                  {
                      overlaps = true;
                      break;
                  }
              }

              if (overlaps) continue;

              PlaceRoom(x, y, width, height);
          }
      }

      /// <summary>
      /// Places Room in the dungeon
      /// </summary>
      /// <param name="x">x position</param>
      /// <param name="y">y position</param>
      /// <param name="width">witdh of room</param>
      /// <param name="height">height or room</param>
      public void PlaceRoom(int x, int y, int width, int height)
      {
          var room = new RectInt(x, y, width, height);

          _rooms.Add(room);
          _startRegion();

          for (int xpos = 0; xpos < width; xpos++)
          {
              for (int ypos = 0; ypos < height; ypos++)
              {
                  _setCell(new Vector2Int(xpos + x, ypos + y));
              }
          }
      }

      /// <summary>
      /// Phase three
      /// find all empty spaces and fill them with maze like patterns.
      /// </summary>
      public void AddMazes()
      {
          // Fill in all of the empty space with mazes.
          for (var y = 1; y < _size.y; y += 2)
          {
              for (var x = 1; x < _size.x; x += 2)
              {
                  var pos = x + y * _size.x;
                  if (_dungeon[pos] != TileTypes.Wall) continue;
                  _growMaze(new Vector2Int(x, y));
              }
          }
      }
      /// Implementation of the "growing tree" algorithm from here:
      /// http://www.astrolog.org/labyrnth/algrithm.htm.
      private void _growMaze(Vector2Int start)
      {
          var cells = new List<Vector2Int>();
          Vector2Int lastDir = Vector2Int.zero;

          _startRegion();
          _setCell(start);

          cells.Add(start);
          while (cells.Count != 0)
          {
              var cell = cells.Last();

              // See which adjacent cells are open.
              var unmadeCells = new List<Vector2Int>();

              if (_canCarve(cell, new Vector2Int(1, 0), ref _dungeon)) unmadeCells.Add(new Vector2Int(1, 0));
              if (_canCarve(cell, new Vector2Int(-1, 0), ref _dungeon)) unmadeCells.Add(new Vector2Int(-1, 0));
              if (_canCarve(cell, new Vector2Int(0, 1), ref _dungeon)) unmadeCells.Add(new Vector2Int(0, 1));
              if (_canCarve(cell, new Vector2Int(0, -1), ref _dungeon)) unmadeCells.Add(new Vector2Int(0, -1));

              if (unmadeCells.Count != 0)
              {
                  // Based on how "windy" passages are, try to prefer carving in the
                  // same direction.
                  Vector2Int dir;
                  if (unmadeCells.Contains(lastDir) && UnityEngine.Random.Range(0, 100) > _windingPercent)
                  {
                      dir = lastDir;
                  }
                  else
                  {
                      dir = unmadeCells[UnityEngine.Random.Range(0, unmadeCells.Count)];
                  }

                  _setCell(cell + dir);
                  _setCell(cell + dir * 2);

                  cells.Add(cell + dir * 2);
                  lastDir = dir;
              }
              else
              {
                  // No adjacent uncarved cells.
                  cells.RemoveAt(cells.Count - 1);

                  // This path has ended.
                  lastDir = Vector2Int.zero;
              }
          }
      }

      /// <summary>
      /// Phase four
      /// Connects every region with two connection plus the shared side by chance.
      /// </summary>
      public void ConnectRegions()
      {
          // Find all of the tiles that can connect two (or more) regions.
          var connectorRegions = new Dictionary<Vector2Int, List<int>>();
          for (int x = 1; x < _size.x - 1; x++)
          {
              for (int y = 1; y < _size.y - 1; y++)
              {
                  var pos = new Vector2Int(x, y);

                  // Can't already be part of a region.
                  if (_getCell(pos) != TileTypes.Wall) continue;

                  var regions = new List<int>();

                  if (_regions[_getIndexForPos(pos + new Vector2Int(1, 0))] != -1) regions.Add(_regions[_getIndexForPos(pos + new Vector2Int(1, 0))]);
                  if (_regions[_getIndexForPos(pos + new Vector2Int(-1, 0))] != -1) regions.Add(_regions[_getIndexForPos(pos + new Vector2Int(-1, 0))]);
                  if (_regions[_getIndexForPos(pos + new Vector2Int(0, 1))] != -1) regions.Add(_regions[_getIndexForPos(pos + new Vector2Int(0, 1))]);
                  if (_regions[_getIndexForPos(pos + new Vector2Int(0, -1))] != -1) regions.Add(_regions[_getIndexForPos(pos + new Vector2Int(0, -1))]);

                  if (regions.Count < 2) continue;

                  connectorRegions[pos] = regions;
              }
          }

          var connectors = connectorRegions.Keys.ToList();

          // Keep track of which regions have been merged. This maps an original
          // region index to the one it has been merged to.
          var merged = new List<int>();
          var openRegions = new List<int>();
          for (var i = 0; i <= _currentRegion; i++)
          {
              merged.Add(i);
              openRegions.Add(i);
          }

          // Keep connecting regions until we're down to one.
          while (openRegions.Count > 1)
          {
              var connector = connectors[UnityEngine.Random.Range(0, connectors.Count)];

              // Carve the connection.
              _addJunction(connector);

              // Merge the connected regions. We'll pick one region (arbitrarily) and
              // map all of the other regions to its index.
              var regions = connectorRegions[connector].Select((region) => merged[region]);
              int dest = regions.First();
              List<int> sources = regions.Skip(1).ToList();

              // Merge all of the affected regions. We have to look at *all* of the
              // regions because other regions may have previously been merged with
              // some of the ones we're merging now.
              for (var i = 0; i <= _currentRegion; i++)
              {
                  if (sources.Contains(merged[i]))
                  {
                      merged[i] = dest;
                  }
              }

              // The sources are no longer in use.
              openRegions.RemoveAll(or => sources.Contains(or));

              // Remove any connectors that aren't needed anymore.
              connectors.RemoveAll((pos) => {
                  // Don't allow connectors right next to each other.
                  if ((connector - pos).magnitude < 2) return true;

                  // If the connector no long spans different regions, we don't need it.
                  var r = new HashSet<int>();
                  foreach (var item in connectorRegions[pos])
                  {
                      r.Add(merged[item]);
                  }

                  if (r.Count > 1) return false;

                  // This connecter isn't needed, but connect it occasionally so that the
                  // dungeon isn't singly-connected.

                  if (UnityEngine.Random.Range(0, _extraConnectorChance) == 0) _addJunction(pos);

                  return true;
              });
          }
      }
      private void _addJunction(Vector2Int pos)
      {
          _setCell(pos, TileTypes.Doorway);
      }

      /// <summary>
      /// Phase five
      /// Removes all the maze paths that end in a dead end.
      /// </summary>
      public void RemoveDeadEnds()
      {
          var done = false;

          while (!done)
          {
              done = true;

              for (int x = 1; x < _size.x - 1; x++)
              {
                  for (int y = 1; y < _size.y - 1; y++)
                  {
                      var pos = new Vector2Int(x, y);
                      if (_getCell(pos) == TileTypes.Wall) continue;

                      // If it only has one exit, it's a dead end.
                      var exits = 0;
                      if (_getCell(pos + new Vector2Int(1, 0)) != TileTypes.Wall) exits++;
                      if (_getCell(pos + new Vector2Int(-1, 0)) != TileTypes.Wall) exits++;
                      if (_getCell(pos + new Vector2Int(0, 1)) != TileTypes.Wall) exits++;
                      if (_getCell(pos + new Vector2Int(0, -1)) != TileTypes.Wall) exits++;

                      if (exits != 1) continue;

                      done = false;
                      _setCell(pos, TileTypes.Wall);
                  }
              }
          }
      }
      #endregion

      #region Common Functions
      private void _startRegion()
      {
          _currentRegion++;
      }

      private void _setCell(Vector2Int pos, TileTypes type = TileTypes.Floor)
      {
          _dungeon[pos.x + pos.y * _size.x] = type;
          _regions[pos.x + pos.y * _size.x] = _currentRegion;
      }

      private int _getIndexForPos(Vector2Int pos)
      {
          return pos.x + pos.y * _size.x;
      }

      private TileTypes _getCell(Vector2Int pos)
      {
          return _dungeon[pos.x + pos.y * _size.x];
      }

      private bool _canCarve(Vector2Int pos, Vector2Int dir, ref List<TileTypes> dungeon)
      {
          var p = pos + dir*2;
          if (p.x < 0 || p.y < 0 || p.x >= _size.x || p.y >= _size.y) return false;
          if (dungeon[p.x + p.y * _size.x] != TileTypes.Wall) return false;
          p = pos + dir * 3;
          if (p.x < 0 || p.y < 0 || p.x >= _size.x || p.y >= _size.y) return false;
          return dungeon[p.x + p.y * _size.x] == TileTypes.Wall;
      }
      #endregion
  }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace game
{

public class PathNode
{
  public Vector2 Position { get; set; }
  public int PathLengthFromStart { get; set; }
  public PathNode CameFrom { get; set; }
  public int HeuristicEstimatePathLength { get; set; }
  public int EstimateFullPathLength {
    get {
      return this.PathLengthFromStart + this.HeuristicEstimatePathLength;
    }
  }
}

public class Pathfinder
{
  int[,] field;

  int min_x;
  int min_y;
  int max_x;
  int max_y;

  Tilemap main_tilemap;
  Tilemap decor_tilemap;

  public Pathfinder(Tilemap main_tilemap, Tilemap decor_tilemap, int field_width, int field_height, int min_x, int min_y, int max_x, int max_y)
  {
    this.main_tilemap = main_tilemap;
    this.decor_tilemap = decor_tilemap;
    this.field = new int[field_width, field_height];
    this.min_x = min_x;
    this.min_y = min_y;
    this.max_x = max_x;
    this.max_y = max_y;
  }

  public List<Vector2> FindPath(Vector2 start, Vector2 goal)
  {
    var closed_set = new List<PathNode>();
    var open_set = new List<PathNode>();

    PathNode start_node = new PathNode()
    {
      Position = start,
      CameFrom = null,
      PathLengthFromStart = 0,
      HeuristicEstimatePathLength = GetHeuristicPathLength(start, goal)
    };

    open_set.Add(start_node);

    while(open_set.Count > 0)
    {
      var current_node = open_set.OrderBy(node => node.EstimateFullPathLength).First();
      if(current_node.Position == goal)
        return GetPathForNode(current_node);

      open_set.Remove(current_node);
      closed_set.Add(current_node);

      var neighbours = GetNeighbours(current_node, goal);
      foreach(var neighbour in neighbours)
      {
        if(closed_set.Count(node => node.Position == neighbour.Position) > 0)
          continue;

        var open_node = open_set.FirstOrDefault(node => node.Position == neighbour.Position);
        if(open_node == null)
          open_set.Add(neighbour);
        else if(open_node.PathLengthFromStart > neighbour.PathLengthFromStart)
        {
          open_node.CameFrom = current_node;
          open_node.PathLengthFromStart = neighbour.PathLengthFromStart;
        }
      }
    }

    return null;
  }

  int GetDistanceBetweenNeighbours()
  {
    return 1;
  }

  int GetHeuristicPathLength(Vector2 from, Vector2 to)
  {
    int from_x = Mathf.RoundToInt(from.x);
    int to_x = Mathf.RoundToInt(to.x);

    int from_y = Mathf.RoundToInt(from.y);
    int to_y = Mathf.RoundToInt(to.y);

    return Mathf.Abs(from_x - to_x) + Mathf.Abs(from_y - to_y);
  }

  List<PathNode> GetNeighbours(PathNode path_node, Vector2 goal)
  {
    var result = new List<PathNode>();
  
    Vector2[] neighbour_points = new Vector2[4];
    neighbour_points[0] = new Vector2(path_node.Position.x + 1, path_node.Position.y);
    neighbour_points[1] = new Vector2(path_node.Position.x - 1, path_node.Position.y);
    neighbour_points[2] = new Vector2(path_node.Position.x, path_node.Position.y + 1);
    neighbour_points[3] = new Vector2(path_node.Position.x, path_node.Position.y - 1);
  
    foreach(var point in neighbour_points)
    {
      if(point.x < min_x || point.x > max_x)
        continue;
      if(point.y < min_y || point.y > max_y)
        continue;

      Vector3Int tile_pos = new Vector3Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), 0);
      var main_tile = main_tilemap.GetTile<Tile>(tile_pos);
      var decor_tile = decor_tilemap.GetTile<Tile>(tile_pos);
      if(main_tile == null || decor_tile != null)
        continue;

      var neighbour_node = new PathNode()
      {
        Position = point,
        CameFrom = path_node,
        PathLengthFromStart = path_node.PathLengthFromStart + GetDistanceBetweenNeighbours(),
        HeuristicEstimatePathLength = GetHeuristicPathLength(point, goal)
      };

      result.Add(neighbour_node);
    }

    return result;
  }

  List<Vector2> GetPathForNode(PathNode path_node)
  {
    var result = new List<Vector2>();
    var current_node = path_node;

    while(current_node != null)
    {
      result.Add(current_node.Position);
      current_node = current_node.CameFrom;
    }

    result.Reverse();
    return result;
  }
}

}
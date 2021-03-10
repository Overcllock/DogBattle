using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine.Tilemaps;
using System.Linq;

namespace game
{

public enum BattleUnitState
{
  None,
  Spawning,
  Idle,
  Moving,
  Attacking,
  Dying
}

public class BattleUnit : MonoBehaviour
{
  const float MAX_HP = 100f;
  const float DAMAGE_MIN = 5f;
  const float DAMAGE_MAX = 15f;
  const float MOVE_SPEED = 1f; //Lower is faster

  uint id;
  public uint GetID() => id;
  public void SetID(uint value) => id = value;

  float hp;
  public float GetHP() => hp;
  public float GetHPPercent() => Mathf.Clamp01(hp / MAX_HP);

  uint team_index;
  public uint GetTeamIndex() => team_index;

  bool is_moving = false;
  public bool IsMoving() => is_moving;

  bool is_attacking = false;
  public bool IsAttacking() => is_attacking;

  bool is_dying = false;
  public bool IsDying() => is_dying;

  Vector2 target_position;
  List<Vector2> current_path;
  BattleUnit target_unit;

  Battleground battleground;

  StateMachine<BattleUnitState> fsm;

  public BattleUnitState current_mode => fsm.CurrentState();

  abstract class State
  {
    public abstract BattleUnitState GetMode();
    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }
    
    public StateMachine<BattleUnitState> fsm;
    public BattleUnit unit;
  }

  void AddState(State state)
  {
    state.fsm = fsm;
    state.unit = this;
    fsm.Add(state.GetMode(), state.OnEnter, state.OnUpdate, state.OnExit);
  }

  void Awake()
  {
    battleground = Game.GetBattleground();
    fsm = new StateMachine<BattleUnitState>();

    AddState(new StateSpawning());
    AddState(new StateIdle());
    AddState(new StateMoving());
    AddState(new StateAttacking());
    AddState(new StateDying());

    fsm.SwitchTo(BattleUnitState.Spawning);
  }

  void FixedUpdate()
  {
    fsm.Update();
  }

  public void Init(uint team_index)
  {
    this.team_index = team_index;
    this.hp = MAX_HP;
  }

  public void GiveDamage(BattleUnit enemy)
  {
    var damage = UnityEngine.Random.Range(DAMAGE_MIN, DAMAGE_MAX);
    enemy.GetDamage(damage);
  }

  public void GetDamage(float damage)
  {
    hp = Mathf.Clamp(hp - damage, 0, MAX_HP);
    if(hp == 0)
      fsm.TrySwitchTo(BattleUnitState.Dying);
  }

  public void SetTargetPosition(Vector2 pos) => target_position = pos;
  public void SetTargetUnit(BattleUnit unit) => target_unit = unit;

  public void SetCurrentPath(List<Vector2> path) => current_path = path;
  public List<Vector2> GetCurrentPath() => current_path;

  public Vector2 GetTargetPosition() => target_position;
  public BattleUnit GetTargetUnit() => target_unit;

  public bool HasTargetPosition() => !target_position.Equals(Vector2.positiveInfinity);
  public bool HasTargetUnit() => target_unit != null;

  public void ResetTargetPosition() => target_position = Vector2.positiveInfinity;
  public void ResetTargetUnit() => target_unit = null;

  public void GoTo(Vector2 goal)
  {
    is_moving = true;

    var tilemap = battleground.GetMainTilemap();

    var cell_pos = new Vector3Int(Mathf.RoundToInt(goal.x), Mathf.RoundToInt(goal.y), 0);
    var world_pos = tilemap.CellToWorld(cell_pos);
    world_pos.x += Battleground.UNIT_WORLD_POS_OFFSET_X;
    world_pos.y += Battleground.UNIT_WORLD_POS_OFFSET_Y;

    transform.DOMove(world_pos, MOVE_SPEED).OnComplete(() => is_moving = false);
  }

  public void Attack(BattleUnit target)
  {
    is_attacking = true;
    
    var normal_scale = new Vector3(1f, 1f, 1f);
    var target_scale = new Vector3(1.3f, 1.3f, 1f);

    transform.DOScale(target_scale, 0.25f).OnComplete(() => {
      GiveDamage(target);

      transform.DOScale(normal_scale, 0.25f).OnComplete(() => is_attacking = false);
    });
  }

  public void Die()
  {
    is_dying = true;

    var renderer = transform.GetComponentInChildren<SpriteRenderer>();
    renderer.DOFade(0, 0.5f).OnComplete(() => is_dying = false);
  }

  public Vector2 GetTilePosition()
  {
    var battleground = Game.GetBattleground();
    if(battleground == null || !battleground.IsLoaded())
      return Vector2.zero;

    var tilemap = battleground.GetMainTilemap();
    var cell_pos = tilemap.WorldToCell(transform.position);

    var tile_pos = new Vector2(cell_pos.x, cell_pos.y);
    return tile_pos;
  }

#region BattleUnit states

  class StateSpawning : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Spawning; }

    public override void OnUpdate()
    {
      var hud = UI.Find<UIHud>("hud");
      if(hud == null || hud.GetCurrentState() != UIWindowState.Opened)
        return;

      hud.DefineHPBar(unit.GetID());
      fsm.TrySwitchTo(BattleUnitState.Idle);
    }
  }

  class StateIdle : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Idle; }

    Battleground battleground = Game.GetBattleground();
    Vector2 tile_pos;
    List<Vector2> path;

    public override void OnEnter()
    {
      tile_pos = unit.GetTilePosition();
      path = unit.GetCurrentPath();

      battleground.RemoveFromWalkable(tile_pos);
    }

    public override void OnUpdate()
    {
      //Trying to find enemies beside and attack it
      var target_unit = battleground.FindUnitToAttack(tile_pos, unit.GetTeamIndex());
      if(target_unit != null)
      {
        unit.SetTargetUnit(target_unit);
        fsm.TrySwitchTo(BattleUnitState.Attacking);
        return;
      }

      //If next point from current path contains any unit - forget it
      if(path != null)
      {
        if(path.Count == 0 || battleground.ContainsUnitAtPoint(path[0]))
          path = null;
      }

      //Find new relevant path to enemy, if that length less then current path length - replace it
      var rel_path = battleground.GetRelevantPath(tile_pos, unit.GetTeamIndex());
      if(rel_path != null)
      {
        if(path == null || path.Count > rel_path.Count)
        {
          path = rel_path;
          unit.SetCurrentPath(path);
        }
      }

      //If unit has path - set first path point to current target..
      Vector2 point_to_move;
      if(path != null && path.Count > 0)
      {
        point_to_move = path[0];
        path.RemoveAt(0);
      }
      //..or setting random available point
      else
        point_to_move = battleground.GetRandomPointToMove(tile_pos);

      //If failed to find some target point - waiting
      if(point_to_move.Equals(Vector2.positiveInfinity))
        return;

      unit.SetTargetPosition(point_to_move);
      fsm.TrySwitchTo(BattleUnitState.Moving);
    }
  }

  class StateMoving : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Moving; }

    Battleground battleground = Game.GetBattleground();

    public override void OnEnter()
    {
      if(!unit.HasTargetPosition())
      {
        fsm.TrySwitchTo(BattleUnitState.Idle);
        return;
      }

      var current_position = unit.GetTilePosition();
      var target_position = unit.GetTargetPosition();

      battleground.RemoveFromWalkable(target_position);
      battleground.AddToWalkable(current_position);

      unit.GoTo(target_position);
    }

    public override void OnUpdate()
    {
      if(unit.IsMoving())
        return;

      fsm.TrySwitchTo(BattleUnitState.Idle);
    }

    public override void OnExit()
    {
      unit.ResetTargetPosition();
    }
  }

  class StateAttacking : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Attacking; }

    BattleUnit target;

    public override void OnEnter()
    {
      unit.SetCurrentPath(null);

      if(!unit.HasTargetUnit())
      {
        fsm.TrySwitchTo(BattleUnitState.Idle);
        return;
      }

      target = unit.GetTargetUnit();
      unit.Attack(target);
    }

    public override void OnUpdate()
    {
      if(unit.IsAttacking())
        return;

      fsm.TrySwitchTo(BattleUnitState.Idle);
    }

    public override void OnExit()
    {
      unit.ResetTargetUnit();
    }
  }

  class StateDying : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Dying; }

    Battleground battleground = Game.GetBattleground();

    public override void OnEnter()
    {
      unit.Die();
    }

    public override void OnUpdate()
    {
      if(unit.IsDying())
        return;

      battleground.AddToWalkable(unit.GetTilePosition());
      Destroy(unit.gameObject);

      battleground.RemoveUnit(unit.GetID());
    }
  }

#endregion
}

public class Battleground
{
  //NOTE: this params must being in config or autodetected
  const int MIN_X = -3;
  const int MIN_Y = -4;
  const int MAX_X = 2;
  const int MAX_Y = 3;

  public const float UNIT_WORLD_POS_OFFSET_X = 0.5f;
  public const float UNIT_WORLD_POS_OFFSET_Y = 0.35f;
  //

  public delegate void UnitDead(uint unit_id, uint team_index);
  public UnitDead OnUnitDead;

  FieldBounds bounds = new FieldBounds(MIN_X, MAX_X, MIN_Y, MAX_Y);

  Tilemap main_tilemap;
  public Tilemap GetMainTilemap() => main_tilemap;

  Tilemap decor_tilemap;

  Pathfinder pathfinder;

  bool is_loaded = false;
  public bool IsLoaded() => is_loaded;

  Dictionary<uint, BattleUnit> units = new Dictionary<uint, BattleUnit>();
  List<Vector2> walkable_points = new List<Vector2>();

  public async UniTask Load()
  {
    var location = await Assets.TryReuseAsync("Prefabs/location");
    Error.Assert(location != null, "Location loading failed.");

    var main_tilemap_go = location.GetChild("grid/battleground_tilemap");
    main_tilemap = main_tilemap_go.GetComponent<Tilemap>();

    var decor_tilemap_go = location.GetChild("grid/decor_tilemap");
    decor_tilemap = decor_tilemap_go.GetComponent<Tilemap>();
    
    Error.Assert(main_tilemap_go != null && decor_tilemap != null, "Tilemaps init failed.");

    pathfinder = new Pathfinder(this, main_tilemap, decor_tilemap, bounds);

    walkable_points.AddRange(bounds.GetAllPoints());

    is_loaded = true;
  }

  public void AddUnit(BattleUnit unit)
  {
    var id = units.Keys.Count > 0 ? units.Keys.Max() + 1 : 0;
    unit.SetID(id);
    units.Add(id, unit);
  }

  public void RemoveUnit(uint id)
  {
    if(!units.ContainsKey(id))
      return;

    var unit = units[id];
    units.Remove(id);

    OnUnitDead?.Invoke(id, unit.GetTeamIndex());
  }

  public BattleUnit GetUnit(uint id)
  {
    if(!units.ContainsKey(id))
      return null;
    
    return units[id];
  }

  public int GetTeamUnitsCount(uint team_index)
  {
    return units.Values.Count(unit => unit.GetTeamIndex() == team_index);
  }

  public List<Vector2> GetRelevantPath(Vector2 start, uint team_index)
  {
    List<Vector2> rel_path = null;
    int min_path_length = bounds.GetWidth() * bounds.GetHeight();

    foreach(var unit in units.Values)
    {
      if(unit.GetTeamIndex() == team_index)
        continue;

      var unit_tilepos = unit.GetTilePosition();

      var path = pathfinder.FindPath(start, unit_tilepos);
      if(path == null || path.Count == 0)
        continue;

      var path_length = path.Count;
      if(path_length < min_path_length)
      {
        min_path_length = path_length;
        rel_path = path;
      }
    }

    return rel_path;
  }

  public Vector2 GetRandomPointToMove(Vector2 start)
  {
    var neighbours = Pathfinder.GetPointNeighbours(start);
    neighbours.Shuffle();

    foreach(var neighbour in neighbours)
    {
      if(pathfinder.IsWalkable(neighbour))
        return neighbour;
    }

    return Vector2.positiveInfinity;
  }

  public BattleUnit GetTileUnit(Vector2 tile_pos)
  {
    foreach(var unit in units.Values)
    {
      if(unit.GetTilePosition().Equals(tile_pos))
        return unit;
    }

    return null;
  }

  public BattleUnit FindUnitToAttack(Vector2 pos, uint team_index)
  {
    BattleUnit rel_unit = null;
    float min_hp = float.MaxValue;
    var neighbour_points = Pathfinder.GetPointNeighbours(pos, with_diagonal: true);

    foreach(var point in neighbour_points)
    {
      var tile_unit = GetTileUnit(point);
      if(tile_unit == null || tile_unit.GetTeamIndex() == team_index)
        continue;
      
      var hp = tile_unit.GetHP();
      if(hp < min_hp)
      {
        min_hp = hp;
        rel_unit = tile_unit;
      }
    }

    return rel_unit;
  }

  public void AddToWalkable(Vector2 point)
  {
    walkable_points.Add(point);
  }

  public void RemoveFromWalkable(Vector2 point)
  {
    walkable_points.Remove(point);
  }

  public bool ContainsUnitAtPoint(Vector2 point)
  {
    return !walkable_points.Contains(point);
  }

  public void Reset()
  {
    foreach(var unit in units.Values)
      GameObject.Destroy(unit.gameObject);

    units.Clear();
    walkable_points.Clear();
    walkable_points.AddRange(bounds.GetAllPoints());
  }
}

public class BattleManager
{
  const int MIN_UNITS_IN_TEAM = 2;
  const int MAX_UNITS_IN_TEAM = 6;

  FieldBounds team_1_spawn_bounds = new FieldBounds(-3, 2, 3, 3);
  FieldBounds team_2_spawn_bounds = new FieldBounds(-3, 2, -4, -4);

  Battleground battleground;

  public BattleManager()
  {
    this.battleground = Game.GetBattleground();
    Error.Assert(this.battleground != null, "Unable to create BattleManager - battleground is not exist.");
  }

  public void StartBattle()
  {
    battleground.OnUnitDead += OnUnitDead;

    var team_1_units_count = UnityEngine.Random.Range(MIN_UNITS_IN_TEAM, MAX_UNITS_IN_TEAM);
    SpawnUnits("Prefabs/dog_brown", team_1_spawn_bounds, team_1_units_count, team_index: 0);

    var team_2_units_count = UnityEngine.Random.Range(MIN_UNITS_IN_TEAM, MAX_UNITS_IN_TEAM);
    SpawnUnits("Prefabs/dog_gray", team_2_spawn_bounds, team_2_units_count, team_index: 1);
  }

  public void StopBattle()
  {
    battleground.OnUnitDead -= OnUnitDead;

    DOTween.KillAll(); //TODO: this is so bad, must kill only battleground tweens

    Game.TrySwitchState(GameMode.IdleScreen);
  }

  void OnUnitDead(uint id, uint team_index)
  {
    var alive_team_units = battleground.GetTeamUnitsCount(team_index);
    if(alive_team_units == 0)
      StopBattle();
  }

  async void SpawnUnits(string unit_prefab, FieldBounds bounds, int count, uint team_index)
  {
    var battleground = Game.GetBattleground();
    var tilemap = battleground.GetMainTilemap();

    var points = bounds.GetAllPoints();
    points.Shuffle();

    if(points.Count < count)
    {
      Debug.LogWarning($"Can't spawn {count} units, trim to free points count.");
      count = points.Count;
    }

    for(int i = 0; i < count; i++)
    {
      var asset = await Assets.LoadAsync(unit_prefab);
      var unit = asset.AddComponentOnce<BattleUnit>();
      unit.Init(team_index);
      
      var cell_x = Mathf.RoundToInt(points[i].x);
      var cell_y = Mathf.RoundToInt(points[i].y);

      var point = new Vector3Int(cell_x, cell_y, 0);
      var world_pos = tilemap.CellToWorld(point);
      world_pos.x += Battleground.UNIT_WORLD_POS_OFFSET_X;
      world_pos.y += Battleground.UNIT_WORLD_POS_OFFSET_Y;
      
      unit.transform.position = world_pos;

      battleground.AddUnit(unit);
    }
  }
}

}
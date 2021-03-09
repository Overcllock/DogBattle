using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
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

  uint id;
  public uint GetID() => id;
  public void SetID(uint value) => id = value;

  float hp;
  public float GetHP() => hp;
  public float GetHPPercent() => Mathf.Clamp01(hp / MAX_HP);

  uint team_index;
  public uint GetTeamIndex() => team_index;

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

    public override void OnEnter()
    {

    }

    public override void OnUpdate()
    {
      
    }

    public override void OnExit()
    {
      
    }
  }

  class StateMoving : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Moving; }

    public override void OnEnter()
    {

    }

    public override void OnUpdate()
    {
      
    }

    public override void OnExit()
    {
      
    }
  }

  class StateAttacking : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Attacking; }

    public override void OnEnter()
    {

    }

    public override void OnUpdate()
    {
      
    }

    public override void OnExit()
    {
      
    }
  }

  class StateDying : State
  {
    public override BattleUnitState GetMode() { return BattleUnitState.Dying; }

    public override void OnEnter()
    {

    }

    public override void OnUpdate()
    {
      
    }

    public override void OnExit()
    {
      
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
  //

  FieldBounds bounds = new FieldBounds(MIN_X, MAX_X, MIN_Y, MAX_Y);

  Tilemap main_tilemap;
  public Tilemap GetMainTilemap() => main_tilemap;

  Tilemap decor_tilemap;

  Pathfinder pathfinder;

  bool is_loaded = false;
  public bool IsLoaded() => is_loaded;

  Dictionary<uint, BattleUnit> units = new Dictionary<uint, BattleUnit>();

  public async UniTask Load()
  {
    var location = await Assets.TryReuseAsync("Prefabs/location");
    Error.Assert(location != null, "Location loading failed.");

    var main_tilemap_go = location.GetChild("grid/battleground_tilemap");
    main_tilemap = main_tilemap_go.GetComponent<Tilemap>();

    var decor_tilemap_go = location.GetChild("grid/decor_tilemap");
    decor_tilemap = decor_tilemap_go.GetComponent<Tilemap>();
    
    Error.Assert(main_tilemap_go != null && decor_tilemap != null, "Tilemaps init failed.");

    pathfinder = new Pathfinder(main_tilemap, decor_tilemap, bounds);

    is_loaded = true;
  }

  public void AddUnit(BattleUnit unit)
  {
    var id = units.Keys.Count > 0 ? units.Keys.Max() + 1 : 0;
    unit.SetID(id);
    units.Add(id, unit);
  }

  public BattleUnit GetUnit(uint id)
  {
    if(!units.ContainsKey(id))
      return null;
    
    return units[id];
  }

  public BattleUnit FindNearestEnemyUnit(Vector2 point, uint team_index)
  {
    int min_dist = bounds.GetWidth() * bounds.GetHeight();
    BattleUnit nearest_unit = null;

    foreach(var unit in units.Values)
    {
      var unit_tilepos = unit.GetTilePosition();
      var dist = Pathfinder.GetHeuristicPathLength(point, unit_tilepos);
      if(dist < min_dist)
        nearest_unit = unit;
    }

    return nearest_unit;
  }

  public void Reset()
  {
    units.Clear();
  }
}

public class BattleManager
{
  const int MIN_UNITS_IN_TEAM = 2;
  const int MAX_UNITS_IN_TEAM = 6;

  FieldBounds team_1_spawn_bounds = new FieldBounds(-3, 2, 3, 3);
  FieldBounds team_2_spawn_bounds = new FieldBounds(-3, 2, -4, -4);

  public void StartBattle()
  {
    //Team 1
    var team_1_units_count = UnityEngine.Random.Range(MIN_UNITS_IN_TEAM, MAX_UNITS_IN_TEAM);
    SpawnUnits("Prefabs/dog_brown", team_1_spawn_bounds, team_1_units_count, team_index: 0);

    //Team 2
    var team_2_units_count = UnityEngine.Random.Range(MIN_UNITS_IN_TEAM, MAX_UNITS_IN_TEAM);
    SpawnUnits("Prefabs/dog_gray", team_2_spawn_bounds, team_2_units_count, team_index: 1);
  }

  public void StopBattle()
  {
    Game.TrySwitchState(GameMode.IdleScreen);
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
      
      var cell_x = Mathf.RoundToInt(points[i].x);
      var cell_y = Mathf.RoundToInt(points[i].y);

      var point = new Vector3Int(cell_x, cell_y, 0);
      var world_pos = tilemap.CellToWorld(point);
      
      unit.transform.position = world_pos;

      battleground.AddUnit(unit);
    }
  }

  public void Reset()
  {

  }
}

}
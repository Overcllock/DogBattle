using UnityEngine;
using Cysharp.Threading.Tasks;

namespace game 
{

public enum GameMode
{
  None,
  Loading,
  IdleScreen,
  Battle
}

public class GameLoop
{
  StateMachine<GameMode> fsm;

  public GameMode current_mode => fsm.CurrentState();

  abstract class State
  {
    public abstract GameMode GetMode();
    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }
    
    public StateMachine<GameMode> fsm;
  }

  void AddState(State state)
  {
    state.fsm = fsm;
    fsm.Add(state.GetMode(), state.OnEnter, state.OnUpdate, state.OnExit);
  }
  
  public void Init()
  {
    fsm = new StateMachine<GameMode>();
    
    AddState(new StateLoading());
    AddState(new StateIdleScreen());
    AddState(new StateBattle());
    
    fsm.SwitchTo(GameMode.Loading);
  }

  public void Tick()
  {
    fsm?.Update();
  }

  public bool TrySwitchTo(GameMode state)
  {
    if(fsm.CurrentState() == state)
      return false;
    SwitchTo(state);
    return true;
  }

  void SwitchTo(GameMode state)
  {
    fsm.SwitchTo(state);
  }

  class StateLoading : State
  {
    public override GameMode GetMode() { return GameMode.Loading; }

    bool loading_completed = false;
    UIWindow ui;

    public override async void OnEnter()
    {
      ui = await UI.OpenAsync("loading");
      await Loading();
    }

    public override void OnUpdate()
    {
      if(!loading_completed)
        return;

      fsm.TrySwitchTo(GameMode.IdleScreen);
    }

    public override async void OnExit()
    {
      await ui.CloseAsync();
    }

    async UniTask Loading()
    {
      await Assets.PreloadAsync("Prefabs/location");
      await UI.Preload("start");
      await UI.Preload("hud");

      loading_completed = true;
    }
  }

  class StateIdleScreen : State
  {
    public override GameMode GetMode() { return GameMode.IdleScreen; }

    UIWindow ui;

    public override async void OnEnter()
    {
      Assets.TryReuse("Prefabs/location");
      await UniTask.WaitWhile(() => UI.Exists("loading"));
      ui = await UI.OpenAsync("start");
    }

    public override async void OnExit()
    {
      await ui.CloseAsync();
    }
  }

  class StateBattle : State
  {
    public override GameMode GetMode() { return GameMode.Battle; }

    UIWindow ui;

    public override async void OnEnter()
    {
      await UniTask.WaitWhile(() => UI.Exists("start"));
      ui = UI.OpenSync("hud");
    }
  }
}

} //namespace game

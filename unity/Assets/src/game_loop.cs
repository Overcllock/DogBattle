using UnityEngine;

namespace game 
{

public enum GameMode
{
  None,
  Loading
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
    
    fsm.SwitchTo(GameMode.Loading);
  }

  public void Tick()
  {
    fsm.Update();
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

    public override void OnEnter()
    {
      
    }

    public override void OnExit()
    {
      
    }
  }
}

} //namespace game

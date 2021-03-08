using System.Collections.Generic;
using UnityEngine;

namespace game 
{

public class StateMachine<T>
{
  class State
  {
    public T id;
    public StateAction OnEnter;
    public StateAction OnUpdate;
    public StateAction OnExit;

    public State(T id, StateAction OnEnter, StateAction OnUpdate, StateAction OnExit)
    {
      this.id = id;
      this.OnEnter = OnEnter;
      this.OnUpdate = OnUpdate;
      this.OnExit = OnExit;
    }
  }

  bool enable_log = true;

  State current_state = null;
  Dictionary<T, State> states = new Dictionary<T, State>();

  public delegate void StateAction();

  public void Add(T id, StateAction OnEnter, StateAction OnUpdate, StateAction OnExit)
  {
    var new_state = new State(id, OnEnter, OnUpdate, OnExit);
    states.Add(id, new_state);
  }

  public T CurrentState()
  {
    return current_state.id;
  }

  public void Update()
  {
    if(current_state != null)
      current_state?.OnUpdate();
  }

  public void Shutdown()
  {
    if(current_state != null)
      current_state?.OnExit();
    current_state = null;
  }

  public void TrySwitchTo(T state)
  {
    if(current_state != null && current_state.id.Equals(state))
      return;
    
    SwitchTo(state);
  }

  public void SwitchTo(T state)
  {
    Error.Assert(states.ContainsKey(state), "Trying to switch to unknown state " + state.ToString());
    Error.Assert(current_state == null || !current_state.id.Equals(state), "Trying to switch to " + state.ToString() + " but that is already current state");

    var new_state = states[state];
    if(enable_log)
      Debug.Log("Switching state: " + (current_state != null ? current_state.id.ToString() : "null") + " -> " + state.ToString());

    if(current_state != null)
      current_state?.OnExit();

    if(new_state.OnEnter != null)
      new_state?.OnEnter();

    current_state = new_state;
  }

  public void LogSetEnabled(bool enabled)
  {
    this.enable_log = enabled;
  }
}

} //namespace game

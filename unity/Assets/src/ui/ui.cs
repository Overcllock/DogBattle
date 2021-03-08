using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;

namespace game 
{

public class UI 
{
  static GameObject root;

  public static Dictionary<uint, UIWindow> idle_windows = new Dictionary<uint, UIWindow>();
  public static Dictionary<uint, UIWindow> opened_windows = new Dictionary<uint, UIWindow>();

  public static void Init()
  {
    root = Assets.Load("UI/ui_root");
    Error.Verify(root != null);
  }

  static UIWindow LoadWindowSync(string name)
  {
    var prefab = Assets.Load($"UI/windows/{name}", root.transform);
    var window = prefab.GetComponent<UIWindow>();

    Error.Assert(window != null, $"Failed to load UI window ({name}). UIWindow component not found.");

    return window;
  }

  static async UniTask<UIWindow> LoadWindowAsync(string name)
  {
    var prefab = await Assets.LoadAsync($"UI/windows/{name}", root.transform);
    var window = prefab.GetComponent<UIWindow>();

    Error.Assert(window != null, $"Failed to load UI window ({name}). UIWindow component not found.");

    return window;
  }

  public static async UniTask<UIWindow> Preload(string name)
  {
    var window = await LoadWindowAsync(name);
    return window;
  }

  public static UIWindow OpenSync(string name)
  {
    var window = LoadWindowSync(name);
    window.TryExecuteCommand("open");
    return window;
  }

  public static async UniTask<UIWindow> OpenAsync(string name)
  {
    var window = await LoadWindowAsync(name);
    window.TryExecuteCommand("open");
    return window;
  }

  public static void CloseAll(bool force = false)
  {
    foreach(var window in opened_windows.Values)
      window.TryExecuteCommand("close");
  }

  public static void Close(string name, bool force = false)
  {
    var hash = Hash.CRC32(name);
    if(!opened_windows.ContainsKey(hash))
    {
      Debug.LogError($"Window {name} doesn't exist.");
      return;
    }

    var window = opened_windows[hash];
    window.TryExecuteCommand("close");
  }
}

public enum UIWindowState
{
  None,
  PreInit,
  Init,
  Idle,
  Opening,
  Opened,
  Closing,
  Closed
};

public abstract class UIWindow : MonoBehaviour
{
  protected StateMachine<UIWindowState> fsm = new StateMachine<UIWindowState>();
  protected CommandProcessor cmd_proc = new CommandProcessor();

  abstract class State
  {
    public abstract UIWindowState GetMode();
    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }
    
    public StateMachine<UIWindowState> fsm;
    public UIWindow window;
  }

  public UIWindowState GetCurrentState() => fsm.CurrentState();

  bool pre_init_completed = false;
  bool init_completed = false;

  uint name_hash;
  public uint GetNameHash() => name_hash;
  public void SetNameHash(uint hash) => name_hash = hash;

  protected void Awake()
  {
    InitFSM();
    fsm.TrySwitchTo(UIWindowState.PreInit);
  }

  protected virtual void PreInit()
  {
    DefineCommands();
    pre_init_completed = true;
  }

  protected virtual void Init()
  {
    init_completed = true;
  }

  void InitFSM()
  {
    AddState(new StatePreInit());
    AddState(new StateInit());
    AddState(new StateIdle());
    AddState(new StateOpening());
    AddState(new StateOpened());
    AddState(new StateClosing());
    AddState(new StateClosed());
  }

  void AddState(State state)
  {
    state.fsm = fsm;
    state.window = this;
    fsm.Add(state.GetMode(), state.OnEnter, state.OnUpdate, state.OnExit);
  }

  void FixedUpdate()
  {
    fsm.Update();
    cmd_proc?.Tick();
  }

  void OnDestroy()
  {
    fsm.Shutdown();
    cmd_proc.Destroy();
  }

#region UIWindow states

  class StatePreInit : State
  {
    public override UIWindowState GetMode() { return UIWindowState.PreInit; }

    public override void OnEnter()
    {
      window.PreInit();
    }

    public override void OnUpdate()
    {
      if(!window.pre_init_completed)
        return;
      
      fsm.TrySwitchTo(UIWindowState.Init);
    }
  }

  class StateInit : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Init; }

    public override void OnEnter()
    {
      window.Init();
    }

    public override void OnUpdate()
    {
      if(!window.init_completed)
        return;
      
      fsm.TrySwitchTo(UIWindowState.Idle);
    }
  }

  class StateIdle : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Idle; }

    uint hash;

    public override void OnEnter()
    {
      hash = window.GetNameHash();
      UI.idle_windows.Add(hash, window);
    }

    public override void OnExit()
    {
      UI.idle_windows.Remove(hash);
    }
  }

  class StateOpening : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Opening; }

    public override void OnEnter()
    {
      //TODO: block user input

      window.TryExecuteCommand("do_open_anim");
    }

    public override void OnUpdate()
    {
      //TODO: waiting for open anim

      fsm.TrySwitchTo(UIWindowState.Opened);
    }

    public override void OnExit()
    {
      //TODO: unblock user input
    }
  }

  class StateOpened : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Opened; }

    uint hash;

    public override void OnEnter()
    {
      hash = window.GetNameHash();
      UI.opened_windows.Add(hash, window);
    }

    public override void OnExit()
    {
      //TODO: block user input

      UI.opened_windows.Remove(hash);
    }
  }

  class StateClosing : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Closing; }

    public override void OnEnter()
    {
      //TODO: playing anims and etc
    }

    public override void OnUpdate()
    {
      //TODO: waiting for anims and etc
      fsm.TrySwitchTo(UIWindowState.Closed);
    }

    public override void OnExit()
    {
      
    }
  }

  class StateClosed : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Closed; }

    public override void OnEnter()
    {
      Assets.Release(window.gameObject);
    }
  }

#endregion

#region UIWindow commands
  public bool TryExecuteCommand(string command)
  {
    if(!cmd_proc.CommandExists(command))
      return false;

    cmd_proc.ExecuteCommand(command);
    return true;
  }

  protected virtual void DefineCommands()
  {
    cmd_proc.DefineSyncCommand("open", Open);
    cmd_proc.DefineSyncCommand("close", Close);
  }

  protected virtual void Open()
  {
    fsm.TrySwitchTo(UIWindowState.Opening);
  }

  protected virtual void Close()
  {
    fsm.TrySwitchTo(UIWindowState.Closing);
  }

#endregion
}

} //namespace game
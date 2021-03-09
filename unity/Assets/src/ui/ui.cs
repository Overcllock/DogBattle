using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

namespace game 
{

public class UI 
{
  static GameObject root;
  static Transform windows_container;

  public static Image blackout;

  public static Dictionary<uint, UIWindow> idle_windows = new Dictionary<uint, UIWindow>();
  public static Dictionary<uint, UIWindow> opened_windows = new Dictionary<uint, UIWindow>();

  public static void Init()
  {
    root = Assets.Load("Prefabs/ui/ui_root");
    Error.Verify(root != null);

    windows_container = root.GetChild("windows")?.transform;
    Error.Verify(windows_container != null);

    blackout = root.GetChild("blackout")?.GetComponent<Image>();
    Error.Verify(blackout != null);

    //EnableBlackout(true);
  }

  public static void EnableBlackout(bool enable)
  {
    blackout?.gameObject.SetActive(enable);
  }

  static UIWindow LoadWindowSync(string name)
  {
    var prefab = Assets.TryReuse($"Prefabs/ui/windows/{name}", parent: windows_container.transform);
    var window = prefab.GetComponent<UIWindow>();

    Error.Assert(window != null, $"Failed to load UI window ({name}). UIWindow component not found.");

    window.SetNameHash(Hash.CRC32(name));

    return window;
  }

  static async UniTask<UIWindow> LoadWindowAsync(string name)
  {
    var prefab = await Assets.TryReuseAsync($"Prefabs/ui/windows/{name}", parent: windows_container.transform);
    var window = prefab.GetComponent<UIWindow>();

    Error.Assert(window != null, $"Failed to load UI window ({name}). UIWindow component not found.");

    window.SetNameHash(Hash.CRC32(name));

    return window;
  }

  public static async UniTask<UIWindow> Preload(string name)
  {
    var prefab = await Assets.PreloadAsync($"Prefabs/ui/windows/{name}", windows_container.transform);
    Error.Assert(prefab != null, $"Failed to preload UI window ({name}). Prefab not found.");

    var window = prefab.GetComponent<UIWindow>();
    Error.Assert(window != null, $"Failed to preload UI window ({name}). UIWindow component not found.");

    window.SetNameHash(Hash.CRC32(name));

    return window;
  }

  public static UIWindow OpenSync(string name)
  {
    var window = LoadWindowSync(name);
    window.Open();
    return window;
  }

  public static async UniTask<UIWindow> OpenAsync(string name)
  {
    var window = await LoadWindowAsync(name);
    await window.OpenAsync();
    return window;
  }

  public static void CloseAll(bool force = false)
  {
    foreach(var window in opened_windows.Values)
      window.Close();
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
    window.Close();
  }

  public static bool Exists(string name, bool is_opened = true)
  {
    var hash = Hash.CRC32(name);
    return is_opened && opened_windows.ContainsKey(hash) || idle_windows.ContainsKey(hash);
  }

  public static T Find<T>(string name, bool is_opened = true) where T : UIWindow
  {
    var hash = Hash.CRC32(name);
    var targed_pool = is_opened ? opened_windows : idle_windows;

    if(!targed_pool.ContainsKey(hash))
      return null;

    if(targed_pool[hash] is T result)
      return result;

    return null;
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

  protected abstract class State
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
    var cg = GetComponent<CanvasGroup>();
    if(cg != null)
      cg.alpha = 0;
      
    pre_init_completed = true;
  }

  protected virtual void Init()
  {
    init_completed = true;
  }

  void InitFSM()
  {
    fsm.LogSetEnabled(false);

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

  public void TrySwitchTo(UIWindowState new_state)
  {
    fsm.TrySwitchTo(new_state);
  }

  public virtual void Open()
  { 
    TrySwitchTo(UIWindowState.Opened);
  }

  public virtual void Close()
  { 
    TrySwitchTo(UIWindowState.Closed);
  }

  public virtual async UniTask OpenAsync()
  { 
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Idle);
    TrySwitchTo(UIWindowState.Opened);
  }

  public virtual async UniTask CloseAsync()
  { 
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Opened);
    TrySwitchTo(UIWindowState.Closed);
  }

  void FixedUpdate()
  {
    fsm.Update();
  }

  void OnDestroy()
  {
    fsm.Shutdown();
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
      UI.opened_windows.AddUnique(window.GetNameHash(), window);
      window.EnableUIInput(false);
    }

    public override void OnExit()
    {
      window.EnableUIInput(true);
    }
  }

  class StateOpened : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Opened; }

    public override void OnEnter()
    {
      UI.opened_windows.AddUnique(window.GetNameHash(), window);
    }

    public override void OnExit()
    {
      window.EnableUIInput(false);
    }
  }

  class StateClosing : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Closing; }
  }

  class StateClosed : State
  {
    public override UIWindowState GetMode() { return UIWindowState.Closed; }

    public override void OnEnter()
    {
      UI.opened_windows.Remove(window.GetNameHash());
      Assets.Release(window.gameObject);
    }
  }

#endregion
}

} //namespace game
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace game 
{

public class UIHud : UIWindow
{
  const float HP_BAR_Y_OFFSET = 45f;

  GameObject canvas_go;

  Battleground battleground;

  Dictionary<uint, Scrollbar> hp_bars = new Dictionary<uint, Scrollbar>(); 
  Stack<uint> invalid_ids = new Stack<uint>();

  protected override void Init()
  {
    battleground = Game.GetBattleground();
    canvas_go = this.GetChild("canvas");
    this.GetChild("canvas/btn_exit").MakeButton(Exit);
    base.Init();
  }

  void Exit()
  {
    Game.Quit();
  }

  public override async void Open()
  {
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Idle);
    TrySwitchTo(UIWindowState.Opened);
  }

  public override async void Close()
  {
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Opened);
    TrySwitchTo(UIWindowState.Closed);
  }

  void Update()
  {
    UpdateHPBars();
  }

  void UpdateHPBars()
  {
    while(invalid_ids.Count > 0)
    {
      var id = invalid_ids.Pop();
      hp_bars.Remove(id);
    }

    foreach(var kv in hp_bars)
    {
      var unit_id = kv.Key;
      var hp_bar = kv.Value;

      var unit = battleground.GetUnit(unit_id);
      if(unit == null)
      {
        invalid_ids.Push(unit_id);
        Destroy(hp_bar.gameObject);
        continue;
      }

      hp_bar.size = unit.GetHPPercent();

      var screen_pos = Camera.main.WorldToScreenPoint(unit.transform.position);
      screen_pos.y += HP_BAR_Y_OFFSET;

      hp_bar.transform.position = screen_pos;
    }
  }

  public void DefineHPBar(uint unit_id)
  {
    var unit = battleground.GetUnit(unit_id);
    if(unit == null)
      return;
    
    var bar_go = Assets.Load("Prefabs/ui/hp_bar", canvas_go.transform);

    var bar = bar_go.GetComponent<Scrollbar>();
    Error.Assert(bar != null, "Cannot define hp bar: missing scrollbar component.");
    hp_bars.Add(unit_id, bar);
  }
}

}
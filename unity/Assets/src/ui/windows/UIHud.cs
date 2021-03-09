using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;

namespace game 
{

public class UIHud : UIWindow
{
  const float HP_BAR_X_OFFSET = 26f;
  const float HP_BAR_Y_OFFSET = 53f;

  GameObject canvas_go;

  Battleground battleground;

  Dictionary<uint, Scrollbar> hp_bars = new Dictionary<uint, Scrollbar>(); 

  protected override void Init()
  {
    battleground = Game.GetBattleground();
    canvas_go = this.GetChild("canvas");
    base.Init();
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
    foreach(var kv in hp_bars)
    {
      var unit_id = kv.Key;
      var hp_bar = kv.Value;

      var unit = battleground.GetUnit(unit_id);
      if(unit == null)
      {
        Assets.Release(hp_bar.gameObject);
        hp_bars.Remove(unit_id);
        continue;
      }

      hp_bar.value = unit.GetHPPercent();

      var screen_pos = Camera.main.WorldToScreenPoint(unit.transform.position);
      screen_pos.y += HP_BAR_Y_OFFSET;
      screen_pos.x += HP_BAR_X_OFFSET;

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
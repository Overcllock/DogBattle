using UnityEngine;
using Cysharp.Threading.Tasks;

namespace game 
{

public class UIStart : UIWindow
{
  protected override void Init()
  {
    this.GetChild("canvas/btn_start").MakeButton(DoStart);
    base.Init();
  }

  void DoStart()
  {
    Debug.Log("Starting battle...");
    Game.TrySwitchState(GameMode.Battle);
  }

  public override async UniTask OpenAsync()
  {
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Idle);

    TrySwitchTo(UIWindowState.Opening);

    await this.PlayOpenAnim(duration: 0.5f);

    TrySwitchTo(UIWindowState.Opened);
  }

  public override async UniTask CloseAsync()
  {
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Opened);

    TrySwitchTo(UIWindowState.Closing);

    await this.PlayCloseAnim(duration: 0.5f);

    TrySwitchTo(UIWindowState.Closed);
  }
}

}
using Cysharp.Threading.Tasks;

namespace game 
{

public class UILoading : UIWindow
{
  protected override void Init()
  {
    base.Init();
  }

  public override async UniTask OpenAsync()
  {
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Idle);

    TrySwitchTo(UIWindowState.Opening);

    await this.PlayOpenAnim();

    TrySwitchTo(UIWindowState.Opened);
  }

  public override async UniTask CloseAsync()
  {
    await UniTask.WaitWhile(() => GetCurrentState() != UIWindowState.Opened);

    TrySwitchTo(UIWindowState.Closing);

    await this.PlayCloseAnim();

    TrySwitchTo(UIWindowState.Closed);
  }
}

}
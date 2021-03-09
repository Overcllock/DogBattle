using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using DG.Tweening;

namespace game
{

public static class AnimHelper
{
  public static async UniTask PlayOpenAnim(this UIWindow wnd, float duration = 1f)
  {
    if(wnd == null)
      return;

    var cg = wnd.GetComponent<CanvasGroup>();
    if(cg == null)
      return;

    var tween = cg.DOFade(1, duration);
    await tween.AsyncWaitForCompletion();
  }

  public static async UniTask PlayCloseAnim(this UIWindow wnd, float duration = 1f)
  {
    if(wnd == null)
      return;

    var cg = wnd.GetComponent<CanvasGroup>();
    if(cg == null)
      return;

    var tween = cg.DOFade(0, duration);
    await tween.AsyncWaitForCompletion();
  }
}

}
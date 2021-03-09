using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace game 
{
  public class Game : MonoBehaviour
  {
    public static Game self;

    static GameLoop game_loop = new GameLoop();

    void Awake()
    {
      Init();
    }

    void Init()
    {
      self = this;

#if UNITY_EDITOR
      Assets.InitForEditor();
#endif

      DOTween.Init();
      UI.Init();
      game_loop.Init();
    }

    void Update()
    {
      
    }

    void FixedUpdate()
    {
      game_loop.Tick();
    }

    public static void Quit()
    {
      Application.Quit();
    }

    public static void TrySwitchState(GameMode new_state)
    {
      game_loop?.TrySwitchTo(new_state);
    }

    void OnApplicationPause(bool paused)
    {
      
    }

    void OnApplicationQuit()
    {
      
    }
  }
}
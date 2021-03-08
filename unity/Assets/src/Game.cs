using UnityEngine;
using Cysharp.Threading.Tasks;

namespace game 
{
  public class Game : MonoBehaviour
  {
    public static Game self;

    GameLoop game_loop;

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

      UI.Init();
    }

    void Update()
    {
      
    }

    void FixedUpdate()
    {

    }

    public static void Quit()
    {
      Application.Quit();
    }

    void OnApplicationPause(bool paused)
    {
      
    }

    void OnApplicationQuit()
    {
      
    }
  }
}
using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using DG.Tweening;

namespace game
{

public enum BattleUnitState
{
  None,
  Spawning,
  Idle,
  Moving,
  Attacking,
  Dying
}

public class BattleUnit : MonoBehaviour
{
  uint team_index;
  public uint GetTeamIndex() => team_index;


}

public class BattleManager
{
  
}

}
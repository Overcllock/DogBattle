using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UniRx;

namespace game.tasks {
  
public enum EnumGameTaskPriority
{
  Default,
  High,
  Interrupt
}

public interface ITask
{
  bool IsAsync();
  UniTask ProcessAsyncTask();
  void ProcessSyncTask();
  void Subscribe(Action<bool> on_finished);
  void Unsubscribe(Action<bool> on_finished);
  void Stop();
  bool IsParallel();
  bool IsCompleted();
  EnumGameTaskPriority GetPriority();
  CancellationToken GetCancellationToken();
}

public abstract class GameTaskBase : ITask
{
  protected EnumGameTaskPriority task_priority;

  protected Action<bool> on_finished;

  protected CancellationTokenSource cts;
  public CancellationToken GetCancellationToken() => cts != null ? cts.Token : default(CancellationToken);

  protected bool is_parallel;
  public bool IsParallel() => is_parallel;

  protected bool is_completed = false;
  public bool IsCompleted() => is_completed;

  public abstract bool IsAsync();

  public void Stop()
  {
    cts?.Cancel();
    on_finished?.Invoke(true);
  }

  public virtual async UniTask ProcessAsyncTask()
  {
    await UniTask.WaitForFixedUpdate();
  }

  public virtual void ProcessSyncTask()
  {
    //NOTE: do nothing by default
  }

  public void Subscribe(Action<bool> on_finished)
  {
    this.on_finished += on_finished;
  }

  public void Unsubscribe(Action<bool> on_finished)
  {
    this.on_finished -= on_finished;
  }

  public EnumGameTaskPriority GetPriority()
  {
    return task_priority;
  }

  public CancellationToken GetToken()
  {
    return cts.Token;
  }
}

public class GameTaskSync : GameTaskBase
{
  public override bool IsAsync() => false;

  Action task_action;

  public static GameTaskSync Create(Action task_action, EnumGameTaskPriority priority = EnumGameTaskPriority.Default)
  {
    return new GameTaskSync(task_action, priority);
  }

  public GameTaskSync(Action task_action, EnumGameTaskPriority priority)
  {
    this.task_action = task_action;
    this.task_priority = priority;
  }

  public override void ProcessSyncTask()
  {
    if(task_action == null)
      return;

    task_action.Invoke();
    
    on_finished?.Invoke(false);
    is_completed = true;
  }
}

public class GameTaskAsync : GameTaskBase
{
  public override bool IsAsync() => true;

  Func<UniTask> task_action;

  public static GameTaskAsync Create(
    Func<UniTask> task_action, 
    EnumGameTaskPriority priority = EnumGameTaskPriority.Default, 
    CancellationTokenSource cts = default(CancellationTokenSource),
    bool is_parallel = false)
  {
    return new GameTaskAsync(task_action, priority, cts, is_parallel);
  }

  public GameTaskAsync(Func<UniTask> task_action, EnumGameTaskPriority priority, CancellationTokenSource cts, bool is_parallel)
  {
    this.task_action = task_action;
    this.task_priority = priority;
    this.cts = cts;
    this.is_parallel = is_parallel;
  }

  public override async UniTask ProcessAsyncTask()
  {
    if(task_action == null)
      return;

    await UniTask.Create(task_action).AttachExternalCancellation(GetToken());
    
    on_finished?.Invoke(false);
    is_completed = true;
  }
}

public class GameTaskManager
{
  ITask current_task;
  UniTask current_task_value;

  List<ITask> tasks = new List<ITask>();
  Queue<ITask> queued_parallel_tasks = new Queue<ITask>();
  List<ITask> running_parallel_tasks = new List<ITask>();

  public void ScheduleSyncTask(
    Action task_action, 
    Action<bool> on_finished, 
    EnumGameTaskPriority task_priority = EnumGameTaskPriority.Default)
  {
    if(task_action == null)
      return;

    var task = GameTaskSync.Create(task_action, task_priority);
    task.Subscribe(on_finished);
    AddTask(task, task_priority);
  }

  public void ScheduleAsyncTask(
    Func<UniTask> task_action, 
    Action<bool> on_finished, 
    EnumGameTaskPriority task_priority = EnumGameTaskPriority.Default,
    CancellationTokenSource cts = default(CancellationTokenSource),
    bool is_parallel = false)
  {
    if(task_action == null)
      return;

    var task = GameTaskAsync.Create(task_action, task_priority, cts, is_parallel);
    task.Subscribe(on_finished);
    AddTask(task, task_priority);
  }

  public bool IsBusy()
  {
    if(current_task == null)
      return false;
    
    return !current_task.IsCompleted();
  }

  public void Tick()
  {
    UpdateParallel();
    RunParallel();

    if(IsBusy())
      return;
    
    RunNext();
  }

  void StopCurrent()
  {
    if(current_task != null)
    {
      current_task.Stop();
      current_task = null;
      current_task_value.Forget();
    }
  }

  void StopAll()
  {
    UpdateParallel();
    StopCurrent();

    foreach(var task in tasks)
      task.Stop();
    
    foreach(var task in running_parallel_tasks)
      task.Stop();
  }

  public void Clear()
  {
    StopAll();
    tasks.Clear();
    queued_parallel_tasks.Clear();
    running_parallel_tasks.Clear();
  }

  void AddTask(ITask task, EnumGameTaskPriority priority)
  {
    if(task.IsParallel())
    {
      queued_parallel_tasks.Enqueue(task);
      return;
    }

    switch(priority)
    {
      case EnumGameTaskPriority.Default:
      {
        tasks.Add(task);
      }
      break;

      case EnumGameTaskPriority.High:
      {
        tasks.Insert(0, task);
      }
      break;

      case EnumGameTaskPriority.Interrupt:
      {
        if(current_task != null && current_task.GetPriority() != EnumGameTaskPriority.Interrupt)
          StopCurrent();
        tasks.Insert(0, task);
      }
      break;
    }
  }

  void RunNext()
  {
    current_task = GetNextTask();
    if(current_task == null)
    {
      current_task_value = UniTask.CompletedTask;
      return;
    }

    if(current_task.IsAsync())
      current_task_value = current_task.ProcessAsyncTask().AttachExternalCancellation(current_task.GetCancellationToken());
    else
      current_task.ProcessSyncTask();
  }

  void RunParallel()
  {
    if(queued_parallel_tasks.Count == 0)
      return;

    var task = queued_parallel_tasks.Dequeue();
    task.ProcessAsyncTask().AttachExternalCancellation(task.GetCancellationToken());

    running_parallel_tasks.Add(task);
  }

  void UpdateParallel()
  {
    running_parallel_tasks.RemoveAll(task => task.IsCompleted());
  }

  ITask GetNextTask()
  {
    if(tasks.Count > 0)
    {
      var task = tasks[0]; 
      tasks.RemoveAt(0);
      return task;
    }
    else
      return null;
  }

  public ITask GetCurrentTask()
  {
    return current_task;
  }
}

}
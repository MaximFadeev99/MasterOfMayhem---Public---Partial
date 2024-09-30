using Cysharp.Threading.Tasks;
using MasterOfMayhem.Tasks;
using MasterOfMayhem.Humanoids.AbstractHumanoid;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterOfMayhem.Base
{
    public class TaskManager
    {
        private readonly Dictionary<Task, Humanoid[]> _activeTasks = new();
        private readonly List<Task> _pendingTasks = new();
        private readonly HumanResourceManager _humanResourceManager;
        private readonly TaskPriorities _absolutePriority;

        private bool _isWorking = false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="humanResourceManager"></param>
        /// <param name="absolutePriority">TaskManager will interrupt minions' recreational activities if there are no idling minions 
        /// and the priority of the assigned task equals or is higher than this value</param>
        public TaskManager(HumanResourceManager humanResourceManager, TaskPriorities absolutePriority) 
        {
            _humanResourceManager = humanResourceManager;
            _absolutePriority = absolutePriority;
            _isWorking = true;
            _ = AssignTask();
            _ = ReplaceFailedExecutorsInActiveTasks();
        }

        public void RegisterNewTask(Task newTask) 
        {
            _pendingTasks.Add(newTask);
            newTask.Completed += OnTaskCompleted;
        }

        public Humanoid[] InquireExecutors(Task targetTask) 
        {
            if (_activeTasks.ContainsKey(targetTask) == false)
                return null;

            return _activeTasks[targetTask];
        }

        public void OnDestroy()
        {
            _isWorking = false;
        }

        private async UniTaskVoid AssignTask() 
        {
            while(_isWorking) 
            {
                if (_pendingTasks.Count != 0) 
                {
                    Task nextTaskToAssign = GetTaskWithHighestPriority();
                    Humanoid[] chosenExecutors = GetAppropriateExecutors(nextTaskToAssign);

                    if (chosenExecutors != null) 
                    {
                        Humanoid[] assignedExecutors = new Humanoid[nextTaskToAssign.MaxExecutorCount];

                        for (int i = 0; i < chosenExecutors.Length; i++)
                        {
                            assignedExecutors[i] = chosenExecutors[i];
                            assignedExecutors[i].TaskHandlingAspect.SetTask(nextTaskToAssign);
                        }

                        _pendingTasks.Remove(nextTaskToAssign);
                        _activeTasks.Add(nextTaskToAssign, assignedExecutors);

                    }
                }

                await UniTask.Yield();
            }                
        }

        private async UniTaskVoid ReplaceFailedExecutorsInActiveTasks() 
        {
            while (_isWorking) 
            {
                if (_activeTasks.Count != 0) 
                {
                    Task nextTask = _activeTasks
                        .FirstOrDefault(activeTask => activeTask.Value
                        .Any(executor => executor == null || executor.CanCompleteTask == false)).Key;

                    if (nextTask != null) 
                    {
                        EliminationTask eliminationTask = nextTask is EliminationTask eTask ? eTask : null;
                        List<int> indicesForReplacement = new();

                        for (int i = 0; i < _activeTasks[nextTask].Length; i++)
                        {
                            if (_activeTasks[nextTask][i] == null) 
                            {
                                indicesForReplacement.Add(i);
                                continue;
                            }

                            if (_activeTasks[nextTask][i].CanCompleteTask == false ||
                                _activeTasks[nextTask][i].CurrentHealth < _activeTasks[nextTask][i].EscapeAspect.EscapeHealthThreshold)
                            {
                                indicesForReplacement.Add(i);
                                _activeTasks[nextTask][i].TaskHandlingAspect.SetTask(null);
                                eliminationTask?.RemoveEngagedMinion(_activeTasks[nextTask][i]);
                                _activeTasks[nextTask][i] = null;
                            }
                        }

                        Humanoid[] replacingHumanoids = GetAppropriateExecutors(nextTask);

                        if (replacingHumanoids != null) 
                        {
                            for (int i = 0; i < indicesForReplacement.Count; i++)
                            {
                                if (i > replacingHumanoids.Length - 1)
                                    break;

                                _activeTasks[nextTask][indicesForReplacement[i]] = replacingHumanoids[i];
                                replacingHumanoids[i].TaskHandlingAspect.SetTask(nextTask);
                            }
                        }                                             
                    }           
                }

                await UniTask.Yield();
            }        
        }

        private Task GetTaskWithHighestPriority() 
        {
            return _pendingTasks.OrderByDescending(task => task.Priority).First();
        }

        private Humanoid[] GetAppropriateExecutors(Task taskToExecute) 
        {
            IEnumerable<Humanoid> potentialExecutors = GetExecutorsByTaskPriority(taskToExecute);
            int assignedExecutorCount = _activeTasks.ContainsKey(taskToExecute) ?
                _activeTasks[taskToExecute].Where(executor => executor != null).Count() : 0;          

            if (potentialExecutors.Count() == 0)
                return null;

            if (taskToExecute is EliminationTask eliminationTask)
            {
                potentialExecutors = GetBestExecutorsForEliminationTask(potentialExecutors, eliminationTask, assignedExecutorCount);
            }
            else 
            {
                potentialExecutors = potentialExecutors
                                    .OrderBy(minion =>Vector3.Distance(minion.Transform.position, taskToExecute.Transform.position))
                                    .Take(taskToExecute.MaxExecutorCount - assignedExecutorCount);
            }

            return potentialExecutors.ToArray();
        }

        private IEnumerable<Humanoid> GetExecutorsByTaskPriority(Task targetTask) 
        {
            bool isTaskHasAbsolutePriotiry = targetTask.Priority >= _absolutePriority;
            IEnumerable<Humanoid> potentialExecutors = isTaskHasAbsolutePriotiry ?
                _humanResourceManager.AllMinions.Where(minion => minion.RecreationalAspect.CurrentActionPoints >= 0 &&
                minion.TaskHandlingAspect.CurrentTask.Value != targetTask) :
                _humanResourceManager.AllMinions.Where(minion => minion.IsIdling &&
                       minion.RecreationalAspect.CurrentActionPoints >= targetTask.FatiguePoints &&
                       minion.RecreationalAspect.IsRecreating == false);

            return potentialExecutors;
        }

        private IEnumerable<Humanoid> GetBestExecutorsForEliminationTask
            (IEnumerable<Humanoid> potentialExecutors, EliminationTask eliminationTask, int assignedExecutorCount) 
        {
            potentialExecutors = potentialExecutors
                    .Where(executor => executor.TaskHandlingAspect.CurrentTask.Value != eliminationTask &&
                           executor.EscapeAspect.IsRunningAway == false &&
                           executor.CurrentHealth > executor.EscapeAspect.EscapeHealthThreshold);
            potentialExecutors = potentialExecutors
                                .OrderBy(minion => Vector3.Distance(minion.Transform.position, eliminationTask.EnemyAgent.Transform.position))
                                .Take(eliminationTask.MaxExecutorCount - assignedExecutorCount);

            foreach (Humanoid executor in potentialExecutors) 
            {
                if (executor.IsIdling == false) 
                    executor.TaskHandlingAspect.CurrentTask.Value.Cancel();
            }

            return potentialExecutors;
        }

        private void OnTaskCompleted(Task accomplishedTask) 
        {
            if (_pendingTasks.Contains(accomplishedTask))
                _pendingTasks.Remove(accomplishedTask);

            if (_activeTasks.ContainsKey(accomplishedTask))
                _activeTasks.Remove(accomplishedTask);

            accomplishedTask.Completed -= OnTaskCompleted;
        }
    }
}
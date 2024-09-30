using Cysharp.Threading.Tasks;
using MasterOfMayhem.Humanoids.AbstractHumanoid;
using MasterOfMayhem.PlaceableObjects.Buildings;
using MasterOfMayhem.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfMayhem.Base
{
    public class WorkingPlaceManager
    {
        private readonly List<WorkingPlace> _allWorkingPlaces = new();
        private readonly Dictionary<WorkingPlaceTask, Humanoid> _issuedWorkingTasks = new();
        private readonly TaskManager _taskManager;

        private bool _isWorking;

        public WorkingPlaceManager(TaskManager taskManager) 
        {
            _taskManager = taskManager;
            _isWorking = true;
            _ = IssueTasksForVacantWorkingPlaces();
            _ = RelayAssignedMinionsToWorkingPlaces();
        }

        public void InformOfNewWorkingPlaces(IReadOnlyList<WorkingPlace> newlyAddedPlaces)
        {
            foreach (WorkingPlace workingPlace in newlyAddedPlaces)
            {
                _allWorkingPlaces.Add(workingPlace);
                workingPlace.Demolished += OnWorkingPlaceDemolished;
            }
        }

        public void OnDestroy()
        {
            _isWorking = false;
        }

        private async UniTaskVoid IssueTasksForVacantWorkingPlaces() 
        {
            while(_isWorking) 
            {
                if (_allWorkingPlaces.Count != 0) 
                {
                    WorkingPlace nextWorkingPlace = _allWorkingPlaces
                        .Where(workingPlace => _issuedWorkingTasks
                                               .Any(workingTask => workingTask.Key.WorkingPlace == workingPlace) == false && 
                        (workingPlace.AssignedMinion == null ||  workingPlace.AssignedMinion.CanCompleteTask == false) &&
                         workingPlace.IsConstructed && workingPlace.IsSabotaged.Value == false)
                        .OrderByDescending(workingPlace => workingPlace.StopWorkingTimer)
                        .FirstOrDefault();

                    if (nextWorkingPlace != null)
                    {
                        WorkingPlaceTask newTask = new(nextWorkingPlace, nextWorkingPlace.Transform, TaskPriorities.Low, 1);
                        _issuedWorkingTasks.Add(newTask, null);
                        newTask.Completed += OnWorkingPlaceTaskCompleted;
                        _taskManager.RegisterNewTask(newTask);
                    }
                }

                await UniTask.WaitForSeconds(1.5f);
            }     
        }

        private async UniTaskVoid RelayAssignedMinionsToWorkingPlaces() 
        {
            while (_isWorking) 
            {
                WorkingPlaceTask[] tasksWithoutMinions = _issuedWorkingTasks
                                                                        .Where(kvp => kvp.Value == null)
                                                                        .Select(kvp => kvp.Key)
                                                                        .ToArray();

                if (tasksWithoutMinions.Length != 0) 
                {
                    foreach (WorkingPlaceTask task in tasksWithoutMinions) 
                    {
                        Humanoid[] assignedExecutor = _taskManager.InquireExecutors(task);

                        if (assignedExecutor != null)
                        {
                            task.WorkingPlace.AssignWorkingMinion(assignedExecutor[0]);
                            task.Start();
                            _issuedWorkingTasks[task] = assignedExecutor[0];
                        }
                    }
                }

                await UniTask.WaitForSeconds(0.7f);
            } 
        }

        private void OnWorkingPlaceDemolished(WorkingPlace demolishedWorkingPlace)
        {
            demolishedWorkingPlace.Demolished -= OnWorkingPlaceDemolished;
            _allWorkingPlaces.Remove(demolishedWorkingPlace);

            WorkingPlaceTask targetTask = _issuedWorkingTasks
                .FirstOrDefault(kvp => kvp.Key.WorkingPlace == demolishedWorkingPlace)
                .Key;

            if (targetTask == null)
                return;

            targetTask.Cancel();
            _issuedWorkingTasks.Remove(targetTask);
        }

        private void OnWorkingPlaceTaskCompleted(Task completedTask) 
        {
            completedTask.Completed -= OnWorkingPlaceTaskCompleted;

            if (completedTask is WorkingPlaceTask workingPlaceTask == false)
                return;

            _issuedWorkingTasks.Remove(workingPlaceTask);
        }
    }
}
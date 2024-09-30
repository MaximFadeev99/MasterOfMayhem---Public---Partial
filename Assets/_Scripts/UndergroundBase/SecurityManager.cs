using Cysharp.Threading.Tasks;
using MasterOfMayhem.Humanoids.AbstractHumanoid;
using MasterOfMayhem.Humanoids.Enemies;
using MasterOfMayhem.PlaceableObjects.Buildings;
using MasterOfMayhem.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfMayhem.Base
{
    [Serializable]
    public class SecurityManager
    {
        private readonly List<Humanoid> _reportedEnemyAgents = new();
        private readonly Queue<PassApplication> _pendingApplications = new();

        private TaskManager _taskManager;
        private IReadOnlyList<UndergroundEntrance> _constructedEntrances = null;
        private bool _isWorking;

        public Action<Humanoid, bool> DecisionOnApplicationMade;

        public void Intialize(TaskManager taskManager) 
        {
            _taskManager = taskManager;
            _isWorking = true;
            _ = HandlePendingPassApplications();
        }

        public void UpdateEntrances(IReadOnlyList<UndergroundEntrance> constructedEntrances) 
        {
            _constructedEntrances = constructedEntrances;
        }

        public void RegisterPassApplication(PassApplication application) 
        {
            application.IsApproved = false;
            _pendingApplications.Enqueue(application);
        }

        public void OnDestroy() 
        {
            _isWorking = false;
        }

        private async UniTaskVoid HandlePendingPassApplications() 
        {
            while (_isWorking) 
            {
                if (_pendingApplications.Count != 0) 
                {
                    PassApplication currentApplication = _pendingApplications.Dequeue();
                    bool isApproved = LookIntoPassApplication(currentApplication);

                    if (isApproved) 
                    {
                        _ = currentApplication.UndergroundEntrance.Open();
                        await UniTask.WaitUntil(() => currentApplication.UndergroundEntrance.AreDoorsOpen);
                    }

                    currentApplication.CompletionSource.TrySetResult(isApproved);
                }
            
                await UniTask.Yield();
            }       
        }

        public bool LookIntoPassApplication(PassApplication passApplication)
        {
            if (_constructedEntrances == null || passApplication.UndergroundEntrance == null ||
                _constructedEntrances.Contains(passApplication.UndergroundEntrance) == false)
            {
                return false;
            }

            //verification logic
            //**** Currently everyone gets approved ****
            //result of verification process

            passApplication.IsApproved = true;

            return passApplication.IsApproved;
        }

        public void ReportEnemy(EnemyAgent spottedAgent)
        {
            if (_reportedEnemyAgents.Contains(spottedAgent))
                return;

            _reportedEnemyAgents.Add(spottedAgent);
            spottedAgent.Died += OnEnemyAgentDied;
            EliminationTask newFightingTask = new(spottedAgent.Transform, TaskPriorities.High, spottedAgent, 5);
            _taskManager.RegisterNewTask(newFightingTask);
        }

        private void OnEnemyAgentDied(Humanoid deadAgent) 
        {
            deadAgent.Died -= OnEnemyAgentDied;
            _reportedEnemyAgents.Remove(deadAgent);
        }
    }

    public struct PassApplication 
    {
        public Humanoid Humanoid;
        public UndergroundEntrance UndergroundEntrance;
        public bool IsApproved;
        public UniTaskCompletionSource<bool> CompletionSource;
    }
}
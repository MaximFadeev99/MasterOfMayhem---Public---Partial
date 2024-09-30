using Cysharp.Threading.Tasks;
using MasterOfMayhem.Humanoids.AbstractHumanoid;
using MasterOfMayhem.PlaceableObjects.Beds;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfMayhem.Base
{
    public class HumanResourceManager 
    {
        private readonly List<Humanoid> _allMinions = new();
        private readonly List<Bed> _allBeds = new();
        private readonly Dictionary<Humanoid, Bed> _minionBedDictionary = new();

        private bool _isWorking = false;

        public IReadOnlyList<Humanoid> AllMinions => _allMinions;

        public Action<Humanoid[]> MinionsAdded;
        public Action<Humanoid> MinionRemoved;

        public HumanResourceManager() 
        {
            _isWorking = true;
            _ = AssignBedsToMinions();
        }

        public void RegisterNewMinions(params Humanoid[] newMinions) 
        {
            _allMinions.AddRange(newMinions);

            foreach (Humanoid minion in newMinions) 
            {
                _minionBedDictionary.Add(minion, null);
                minion.Died += RemoveMinion;
            }

            MinionsAdded?.Invoke(newMinions);
        }

        public void InformOfNewBeds(IReadOnlyList<Bed> newlyConstructedBeds)
        {
            foreach (Bed bed in newlyConstructedBeds)
            {
                _allBeds.Add(bed);
                bed.Demolished += OnBedDemolished;
            }
        }

        public void OnDestroy()
        {
            _isWorking = false;
        }

        private async UniTaskVoid AssignBedsToMinions() 
        {
            while (_isWorking) 
            {
                if (_allBeds.Count != 0 && _allMinions.Count != 0) 
                {
                    Humanoid minionWithoutBed = _allMinions
                        .FirstOrDefault(minion => minion.RecreationalAspect.SleepingPart.SleepingPlace.Key == null);

                    if (minionWithoutBed != null)
                    {
                        Bed vacantBed = _allBeds.FirstOrDefault(bed => bed.IsConstructed && bed.IsOccupied == false  &&
                                                                bed.IsSabotaged.Value == false);

                        if (vacantBed != null) 
                        {
                            int occupiedLevel = vacantBed.Occupy();
                            minionWithoutBed.RecreationalAspect.SleepingPart.AssignSleepingPlace(new(vacantBed, occupiedLevel));
                        }
                    }
                }

                await UniTask.WaitForSeconds(1.1f);
            }       
        }

        private void RemoveMinion(Humanoid minionToRemove) 
        {
            minionToRemove.Died -= RemoveMinion;
            _allMinions.Remove(minionToRemove);
            _minionBedDictionary.Remove(minionToRemove);
            minionToRemove.RecreationalAspect.SleepingPart.VacateSleepingPlace();
            MinionRemoved?.Invoke(minionToRemove);
        }

        private void OnBedDemolished(Bed demolishedBed) 
        {
            demolishedBed.Demolished -= OnBedDemolished;
            _allBeds.Remove(demolishedBed);
            IEnumerable<Humanoid> occupyingMinions = _minionBedDictionary
                .Where(kvPair => kvPair.Value == demolishedBed)
                .Select(kvPair => kvPair.Key);

            foreach (Humanoid minion in occupyingMinions) 
                minion.RecreationalAspect.SleepingPart.AssignSleepingPlace(new(null, 0));
        }
    }
}
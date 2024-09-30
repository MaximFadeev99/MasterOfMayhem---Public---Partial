using MasterOfMayhem.PlaceableObjects.Buildings;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterOfMayhem.Base
{
    public class WarehouseManager
    {
        private readonly List<ObjectSpawningBuilding> _allSpawningBuildings = new();

        public ObjectSpawningBuilding GetMostSuitableWarehouse(Vector3 applicantWorldPosition) 
        {
            if (_allSpawningBuildings == null || _allSpawningBuildings.Count == 0) 
                return null;

            if (_allSpawningBuildings.Count == 1 && _allSpawningBuildings[0].IsWorking) 
                return _allSpawningBuildings[0];    

            IEnumerable<ObjectSpawningBuilding> orderedBuildings = _allSpawningBuildings
                .Where(spawningBuilding => spawningBuilding.IsWorking)
                .OrderBy(spawningBuilding => spawningBuilding.OrderCount);

            if (orderedBuildings.Count() == 0) 
                return null;

            if (orderedBuildings.Count() == 1) 
                return orderedBuildings.First();

            IEnumerable<ObjectSpawningBuilding> buildingsWithSameOrderCount = orderedBuildings
                .TakeWhile(spawningBuilding => spawningBuilding.OrderCount == orderedBuildings.First().OrderCount);

            if (buildingsWithSameOrderCount.Count() == 1) 
                return buildingsWithSameOrderCount.First();

            return buildingsWithSameOrderCount
                    .OrderBy(spawningBuilding => Vector3.Distance(spawningBuilding.Transform.position, applicantWorldPosition))
                    .First();
        }

        public void InformOfNewSpawningBuildings(IReadOnlyList<ObjectSpawningBuilding> newlyAddedBuildings)
        {
            foreach (ObjectSpawningBuilding building in newlyAddedBuildings)
            {
                _allSpawningBuildings.Add(building);
                building.Demolished += OnSpawningBuildingDemolished;
                building.CheckIfCanBeDestroyed += CanSpawningBuildingBeDestroyed;
            }
        }

        public bool CanSpawningBuildingBeDestroyed()
        {
            return _allSpawningBuildings.Count > 1;
        }

        private void OnSpawningBuildingDemolished(ObjectSpawningBuilding demolishedSpawningBuilding)
        {
            demolishedSpawningBuilding.Demolished -= OnSpawningBuildingDemolished;
            demolishedSpawningBuilding.CheckIfCanBeDestroyed -= CanSpawningBuildingBeDestroyed;
            _allSpawningBuildings.Remove(demolishedSpawningBuilding);
        }
    }
}